using System.Collections.Generic;
using System.Linq;

namespace TL60_RevisionDeTablas.Models
{
    [cite_start]// (Pegar el código EXACTO de ProcessingResult.cs de la plantilla )

    public class ProcessingResult
    {
        public List<ElementData> ElementosProcesados { get; set; }
        [cite: 135]
        public List<string> Errores { get; set; }
        [cite: 136]
        public bool Exitoso { get; set; }
        [cite: 137]
        public string Mensaje { get; set; }
        [cite: 138]

        [cite_start] public int CantidadCorrectos => ElementosProcesados.Count(e => e.DatosCompletos); [cite: 139]
        [cite_start] public int CantidadConVacios => ElementosProcesados.Count(e => e.ParametrosVacios.Count > 0); [cite: 141]
        [cite_start] public int CantidadACorregir => ElementosProcesados.Count(e => e.ParametrosActualizar.Count > 0); [cite: 143]

        public ProcessingResult()
        {
            ElementosProcesados = new List<ElementData>();
            Errores = new List<string>();
            Exitoso = false;
            [cite_start] Mensaje = string.Empty; [cite: 145]
        }

        public string GenerarResumen()
        {
            var resumen = $"RESUMEN:\n\n";
            [cite_start] resumen += $"✅ Correctos: {CantidadCorrectos}\n"; [cite: 147]
            resumen += $"🔧 A corregir: {CantidadACorregir}\n";
            resumen += $"⚠ Con vacíos: {CantidadConVacios}\n";
            if (Errores.Count > 0)
            {
                [cite_start] resumen += $"\n❌ Errores: {Errores.Count}\n"; [cite: 148]
            }
            return resumen;
        }
    }
}