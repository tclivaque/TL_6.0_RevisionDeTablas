// Services/ScheduleEmptinessChecker.cs
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TL60_RevisionDeTablas.Services
{
    /// <summary>
    /// Clase optimizada para verificar si una tabla de planificación está vacía.
    /// Usa estrategia de "Early Exit" para detenerse al encontrar el primer elemento válido.
    /// Basado en el código Python de replicación de schedules, pero optimizado para solo detectar vacío/no vacío.
    /// </summary>
    public class ScheduleEmptinessChecker
    {
        private readonly Document _doc;

        public ScheduleEmptinessChecker(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        /// <summary>
        /// Verifica si la tabla está vacía (no tiene elementos que cumplan los filtros).
        /// Retorna true si está vacía, false si tiene al menos 1 elemento válido.
        /// </summary>
        public bool IsScheduleEmpty(ViewSchedule schedule)
        {
            try
            {
                ScheduleDefinition definition = schedule.Definition;
                if (definition == null) return true;

                // PASO 1: Recolectar elementos usando el contexto del schedule
                var allElements = CollectElementsFromSchedule(schedule, definition);
                if (allElements == null || allElements.Count == 0)
                    return true; // No hay elementos en absoluto

                // PASO 2: Obtener información de filtros
                var filtersInfo = AnalyzeScheduleFilters(definition);

                // PASO 3: Obtener información de campos (necesaria para evaluar filtros)
                var fieldsInfo = AnalyzeScheduleFields(definition);

                // PASO 4: EARLY EXIT - Buscar el PRIMER elemento que pase los filtros
                return !FindFirstValidElement(allElements, filtersInfo, fieldsInfo);
            }
            catch (Exception)
            {
                // En caso de error, asumir que NO está vacía (evitar falsos positivos)
                return false;
            }
        }

        #region Recolección de Elementos

        private List<ElementInfo> CollectElementsFromSchedule(ViewSchedule schedule, ScheduleDefinition definition)
        {
            var allElements = new List<ElementInfo>();

            try
            {
                ElementId categoryId = definition.CategoryId;
                BuiltInCategory builtInCategory = (BuiltInCategory)categoryId.IntegerValue;

                // Recolectar del modelo actual usando contexto del schedule
                FilteredElementCollector scheduleCollector = new FilteredElementCollector(_doc, schedule.Id)
                    .OfCategory(builtInCategory)
                    .WhereElementIsNotElementType();

                foreach (Element elem in scheduleCollector)
                {
                    allElements.Add(new ElementInfo { Element = elem, IsFromLink = false });
                }

                // Recolectar de vínculos (si la tabla los incluye)
                var linkInstances = new FilteredElementCollector(_doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .ToElements();

                foreach (RevitLinkInstance linkInstance in linkInstances)
                {
                    try
                    {
                        Document linkDoc = linkInstance.GetLinkDocument();
                        if (linkDoc == null) continue;

                        FilteredElementCollector linkCollector = new FilteredElementCollector(linkDoc)
                            .OfCategory(builtInCategory)
                            .WhereElementIsNotElementType();

                        foreach (Element elem in linkCollector)
                        {
                            allElements.Add(new ElementInfo { Element = elem, IsFromLink = true, LinkDocument = linkDoc });
                        }
                    }
                    catch { /* Ignorar errores en vínculos */ }
                }

                return allElements;
            }
            catch
            {
                return new List<ElementInfo>();
            }
        }

        #endregion

        #region Análisis de Filtros

        private List<FilterInfo> AnalyzeScheduleFilters(ScheduleDefinition definition)
        {
            var filtersInfo = new List<FilterInfo>();

            try
            {
                int filterCount = definition.GetFilterCount();

                for (int i = 0; i < filterCount; i++)
                {
                    try
                    {
                        ScheduleFilter filter = definition.GetFilter(i);
                        ScheduleFieldId fieldId = filter.FieldId;

                        // Buscar el índice del campo
                        int? fieldIndex = null;
                        for (int j = 0; j < definition.GetFieldCount(); j++)
                        {
                            ScheduleField field = definition.GetField(j);
                            if (field.FieldId.IntegerValue == fieldId.IntegerValue)
                            {
                                fieldIndex = j;
                                break;
                            }
                        }

                        if (fieldIndex == null) continue;

                        var filterInfo = new FilterInfo
                        {
                            FieldIndex = fieldIndex.Value,
                            FilterType = filter.FilterType,
                            StringValue = filter.IsStringValue ? filter.GetStringValue() : null,
                            DoubleValue = filter.IsDoubleValue ? (double?)filter.GetDoubleValue() : null,
                            ElementIdValue = filter.IsElementIdValue ? filter.GetElementIdValue() : null
                        };

                        filtersInfo.Add(filterInfo);
                    }
                    catch { /* Ignorar filtros problemáticos */ }
                }
            }
            catch { /* Ignorar errores */ }

            return filtersInfo;
        }

        #endregion

        #region Análisis de Campos

        private List<FieldInfo> AnalyzeScheduleFields(ScheduleDefinition definition)
        {
            var fieldsInfo = new List<FieldInfo>();

            try
            {
                for (int i = 0; i < definition.GetFieldCount(); i++)
                {
                    try
                    {
                        ScheduleField field = definition.GetField(i);

                        var fieldInfo = new FieldInfo
                        {
                            Index = i,
                            ParameterId = field.ParameterId,
                            FieldName = field.GetName()
                        };

                        fieldsInfo.Add(fieldInfo);
                    }
                    catch { /* Ignorar campos problemáticos */ }
                }
            }
            catch { /* Ignorar errores */ }

            return fieldsInfo;
        }

        #endregion

        #region Early Exit - Búsqueda del Primer Elemento Válido

        /// <summary>
        /// Busca el PRIMER elemento que pase todos los filtros.
        /// Retorna true si encuentra al menos uno, false si ninguno pasa.
        /// </summary>
        private bool FindFirstValidElement(List<ElementInfo> elements, List<FilterInfo> filters, List<FieldInfo> fields)
        {
            foreach (var elementInfo in elements)
            {
                bool elementPasses = true;

                // Aplicar cada filtro
                foreach (var filter in filters)
                {
                    if (!elementPasses) break; // Early exit si ya falló un filtro

                    try
                    {
                        if (filter.FieldIndex >= fields.Count) continue;

                        FieldInfo field = fields[filter.FieldIndex];
                        string elementValue = GetParameterValue(elementInfo, field);

                        // Evaluar filtro
                        if (!EvaluateFilter(elementValue, filter))
                        {
                            elementPasses = false;
                        }
                    }
                    catch
                    {
                        elementPasses = false;
                    }
                }

                // EARLY EXIT: Si encontramos UN elemento válido, retornamos inmediatamente
                if (elementPasses)
                {
                    return true; // Encontrado! La tabla NO está vacía
                }
            }

            // Si llegamos aquí, ningún elemento pasó los filtros
            return false; // La tabla está vacía
        }

        #endregion

        #region Obtener Valor de Parámetro

        private string GetParameterValue(ElementInfo elementInfo, FieldInfo fieldInfo)
        {
            try
            {
                Element element = elementInfo.Element;
                ElementId paramId = fieldInfo.ParameterId;

                // Casos especiales para BuiltInParameters comunes
                int paramIdInt = paramId.IntegerValue;

                // Assembly Code
                if (paramIdInt == (int)BuiltInParameter.UNIFORMAT_CODE)
                {
                    Document doc = elementInfo.IsFromLink ? elementInfo.LinkDocument : _doc;
                    ElementId typeId = element.GetTypeId();
                    if (typeId != null && typeId != ElementId.InvalidElementId)
                    {
                        ElementType elemType = doc.GetElement(typeId) as ElementType;
                        if (elemType != null)
                        {
                            Parameter param = elemType.get_Parameter(BuiltInParameter.UNIFORMAT_CODE);
                            if (param != null && param.HasValue)
                                return param.AsString() ?? string.Empty;
                        }
                    }
                    return string.Empty;
                }

                // Length
                if (paramIdInt == (int)BuiltInParameter.CURVE_ELEM_LENGTH)
                {
                    Parameter param = element.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                    if (param != null && param.HasValue)
                        return param.AsValueString() ?? string.Empty;
                    return string.Empty;
                }

                // Buscar parámetro por ID en instancia
                foreach (Parameter param in element.Parameters)
                {
                    if (param.Id.IntegerValue == paramIdInt)
                    {
                        if (!param.HasValue) return string.Empty;

                        switch (param.StorageType)
                        {
                            case StorageType.String:
                                return param.AsString() ?? string.Empty;
                            case StorageType.Integer:
                                return param.AsInteger().ToString();
                            case StorageType.Double:
                                return param.AsValueString() ?? param.AsDouble().ToString();
                            case StorageType.ElementId:
                                return param.AsElementId().IntegerValue.ToString();
                            default:
                                return string.Empty;
                        }
                    }
                }

                // Buscar en tipo si no se encontró en instancia
                Document currentDoc = elementInfo.IsFromLink ? elementInfo.LinkDocument : _doc;
                ElementId typeId2 = element.GetTypeId();
                if (typeId2 != null && typeId2 != ElementId.InvalidElementId)
                {
                    ElementType elemType = currentDoc.GetElement(typeId2) as ElementType;
                    if (elemType != null)
                    {
                        foreach (Parameter param in elemType.Parameters)
                        {
                            if (param.Id.IntegerValue == paramIdInt)
                            {
                                if (!param.HasValue) return string.Empty;

                                switch (param.StorageType)
                                {
                                    case StorageType.String:
                                        return param.AsString() ?? string.Empty;
                                    case StorageType.Integer:
                                        return param.AsInteger().ToString();
                                    case StorageType.Double:
                                        return param.AsValueString() ?? param.AsDouble().ToString();
                                    case StorageType.ElementId:
                                        return param.AsElementId().IntegerValue.ToString();
                                    default:
                                        return string.Empty;
                                }
                            }
                        }
                    }
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        #endregion

        #region Evaluación de Filtros

        private bool EvaluateFilter(string elementValue, FilterInfo filter)
        {
            try
            {
                string filterValue = filter.StringValue ?? filter.DoubleValue?.ToString() ?? filter.ElementIdValue?.IntegerValue.ToString() ?? string.Empty;

                switch (filter.FilterType)
                {
                    case ScheduleFilterType.Equal:
                        return elementValue == filterValue;

                    case ScheduleFilterType.NotEqual:
                        return elementValue != filterValue;

                    case ScheduleFilterType.Contains:
                        return elementValue.Contains(filterValue);

                    case ScheduleFilterType.NotContains:
                        return !elementValue.Contains(filterValue);

                    case ScheduleFilterType.BeginsWith:
                        return elementValue.StartsWith(filterValue);

                    case ScheduleFilterType.NotBeginsWith:
                        return !elementValue.StartsWith(filterValue);

                    case ScheduleFilterType.EndsWith:
                        return elementValue.EndsWith(filterValue);

                    case ScheduleFilterType.NotEndsWith:
                        return !elementValue.EndsWith(filterValue);

                    case ScheduleFilterType.GreaterThan:
                        if (double.TryParse(elementValue, out double elemVal) && filter.DoubleValue.HasValue)
                            return elemVal > filter.DoubleValue.Value;
                        return false;

                    case ScheduleFilterType.GreaterThanOrEqual:
                        if (double.TryParse(elementValue, out double elemVal2) && filter.DoubleValue.HasValue)
                            return elemVal2 >= filter.DoubleValue.Value;
                        return false;

                    case ScheduleFilterType.LessThan:
                        if (double.TryParse(elementValue, out double elemVal3) && filter.DoubleValue.HasValue)
                            return elemVal3 < filter.DoubleValue.Value;
                        return false;

                    case ScheduleFilterType.LessThanOrEqual:
                        if (double.TryParse(elementValue, out double elemVal4) && filter.DoubleValue.HasValue)
                            return elemVal4 <= filter.DoubleValue.Value;
                        return false;

                    case ScheduleFilterType.HasValue:
                        return !string.IsNullOrEmpty(elementValue);

                    case ScheduleFilterType.HasNoValue:
                        return string.IsNullOrEmpty(elementValue);

                    default:
                        return true; // Si no podemos evaluar, asumir que pasa
                }
            }
            catch
            {
                return true; // En caso de error, asumir que pasa (evitar falsos negativos)
            }
        }

        #endregion

        #region Clases Auxiliares

        private class ElementInfo
        {
            public Element Element { get; set; }
            public bool IsFromLink { get; set; }
            public Document LinkDocument { get; set; }
        }

        private class FilterInfo
        {
            public int FieldIndex { get; set; }
            public ScheduleFilterType FilterType { get; set; }
            public string StringValue { get; set; }
            public double? DoubleValue { get; set; }
            public ElementId ElementIdValue { get; set; }
        }

        private class FieldInfo
        {
            public int Index { get; set; }
            public ElementId ParameterId { get; set; }
            public string FieldName { get; set; }
        }

        #endregion
    }
}