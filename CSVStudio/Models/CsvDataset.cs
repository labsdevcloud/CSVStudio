using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace CSVStudio.Models
{
    /// <summary>
    /// Rappresenta il dataset CSV in memoria.
    /// Centralizza header, chiavi uniche, righe e info.
    /// </summary>
    public class CsvDataset
    {
        public string[] Headers { get; set; } = Array.Empty<string>();
        public string[] UniqueKeys { get; set; } = Array.Empty<string>();
        public List<IDictionary<string, object>> Rows { get; set; } = new();
        public CsvFileInfo? Info { get; set; }

        public int RowCount => Rows.Count;
        public int ColumnCount => Headers.Length;

        /// <summary>
        /// Crea una copia profonda del dataset (per non rompere lo stato).
        /// </summary>
        public CsvDataset Clone()
        {
            var clone = new CsvDataset
            {
                Headers = (string[])Headers.Clone(),
                UniqueKeys = (string[])UniqueKeys.Clone(),
                Info = Info
            };

            foreach (var row in Rows)
            {
                var newRow = new ExpandoObject() as IDictionary<string, object>;
                foreach (var kvp in row)
                    newRow[kvp.Key] = kvp.Value;
                clone.Rows.Add(newRow);
            }

            return clone;
        }
    }
}