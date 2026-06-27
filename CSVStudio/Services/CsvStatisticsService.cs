using CSVStudio.Models;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;

namespace CSVStudio.Services
{
    /// <summary>
    /// Calcola statistiche per colonna a partire dai dati caricati.
    /// </summary>
    public class CsvStatisticsService
    {
        public List<ColumnStatistics> Compute(
            string[] headers,
            string[] uniqueKeys,
            List<IDictionary<string, object>> rows)
        {
            var stats = new List<ColumnStatistics>();

            for (int i = 0; i < headers.Length; i++)
            {
                var key = uniqueKeys[i];
                var values = rows
                    .Select(r => r.TryGetValue(key, out var v) ? v?.ToString() ?? "" : "")
                    .ToList();

                stats.Add(ComputeForColumn(headers[i], values));
            }

            return stats;
        }

        private static ColumnStatistics ComputeForColumn(string columnName, List<string> values)
        {
            var stat = new ColumnStatistics
            {
                ColumnName = columnName,
                TotalValues = values.Count
            };

            var nonEmpty = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            stat.NonEmptyValues = nonEmpty.Count;
            stat.EmptyValues = values.Count - nonEmpty.Count;

            var distinct = nonEmpty.Distinct().ToList();
            stat.DistinctValues = distinct.Count;
            stat.DuplicateValues = nonEmpty.Count - distinct.Count;

            // Detect type
            stat.DetectedType = DetectType(nonEmpty);

            // Length stats
            if (nonEmpty.Count > 0)
            {
                stat.MinLength = nonEmpty.Min(v => v.Length);
                stat.MaxLength = nonEmpty.Max(v => v.Length);
                stat.AvgLength = nonEmpty.Average(v => v.Length);
            }

            // Min / Max
            if (nonEmpty.Count > 0)
            {
                if (stat.DetectedType == "Number")
                {
                    var numbers = nonEmpty
                        .Select(v => double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var n)
                            ? (double?)n : null)
                        .Where(n => n.HasValue)
                        .Select(n => n!.Value)
                        .ToList();

                    if (numbers.Count > 0)
                    {
                        stat.MinValue = numbers.Min().ToString(CultureInfo.InvariantCulture);
                        stat.MaxValue = numbers.Max().ToString(CultureInfo.InvariantCulture);
                    }
                }
                else
                {
                    var sorted = nonEmpty.OrderBy(v => v, System.StringComparer.OrdinalIgnoreCase).ToList();
                    stat.MinValue = Truncate(sorted.First(), 30);
                    stat.MaxValue = Truncate(sorted.Last(), 30);
                }
            }

            return stat;
        }

        private static string DetectType(List<string> values)
        {
            if (values.Count == 0) return "Empty";

            int numbers = 0, dates = 0, bools = 0;
            int sample = System.Math.Min(values.Count, 200);

            for (int i = 0; i < sample; i++)
            {
                var v = values[i].Trim();
                if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) numbers++;
                else if (System.DateTime.TryParse(v, out _)) dates++;
                else if (v.Equals("true", System.StringComparison.OrdinalIgnoreCase)
                      || v.Equals("false", System.StringComparison.OrdinalIgnoreCase)) bools++;
            }

            double threshold = sample * 0.8;
            if (numbers >= threshold) return "Number";
            if (dates >= threshold) return "Date";
            if (bools >= threshold) return "Boolean";
            return "Text";
        }

        private static string Truncate(string value, int length) =>
            value.Length <= length ? value : value.Substring(0, length) + "…";
    }
}