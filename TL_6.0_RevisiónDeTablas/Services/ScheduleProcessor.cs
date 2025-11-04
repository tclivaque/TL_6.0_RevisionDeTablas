// Services/ScheduleProcessor.cs
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TL60_RevisionDeTablas.Models;

namespace TL60_RevisionDeTablas.Services
{
    /// <summary>
    /// Procesador para auditar Tablas de Planificación (ViewSchedule)
    /// </summary>
    public class ScheduleProcessor
    {
        private readonly Document _doc;
        private readonly GoogleSheetsService _sheetsService;
        private readonly ScheduleEmptinessChecker _emptinessChecker;

        private readonly List<string> _expectedHeadings = new List<string>
        {
            "CODIGO",
            "DESCRIPCION",
            "ACTIVO",
            "MODULO",
            "NIVEL",
            "AMBIENTE", // Este es el caso base
            "PARCIAL",
            "UNIDAD",
            "ID DE ELEMENTO"
        };
        private const int _expectedHeadingCount = 9;


        public ScheduleProcessor(Document doc, GoogleSheetsService sheetsService)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _sheetsService = sheetsService ?? throw new ArgumentNullException(nameof(sheetsService));
            _emptinessChecker = new ScheduleEmptinessChecker(doc);
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
                Categoria = "Tabla de Planificación"
            };

            ScheduleDefinition definition = view.Definition;

            string[] parts = view.Name.Split(new[] { " - " }, StringSplitOptions.None);
            elementData.CodigoIdentificacion = parts.Length > 0 ?
                parts[0].Trim() : view.Name;

            // --- Ejecutar Auditorías ---
            var auditFilter = ProcessFilters(definition, elementData.CodigoIdentificacion);
            var auditColumns = ProcessColumns(definition);
            var auditContent = ProcessContent(view, definition);

            elementData.AuditResults.Add(auditFilter);
            elementData.AuditResults.Add(auditColumns);
            elementData.AuditResults.Add(auditContent);

            // --- Almacenar datos para corrección ---
            if (auditFilter.Estado == EstadoParametro.Corregir)
            {
                elementData.FiltrosCorrectos = (List<ScheduleFilterInfo>)auditFilter.Tag;
            }
            if (auditContent.Estado == EstadoParametro.Corregir)
            {
                elementData.IsItemizedCorrect = false;
            }

            elementData.DatosCompletos = elementData.AuditResults.All(r => r.Estado == EstadoParametro.Correcto);

            return elementData;
        }

        #region Auditoría 1: FILTRO

        private AuditItem ProcessFilters(ScheduleDefinition definition, string assemblyCode)
        {
            var item = new AuditItem
            {
                AuditType = "FILTRO",
                IsCorrectable = true
            };

            var filtrosActuales = definition.GetFilters().ToList();
            var filtrosCorrectosInfo = new List<ScheduleFilterInfo>();

            ScheduleFilter assemblyCodeFilter = FindAssemblyCodeFilter(filtrosActuales, definition);
            if (assemblyCodeFilter != null)
            {
                string fieldName = definition.GetField(assemblyCodeFilter.FieldId).GetName();
                filtrosCorrectosInfo.Add(new ScheduleFilterInfo
                {
                    FieldName = fieldName,
                    FilterType = ScheduleFilterType.Equal,
                    Value = assemblyCode
                });
            }

            ScheduleFilter empresaFilter = filtrosActuales.FirstOrDefault(f =>
                definition.GetField(f.FieldId).GetName().Equals("EMPRESA", StringComparison.OrdinalIgnoreCase));
            if (empresaFilter != null)
            {
                filtrosCorrectosInfo.Add(new ScheduleFilterInfo
                {
                    FieldName = "EMPRESA",
                    FilterType = ScheduleFilterType.Equal,
                    Value = "RNG"
                });
            }

            foreach (var filter in filtrosActuales)
            {
                if (filter != assemblyCodeFilter && filter != empresaFilter)
                {
                    var field = definition.GetField(filter.FieldId);
                    if (field == null) continue;

                    filtrosCorrectosInfo.Add(new ScheduleFilterInfo
                    {
                        FieldName = field.GetName(),
                        FilterType = filter.FilterType,
                        Value = GetFilterValueObject(filter)
                    });
                }
            }

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

        private ScheduleFilter FindAssemblyCodeFilter(List<ScheduleFilter> filters, ScheduleDefinition def)
        {
            var matFilter = filters.FirstOrDefault(f =>
                def.GetField(f.FieldId).GetName().Equals("MATERIAL_ASSEMBLY CODE", StringComparison.OrdinalIgnoreCase));
            if (matFilter != null) return matFilter;

            return filters.FirstOrDefault(f =>
                def.GetField(f.FieldId).GetName().Equals("Assembly Code", StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Auditoría 2: CANTIDAD DE COLUMNAS

        private AuditItem ProcessColumns(ScheduleDefinition definition)
        {
            var item = new AuditItem
            {
                AuditType = "CANTIDAD DE COLUMNAS",
                IsCorrectable = false
            };

            var actualHeadings = new List<string>();
            for (int i = 0; i < definition.GetFieldCount(); i++)
            {
                var field = definition.GetField(i);
                if (!field.IsHidden)
                {
                    actualHeadings.Add(field.ColumnHeading.ToUpper().Trim());
                }
            }

            item.ValorCorrecto = $"Total: {_expectedHeadingCount}\n" + string.Join("\n", _expectedHeadings);
            item.ValorActual = $"Total: {actualHeadings.Count}\n" + string.Join("\n", actualHeadings);

            if (actualHeadings.Count != _expectedHeadingCount)
            {
                item.Estado = EstadoParametro.Error;
                item.Mensaje = $"Error: Se esperaban {_expectedHeadingCount} columnas visibles, pero se encontraron {actualHeadings.Count}.";
                return item;
            }

            for (int i = 0; i < _expectedHeadingCount; i++)
            {
                string actual = actualHeadings[i];
                string expected = _expectedHeadings[i];

                if (i == 5 && (actual == "AMBIENTE" || actual == "EJES"))
                {
                    continue;
                }

                if (actual != expected)
                {
                    item.Estado = EstadoParametro.Error;
                    item.Mensaje = $"Error en columna {i + 1}: Se esperaba '{expected}', pero se encontró '{actual}'.";
                    return item;
                }
            }

            item.Estado = EstadoParametro.Correcto;
            item.Mensaje = "Columnas correctas.";
            return item;
        }

        #endregion

        #region Auditoría 3: CONTENIDO (¡OPTIMIZADO CON EARLY EXIT!)

        /// <summary>
        /// Auditoría de CONTENIDO: Revisa "Itemize" y si la tabla está vacía.
        /// AHORA USA: ScheduleEmptinessChecker con estrategia de Early Exit optimizada.
        /// </summary>
        private AuditItem ProcessContent(ViewSchedule view, ScheduleDefinition definition)
        {
            var item = new AuditItem
            {
                AuditType = "CONTENIDO",
                IsCorrectable = true
            };

            // 1. Verificar "Itemize every instance" (RÁPIDO)
            bool isItemized = definition.IsItemized;

            // 2. Verificar si la tabla está vacía (¡NUEVO MÉTODO OPTIMIZADO!)
            bool isEmpty = false;
            try
            {
                isEmpty = _emptinessChecker.IsScheduleEmpty(view);
            }
            catch (Exception)
            {
                // Si falla, asumir que NO está vacía (evitar falsos positivos)
                isEmpty = false;
            }

            // --- Construir Strings ---
            string actualItemizedStr = isItemized ? "Sí" : "No";
            string actualRowsStr = isEmpty ? "Vacía (0 elementos)" : "Con Datos";

            item.ValorActual = $"Detallar cada ejemplar: {actualItemizedStr}\nContenido: {actualRowsStr}";
            item.ValorCorrecto = $"Detallar cada ejemplar: Sí\nContenido: Con Datos";

            // --- Validar ---
            if (isItemized && !isEmpty)
            {
                item.Estado = EstadoParametro.Correcto;
                item.Mensaje = "Contenido correcto.";
            }
            else
            {
                // Si 'Itemize' está mal, es Corregir. Si solo está vacía, es Vacio (Advertencia).
                item.Estado = !isItemized ? EstadoParametro.Corregir : EstadoParametro.Vacio;

                var mensajes = new List<string>();
                if (!isItemized) mensajes.Add("Error: 'Detallar cada ejemplar' está desactivado.");
                if (isEmpty) mensajes.Add("Advertencia: La tabla no tiene elementos (está vacía).");
                item.Mensaje = string.Join("\n", mensajes);
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