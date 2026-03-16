namespace FelderBot.Options;

public class AzureSearchOptions
{
    public const string SectionName = "AzureAISearch";

    public string Endpoint { get; set; } = "";
    public string IndexName { get; set; } = "";
    public string ApiKey { get; set; } = "";
    /// <summary>Antal dokumenter/chunks der hentes til RAG-kontekst (default 5).</summary>
    public int Top { get; set; } = 5;
    /// <summary>Semantic configuration på indexet (valgfri). Kræver semantic ranker på search service.</summary>
    public string? SemanticConfigurationName { get; set; }
    /// <summary>Feltnavn for dokument-id i indexet.</summary>
    public string IdFieldName { get; set; } = "id";
    /// <summary>Feltnavn for den tekst der inkluderes i LLM-kontekst.</summary>
    public string ContentFieldName { get; set; } = "content";
    /// <summary>Valgfrit feltnavn for kilde/reference (fx "source").</summary>
    public string? SourceFieldName { get; set; }
}
