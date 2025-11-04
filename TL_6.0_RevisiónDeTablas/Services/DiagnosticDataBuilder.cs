// Services/DiagnosticDataBuilder.cs
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
                if (elementData.AuditResults == null) continue;

                string idMostrar = elementData.ElementId?.IntegerValue.ToString() ?? "N/A";

                // (NUEVA LÓGICA) Itera sobre cada resultado de auditoría y crea una fila
                foreach (var auditItem in elementData.AuditResults)
                {
                    var row = new DiagnosticRow
                    {
                        // Datos del Elemento
                        ElementId = elementData.ElementId,
                        IdMostrar = idMostrar,
                        CodigoIdentificacion = elementData.CodigoIdentificacion,
                        Descripcion = elementData.Nombre,

                        // Datos de la Auditoría Específica
                        NombreParametro = auditItem.AuditType.ToUpper(), // "FILTRO", "CONTENIDO", etc.
                        ValorActual = auditItem.ValorActual,
                        ValorCorregido = auditItem.ValorCorrecto,
                        Estado = auditItem.Estado,
                        Mensaje = auditItem.Mensaje
                    };
                    rows.Add(row);
                }
            }

            // Ordenar: CORREGIR → VACÍO → ERROR → CORRECTO
            rows = rows.OrderBy(r => GetEstadoOrder(r.Estado))
                      .ThenBy(r => r.ElementId?.IntegerValue ?? 0)
                      .ThenBy(r => r.NombreParametro) // Ordenar por tipo de auditoría
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
                case EstadoParametro.Error: return 3;
                case EstadoParametro.Correcto: return 4;
                default: return 5;
            }
        }
    }
}