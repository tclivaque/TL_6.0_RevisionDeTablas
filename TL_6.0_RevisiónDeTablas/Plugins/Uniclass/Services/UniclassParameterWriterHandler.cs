using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TL60_AuditoriaUnificada.Models;

namespace TL60_AuditoriaUnificada.Plugins.Uniclass.Services
{
    /// <summary>
    /// Handler para ejecutar correcciones de parámetros Uniclass desde eventos UI
    /// </summary>
    public class UniclassParameterWriterHandler : IExternalEventHandler
    {
        private Document _doc;
        private List<ElementData> _elementosData;
        private ProcessingResult _result;
        private ManualResetEvent _resetEvent;

        public ProcessingResult Result => _result;

        /// <summary>
        /// Configura los datos para la ejecución del handler
        /// </summary>
        public void SetData(Document doc, List<ElementData> elementosData, ManualResetEvent resetEvent)
        {
            _doc = doc;
            _elementosData = elementosData;
            _resetEvent = resetEvent;
            _result = null;
        }

        /// <summary>
        /// Ejecuta la corrección de parámetros dentro del contexto de Revit API
        /// </summary>
        public void Execute(UIApplication app)
        {
            try
            {
                var writer = new UniclassParameterWriter(_doc);
                _result = writer.UpdateParameters(_elementosData);
            }
            catch (Exception ex)
            {
                _result = new ProcessingResult
                {
                    Exitoso = false,
                    Mensaje = $"Error al corregir parámetros Uniclass: {ex.Message}"
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
            return "UniclassParameterWriterHandler";
        }
    }

    /// <summary>
    /// Wrapper para ejecutar correcciones de parámetros Uniclass de manera asíncrona
    /// </summary>
    public class UniclassParameterWriterAsync
    {
        private ExternalEvent _externalEvent;
        private UniclassParameterWriterHandler _handler;

        public UniclassParameterWriterAsync()
        {
            _handler = new UniclassParameterWriterHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        /// <summary>
        /// Ejecuta la corrección de parámetros de manera asíncrona
        /// </summary>
        public ProcessingResult UpdateParametersAsync(Document doc, List<ElementData> elementosData)
        {
            using (var resetEvent = new ManualResetEvent(false))
            {
                _handler.SetData(doc, elementosData, resetEvent);
                _externalEvent.Raise();

                // Esperar hasta 15 minutos
                bool completed = resetEvent.WaitOne(TimeSpan.FromSeconds(900));

                if (!completed)
                {
                    return new ProcessingResult
                    {
                        Exitoso = false,
                        Mensaje = "Timeout: La corrección tardó más de 15 minutos."
                    };
                }

                return _handler.Result ?? new ProcessingResult
                {
                    Exitoso = false,
                    Mensaje = "Error: No se pudo obtener resultado."
                };
            }
        }
    }
}
