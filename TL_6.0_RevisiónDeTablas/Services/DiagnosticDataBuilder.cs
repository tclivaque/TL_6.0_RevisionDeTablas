using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using TL60_RevisionDeTablas.Models;

namespace TL60_RevisionDeTablas.Services
{
    /// <summary>
    [cite_start]/// Construye los datos para la ventana de diagnóstico [cite: 257]
    /// </summary>
    public class DiagnosticDataBuilder
    {
        public List<DiagnosticRow> BuildDiagnosticRows(List<ElementData> elementosData)
        {
            [cite_start]var rows = new List<DiagnosticRow>(); [cite: 258]

            foreach (var elementData in elementosData)
            {
                string idMostrar = elementData.ElementId?.IntegerValue.ToString() ?? "N/A"; [cite: 259]

                [cite_start]// Parámetros a corregir (AZUL) [cite: 259]
                foreach (var paramActualizar in elementData.ParametrosActualizar)
                {
                    var row = new DiagnosticRow
                    {
                        [cite_start]ElementId = elementData.ElementId, [cite: 260]
                        IdMostrar = idMostrar,
                        [cite_start]CodigoIdentificacion = elementData.CodigoIdentificacion, [cite: 261]
                        Descripcion = elementData.Nombre,
                        NombreParametro = paramActualizar.Key, // "Filtros"
                        
                        // (MODIFICADO) Leer de nuestro nuevo diccionario
                        ValorActual = elementData.ParametrosActuales.ContainsKey(paramActualizar.Key) 
                                        ? elementData.ParametrosActuales[paramActualizar.Key] 
                                        : "(Error al leer valor actual)",

                        ValorCorregido = paramActualizar.Value,
                        [cite_start]Estado = EstadoParametro.Corregir, [cite: 262]
                        Mensaje = string.Join("\n", elementData.Mensajes) // Mostrar mensajes
                    };
                    rows.Add(row);
                }

                [cite_start]// Parámetros correctos (VERDE) [cite: 267]
                foreach (var paramCorrecto in elementData.ParametrosCorrectos)
                {
                    var row = new DiagnosticRow
                    {
                        [cite_start]ElementId = elementData.ElementId, [cite: 268]
                        IdMostrar = idMostrar,
                        [cite_start]CodigoIdentificacion = elementData.CodigoIdentificacion, [cite: 269]
                        Descripcion = elementData.Nombre,
                        NombreParametro = paramCorrecto.Key,
                        ValorActual = paramCorrecto.Value,
                        [cite_start]ValorCorregido = paramCorrecto.Value, [cite: 270]
                        [cite_start]Estado = EstadoParametro.Correcto [cite: 271]
                    };
                    rows.Add(row);
                }
            }

            [cite_start]// Ordenar: CORREGIR → CORRECTO [cite: 275]
            rows = rows.OrderBy(r => GetEstadoOrder(r.Estado))
                      .ThenBy(r => r.ElementId?.IntegerValue ?? 0)
                      .ToList();
            
            for (int i = 0; i < rows.Count; i++)
            {
                [cite_start]rows[i].NumeroFila = i + 1; [cite: 277]
            }

            return rows;
        }

        private int GetEstadoOrder(EstadoParametro estado)
        {
            switch (estado)
            {
                [cite_start]case EstadoParametro.Corregir: return 1; [cite: 279]
                case EstadoParametro.Vacio: return 2;
                case EstadoParametro.Error: return 3;
                case EstadoParametro.Correcto: return 4;
                default: return 5;
            }
        }
        
        [cite_start]// (El método GetActualValue de la plantilla [cite: 280] se eliminó porque ya no es necesario)
    }
}using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using TL60_RevisionDeTablas.Models;

namespace TL60_RevisionDeTablas.Services
{
    /// <summary>
    [cite_start]/// Construye los datos para la ventana de diagnóstico [cite: 257]
    /// </summary>
    public class DiagnosticDataBuilder
    {
        public List<DiagnosticRow> BuildDiagnosticRows(List<ElementData> elementosData)
        {
            [cite_start]var rows = new List<DiagnosticRow>(); [cite: 258]

            foreach (var elementData in elementosData)
            {
                string idMostrar = elementData.ElementId?.IntegerValue.ToString() ?? "N/A"; [cite: 259]

                [cite_start]// Parámetros a corregir (AZUL) [cite: 259]
                foreach (var paramActualizar in elementData.ParametrosActualizar)
                {
                    var row = new DiagnosticRow
                    {
                        [cite_start]ElementId = elementData.ElementId, [cite: 260]
                        IdMostrar = idMostrar,
                        [cite_start]CodigoIdentificacion = elementData.CodigoIdentificacion, [cite: 261]
                        Descripcion = elementData.Nombre,
                        NombreParametro = paramActualizar.Key, // "Filtros"
                        
                        // (MODIFICADO) Leer de nuestro nuevo diccionario
                        ValorActual = elementData.ParametrosActuales.ContainsKey(paramActualizar.Key) 
                                        ? elementData.ParametrosActuales[paramActualizar.Key] 
                                        : "(Error al leer valor actual)",

                        ValorCorregido = paramActualizar.Value,
                        [cite_start]Estado = EstadoParametro.Corregir, [cite: 262]
                        Mensaje = string.Join("\n", elementData.Mensajes) // Mostrar mensajes
                    };
                    rows.Add(row);
                }

                [cite_start]// Parámetros correctos (VERDE) [cite: 267]
                foreach (var paramCorrecto in elementData.ParametrosCorrectos)
                {
                    var row = new DiagnosticRow
                    {
                        [cite_start]ElementId = elementData.ElementId, [cite: 268]
                        IdMostrar = idMostrar,
                        [cite_start]CodigoIdentificacion = elementData.CodigoIdentificacion, [cite: 269]
                        Descripcion = elementData.Nombre,
                        NombreParametro = paramCorrecto.Key,
                        ValorActual = paramCorrecto.Value,
                        [cite_start]ValorCorregido = paramCorrecto.Value, [cite: 270]
                        [cite_start]Estado = EstadoParametro.Correcto [cite: 271]
                    };
                    rows.Add(row);
                }
            }

            [cite_start]// Ordenar: CORREGIR → CORRECTO [cite: 275]
            rows = rows.OrderBy(r => GetEstadoOrder(r.Estado))
                      .ThenBy(r => r.ElementId?.IntegerValue ?? 0)
                      .ToList();
            
            for (int i = 0; i < rows.Count; i++)
            {
                [cite_start]rows[i].NumeroFila = i + 1; [cite: 277]
            }

            return rows;
        }

        private int GetEstadoOrder(EstadoParametro estado)
        {
            switch (estado)
            {
                [cite_start]case EstadoParametro.Corregir: return 1; [cite: 279]
                case EstadoParametro.Vacio: return 2;
                case EstadoParametro.Error: return 3;
                case EstadoParametro.Correcto: return 4;
                default: return 5;
            }
        }
        
        [cite_start]// (El método GetActualValue de la plantilla [cite: 280] se eliminó porque ya no es necesario)
    }
}