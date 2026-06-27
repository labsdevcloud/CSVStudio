using CSVStudio.Models;

namespace CSVStudio.Services.Operations
{
    /// <summary>
    /// Contratto base per tutte le operazioni di trasformazione CSV.
    /// </summary>
    public interface ICsvOperation
    {
        /// <summary>Nome breve (es: "Trim spaces").</summary>
        string Name { get; }

        /// <summary>Icona Segoe (es: "&#xE8E5;").</summary>
        string IconGlyph { get; }

        /// <summary>Descrizione per UI.</summary>
        string Description { get; }

        /// <summary>Esegue l'operazione e restituisce un nuovo dataset.</summary>
        CsvOperationResult Execute(CsvDataset dataset);
    }

    /// <summary>
    /// Risultato di un'operazione: dataset trasformato + report.
    /// </summary>
    public class CsvOperationResult
    {
        public CsvDataset Dataset { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
        public bool Success { get; set; } = true;
    }
}