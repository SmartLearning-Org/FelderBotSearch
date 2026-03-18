# Gennemgang: Søgning og retrieval af dokumenter i FelderBot

Denne guide tager dig gennem den del af koden, der sørger for søgning og retrieval af dokumenter (RAG). Du læser dig fra brugerens besked til hvordan kontekst hentes fra Azure AI Search, hvordan den ender i instruktionerne til modellen, og hvordan kilder vises i chatten. Når du er færdig, har du et sammenhængende billede af hvordan søgning hænger sammen i projektet.

---

## Det store overblik

FelderBot bruger **Azure AI Search** til at hente relevante tekststykker (chunks) baseret på brugerens besked. De fundne stykker lægges ind i de instruktioner, der sendes til OpenAI, så modellen kan svare ud fra den konkrete kontekst – og evt. nævne kilder. Hvis søgning ikke er konfigureret eller fejler, kører chatten videre uden den ekstra kontekst; brugeren får stadig et svar.

I det følgende følger vi flowet trin for trin: fra det sted hvor søgning trigges, over konfiguration og selve søgekaldet, til hvordan resultaterne bliver til kontekst og kilder i UI.

---

## 1. Hvor starter søgningen?

Søgning starter når brugeren har sendt en besked og `Chat.razor` kalder `OpenAIService.SendMessageStreamingAsync(text, _previousResponseId)`. I `OpenAIResponsesService.SendMessageStreamingAsync` sker følgende i starten:

1. Den basale systemprompt hentes via `_instructionsLoader.GetInstructionsAsync()`.
2. **Før der sendes noget til OpenAI**, kaldes `GetRagContextAsync(userMessage, cancellationToken)`.

Så **brugerens besked er det der driver søgningen**: den sendes ufiltreret videre som søgetekst. Det er med vilje – brugeren skriver fx "Hvad kan man se i Christiansfeld?" og netop den sætning bruges til at finde relevante dokumentchunks i Azure AI Search.

`GetRagContextAsync` er en privat metode der blot finder ud af hvor mange resultater der skal hentes (fra indstillinger, mindst 5), kalder `_searchService.SearchAsync(userMessage, top, cancellationToken)` og returnerer listen. Hvis Azure kaster en exception, logges en warning og metoden returnerer en tom liste – så chatten fortsætter uden RAG-kontekst i stedet for at fejle. Her møder vi altså **søgegrænsefladen** `ISearchService`: den tager en søgetekst og et antal, og returnerer en liste af `SearchResult` (id, indhold, evt. kilde). Resten af søgelogikken ligger i implementeringen af den grænseflade og i de indstillinger den læser. Dem gennemgår vi nu.

---

## 2. Hvilke indstillinger styrer søgningen?

Alt hvad søgningen bruger af adresse, index, feltnavne og antal resultater kommer fra **AzureSearchOptions**. Denne klasse ligger i `Options/AzureSearchOptions.cs` og bindes til konfigurationssektionen `**AzureAISearch`** i `Program.cs`. Når du åbner `appsettings.json`, ser du typisk noget i retning af:

- **Endpoint** – URL til din Azure AI Search-tjeneste (fx `https://turistsearch.search.windows.net`). Hvis den er tom, vil søgningen blive sprunget over.
- **IndexName** – navnet på det index du søger i (fx `rag-1773382063367`). Også her: tom = ingen søgning.
- **ApiKey** – nøglen til Azure. Den bør sættes via User Secrets i development (`dotnet user-secrets set "AzureAISearch:ApiKey" "..."`) og ikke stå i repo.
- **Top** – hvor mange chunks der skal hentes pr. brugerbesked (standard 5). Det tal bruges både som "antal resultater" i Azure og som grænse for hvor meget kontekst der lægges i instruktionerne.
- **SemanticConfigurationName** – valgfri. Hvis du har sat en semantic konfiguration på indexet og angiver dens navn her, bruges semantic search. Ellers bruges "almindelig" keyword-søgning.
- **IdFieldName**, **ContentFieldName**, **SourceFieldName** – feltnavnene i dit Azure-index. De skal matche det index du faktisk har:
  - **IdFieldName** (default `"id"`) – feltet der indeholder et entydigt id for hvert chunk.
  - **ContentFieldName** (default `"content"`) – feltet med den tekst der skal ind i LLM-konteksten; det er denne tekst der ender i instruktionerne.
  - **SourceFieldName** (valgfri) – fx `"title"` eller `"source"`. Hvis du sætter det, bruges værdien både i kontekstteksten som kildeangivelse og i listen "Kilder:" under svaret i UI. Hvis du lader det være null/ude, er der ingen kildevisning.

I `appsettings.Development.json` overstyrer projektet ofte kun OpenAI; Azure-indstillinger arves fra `appsettings.json`, medmindre du eksplicit sætter `AzureAISearch` også i Development (fx anden IndexName eller ApiKey via User Secrets). Når du læser koden, er det derfor disse værdier du skal kigge på for at forstå hvilket index der bruges og hvilke felter der læses.

---

## 3. Hvordan kommer SearchClient ind i billedet?

Søgekaldene foretages ikke direkte fra `OpenAIResponsesService`, men via en **SearchClient** fra Azure SDK. Den oprettes i `Program.cs` som en singleton:

- Konfigurationen læses fra `IOptions<AzureSearchOptions>`.
- Hvis **Endpoint**, **IndexName** eller **ApiKey** er tomme, oprettes en placeholder-klient mod `https://localhost/` med indexnavn `"placeholder"`. Den bruges så servicen ikke crasher ved manglende config; selve søgningen bliver alligevel sprunget over i `AzureSearchService` fordi den tjekker Endpoint og IndexName før den kalder Azure.
- Hvis alt er udfyldt, bygges en rigtig `SearchClient` med endpoint (uden afsluttende `/`), indexnavn og `AzureKeyCredential(ApiKey)`.

`ISearchService` registreres som **scoped** med implementeringen `AzureSearchService`. Det betyder at hver HTTP-anmodning (hver brugerbesked) får sin egen instans af `AzureSearchService`, som igen bruger den fælles `SearchClient` og de indlæste `AzureSearchOptions`. Når du læser `AzureSearchService` næste afsnit, ved du altså at `_searchClient` og `_options` kommer fra denne registrering og fra `appsettings`/User Secrets.

---

## 4. Søgegrænsefladen og resultatmodellen

Før vi ser implementeringen, er det nyttigt at forstå hvad den **skal** levere. Grænsefladen er `ISearchService` i `Services/ISearchService.cs`: én metode, `SearchAsync(string query, int top, CancellationToken cancellationToken = default)`. Den tager søgeteksten (brugerens besked), antal ønskede resultater og en valgfri cancellation token, og returnerer en liste af **SearchResult**.

`SearchResult` (i `Models/SearchResult.cs`) har tre felter:

- **Id** – dokument-/chunk-id fra indexet. Bruges ikke i den nuværende kontekstblok, men er nyttig til logging eller deduplikering.
- **Content** – den tekst der faktisk indgår i LLM-konteksten (aldrig null i praksis; kan være tom string). Kommer fra det felt du har konfigureret som `ContentFieldName`.
- **Source** – valgfri kilde (fx dokumenttitel). Hvis den er sat, bruges den både i kontekstteksten og i "Kilder:" i UI.

Rækkefølgen af resultaterne følger Azure AI Search’ rangering (relevans eller semantic score). Så når `OpenAIResponsesService` kalder `SearchAsync(userMessage, top)`, får den tilbage de øverste `top` resultater i den rækkefølge Azure vurderer som mest relevante.

---

## 5. Ind i AzureSearchService – hvad sker der ved et søgekald?

Implementeringen ligger i `Services/AzureSearchService.cs`. Den injicerer `SearchClient` og `IOptions<AzureSearchOptions>` og implementerer `SearchAsync`. Du kan læse metoden som en række trin.

**Først tjekkes konfigurationen.** Hvis `_options.IndexName` eller `_options.Endpoint` er null eller kun mellemrum, returneres straks en tom liste uden at kalde Azure. ApiKey tjekkes ikke her; hvis den er forkert, vil det efterfølgende kald til Azure give fejl (som fanges i `GetRagContextAsync`).

**Derefter bestemmes hvilke felter der skal hentes.** Der bygges en liste: `IdFieldName`, `ContentFieldName` og – hvis `SourceFieldName` er sat – også den. Kun disse felter anmodes fra Azure (via **Select**), så payloaden holdes lille.

**Så sættes søgeparametrene.** Et `SearchOptions`-objekt oprettes med `Size = top` (antal resultater), og de valgte felter tilføjes til `Select`. Hvis `SemanticConfigurationName` i options er sat og ikke tom, sættes `SemanticSearch` på options med netop det konfigurationsnavn – det aktiverer semantic search, forudsat at din Azure Search-tjeneste og index er sat op til det.

**Nu kalder servicen Azure.** `_searchClient.SearchAsync<SearchDocument>(query, searchOptions, cancellationToken)` udføres. `SearchDocument` er en slags ordbog; feltværdier er ikke strengetyper fra start, så servicen bruger en lille hjælpemetode **GetString(doc, key)** til at læse værdier sikkert: hvis nøglen mangler eller værdien er null returneres null; er værdien allerede en string bruges den; ellers bruges `ToString()`. På den måde kan felter der i indexet er tal eller datoer stadig bruges som Id, Content eller Source uden at koden kaster.

**Til sidst mappes hvert Azure-resultat til en `SearchResult`.** For hvert element i `response.Value.GetResultsAsync()` læses Id, Content og evt. Source via `GetString` med de konfigurerede feltnavne; Content sættes til `""` hvis null. Listen af `SearchResult` returneres. Denne liste er det som `GetRagContextAsync` får tilbage og som derefter bliver til kontekst og kilder.

---

## 6. Fra søgeresultater til instruktioner og kilder

Når `GetRagContextAsync` har returneret (muligvis tom) liste, sker resten i `OpenAIResponsesService.SendMessageStreamingAsync`.

**Kontekstblokken bygges.** Hvis der er mindst ét resultat, laves en lang streng: for hvert `SearchResult` bliver enten kun `Content` brugt, eller – hvis `Source` er sat – teksten `"[{Source}]\n{Content}"`. De enkelte resultater joines med `"\n\n---\n\n"`. Denne streng sættes ind efter en fast header (noget i retning af "Brug følgende kontekst til at besvare brugeren. Svar ud fra denne kontekst og nævn kilder hvis det giver mening.") og tilføjes til den allerede hentede systemprompt. Det er den samlede streng der sendes som **instructions** til OpenAI. Så modellen ser både den generelle rolle og den konkrete dokumentkontekst.

**Kilder til UI.** Fra søgeresultaterne udtrækkes alle ikke-tomme `Source`-værdier, deduplikeres og samles i en liste. Den liste sendes til chatten som den **første** chunk i streamen via `StreamingChunk.FromSources(sources)`. Det betyder at UI kan vise "Kilder:" under assistent-svaret, selv mens teksten stadig streames. I den nuværende kode er den eneste søgeparameter der sendes til Azure brugerens rå besked og `top`; der er ingen ekstra filtre eller query-parametre.

---

## 7. Hvordan brugeren ser kilder

Kæden fra søgning til skærm er kort. `StreamingChunk` (i `Services/StreamingChunk.cs`) har en egenskab **Sources** (`IReadOnlyList<string>?`) og en factory **FromSources(sources)** der sætter den. Når `Chat.razor` itererer over chunks fra `SendMessageStreamingAsync`, og en chunk har `Sources` sat, kopieres listen over til `assistantMessage.Sources`. Under hver assistent-boble tjekker UI’en om `msg.Sources` har elementer; hvis ja, vises en "Kilder:"-sektion med de værdier. Så kilder kommer direkte fra feltet du har konfigureret som **SourceFieldName** i Azure-indexet – typisk titel eller dokumentnavn – og ender som den distinkte liste under svaret.

---

## 8. Et samlet flow – fra besked til kilder

Du har nu fulgt hele vejen. Kort opsummeret:

1. Brugeren sender en besked → **Chat.razor** kalder **OpenAIResponsesService.SendMessageStreamingAsync**.
2. Servicen henter systemprompt og kalder **GetRagContextAsync(userMessage)**. Her læses **Top** fra **AzureSearchOptions** (minimum 5 hvis ≤ 0), og **ISearchService.SearchAsync(userMessage, top)** kaldes. Ved fejl logges warning og returneres tom liste.
3. **AzureSearchService.SearchAsync** tjekker Endpoint/IndexName; ved tom returneres tom liste. Ellers bygges Select og SearchOptions (Size, evt. SemanticSearch), Azure kaldes via **SearchClient**, og hvert dokument mappes til **SearchResult** (Id, Content, Source) via de konfigurerede feltnavne og **GetString**.
4. **OpenAIResponsesService** bygger kontekstblok fra **SearchResult**-listen (med evt. `[Source]` foran hvert stykke), appender den til instructions med den faste header, udtrækker distinkte kilder og sender dem som første chunk med **FromSources**. Derefter sendes request til OpenAI med de fulde instructions og brugerbeskeden.
5. **Chat.razor** modtager chunk med **Sources**, sætter **assistantMessage.Sources**, og viser "Kilder:" under boblen.

Søgning og retrieval er dermed styret af **AzureSearchOptions** og **AzureAISearch** i konfigurationen; selve flowet er samlet omkring **ISearchService**, **AzureSearchService** og **GetRagContextAsync**, og kilder følger med fra index-felt til UI gennem **StreamingChunk** og **ChatMessage.Sources**.