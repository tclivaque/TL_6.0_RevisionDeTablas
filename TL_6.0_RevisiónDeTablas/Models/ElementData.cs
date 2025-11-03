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
        [cite: 107]
        public Element Element { get; set; }
        [cite: 108]
        public string Nombre { get; set; }
        [cite: 109]
        public string Categoria { get; set; }
        [cite: 110]
        public string Grupo { get; set; }
        [cite: 111]
        public string CodigoIdentificacion { get; set; }
        [cite: 112]
        public bool DatosCompletos { get; set; }
        [cite: 113]

        /// <summary>
        /// Clave: Nombre del parámetro ("Filtros")
        /// Valor: Valor CORREGIDO (string multi-línea)
        /// </summary>
        public Dictionary<string, string> ParametrosActualizar { get; set; }
        [cite: 114]

        /// <summary>
        /// (MODIFICADO)
        /// Clave: Nombre del parámetro ("Filtros")
        /// Valor: Valor ACTUAL (string multi-línea)
        /// </summary>
        public Dictionary<string, string> ParametrosActuales { get; set; }

        public List<string> ParametrosVacios { get; set; }
        [cite: 115]
        public Dictionary<string, string> ParametrosCorrectos { get; set; }
        [cite: 116]
        public List<string> Mensajes { get; set; }
        [cite: 117]

        public ElementData()
        {
            ParametrosActualizar = new Dictionary<string, string>();
            ParametrosActuales = new Dictionary<string, string>(); // Inicializado
            ParametrosVacios = new List<string>();
            ParametrosCorrectos = new Dictionary<string, string>();
            Mensajes = new List<string>();
            [cite_start] DatosCompletos = false; [cite: 118]
        }
    }
}