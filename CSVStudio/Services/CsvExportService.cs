using CSVStudio.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CSVStudio.Services
{
    public class CsvExportService
    {
        // ─────────────────────────────────────────────
        // CSV
        // ─────────────────────────────────────────────
        public async Task ExportToCsvAsync(
            CsvDataset dataset,
            string filePath,
            Encoding encoding,
            char separator,
            char quote)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = separator.ToString(),
                Quote = quote,
                HasHeaderRecord = true
            };

            await using var writer = new StreamWriter(filePath, false, encoding);
            await using var csv = new CsvWriter(writer, config);

            // Header
            foreach (var h in dataset.Headers)
                csv.WriteField(h);
            await csv.NextRecordAsync();

            // Righe
            foreach (var row in dataset.Rows)
            {
                foreach (var key in dataset.UniqueKeys)
                {
                    var val = row.TryGetValue(key, out var v) ? v?.ToString() ?? "" : "";
                    csv.WriteField(val);
                }
                await csv.NextRecordAsync();
            }
        }

        // ─────────────────────────────────────────────
        // JSON
        // ─────────────────────────────────────────────
        public async Task ExportToJsonAsync(CsvDataset dataset, string filePath)
        {
            var list = new List<Dictionary<string, string>>();

            foreach (var row in dataset.Rows)
            {
                var dict = new Dictionary<string, string>();
                for (int i = 0; i < dataset.Headers.Length; i++)
                {
                    var key = dataset.Headers[i];
                    var uniqueKey = dataset.UniqueKeys[i];
                    var val = row.TryGetValue(uniqueKey, out var v) ? v?.ToString() ?? "" : "";

                    // Gestione header duplicati: aggiungi suffisso
                    if (dict.ContainsKey(key))
                        key = $"{key}_{i}";

                    dict[key] = val;
                }
                list.Add(dict);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, list, options);
        }
    }
}