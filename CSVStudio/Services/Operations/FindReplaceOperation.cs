using CSVStudio.Models;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.RegularExpressions;

namespace CSVStudio.Services.Operations
{
    /// <summary>
    /// Find &amp; Replace globale (su tutte le celle).
    /// Configurabile tramite Find/Replace/Regex/MatchCase.
    /// </summary>
    public class FindReplaceOperation : ICsvOperation
    {
        public string Name => "Find & Replace";
        public string IconGlyph => "\uE721"; // Search
        public string Description => "Finds and replaces text across all cells.";

        public string Find { get; set; } = string.Empty;
        public string Replace { get; set; } = string.Empty;
        public bool UseRegex { get; set; }
        public bool MatchCase { get; set; }

        public CsvOperationResult Execute(CsvDataset dataset)
        {
            if (string.IsNullOrEmpty(Find))
            {
                return new CsvOperationResult
                {
                    Dataset = dataset,
                    Summary = "Nothing to find.",
                    Success = false
                };
            }

            int replacements = 0;
            var newRows = new List<IDictionary<string, object>>();

            Regex? regex = null;
            if (UseRegex)
            {
                var options = MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                regex = new Regex(Find, options);
            }

            foreach (var row in dataset.Rows)
            {
                var newRow = new ExpandoObject() as IDictionary<string, object>;
                foreach (var kvp in row)
                {
                    var val = kvp.Value?.ToString() ?? string.Empty;
                    string newVal;

                    if (regex != null)
                    {
                        var matches = regex.Matches(val).Count;
                        if (matches > 0) replacements += matches;
                        newVal = regex.Replace(val, Replace);
                    }
                    else
                    {
                        if (MatchCase)
                        {
                            int idx = 0, count = 0;
                            while ((idx = val.IndexOf(Find, idx, StringComparison.Ordinal)) >= 0)
                            {
                                count++;
                                idx += Find.Length;
                            }
                            replacements += count;
                            newVal = val.Replace(Find, Replace);
                        }
                        else
                        {
                            int idx = 0, count = 0;
                            while ((idx = val.IndexOf(Find, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
                            {
                                count++;
                                idx += Find.Length;
                            }
                            replacements += count;
                            newVal = ReplaceIgnoreCase(val, Find, Replace);
                        }
                    }

                    newRow[kvp.Key] = newVal;
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
                Summary = $"Replaced {replacements} occurrence(s)."
            };
        }

        private static string ReplaceIgnoreCase(string input, string find, string replace)
        {
            if (string.IsNullOrEmpty(find)) return input;
            return Regex.Replace(input,
                Regex.Escape(find),
                replace.Replace("$", "$$"),
                RegexOptions.IgnoreCase);
        }
    }
}