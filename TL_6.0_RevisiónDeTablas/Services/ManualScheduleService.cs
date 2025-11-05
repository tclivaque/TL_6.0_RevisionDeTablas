// Services/ManualScheduleService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using TL60_RevisionDeTablas.Services;

namespace TL60_RevisionDeTablas.Services
{
    /// <summary>
    /// Lee las hojas "Matriz UniClass" para determinar si una tabla es de metrado MANUAL o REVIT.
    /// </summary>
    public class ManualScheduleService
    {
        private readonly GoogleSheetsService _sheetsService;
        private readonly string _docTitle;
        private readonly Dictionary<string, string> _classificationCache;

        private const string SHEET_ACTIVO = "Matriz UniClass - ACTIVO";
        private const string SHEET_SITIO = "Matriz UniClass - SITIO";

        // Códigos de proyecto que SÓLO leen de ACTIVO
        private readonly List<string> _activoOnlyCodes = new List<string>
        {
            "469", "470", "471", "472", "473", "474", "475", "476",
            "477", "478", "479", "480", "455", "456"
        };

        // Código de proyecto que lee AMBAS
        private const string _dualReadCode = "200114-CCC02-MO-ES-000410";

        public ManualScheduleService(GoogleSheetsService sheetsService, string docTitle)
        {
            _sheetsService = sheetsService ?? throw new ArgumentNullException(nameof(sheetsService));
            _docTitle = docTitle ?? string.Empty;
            _classificationCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Carga los datos de clasificación desde Google Sheets basado en el título del documento.
        /// </summary>
        public void LoadClassificationData(string spreadsheetId)
        {
            var sheetsToRead = new List<string>();

            // 1. Determinar qué hojas leer
            if (_activoOnlyCodes.Any(code => _docTitle.Contains(code)))
            {
                sheetsToRead.Add(SHEET_SITIO); // Se invirtió la lógica según conversación
            }
            else if (_docTitle.Contains(_dualReadCode))
            {
                sheetsToRead.Add(SHEET_SITIO); // Se lee SITO
                sheetsToRead.Add(SHEET_ACTIVO); // Y ACTIVO
            }
            else
            {
                sheetsToRead.Add(SHEET_ACTIVO); // Default: ACTIVO
            }

            // 2. Leer los datos y construir el caché
            foreach (var sheetName in sheetsToRead)
            {
                try
                {
                    // Leer desde Columna D (índice 3) hasta Columna L (índice 11)
                    // El rango D:L tiene 9 columnas (0-8)
                    var data = _sheetsService.ReadData(spreadsheetId, $"'{sheetName}'!D:L");
                    if (data == null || data.Count <= 1) continue; // Saltar encabezado o si está vacía

                    foreach (var row in data.Skip(1)) // Saltar encabezado
                    {
                        if (row.Count < 9) continue; // Asegurarse que la fila llega hasta la Col L

                        string assemblyCode = GoogleSheetsService.GetCellValue(row, 0); // Col D (Índice 0 en rango D:L)
                        string metradoType = GoogleSheetsService.GetCellValue(row, 8); // Col L (Índice 8 en rango D:L)

                        if (string.IsNullOrWhiteSpace(assemblyCode) || string.IsNullOrWhiteSpace(metradoType))
                            continue;

                        // Añadir o sobreescribir. Si se leen ambas hojas, ACTIVO tiene prioridad.
                        _classificationCache[assemblyCode.Trim()] = metradoType.Trim();
                    }
                }
                catch (Exception ex)
                {
                    // Si una hoja falla (ej. no existe), solo se ignora.
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

            // Si es "REVIT" o si no se encontró, se trata como REVIT.
            return "REVIT";
        }
    }
}