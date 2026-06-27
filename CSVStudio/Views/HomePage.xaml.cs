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
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace CSVStudio.Views
{
    public sealed partial class HomePage : Page
    {
        private readonly CsvDetectionService _detection = new();
        private readonly CsvStatisticsService _statistics = new();

        // Stato corrente
        private string? _currentFilePath;
        private CsvFileInfo? _currentInfo;
        private string[] _currentHeaders = Array.Empty<string>();
        private string[] _currentUniqueKeys = Array.Empty<string>();
        private List<IDictionary<string, object>> _currentRows = new();

        public HomePage()
        {
            this.InitializeComponent();
        }

        // ─────────────────────────────────────────────────
        // DRAG & DROP / BROWSE
        // ─────────────────────────────────────────────────
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
                    LoadFile(file.Path);
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
                LoadFile(file.Path);
            }
        }

        // ─────────────────────────────────────────────────
        // LOAD: detection automatica
        // ─────────────────────────────────────────────────
        private void LoadFile(string path)
        {
            try
            {
                _currentFilePath = path;
                var info = _detection.Detect(path);
                _currentInfo = info;

                // Sync UI controls con i valori rilevati
                SyncOverrideControls(info);

                // Carica i dati
                LoadCsvWithInfo(info);
            }
            catch (Exception ex)
            {
                ShowError($"Errore: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────
        // APPLY: ricarica con parametri manuali
        // ─────────────────────────────────────────────────
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath) || _currentInfo is null) return;

            try
            {
                var info = new CsvFileInfo
                {
                    FilePath = _currentFilePath,
                    FileName = Path.GetFileName(_currentFilePath),
                    FileSizeBytes = new FileInfo(_currentFilePath).Length,
                    Encoding = GetSelectedEncoding(),
                    EncodingName = GetSelectedEncodingName(),
                    Separator = GetSelectedSeparator(),
                    SeparatorName = (EncodingNameFromCombo(SeparatorCombo) ?? "Custom"),
                    Quote = GetSelectedQuote(),
                    QuoteName = GetSelectedQuote() == '"' ? "Double quote" : "Single quote",
                    HasHeader = HasHeaderCheck.IsChecked ?? true
                };

                _currentInfo = info;
                LoadCsvWithInfo(info);
            }
            catch (Exception ex)
            {
                ShowError($"Errore apply: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────
        // CORE: carica dati con info
        // ─────────────────────────────────────────────────
        private void LoadCsvWithInfo(CsvFileInfo info)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = info.Separator.ToString(),
                Quote = info.Quote,
                HasHeaderRecord = info.HasHeader,
                BadDataFound = null,
                MissingFieldFound = null
            };

            using var reader = new StreamReader(info.FilePath, info.Encoding, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, config);

            // Header
            string[] headers;
            if (info.HasHeader)
            {
                csv.Read();
                csv.ReadHeader();
                headers = csv.HeaderRecord ?? Array.Empty<string>();
            }
            else
            {
                csv.Read();
                int colCount = csv.Parser.Count;
                headers = Enumerable.Range(1, colCount).Select(i => $"Column {i}").ToArray();
                reader.BaseStream.Seek(0, SeekOrigin.Begin);
                reader.DiscardBufferedData();
                csv.Read();
            }

            if (headers.Length == 0)
            {
                ShowError("Nessuna colonna trovata.");
                return;
            }

            var duplicates = headers
                .GroupBy(h => h)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            info.HasDuplicateHeaders = duplicates.Count > 0;
            info.HasEmptyHeaders = headers.Any(string.IsNullOrWhiteSpace);

            // Colonne DataGrid
            PreviewGrid.Columns.Clear();
            var uniqueKeys = MakeUniqueKeys(headers);

            for (int i = 0; i < headers.Length; i++)
            {
                var col = new DataGridTextColumn
                {
                    Header = headers[i],
                    Binding = new Binding { Path = new PropertyPath($"[{uniqueKeys[i]}]") }
                };
                PreviewGrid.Columns.Add(col);
            }

            // Righe
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

            _currentHeaders = headers;
            _currentUniqueKeys = uniqueKeys;
            _currentRows = rows;

            PreviewGrid.ItemsSource = rows;

            // Statistiche
            var stats = _statistics.Compute(headers, uniqueKeys, rows);
            StatsGrid.ItemsSource = stats;

            // UI info
            UpdateInfoPanels(info, duplicates);
            ApplyButton.IsEnabled = true;
        }

        // ─────────────────────────────────────────────────
        // UI HELPERS
        // ─────────────────────────────────────────────────
        private void UpdateInfoPanels(CsvFileInfo info, List<string> duplicates)
        {
            FileNameText.Text = info.FileName;
            FileStatsText.Text = $"{info.PreviewRows} righe (preview) • {info.ColumnCount} colonne • {FormatSize(info.FileSizeBytes)}";

            var lines = new List<string>
            {
                $"Encoding: {info.EncodingName} ({(info.EncodingConfidence * 100):0}%)",
                $"Separator: {info.SeparatorName}",
                $"Quote: {info.QuoteName}",
                $"Header: {(info.HasHeader ? "detected" : "not detected")}"
            };

            if (info.HasDuplicateHeaders)
                lines.Add($"⚠ Duplicate columns: {string.Join(", ", duplicates)}");
            if (info.HasEmptyHeaders)
                lines.Add("⚠ Empty column names");

            DetectionInfoText.Text = string.Join("\n", lines);
        }

        private void SyncOverrideControls(CsvFileInfo info)
        {
            SelectComboByTag(EncodingCombo, EncodingTagFromEncoding(info.Encoding));
            SelectComboByTag(SeparatorCombo, info.Separator == '\t' ? "\t" : info.Separator.ToString());
            SelectComboByTag(QuoteCombo, info.Quote.ToString());
            HasHeaderCheck.IsChecked = info.HasHeader;
        }

        private void ShowError(string msg)
        {
            FileNameText.Text = "Errore";
            FileStatsText.Text = msg;
            DetectionInfoText.Text = string.Empty;
            PreviewGrid.ItemsSource = null;
            PreviewGrid.Columns.Clear();
            StatsGrid.ItemsSource = null;
        }

        // ─────────────────────────────────────────────────
        // OVERRIDE: lettura ComboBox
        // ─────────────────────────────────────────────────
        private Encoding GetSelectedEncoding()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var tag = EncodingTagFromCombo(EncodingCombo);
            return tag switch
            {
                "utf-8" => new UTF8Encoding(false),
                "utf-8-bom" => new UTF8Encoding(true),
                "utf-16" => Encoding.Unicode,
                "utf-16BE" => Encoding.BigEndianUnicode,
                "windows-1252" => Encoding.GetEncoding(1252),
                "iso-8859-1" => Encoding.GetEncoding("iso-8859-1"),
                _ => Encoding.UTF8
            };
        }

        private string GetSelectedEncodingName() =>
            (EncodingCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "UTF-8";

        private char GetSelectedSeparator()
        {
            var tag = EncodingTagFromCombo(SeparatorCombo);
            return tag switch
            {
                "\t" => '\t',
                ";" => ';',
                "|" => '|',
                _ => ','
            };
        }

        private char GetSelectedQuote()
        {
            var tag = EncodingTagFromCombo(QuoteCombo);
            return tag == "'" ? '\'' : '"';
        }

        // ─────────────────────────────────────────────────
        // UTILS
        // ─────────────────────────────────────────────────
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

        private static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }
            return $"{size:0.#} {units[unit]}";
        }

        private static string? EncodingTagFromCombo(ComboBox combo) =>
            (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        private static string? EncodingNameFromCombo(ComboBox combo) =>
            (combo.SelectedItem as ComboBoxItem)?.Content?.ToString();

        private static void SelectComboByTag(ComboBox combo, string tag)
        {
            foreach (var item in combo.Items)
            {
                if (item is ComboBoxItem cbi && cbi.Tag?.ToString() == tag)
                {
                    combo.SelectedItem = cbi;
                    return;
                }
            }
            // Fallback: primo elemento
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private static string EncodingTagFromEncoding(Encoding enc)
        {
            return enc.WebName.ToLowerInvariant() switch
            {
                "utf-8" => "utf-8",
                "utf-16" => "utf-16",
                "utf-16be" => "utf-16BE",
                "windows-1252" => "windows-1252",
                "iso-8859-1" => "iso-8859-1",
                _ => "utf-8"
            };
        }
    }
}