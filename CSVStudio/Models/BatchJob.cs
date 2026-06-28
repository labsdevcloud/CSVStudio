using System.Collections.Generic;

namespace CSVStudio.Models
{
    /// <summary>
    /// Rappresenta un singolo file da processare nel batch.
    /// </summary>
    public class BatchJob
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }

        public CsvFileInfo? DetectedInfo { get; set; }
        public BatchJobStatus Status { get; set; } = BatchJobStatus.Pending;
        public string StatusMessage { get; set; } = string.Empty;
        public string OutputPath { get; set; } = string.Empty;

        // Per UI binding
        public string StatusIcon => Status switch
        {
            BatchJobStatus.Pending => "⏳",
            BatchJobStatus.Processing => "⚙️",
            BatchJobStatus.Success => "✅",
            BatchJobStatus.Failed => "❌",
            _ => "•"
        };

        public string DetectedSummary => DetectedInfo == null
            ? "—"
            : $"{DetectedInfo.EncodingName} • sep: {DetectedInfo.SeparatorName}";
    }

    public enum BatchJobStatus
    {
        Pending,
        Processing,
        Success,
        Failed
    }
}