// Models/DiagnosticRow.cs
using System.ComponentModel;
using System.Windows.Media;
using Autodesk.Revit.DB;

namespace TL60_AuditoriaUnificada.Models
{
    /// <summary>
    /// Modelo para una fila del diagnóstico (tabla de la ventana).
    /// Soporta tanto plugin Tablas como plugin COBie.
    /// </summary>
    public class DiagnosticRow : INotifyPropertyChanged
    {
        private bool _isChecked;

        public event PropertyChangedEventHandler PropertyChanged;

        public int NumeroFila { get; set; }
        public bool EsSeleccionable { get; set; }
        public ElementId ElementId { get; set; }
        public string IdMostrar { get; set; }

        /// <summary>
        /// Indica si la fila está marcada para corrección
        /// </summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }
        }

        /// <summary>
        /// Grupo para agrupar filas: "UNIDADES GLOBALES" o "TABLAS"
        /// Usado por: Plugin Tablas
        /// </summary>
        public string Grupo { get; set; }

        /// <summary>
        /// Grupo COBie: "FACILITY", "FLOOR", "SPACE", "TYPE", "COMPONENT"
        /// Usado por: Plugin COBie
        /// </summary>
        public string GrupoCOBie { get; set; }

        /// <summary>
        /// Código de ensamblaje (Assembly Code)
        /// Usado por: Plugin COBie
        /// </summary>
        public string AssemblyCode { get; set; }

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
    /// Estados posibles de un parámetro durante la auditoría
    ///
    /// Criterios unificados para todos los plugins:
    /// - Correcto: Información validada como correcta
    /// - Advertencia: Incompatibilidades que NO se pueden corregir automáticamente (requieren revisión manual)
    /// - Corregir: Incompatibilidades que SÍ se pueden corregir automáticamente con el plugin
    /// - Error: Errores de ejecución, lectura de datos, Assembly Codes no encontrados, valores correctos vacíos, etc.
    /// </summary>
    public enum EstadoParametro
    {
        Correcto,      // Verde - Información validada como correcta
        Advertencia,   // Amarillo - Incompatibilidades NO corregibles (requieren revisión manual)
        Corregir,      // Azul - Incompatibilidades SÍ corregibles automáticamente
        Error          // Rojo - Errores de ejecución, lectura, AC no encontrados, valores correctos vacíos
    }
}