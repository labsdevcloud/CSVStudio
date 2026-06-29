using CommunityToolkit.WinUI.UI.Controls;
using CSVStudio.Models;
using CSVStudio.Services;
using CSVStudio.Services.Operations;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace CSVStudio.Views
{
    public sealed partial class HomePage : Page
    {
        // ─────────────────────────────────────────────────
        // SERVICES
        // ─────────────────────────────────────────────────
        private readonly CsvDetectionService _detection = new();
        private readonly CsvStatisticsService _statistics = new();
        private readonly CsvExportService _export = new();

        // ─────────────────────────────────────────────────
        // STATE
        // ─────────────────────────────────────────────────
        private string? _currentFilePath;
        private CsvFileInfo? _currentInfo;

        // Dataset corrente (modificato dalle operazioni) e originale (per Reset)
        private CsvDataset _currentDataset = new();
        private CsvDataset? _originalDataset;

        // Editing: mappa colonna→chiave + cattura dell'editor corrente (per il write-back manuale)
        private readonly Dictionary<DataGridColumn, string> _editColumnKeys = new();
        private TextBox? _editingTextBox;
        private IDictionary<string, object>? _editingRow;
        private string? _editingKey;
        private string? _editingOldValue;   // valore della cella PRIMA della modifica (per undo)

        // Undo / Redo: snapshot dell'intero dataset (pila limitata)
        private readonly List<CsvDataset> _undo = new();
        private readonly List<CsvDataset> _redo = new();
        private const int MaxUndoSteps = 50;

        // Operazioni disponibili
        private readonly List<ICsvOperation> _availableOperations = new()
        {
            new TrimSpacesOperation(),
            new RemoveEmptyRowsOperation(),
            new RemoveDuplicateRowsOperation(),
            new RemoveDuplicateColumnsOperation(),
            new ChangeCaseOperation(CaseMode.Upper),
            new ChangeCaseOperation(CaseMode.Lower),
            new ChangeCaseOperation(CaseMode.Title)
        };

        // ─────────────────────────────────────────────────
        // CTOR
        // ─────────────────────────────────────────────────
        public HomePage()
        {
            this.InitializeComponent();
            OperationsList.ItemsSource = _availableOperations;
        }

        // ─────────────────────────────────────────────────
        // DRAG & DROP / BROWSE
        // ─────────────────────────────────────────────────
        private void DropArea_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Drop to load";
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
        // LOAD: auto-detection iniziale
        // ─────────────────────────────────────────────────
        private void LoadFile(string path)
        {
            try
            {
                _currentFilePath = path;
                var info = _detection.Detect(path);
                _currentInfo = info;

                SyncOverrideControls(info);
                LoadCsvWithInfo(info);
            }
            catch (Exception ex)
            {
                ShowError($"Error: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────
        // APPLY OVERRIDE: ricarica con parametri scelti
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
                    SeparatorName = EncodingNameFromCombo(SeparatorCombo) ?? "Custom",
                    Quote = GetSelectedQuote(),
                    QuoteName = GetSelectedQuote() == '"' ? "Double quote" : "Single quote",
                    HasHeader = HasHeaderCheck.IsChecked ?? true
                };

                _currentInfo = info;
                LoadCsvWithInfo(info);
            }
            catch (Exception ex)
            {
                ShowError($"Apply error: {ex.Message}");
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
                ShowError("No columns found.");
                return;
            }

            var duplicates = headers
                .GroupBy(h => h)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            info.HasDuplicateHeaders = duplicates.Count > 0;
            info.HasEmptyHeaders = headers.Any(string.IsNullOrWhiteSpace);

            // Colonne DataGrid (Preview = sola lettura, Edit = modificabile)
            var uniqueKeys = MakeUniqueKeys(headers);
            BuildColumns(PreviewGrid, headers, uniqueKeys, editable: false);
            BuildColumns(EditGrid, headers, uniqueKeys, editable: true);

            // Righe
            var rows = new List<IDictionary<string, object>>();
            int rowCount = 0;

            // Opzione B: carichiamo l'INTERO file (niente limite di 1000 righe),
            // così l'editing e l'export coprono tutti i dati.
            if (info.HasHeader)
            {
                while (csv.Read())
                {
                    AddRow(csv, headers, uniqueKeys, rows);
                    rowCount++;
                }
            }
            else
            {
                AddRow(csv, headers, uniqueKeys, rows);
                rowCount++;
                while (csv.Read())
                {
                    AddRow(csv, headers, uniqueKeys, rows);
                    rowCount++;
                }
            }

            info.PreviewRows = rowCount;
            info.ColumnCount = headers.Length;

            PreviewGrid.ItemsSource = rows;
            EditGrid.ItemsSource = rows;

            // Statistiche
            var stats = _statistics.Compute(headers, uniqueKeys, rows);
            StatsGrid.ItemsSource = stats;

            // UI info
            UpdateInfoPanels(info, duplicates);

            // Dataset corrente + snapshot originale
            _currentDataset = new CsvDataset
            {
                Headers = headers,
                UniqueKeys = uniqueKeys,
                Rows = rows,
                Info = info
            };
            _originalDataset = _currentDataset.Clone();

            // Nuovo file = cronologia azzerata
            _undo.Clear();
            _redo.Clear();
            UpdateUndoRedoButtons();

            ApplyButton.IsEnabled = true;
        }

        // ─────────────────────────────────────────────────
        // OPERATIONS
        // ─────────────────────────────────────────────────
        private void OperationApply_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDataset.Rows.Count == 0)
            {
                ShowInfoBar("Load a CSV file first.", InfoBarSeverity.Warning);
                return;
            }

            if (sender is Button btn && btn.Tag is ICsvOperation op)
            {
                try
                {
                    var result = op.Execute(_currentDataset);
                    if (result.Success)
                    {
                        PushUndo();
                        _currentDataset = result.Dataset;
                        RefreshDataView();
                        ShowInfoBar($"{op.Name}: {result.Summary}", InfoBarSeverity.Success);
                    }
                    else
                    {
                        ShowInfoBar($"{op.Name} failed.", InfoBarSeverity.Error);
                    }
                }
                catch (Exception ex)
                {
                    ShowInfoBar($"Error: {ex.Message}", InfoBarSeverity.Error);
                }
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_originalDataset is null)
            {
                ShowInfoBar("Nothing to reset.", InfoBarSeverity.Warning);
                return;
            }

            PushUndo();
            _currentDataset = _originalDataset.Clone();
            RebuildPreviewColumns();
            RefreshDataView();
            ShowInfoBar("Dataset restored.", InfoBarSeverity.Informational);
        }

        private void RefreshDataView()
        {
            // Se il numero di colonne è cambiato, ricostruisco le colonne
            if (PreviewGrid.Columns.Count != _currentDataset.Headers.Length)
                RebuildPreviewColumns();

            // Refresh preview + edit (stessa sorgente dati)
            PreviewGrid.ItemsSource = null;
            PreviewGrid.ItemsSource = _currentDataset.Rows;
            EditGrid.ItemsSource = null;
            EditGrid.ItemsSource = _currentDataset.Rows;

            // Refresh stats
            var stats = _statistics.Compute(
                _currentDataset.Headers,
                _currentDataset.UniqueKeys,
                _currentDataset.Rows);
            StatsGrid.ItemsSource = stats;

            // Refresh sidebar info
            if (_currentInfo != null)
            {
                FileStatsText.Text =
                    $"{_currentDataset.RowCount} righe • {_currentDataset.ColumnCount} colonne • " +
                    $"{FormatSize(_currentInfo.FileSizeBytes)}";
            }
        }

        private void RebuildPreviewColumns()
        {
            BuildColumns(PreviewGrid, _currentDataset.Headers, _currentDataset.UniqueKeys, editable: false);
            BuildColumns(EditGrid, _currentDataset.Headers, _currentDataset.UniqueKeys, editable: true);
        }

        // Costruisce le colonne per una griglia.
        // - Preview (editable=false): DataGridTextColumn in sola lettura.
        // - Edit   (editable=true):  DataGridTemplateColumn con TextBox di editing.
        //   Le colonne sono dinamiche e i dati sono ExpandoObject (binding "a indice"),
        //   che una DataGridTextColumn NON considera modificabile: serve la template column.
        private void BuildColumns(DataGrid grid, string[] headers, string[] uniqueKeys, bool editable)
        {
            grid.Columns.Clear();
            if (editable) _editColumnKeys.Clear();

            for (int i = 0; i < headers.Length; i++)
            {
                if (editable)
                {
                    var col = new DataGridTemplateColumn
                    {
                        Header = headers[i],
                        CellTemplate = BuildCellTemplate(uniqueKeys[i], forEditing: false),
                        CellEditingTemplate = BuildCellTemplate(uniqueKeys[i], forEditing: true)
                    };
                    _editColumnKeys[col] = uniqueKeys[i];   // serve al write-back manuale
                    grid.Columns.Add(col);
                }
                else
                {
                    grid.Columns.Add(new DataGridTextColumn
                    {
                        Header = headers[i],
                        Binding = new Binding
                        {
                            Path = new PropertyPath($"[{uniqueKeys[i]}]"),
                            Mode = BindingMode.OneWay
                        }
                    });
                }
            }
        }

        // Crea via XAML il template di cella: TextBlock per la visualizzazione,
        // TextBox (TwoWay) per la modifica. Le chiavi sono già sanificate
        // (solo lettere/cifre/underscore), quindi sicure dentro il binding.
        private static DataTemplate BuildCellTemplate(string key, bool forEditing)
        {
            string inner = forEditing
                ? $"<TextBox Text=\"{{Binding [{key}], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}\" BorderThickness=\"0\"/>"
                : $"<TextBlock Text=\"{{Binding [{key}]}}\" VerticalAlignment=\"Center\" Margin=\"12,0,12,0\"/>";

            string xaml =
                "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" + inner + "</DataTemplate>";

            return (DataTemplate)XamlReader.Load(xaml);
        }

        private void ShowInfoBar(string message, InfoBarSeverity severity)
        {
            OperationResultBar.Message = message;
            OperationResultBar.Severity = severity;
            OperationResultBar.IsOpen = true;
        }

        // ─────────────────────────────────────────────────
        // UNDO / REDO (snapshot dell'intero dataset)
        // ─────────────────────────────────────────────────
        // Da chiamare PRIMA di ogni modifica ai dati.
        private void PushUndo()
        {
            _undo.Add(_currentDataset.Clone());
            if (_undo.Count > MaxUndoSteps) _undo.RemoveAt(0);   // scarto il più vecchio
            _redo.Clear();
            UpdateUndoRedoButtons();
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e) => Undo();
        private void RedoButton_Click(object sender, RoutedEventArgs e) => Redo();

        private void Undo()
        {
            if (_undo.Count == 0) return;
            _redo.Add(_currentDataset.Clone());
            var snapshot = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            RestoreSnapshot(snapshot);
            UpdateUndoRedoButtons();
            ShowInfoBar("Undo.", InfoBarSeverity.Informational);
        }

        private void Redo()
        {
            if (_redo.Count == 0) return;
            _undo.Add(_currentDataset.Clone());
            var snapshot = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            RestoreSnapshot(snapshot);
            UpdateUndoRedoButtons();
            ShowInfoBar("Redo.", InfoBarSeverity.Informational);
        }

        private void RestoreSnapshot(CsvDataset snapshot)
        {
            _currentDataset = snapshot;
            RebuildPreviewColumns();
            RefreshDataView();
        }

        private void UpdateUndoRedoButtons()
        {
            UndoButton.IsEnabled = _undo.Count > 0;
            RedoButton.IsEnabled = _redo.Count > 0;
        }

        // ─────────────────────────────────────────────────
        // EDIT (Opzione B): righe, colonne, rinomina
        // ─────────────────────────────────────────────────

        // ----- Helpers comuni -----
        private bool HasData() => _currentDataset.Headers.Length > 0;

        private IDictionary<string, object> NewEmptyRow()
        {
            var row = new ExpandoObject() as IDictionary<string, object>;
            foreach (var key in _currentDataset.UniqueKeys)
                row[key] = string.Empty;
            return row;
        }

        private int SelectedRowIndex()
        {
            if (EditGrid.SelectedItem is IDictionary<string, object> sel)
                return _currentDataset.Rows.IndexOf(sel);
            return -1;
        }

        private int CurrentColumnIndex()
        {
            if (EditGrid.CurrentColumn != null)
            {
                int i = EditGrid.Columns.IndexOf(EditGrid.CurrentColumn);
                if (i >= 0) return i;
            }
            return -1;
        }

        // Selezione/scroll vanno rimandati a dopo il layout (altrimenti "Invalid row index").
        private void SelectRowDeferred(IDictionary<string, object> row)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    EditGrid.UpdateLayout();
                    EditGrid.SelectedItem = row;
                    EditGrid.ScrollIntoView(row, null);
                }
                catch { /* tempistiche di layout: ignoriamo */ }
            });
        }

        // ----- RIGHE -----
        private void AddRowAbove_Click(object sender, RoutedEventArgs e)
        {
            if (!HasData()) { ShowInfoBar("Load a CSV file first.", InfoBarSeverity.Warning); return; }
            PushUndo();
            int idx = SelectedRowIndex();
            var row = NewEmptyRow();
            _currentDataset.Rows.Insert(idx < 0 ? 0 : idx, row);
            RefreshDataView();
            SelectRowDeferred(row);
        }

        private void AddRowBelow_Click(object sender, RoutedEventArgs e)
        {
            if (!HasData()) { ShowInfoBar("Load a CSV file first.", InfoBarSeverity.Warning); return; }
            PushUndo();
            int idx = SelectedRowIndex();
            var row = NewEmptyRow();
            _currentDataset.Rows.Insert(idx < 0 ? _currentDataset.Rows.Count : idx + 1, row);
            RefreshDataView();
            SelectRowDeferred(row);
        }

        private void DuplicateRow_Click(object sender, RoutedEventArgs e)
        {
            int idx = SelectedRowIndex();
            if (idx < 0) { ShowInfoBar("Select a row to duplicate.", InfoBarSeverity.Warning); return; }

            PushUndo();
            var src = _currentDataset.Rows[idx];
            var clone = new ExpandoObject() as IDictionary<string, object>;
            foreach (var kv in src) clone[kv.Key] = kv.Value;

            _currentDataset.Rows.Insert(idx + 1, clone);
            RefreshDataView();
            SelectRowDeferred(clone);
        }

        // ----- COLONNE -----
        private void AddColumnBefore_Click(object sender, RoutedEventArgs e)
        {
            if (!HasData()) { ShowInfoBar("Load a CSV file first.", InfoBarSeverity.Warning); return; }
            int i = CurrentColumnIndex();
            InsertColumn(i < 0 ? 0 : i, "New Column", null);
        }

        private void AddColumnAfter_Click(object sender, RoutedEventArgs e)
        {
            if (!HasData()) { ShowInfoBar("Load a CSV file first.", InfoBarSeverity.Warning); return; }
            int i = CurrentColumnIndex();
            InsertColumn(i < 0 ? _currentDataset.Headers.Length : i + 1, "New Column", null);
        }

        private void DuplicateColumn_Click(object sender, RoutedEventArgs e)
        {
            int i = CurrentColumnIndex();
            if (i < 0) { ShowInfoBar("Select a cell in the column to duplicate.", InfoBarSeverity.Warning); return; }
            InsertColumn(i + 1, _currentDataset.Headers[i] + " (copy)", _currentDataset.UniqueKeys[i]);
        }

        // Inserisce una colonna all'indice dato; se copyFromKey != null ne copia i valori.
        private void InsertColumn(int atIndex, string headerLabel, string? copyFromKey)
        {
            PushUndo();
            var headers = _currentDataset.Headers.ToList();
            var keys = _currentDataset.UniqueKeys.ToList();

            if (atIndex < 0) atIndex = 0;
            if (atIndex > headers.Count) atIndex = headers.Count;

            string header = MakeUniqueHeader(headerLabel, headers);
            string key = MakeUniqueKeyFrom(headerLabel, keys);

            headers.Insert(atIndex, header);
            keys.Insert(atIndex, key);

            foreach (var row in _currentDataset.Rows)
                row[key] = (copyFromKey != null && row.TryGetValue(copyFromKey, out var v)) ? (v ?? "") : string.Empty;

            _currentDataset.Headers = headers.ToArray();
            _currentDataset.UniqueKeys = keys.ToArray();

            RebuildPreviewColumns();
            RefreshDataView();
            ShowInfoBar("Column added.", InfoBarSeverity.Success);
        }

        private void DeleteColumn_Click(object sender, RoutedEventArgs e)
        {
            int i = CurrentColumnIndex();
            if (i < 0) { ShowInfoBar("Select a cell in the column to delete.", InfoBarSeverity.Warning); return; }
            if (_currentDataset.Headers.Length <= 1)
            {
                ShowInfoBar("Cannot delete the last column.", InfoBarSeverity.Warning);
                return;
            }

            PushUndo();
            var headers = _currentDataset.Headers.ToList();
            var keys = _currentDataset.UniqueKeys.ToList();
            string key = keys[i];

            headers.RemoveAt(i);
            keys.RemoveAt(i);
            foreach (var row in _currentDataset.Rows)
                row.Remove(key);

            _currentDataset.Headers = headers.ToArray();
            _currentDataset.UniqueKeys = keys.ToArray();

            RebuildPreviewColumns();
            RefreshDataView();
            ShowInfoBar("Column deleted.", InfoBarSeverity.Success);
        }

        // ----- RINOMINA COLONNA -----
        private async void RenameColumn_Click(object sender, RoutedEventArgs e)
        {
            int i = CurrentColumnIndex();
            if (i < 0) { ShowInfoBar("Select a cell in the column to rename.", InfoBarSeverity.Warning); return; }

            var input = new TextBox { Text = _currentDataset.Headers[i] };
            var dialog = new ContentDialog
            {
                Title = "Rename column",
                Content = input,
                PrimaryButtonText = "Rename",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            var newName = input.Text?.Trim();
            if (string.IsNullOrEmpty(newName)) return;

            PushUndo();
            // Cambia solo l'INTESTAZIONE visibile (e ciò che scrive l'export):
            // la chiave interna resta invariata, quindi i dati non si toccano.
            var headers = _currentDataset.Headers.ToArray();
            headers[i] = newName;
            _currentDataset.Headers = headers;

            RebuildPreviewColumns();
            ShowInfoBar("Column renamed.", InfoBarSeverity.Success);
        }

        // Intestazione VISIBILE non duplicata.
        private static string MakeUniqueHeader(string baseLabel, List<string> existing)
        {
            string label = baseLabel;
            int n = 1;
            while (existing.Contains(label)) { n++; label = $"{baseLabel} {n}"; }
            return label;
        }

        // Chiave INTERNA univoca (sanificata) per una nuova colonna.
        private static string MakeUniqueKeyFrom(string baseLabel, List<string> existingKeys)
        {
            string baseKey = SanitizeKey(baseLabel);
            string key = baseKey;
            int n = 1;
            while (existingKeys.Contains(key)) { n++; key = $"{baseKey}_{n}"; }
            return key;
        }

        private void DeleteRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (EditGrid.SelectedItems.Count == 0)
            {
                ShowInfoBar("Select one or more rows to delete.", InfoBarSeverity.Warning);
                return;
            }

            PushUndo();
            var toRemove = EditGrid.SelectedItems
                .Cast<IDictionary<string, object>>()
                .ToList();

            foreach (var r in toRemove)
                _currentDataset.Rows.Remove(r);

            RefreshDataView();
            ShowInfoBar($"{toRemove.Count} row(s) deleted.", InfoBarSeverity.Success);
        }

        // Cattura il TextBox di modifica quando la cella entra in editing.
        private void EditGrid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            _editingRow = e.Row?.DataContext as IDictionary<string, object>;
            _editColumnKeys.TryGetValue(e.Column, out _editingKey);
            _editingTextBox = e.EditingElement as TextBox ?? FindChildTextBox(e.EditingElement);

            // Memorizzo il valore PRIMA della modifica: serve a costruire lo snapshot di undo
            // anche se il binding scrivesse il nuovo valore durante la digitazione.
            _editingOldValue = (_editingRow != null && _editingKey != null
                                && _editingRow.TryGetValue(_editingKey, out var cur))
                ? cur?.ToString() ?? "" : "";
        }

        private void EditGrid_CellEditEnded(object sender, DataGridCellEditEndedEventArgs e)
        {
            // Write-back AFFIDABILE: scriviamo noi il valore nel dato, senza dipendere
            // dal binding "a indice" dell'ExpandoObject (che non torna nella sorgente).
            if (e.EditAction == DataGridEditAction.Commit
                && _editingRow != null && _editingKey != null && _editingTextBox != null)
            {
                var newVal = _editingTextBox.Text;
                var oldVal = _editingOldValue ?? "";
                if (oldVal != newVal)
                {
                    // Garantisco che lo snapshot contenga il valore PRECEDENTE:
                    // ripristino il vecchio, scatto lo snapshot, poi applico il nuovo.
                    _editingRow[_editingKey] = oldVal;
                    PushUndo();
                    _editingRow[_editingKey] = newVal;
                }
            }

            _editingTextBox = null;
            _editingRow = null;
            _editingKey = null;
            _editingOldValue = null;

            // Aggiorno statistiche e conteggio per riflettere la modifica.
            var stats = _statistics.Compute(
                _currentDataset.Headers,
                _currentDataset.UniqueKeys,
                _currentDataset.Rows);
            StatsGrid.ItemsSource = stats;

            if (_currentInfo != null)
                FileStatsText.Text =
                    $"{_currentDataset.RowCount} righe • {_currentDataset.ColumnCount} colonne • " +
                    $"{FormatSize(_currentInfo.FileSizeBytes)}";
        }

        private static TextBox? FindChildTextBox(DependencyObject? parent)
        {
            if (parent is TextBox tb) return tb;
            if (parent == null) return null;
            int n = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < n; i++)
            {
                var found = FindChildTextBox(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i));
                if (found != null) return found;
            }
            return null;
        }

        // ─────────────────────────────────────────────────
        // EXPORT
        // ─────────────────────────────────────────────────
        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDataset.Rows.Count == 0)
            {
                ShowInfoBar("Carica prima un file CSV.", InfoBarSeverity.Warning);
                return;
            }

            var format = (ExportFormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "csv";
            var picker = new FileSavePicker();

            if (format == "json")
            {
                picker.FileTypeChoices.Add("JSON file", new List<string> { ".json" });
                picker.SuggestedFileName = "export";
            }
            else
            {
                picker.FileTypeChoices.Add("CSV file", new List<string> { ".csv" });
                picker.SuggestedFileName = "export";
            }

            if (App.MainWindow is null) return;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file == null) return;

            try
            {
                if (format == "json")
                {
                    await _export.ExportToJsonAsync(_currentDataset, file.Path);
                }
                else
                {
                    var encoding = GetExportEncoding();
                    var separator = GetExportSeparator();
                    await _export.ExportToCsvAsync(_currentDataset, file.Path, encoding, separator, '"');
                }

                ShowInfoBar($"File saved: {file.Name}", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowInfoBar($"Export error: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private Encoding GetExportEncoding()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var tag = (ExportEncodingCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            return tag switch
            {
                "utf-8-bom" => new UTF8Encoding(true),
                "utf-16" => Encoding.Unicode,
                "windows-1252" => Encoding.GetEncoding(1252),
                _ => new UTF8Encoding(false)
            };
        }

        private char GetExportSeparator()
        {
            var tag = (ExportSeparatorCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            return tag switch
            {
                "\t" => '\t',
                ";" => ';',
                "|" => '|',
                _ => ','
            };
        }

        // ─────────────────────────────────────────────────
        // UI HELPERS
        // ─────────────────────────────────────────────────
        private void UpdateInfoPanels(CsvFileInfo info, List<string> duplicates)
        {
            FileNameText.Text = info.FileName;
            FileStatsText.Text = $"{info.PreviewRows} rows • {info.ColumnCount} columns • {FormatSize(info.FileSizeBytes)}";

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
            FileNameText.Text = "Error";
            FileStatsText.Text = msg;
            DetectionInfoText.Text = string.Empty;
            PreviewGrid.ItemsSource = null;
            PreviewGrid.Columns.Clear();
            EditGrid.ItemsSource = null;
            EditGrid.Columns.Clear();
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