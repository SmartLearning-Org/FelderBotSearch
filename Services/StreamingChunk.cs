namespace FelderBot.Services;

public sealed class StreamingChunk
{
    public string? Delta { get; init; }
    public string? ResponseId { get; init; }
    public string? ErrorMessage { get; init; }

    public static StreamingChunk FromDelta(string delta) => new() { Delta = delta };
    public static StreamingChunk FromResponseId(string id) => new() { ResponseId = id };
    public static StreamingChunk FromError(string message) => new() { ErrorMessage = message };
}
