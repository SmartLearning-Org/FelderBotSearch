---
name: documenter-chatgpt-integration
description: Gennemgår kode grundigt og laver en udviklerrettet beskrivelse af de vigtigste dele omkring em bestemt feature i en løsning. Bruges når brugeren vil have dokumentation der forklarer hvordan programmet virker, så andre kan udvikle lignende programmer. Output gemmes som .md-fil. Brugeren kan angive hvad der skal lægges særlig vægt på (parameter).
---

# Dokumenter ChatGPT/LLM-integration

## Formål

At producere en markdown-dokumentationsfil der beskriver,en given feature er integreret i en given løsning. Målgruppen er udviklere der skal forstå og kunne bygge lignende løsninger.

## Parameter: Hvilken feature skal gennemgås.
## Parameter: Hvad skal der lægges vægt på?

Brugeren kan angive **hvad der skal lægges vægt på** i dokumentationen. Afpas indhold og dybde derefter. Mulige vægtninger (én eller flere):

- **Arkitektur** – datastrøm, lag (frontend/backend/API), diagrammer
- **Konfiguration** – API-nøgle, options, miljøvariabler, appsettings
- **Prompt og beskeder** – system-prompt, user-prompt, JSON-formatkrav
- **API-kald** – request-opbygning, endpoint, headers, response_format
- **Request/response og DTO’er** – modeller til request/response, JSON-serialisering
- **Domænemodel** – hvordan API-svar mappes til applikationens modeller
- **Controller og service** – endpoints, dependency injection, interfaces
- **Frontend-kald** – hvordan UI kalder backend (fetch/axios, fejlhåndtering)
- **Fejlhåndtering og robusthed** – HTTP-fejl, parsing-fejl, fallbacks
- **Sikkerhed** – hvor API-nøgle holdes, hvad der ikke eksponeres

Hvis brugeren ikke angiver vægtning, lav en **balanceret gennemgang** af alle relevante dele.

## Workflow

1. **Gennemgå koden grundigt** – læs de filer der indeholder integrationen (backend-service, controller, konfiguration, evt. frontend-api).
2. **Identificer kæden** – brugerinput → frontend → backend → LLM-API → parsing → respons til bruger.
3. **Beskriv de vigtigste dele** – med særlig vægt på de områder brugeren har bedt om.
4. **Indsæt konkrete kodeudsnit** – hvor det understøtter forståelsen (prompt, request/response, DTO’er, eksempel på kald).
5. **Gem som .md-fil** – færdig dokumentation i en markdown-fil (evt. sti angivet af brugeren).

## Anbefalet dokumentstruktur

- Formål og målgruppe
- Arkitektur-overblik (evt. med simpel beskrivelse eller ASCII/SVG)
- Konfiguration (hvor står nøgle/model, hvordan indlæses det)
- Prompt og beskeder (system/user, krav til output-format)
- Request mod API (endpoint, body, response_format)
- Request/response DTO’er og domænemodel
- Controller og service (interface, endpoint, validering)
- Frontend-kald (eksempel på kald og fejlhåndtering)
- Fejlhåndtering og JSON-indstillinger
- Opsummering til udviklere (korte trin til at bygge noget lignende)

Tilpas kapitler efter hvad der findes i koden og efter den angivne vægtning.

## Sprog

Skriv dokumentationen på samme sprog som brugerens anmodning (typisk dansk).
