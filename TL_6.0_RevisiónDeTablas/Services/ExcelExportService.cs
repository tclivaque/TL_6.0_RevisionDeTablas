using OfficeOpenXml;
using System.Collections.Generic;
using System.IO;
using TL60_RevisionDeTablas.Models;

namespace TL60_RevisionDeTablas.Services
{
    /// <summary>
    /// Servicio para exportar el diagnóstico a un archivo Excel .xlsx
    /// </summary>
    public class ExcelExportService
    {
        public ExcelExportService()
        {
            // Requerido por la licencia de EPPlus (Ignora la advertencia de obsoleto)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public byte[] ExportToExcel(List<DiagnosticRow> diagnosticRows)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("DiagnosticoFiltros");

                // Encabezados
                worksheet.Cells[1, 1].Value = "ID";
                worksheet.Cells[1, 2].Value = "Assembly Code";
                worksheet.Cells[1, 3].Value = "Nombre de Tabla";
                worksheet.Cells[1, 4].Value = "Encabezado";
                worksheet.Cells[1, 5].Value = "Valor Actual";
                worksheet.Cells[1, 6].Value = "Valor Correcto";
                worksheet.Cells[1, 7].Value = "Estado";
                worksheet.Cells[1, 8].Value = "Mensaje";

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
                    int rowIndex = i + 2; // Empezar desde la fila 2

                    worksheet.Cells[rowIndex, 1].Value = row.IdMostrar;
                    worksheet.Cells[rowIndex, 2].Value = row.CodigoIdentificacion;
                    worksheet.Cells[rowIndex, 3].Value = row.Descripcion;
                    worksheet.Cells[rowIndex, 4].Value = row.NombreParametro;
                    worksheet.Cells[rowIndex, 5].Value = row.ValorActual;
                    worksheet.Cells[rowIndex, 6].Value = row.ValorCorregido;
                    worksheet.Cells[rowIndex, 7].Value = row.Estado.ToString();
                    worksheet.Cells[rowIndex, 8].Value = row.Mensaje;
                }

                // (¡¡¡CORREGIDO!!!)
                // Se eliminó la línea "worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();"
                // para evitar el error de "Método no encontrado".
                // Puedes auto-ajustar manualmente las columnas 1 a 8 si lo deseas:
                worksheet.Cells[1, 1, 1, 8].AutoFitColumns();


                return package.GetAsByteArray();
            }
        }
    }
}