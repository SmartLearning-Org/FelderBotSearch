namespace FelderBot.Services;

public interface IOpenAIResponsesService
{
    IAsyncEnumerable<StreamingChunk> SendMessageStreamingAsync(
        string userMessage,
        string? previousResponseId,
        CancellationToken cancellationToken = default);

    void ClearPreviousResponseId();
    string? GetPreviousResponseId();
}
