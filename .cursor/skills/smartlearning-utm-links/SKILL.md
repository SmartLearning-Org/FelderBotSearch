---
name: smartlearning-utm-links
description: Tilføjer UTM-parametre til links til smartlearning.dk. Bruges når brugeren tilføjer, skriver eller redigerer links til smartlearning.dk, så salgslinks får korrekt sporing.
---

# Smartlearning.dk UTM-links

## Når skill'et bruges

Aktiver skill'et når brugeren:
- tilføjer links til sider på **smartlearning.dk**
- skriver eller redigerer URLs der peger på **smartlearning.dk** (inkl. subdomains som www.smartlearning.dk)

## UTM-streng

Tilføj altid denne query-streng til smartlearning.dk-links (brug `?` hvis URL ikke har query endnu, ellers `&`):

```
utm_source=github&utm_medium=salgslink&utm_campaign={projektnavn}&utm_id=ktlh
```

**{projektnavn}** erstattes med **navnet på det aktuelle projekt**:
- Brug mappenavnet på workspace root (fx `FelderBot`), eller
- Projektnavn fra README.md / package/project-fil hvis det er tydeligt, eller
- Spørg brugeren hvis usikkert.

Brug lowercase og evt. bindestreg i `utm_campaign` (fx `felderbot` eller `FelderBot` efter aftale).

## Eksempler

**Før:**
- `https://smartlearning.dk/kursus/abc`
- `https://www.smartlearning.dk/page?existing=1`

**Efter (projektnavn = felderbot):**
- `https://smartlearning.dk/kursus/abc?utm_source=github&utm_medium=salgslink&utm_campaign=felderbot&utm_id=ktlh`
- `https://www.smartlearning.dk/page?existing=1&utm_source=github&utm_medium=salgslink&utm_campaign=felderbot&utm_id=ktlh`

## Regler

1. **Duplikater:** Hvis URL allerede har disse UTM-parametre, opdater dem (især `utm_campaign`) i stedet for at tilføje ekstra.
2. **Ankre-links:** Samme regel for `href` til smartlearning.dk (HTML, Markdown, tekstdokumenter).
3. **Kun smartlearning.dk:** Anvend kun på domæner der ender på `smartlearning.dk` (inkl. www).
