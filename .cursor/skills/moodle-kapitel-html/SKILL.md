---
name: moodle-kapitel-html
description: Genererer HTML-filer med samme opbygning, layout og funktionalitet som kapitelbaserede Moodle-sider (fx chatgpt-fejlrapportering.html). Bruges når brugeren beder om HTML med kapitel-navigation, dansk kapitelvis side, gradient-header, forrige/næste-knap og tastatur-navigation.
---

# Moodle kapitel-HTML

Generer HTML-filer der matcher den eksisterende kapitelvis side: samme opbygning, CSS, layout og funktionalitet (kapitel-skift, navigation, Prism-syntaksfremhævning).

## Når skill bruges

- Brugeren beder om en ny HTML-fil "med samme opbygning som ..." eller "som chatgpt-fejlrapportering.html"
- Brugeren vil have kapitelvis HTML til Moodle med dansk UI og navigation
- Brugeren vil have en side med forrige/næste-kapitel og samme look & feel

## Fremgangsmåde

1. **Brug skabelonen som udgangspunkt**  
   Læs [assets/template.html](assets/template.html) og brug den som grundstruktur. Erstat kun titel, meta-tekst og kapitel-indhold; behold alt andet (CSS, script, markup-struktur) uændret.

2. **Bevar disse elementer**
   - `lang="da"`, viewport og charset i `<head>`
   - Titel og meta i `.page-header` (gradient-header)
   - Samme eksterne ressourcer: Google Fonts (Open Sans), Prism CSS/JS (prism-tomorrow, prism-csharp, prism-javascript, prism-json)
   - Hele `:root` og alle CSS-regler
   - `.container` med `max-width: 900px`
   - Kapitel-struktur og nav-bar med samme klasser og id'er
   - Hele `<script>`-blokken (kapitel-vælger, piletaster, Prism.highlightAll)

3. **Kapitel-opbygning**
   - Ét `<article class="chapter" data-chapter="N">` per kapitel. Kun ét kapitel har `class="chapter active"` (typisk kapitel 1).
   - Hver kapitel: `<div class="chapter-header">` + `<div class="chapter-body">`.
   - Header-klasser (vælg én per kapitel, gerne roterende): `section-beige`, `section-blue`, `section-green`, `section-teal`.
   - Body-klasser: ingen ekstra = beige baggrund; `alt-bg` = blå; `alt-bg-2` = grøn.
   - Indhold i body: `p`, `h3`, `ul`/`ol`, `li`, `blockquote`, `table`, `pre`/`code` (med `class="language-xxx"` hvor relevant), evt. `.diagram-wrap` med SVG.

4. **Navigation**
   - Nav-bar: knapper med `id="btn-prev"` og `id="btn-next"`, plus `#current-num` og `#total-num`. Opdater ikke id'er eller script – de skal forblive som i skabelonen.

5. **Indhold**
   - Indsæt brugerens titel i `<title>` og i `.page-header h1`.
   - Meta-tekst under overskriften (fx "Kapitelvis gennemgang · Bladr med pilene nedenfor").
   - Erstat placeholder-kapitler med brugerens kapitler; brug samme HTML-struktur inde i hver `.chapter-body`.

## Tjekliste før aflevering

- [ ] Én fil, komplet fra `<!DOCTYPE html>` til `</html>`
- [ ] Alle kapitel har `data-chapter` 1, 2, 3, … og kun kapitel 1 har `active`
- [ ] `#total-num` svarer til antal kapitler
- [ ] Kodeblokke har passende `class="language-xxx"` til Prism
- [ ] Ingen fjernelse af CSS-variabler, nav-bar eller script
