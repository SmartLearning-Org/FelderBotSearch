# FelderBot – Dokumentation af OpenAI Responses API-integration

## Formål

Denne dokumentation beskriver, hvordan OpenAI Responses API er integreret i FelderBot (Blazor-chat), med særlig vægt på endpoint, request-format, streaming og samtale-kædning.

---

## Arkitektur-overblik

Datastrømmen starter hos brugeren i browseren. Brugeren skriver i `Components/Pages/Chat.razor`, som kører som Blazor Server-komponent. Der er ingen separat SPA (Single Page Application, dvs. en selvstændig frontend-app der kører i browseren); alt kører på serveren, og når brugeren sender en besked, kalder Chat.razor den injicerede `IOpenAIResponsesService`. Servicen er implementeret i `Services/OpenAIResponsesService.cs` og bruger en `HttpClient` med BaseAddress og Bearer-token. Den sender en POST-anmodning til OpenAI's Responses-endpoint (typisk …/v1/responses). Svaret kommer tilbage som en SSE-stream (SSE = Server-Sent Events: serveren sender data løbende i en langvarig forbindelse, her med content-type text/event-stream). Servicen læser strømmen linje for linje, parser de relevante event-typer og returnerer dem til Blazor-komponenten som `StreamingChunk`-objekter – enten et stykke tekst (delta), et response-id eller en fejl. Chat.razor opdaterer UI'en med `StateHasChanged`, så brugeren ser teksten komme ind løbende.

Appen bruger OpenAI's Responses API, ikke Chat Completions API. Samtale-konteksten styres derfor ikke ved at sende hele historikken med hver gang, men ved at sende et `previous_response_id`, som API'et bruger til at kæde samtalen sammen.

---

## Konfiguration

Al konfiguration til OpenAI ligger under sektionen `"OpenAI"` i `appsettings.json` eller `appsettings.Development.json`. Sektionens navn svarer til `OpenAIOptions.SectionName` i `Options/OpenAIOptions.cs`:

```5:11:Options/OpenAIOptions.cs
    public const string SectionName = "OpenAI";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-4o";
    public string InstructionsPath { get; set; } = "Prompts/system.md";
}
```

ApiKey bør i development sættes via User Secrets. I `Program.cs` læses konfigurationen ind i `OpenAIOptions` via `Configure` og `GetSection`, og når `HttpClient` registreres for `IOpenAIResponsesService`, sættes BaseAddress, Authorization og Accept:

```9:22:Program.cs
builder.Services.Configure<OpenAIOptions>(
    builder.Configuration.GetSection(OpenAIOptions.SectionName));

builder.Services.AddSingleton<IInstructionsLoader, InstructionsLoader>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IOpenAIResponsesService, OpenAIResponsesService>();
builder.Services.AddHttpClient<IOpenAIResponsesService, OpenAIResponsesService>((sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
    var baseUrl = (opt.BaseUrl ?? "https://api.openai.com/v1").TrimEnd('/') + "/";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opt.ApiKey ?? "");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
});
```

`InstructionsPath` bruges af `InstructionsLoader` i `Services/InstructionsLoader.cs`, som læser filindholdet og cacher det; den tekst sendes som `"instructions"` i hvert request til API'et.

---

## Hvordan Responses API'et bruges

### Endpoint og metode

I `OpenAIResponsesService.SendMessageStreamingAsync` oprettes requesten som en `HttpRequestMessage` med metode Post og den relative sti `"responses"`. Da `HttpClient` allerede har BaseAddress sat i `Program.cs`, bliver den fulde URL til noget i retning af https://api.openai.com/v1/responses. Content er den serialiserede JSON-body. Koden kalder `SendAsync` med `HttpCompletionOption.ResponseHeadersRead`, så vi ikke venter på hele response body; så snart headere er tilgængelige, kan vi læse `response.Content` som en stream:

```64:72:Services/OpenAIResponsesService.cs
        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "responses") { Content = content };

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
```

`StringContent` sætter automatisk `Content-Type: application/json`; Authorization og Accept er allerede sat på `HttpClient` i `Program.cs`.

### Request body (input til API'et)

Responses API bruger et andet format end Chat Completions. I stedet for en messages-array med historik bygger koden i `OpenAIResponsesService` en dictionary med felterne `input`, `instructions`, `model` og `stream`. Feltet `input` er et array med ét objekt: en message med `type`, `role` "user" og `content` som array af `input_text`-objekter med brugerens tekst. Altså sendes kun den aktuelle brugerbesked – ikke tidligere beskeder. `instructions` kommer fra `_instructionsLoader.GetInstructionsAsync`, `model` fra `_options.Value`, og `stream` sættes til true. Hvis der findes et `previousResponseId`, tilføjes det til body som `previous_response_id`; ellers udelades feltet:

```41:63:Services/OpenAIResponsesService.cs
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
```

### Response: SSE-stream og event-typer

Svaret fra API'et er en Server-Sent Events-stream. Servicen læser strømmen med en `StreamReader` og `ReadLineAsync` i et loop. Kun linjer der starter med `"data: "` behandles; indholdet efter "data: " trimmes, og hvis det er tomt eller `"[DONE]"`, fortsætter loopet. Ellers parses det som JSON. Ved parsing-fejl bruges en tom catch der bare continue'er, så én dårlig linje ikke vælter hele strømmen. Når vi har et `JsonElement` (root), læses feltet `type` og der switch'es på det:

```84:127:Services/OpenAIResponsesService.cs
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
```

Ved `response.output_text.delta` læses `delta` og yield'es som `StreamingChunk.FromDelta(delta)`. Ved `response.completed` hentes `response.id` og yield'es som `StreamingChunk.FromResponseId(responseId)`. Ved `error` læses `message` og yield'es som `StreamingChunk.FromError`. Andre event-typer ignoreres. Den interne model er `StreamingChunk` i `Services/StreamingChunk.cs`:

```1:12:Services/StreamingChunk.cs
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
```

### Samtale-kædning med previous_response_id

Ved den første brugerbesked sendes `previous_response_id` slet ikke med i body (if'en i linje 61–62 tilføjer kun feltet når `previousResponseId` er ikke-tom). Når svaret er færdigt, kommer et `response.completed`-event med `response.id`; servicen returnerer det via `StreamingChunk.FromResponseId(responseId)`. I Chat.razor gemmes det i `_previousResponseId` og sendes med ved næste kald. Servicen har også `GetPreviousResponseId` og `ClearPreviousResponseId` i `Services/OpenAIResponsesService.cs`, som læser og sletter id i session; de bruges ikke i den nuværende Chat-UI, men gør servicen klar til session-baseret brug.

---

## Interface og service

Servicen er defineret ved `IOpenAIResponsesService` i `Services/IOpenAIResponsesService.cs`:

```1:12:Services/IOpenAIResponsesService.cs
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
```

Der findes ingen API-controller; når brugeren trykker Send, kalder Chat.razor servicen direkte. Implementeringen er `OpenAIResponsesService`, som får `HttpClient`, `IOptions<OpenAIOptions>`, `IInstructionsLoader` og `IHttpContextAccessor` injiceret. Alle kald til OpenAI går gennem denne service.

---

## Frontend-kald

Chat.razor injicerer `IOpenAIResponsesService` (linje 65–66) og har en privat `_previousResponseId` (linje 56). Når brugeren sender en besked, kaldes `SendMessage` (OnValidSubmit på EditForm, linje 35); der kaldes `SendMessageStreamingAsync` med beskedteksten og `_previousResponseId`. Resultatet konsumeres med `await foreach`; for hver chunk håndteres fejl, delta og response-id, og `StateHasChanged` kaldes så UI'en opdateres:

```114:136:Components/Pages/Chat.razor
        try
        {
            await foreach (var chunk in OpenAIService.SendMessageStreamingAsync(text, _previousResponseId))
            {
                if (chunk.ErrorMessage != null)
                {
                    errorSeen = chunk.ErrorMessage;
                    assistantMessage.Error = chunk.ErrorMessage;
                    assistantMessage.IsStreaming = false;
                    break;
                }
                if (chunk.Delta != null)
                {
                    assistantMessage.Content += chunk.Delta;
                    assistantMessage.IsStreaming = true;
                }
                if (chunk.ResponseId != null)
                {
                    _previousResponseId = chunk.ResponseId;
                    assistantMessage.IsStreaming = false;
                }
                _shouldScrollToBottom = true;
                StateHasChanged();
            }
```

Ved "Ny samtale" (knap der kalder `StartNewChat`, linje 8) nulstilles `_previousResponseId` og messages-listen:

```89:95:Components/Pages/Chat.razor
    private void StartNewChat()
    {
        _previousResponseId = null;
        _messages.Clear();
        _inputModel.Message = "";
        StateHasChanged();
    }
```

Der bruges ingen REST-endpoint fra browseren mod egen backend; alt går gennem Blazor Server og den injicerede service.

---

## Fejlhåndtering og robusthed

I starten af `SendMessageStreamingAsync` tjekkes om brugerbeskeden er tom eller kun mellemrum; i så fald yield'es én `StreamingChunk.FromError` og metoden afbryder:

```32:39:Services/OpenAIResponsesService.cs
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            yield return StreamingChunk.FromError("Beskeden må ikke være tom.");
            yield break;
        }
```

Efter `SendAsync` tjekkes `response.IsSuccessStatusCode`; hvis ikke, læses body som streng og returneres som fejl-chunk med statuskode og body:

```73:79:Services/OpenAIResponsesService.cs
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            yield return StreamingChunk.FromError($"API fejl ({(int)response.StatusCode}): {err}");
            yield break;
        }
```

Ved streaming ignoreres ukendte eller ugyldige data-linjer; ved et `error`-event returneres en error-chunk som vist i switch'en ovenfor. I Chat.razor er hele `await foreach` omkranset af try/catch (linje 113 og 147–151); ved undtagelse sættes `assistantMessage.Error` og streaming flag slås fra.

---

## Opsummering (Responses API)

POST sendes mod `{baseUrl}/responses` med JSON body: `input` (array med message med type, role og content med input_text), `instructions`, `model`, `stream: true` og evt. `previous_response_id`. Response læses som stream; parse linjer der starter med `data: ` og håndter `response.output_text.delta` (tekst), `response.completed` (gem `response.id` til næste request) og `error` (fejlbesked). HttpClient konfigureres med BaseUrl, Bearer ApiKey og Accept text/event-stream. Ved ikke-2xx læses body og returneres som fejl-chunk; ved error-events i SSE bruges message-feltet. Ved at følge denne kæde – request med input og evt. previous_response_id, streaming, parsing af delta og response_id – kan man genbruge mønsteret i lignende løsninger der bruger OpenAI Responses API.
