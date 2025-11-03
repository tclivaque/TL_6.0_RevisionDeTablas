using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TL60_RevisionDeTablas.Models;

namespace TL60_RevisionDeTablas.Services
{
    /// <summary>
    [cite_start]/// Maneja la escritura de filtros usando ExternalEvent [cite: 67]
                /// </summary>
    public class ScheduleWriterHandler : IExternalEventHandler
    {
        private Document _doc;
        private List<ElementData> _elementosData;
        private ProcessingResult _result;
        private ManualResetEvent _resetEvent;

        [cite_start] public ProcessingResult Result => _result; [cite: 68]

        public void SetData(
            Document doc,
            List<ElementData> elementosData,
            ManualResetEvent resetEvent)
        {
            _doc = doc;
            _elementosData = elementosData;
            _resetEvent = resetEvent;
            [cite_start] _result = null; [cite: 70]
        }

        /// <summary>
        [cite_start]/// Se ejecuta en el contexto de Revit [cite: 71]
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
                    [cite_start]Mensaje = $"Error al escribir filtros: {ex.Message}"[cite: 73]
                };
                [cite_start] _result.Errores.Add(ex.Message); [cite: 74]
            }
            finally
            {
                [cite_start] _resetEvent?.Set(); [cite: 75]
            }
        }

        public string GetName()
        {
            [cite_start] return "ScheduleWriterHandler"; [cite: 76]
        }
    }

    /// <summary>
    [cite_start]/// Clase auxiliar para manejar la escritura asíncrona [cite: 76]
                /// </summary>
    public class ScheduleWriterAsync
    {
        private ExternalEvent _externalEvent;
        private ScheduleWriterHandler _handler;

        public ScheduleWriterAsync()
        {
            _handler = new ScheduleWriterHandler();
            [cite_start] _externalEvent = ExternalEvent.Create(_handler); [cite: 78]
        }

        /// <summary>
        /// Escribe filtros de forma asíncrona
        /// </summary>
        public ProcessingResult WriteFiltersAsync(
            Document doc,
            List<ElementData> elementosData)
        {
            [cite_start] using (var resetEvent = new ManualResetEvent(false)) [cite: 79]
            {
                _handler.SetData(doc, elementosData, resetEvent);
                [cite_start] _externalEvent.Raise(); [cite: 80]
                
                bool completed = resetEvent.WaitOne(TimeSpan.FromSeconds(100)); // Timeout 100s [cite: 81]

                if (!completed)
                {
                    return new ProcessingResult { Exitoso = false, Mensaje = "Timeout: La escritura tardó más de 100 segundos." }; [cite: 83]
                }

                return _handler.Result ?? new ProcessingResult { Exitoso = false, Mensaje = "Error: No se pudo obtener el resultado." }; [cite: 85]
            }
        }
    }
}