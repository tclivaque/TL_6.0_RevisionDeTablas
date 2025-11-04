using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TL60_RevisionDeTablas.Models;

namespace TL60_RevisionDeTablas.Services
{
    /// <summary>
    /// Procesador para Tablas de Planificación (ViewSchedule)
    /// </summary>
    public class ScheduleProcessor
    {
        private readonly Document _doc;
        private readonly GoogleSheetsService _sheetsService;
        private HashSet<string> _assemblyCodesFromSheets;

        public ScheduleProcessor(Document doc, GoogleSheetsService sheetsService)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _sheetsService = sheetsService ?? throw new ArgumentNullException(nameof(sheetsService));
            LoadDataFromSheets();
        }

        private void LoadDataFromSheets()
        {
            _assemblyCodesFromSheets = new HashSet<string>();
            try
            {
                string spreadsheetId = "14bYBONt68lfM-sx6iIJxkYExXS0u7sdgijEScL3Ed3Y";
                string range = "'TABLAS_ARQ'!B2:B";
                var values = _sheetsService.ReadData(spreadsheetId, range);

                foreach (var row in values)
                {
                    string key = GoogleSheetsService.GetCellValue(row, 0);
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        _assemblyCodesFromSheets.Add(key.Trim().ToUpper());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar datos de Sheets: {ex.Message}");
            }
        }

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

                if (string.IsNullOrWhiteSpace(groupValue) || !groupValue.StartsWith("C."))
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
            var filtrosActuales = definition.GetFilters();
            elementData.FiltrosActualesString = GetFiltersAsString(definition, filtrosActuales);

            // 1. Parsear Assembly Code del nombre
            string[] parts = view.Name.Split(new[] { " - " }, StringSplitOptions.None);
            if (parts.Length < 2)
            {
                elementData.Mensajes.Add("Error: El nombre de la tabla no tiene el formato esperado (Codigo - Descripcion - RNG).");
                return elementData;
            }
            string assemblyCode = parts[0].Trim();
            elementData.CodigoIdentificacion = assemblyCode;


            // 2. Determinar Filtros Correctos (NUEVA LÓGICA)
            var filtrosCorrectos = new List<ScheduleFilterInfo>();

            // --- Filtro 1 (Código): Buscar 'MATERIAL_ASSEMBLY CODE' primero ---
            string codigoFieldName = "Assembly Code"; // Fallback
            if (FindField(definition, "MATERIAL_ASSEMBLY CODE") != null)
            {
                codigoFieldName = "MATERIAL_ASSEMBLY CODE"; // Prioridad
            }
            filtrosCorrectos.Add(new ScheduleFilterInfo
            {
                FieldName = codigoFieldName,
                FilterType = ScheduleFilterType.Equal,
                Value = assemblyCode
            });

            // --- Filtro 2 (Empresa): Siempre 'EMPRESA' ---
            filtrosCorrectos.Add(new ScheduleFilterInfo
            {
                FieldName = "EMPRESA",
                FilterType = ScheduleFilterType.Equal,
                Value = "RNG"
            });

            // --- Filtro 3 (Opcional): Buscar 'METRADO' en filtros actuales ---
            if (CheckForMetradoFilter(definition, filtrosActuales))
            {
                filtrosCorrectos.Add(new ScheduleFilterInfo
                {
                    FieldName = "Assembly Code",
                    FilterType = ScheduleFilterType.Equal,
                    Value = "METRADO"
                });
            }

            // 3. Almacenar resultados en ElementData
            elementData.FiltrosCorrectos = filtrosCorrectos;
            elementData.FiltrosCorrectosString = string.Join("\n", filtrosCorrectos.Select(f => f.AsString()));

            // 4. Comparar y asignar estado
            if (elementData.FiltrosActualesString == elementData.FiltrosCorrectosString)
            {
                elementData.DatosCompletos = true;
            }
            else
            {
                elementData.Mensajes.Add(GetErrorMessage(elementData.FiltrosActualesString, elementData.FiltrosCorrectosString));
            }

            return elementData;
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

        /// <summary>
        /// Revisa si el filtro 'Assembly Code equals METRADO' existe
        /// </summary>
        private bool CheckForMetradoFilter(ScheduleDefinition definition, IList<ScheduleFilter> filters)
        {
            foreach (var filter in filters)
            {
                var field = definition.GetField(filter.FieldId);
                if (field != null &&
                    field.GetName().Equals("Assembly Code", StringComparison.OrdinalIgnoreCase) &&
                    filter.FilterType == ScheduleFilterType.Equal &&
                    GetFilterValue2021(filter).Equals("METRADO", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

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
                string value = GetFilterValue2021(filter);
                filterStrings.Add($"{fieldName} {condition} {value}");
            }
            return string.Join("\n", filterStrings);
        }

        // (¡¡¡CORREGIDO!!!)
        private string GetFilterValue2021(ScheduleFilter filter)
        {
            if (filter.FilterType == ScheduleFilterType.HasValue || filter.FilterType == ScheduleFilterType.HasNoValue)
                return string.Empty;

            if (filter.IsStringValue)
            {
                return filter.GetStringValue();
            }

            // API 2021 no tiene 'GetIntegerValue', los enteros se leen como Double
            if (filter.IsDoubleValue)
            {
                return filter.GetDoubleValue().ToString();
            }

            if (filter.IsElementIdValue)
            {
                return filter.GetElementIdValue().IntegerValue.ToString();
            }

            return "(valor no legible)";
        }

        private string GetErrorMessage(string actual, string correcto)
        {
            if (actual == "(Sin Filtros)") return "Error: No hay filtros.";

            return "Error: Los filtros no coinciden con el orden o los valores correctos.";
        }
    }
}