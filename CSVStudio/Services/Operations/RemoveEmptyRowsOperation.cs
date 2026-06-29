using CSVStudio.Models;
using System.Collections.Generic;
using System.Linq;

namespace CSVStudio.Services.Operations
{
    public class RemoveEmptyRowsOperation : ICsvOperation
    {
        public string Name => "Remove empty rows";
        public string IconGlyph => "\uE74D"; // Delete
        public string Description => "Removes rows where every cell is empty.";

        public CsvOperationResult Execute(CsvDataset dataset)
        {
            var newRows = new List<IDictionary<string, object>>();
            int removed = 0;

            foreach (var row in dataset.Rows)
            {
                bool allEmpty = row.Values.All(v => string.IsNullOrWhiteSpace(v?.ToString()));
                if (allEmpty) removed++;
                else newRows.Add(row);
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
                Summary = $"Removed {removed} empty row(s)."
            };
        }
    }
}