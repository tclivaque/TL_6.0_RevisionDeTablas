// Commands/MainCommand.cs
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using TL60_RevisionDeTablas.Models;
using TL60_RevisionDeTablas.Services;
using TL60_RevisionDeTablas.UI;

namespace TL60_RevisionDeTablas.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MainCommand : IExternalCommand
    {
        private const string SPREADSHEET_ID = "14bYBONt68lfM-sx6iIJxkYExXS0u7sdgijEScL3Ed3Y";

        private static readonly List<string> NOMBRES_WIP = new List<string>
        {
            "TL", "TITO", "PDONTADENEA", "ANDREA", "EFRAIN",
            "PROYECTOSBIM", "ASISTENTEBIM", "LUIS", "DIEGO", "JORGE", "MIGUEL"
        };

        private static readonly Regex _acRegex = new Regex(@"^C\.(\d{2,3}\.)+\d{2,3}");

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // 1. Inicializar Servicios
                var sheetsService = new GoogleSheetsService();
                string docTitle = Path.GetFileNameWithoutExtension(doc.Title);

                var manualScheduleService = new ManualScheduleService(sheetsService, docTitle);
                manualScheduleService.LoadClassificationData(SPREADSHEET_ID);

                var processor = new ScheduleProcessor(doc, sheetsService, manualScheduleService, SPREADSHEET_ID);

                var elementosData = new List<ElementData>();

                // 2. Obtener todas las tablas
                var allSchedules = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Schedules)
                    .WhereElementIsNotElementType()
                    .Cast<ViewSchedule>();

                // ==========================================================
                // ===== LÓGICA DE BIFURCACIÓN (MODIFICADA) =====
                // ==========================================================

                foreach (ViewSchedule view in allSchedules)
                {
                    if (view == null || view.Definition == null) continue;

                    string viewName = view.Name;
                    string viewNameUpper = viewName.ToUpper();

                    // -----------------------------------------------------------------
                    // FILTRO 1: ¿Nombre empieza con "C."?
                    // -----------------------------------------------------------------
                    if (viewNameUpper.StartsWith("C."))
                    {
                        // -----------------------------------------------------------------
                        // FILTRO 2 (REQ 4.A/B): ¿Es "copy", "copia" o WIP?
                        // -----------------------------------------------------------------
                        if (viewNameUpper.Contains("COPY") || viewNameUpper.Contains("COPIA"))
                        {
                            // ==========================================================
                            // ===== CORRECCIÓN: Evitar Falso Positivo "COPIA" =====
                            // ==========================================================
                            string grupoVista = view.LookupParameter("GRUPO DE VISTA")?.AsString() ?? string.Empty;
                            string subGrupoVista = view.LookupParameter("SUBGRUPO DE VISTA")?.AsString() ?? string.Empty;

                            // El estado corregido de "COPIA" es "REVISAR" y "" (vacío)
                            if (grupoVista.Equals("REVISAR", StringComparison.OrdinalIgnoreCase) &&
                                string.IsNullOrEmpty(subGrupoVista))
                            {
                                continue; // Ya está corregido, no reportar.
                            }
                            // ==========================================================

                            elementosData.Add(processor.CreateCopyReclassifyJob(view));
                            continue; // Fin del análisis para esta tabla
                        }

                        if (NOMBRES_WIP.Any(name => viewNameUpper.Contains(name)))
                        {
                            elementosData.Add(processor.CreateWipReclassifyJob(view));
                            continue; // Fin del análisis para esta tabla
                        }

                        // Extraer Assembly Code del nombre
                        Match acMatch = _acRegex.Match(viewName);
                        string assemblyCode = acMatch.Success ? acMatch.Value : "INVALID_AC";

                        // -----------------------------------------------------------------
                        // FILTRO 3 (Req G-Sheets): ¿Es "MANUAL" o "REVIT"?
                        // -----------------------------------------------------------------
                        string scheduleType = manualScheduleService.GetScheduleType(assemblyCode);

                        if (scheduleType.Equals("MANUAL", StringComparison.OrdinalIgnoreCase))
                        {
                            // (Corrección Falso Positivo "MANUAL")
                            string grupoVista = view.LookupParameter("GRUPO DE VISTA")?.AsString() ?? string.Empty;
                            string subGrupoVista = view.LookupParameter("SUBGRUPO DE VISTA")?.AsString() ?? string.Empty;

                            if (grupoVista.Equals("REVISAR", StringComparison.OrdinalIgnoreCase) &&
                                subGrupoVista.Equals("METRADO MANUAL", StringComparison.OrdinalIgnoreCase))
                            {
                                continue; // Ya está corregido, no reportar.
                            }

                            elementosData.Add(processor.CreateManualRenamingJob(view));
                            continue; // Fin del análisis para esta tabla
                        }

                        // -----------------------------------------------------------------
                        // FILTRO 4 (Grupo Vista): ¿Es "Metrado" o "Soporte"?
                        // -----------------------------------------------------------------

                        // Es "REVIT" (o no encontrado)
                        Parameter groupParam = view.LookupParameter("GRUPO DE VISTA");
                        string groupValue = groupParam?.AsString() ?? string.Empty;

                        if (groupValue.StartsWith("C.", StringComparison.OrdinalIgnoreCase))
                        {
                            // CASO A: "Tabla de Metrados" -> Auditar
                            elementosData.Add(processor.ProcessSingleElement(view, assemblyCode));
                        }
                        else
                        {
                            // CASO B: "Tabla de Soporte" -> Renombrar
                            elementosData.Add(processor.CreateRenamingJob(view));
                        }
                    }
                }
                // ==========================================================
                // ===== FIN DE LÓGICA DE BIFURCACIÓN =====
                // ==========================================================

                // (NUEVO) 3. POST-PROCESO: Auditoría de Duplicados
                RunDuplicateCheck(elementosData);

                // 4. Construir datos de diagnóstico
                var diagnosticBuilder = new DiagnosticDataBuilder();
                List<DiagnosticRow> diagnosticRows = diagnosticBuilder.BuildDiagnosticRows(elementosData);

                // 5. Preparar los Writers Asíncronos
                var writerAsync = new ScheduleUpdateAsync();
                var viewActivator = new ViewActivatorAsync();

                // 6. Crear y mostrar ventana Modeless
                var mainWindow = new MainWindow(
                    diagnosticRows,
                    elementosData,
                    doc,
                    writerAsync,
                    viewActivator
                );

                mainWindow.Show(); // Modeless

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Error: {ex.Message}\nStackTrace: {ex.StackTrace}";
                TaskDialog.Show("Error", message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// (NUEVO) Ejecuta la auditoría de duplicados
        /// </summary>
        private void RunDuplicateCheck(List<ElementData> elementosData)
        {
            // 1. Filtrar solo tablas de metrados (las que tienen auditorías de FILTRO)
            var metradosTablas = elementosData
                .Where(ed => ed.AuditResults.Any(ar => ar.AuditType == "FILTRO"))
                .ToList();

            // 2. Agrupar por Assembly Code
            var groupedByAC = metradosTablas.GroupBy(ed => ed.CodigoIdentificacion);

            foreach (var acGroup in groupedByAC)
            {
                if (acGroup.Count() < 2) continue; // No hay duplicados de A.C.

                // 3. Sub-agrupar por Tipo (Material vs Cantidades)
                var groupedByType = acGroup.GroupBy(ed =>
                    (ed.Element as ViewSchedule)?.Definition.IsMaterialTakeoff ?? false
                );

                foreach (var typeGroup in groupedByType)
                {
                    if (typeGroup.Count() < 2) continue;

                    // 4. Sub-agrupar por Categoría
                    var groupedByCategory = typeGroup.GroupBy(ed =>
                        (ed.Element as ViewSchedule)?.Definition.CategoryId.ToString() ?? "NONE"
                    );

                    foreach (var categoryGroup in groupedByCategory)
                    {
                        if (categoryGroup.Count() < 2) continue;

                        // 5. ¡Duplicado Encontrado! Marcar todos
                        foreach (var elementData in categoryGroup)
                        {
                            elementData.AuditResults.Add(new AuditItem
                            {
                                AuditType = "DUPLICADO",
                                Estado = EstadoParametro.Vacio, // Estado Advertencia
                                Mensaje = "Advertencia: Tabla duplicada (mismo A.C., tipo y categoría).",
                                ValorActual = elementData.Nombre,
                                ValorCorregido = "N/A",
                                IsCorrectable = false
                            });

                            elementData.DatosCompletos = false;
                        }
                    }
                }
            }
        }
    }
}