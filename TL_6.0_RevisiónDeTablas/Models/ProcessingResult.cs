using System.Collections.Generic;
using System.Linq;

namespace TL60_RevisionDeTablas.Models
{
    public class ProcessingResult
    {
        public List<ElementData> ElementosProcesados { get; set; }
        public List<string> Errores { get; set; }
        public bool Exitoso { get; set; }
        public string Mensaje { get; set; }

        // (CORREGIDO) Apunta a la nueva propiedad 'DatosCompletos'
        public int CantidadCorrectos => ElementosProcesados.Count(e => e.DatosCompletos);

        // (CORREGIDO) Apunta a la nueva propiedad 'DatosCompletos'
        public int CantidadACorregir => ElementosProcesados.Count(e => !e.DatosCompletos);

        // (CORREGIDO) Esta propiedad ya no tiene sentido en la nueva lógica
        public int CantidadConVacios => 0;

        public ProcessingResult()
        {
            ElementosProcesados = new List<ElementData>();
            Errores = new List<string>();
            Exitoso = false;
            Mensaje = string.Empty;
        }

        public string GenerarResumen()
        {
            var resumen = $"RESUMEN:\n\n";
            resumen += $"✅ Correctos: {CantidadCorrectos}\n";
            resumen += $"🔧 A corregir: {CantidadACorregir}\n";
            // resumen += $"⚠ Con vacíos: {CantidadConVacios}\n"; // Eliminado
            if (Errores.Count > 0)
            {
                resumen += $"\n❌ Errores: {Errores.Count}\n";
            }
            return resumen;
        }
    }
}