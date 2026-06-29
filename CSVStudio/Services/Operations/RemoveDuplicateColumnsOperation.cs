using CSVStudio.Models;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace CSVStudio.Services.Operations
{
    public class RemoveDuplicateColumnsOperation : ICsvOperation
    {
        public string Name => "Remove duplicate columns";
        public string IconGlyph => "\uE74D"; // Delete
        public string Description => "Keeps only one column for each duplicate header.";

        public CsvOperationResult Execute(CsvDataset dataset)
        {
            // Costruisce mapping: header → primo indice
            var keepIndices = new List<int>();
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < dataset.Headers.Length; i++)
            {
                var header = dataset.Headers[i] ?? "";
                if (seen.Add(header))
                    keepIndices.Add(i);
            }

            if (keepIndices.Count == dataset.Headers.Length)
            {
                return new CsvOperationResult
                {
                    Dataset = dataset,
                    Summary = "No duplicate columns found."
                };
            }

            var newHeaders = keepIndices.Select(i => dataset.Headers[i]).ToArray();
            var newKeys = keepIndices.Select(i => dataset.UniqueKeys[i]).ToArray();

            var newRows = new List<IDictionary<string, object>>();
            foreach (var row in dataset.Rows)
            {
                var newRow = new ExpandoObject() as IDictionary<string, object>;
                for (int i = 0; i < newKeys.Length; i++)
                {
                    newRow[newKeys[i]] = row.TryGetValue(newKeys[i], out var v) ? v ?? "" : "";
                }
                newRows.Add(newRow);
            }

            int removed = dataset.Headers.Length - newHeaders.Length;

            return new CsvOperationResult
            {
                Dataset = new CsvDataset
                {
                    Headers = newHeaders,
                    UniqueKeys = newKeys,
                    Rows = newRows,
                    Info = dataset.Info
                },
                Summary = $"Removed {removed} duplicate column(s)."
            };
        }
    }
}