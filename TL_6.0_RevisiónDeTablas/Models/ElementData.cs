using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace TL60_RevisionDeTablas.Models
{
    /// <summary>
    /// Contiene los datos de un elemento procesado
    /// </summary>
    public class ElementData
    {
        public ElementId ElementId { get; set; }
        public Element Element { get; set; }
        public string Nombre { get; set; }
        public string Categoria { get; set; }
        public string Grupo { get; set; }
        public string CodigoIdentificacion { get; set; }
        public bool DatosCompletos { get; set; }

        /// <summary>
        /// (NUEVO) Almacena la lista de filtros que se deben ESCRIBIR
        /// </summary>
        public List<ScheduleFilterInfo> FiltrosCorrectos { get; set; }

        /// <summary>
        /// (NUEVO) Almacena el valor actual de los filtros como string
        /// </summary>
        public string FiltrosActualesString { get; set; }

        /// <summary>
        /// (NUEVO) Almacena el valor correcto de los filtros como string
        /// </summary>
        public string FiltrosCorrectosString { get; set; }

        public List<string> Mensajes { get; set; }

        // --- Propiedades antiguas (ya no se usan) ---
        // public Dictionary<string, string> ParametrosActualizar { get; set; }
        // public Dictionary<string, string> ParametrosActuales { get; set; }
        // public List<string> ParametrosVacios { get; set; }
        // public Dictionary<string, string> ParametrosCorrectos { get; set; }


        public ElementData()
        {
            FiltrosCorrectos = new List<ScheduleFilterInfo>();
            Mensajes = new List<string>();
            DatosCompletos = false;
        }
    }
}