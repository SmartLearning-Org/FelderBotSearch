# FelderBot – Dokumentation for AI-agenter

Denne fil beskriver projektet så en AI-agent (fx Claude) hurtigt kan forstå arkitekturen, konventioner og udvidelsespunkter uden at læse hele kodebasen.

**Vedligeholdelse:** Cursor-reglen i `.cursor/rules/opdater-claude-md.mdc` pålægger at opdatere denne fil løbende ved strukturelle eller funktionelle ændringer i projektet.

---

## 1. Hvad er projektet?

**FelderBot** er en webbaseret chat-applikation der taler med OpenAI via **Responses API** (ikke Chat Completions API). Brugeren chatter i en Blazor Server-side UI; svar streames token for token, og samtalen kædes via `previous_response_id`. Appen er en **RAG** (Retrieval-Augmented Generation): før hvert OpenAI-kald hentes relevante dokumenter fra et **Azure AI Search**-index, og konteksten injiceres i anmodningen. Appen er en opgave i faget "Programmering af AI" og har et fokus på **Christiansfeld** (turist-assistent).

- **Sprog:** C#, Blazor, dansk UI og system-prompt
- **Runtime:** .NET 9, ASP.NET Core
- **Frontend:** Blazor Server (InteractiveServer), Bootstrap, Markdig til Markdown i assistent-svar
- **API:** OpenAI Responses API (`POST …/v1/responses`), SSE-streaming
- **RAG:** Azure AI Search (keyword/semantic); kontekst tilføjes til `instructions` før POST

---

## 2. Projektstruktur

```
FelderBot/
├── Program.cs                 # DI, HttpClient, SearchClient, Session, Blazor Server
├── appsettings.json           # OpenAI + AzureAISearch-sektioner
├── appsettings.Development.json
├── Components/
│   ├── App.razor              # HTML-shell, scrollChatToBottom JS
│   ├── Routes.razor           # Router, MainLayout, FocusOnNavigate
│   ├── Layout/MainLayout.razor
│   ├── Pages/
│   │   ├── Chat.razor         # Hovedside: chat UI, SendMessage, StartNewChat
│   │   ├── Chat.razor.css     # Scoped CSS for chat
│   │   └── Error.razor
│   └── _Imports.razor
├── Models/
│   ├── ChatMessage.cs         # IsUser, Content, IsStreaming, Error
│   └── SearchResult.cs        # RAG: Id, Content, Source (søgeresultat)
├── Options/
│   ├── OpenAIOptions.cs       # SectionName "OpenAI", BaseUrl, ApiKey, Model, InstructionsPath
│   └── AzureSearchOptions.cs  # SectionName "AzureAISearch", Endpoint, IndexName, ApiKey, Top, feltnavne
├── Prompts/
│   └── system.md              # System-prompt (instructions) – caches 5 min
├── Services/
│   ├── IOpenAIResponsesService.cs
│   ├── OpenAIResponsesService.cs   # RAG: kalder ISearchService, bygger instructions med kontekst; POST, SSE
│   ├── IInstructionsLoader.cs
│   ├── InstructionsLoader.cs       # Læser InstructionsPath, MemoryCache 5 min
│   ├── ISearchService.cs           # SearchAsync(query, top) → RAG-chunks
│   ├── AzureSearchService.cs       # Azure AI Search (keyword/semantic), feltnavne fra options
│   └── StreamingChunk.cs           # Delta, ResponseId, ErrorMessage
├── README.md
├── Dokumentation-Responses-API.md  # Detaljeret API-dokumentation
└── claude.md                  # Denne fil
```

**Vigtige filer for en agent:**

- **Ændre adfærd/instruktioner:** `Prompts/system.md`
- **Ændre API/konfiguration:** `appsettings*.json`, `Options/OpenAIOptions.cs`, `Options/AzureSearchOptions.cs`
- **Ændre chat-flow/UI:** `Components/Pages/Chat.razor`, `Services/OpenAIResponsesService.cs`
- **RAG / søgning:** `Services/ISearchService.cs`, `Services/AzureSearchService.cs`, `Options/AzureSearchOptions.cs` (feltnavne, Top, SemanticConfigurationName)
- **Forstå API-format:** `Dokumentation-Responses-API.md`

---

## 3. Dataflow (kort)

1. Bruger skriver i `Chat.razor` og trykker Send.
2. `Chat.razor` kalder `IOpenAIResponsesService.SendMessageStreamingAsync(text, _previousResponseId)`.
3. **RAG:** `OpenAIResponsesService` kalder `ISearchService.SearchAsync(userMessage, top)` (Azure AI Search). Søgeresultater konkateneres til en kontekst-blok. Hvis søgning fejler, logges fejl og bruges kun base instructions (ingen kontekst).
4. `OpenAIResponsesService` bygger request: `input` (kun nuværende brugerbesked), `instructions` (base fra `InstructionsLoader` + evt. RAG-kontekst-blok), `model`, `stream: true`, evt. `previous_response_id`.
5. POST til `{BaseUrl}/responses`; response læses som SSE-stream.
6. Servicen parser linjer `data: {...}`, udtrækker `response.output_text.delta`, `response.completed` (response.id), `error` og yield'er `StreamingChunk`.
7. `Chat.razor` opdaterer `_messages` og `_previousResponseId` ved hver chunk og kalder `StateHasChanged()`; ved `response.completed` gemmes id til næste request.
8. "Ny samtale" sætter `_previousResponseId = null` og tømmer `_messages`.

**Vigtigt:** Samtalehistorik sendes **ikke** med hver gang; kun den aktuelle brugerbesked og `previous_response_id` bruges. Kontekst kommer fra **Azure AI Search** (RAG) og tilføjes til `instructions`; derefter håndteres samtale af OpenAI Responses API.

---

## 4. Konfiguration

- **OpenAI (sektion `OpenAI`):** `BaseUrl`, `ApiKey`, `Model`, `InstructionsPath`.  
  API-nøgle: `dotnet user-secrets set "OpenAI:ApiKey" "din-nøgle"`. Aldrig commit ApiKey.  
  System-prompt læses fra `InstructionsPath` (default `Prompts/system.md`), caches 5 min via `InstructionsLoader`.
- **Azure AI Search (sektion `AzureAISearch`):** `Endpoint`, `IndexName`, `ApiKey`, `Top` (antal chunks, default 5), `SemanticConfigurationName` (valgfri), `IdFieldName`, `ContentFieldName`, `SourceFieldName` (valgfri).  
  Nøgle kan sættes via User Secrets: `dotnet user-secrets set "AzureAISearch:ApiKey" "..."`.  
  Hvis Endpoint/IndexName/ApiKey mangler, udelades RAG (ingen søgning); chat virker stadig med kun system-prompt.

---

## 5. Nøgle-typer og grænseflader

| Type | Formål |
|------|--------|
| `ChatMessage` | Bruger/assistent-boble: `IsUser`, `Content`, `IsStreaming`, `Error` |
| `SearchResult` | RAG-chunk: `Id`, `Content`, `Source` (fra Azure AI Search) |
| `StreamingChunk` | Én af: `Delta`, `ResponseId`, `ErrorMessage` (factory-metoder: `FromDelta`, `FromResponseId`, `FromError`) |
| `IOpenAIResponsesService` | `SendMessageStreamingAsync(userMessage, previousResponseId)`, `ClearPreviousResponseId`, `GetPreviousResponseId` |
| `IInstructionsLoader` | `GetInstructionsAsync()` – returnerer system-prompt-tekst |
| `ISearchService` | `SearchAsync(query, top)` – returnerer RAG-chunks fra Azure AI Search |
| `OpenAIOptions` | BaseUrl, ApiKey, Model, InstructionsPath |
| `AzureSearchOptions` | Endpoint, IndexName, ApiKey, Top, SemanticConfigurationName, IdFieldName, ContentFieldName, SourceFieldName |

`OpenAIResponsesService` har også session-baseret `GetPreviousResponseId`/`ClearPreviousResponseId`/`SetPreviousResponseId`; Chat-UI'en bruger i øjeblikket kun sin egen `_previousResponseId` (komponent-state), ikke session.

---

## 6. SSE-events fra Responses API

Servicen håndterer kun disse event-typer i `data:`-linjer:

- **`response.output_text.delta`** – felt `delta`: stykke af svar-tekst → `StreamingChunk.FromDelta(delta)`
- **`response.completed`** – felt `response.id` → `StreamingChunk.FromResponseId(id)` (bruges til næste `previous_response_id`)
- **`error`** – felt `message` → `StreamingChunk.FromError(message)`

Andre events ignoreres. Linjer der ikke starter med `data: ` eller som er `[DONE]`/tomme springes over. Parse-fejl i en enkelt linje ignoreres (continue).

---

## 7. Frontend / Chat-UI

- **Route:** `/` (Chat.razor er default-side).
- **Render mode:** `InteractiveServer`.
- Brugerbeskeder vises som plain text (HTML-encoded, newlines som `<br />`); assistent-svar rendres som **Markdown** via Markdig (`Markdown.ToHtml`) med `UsePipeTables()` så pipe-tabeller rendres korrekt (kræver `@using Markdig`).
- Streaming-indikator: `▌` (`.chat-cursor`) mens `IsStreaming` er true.
- Fejl vises i rød under boblen (`msg.Error`).
- Scrolling: `scrollChatToBottom` kaldes via JS efter render når `_shouldScrollToBottom` er sat (`OnAfterRenderAsync`).
- Ingen dark mode; kun lys tema.

---

## 8. Fejlhåndtering

- Tom brugerbesked: servicen returnerer én `StreamingChunk.FromError("Beskeden må ikke være tom.")` og afbryder.
- **RAG/søgning fejler:** Logges som warning; brugeren får svar uden RAG-kontekst (kun base instructions).
- HTTP ikke-2xx: body læses og returneres som én error-chunk med status og body.
- SSE `error`-event: message returneres som error-chunk.
- I `Chat.razor`: try/catch omkring `await foreach`; ved exception sættes `assistantMessage.Error` og `IsStreaming = false`.

---

## 9. Udvidelser en agent kan lave

- **Ny side/route:** Tilføj `Components/Pages/*.razor` med `@page "/path"` og evt. link i layout/menu.
- **Ændre system-prompt:** Rediger `Prompts/system.md` (eller sæt anden `InstructionsPath` i config).
- **Anden model/endpoint:** Ændre `OpenAI:Model` og `OpenAI:BaseUrl`; servicen bruger allerede disse.
- **Session-baseret previous_response_id:** Chat.razor kan ud over egen state kalde `OpenAIService`’s `GetPreviousResponseId`/`ClearPreviousResponseId` og bruge session ved genindlæsning.
- **Flere input-typer:** Request-body under `input` kan udvides (fx filer) ift. Responses API-dokumentation; `OpenAIResponsesService` skal bygge body derefter.
- **Dark mode:** Tilføj tema-variabler og klasse på body/container; styles i `Chat.razor.css` og evt. `app.css`.

---

## 10. Kørsel og krav

- **Krav:** .NET 9 SDK, OpenAI API-nøgle (User Secrets). Valgfrit: Azure AI Search (Endpoint, IndexName, ApiKey) for RAG; uden det virker chat med kun system-prompt.
- **Kør:** `dotnet run` – åbn den viste URL (fx https://localhost:5001). Chat er på forsiden.

---

## 11. Referencer i koden

- **Responses API-dokumentation (projektet):** `Dokumentation-Responses-API.md`
- **Options-binding:** `Program.cs` → `Configure<OpenAIOptions>`, `Configure<AzureSearchOptions>`
- **HttpClient for OpenAI:** Registreret i `Program.cs` med BaseAddress, Bearer ApiKey, Accept `text/event-stream`
- **SearchClient (Azure AI Search):** Registreret i `Program.cs`; ved manglende config bruges placeholder (søgning udelades i `AzureSearchService`).
- **Instructions cache:** `InstructionsLoader` + `IMemoryCache`, 5 min
- **RAG-flow:** `OpenAIResponsesService.GetRagContextAsync` → `ISearchService.SearchAsync`; kontekst tilføjes til `instructions`.

En AI-agent bør læse `Dokumentation-Responses-API.md` ved ændringer i request/response-format eller streaming-logik. Ved ændring af søgning eller feltnavne: `AzureSearchOptions`, `AzureSearchService`, `ISearchService`.
