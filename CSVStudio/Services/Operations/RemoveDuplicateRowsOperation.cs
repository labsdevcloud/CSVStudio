using CSVStudio.Models;
using System.Collections.Generic;
using System.Linq;

namespace CSVStudio.Services.Operations
{
    public class RemoveDuplicateRowsOperation : ICsvOperation
    {
        public string Name => "Remove duplicate rows";
        public string IconGlyph => "\uE8C8"; // Copy/duplicate icon family
        public string Description => "Elimina righe identiche mantenendo la prima occorrenza.";

        public CsvOperationResult Execute(CsvDataset dataset)
        {
            var seen = new HashSet<string>();
            var newRows = new List<IDictionary<string, object>>();
            int removed = 0;

            foreach (var row in dataset.Rows)
            {
                var key = string.Join("\u001F",
                    dataset.UniqueKeys.Select(k =>
                        row.TryGetValue(k, out var v) ? v?.ToString() ?? "" : ""));

                if (seen.Add(key))
                    newRows.Add(row);
                else
                    removed++;
            }

            return new CsvOperationResult
            {
                Dataset = new CsvDataset
                {
                    Headers = dataset.Headers,
                    UniqueKeys = dataset.UniqueKeys,
                    Rows = newRows,
                    Info = dataset.Info
                },
                Summary = $"Removed {removed} duplicate row(s)."
            };
        }
    }
}