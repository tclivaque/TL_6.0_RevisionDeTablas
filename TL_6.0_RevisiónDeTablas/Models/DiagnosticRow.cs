using System.Windows.Media;
using Autodesk.Revit.DB;

namespace TL60_RevisionDeTablas.Models
{
    /// <summary>
    /// Modelo para una fila del diagnóstico (tabla de la ventana)
    /// </summary>
    public class DiagnosticRow
    {
        public int NumeroFila { get; set; }
        public bool EsSeleccionable { get; set; }
        public ElementId ElementId { get; set; }
        public string IdMostrar { get; set; }
        public string Grupo { get; set; }
        public string CodigoIdentificacion { get; set; }
        public string Descripcion { get; set; }
        public string NombreParametro { get; set; }
        public string ValorActual { get; set; }
        public string ValorCorregido { get; set; }
        public EstadoParametro Estado { get; set; }
        public string Mensaje { get; set; }
        public SolidColorBrush BackgroundColor { get; set; }
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