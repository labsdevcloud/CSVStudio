using CSVStudio.Models;
using System.Collections.Generic;
using System.Dynamic;

namespace CSVStudio.Services.Operations
{
    public class TrimSpacesOperation : ICsvOperation
    {
        public string Name => "Trim spaces";
        public string IconGlyph => "\uE8C8"; // Edit
        public string Description => "Removes leading and trailing spaces from every cell.";

        public CsvOperationResult Execute(CsvDataset dataset)
        {
            int trimmed = 0;
            var newRows = new List<IDictionary<string, object>>();

            foreach (var row in dataset.Rows)
            {
                var newRow = new ExpandoObject() as IDictionary<string, object>;
                foreach (var kvp in row)
                {
                    var str = kvp.Value?.ToString() ?? string.Empty;
                    var t = str.Trim();
                    if (t.Length != str.Length) trimmed++;
                    newRow[kvp.Key] = t;
                }
                newRows.Add(newRow);
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
                Summary = $"Trimmed {trimmed} cell(s)."
            };
        }
    }
}