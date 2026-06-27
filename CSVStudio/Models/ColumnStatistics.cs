namespace CSVStudio.Models
{
    /// <summary>
    /// Statistiche calcolate per una singola colonna del CSV.
    /// </summary>
    public class ColumnStatistics
    {
        public string ColumnName { get; set; } = string.Empty;
        public string DetectedType { get; set; } = "Text";

        public int TotalValues { get; set; }
        public int EmptyValues { get; set; }
        public int NonEmptyValues { get; set; }
        public int DistinctValues { get; set; }
        public int DuplicateValues { get; set; }

        public string MinValue { get; set; } = string.Empty;
        public string MaxValue { get; set; } = string.Empty;

        public int MinLength { get; set; }
        public int MaxLength { get; set; }
        public double AvgLength { get; set; }

        // Property utility per UI binding
        public string EmptyPercentage =>
            TotalValues == 0 ? "0%" : $"{(EmptyValues * 100.0 / TotalValues):0}%";

        public string DistinctPercentage =>
            TotalValues == 0 ? "0%" : $"{(DistinctValues * 100.0 / TotalValues):0}%";
    }
}