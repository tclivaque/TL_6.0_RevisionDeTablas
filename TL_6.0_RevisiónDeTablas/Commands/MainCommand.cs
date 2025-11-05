// Commands/MainCommand.cs
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System;
using System.Collections.Generic;
using System.Linq; // Necesario para .ToList()
using System.IO; // (NUEVO) Necesario para Path
using TL60_RevisionDeTablas.Models;
using TL60_RevisionDeTablas.Services;
using TL60_RevisionDeTablas.UI;

namespace TL60_RevisionDeTablas.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MainCommand : IExternalCommand
    {
        // (NUEVO) ID del Spreadsheet de donde se leerán las matrices
        private const string SPREADSHEET_ID = "14bYBONt68lfM-sx6iIJxkYExXS0u7sdgijEScL3Ed3Y";

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
                var processor = new ScheduleProcessor(doc, sheetsService);

                // (NUEVO) Inicializar el servicio de clasificación Manual/Revit
                string docTitle = Path.GetFileNameWithoutExtension(doc.Title);
                var manualScheduleService = new ManualScheduleService(sheetsService, docTitle);
                manualScheduleService.LoadClassificationData(SPREADSHEET_ID); // Lee G-Sheets

                var elementosData = new List<ElementData>();

                // 2. Obtener todas las tablas
                var allSchedules = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Schedules)
                    .WhereElementIsNotElementType()
                    .Cast<ViewSchedule>();

                // ==========================================================
                // ===== INICIO DE LÓGICA DE BIFURCACIÓN (MODIFICADA) =====
                // ==========================================================

                foreach (ViewSchedule view in allSchedules)
                {
                    if (view == null || view.Definition == null) continue;

                    // PRIMER FILTRO (Sin cambios): El nombre debe empezar con "C."
                    if (view.Name.StartsWith("C.", StringComparison.OrdinalIgnoreCase))
                    {
                        // (NUEVO) PASO 2: Clasificar REVIT vs MANUAL

                        // Obtener el Assembly Code del nombre de la tabla
                        string assemblyCode = view.Name.Split(new[] { " - " }, StringSplitOptions.None)[0].Trim();
                        string scheduleType = manualScheduleService.GetScheduleType(assemblyCode);

                        if (scheduleType.Equals("MANUAL", StringComparison.OrdinalIgnoreCase))
                        {
                            // CASO NUEVO: Es MANUAL, crear trabajo de reclasificación
                            elementosData.Add(processor.CreateManualRenamingJob(view));
                        }
                        else
                        {
                            // CASO "REVIT" (o no encontrado): Ejecutar lógica original
                            // SEGUNDO FILTRO (Lógica original): Revisar "GRUPO DE VISTA"
                            Parameter groupParam = view.LookupParameter("GRUPO DE VISTA");
                            string groupValue = groupParam?.AsString() ?? string.Empty;

                            if (groupValue.StartsWith("C.", StringComparison.OrdinalIgnoreCase))
                            {
                                // CASO A: "Tabla de Metrados" -> Auditar
                                elementosData.Add(processor.ProcessSingleElement(view));
                            }
                            else
                            {
                                // CASO B: "Tabla de Soporte" -> Renombrar
                                elementosData.Add(processor.CreateRenamingJob(view));
                            }
                        }
                    }
                }
                // ==========================================================
                // ===== FIN DE LÓGICA DE BIFURCACIÓN =====
                // ==========================================================

                // 3. Construir datos de diagnóstico
                var diagnosticBuilder = new DiagnosticDataBuilder();
                List<DiagnosticRow> diagnosticRows = diagnosticBuilder.BuildDiagnosticRows(elementosData);

                // 4. Preparar los Writers Asíncronos
                var writerAsync = new ScheduleUpdateAsync();
                var viewActivator = new ViewActivatorAsync();

                // 5. Crear y mostrar ventana Modeless
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
    }
}