namespace FelderBot.Models;

/// <summary>Ét søgeresultat til brug i RAG-kontekst.</summary>
public sealed class SearchResult
{
    public string? Id { get; init; }
    public string Content { get; init; } = "";
    public string? Source { get; init; }
}
