using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using TL60_RevisionDeTablas.Models;

namespace TL60_RevisionDeTablas.Services
{
    /// <summary>
    /// Construye los datos para la ventana de diagnóstico
    /// </summary>
    public class DiagnosticDataBuilder
    {
        public List<DiagnosticRow> BuildDiagnosticRows(List<ElementData> elementosData)
        {
            var rows = new List<DiagnosticRow>();

            foreach (var elementData in elementosData)
            {
                string idMostrar = elementData.ElementId?.IntegerValue.ToString() ?? "N/A";
                var row = new DiagnosticRow
                {
                    ElementId = elementData.ElementId,
                    IdMostrar = idMostrar,
                    CodigoIdentificacion = elementData.CodigoIdentificacion,
                    Descripcion = elementData.Nombre,
                    NombreParametro = "Filtros", // Siempre será "Filtros"
                    ValorActual = elementData.FiltrosActualesString,
                    ValorCorregido = elementData.FiltrosCorrectosString,
                    Mensaje = string.Join("\n", elementData.Mensajes)
                };

                // Asignar estado
                if (elementData.DatosCompletos)
                {
                    row.Estado = EstadoParametro.Correcto;
                }
                else if (elementData.Mensajes.Count > 0)
                {
                    row.Estado = EstadoParametro.Corregir; // Azul (o Rojo si prefieres)
                }
                else
                {
                    row.Estado = EstadoParametro.Vacio; // Naranja (si no hay mensaje)
                }

                rows.Add(row);
            }

            // Ordenar: CORREGIR → VACÍO → CORRECTO
            rows = rows.OrderBy(r => GetEstadoOrder(r.Estado))
                      .ThenBy(r => r.ElementId?.IntegerValue ?? 0)
                      .ToList();

            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].NumeroFila = i + 1;
            }

            return rows;
        }

        private int GetEstadoOrder(EstadoParametro estado)
        {
            switch (estado)
            {
                case EstadoParametro.Corregir: return 1;
                case EstadoParametro.Vacio: return 2;
                case EstadoParametro.Error: return 3; // Puedes usar Error si quieres
                case EstadoParametro.Correcto: return 4;
                default: return 5;
            }
        }
    }
}