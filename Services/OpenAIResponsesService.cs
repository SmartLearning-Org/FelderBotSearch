using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FelderBot.Options;
using Microsoft.Extensions.Options;

namespace FelderBot.Services;

public class OpenAIResponsesService : IOpenAIResponsesService
{
    private const string PreviousResponseIdKey = "PreviousResponseId";
    private readonly HttpClient _httpClient;
    private readonly IOptions<OpenAIOptions> _options;
    private readonly IInstructionsLoader _instructionsLoader;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OpenAIResponsesService(
        HttpClient httpClient,
        IOptions<OpenAIOptions> options,
        IInstructionsLoader instructionsLoader,
        IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _options = options;
        _instructionsLoader = instructionsLoader;
        _httpContextAccessor = httpContextAccessor;
    }

    public async IAsyncEnumerable<StreamingChunk> SendMessageStreamingAsync(
        string userMessage,
        string? previousResponseId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            yield return StreamingChunk.FromError("Beskeden må ikke være tom.");
            yield break;
        }

        var instructions = await _instructionsLoader.GetInstructionsAsync(cancellationToken);
        var opt = _options.Value;

        var body = new Dictionary<string, object?>
        {
            ["input"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = "message",
                    ["role"] = "user",
                    ["content"] = new object[]
                    {
                        new Dictionary<string, object> { ["type"] = "input_text", ["text"] = userMessage.Trim() }
                    }
                }
            },
            ["instructions"] = instructions,
            ["model"] = opt.Model ?? "gpt-4o",
            ["stream"] = true
        };

        if (!string.IsNullOrEmpty(previousResponseId))
            body["previous_response_id"] = previousResponseId;

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "responses") { Content = content };

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            yield return StreamingChunk.FromError($"API fejl ({(int)response.StatusCode}): {err}");
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break;

            if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;
            var data = line.Substring(6).Trim();
            if (data.Length == 0 || data == "[DONE]") continue;

            JsonElement root;
            try
            {
                root = JsonDocument.Parse(data).RootElement;
            }
            catch
            {
                continue;
            }

            var type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

            switch (type)
            {
                case "response.output_text.delta":
                    if (root.TryGetProperty("delta", out var deltaProp))
                    {
                        var delta = deltaProp.GetString();
                        if (!string.IsNullOrEmpty(delta))
                            yield return StreamingChunk.FromDelta(delta);
                    }
                    break;
                case "response.completed":
                    if (root.TryGetProperty("response", out var respProp) && respProp.TryGetProperty("id", out var idProp))
                    {
                        var responseId = idProp.GetString();
                        if (!string.IsNullOrEmpty(responseId))
                            yield return StreamingChunk.FromResponseId(responseId);
                    }
                    break;
                case "error":
                    if (root.TryGetProperty("message", out var msgProp))
                        yield return StreamingChunk.FromError(msgProp.GetString() ?? "Ukendt fejl");
                    break;
            }
        }
    }

    public void ClearPreviousResponseId()
    {
        var ctx = _httpContextAccessor.HttpContext;
        ctx?.Session?.Remove(PreviousResponseIdKey);
    }

    public string? GetPreviousResponseId()
    {
        var ctx = _httpContextAccessor.HttpContext;
        return ctx?.Session?.GetString(PreviousResponseIdKey);
    }

    private void SetPreviousResponseId(string id)
    {
        var ctx = _httpContextAccessor.HttpContext;
        ctx?.Session?.SetString(PreviousResponseIdKey, id);
    }
}
