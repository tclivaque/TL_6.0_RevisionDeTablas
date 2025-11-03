using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.DB;
using TL60_RevisionDeTablas.Models;
using TL60_RevisionDeTablas.Services;

namespace TL60_RevisionDeTablas.UI
{
    public partial class MainWindow : Window
    {
        private readonly List<DiagnosticRow> _diagnosticRows;
        private readonly List<ElementData> _elementosData;
        private readonly Document _doc;
        private readonly ScheduleWriterAsync _writerAsync;

        // (MODIFICADO) Constructor adaptado
        public MainWindow(
            List<DiagnosticRow> diagnosticRows,
            List<ElementData> elementosData,
            Document doc,
            ScheduleWriterAsync writerAsync)
        {
            InitializeComponent();
            _diagnosticRows = diagnosticRows;
            _elementosData = elementosData;
            _doc = doc;
            _writerAsync = writerAsync;

            LoadData();
        }

        private void LoadData()
        {
            // Cargar datos en DataGrid
            DiagnosticDataGrid.ItemsSource = null;
            DiagnosticDataGrid.ItemsSource = _diagnosticRows;

            [cite_start]// Actualizar estadísticas [cite: 147]
            int correctos = _elementosData.Count(e => e.DatosCompletos);
            int aCorregir = _elementosData.Count(e => e.ParametrosActualizar.Count > 0);
            int errores = _elementosData.Count(e => e.Mensajes.Count > 0 && e.ParametrosActualizar.ContainsKey("Filtros") && e.ParametrosActualizar["Filtros"] == "Error de Formato");

            StatsText.Text = $"Total: {_elementosData.Count} | ✅ Correctos: {correctos} | 🔧 A Corregir: {aCorregir} | ❌ Errores: {errores}";
        }

        /// <summary>
        /// Botón "Corregir" - Escribe parámetros de forma asíncrona
        /// </summary>
        private async void CorregirButton_Click(object sender, RoutedEventArgs e)
        {
            // Solo procesar los que necesitan corrección
            var elementosACorregir = _elementosData
                .Where(e => e.ParametrosActualizar.Count > 0 && e.Mensajes.Count > 0)
                .ToList();

            if (elementosACorregir.Count == 0)
            {
                MessageBox.Show("No hay elementos para corregir.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                [cite_start] CorregirButton.IsEnabled = false; [cite: 93]
                [cite_start] CorregirButton.Content = "Corrigiendo..."; [cite: 94]

                ProcessingResult writeResult = await Task.Run(() =>
                {
                // Llama al writer asíncrono
                [cite_start] return _writerAsync.WriteFiltersAsync(_doc, elementosACorregir); [cite: 94]
                });

            [cite_start] CorregirButton.IsEnabled = true; [cite: 95]
                [cite_start] CorregirButton.Content = "Corregir Filtros"; [cite: 96]

                if (!writeResult.Exitoso)
            {
                [cite_start] MessageBox.Show(writeResult.Mensaje, "Error al Corregir", MessageBoxButton.OK, MessageBoxImage.Error); [cite: 97]
                }
            else
            {
                MessageBox.Show(writeResult.Mensaje, "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                // (Opcional) Recargar datos y cerrar
                this.Close();
            }
        }
            catch (Exception ex)
            {
                [cite_start] MessageBox.Show($"Error fatal: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); [cite: 99]
        CorregirButton.IsEnabled = true;
                [cite_start] CorregirButton.Content = "Corregir Filtros"; [cite: 101]
    }
}

private void CerrarButton_Click(object sender, RoutedEventArgs e)
{
    [cite_start] this.Close(); [cite: 102]
        }
    }
}