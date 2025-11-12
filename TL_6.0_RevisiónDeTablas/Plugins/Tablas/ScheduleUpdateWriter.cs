// Plugins/Tablas/ScheduleUpdateWriter.cs
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using TL60_RevisionDeTablas.Models;
using Autodesk.Revit.UI;

namespace TL60_RevisionDeTablas.Plugins.Tablas
{
    public class ScheduleUpdateWriter
    {
        private readonly Document _doc;
        private const string EMPRESA_PARAM_NAME = "EMPRESA";
        private const string EMPRESA_PARAM_VALUE = "RNG";


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
            int tablasReclasificadas = 0;
            int nombresCorregidos = 0;
            int linksIncluidos = 0;
            int columnasRenombradas = 0;
            int columnasOcultadas = 0;
            int parcialCorregidos = 0;
            int empresaCorregidos = 0;

            ParameterElement empresaParamElem = new FilteredElementCollector(_doc)
                .OfClass(typeof(ParameterElement))
                .Cast<ParameterElement>()
                .FirstOrDefault(e => e.Name.Equals(EMPRESA_PARAM_NAME, StringComparison.OrdinalIgnoreCase));

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

                        // --- 1. Corregir NOMBRE (VIEW NAME) ---
                        var nameAudit = elementData.AuditResults.FirstOrDefault(a => a.AuditType == "VIEW NAME" && a.IsCorrectable);
                        if (nameAudit != null && nameAudit.Tag is RenamingJobData jobData)
                        {
                            if (RenameAndReclassify(view, jobData, result.Errores))
                            {
                                nombresCorregidos++;
                                tablasReclasificadas++;
                                tablaModificada = true;
                            }
                        }

                        // --- 2. Corregir FILTROS ---
                        var filterAudit = elementData.AuditResults.FirstOrDefault(a => a.AuditType == "FILTROS" && a.IsCorrectable);
                        if (filterAudit != null && filterAudit.Tag is List<ScheduleFilterInfo> filtrosCorrectos)
                        {
                            if (WriteFilters(definition, filtrosCorrectos, result.Errores, elementData.Nombre))
                            {
                                filtrosCorregidos++;
                                tablaModificada = true;
                            }
                        }

                        // --- 3. Corregir CONTENIDO (Itemize) ---
                        var contentAudit = elementData.AuditResults.FirstOrDefault(a => a.AuditType == "CONTENIDO" && a.IsCorrectable);
                        if (contentAudit != null)
                        {
                            if (!definition.IsItemized)
                            {
                                definition.IsItemized = true;
                                contenidosCorregidos++;
                                tablaModificada = true;
                            }
                        }

                        // --- 4. Corregir INCLUDE LINKS ---
                        var linksAudit = elementData.AuditResults.FirstOrDefault(a => a.AuditType == "LINKS" && a.IsCorrectable);
                        if (linksAudit != null)
                        {
                            if (!definition.IncludeLinkedFiles)
                            {
                                definition.IncludeLinkedFiles = true;
                                linksIncluidos++;
                                tablaModificada = true;
                            }
                        }

                        // --- 5. Corregir COLUMNAS ---
                        var columnAudit = elementData.AuditResults.FirstOrDefault(a => a.AuditType == "COLUMNAS" && a.IsCorrectable);
                        if (columnAudit != null && columnAudit.Tag != null)
                        {
                            if (columnAudit.Tag is Dictionary<ScheduleField, string> headingsToFix)
                            {
                                if (WriteHeadings(headingsToFix, result.Errores, elementData.Nombre))
                                {
                                    columnasRenombradas += headingsToFix.Count;
                                    tablaModificada = true;
                                }
                            }
                            else if (columnAudit.Tag is List<ScheduleField> fieldsToHide)
                            {
                                if (HideColumns(fieldsToHide, result.Errores, elementData.Nombre))
                                {
                                    columnasOcultadas += fieldsToHide.Count;
                                    tablaModificada = true;
                                }
                            }
                        }


                        // ==========================================================
                        // ===== 6. Corregir FORMATO PARCIAL (¡SOLUCIÓN CORREGIDA!)
                        // ==========================================================
                        var parcialAudit = elementData.AuditResults.FirstOrDefault(a => a.AuditType == "FORMATO PARCIAL" && a.IsCorrectable);
                        if (parcialAudit != null && parcialAudit.Tag is ScheduleFieldId)
                        {
                            try
                            {
                                ScheduleFieldId fieldId = (ScheduleFieldId)parcialAudit.Tag;
                                ScheduleField field = definition.GetField(fieldId);

                                if (field != null && field.IsValidObject)
                                {
                                    // Leer el FormatOptions actual
                                    FormatOptions options = field.GetFormatOptions();

                                    // ========================================
                                    // ¡CORRECCIÓN APLICADA!
                                    // Solo modificamos las propiedades de formato,
                                    // SIN tocar el UnitTypeId (que causaba el error)
                                    // ========================================

                                    options.UseDefault = false;
                                    options.Accuracy = 0.01;
                                    options.RoundingMethod = RoundingMethod.Nearest;
                                    options.SetSymbolTypeId(new ForgeTypeId()); // Sin símbolo de unidad

                                    // ❌ LÍNEA ELIMINADA (era la que causaba el error):
                                    // options.SetUnitTypeId(UnitTypeId.General);

                                    // ✅ NO tocamos el UnitTypeId - el campo conserva su tipo original

                                    // Aplicar los cambios
                                    field.SetFormatOptions(options);

                                    parcialCorregidos++;
                                    tablaModificada = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                result.Errores.Add($"Error al corregir formato 'PARCIAL' en tabla '{elementData.Nombre}': {ex.Message}");
                            }
                        }


                        // ==========================================================
                        // ===== 7. Corregir PARÁMETRO EMPRESA
                        // ==========================================================
                        var empresaAudit = elementData.AuditResults.FirstOrDefault(a => a.AuditType == "PARÁMETRO EMPRESA" && a.IsCorrectable);
                        if (empresaAudit != null)
                        {
                            try
                            {
                                Parameter param = view.LookupParameter(EMPRESA_PARAM_NAME);

                                if (param == null && empresaParamElem != null)
                                {
                                    result.Errores.Add($"Error en tabla '{elementData.Nombre}': El parámetro 'EMPRESA' no está vinculado. Ejecute el Add-in 'Verificar Parámetro Empresa'.");
                                }
                                else if (param != null && !param.IsReadOnly)
                                {
                                    param.Set(EMPRESA_PARAM_VALUE);
                                    empresaCorregidos++;
                                    tablaModificada = true;
                                }
                                else if (param == null && empresaParamElem == null)
                                {
                                    result.Errores.Add($"Error en tabla '{elementData.Nombre}': No se encontró el Parámetro Compartido 'EMPRESA' en el proyecto.");
                                }
                            }
                            catch (Exception ex)
                            {
                                result.Errores.Add($"Error al asignar 'EMPRESA' en tabla '{elementData.Nombre}': {ex.Message}");
                            }
                        }

                        if (tablaModificada)
                        {
                            tablasCorregidas++;
                        }
                    } // Fin del foreach


                    // ==========================================================
                    // ===== 8. Lógica de Éxito
                    // ==========================================================
                    if (result.Errores.Count == 0)
                    {
                        result.Exitoso = true;
                    }

                    trans.Commit();
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    result.Exitoso = false;
                    result.Mensaje = $"Error fatal en la transacción: {ex.Message}";
                    result.Errores.Add(ex.Message);
                }
            } // Fin del using Transaction


            // ==========================================================
            // ===== 9. Lógica de Mensaje Final
            // ==========================================================
            if (result.Exitoso)
            {
                result.Mensaje = $"Corrección completa.\n\n" +
                                 $"Tablas únicas modificadas: {tablasCorregidas}\n\n" +
                                 $"Detalles:\n" +
                                 $"- Nombres de tabla corregidos: {nombresCorregidos}\n" +
                                 $"- Tablas reclasificadas: {tablasReclasificadas}\n" +
                                 $"- Parámetros 'EMPRESA' corregidos: {empresaCorregidos}\n" +
                                 $"- Filtros corregidos: {filtrosCorregidos}\n" +
                                 $"- Formatos 'PARCIAL' corregidos: {parcialCorregidos}\n" +
                                 $"- Contenidos (Itemize) corregidos: {contenidosCorregidos}\n" +
                                 $"- 'Include Links' activados: {linksIncluidos}\n" +
                                 $"- Encabezados renombrados: {columnasRenombradas}\n" +
                                 $"- Columnas ocultadas: {columnasOcultadas}";
            }
            else
            {
                if (result.Errores.Count > 0)
                {
                    result.Mensaje = $"Se encontraron {result.Errores.Count} errores durante la corrección (la transacción fue revertida):\n\n" +
                                     string.Join("\n", result.Errores.Take(5));
                }
                else if (string.IsNullOrEmpty(result.Mensaje))
                {
                    result.Mensaje = "La corrección se ejecutó pero no se detectaron errores. La transacción fue revertida.";
                }
            }

            return result;
        }

        /// <summary>
        /// Renombra la tabla y asigna GRUPO DE VISTA, SUBGRUPO DE VISTA y SUBPARTIDA
        /// </summary>
        private bool RenameAndReclassify(ViewSchedule view, RenamingJobData jobData, List<string> errores)
        {
            try
            {
                if (jobData.NuevoNombre != null && view.Name != jobData.NuevoNombre)
                {
                    view.Name = jobData.NuevoNombre;
                }

                Parameter paramGrupo = view.LookupParameter("GRUPO DE VISTA");
                if (paramGrupo != null && !paramGrupo.IsReadOnly && jobData.NuevoGrupoVista != null)
                {
                    paramGrupo.Set(jobData.NuevoGrupoVista);
                }

                Parameter paramSubGrupo = view.LookupParameter("SUBGRUPO DE VISTA");
                if (paramSubGrupo != null && !paramSubGrupo.IsReadOnly && jobData.NuevoSubGrupoVista != null)
                {
                    paramSubGrupo.Set(jobData.NuevoSubGrupoVista);
                }

                // Asignar el parámetro SUBPARTIDA
                Parameter paramSubPartida = view.LookupParameter("SUBGRUPO DE VISTA_SUBPARTIDA");
                if (paramSubPartida != null && !paramSubPartida.IsReadOnly && jobData.NuevoSubGrupoVistaSubpartida != null)
                {
                    paramSubPartida.Set(jobData.NuevoSubGrupoVistaSubpartida);
                }

                return true;
            }
            catch (Exception ex)
            {
                errores.Add($"Error al reclasificar tabla '{view.Name}': {ex.Message}");
                return false;
            }
        }

        #region Métodos Helper

        private bool WriteHeadings(Dictionary<ScheduleField, string> headingsToFix, List<string> errores, string nombreTabla)
        {
            try
            {
                foreach (var kvp in headingsToFix)
                {
                    ScheduleField field = kvp.Key;
                    string correctedHeading = kvp.Value;
                    if (field != null && field.IsValidObject && field.ColumnHeading != correctedHeading)
                    {
                        field.ColumnHeading = correctedHeading;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                errores.Add($"Error al escribir encabezados en '{nombreTabla}': {ex.Message}");
                return false;
            }
        }

        private bool HideColumns(List<ScheduleField> fieldsToHide, List<string> errores, string nombreTabla)
        {
            try
            {
                foreach (var field in fieldsToHide)
                {
                    if (field != null && field.IsValidObject && !field.IsHidden)
                    {
                        field.IsHidden = true;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                errores.Add($"Error al ocultar columnas en '{nombreTabla}': {ex.Message}");
                return false;
            }
        }

        private bool WriteFilters(ScheduleDefinition definition, List<ScheduleFilterInfo> filtrosCorrectos, List<string> errores, string nombreTabla)
        {
            try
            {
                definition.ClearFilters();
                foreach (var filtroInfo in filtrosCorrectos)
                {
                    ScheduleField field = FindField(definition, filtroInfo.FieldName);
                    if (field == null)
                    {
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

        private ScheduleFilter CreateScheduleFilter(ScheduleFieldId fieldId, ScheduleFilterInfo filtroInfo, List<string> errores, string nombreTabla)
        {
            if (filtroInfo.Value == null &&
                filtroInfo.FilterType != ScheduleFilterType.HasValue &&
                filtroInfo.FilterType != ScheduleFilterType.HasNoValue)
            {
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
                    return new ScheduleFilter(fieldId, filtroInfo.FilterType, (double)i);
                case ElementId id:
                    return new ScheduleFilter(fieldId, filtroInfo.FilterType, id);
                case null:
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
                var name = field.GetName();
                if (name.Equals(fieldName, StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith($": {fieldName}", StringComparison.OrdinalIgnoreCase))
                {
                    return field;
                }
            }
            return null;
        }

        #endregion
    }
}