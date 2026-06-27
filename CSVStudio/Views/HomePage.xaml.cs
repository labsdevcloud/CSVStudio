using CommunityToolkit.WinUI.UI.Controls;
using CSVStudio.Models;
using CSVStudio.Services;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace CSVStudio.Views
{
    public sealed partial class HomePage : Page
    {
        private readonly CsvDetectionService _detection = new();

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
                // 1. AUTO-DETECTION
                var info = _detection.Detect(path);

                // 2. Apertura con encoding e separator rilevati
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = info.Separator.ToString(),
                    Quote = info.Quote,
                    HasHeaderRecord = info.HasHeader,
                    BadDataFound = null,
                    MissingFieldFound = null
                };

                using var reader = new StreamReader(path, info.Encoding, detectEncodingFromByteOrderMarks: true);
                using var csv = new CsvReader(reader, config);

                // 3. Lettura header (se presenti)
                string[] headers;
                if (info.HasHeader)
                {
                    csv.Read();
                    csv.ReadHeader();
                    headers = csv.HeaderRecord ?? Array.Empty<string>();
                }
                else
                {
                    // Leggi una riga per capire il numero di colonne
                    csv.Read();
                    int colCount = csv.Parser.Count;
                    headers = Enumerable.Range(1, colCount).Select(i => $"Column {i}").ToArray();
                    // Re-apri il file per non saltare la prima riga
                    reader.BaseStream.Seek(0, SeekOrigin.Begin);
                    reader.DiscardBufferedData();
                    csv.Read();
                }

                if (headers.Length == 0)
                {
                    FileInfoText.Text = "Nessuna colonna trovata.";
                    DetectionInfoText.Text = string.Empty;
                    PreviewGrid.ItemsSource = null;
                    PreviewGrid.Columns.Clear();
                    return;
                }

                // 4. Detect duplicati negli header
                var duplicates = headers
                    .GroupBy(h => h)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();
                info.HasDuplicateHeaders = duplicates.Count > 0;
                info.HasEmptyHeaders = headers.Any(string.IsNullOrWhiteSpace);

                // 5. Configura colonne DataGrid
                PreviewGrid.Columns.Clear();
                PreviewGrid.AutoGenerateColumns = false;

                // Per gestire chiavi duplicate negli ExpandoObject, le rendiamo uniche
                var uniqueKeys = MakeUniqueKeys(headers);

                for (int i = 0; i < headers.Length; i++)
                {
                    var col = new DataGridTextColumn
                    {
                        Header = headers[i],
                        Binding = new Binding
                        {
                            Path = new PropertyPath($"[{uniqueKeys[i]}]")
                        }
                    };
                    PreviewGrid.Columns.Add(col);
                }

                // 6. Lettura righe
                var rows = new List<IDictionary<string, object>>();
                int rowCount = 0;

                if (info.HasHeader)
                {
                    while (csv.Read())
                    {
                        AddRow(csv, headers, uniqueKeys, rows);
                        rowCount++;
                        if (rowCount >= 1000) break;
                    }
                }
                else
                {
                    // Aggiungi la prima riga già letta + le successive
                    AddRow(csv, headers, uniqueKeys, rows);
                    rowCount++;
                    while (csv.Read() && rowCount < 1000)
                    {
                        AddRow(csv, headers, uniqueKeys, rows);
                        rowCount++;
                    }
                }

                info.PreviewRows = rowCount;
                info.ColumnCount = headers.Length;

                PreviewGrid.ItemsSource = rows;

                // 7. UI: aggiorna pannelli info
                FileInfoText.Text = $"{info.FileName}  •  {info.PreviewRows} righe (preview)  •  {info.ColumnCount} colonne";

                var detectionLines = new List<string>
                {
                    $"🔤 Encoding: {info.EncodingName}  ({(info.EncodingConfidence * 100):0}%)",
                    $"✂️ Separator: {info.SeparatorName}",
                    $"🪧 Quote: {info.QuoteName}",
                    $"🏷️ Header: {(info.HasHeader ? "detected" : "not detected")}"
                };

                if (info.HasDuplicateHeaders)
                    detectionLines.Add($"⚠️ Duplicate columns: {string.Join(", ", duplicates)}");
                if (info.HasEmptyHeaders)
                    detectionLines.Add("⚠️ Empty column names detected");

                DetectionInfoText.Text = string.Join("\n", detectionLines);
            }
            catch (Exception ex)
            {
                FileInfoText.Text = $"Errore lettura: {ex.Message}";
                DetectionInfoText.Text = string.Empty;
                PreviewGrid.ItemsSource = null;
                PreviewGrid.Columns.Clear();
            }
        }

        private static void AddRow(CsvReader csv, string[] headers, string[] uniqueKeys, List<IDictionary<string, object>> rows)
        {
            var row = new ExpandoObject() as IDictionary<string, object>;
            for (int i = 0; i < headers.Length; i++)
            {
                row[uniqueKeys[i]] = csv.GetField(i) ?? string.Empty;
            }
            rows.Add(row);
        }

        private static string[] MakeUniqueKeys(string[] headers)
        {
            var keys = new string[headers.Length];
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < headers.Length; i++)
            {
                var key = SanitizeKey(headers[i]);
                if (counts.TryGetValue(key, out int n))
                {
                    counts[key] = n + 1;
                    keys[i] = $"{key}_{n + 1}";
                }
                else
                {
                    counts[key] = 1;
                    keys[i] = key;
                }
            }
            return keys;
        }

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