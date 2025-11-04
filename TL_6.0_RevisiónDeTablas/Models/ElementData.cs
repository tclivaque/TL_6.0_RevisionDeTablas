// Models/ElementData.cs
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace TL60_RevisionDeTablas.Models
{
    /// <summary>
    /// Contiene los datos de un elemento (ViewSchedule) procesado
    /// y todos sus resultados de auditoría.
    /// </summary>
    public class ElementData
    {
        public ElementId ElementId { get; set; }
        public Element Element { get; set; }
        public string Nombre { get; set; }
        public string Categoria { get; set; }
        public string CodigoIdentificacion { get; set; }

        /// <summary>
        /// Se establece en True solo si TODAS las auditorías (Filtro, Contenido, Columnas) son correctas.
        /// </summary>
        public bool DatosCompletos { get; set; }

        /// <summary>
        /// (NUEVO) Almacena la lista de todos los resultados de la auditoría (Filtro, Contenido, etc.)
        /// </summary>
        public List<AuditItem> AuditResults { get; set; }

        /// <summary>
        /// Almacena la lista de filtros que se deben ESCRIBIR
        /// (solo se rellena si la auditoría de filtros falla)
        /// </summary>
        public List<ScheduleFilterInfo> FiltrosCorrectos { get; set; }

        /// <summary>
        /// Almacena el estado del check "Itemize"
        /// (solo se usa si la auditoría de contenido falla)
        /// </summary>
        public bool IsItemizedCorrect { get; set; }

        /// <summary>
        /// Almacena el trabajo de corrección de encabezados (Campo/Valor Correcto)
        /// </summary>
        public Dictionary<ScheduleField, string> HeadingsCorregidos { get; set; }
        public ElementData()
        {
            AuditResults = new List<AuditItem>();
            FiltrosCorrectos = new List<ScheduleFilterInfo>();
            DatosCompletos = false;
            IsItemizedCorrect = true; // Asumir correcto hasta que se demuestre lo contrario
        }
    }
}