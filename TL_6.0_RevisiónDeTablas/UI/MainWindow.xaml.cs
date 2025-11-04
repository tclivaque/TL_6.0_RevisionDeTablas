using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls; // (¡¡¡CORREGIDO!!!) Esta línea faltaba
using Autodesk.Revit.DB;
using TL60_RevisionDeTablas.Models;
using TL60_RevisionDeTablas.Services;
using System.IO; // Para el guardado de Excel

namespace TL60_RevisionDeTablas.UI
{
    public partial class MainWindow : Window
    {
        private readonly List<DiagnosticRow> _diagnosticRows;
        private readonly List<ElementData> _elementosData;
        private readonly Document _doc;
        private readonly ScheduleWriterAsync _writerAsync;
        private readonly ViewActivatorAsync _viewActivator; // (NUEVO) Handler para abrir vista

        public MainWindow(
            List<DiagnosticRow> diagnosticRows,
            List<ElementData> elementosData,
            Document doc,
            ScheduleWriterAsync writerAsync,
            ViewActivatorAsync viewActivator) // (NUEVO) Recibe el handler
        {
            InitializeComponent();
            _diagnosticRows = diagnosticRows;
            _elementosData = elementosData;
            _doc = doc;
            _writerAsync = writerAsync;
            _viewActivator = viewActivator; // (NUEVO) Almacena el handler

            LoadData();
        }

        private void LoadData()
        {
            DiagnosticDataGrid.ItemsSource = null;
            DiagnosticDataGrid.ItemsSource = _diagnosticRows;

            int correctos = _diagnosticRows.Count(r => r.Estado == EstadoParametro.Correcto);
            int aCorregir = _diagnosticRows.Count(r => r.Estado == EstadoParametro.Corregir);
            int errores = _diagnosticRows.Count(r => r.Estado == EstadoParametro.Error);

            StatsText.Text = $"Total: {_diagnosticRows.Count} | ✅ Correctos: {correctos} | 🔧 A Corregir: {aCorregir} | ❌ Errores: {errores}";
        }

        /// <summary>
        /// Botón "Corregir"
        /// </summary>
        private async void CorregirButton_Click(object sender, RoutedEventArgs e)
        {
            // (ACTUALIZADO) Filtrar por estado de la fila
            var elementosACorregir = _diagnosticRows
                .Where(r => r.Estado == EstadoParametro.Corregir)
                .Select(r => _elementosData.FirstOrDefault(ed => ed.ElementId == r.ElementId))
                .Where(ed => ed != null)
                .ToList();

            if (elementosACorregir.Count == 0)
            {
                MessageBox.Show("No hay elementos para corregir.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                CorregirButton.IsEnabled = false;
                CorregirButton.Content = "Corrigiendo...";

                ProcessingResult writeResult = await Task.Run(() =>
                {
                    return _writerAsync.WriteFiltersAsync(_doc, elementosACorregir);
                });

                CorregirButton.IsEnabled = true;
                CorregirButton.Content = "Corregir Filtros";

                if (!writeResult.Exitoso)
                {
                    MessageBox.Show(writeResult.Mensaje, "Error al Corregir", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show(writeResult.Mensaje, "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.Close(); // Cerrar al éxito
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fatal: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                CorregirButton.IsEnabled = true;
                CorregirButton.Content = "Corregir Filtros";
            }
        }

        /// <summary>
        /// (NUEVO) Botón "Exportar a Excel"
        /// </summary>
        private void ExportarButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"Diagnostico_Filtros_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                Filter = "Archivos de Excel (*.xlsx)|*.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var exportService = new ExcelExportService();
                    byte[] fileBytes = exportService.ExportToExcel(_diagnosticRows);
                    File.WriteAllBytes(dialog.FileName, fileBytes);

                    MessageBox.Show($"Reporte exportado exitosamente a:\n{dialog.FileName}", "Exportación Completa", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al exportar el archivo: {ex.Message}", "Error de Exportación", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// (NUEVO) Botón "Ojo" 👁️ para abrir la vista
        /// </summary>
        private void AbrirViewButton_Click(object sender, RoutedEventArgs e)
        {
            // Obtener la fila (DiagnosticRow) del botón en el que se hizo clic
            var button = sender as Button;
            var row = button?.DataContext as DiagnosticRow;
            if (row == null || row.ElementId == null || row.ElementId == ElementId.InvalidElementId)
            {
                return;
            }

            try
            {
                // Llamar al handler asíncrono para activar la vista
                _viewActivator.ActivateViewAsync(row.ElementId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al intentar abrir la vista: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CerrarButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}