using CSVStudio.Models;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;

namespace CSVStudio.Services.Operations
{
    public enum CaseMode { Upper, Lower, Title }

    public class ChangeCaseOperation : ICsvOperation
    {
        private readonly CaseMode _mode;

        public ChangeCaseOperation(CaseMode mode) { _mode = mode; }

        public string Name => _mode switch
        {
            CaseMode.Upper => "Change case to UPPER",
            CaseMode.Lower => "Change case to lower",
            _ => "Change case to Title"
        };

        public string IconGlyph => "\uE8D2"; // Font icon

        public string Description => "Transforms text cells to the selected case.";

        public CsvOperationResult Execute(CsvDataset dataset)
        {
            int changed = 0;
            var newRows = new List<IDictionary<string, object>>();
            var ti = CultureInfo.CurrentCulture.TextInfo;

            foreach (var row in dataset.Rows)
            {
                var newRow = new ExpandoObject() as IDictionary<string, object>;
                foreach (var kvp in row)
                {
                    var val = kvp.Value?.ToString() ?? string.Empty;
                    string transformed = _mode switch
                    {
                        CaseMode.Upper => val.ToUpperInvariant(),
                        CaseMode.Lower => val.ToLowerInvariant(),
                        _ => ti.ToTitleCase(val.ToLowerInvariant())
                    };
                    if (transformed != val) changed++;
                    newRow[kvp.Key] = transformed;
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
                Summary = $"Changed {changed} cell(s)."
            };
        }
    }
}