using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace CSVStudio.Views
{
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            this.InitializeComponent();
        }

        private void DropArea_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Rilascia per caricare";
            e.DragUIOverride.IsCaptionVisible = true;
        }

        private async void DropArea_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0 && items[0] is StorageFile file)
                {
                    LoadCsv(file.Path);
                }
            }
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".csv");
            picker.FileTypeFilter.Add(".txt");

            if (App.MainWindow is null) return;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                LoadCsv(file.Path);
            }
        }

        private void LoadCsv(string path)
        {
            try
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    DetectDelimiter = true,
                    HasHeaderRecord = true,
                    BadDataFound = null,
                    MissingFieldFound = null
                };

                using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                using var csv = new CsvReader(reader, config);

                csv.Read();
                csv.ReadHeader();
                var headers = csv.HeaderRecord ?? Array.Empty<string>();

                if (headers.Length == 0)
                {
                    FileInfoText.Text = "Nessuna colonna trovata nel file.";
                    PreviewGrid.ItemsSource = null;
                    PreviewGrid.Columns.Clear();
                    return;
                }

                // 1. Rigenera le colonne manualmente
                PreviewGrid.Columns.Clear();
                PreviewGrid.AutoGenerateColumns = false;

                for (int i = 0; i < headers.Length; i++)
                {
                    var col = new CommunityToolkit.WinUI.UI.Controls.DataGridTextColumn
                    {
                        Header = headers[i],
                        Binding = new Microsoft.UI.Xaml.Data.Binding
                        {
                            Path = new PropertyPath($"[{SanitizeKey(headers[i])}]")
                        }
                    };
                    PreviewGrid.Columns.Add(col);
                }

                // 2. Costruisce righe come ExpandoObject (dynamic)
                var rows = new List<IDictionary<string, object>>();
                int rowCount = 0;
                while (csv.Read())
                {
                    var row = new ExpandoObject() as IDictionary<string, object>;
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var key = SanitizeKey(headers[i]);
                        row[key] = csv.GetField(i) ?? string.Empty;
                    }
                    rows.Add(row);
                    rowCount++;
                    if (rowCount >= 1000) break; // preview first 1000 rows
                }

                PreviewGrid.ItemsSource = rows;
                FileInfoText.Text = $"{Path.GetFileName(path)}  •  {rowCount} righe (preview)  •  {headers.Length} colonne";
            }
            catch (Exception ex)
            {
                FileInfoText.Text = $"Errore lettura: {ex.Message}";
                PreviewGrid.ItemsSource = null;
                PreviewGrid.Columns.Clear();
            }
        }

        // ExpandoObject non accetta chiavi con caratteri strani: ripuliamole
        private static string SanitizeKey(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "col";
            var chars = input.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray();
            var result = new string(chars);
            if (char.IsDigit(result[0])) result = "_" + result;
            return result;
        }
    }
}