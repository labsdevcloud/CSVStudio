using CSVStudio.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UtfUnknown;

namespace CSVStudio.Services
{
    /// <summary>
    /// Service responsible for auto-detecting CSV properties:
    /// encoding, separator, quote character, header presence.
    /// </summary>
    public class CsvDetectionService
    {
        // Separator candidates: comma, semicolon, tab, pipe
        private static readonly char[] CandidateSeparators = { ',', ';', '\t', '|' };

        // Number of rows analyzed for separator detection
        private const int SampleLines = 50;

        /// <summary>
        /// Analyzes the file and returns detected CSV info.
        /// </summary>
        public CsvFileInfo Detect(string filePath)
        {
            var info = new CsvFileInfo
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                FileSizeBytes = new FileInfo(filePath).Length
            };

            // 1. Encoding detection
            DetectEncoding(filePath, info);

            // 2. Read sample lines using detected encoding
            var sampleLines = ReadSampleLines(filePath, info.Encoding, SampleLines);

            if (sampleLines.Count == 0)
                return info;

            // 3. Separator detection
            info.Separator = DetectSeparator(sampleLines);
            info.SeparatorName = GetSeparatorName(info.Separator);

            // 4. Quote detection (semplice: " o ')
            info.Quote = DetectQuote(sampleLines, info.Separator);
            info.QuoteName = info.Quote == '"' ? "Double quote" : "Single quote";

            // 5. Header detection
            info.HasHeader = DetectHasHeader(sampleLines, info.Separator);

            return info;
        }

        // ─────────────────────────────────────────────────────────
        // ENCODING
        // ─────────────────────────────────────────────────────────
        private static void DetectEncoding(string filePath, CsvFileInfo info)
        {
            // Registra encoding addizionali (Windows-1252, ecc.)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var result = CharsetDetector.DetectFromFile(filePath);
            if (result?.Detected != null)
            {
                info.Encoding = result.Detected.Encoding ?? Encoding.UTF8;
                info.EncodingName = result.Detected.EncodingName ?? "UTF-8";
                info.EncodingConfidence = result.Detected.Confidence;
            }
            else
            {
                info.Encoding = Encoding.UTF8;
                info.EncodingName = "UTF-8 (fallback)";
                info.EncodingConfidence = 0f;
            }
        }

        // ─────────────────────────────────────────────────────────
        // SAMPLE READING
        // ─────────────────────────────────────────────────────────
        private static List<string> ReadSampleLines(string path, Encoding encoding, int maxLines)
        {
            var lines = new List<string>();
            using var reader = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks: true);
            string? line;
            while ((line = reader.ReadLine()) != null && lines.Count < maxLines)
            {
                lines.Add(line);
            }
            return lines;
        }

        // ─────────────────────────────────────────────────────────
        // SEPARATOR
        // ─────────────────────────────────────────────────────────
        private static char DetectSeparator(List<string> lines)
        {
            // Per ogni candidato, calcola quante occorrenze MEDIE per riga
            // e quanto è "stabile" il conteggio tra le righe.
            // Il separatore corretto avrà:
            //  - alta occorrenza media
            //  - bassa varianza tra le righe
            var scores = new Dictionary<char, double>();

            foreach (var sep in CandidateSeparators)
            {
                var counts = lines.Select(l => CountOutsideQuotes(l, sep)).ToList();
                if (counts.Count == 0 || counts.All(c => c == 0))
                {
                    scores[sep] = 0;
                    continue;
                }

                double avg = counts.Average();
                if (avg == 0)
                {
                    scores[sep] = 0;
                    continue;
                }

                double variance = counts.Select(c => Math.Pow(c - avg, 2)).Average();
                double stability = 1.0 / (1.0 + variance);

                // Score = media occorrenze * stabilità
                scores[sep] = avg * stability;
            }

            // Vince il candidato con score più alto
            return scores.OrderByDescending(kv => kv.Value).First().Key;
        }

        private static int CountOutsideQuotes(string line, char target)
        {
            int count = 0;
            bool inQuote = false;
            foreach (var c in line)
            {
                if (c == '"') inQuote = !inQuote;
                else if (!inQuote && c == target) count++;
            }
            return count;
        }

        private static string GetSeparatorName(char sep) => sep switch
        {
            ',' => "Comma",
            ';' => "Semicolon",
            '\t' => "Tab",
            '|' => "Pipe",
            _ => sep.ToString()
        };

        // ─────────────────────────────────────────────────────────
        // QUOTE
        // ─────────────────────────────────────────────────────────
        private static char DetectQuote(List<string> lines, char separator)
        {
            int doubleQuotes = lines.Sum(l => l.Count(c => c == '"'));
            int singleQuotes = lines.Sum(l => l.Count(c => c == '\''));
            return doubleQuotes >= singleQuotes ? '"' : '\'';
        }

        // ─────────────────────────────────────────────────────────
        // HEADER
        // ─────────────────────────────────────────────────────────
        private static bool DetectHasHeader(List<string> lines, char separator)
        {
            if (lines.Count < 2) return true;

            var firstRow = SplitLine(lines[0], separator);
            var secondRow = SplitLine(lines[1], separator);

            if (firstRow.Length != secondRow.Length) return true;

            // Euristica: se la prima riga ha SOLO testo (no numeri/date)
            // e la seconda ha qualche numero, probabilmente è un header.
            int numericInFirst = firstRow.Count(IsNumericOrDate);
            int numericInSecond = secondRow.Count(IsNumericOrDate);

            return numericInFirst == 0 && numericInSecond > 0
                || numericInFirst < numericInSecond;
        }

        private static string[] SplitLine(string line, char separator)
        {
            // Semplice split (per detection è sufficiente)
            return line.Split(separator);
        }

        private static bool IsNumericOrDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            value = value.Trim().Trim('"');
            return double.TryParse(value, System.Globalization.NumberStyles.Any,
                                   System.Globalization.CultureInfo.InvariantCulture, out _)
                || DateTime.TryParse(value, out _);
        }
    }
}