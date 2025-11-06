// Services/ManualScheduleService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using TL60_RevisionDeTablas.Services;

namespace TL60_RevisionDeTablas.Services
{
    /// <summary>
    /// Lee las hojas "Matriz UniClass" para determinar si una tabla es de metrado MANUAL o REVIT.
    /// (MODIFICADO: Ya no lee la descripción, solo el tipo de metrado)
    /// </summary>
    public class ManualScheduleService
    {
        private readonly GoogleSheetsService _sheetsService;
        private readonly string _docTitle;
        // (MODIFICADO) El caché ahora solo guarda el tipo (Col L)
        private readonly Dictionary<string, string> _classificationCache;

        private const string SHEET_ACTIVO = "Matriz UniClass - ACTIVO";
        private const string SHEET_SITIO = "Matriz UniClass - SITIO";

        // Códigos de proyecto que SÓLO leen de SITIO
        private readonly List<string> _sitioOnlyCodes = new List<string>
        {
            "469", "470", "471", "472", "473", "474", "475", "476",
            "477", "478", "479", "480", "455", "456"
        };

        private const string _dualReadCode = "200114-CCC02-MO-ES-000410";

        public ManualScheduleService(GoogleSheetsService sheetsService, string docTitle)
        {
            _sheetsService = sheetsService ?? throw new ArgumentNullException(nameof(sheetsService));
            _docTitle = docTitle ?? string.Empty;
            _classificationCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public void LoadClassificationData(string spreadsheetId)
        {
            var sheetsToRead = new List<string>();

            if (_sitioOnlyCodes.Any(code => _docTitle.Contains(code)))
            {
                sheetsToRead.Add(SHEET_SITIO);
            }
            else if (_docTitle.Contains(_dualReadCode))
            {
                sheetsToRead.Add(SHEET_SITIO);
                sheetsToRead.Add(SHEET_ACTIVO);
            }
            else
            {
                sheetsToRead.Add(SHEET_ACTIVO); // Default
            }

            foreach (var sheetName in sheetsToRead)
            {
                try
                {
                    // Leer rango D:L (Col D=AC, Col L=Tipo)
                    var data = _sheetsService.ReadData(spreadsheetId, $"'{sheetName}'!D:L");
                    if (data == null || data.Count <= 1) continue;

                    foreach (var row in data.Skip(1))
                    {
                        if (row.Count < 9) continue; // Asegurar que la fila llega hasta la Col L

                        string assemblyCode = GoogleSheetsService.GetCellValue(row, 0); // Col D (Índice 0 en rango D:L)
                        string metradoType = GoogleSheetsService.GetCellValue(row, 8); // Col L (Índice 8 en rango D:L)

                        if (string.IsNullOrWhiteSpace(assemblyCode))
                            continue;

                        _classificationCache[assemblyCode.Trim()] = string.IsNullOrWhiteSpace(metradoType) ? "REVIT" : metradoType.Trim();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al leer {sheetName}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Obtiene el tipo de metrado (MANUAL o REVIT) para un Assembly Code.
        /// </summary>
        public string GetScheduleType(string assemblyCode)
        {
            if (string.IsNullOrWhiteSpace(assemblyCode))
                return "REVIT"; // Default

            if (_classificationCache.TryGetValue(assemblyCode, out string type))
            {
                if (type.Equals("MANUAL", StringComparison.OrdinalIgnoreCase))
                    return "MANUAL";
            }

            return "REVIT";
        }
    }
}