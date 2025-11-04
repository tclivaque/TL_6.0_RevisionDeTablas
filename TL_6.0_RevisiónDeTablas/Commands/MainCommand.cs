// Commands/MainCommand.cs
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
                var sheetsService = new GoogleSheetsService(); // Aún se inicializa por si se usa

                // 2. Procesar elementos (Lógica de auditoría principal)
                var processor = new ScheduleProcessor(doc, sheetsService);
                List<ElementData> elementosData = processor.ProcessElements();

                // 3. Construir datos de diagnóstico (Ahora crea múltiples filas por elemento)
                var diagnosticBuilder = new DiagnosticDataBuilder();
                List<DiagnosticRow> diagnosticRows = diagnosticBuilder.BuildDiagnosticRows(elementosData);

                // 4. Preparar los Writers Asíncronos
                var writerAsync = new ScheduleUpdateAsync(); // (NOMBRE ACTUALIZADO)
                var viewActivator = new ViewActivatorAsync();

                // 5. Crear y mostrar ventana Modeless
                var mainWindow = new MainWindow(
                    diagnosticRows,
                    elementosData,
                    doc,
                    writerAsync, // (NOMBRE ACTUALIZADO)
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