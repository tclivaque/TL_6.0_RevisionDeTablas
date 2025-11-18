using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TL60_RevisionDeTablas.UI;
using TL60_RevisionDeTablas.Plugins.COBie.UI;
using TL60_RevisionDeTablas.Plugins.Tablas.UI;
using TL60_RevisionDeTablas.Models;
using TL60_RevisionDeTablas.Core;
using TL60_RevisionDeTablas.Plugins.COBie.Services;
using TL60_RevisionDeTablas.Plugins.COBie.Models;

namespace TL60_RevisionDeTablas.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class UnifiedAuditCommand : IExternalCommand
    {
        private const string COBIE_SPREADSHEET_ID = "14bYBONt68lfM-sx6iIJxkYExXS0u7sdgijEScL3Ed3Y";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uidoc = uiApp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // ====================================
                // PARTE 1: PROCESAR PLUGIN COBie
                // ====================================
                CobiePluginControl cobieControl = null;
                try
                {
                    cobieControl = ProcessCobiePlugin(doc);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Error en COBie", $"Error al procesar plugin COBie:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
                }

                // ====================================
                // PARTE 2: PLUGIN TABLAS (Por implementar)
                // ====================================
                // TODO: Implementar ProcessTablasPlugin cuando se complete la integración
                // El plugin Tablas requiere más parámetros y lógica que se integrará después
                TablasPluginControl tablasControl = null;

                // ====================================
                // PARTE 3: MOSTRAR VENTANA UNIFICADA
                // ====================================
                if (cobieControl != null)
                {
                    var unifiedWindow = new UnifiedWindow(cobieControl, tablasControl);
                    unifiedWindow.Show();
                    return Result.Succeeded;
                }
                else
                {
                    TaskDialog.Show("Error", "No se pudo cargar el plugin COBie.");
                    return Result.Failed;
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error General", $"Ocurrió un error:\n{ex.Message}\n{ex.StackTrace}");
                message = ex.Message;
                return Result.Failed;
            }
        }

        private CobiePluginControl ProcessCobiePlugin(Document doc)
        {
            // Inicializar servicios
            var sheetsService = new GoogleSheetsService();
            sheetsService.Initialize();

            // Leer configuración
            var configManager = new ConfigurationManager(sheetsService);
            CobieConfig config = configManager.ReadConfiguration(COBIE_SPREADSHEET_ID);

            // Leer definiciones de parámetros
            var paramReader = new ParameterDefinitionReader(sheetsService);
            List<ParameterDefinition> definitions = paramReader.ReadParameterDefinitions(config.SpreadsheetId);

            // Leer matriz de control
            var controlMatrix = new ControlMatrixService(sheetsService);
            controlMatrix.LoadMatrix(config.SpreadsheetId);
            string docTitleWithoutExt = Path.GetFileNameWithoutExtension(doc.Title);
            ModelPermissions permissions = controlMatrix.GetPermissions(docTitleWithoutExt);

            // Procesar elementos
            var elementosData = new List<ElementData>();
            var facilityProcessor = new FacilityProcessor(doc, definitions);
            var floorProcessor = new FloorProcessor(doc, definitions, sheetsService, config);
            var roomProcessor = new RoomProcessor(doc, definitions, sheetsService, config);
            var elementProcessor = new ElementProcessor(doc, definitions, sheetsService, config);

            // Procesar según permisos (código simplificado del CobieCommand original)
            if (permissions.IsAllowed("FACILITY"))
            {
                var facilityData = facilityProcessor.ProcessFacility();
                if (facilityData != null) elementosData.Add(facilityData);
            }

            if (permissions.IsAllowed("FLOOR"))
            {
                var floorsData = floorProcessor.ProcessFloors();
                elementosData.AddRange(floorsData);
            }

            if (permissions.IsAllowed("SPACE"))
            {
                roomProcessor.Initialize();
                var roomsData = roomProcessor.ProcessRooms();
                elementosData.AddRange(roomsData);
            }

            bool typeAllowed = permissions.IsAllowed("TYPE");
            bool componentAllowed = permissions.IsAllowed("COMPONENT");

            if (typeAllowed || componentAllowed)
            {
                elementProcessor.Initialize();
                var elementsDataMep = elementProcessor.ProcessElements();
                if (!typeAllowed) elementsDataMep.RemoveAll(e => e.GrupoCOBie == "TYPE");
                if (!componentAllowed) elementsDataMep.RemoveAll(e => e.GrupoCOBie == "COMPONENT");
                elementosData.AddRange(elementsDataMep);
            }

            // Aplicar gráficos
            var viewManager = new ViewGraphicsManager(doc, doc.ActiveView, config.Categorias);
            if (viewManager.IsView3D())
            {
                viewManager.ApplyGraphicsAndIsolate(elementosData);
            }

            // Construir datos de diagnóstico (usar fully qualified name para evitar ambigüedad)
            var diagnosticBuilder = new TL60_RevisionDeTablas.Plugins.COBie.Services.DiagnosticDataBuilder();
            var diagnosticRows = diagnosticBuilder.BuildDiagnosticRows(elementosData);

            // Crear UserControl
            var cobieControl = new CobiePluginControl(
                diagnosticRows, elementosData, doc,
                facilityProcessor, floorProcessor, roomProcessor, elementProcessor, config.Categorias);

            return cobieControl;
        }
    }
}
