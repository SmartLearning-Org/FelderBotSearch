# FelderBot – ChatGPT-samtale med Blazor og OpenAI Responses API

Webbaseret chat med OpenAI via Responses API, streaming og samtale-kædning med `previous_response_id`.

Koden er en del af en opgave på faget [Programmering af AI](https://www.smartlearning.dk/akademiuddannelser/it/programmering-af-ai?utm_source=github&utm_medium=salgslink&utm_campaign=felderbot&utm_id=ktlh) hos SmartLearning.

## Krav

- .NET 9 SDK
- OpenAI API-nøgle

## Konfiguration

1. **API-nøgle** (aldrig commit til repo):
   ```bash
   dotnet user-secrets set "OpenAI:ApiKey" "din-api-nøgle"
   ```

2. **Valgfri** i `appsettings.json` eller `appsettings.Development.json`:
   - `OpenAI:BaseUrl` – standard: `https://api.openai.com/v1`
   - `OpenAI:Model` – f.eks. `gpt-5.2`
   - `OpenAI:InstructionsPath` – sti til system-prompt (standard: `Prompts/system.md`)

## System-prompt (.md)

Rediger `Prompts/system.md` for at ændre assistentens personlighed og instruktioner. Filen caches i 5 minutter.

## Kørsel

```bash
dotnet run
```

Åbn browseren på den viste URL (f.eks. https://localhost:5001) og gå til **Chat** i menuen.

## Funktionalitet

- **Streaming:** Svar vises token for token.
- **Samtale-kædning:** `previous_response_id` gemmes i session, så opfølgende beskeder er en del af samme samtale.
- **Ny samtale:** Knappen "Ny samtale" rydder session og starter forfra.
- **Lys tema:** Kun lys UI (ingen dark mode).
