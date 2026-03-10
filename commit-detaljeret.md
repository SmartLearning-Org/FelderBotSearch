# Detaljeret commit: Systemprompt i konsol + detaljeret prompt

## Ændringer

### 1. Browserkonsol viser systemprompt-navn
- **Chat.razor**: Ved første render logges navnet på den aktive systemprompt til browserens konsol (`console.log("Systemprompt: " + InstructionsPath)`).
- **Chat.razor**: Injiceret `IOptions<OpenAIOptions>` for at læse `InstructionsPath` (fx `Prompts/system.md`).
- **_Imports.razor**: Tilføjet `@using FelderBot.Options` og `@using Microsoft.Extensions.Options` så `IOptions<OpenAIOptions>` er tilgængelig i komponenter.

### 2. Prompts/detaljeret.md
- Ny, udvidet system-prompt til turistguiden i Christiansfeld.
- Indeholder systeminstruktioner, regler, opgaver, kontekst og few-shot eksempler.
- Bruges som reference/detaljeret prompt (kan vælges via `OpenAI:InstructionsPath` i appsettings).

### 3. Øvrige filer (tidligere commit)
- claude.md: dokumentation for AI-agenter.
- Opdateringer i Chat.razor.css, system.md og appsettings efter behov.

## Teknisk
- Systemprompt-sti kommer fra `OpenAIOptions.InstructionsPath` (standard: `Prompts/system.md`).
- Konsollog sker kun ved `firstRender` for at undgå gentagne log under re-renders.
