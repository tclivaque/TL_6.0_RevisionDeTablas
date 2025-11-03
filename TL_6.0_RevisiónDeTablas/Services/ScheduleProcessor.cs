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

        // Cache de Assembly Codes de Google Sheets (aún no usado, pero listo)
        private HashSet<string> _assemblyCodesFromSheets;

        public ScheduleProcessor(Document doc, GoogleSheetsService sheetsService)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _sheetsService = sheetsService ?? throw new ArgumentNullException(nameof(sheetsService));

            // Cargar datos de Google Sheets
            LoadDataFromSheets();
        }

        /// <summary>
        /// Carga Assembly Codes de la hoja "TABLAS_ARQ"
        /// </summary>
        private void LoadDataFromSheets()
        {
            _assemblyCodesFromSheets = new HashSet<string>();
            try
            {
                // ID y Hoja vienen del Contexto del Proyecto
                string spreadsheetId = "14bYBONt68lfM-sx6iIJxkYExXS0u7sdgijEScL3Ed3Y";
                string range = "'TABLAS_ARQ'!B2:B"; // Columna B

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
                // No bloquear el plugin si falla Google, solo registrar
                System.Diagnostics.Debug.WriteLine($"Error al cargar datos de Sheets: {ex.Message}");
            }
        }

        /// <summary>
        /// Procesa todas las tablas de planificación filtradas
        /// </summary>
        public List<ElementData> ProcessElements()
        {
            var elementosData = new List<ElementData>();

            // 1. Obtener todas las tablas (OST_Schedules)
            var collector = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Schedules)
                .WhereElementIsNotElementType();

            foreach (Element elem in collector)
            {
                ViewSchedule view = elem as ViewSchedule;
                if (view == null || view.Definition == null) continue;

                // 2. Filtrar por parámetro "GRUPO DE VISTA"
                Parameter groupParam = view.get_Parameter(BuiltInParameter.VIEW_GROUP);
                string groupValue = groupParam?.AsString();

                if (string.IsNullOrWhiteSpace(groupValue) || !groupValue.StartsWith("C."))
                {
                    continue; // Ignorar esta tabla
                }

                // 3. Procesar la tabla individual
                var elementData = ProcessSingleElement(view);
                elementosData.Add(elementData);
            }

            return elementosData;
        }

        /// <summary>
        /// Procesa una tabla individual
        /// </summary>
        private ElementData ProcessSingleElement(ViewSchedule view)
        {
            var elementData = new ElementData
            {
                ElementId = view.Id,
                Element = view,
                Nombre = view.Name,
                Categoria = "Tabla de Planificación"
            };

            // 4. Parsear el nombre para obtener Assembly Code
            string[] parts = view.Name.Split(new[] { " - " }, StringSplitOptions.None);
            string assemblyCode = string.Empty;

            if (parts.Length >= 2) // "Codigo" - "Descripcion" - "RNG"
            {
                assemblyCode = parts[0].Trim();
                elementData.CodigoIdentificacion = assemblyCode;
            }
            else
            {
                elementData.Mensajes.Add("Error: El nombre de la tabla no tiene el formato esperado (Codigo - Descripcion - RNG).");
                elementData.ParametrosActuales["Filtros"] = GetFiltersAsString(view.Definition.GetFilters());
                elementData.ParametrosActualizar["Filtros"] = "Error de Formato";
                return elementData;
            }

            // 5. Definir valores correctos
            string valorCorrectoFiltros = $"Assembly Code equals {assemblyCode}\nEMPRESA equals RNG";
            string valorActualFiltros = GetFiltersAsString(view.Definition.GetFilters());

            [cite_start]// Almacenar valores para el builder [cite: 114, 261]
            elementData.ParametrosActuales["Filtros"] = valorActualFiltros;
            elementData.ParametrosActualizar["Filtros"] = valorCorrectoFiltros;

            // 6. Comparar y asignar estado
            if (valorActualFiltros == valorCorrectoFiltros)
            {
                [cite_start] elementData.ParametrosCorrectos["Filtros"] = valorActualFiltros; [cite: 116]
                elementData.DatosCompletos = true;
            }
            else
            {
                // Asignar mensaje de error específico
                elementData.Mensajes.Add(GetErrorMessage(view.Definition.GetFilters(), assemblyCode));
            }

            return elementData;
        }

        /// <summary>
        /// Convierte la lista de filtros a un string multi-línea
        /// </summary>
        private string GetFiltersAsString(IList<ScheduleFilter> filters)
        {
            if (filters == null || filters.Count == 0)
                return "(Sin Filtros)";

            StringBuilder sb = new StringBuilder();
            foreach (var filter in filters)
            {
                if (filter == null || !filter.IsFilterValid()) continue;

                ScheduleField field = _doc.GetElement(filter.FieldId) as ScheduleField;
                if (field == null) continue;

                string fieldName = field.GetName();
                string condition = filter.FilterType.ToString();
                string value = GetFilterValue(filter);

                sb.AppendLine($"{fieldName} {condition} {value}");
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Obtiene el valor de un filtro (string, double, int, etc.)
        /// </summary>
        private string GetFilterValue(ScheduleFilter filter)
        {
            if (filter.FilterType == ScheduleFilterType.HasValue ||
                filter.FilterType == ScheduleFilterType.HasNoValue)
                return string.Empty;

            if (filter.Value is string s) return s;
            if (filter.Value is double d) return d.ToString();
            if (filter.Value is int i) return i.ToString();
            if (filter.Value is ElementId id) return id.IntegerValue.ToString();

            return filter.Value?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Genera un mensaje de error detallado sobre por qué fallaron los filtros
        /// </summary>
        private string GetErrorMessage(IList<ScheduleFilter> filters, string expectedCode)
        {
            if (filters.Count == 0) return "Error: No hay filtros.";
            if (filters.Count != 2) return $"Error: Se esperaban 2 filtros, pero se encontraron {filters.Count}.";

            // Validar Filtro 1 (Assembly Code)
            var f1 = filters[0];
            var field1 = _doc.GetElement(f1.FieldId) as ScheduleField;
            if (field1 == null || field1.GetName() != "Assembly Code")
                return "Error de Orden: El primer filtro no es 'Assembly Code'.";
            if (f1.FilterType != ScheduleFilterType.Equal)
                return "Error Filtro 1: Debe ser 'equals'.";
            if (GetFilterValue(f1) != expectedCode)
                return $"Error Filtro 1: El valor es '{GetFilterValue(f1)}' pero se esperaba '{expectedCode}'.";

            // Validar Filtro 2 (EMPRESA)
            var f2 = filters[1];
            var field2 = _doc.GetElement(f2.FieldId) as ScheduleField;
            if (field2 == null || field2.GetName() != "EMPRESA")
                return "Error de Orden: El segundo filtro no es 'EMPRESA'.";
            if (f2.FilterType != ScheduleFilterType.Equal)
                return "Error Filtro 2: Debe ser 'equals'.";
            if (GetFilterValue(f2) != "RNG")
                return "Error Filtro 2: El valor no es 'RNG'.";

            return "Error desconocido.";
        }
    }
}