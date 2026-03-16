using FelderBot.Models;

namespace FelderBot.Services;

public interface ISearchService
{
    /// <summary>Søger i Azure AI Search-indexet og returnerer de mest relevante chunks til RAG.</summary>
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int top, CancellationToken cancellationToken = default);
}
