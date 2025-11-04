using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using TL60_RevisionDeTablas.Models;

namespace TL60_RevisionDeTablas.Services
{
    /// <summary>
    /// Escribe los filtros corregidos en las tablas
    /// </summary>
    public class ScheduleFilterWriter
    {
        private readonly Document _doc;

        public ScheduleFilterWriter(Document doc)
        {
            _doc = doc;
        }

        public ProcessingResult WriteFilters(List<ElementData> elementosData)
        {
            var result = new ProcessingResult { Exitoso = false };
            int tablasCorregidas = 0;

            using (Transaction trans = new Transaction(_doc, "Corregir Filtros de Tablas"))
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

                        // 1. Limpiar filtros existentes
                        definition.ClearFilters();

                        // 2. Iterar y añadir los filtros correctos
                        foreach (var filtroInfo in elementData.FiltrosCorrectos)
                        {
                            ScheduleField field = FindField(definition, filtroInfo.FieldName);
                            if (field == null)
                            {
                                result.Errores.Add($"Tabla '{elementData.Nombre}' no tiene el campo '{filtroInfo.FieldName}' para filtrar.");
                                continue;
                            }

                            // (¡¡¡CORREGIDO!!!)
                            // Crear el filtro basado en el TIPO del valor
                            ScheduleFilter newFilter;
                            if (filtroInfo.Value is string s)
                            {
                                newFilter = new ScheduleFilter(field.FieldId, filtroInfo.FilterType, s);
                            }
                            else if (filtroInfo.Value is double d)
                            {
                                newFilter = new ScheduleFilter(field.FieldId, filtroInfo.FilterType, d);
                            }
                            else if (filtroInfo.Value is int i)
                            {
                                // API 2021 no tiene constructor de Int, usar Double
                                newFilter = new ScheduleFilter(field.FieldId, filtroInfo.FilterType, (double)i);
                            }
                            else if (filtroInfo.Value is ElementId id)
                            {
                                // (CORREGIDO) Esta es la sobrecarga correcta para ElementId
                                newFilter = new ScheduleFilter(field.FieldId, filtroInfo.FilterType, id);
                            }
                            else
                            {
                                result.Errores.Add($"Valor de filtro no compatible para '{filtroInfo.FieldName}' en tabla '{elementData.Nombre}'.");
                                continue;
                            }

                            definition.AddFilter(newFilter);
                        }

                        tablasCorregidas++;
                    }

                    trans.Commit();
                    result.Exitoso = true;
                    result.Mensaje = $"Se corrigieron los filtros de {tablasCorregidas} tablas exitosamente.";
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    result.Exitoso = false;
                    result.Mensaje = $"Error al escribir filtros: {ex.Message}";
                    result.Errores.Add(ex.Message);
                }
            }
            return result;
        }

        /// <summary>
        /// Busca un ScheduleField en la definición por su nombre
        /// </summary>
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