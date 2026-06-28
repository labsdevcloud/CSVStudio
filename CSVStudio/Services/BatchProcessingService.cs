using CSVStudio.Models;
using CSVStudio.Services.Operations;
using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSVStudio.Services
{
    /// <summary>
    /// Servizio che processa più CSV in batch applicando una lista di operazioni.
    /// </summary>
    public class BatchProcessingService
    {
        private readonly CsvDetectionService _detection = new();
        private readonly CsvExportService _export = new();

        public async Task ProcessAsync(
            List<BatchJob> jobs,
            List<ICsvOperation> operations,
            string outputFolder,
            string suffix,
            Action<BatchJob>? onJobUpdated = null)
        {
            foreach (var job in jobs)
            {
                try
                {
                    job.Status = BatchJobStatus.Processing;
                    onJobUpdated?.Invoke(job);

                    // 1. Detect
                    var info = _detection.Detect(job.FilePath);
                    job.DetectedInfo = info;

                    // 2. Load dataset
                    var dataset = LoadDataset(info);

                    // 3. Apply operations in sequence
                    foreach (var op in operations)
                    {
                        var result = op.Execute(dataset);
                        if (result.Success)
                            dataset = result.Dataset;
                    }

                    // 4. Build output path
                    var fileName = Path.GetFileNameWithoutExtension(job.FilePath);
                    var ext = Path.GetExtension(job.FilePath);
                    var outName = $"{fileName}{suffix}{ext}";
                    var outPath = string.IsNullOrWhiteSpace(outputFolder)
                        ? Path.Combine(Path.GetDirectoryName(job.FilePath) ?? ".", outName)
                        : Path.Combine(outputFolder, outName);

                    // 5. Export with same encoding/separator as input
                    await _export.ExportToCsvAsync(dataset, outPath, info.Encoding, info.Separator, info.Quote);

                    job.OutputPath = outPath;
                    job.Status = BatchJobStatus.Success;
                    job.StatusMessage = $"Saved as {Path.GetFileName(outPath)}";
                }
                catch (Exception ex)
                {
                    job.Status = BatchJobStatus.Failed;
                    job.StatusMessage = ex.Message;
                }
                finally
                {
                    onJobUpdated?.Invoke(job);
                }
            }
        }

        // ─────────────────────────────────────────────
        // Helper: load dataset usando l'info detected
        // ─────────────────────────────────────────────
        private CsvDataset LoadDataset(CsvFileInfo info)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = info.Separator.ToString(),
                Quote = info.Quote,
                HasHeaderRecord = info.HasHeader,
                BadDataFound = null,
                MissingFieldFound = null
            };

            using var reader = new StreamReader(info.FilePath, info.Encoding, true);
            using var csv = new CsvReader(reader, config);

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

            var uniqueKeys = MakeUniqueKeys(headers);
            var rows = new List<IDictionary<string, object>>();

            if (info.HasHeader)
            {
                while (csv.Read()) AddRow(csv, headers, uniqueKeys, rows);
            }
            else
            {
                AddRow(csv, headers, uniqueKeys, rows);
                while (csv.Read()) AddRow(csv, headers, uniqueKeys, rows);
            }

            return new CsvDataset
            {
                Headers = headers,
                UniqueKeys = uniqueKeys,
                Rows = rows,
                Info = info
            };
        }

        private static void AddRow(CsvReader csv, string[] headers, string[] uniqueKeys, List<IDictionary<string, object>> rows)
        {
            var row = new ExpandoObject() as IDictionary<string, object>;
            for (int i = 0; i < headers.Length; i++)
                row[uniqueKeys[i]] = csv.GetField(i) ?? string.Empty;
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