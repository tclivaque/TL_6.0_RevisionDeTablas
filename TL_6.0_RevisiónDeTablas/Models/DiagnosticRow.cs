using System.Windows.Media;
using Autodesk.Revit.DB;

namespace TL60_RevisionDeTablas.Models
{
    [cite_start]// (Pegar el código EXACTO de DiagnosticRow.cs y EstadoParametro de la plantilla )

    /// <summary>
    /// Modelo para una fila del diagnóstico (tabla de la ventana)
    /// </summary>
    public class DiagnosticRow
    {
        public int NumeroFila { get; set; }
        [cite: 121]
        public bool EsSeleccionable { get; set; }
        [cite: 122]
        public ElementId ElementId { get; set; }
        [cite: 123]
        public string IdMostrar { get; set; }
        [cite: 124]
        public string Grupo { get; set; }
        [cite: 125]
        public string CodigoIdentificacion { get; set; }
        [cite: 126]
        public string Descripcion { get; set; }
        [cite: 127]
        public string NombreParametro { get; set; }
        [cite: 128]
        public string ValorActual { get; set; }
        [cite: 129]
        public string ValorCorregido { get; set; }
        [cite: 130]
        public EstadoParametro Estado { get; set; }
        [cite: 131]
        public string Mensaje { get; set; }
        [cite: 132]
        public SolidColorBrush BackgroundColor { get; set; }
        [cite: 133]
    }

    /// <summary>
    /// Estados posibles de un parámetro
    /// </summary>
    public enum EstadoParametro
    {
        Correcto,   // Verde
        Vacio,      // Naranja
        Corregir,   // Azul
        Error       // Rojo
    }
}