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
            [cite_start] _doc = doc; [cite: 207]
        }

        public ProcessingResult WriteFilters(List<ElementData> elementosData)
        {
            [cite_start] var result = new ProcessingResult { Exitoso = false }; [cite: 209]
            int tablasCorregidas = 0;

            using (Transaction trans = new Transaction(_doc, "Corregir Filtros de Tablas"))
            {
                try
                {
                    [cite_start] trans.Start(); [cite: 211]

                    foreach (var elementData in elementosData)
                    {
                        [cite_start] if (elementData.ElementId == null || elementData.ElementId == ElementId.InvalidElementId) [cite: 212]
                            continue;

                        [cite_start] ViewSchedule view = _doc.GetElement(elementData.ElementId) as ViewSchedule; [cite: 213]
                        if (view == null) continue;

                        ScheduleDefinition definition = view.Definition;

                        // 1. Limpiar filtros existentes
                        definition.ClearFilters();

                        // 2. Obtener el Assembly Code y Empresa
                        string assemblyCode = elementData.CodigoIdentificacion;
                        string empresa = "RNG"; // Definido en la lógica

                        // 3. Encontrar los campos (Fields)
                        ScheduleField codeField = FindField(definition, "Assembly Code");
                        ScheduleField empresaField = FindField(definition, "EMPRESA");

                        if (codeField == null || empresaField == null)
                        {
                            result.Errores.Add($"Tabla '{elementData.Nombre}' no tiene los campos 'Assembly Code' o 'EMPRESA' para filtrar.");
                            continue;
                        }

                        // 4. Crear y añadir nuevos filtros EN ORDEN
                        ScheduleFilter codeFilter = new ScheduleFilter(codeField.FieldId, ScheduleFilterType.Equal, assemblyCode);
                        ScheduleFilter empresaFilter = new ScheduleFilter(empresaField.FieldId, ScheduleFilterType.Equal, empresa);

                        definition.AddFilter(codeFilter);
                        definition.AddFilter(empresaFilter);

                        tablasCorregidas++;
                    }

                    [cite_start] trans.Commit(); [cite: 223]
                    result.Exitoso = true;
                    [cite_start] result.Mensaje = $"Se corrigieron los filtros de {tablasCorregidas} tablas exitosamente."; [cite: 225]
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    result.Exitoso = false;
                    [cite_start] result.Mensaje = $"Error al escribir filtros: {ex.Message}"; [cite: 227]
                    result.Errores.Add(ex.Message);
                }
            }
            [cite_start] return result; [cite: 228]
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