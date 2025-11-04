// Services/ScheduleUpdateWriter.cs (Anteriormente ScheduleFilterWriter.cs)
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using TL60_RevisionDeTablas.Models;

namespace TL60_RevisionDeTablas.Services
{
    /// <summary>
    /// Escribe las correcciones (Filtros, Contenido) en las tablas
    /// </summary>
    public class ScheduleUpdateWriter
    {
        private readonly Document _doc;

        public ScheduleUpdateWriter(Document doc)
        {
            _doc = doc;
        }

        public ProcessingResult UpdateSchedules(List<ElementData> elementosData)
        {
            var result = new ProcessingResult { Exitoso = false };
            int tablasCorregidas = 0;
            int filtrosCorregidos = 0;
            int contenidosCorregidos = 0;

            using (Transaction trans = new Transaction(_doc, "Corregir Auditoría de Tablas"))
            {
                try
                {
                    trans.Start();

                    foreach (var elementData in elementosData)
                    {
                        if (elementData.ElementId == null || elementData.ElementId == ElementId.InvalidElementId)
                            continue;

                        ViewSchedule view = _doc.GetElement(elementData.ElementId) as ViewSchedule;
                        if (view == null) continue;

                        ScheduleDefinition definition = view.Definition;
                        bool tablaModificada = false;

                        // --- 1. Corregir FILTROS ---
                        var filterAudit = elementData.AuditResults.FirstOrDefault(a => a.AuditType == "FILTRO");
                        if (filterAudit != null && filterAudit.Estado == EstadoParametro.Corregir)
                        {
                            if (WriteFilters(definition, elementData.FiltrosCorrectos, result.Errores, elementData.Nombre))
                            {
                                filtrosCorregidos++;
                                tablaModificada = true;
                            }
                        }

                        // --- 2. Corregir CONTENIDO (Itemize) ---
                        var contentAudit = elementData.AuditResults.FirstOrDefault(a => a.AuditType == "CONTENIDO");
                        if (contentAudit != null && contentAudit.Estado == EstadoParametro.Corregir)
                        {
                            if (!definition.IsItemized)
                            {
                                definition.IsItemized = true;
                                contenidosCorregidos++;
                                tablaModificada = true;
                            }
                        }

                        // --- 3. Corregir CANTIDAD DE COLUMNAS (No implementado) ---
                        // (La corrección de columnas no se implementa por complejidad)


                        if (tablaModificada)
                        {
                            tablasCorregidas++;
                        }
                    }

                    trans.Commit();
                    result.Exitoso = true;
                    result.Mensaje = $"Corrección completa.\n" +
                                     $"Tablas modificadas: {tablasCorregidas}\n" +
                                     $"Correcciones de Filtros: {filtrosCorregidos}\n" +
                                     $"Correcciones de Contenido: {contenidosCorregidos}";
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    result.Exitoso = false;
                    result.Mensaje = $"Error al escribir correcciones: {ex.Message}";
                    result.Errores.Add(ex.Message);
                }
            }
            return result;
        }

        /// <summary>
        /// Lógica de escritura de filtros (extraída)
        /// </summary>
        private bool WriteFilters(ScheduleDefinition definition, List<ScheduleFilterInfo> filtrosCorrectos, List<string> errores, string nombreTabla)
        {
            try
            {
                // 1. Limpiar filtros existentes
                definition.ClearFilters();

                // 2. Iterar y añadir los filtros correctos
                foreach (var filtroInfo in filtrosCorrectos)
                {
                    ScheduleField field = FindField(definition, filtroInfo.FieldName);
                    if (field == null)
                    {
                        errores.Add($"Tabla '{nombreTabla}' no tiene el campo '{filtroInfo.FieldName}' para filtrar.");
                        continue;
                    }

                    ScheduleFilter newFilter = CreateScheduleFilter(field.FieldId, filtroInfo, errores, nombreTabla);
                    if (newFilter != null)
                    {
                        definition.AddFilter(newFilter);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                errores.Add($"Error al escribir filtros en '{nombreTabla}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Crea un ScheduleFilter basado en el TIPO del valor
        /// </summary>
        private ScheduleFilter CreateScheduleFilter(ScheduleFieldId fieldId, ScheduleFilterInfo filtroInfo, List<string> errores, string nombreTabla)
        {
            if (filtroInfo.Value == null &&
                filtroInfo.FilterType != ScheduleFilterType.HasValue &&
                filtroInfo.FilterType != ScheduleFilterType.HasNoValue)
            {
                // Un valor nulo solo es válido para HasValue/HasNoValue
                // (Ejemplo: "METRADO" podría no tener valor si es 'HasNoValue')
                errores.Add($"Valor de filtro nulo no compatible para '{filtroInfo.FieldName}' en tabla '{nombreTabla}'.");
                return null;
            }

            switch (filtroInfo.Value)
            {
                case string s:
                    return new ScheduleFilter(fieldId, filtroInfo.FilterType, s);
                case double d:
                    return new ScheduleFilter(fieldId, filtroInfo.FilterType, d);
                case int i:
                    return new ScheduleFilter(fieldId, filtroInfo.FilterType, (double)i); // Revit 2021 usa Double para Ints
                case ElementId id:
                    return new ScheduleFilter(fieldId, filtroInfo.FilterType, id);
                case null:
                    // Esto es para HasValue o HasNoValue
                    return new ScheduleFilter(fieldId, filtroInfo.FilterType);
                default:
                    errores.Add($"Valor de filtro no compatible ({filtroInfo.Value.GetType()}) para '{filtroInfo.FieldName}' en tabla '{nombreTabla}'.");
                    return null;
            }
        }

        private ScheduleField FindField(ScheduleDefinition definition, string fieldName)
        {
            for (int i = 0; i < definition.GetFieldCount(); i++)
            {
                var field = definition.GetField(i);
                if (field.GetName().Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    return field;
                }
            }
            return null;
        }
    }
}