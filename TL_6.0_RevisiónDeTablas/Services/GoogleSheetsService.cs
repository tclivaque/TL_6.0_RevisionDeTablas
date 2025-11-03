using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;

namespace TL60_RevisionDeTablas.Services
{
    public class GoogleSheetsService
    {
        private SheetsService _service;

        // CRÍTICO: Usar el nombre de tu archivo JSON
        private static readonly string CREDENTIALS_PATH = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "revitsheetsintegration-89c34b39c2ae.json" // Nombre de tu Contexto
        );

        public GoogleSheetsService()
        {
            InitializeService();
        }

        private void InitializeService()
        {
            try
            {
                GoogleCredential credential;
                [cite_start] using (var stream = new FileStream(CREDENTIALS_PATH, FileMode.Open, FileAccess.Read)) [cite: 239]
                {
                    credential = GoogleCredential.FromStream(stream)
                        .CreateScoped(new[] { SheetsService.Scope.Spreadsheets });
                }

                _service = new SheetsService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    [cite_start]// IMPORTANTE: NO usar acentos 
                    ApplicationName = "TL60RevisionDeTablas"
                });
            }
            catch (Exception ex)
            {
                [cite_start] throw new Exception($"Error al inicializar Google Sheets. Asegúrate que el archivo JSON esté en la carpeta del DLL: {ex.Message}", ex); [cite: 242, 327]
            }
        }

        [cite_start]// ... (El resto de métodos ReadData, WriteData, GetCellValue son idénticos a la plantilla) ... [cite: 243-255]

        [cite_start]// (Pegar aquí los métodos ReadData, WriteData y GetCellValue de la plantilla [cite: 243-255])

        /// <summary>
        /// Lee datos de una hoja
        /// </summary>
        public IList<IList<object>> ReadData(string spreadsheetId, string range)
        {
            try
            {
                [cite_start] var request = _service.Spreadsheets.Values.Get(spreadsheetId, range); [cite: 244]
                var response = request.Execute();
                return response.Values ?? new List<IList<object>>();
            }
            catch (Exception ex)
            {
                [cite_start] throw new Exception($"Error al leer datos de Google Sheets: {ex.Message}", ex); [cite: 245]
            }
        }

        /// <summary>
        /// Escribe datos en una celda (batch update)
        /// </summary>
        public void WriteData(
            string spreadsheetId,
            Dictionary<string, string> cellValues)
        {
            try
            {
                [cite_start] var dataToWrite = new List<ValueRange>(); [cite: 247]
                foreach (var kvp in cellValues)
                {
                    var valueRange = new ValueRange
                    {
                        Range = kvp.Key,
                        [cite_start]Values = new List<IList<object>> { new List<object> { kvp.Value } }[cite: 249]
                    };
                    dataToWrite.Add(valueRange);
                }

                var batchRequest = new BatchUpdateValuesRequest
                {
                    Data = dataToWrite,
                    ValueInputOption = "RAW"
                [cite_start]
                }; [cite: 251]
                
                var request = _service.Spreadsheets.Values.BatchUpdate(
                    batchRequest,
                    spreadsheetId
                );
                [cite_start] request.Execute(); [cite: 253]
            }
            catch (Exception ex)
            {
                [cite_start] throw new Exception($"Error al escribir datos en Google Sheets: {ex.Message}", ex); [cite: 253]
            }
        }

        /// <summary>
        /// Obtiene valor de celda (helper)
        /// </summary>
        public static string GetCellValue(IList<object> row, int columnIndex)
        {
            if (row == null || columnIndex >= row.Count)
                return string.Empty;
            [cite_start] return row[columnIndex]?.ToString()?.Trim() ?? string.Empty; [cite: 255]
        }
    }
}