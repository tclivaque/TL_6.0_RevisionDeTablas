using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TL60_RevisionDeTablas.Models;

namespace TL60_RevisionDeTablas.Services
{
    /// <summary>
    /// Maneja la escritura de filtros usando ExternalEvent
    /// </summary>
    public class ScheduleWriterHandler : IExternalEventHandler
    {
        private Document _doc;
        private List<ElementData> _elementosData;
        private ProcessingResult _result;
        private ManualResetEvent _resetEvent;

        public ProcessingResult Result => _result;

        public void SetData(
            Document doc,
            List<ElementData> elementosData,
            ManualResetEvent resetEvent)
        {
            _doc = doc;
            _elementosData = elementosData;
            _resetEvent = resetEvent;
            _result = null;
        }

        /// <summary>
        /// Se ejecuta en el contexto de Revit
        /// </summary>
        public void Execute(UIApplication app)
        {
            try
            {
                // Llama a nuestro nuevo Writer
                var writer = new ScheduleFilterWriter(_doc);
                _result = writer.WriteFilters(_elementosData);
            }
            catch (Exception ex)
            {
                _result = new ProcessingResult
                {
                    Exitoso = false,
                    Mensaje = $"Error al escribir filtros: {ex.Message}"
                };
                _result.Errores.Add(ex.Message);
            }
            finally
            {
                _resetEvent?.Set();
            }
        }

        public string GetName()
        {
            return "ScheduleWriterHandler";
        }
    }

    /// <summary>
    /// Clase auxiliar para manejar la escritura asíncrona
    /// </summary>
    public class ScheduleWriterAsync
    {
        private ExternalEvent _externalEvent;
        private ScheduleWriterHandler _handler;

        public ScheduleWriterAsync()
        {
            _handler = new ScheduleWriterHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        /// <summary>
        /// Escribe filtros de forma asíncrona
        /// </summary>
        public ProcessingResult WriteFiltersAsync(
            Document doc,
            List<ElementData> elementosData)
        {
            using (var resetEvent = new ManualResetEvent(false))
            {
                _handler.SetData(doc, elementosData, resetEvent);
                _externalEvent.Raise();

                bool completed = resetEvent.WaitOne(TimeSpan.FromSeconds(100)); // Timeout 100s

                if (!completed)
                {
                    return new ProcessingResult { Exitoso = false, Mensaje = "Timeout: La escritura tardó más de 100 segundos." };
                }

                return _handler.Result ?? new ProcessingResult { Exitoso = false, Mensaje = "Error: No se pudo obtener el resultado." };
            }
        }
    }
}