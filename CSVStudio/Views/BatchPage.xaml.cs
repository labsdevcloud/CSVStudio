using CSVStudio.Models;
using CSVStudio.Services;
using CSVStudio.Services.Operations;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace CSVStudio.Views
{
    public sealed partial class BatchPage : Page
    {
        private readonly BatchProcessingService _processor = new();
        private readonly DispatcherQueue _ui;

        private readonly ObservableCollection<BatchJob> _jobs = new();

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

        public BatchPage()
        {
            this.InitializeComponent();
            _ui = DispatcherQueue.GetForCurrentThread();
            OperationsList.ItemsSource = _availableOperations;
            JobsGrid.ItemsSource = _jobs;
            UpdateFilesCount();
        }

        // ─────────────────────────────────────────────
        // ADD FILES / FOLDER
        // ─────────────────────────────────────────────
        private async void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".csv");
            picker.FileTypeFilter.Add(".txt");

            if (App.MainWindow is null) return;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var files = await picker.PickMultipleFilesAsync();
            foreach (var f in files) AddJob(f.Path);
            UpdateFilesCount();
        }

        private async void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");

            if (App.MainWindow is null) return;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder == null) return;

            try
            {
                var csvFiles = Directory.EnumerateFiles(folder.Path, "*.csv", SearchOption.TopDirectoryOnly);
                foreach (var f in csvFiles) AddJob(f);
                UpdateFilesCount();
                Log($"Added folder: {folder.Path}");
            }
            catch (Exception ex)
            {
                Log($"❌ Errore caricamento cartella: {ex.Message}");
            }
        }

        private void ClearList_Click(object sender, RoutedEventArgs e)
        {
            _jobs.Clear();
            UpdateFilesCount();
            LogText.Text = string.Empty;
            BatchProgress.Value = 0;
        }

        // ─────────────────────────────────────────────
        // DRAG & DROP
        // ─────────────────────────────────────────────
        private void FilesArea_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Aggiungi al batch";
            e.DragUIOverride.IsCaptionVisible = true;
        }

        private async void FilesArea_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                foreach (var item in items)
                {
                    if (item is StorageFile f)
                        AddJob(f.Path);
                    else if (item is StorageFolder folder)
                    {
                        try
                        {
                            var csvFiles = Directory.EnumerateFiles(folder.Path, "*.csv", SearchOption.TopDirectoryOnly);
                            foreach (var p in csvFiles) AddJob(p);
                        }
                        catch (Exception ex) { Log($"❌ {ex.Message}"); }
                    }
                }
                UpdateFilesCount();
            }
        }

        // ─────────────────────────────────────────────
        // OUTPUT FOLDER PICKER
        // ─────────────────────────────────────────────
        private async void PickFolder_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");

            if (App.MainWindow is null) return;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null) OutputFolderText.Text = folder.Path;
        }

        // ─────────────────────────────────────────────
        // RUN
        // ─────────────────────────────────────────────
        private async void RunButton_Click(object sender, RoutedEventArgs e)
        {
            if (_jobs.Count == 0)
            {
                Log("⚠ Nessun file aggiunto.");
                return;
            }

            var selectedOps = GetSelectedOperations();
            if (selectedOps.Count == 0)
            {
                Log("⚠ Nessuna operazione selezionata.");
                return;
            }

            // Reset stato
            foreach (var j in _jobs)
            {
                j.Status = BatchJobStatus.Pending;
                j.StatusMessage = string.Empty;
            }
            RefreshJobs();

            BatchProgress.Value = 0;
            BatchProgress.Maximum = _jobs.Count;
            RunButton.IsEnabled = false;
            Log($"▶ Avvio batch su {_jobs.Count} file con {selectedOps.Count} operazioni...");

            int completed = 0;

            try
            {
                await _processor.ProcessAsync(
                    _jobs.ToList(),
                    selectedOps,
                    OutputFolderText.Text,
                    SuffixText.Text ?? "_clean",
                    onJobUpdated: job =>
                    {
                        _ui.TryEnqueue(() =>
                        {
                            if (job.Status == BatchJobStatus.Success)
                            {
                                completed++;
                                BatchProgress.Value = completed;
                                Log($"✅ {job.FileName} → {Path.GetFileName(job.OutputPath)}");
                            }
                            else if (job.Status == BatchJobStatus.Failed)
                            {
                                completed++;
                                BatchProgress.Value = completed;
                                Log($"❌ {job.FileName}: {job.StatusMessage}");
                            }
                            RefreshJobs();
                        });
                    });

                int ok = _jobs.Count(j => j.Status == BatchJobStatus.Success);
                int fail = _jobs.Count(j => j.Status == BatchJobStatus.Failed);
                Log($"✔ Batch completato: {ok} OK, {fail} errori.");
            }
            catch (Exception ex)
            {
                Log($"❌ Errore globale: {ex.Message}");
            }
            finally
            {
                RunButton.IsEnabled = true;
            }
        }

        // ─────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────
        private void AddJob(string path)
        {
            if (_jobs.Any(j => string.Equals(j.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                return;

            var info = new FileInfo(path);
            _jobs.Add(new BatchJob
            {
                FilePath = path,
                FileName = info.Name,
                FileSizeBytes = info.Exists ? info.Length : 0
            });
        }

        private List<ICsvOperation> GetSelectedOperations()
        {
            var selected = new List<ICsvOperation>();
            // ItemsControl non ha SelectedItems → iteriamo i container generati
            for (int i = 0; i < OperationsList.Items.Count; i++)
            {
                var container = OperationsList.ContainerFromIndex(i) as ContentPresenter;
                var cb = FindChildCheckBox(container);
                if (cb != null && cb.IsChecked == true && cb.Tag is ICsvOperation op)
                    selected.Add(op);
            }
            return selected;
        }

        private static CheckBox? FindChildCheckBox(DependencyObject? parent)
        {
            if (parent == null) return null;
            int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is CheckBox cb) return cb;
                var result = FindChildCheckBox(child);
                if (result != null) return result;
            }
            return null;
        }

        private void RefreshJobs()
        {
            JobsGrid.ItemsSource = null;
            JobsGrid.ItemsSource = _jobs;
        }

        private void UpdateFilesCount()
        {
            FilesCountText.Text = _jobs.Count == 0
                ? "Nessun file"
                : $"{_jobs.Count} file in coda";
        }

        private void Log(string line)
        {
            LogText.Text += (LogText.Text.Length == 0 ? "" : "\n") + $"[{DateTime.Now:HH:mm:ss}] {line}";
            LogScroll.UpdateLayout();
            LogScroll.ChangeView(null, double.MaxValue, null);
        }
    }
}