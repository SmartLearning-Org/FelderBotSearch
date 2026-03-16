using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using FelderBot.Models;
using FelderBot.Options;
using Microsoft.Extensions.Options;

namespace FelderBot.Services;

public class AzureSearchService : ISearchService
{
    private readonly SearchClient _searchClient;
    private readonly AzureSearchOptions _options;

    public AzureSearchService(SearchClient searchClient, IOptions<AzureSearchOptions> options)
    {
        _searchClient = searchClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<Models.SearchResult>> SearchAsync(string query, int top, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.IndexName) || string.IsNullOrWhiteSpace(_options.Endpoint))
            return Array.Empty<Models.SearchResult>();

        var select = new List<string> { _options.IdFieldName, _options.ContentFieldName };
        if (!string.IsNullOrEmpty(_options.SourceFieldName))
            select.Add(_options.SourceFieldName);

        var searchOptions = new SearchOptions { Size = top };
        foreach (var field in select)
            searchOptions.Select.Add(field);

        if (!string.IsNullOrWhiteSpace(_options.SemanticConfigurationName))
            searchOptions.SemanticSearch = new SemanticSearchOptions { SemanticConfigurationName = _options.SemanticConfigurationName };

        var response = await _searchClient.SearchAsync<SearchDocument>(query, searchOptions, cancellationToken);
        var results = new List<Models.SearchResult>();
        await foreach (Azure.Search.Documents.Models.SearchResult<SearchDocument> result in response.Value.GetResultsAsync())
        {
            var doc = result.Document;
            var id = GetString(doc, _options.IdFieldName);
            var content = GetString(doc, _options.ContentFieldName) ?? "";
            var source = _options.SourceFieldName != null ? GetString(doc, _options.SourceFieldName) : null;
            results.Add(new Models.SearchResult { Id = id, Content = content, Source = source });
        }

        return results;
    }

    private static string? GetString(SearchDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var value) || value == null)
            return null;
        if (value is string s)
            return s;
        return value.ToString();
    }
}
