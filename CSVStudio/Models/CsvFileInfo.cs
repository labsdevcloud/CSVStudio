using System.Text;

namespace CSVStudio.Models
{
    /// <summary>
    /// Rappresenta le informazioni rilevate automaticamente da un file CSV.
    /// </summary>
    public class CsvFileInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }

        // Detection results
        public Encoding Encoding { get; set; } = Encoding.UTF8;
        public string EncodingName { get; set; } = "UTF-8";
        public float EncodingConfidence { get; set; }

        public char Separator { get; set; } = ',';
        public string SeparatorName { get; set; } = "Comma";

        public char Quote { get; set; } = '"';
        public string QuoteName { get; set; } = "Double quote";

        public bool HasHeader { get; set; } = true;

        // Statistics
        public int TotalRows { get; set; }
        public int PreviewRows { get; set; }
        public int ColumnCount { get; set; }

        // Issues detected
        public bool HasDuplicateHeaders { get; set; }
        public bool HasEmptyHeaders { get; set; }
    }
}