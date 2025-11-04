// Services/ScheduleProcessor.cs
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TL60_RevisionDeTablas.Models;

namespace TL60_RevisionDeTablas.Services
{
    public class ScheduleProcessor
    {
        private readonly Document _doc;
        private readonly GoogleSheetsService _sheetsService;

        private readonly Dictionary<string, string> _aliasEncabezados = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "DIGO", "CODIGO" },
            { "DESCRIP", "DESCRIPCION" },
            { "ACTIV", "ACTIVO" },
            { "DULO", "MODULO" },
            { "NIVE", "NIVEL" },
            { "AMBIEN", "AMBIENTE" },
            { "EJE", "EJES" },
            { "PARC", "PARCIAL" },
            { "UNID", "UNIDAD" },
            { "ID", "ID" }
        };

        private readonly List<string> _expectedHeadings = new List<string>
        {
            "CODIGO",
            "DESCRIPCION",
            "ACTIVO",
            "MODULO",
            "NIVEL",
            "AMBIENTE",
            "PARCIAL",
            "UNIDAD",
            "ID"
        };
        private const int _expectedHeadingCount = 9;


        public ScheduleProcessor(Document doc, GoogleSheetsService sheetsService)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _sheetsService = sheetsService ?? throw new ArgumentNullException(nameof(sheetsService));
        }

        private void LoadDataFromSheets() { }

        public List<ElementData> ProcessElements()
        {
            var elementosData = new List<ElementData>();
            var collector = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Schedules)
                .WhereElementIsNotElementType();

            foreach (Element elem in collector)
            {
                ViewSchedule view = elem as ViewSchedule;
                if (view == null || view.Definition == null) continue;

                Parameter groupParam = view.LookupParameter("GRUPO DE VISTA");
                string groupValue = groupParam?.AsString();

                if (string.IsNullOrWhiteSpace(groupValue) || !groupValue.ToUpper().StartsWith("C."))
                {
                    continue;
                }

                var elementData = ProcessSingleElement(view);
                elementosData.Add(elementData);
            }

            return elementosData;
        }

        private ElementData ProcessSingleElement(ViewSchedule view)
        {
            var elementData = new ElementData
            {
                ElementId = view.Id,
                Element = view,
                Nombre = view.Name,
                Categoria = view.Category?.Name ?? "Tabla de Planificación"
            };

            ScheduleDefinition definition = view.Definition;

            string[] parts = view.Name.Split(new[] { " - " }, StringSplitOptions.None);
            elementData.CodigoIdentificacion = parts.Length > 0 ?
                parts[0].Trim() : view.Name;

            var auditFilter = ProcessFilters(definition, elementData.CodigoIdentificacion, definition.IsMaterialTakeoff);
            var auditColumns = ProcessColumns(definition);
            var auditContent = ProcessContent(view, definition);

            elementData.AuditResults.Add(auditFilter);
            elementData.AuditResults.Add(auditColumns);
            elementData.AuditResults.Add(auditContent);

            if (auditFilter.Estado == EstadoParametro.Corregir)
            {
                elementData.FiltrosCorrectos = (List<ScheduleFilterInfo>)auditFilter.Tag;
            }

            if (auditColumns.Estado == EstadoParametro.Corregir)
            {
                elementData.HeadingsCorregidos = (Dictionary<ScheduleField, string>)auditColumns.Tag;
            }

            if (auditContent.Estado == EstadoParametro.Corregir)
            {
                elementData.IsItemizedCorrect = false;
            }

            elementData.DatosCompletos = elementData.AuditResults.All(r => r.Estado == EstadoParametro.Correcto);

            return elementData;
        }

        #region Auditoría 1: FILTRO (LÓGICA CORREGIDA)

        private AuditItem ProcessFilters(ScheduleDefinition definition, string assemblyCode, bool isMaterialTakeoff)
        {
            var item = new AuditItem
            {
                AuditType = "FILTRO",
                IsCorrectable = true
            };

            var filtrosActuales = definition.GetFilters().ToList();
            var filtrosCorrectosInfo = new List<ScheduleFilterInfo>();

            // =========================================================
            // ===== INICIO DE CORRECCIÓN DE LÓGICA DE FILTROS =====
            // =========================================================

            // 1. Encontrar los ÍNDICES de los filtros "pie forzado"
            int mainAssemblyCodeFilterIndex = FindMAINAssemblyCodeFilterIndex(filtrosActuales, definition);
            int empresaFilterIndex = FindEmpresaFilterIndex(filtrosActuales, definition);

            // 2. Definir valores correctos para "pie forzado"
            string campoAssemblyCorrecto = isMaterialTakeoff ? "MATERIAL_ASSEMBLY CODE" : "Assembly Code";
            string valorAssemblyCorrecto = assemblyCode;
            ScheduleFilterType tipoAssemblyCorrecto = ScheduleFilterType.Equal;

            string campoEmpresaCorrecto = "EMPRESA";
            string valorEmpresaCorrecto = "RNG";
            ScheduleFilterType tipoEmpresaCorrecto = ScheduleFilterType.Equal;

            // 3. Añadir el filtro de Assembly Code (nuevo o corregido)
            if (mainAssemblyCodeFilterIndex == -1) // No se encontró
            {
                filtrosCorrectosInfo.Add(new ScheduleFilterInfo { FieldName = campoAssemblyCorrecto, FilterType = tipoAssemblyCorrecto, Value = valorAssemblyCorrecto });
            }
            else
            {
                // Se encontró, verificarlo
                ScheduleFilter filter = filtrosActuales[mainAssemblyCodeFilterIndex];
                string campoActual = definition.GetField(filter.FieldId).GetName();
                var tipoActual = filter.FilterType;
                var valorActual = GetFilterValueString(filter);

                if (campoActual.Equals(campoAssemblyCorrecto, StringComparison.OrdinalIgnoreCase) &&
                    tipoActual == tipoAssemblyCorrecto &&
                    valorActual.Equals(valorAssemblyCorrecto, StringComparison.OrdinalIgnoreCase))
                {
                    filtrosCorrectosInfo.Add(new ScheduleFilterInfo { FieldName = campoActual, FilterType = tipoActual, Value = valorActual });
                }
                else
                {
                    filtrosCorrectosInfo.Add(new ScheduleFilterInfo { FieldName = campoAssemblyCorrecto, FilterType = tipoAssemblyCorrecto, Value = valorAssemblyCorrecto });
                }
            }

            // 4. Añadir el filtro de Empresa (nuevo o corregido)
            if (empresaFilterIndex == -1) // No se encontró
            {
                filtrosCorrectosInfo.Add(new ScheduleFilterInfo { FieldName = campoEmpresaCorrecto, FilterType = tipoEmpresaCorrecto, Value = valorEmpresaCorrecto });
            }
            else
            {
                // Se encontró, verificarlo
                ScheduleFilter filter = filtrosActuales[empresaFilterIndex];
                var tipoActual = filter.FilterType;
                var valorActual = GetFilterValueString(filter);

                if (tipoActual == tipoEmpresaCorrecto &&
                    valorActual.Equals(valorEmpresaCorrecto, StringComparison.OrdinalIgnoreCase))
                {
                    filtrosCorrectosInfo.Add(new ScheduleFilterInfo { FieldName = campoEmpresaCorrecto, FilterType = tipoActual, Value = valorActual });
                }
                else
                {
                    filtrosCorrectosInfo.Add(new ScheduleFilterInfo { FieldName = campoEmpresaCorrecto, FilterType = tipoEmpresaCorrecto, Value = valorEmpresaCorrecto });
                }
            }

            // 5. Añadir todos los demás filtros en su orden original, usando sus ÍNDICES
            for (int i = 0; i < filtrosActuales.Count; i++)
            {
                // Añadir solo si NO es uno de los "pie forzado" que ya procesamos
                if (i != mainAssemblyCodeFilterIndex && i != empresaFilterIndex)
                {
                    var filter = filtrosActuales[i];
                    var field = definition.GetField(filter.FieldId);
                    if (field == null) continue;

                    filtrosCorrectosInfo.Add(new ScheduleFilterInfo
                    {
                        FieldName = field.GetName(),
                        FilterType = filter.FilterType, // Conservar su tipo (ej. NotContains)
                        Value = GetFilterValueObject(filter) // Conservar su valor (ej. METRADO o N/A)
                    });
                }
            }

            // =======================================================
            // ===== FIN DE CORRECCIÓN DE LÓGICA DE FILTROS =====
            // =======================================================

            // 6. Comparar y finalizar
            item.ValorActual = GetFiltersAsString(definition, filtrosActuales);
            item.ValorCorrecto = string.Join("\n", filtrosCorrectosInfo.Select(f => f.AsString()));
            item.Tag = filtrosCorrectosInfo;

            if (item.ValorActual == item.ValorCorrecto)
            {
                item.Estado = EstadoParametro.Correcto;
                item.Mensaje = "Filtros correctos.";
            }
            else
            {
                item.Estado = EstadoParametro.Corregir;
                if (filtrosActuales.Count == 0) item.Mensaje = "Error: No hay filtros.";
                else item.Mensaje = "Error: Los filtros no coinciden con el orden o los valores esperados.";
            }

            return item;
        }

        /// <summary>
        /// (NUEVO) Busca el ÍNDICE del filtro de Assembly Code principal (el que es 'Equal').
        /// </summary>
        private int FindMAINAssemblyCodeFilterIndex(List<ScheduleFilter> filters, ScheduleDefinition def)
        {
            // 1. Buscar el que sea "Equal"
            for (int i = 0; i < filters.Count; i++)
            {
                var filter = filters[i];
                if (filter.FilterType == ScheduleFilterType.Equal)
                {
                    var fieldName = def.GetField(filter.FieldId)?.GetName();
                    if (fieldName != null &&
                        (fieldName.Equals("MATERIAL_ASSEMBLY CODE", StringComparison.OrdinalIgnoreCase) ||
                         fieldName.Equals("Assembly Code", StringComparison.OrdinalIgnoreCase)))
                    {
                        return i; // Encontrado
                    }
                }
            }

            // 2. Si no hay "Equal", no hay filtro principal
            return -1;
        }

        /// <summary>
        /// (NUEVO) Busca el ÍNDICE del filtro de Empresa.
        /// </summary>
        private int FindEmpresaFilterIndex(List<ScheduleFilter> filters, ScheduleDefinition def)
        {
            for (int i = 0; i < filters.Count; i++)
            {
                var fieldName = def.GetField(filters[i].FieldId)?.GetName();
                if (fieldName != null && fieldName.Equals("EMPRESA", StringComparison.OrdinalIgnoreCase))
                {
                    return i; // Encontrado
                }
            }
            return -1; // No encontrado
        }

        #endregion

        #region Auditoría 2: COLUMNAS (LÓGICA CORREGIDA)

        private AuditItem ProcessColumns(ScheduleDefinition definition)
        {
            var item = new AuditItem
            {
                AuditType = "COLUMNAS",
                IsCorrectable = true
            };

            var actualHeadings = new List<string>();
            var visibleFields = new List<ScheduleField>();

            for (int i = 0; i < definition.GetFieldCount(); i++)
            {
                var field = definition.GetField(i);
                if (!field.IsHidden)
                {
                    actualHeadings.Add(field.ColumnHeading.ToUpper().Trim());
                    visibleFields.Add(field);
                }
            }

            if (actualHeadings.Count != _expectedHeadingCount)
            {
                item.Estado = EstadoParametro.Error;
                item.Mensaje = $"Error: Se esperaban {_expectedHeadingCount} columnas visibles, pero se encontraron {actualHeadings.Count}.";
                item.ValorActual = $"Total: {actualHeadings.Count}\n" + string.Join("\n", actualHeadings);
                item.ValorCorrecto = $"Total: {_expectedHeadingCount}\n" + string.Join("\n", _expectedHeadings);
                item.IsCorrectable = false;
                return item;
            }

            var correctedHeadings = new List<string>();
            var headingsToFix = new Dictionary<ScheduleField, string>();
            bool isCorrect = true;

            for (int i = 0; i < _expectedHeadingCount; i++)
            {
                string actual = actualHeadings[i];
                string expectedBase = _expectedHeadings[i]; // ej. "AMBIENTE"
                string corrected = actual;

                if (i == 5) // Posición 6 (AMBIENTE o EJE)
                {
                    string expectedAlt = "EJES";
                    if (actual == expectedBase || actual == expectedAlt) // Si es "AMBIENTE" o "EJES"
                    {
                        corrected = actual;
                    }
                    else if (_aliasEncabezados.TryGetValue(actual, out string aliasValue)) // Si es "AMBIEN" o "EJE"
                    {
                        corrected = aliasValue; // Se convierte en "AMBIENTE" o "EJES"
                    }
                    else
                    {
                        isCorrect = false;
                        corrected = expectedBase; // Forzar a "AMBIENTE"
                    }
                }
                else // Lógica normal para las otras 8 columnas
                {
                    if (actual == expectedBase)
                    {
                        corrected = actual; // Ya es correcto
                    }
                    else if (_aliasEncabezados.TryGetValue(actual, out string aliasValue))
                    {
                        corrected = aliasValue; // Corregir "DIGO" a "CODIGO"
                    }
                }

                // Comparar el valor (potencialmente corregido) con el esperado
                if (i == 5) // Lógica especial para AMBIENTE/EJES
                {
                    if (corrected != "AMBIENTE" && corrected != "EJES")
                    {
                        isCorrect = false;
                        corrected = "AMBIENTE"; // Forzar a "AMBIENTE" si todo falla
                    }
                }
                else if (corrected != expectedBase)
                {
                    isCorrect = false;
                    corrected = expectedBase; // Forzar al valor base si el alias no coincide
                }

                correctedHeadings.Add(corrected);

                if (actual != corrected)
                {
                    isCorrect = false;
                    headingsToFix[visibleFields[i]] = corrected;
                }
            }

            item.ValorActual = string.Join("\n", actualHeadings);
            item.ValorCorrecto = string.Join("\n", correctedHeadings);

            if (isCorrect)
            {
                item.Estado = EstadoParametro.Correcto;
                item.Mensaje = "Encabezados correctos.";
            }
            else
            {
                item.Estado = EstadoParametro.Corregir;
                item.Mensaje = $"Error: {headingsToFix.Count} encabezados necesitan corrección.";
                item.Tag = headingsToFix;
            }

            return item;
        }

        #endregion

        #region Auditoría 3: CONTENIDO (RÁPIDO)

        private AuditItem ProcessContent(ViewSchedule view, ScheduleDefinition definition)
        {
            var item = new AuditItem
            {
                AuditType = "CONTENIDO",
                IsCorrectable = true
            };

            bool isItemized = definition.IsItemized;

            string actualItemizedStr = isItemized ? "Sí" : "No";

            item.ValorActual = $"Detallar cada ejemplar: {actualItemizedStr}";
            item.ValorCorrecto = $"Detallar cada ejemplar: Sí";

            if (isItemized)
            {
                item.Estado = EstadoParametro.Correcto;
                item.Mensaje = "Correcto.";
            }
            else
            {
                item.Estado = EstadoParametro.Corregir;
                item.Mensaje = "Error: 'Detallar cada ejemplar' está desactivado.";
            }

            return item;
        }

        #endregion

        #region Helpers de Filtros

        private string GetFiltersAsString(ScheduleDefinition definition, IList<ScheduleFilter> filters)
        {
            if (filters == null || filters.Count == 0)
                return "(Sin Filtros)";

            var filterStrings = new List<string>();
            foreach (var filter in filters)
            {
                if (filter == null) continue;
                ScheduleField field = definition.GetField(filter.FieldId);
                if (field == null) continue;

                string fieldName = field.GetName();
                string condition = filter.FilterType.ToString();
                string value = GetFilterValueString(filter);
                filterStrings.Add($"{fieldName} {condition} {value}");
            }
            return string.Join("\n", filterStrings);
        }

        private string GetFilterValueString(ScheduleFilter filter)
        {
            if (filter.FilterType == ScheduleFilterType.HasValue || filter.FilterType == ScheduleFilterType.HasNoValue)
                return string.Empty;

            if (filter.IsStringValue)
                return filter.GetStringValue();

            if (filter.IsDoubleValue)
                return filter.GetDoubleValue().ToString();

            if (filter.IsElementIdValue)
                return filter.GetElementIdValue().IntegerValue.ToString();

            return "(valor no legible)";
        }

        private object GetFilterValueObject(ScheduleFilter filter)
        {
            if (filter.FilterType == ScheduleFilterType.HasValue || filter.FilterType == ScheduleFilterType.HasNoValue)
                return null;

            if (filter.IsStringValue)
                return filter.GetStringValue();

            if (filter.IsDoubleValue)
                return filter.GetDoubleValue();

            if (filter.IsElementIdValue)
                return filter.GetElementIdValue();

            return null;
        }

        #endregion
    }
}