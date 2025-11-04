// Services/ExcelExportService.cs
using OfficeOpenXml; // Este 'using' es el correcto
using System.Collections.Generic;
using System.IO;
using TL60_RevisionDeTablas.Models;
// Se eliminó 'using OfficeOpenXml.License;' que era incorrecto

namespace TL60_RevisionDeTablas.Services
{
    public class ExcelExportService
    {
        public ExcelExportService()
        {
            // (CORREGIDO) Revertido al código original.
            // Mi intento anterior de "modernizar" esto fue incorrecto para la v8.2.1.
            // Esta es la línea correcta, aunque genere un aviso de obsoleto.
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public byte[] ExportToExcel(List<DiagnosticRow> diagnosticRows)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("DiagnosticoFiltros");

                // Encabezados (Todos en mayúsculas)
                worksheet.Cells[1, 1].Value = "ID";
                worksheet.Cells[1, 2].Value = "ASSEMBLY CODE";
                worksheet.Cells[1, 3].Value = "NOMBRE DE TABLA";
                worksheet.Cells[1, 4].Value = "ENCABEZADO";
                worksheet.Cells[1, 5].Value = "VALOR ACTUAL";
                worksheet.Cells[1, 6].Value = "VALOR CORRECTO";
                worksheet.Cells[1, 7].Value = "ESTADO";
                worksheet.Cells[1, 8].Value = "MENSAJE";

                // Aplicar formato a los encabezados
                using (var range = worksheet.Cells[1, 1, 1, 8])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }

                // Escribir datos
                for (int i = 0; i < diagnosticRows.Count; i++)
                {
                    var row = diagnosticRows[i];
                    int rowIndex = i + 2;

                    worksheet.Cells[rowIndex, 1].Value = row.IdMostrar;
                    worksheet.Cells[rowIndex, 2].Value = row.CodigoIdentificacion;
                    worksheet.Cells[rowIndex, 3].Value = row.Descripcion;
                    worksheet.Cells[rowIndex, 4].Value = row.NombreParametro;
                    worksheet.Cells[rowIndex, 5].Value = row.ValorActual;
                    worksheet.Cells[rowIndex, 6].Value = row.ValorCorregido;
                    worksheet.Cells[rowIndex, 7].Value = row.Estado.ToString();
                    worksheet.Cells[rowIndex, 8].Value = row.Mensaje;
                }

                worksheet.Cells[1, 1, 1, 8].AutoFitColumns();

                return package.GetAsByteArray();
            }
        }
    }
}