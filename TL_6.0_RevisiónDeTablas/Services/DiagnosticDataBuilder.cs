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

                // Parámetros a corregir (AZUL)
                foreach (var paramActualizar in elementData.ParametrosActualizar)
                {
                    var row = new DiagnosticRow
                    {
                        ElementId = elementData.ElementId,
                        IdMostrar = idMostrar,
                        CodigoIdentificacion = elementData.CodigoIdentificacion,
                        Descripcion = elementData.Nombre,
                        NombreParametro = paramActualizar.Key, // "Filtros"

                        // (MODIFICADO) Leer de nuestro nuevo diccionario
                        ValorActual = elementData.ParametrosActuales.ContainsKey(paramActualizar.Key)
                                        ? elementData.ParametrosActuales[paramActualizar.Key]
                                        : "(Error al leer valor actual)",

                        ValorCorregido = paramActualizar.Value,
                        Estado = EstadoParametro.Corregir,
                        Mensaje = string.Join("\n", elementData.Mensajes) // Mostrar mensajes
                    };
                    rows.Add(row);
                }

                // Parámetros correctos (VERDE)
                foreach (var paramCorrecto in elementData.ParametrosCorrectos)
                {
                    var row = new DiagnosticRow
                    {
                        ElementId = elementData.ElementId,
                        IdMostrar = idMostrar,
                        CodigoIdentificacion = elementData.CodigoIdentificacion,
                        Descripcion = elementData.Nombre,
                        NombreParametro = paramCorrecto.Key,
                        ValorActual = paramCorrecto.Value,
                        ValorCorregido = paramCorrecto.Value,
                        Estado = EstadoParametro.Correcto
                    };
                    rows.Add(row);
                }
            }

            // Ordenar: CORREGIR → CORRECTO
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
                case EstadoParametro.Error: return 3;
                case EstadoParametro.Correcto: return 4;
                default: return 5;
            }
        }

        // (El método GetActualValue de la plantilla se eliminó porque ya no es necesario)
    }
}