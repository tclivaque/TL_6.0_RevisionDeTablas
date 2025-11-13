// Plugins/Tablas/ScheduleUpdateWriter.cs
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using TL60_RevisionDeTablas.Models;
using Autodesk.Revit.UI;

namespace TL60_RevisionDeTablas.Plugins.Tablas
{
    public class ScheduleUpdateWriter
    {
        private readonly Document _doc;
        private const string EMPRESA_PARAM_NAME = "EMPRESA";
        private const string EMPRESA_PARAM_VALUE = "RNG";
        private static string _logPath;

        public ScheduleUpdateWriter(Document doc)
        {
            _doc = doc;
            // Crear archivo de log en el escritorio
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            _logPath = Path.Combine(desktop, $"TL60_CorreccionTablas_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            // Escribir encabezado
            File.WriteAllText(_logPath, $"=== LOG DE CORRECCIÓN DE TABLAS ===\n");
            File.AppendAllText(_logPath, $"Fecha: {DateTime.Now}\n");
            File.AppendAllText(_logPath, $"Documento: {doc.Title}\n\n");
        }

        private void Log(string message)
        {
            File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
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
            int parcialSaltados = 0;
            int empresaCorregidos = 0;

            Log("=== INICIO DE CORRECCIÓN ===\n");

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

                        Log($"\n--- Procesando tabla: {elementData.Nombre} (ID: {elementData.ElementId.IntegerValue}) ---");

                        // --- 1. Corregir NOMBRE ---
                        var nameAudit = elementData.AuditResults.FirstOrDefault(a => a.AuditType == "VIEW NAME" && a.IsCorrectable);
                        if (nameAudit != null && nameAudit.Tag is RenamingJobData jobData)
                        {
                            Log("  [1] Corrigiendo nombre...");
                            if (RenameAndReclassify(view, jobData, result.Errores))
                            {
                                nombresCorregidos++;
                                tablasReclasificadas++;
                                tablaModificada = true;
                                Log("  [1] OK Nombre corregido");
                            }
                        }

                        // --- 2. Corregir FILTROS ---
                        var filterAudit = elementData.AuditResults.FirstOrDefault(a => a.AuditType == "FILTROS" && a.IsCorrectable);
                        if (filterAudit != null && filterAudit.Tag is List<ScheduleFilterInfo> filtrosCorrectos)
                        {
                            Log("  [2] Corrigiendo filtros...");
                            if (WriteFilters(definition, filtrosCorrectos, result.Errores, elementData.Nombre))
                            {
                                filtrosCorregidos++;
                                tablaModificada = true;
                                Log("  [2] OK Filtros corregidos");
                            }
                        }

                        // --- 3. Corregir CONTENIDO ---
                        var contentAudit = elementData.AuditResults.FirstOrDefault(a => a.AuditType == "CONTENIDO" && a.IsCorrectable);
                        if (contentAudit != null)
                        {
                            if (!definition.IsItemized)
                            {
                                Log("  [3] Activando Itemize...");
                                definition.IsItemized = true;
                                contenidosCorregidos++;
                                tablaModificada = true;
                                Log("  [3] OK Itemize activado");
                            }
                        }

                        // --- 4. Corregir INCLUDE LINKS ---
                        var linksAudit = elementData.AuditResults.FirstOrDefault(a => a.AuditType == "LINKS" && a.IsCorrectable);
                        if (linksAudit != null)
                        {
                            if (!definition.IncludeLinkedFiles)
                            {
                                Log("  [4] Activando Include Links...");
                                definition.IncludeLinkedFiles = true;
                                linksIncluidos++;
                                tablaModificada = true;
                                Log("  [4] OK Include Links activado");
                            }
                        }

                        // --- 5. Corregir COLUMNAS ---
                        var columnAudit = elementData.AuditResults.FirstOrDefault(a => a.AuditType == "COLUMNAS" && a.IsCorrectable);
                        if (columnAudit != null && columnAudit.Tag != null)
                        {
                            if (columnAudit.Tag is Dictionary<ScheduleField, string> headingsToFix)
                            {
                                Log($"  [5] Renombrando {headingsToFix.Count} encabezados...");
                                if (WriteHeadings(headingsToFix, result.Errores, elementData.Nombre))
                                {
                                    columnasRenombradas += headingsToFix.Count;
                                    tablaModificada = true;
                                    Log($"  [5] OK {headingsToFix.Count} encabezados renombrados");
                                }
                            }
                            else if (columnAudit.Tag is List<ScheduleField> fieldsToHide)
                            {
                                Log($"  [5] Ocultando {fieldsToHide.Count} columnas...");
                                if (HideColumns(fieldsToHide, result.Errores, elementData.Nombre))
                                {
                                    columnasOcultadas += fieldsToHide.Count;
                                    tablaModificada = true;
                                    Log($"  [5] OK {fieldsToHide.Count} columnas ocultadas");
                                }
                            }
                        }

                        // ==========================================================
                        // ===== 6. Corregir FORMATO PARCIAL (SOLUCIÓN CON TRY-CATCH)
                        // ==========================================================
                        var parcialAudit = elementData.AuditResults.FirstOrDefault(a => a.AuditType == "FORMATO PARCIAL" && a.IsCorrectable);
                        if (parcialAudit != null && parcialAudit.Tag is ScheduleFieldId)
                        {
                            ScheduleFieldId fieldId = (ScheduleFieldId)parcialAudit.Tag;
                            ScheduleField field = definition.GetField(fieldId);

                            if (field != null && field.IsValidObject)
                            {
                                Log($"  [6] Intentando corregir formato PARCIAL (Tipo: {field.FieldType})...");

                                try
                                {
                                    // Intentar corregir sin importar el tipo de campo
                                    FormatOptions options = field.GetFormatOptions();

                                    // Modificar solo lo esencial:
                                    // 1. Desactivar "Use project settings"
                                    options.UseDefault = false;

                                    // 2. Establecer precisión a 2 decimales
                                    options.Accuracy = 0.01;

                                    // 3. Quitar símbolo de unidad (dejar "None")
                                    options.SetSymbolTypeId(new ForgeTypeId());

                                    // NO tocar:
                                    // - SetUnitTypeId() → Conserva las unidades actuales
                                    // - RoundingMethod → Conserva el método actual

                                    // Aplicar cambios
                                    field.SetFormatOptions(options);

                                    // ✅ Éxito
                                    parcialCorregidos++;
                                    tablaModificada = true;
                                    Log($"  [6] OK Formato PARCIAL corregido (Tipo: {field.FieldType})");
                                }
                                catch (Exception ex)
                                {
                                    // ❌ Este campo específico no acepta corrección
                                    string errorMsg = $"No se pudo corregir PARCIAL en '{elementData.Nombre}' (Tipo: {field.FieldType}): {ex.Message}";
                                    result.Errores.Add(errorMsg);
                                    parcialSaltados++;
                                    Log($"  [6] SALTADO ({field.FieldType}): {ex.Message}");
                                }
                            }
                        }

                        // --- 7. Corregir PARÁMETRO EMPRESA ---
                        var empresaAudit = elementData.AuditResults.FirstOrDefault(a => a.AuditType == "PARÁMETRO EMPRESA" && a.IsCorrectable);
                        if (empresaAudit != null)
                        {
                            try
                            {
                                Parameter param = view.LookupParameter(EMPRESA_PARAM_NAME);

                                if (param == null && empresaParamElem != null)
                                {
                                    result.Errores.Add($"Error en '{elementData.Nombre}': Parámetro EMPRESA no vinculado.");
                                    Log("  [7] ERROR: EMPRESA no vinculado");
                                }
                                else if (param != null && !param.IsReadOnly)
                                {
                                    param.Set(EMPRESA_PARAM_VALUE);
                                    empresaCorregidos++;
                                    tablaModificada = true;
                                    Log("  [7] OK EMPRESA asignado");
                                }
                                else if (param == null && empresaParamElem == null)
                                {
                                    result.Errores.Add($"Error en '{elementData.Nombre}': EMPRESA no existe.");
                                    Log("  [7] ERROR: EMPRESA no existe");
                                }
                            }
                            catch (Exception ex)
                            {
                                result.Errores.Add($"Error EMPRESA en '{elementData.Nombre}': {ex.Message}");
                                Log($"  [7] ERROR: {ex.Message}");
                            }
                        }

                        if (tablaModificada)
                        {
                            tablasCorregidas++;
                        }
                    }

                    if (result.Errores.Count == 0)
                    {
                        result.Exitoso = true;
                    }

                    trans.Commit();
                    Log("\n=== TRANSACCIÓN COMPLETADA ===");
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    result.Exitoso = false;
                    result.Mensaje = $"Error fatal: {ex.Message}";
                    result.Errores.Add(ex.Message);
                    Log($"\n=== TRANSACCIÓN REVERTIDA ===");
                    Log($"ERROR: {ex.Message}");
                }
            }

            if (result.Exitoso)
            {
                result.Mensaje = $"Corrección completa.\n\n" +
                                 $"Tablas modificadas: {tablasCorregidas}\n" +
                                 $"Formatos PARCIAL corregidos: {parcialCorregidos}\n" +
                                 $"Formatos PARCIAL saltados: {parcialSaltados}\n\n" +
                                 $"Detalles:\n" +
                                 $"- Nombres: {nombresCorregidos}\n" +
                                 $"- Reclasificadas: {tablasReclasificadas}\n" +
                                 $"- EMPRESA: {empresaCorregidos}\n" +
                                 $"- Filtros: {filtrosCorregidos}\n" +
                                 $"- Itemize: {contenidosCorregidos}\n" +
                                 $"- Links: {linksIncluidos}\n" +
                                 $"- Encabezados: {columnasRenombradas}\n" +
                                 $"- Columnas ocultas: {columnasOcultadas}\n\n" +
                                 $"Log: {_logPath}";

                Log($"\n=== RESUMEN ===");
                Log($"Tablas modificadas: {tablasCorregidas}");
                Log($"PARCIAL corregidos: {parcialCorregidos}");
                Log($"PARCIAL saltados: {parcialSaltados}");
            }
            else
            {
                if (result.Errores.Count > 0)
                {
                    result.Mensaje = $"Errores: {result.Errores.Count} (transacción revertida)\n\n" +
                                     string.Join("\n", result.Errores.Take(5)) +
                                     $"\n\nLog: {_logPath}";
                }

                Log($"\n=== FALLIDO ===");
                Log($"Errores: {result.Errores.Count}");
            }

            return result;
        }

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

                Parameter paramSubPartida = view.LookupParameter("SUBGRUPO DE VISTA_SUBPARTIDA");
                if (paramSubPartida != null && !paramSubPartida.IsReadOnly && jobData.NuevoSubGrupoVistaSubpartida != null)
                {
                    paramSubPartida.Set(jobData.NuevoSubGrupoVistaSubpartida);
                }

                return true;
            }
            catch (Exception ex)
            {
                errores.Add($"Error al reclasificar '{view.Name}': {ex.Message}");
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
                errores.Add($"Error encabezados en '{nombreTabla}': {ex.Message}");
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
                errores.Add($"Error ocultar columnas en '{nombreTabla}': {ex.Message}");
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
                    if (field == null) continue;

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
                errores.Add($"Error filtros en '{nombreTabla}': {ex.Message}");
                return false;
            }
        }

        private ScheduleFilter CreateScheduleFilter(ScheduleFieldId fieldId, ScheduleFilterInfo filtroInfo, List<string> errores, string nombreTabla)
        {
            if (filtroInfo.Value == null &&
                filtroInfo.FilterType != ScheduleFilterType.HasValue &&
                filtroInfo.FilterType != ScheduleFilterType.HasNoValue)
            {
                errores.Add($"Filtro nulo en '{filtroInfo.FieldName}' - '{nombreTabla}'.");
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
                    errores.Add($"Tipo filtro inválido ({filtroInfo.Value.GetType()}) - '{filtroInfo.FieldName}' en '{nombreTabla}'.");
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