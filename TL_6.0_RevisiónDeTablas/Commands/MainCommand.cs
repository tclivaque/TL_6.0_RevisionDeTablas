using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System;
using System.Collections.Generic;
using TL60_RevisionDeTablas.Models;
using TL60_RevisionDeTablas.Services;
using TL60_RevisionDeTablas.UI;

namespace TL60_RevisionDeTablas.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MainCommand : IExternalCommand
    {
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

                // 2. Procesar elementos
                var processor = new ScheduleProcessor(doc, sheetsService);
                List<ElementData> elementosData = processor.ProcessElements();

                // 3. Construir datos de diagnóstico
                var diagnosticBuilder = new DiagnosticDataBuilder();
                [cite_start] List<DiagnosticRow> diagnosticRows = diagnosticBuilder.BuildDiagnosticRows(elementosData); [cite: 104, 257]

                // 4. Preparar el Writer Asíncrono
                var writerAsync = new ScheduleWriterAsync();

                // 5. Crear y mostrar ventana Modeless
                var mainWindow = new MainWindow(
                    diagnosticRows,
                    elementosData,
                    doc,
                    writerAsync // Pasar el writer a la ventana
                );

                mainWindow.Show(); // Modeless [cite: 105]

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