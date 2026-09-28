# Contributing Translations

This folder contains `.po` translation files for the **Material Search Overlay** mod.  
No programming skills needed — just a text editor (Notepad works fine).

## How to edit an existing translation

1. Find the `.po` file for your language (e.g. `ja.po` for Japanese)
2. Open it in any text editor
3. Each string has this structure:
   ```po
   msgid "English text"
   msgstr "Translated text"
   ```
   — edit only the text inside the quotes on the `msgstr` line
4. **Important:** leave `{0}`, `{1}`, `{2}` exactly as they are — the game replaces them with numbers
5. Save the file

## Adding a new language

1. Copy an existing `.po` file (e.g. `ja.po`)
2. Rename it to your language code (e.g. `it.po` for Italian)
3. Open it and change the `Language:` header line (e.g. `Language: it`)
4. Translate all `msgstr` lines

## Reference — all strings

Paths below are shortened: `...` means
`BobbyModding.MaterialSearchOverlay.MaterialSearchOverlayStrings`.

| Context | English (msgid) | Used for |
|---|---|---|
| `...INPUT_BINDINGS.ROOT.MATERIALSEARCH` | Open Material Search Overlay | Keybind name in controls menu |
| `...OVERLAYS.MATERIALSEARCH.NAME` | MATERIAL SEARCH OVERLAY | Overlay title shown in dropdown |
| `...OVERLAYS.MATERIALSEARCH.DESCRIPTION` | Search by name to find anything in your colony and highlight it on the map.<br>Names can be partial and ignore case: 'hatch' finds Hatches, 'iron' finds Iron Ore.<br>Searchable kinds:<br>- Materials, e.g. Iron Ore, Bleach Stone<br>- Plants and food, e.g. Mealwood, Mirth Leaf, Raw Egg<br>- Equipment and boosters, e.g. Atmo Suit, Advanced Medical Booster<br>- Industrial products, e.g. Microchip, Atomic Power Bank<br>- Critters and duplicants, e.g. Hatches, Pufts, Duplicant<br>- Artifacts, e.g. Nuclear Power Plant Model | Legend description shown when the overlay is open |
| `...OVERLAYS.MATERIALSEARCH.BUTTON` | Material Search Overlay | Button label in overlay menu |
| `...OVERLAYS.MATERIALSEARCH.TOOLTIP` | Search for materials, equipment, creatures and more by typing their name | Hover tooltip for the button |
| `...OVERLAYS.MATERIALSEARCH.SEARCH_PLACEHOLDER` | Type a name to search... | Placeholder text in search box |
| `...OVERLAYS.MATERIALSEARCH.MASS_LABEL` | Natural Tile: {0} \| Debris: {1} \| Buildings: {2} \| Backwall: {3} | Mass breakdown after selecting an element |
| `...OVERLAYS.MATERIALSEARCH.COUNT_LABEL` | Found: {0} | Header for the per-kind count summary |
| `...OVERLAYS.MATERIALSEARCH.NO_MATCHES_LABEL` | No matches found | Shown when a search matches nothing in the colony |
| `...OVERLAYS.MATERIALSEARCH.COUNT_MATERIAL` | {0} materials | Count summary |
| `...OVERLAYS.MATERIALSEARCH.COUNT_EQUIPMENT` | {0} equipment | Count summary |
| `...OVERLAYS.MATERIALSEARCH.COUNT_DUPLICANT` | {0} duplicants | Count summary |
| `...OVERLAYS.MATERIALSEARCH.COUNT_BIONIC` | {0} bionic duplicants | Count summary |
| `...OVERLAYS.MATERIALSEARCH.COUNT_BOOSTER` | {0} boosters | Count summary |
| `...OVERLAYS.MATERIALSEARCH.COUNT_CRITTER` | {0} critters | Count summary |
| `...OVERLAYS.MATERIALSEARCH.COUNT_FOOD` | {0} food | Count summary |
| `...OVERLAYS.MATERIALSEARCH.COUNT_PLANT` | {0} plants | Count summary |
| `...OVERLAYS.MATERIALSEARCH.COUNT_INDUSTRIAL` | {0} industrial products | Count summary |
| `...OVERLAYS.MATERIALSEARCH.COUNT_ARTIFACT` | {0} artifacts | Count summary |
| `...OVERLAYS.MATERIALSEARCH.TYPES.MATERIAL` | Material | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.EQUIPMENT` | Equipment | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.DUPLICANT` | Duplicant | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.BIONIC` | Bionic | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.BOOSTER` | Booster | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.CRITTER` | Critter | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.FOOD` | Food | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.PLANT` | Plant | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.INDUSTRIAL` | Industrial | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.ARTIFACT` | Artifact | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TOOLTIPS.MATCH` | Material matches the search term | Element tooltip when highlighted |
| `...OVERLAYS.MATERIALSEARCH.TOOLTIPS.NO_MATCH` | Material does not match the search term | Element tooltip when dimmed |

### Notes for translators

- **The example names in `DESCRIPTION` must be translated too.** The search index is built
  from the game's *localized* names, so a player searching `hatch` in a German save looks
  for the German critter name. Leaving the examples in English advertises searches that
  return nothing. Keep the same shape though - one example per kind - so the list still reads
  as a guide rather than a wall of text.
- `DESCRIPTION` is multi-line. The `\n` sequences must be kept, since they are what separate
  the kinds in the overlay legend.
- Type badges (`TYPES.*`) are short and appear in a narrow column. If a translation is
  much longer than the English, it will be clipped.
- `{0}` placeholders must be kept exactly. The game refuses to load a translation whose
  placeholders do not match the English string, so a mismatched entry silently falls back
  to English.
- An empty `msgstr` also falls back to English, so you can translate a subset and leave
  the rest blank while you work through them.

## Existing translations

| Language | File |
|---|---|
| Czech | `cs.po` |
| German | `de.po` |
| Spanish | `es.po` |
| French | `fr.po` |
| Hungarian | `hu.po` |
| Japanese | `ja.po` |
| Korean | `ko.po` |
| Polish | `pl.po` |
| Portuguese (Brazil) | `pt_BR.po` |
| Russian | `ru.po` |
| Thai | `th.po` |
| Ukrainian | `uk.po` |
| Vietnamese | `vi.po` |
| Chinese (Simplified) | `zh.po` |
| Chinese (Traditional) | `zht.po` |

## How to submit

1. **Preferred:** Fork the repo on GitHub → edit the `.po` file → open a Pull Request
2. **Alternative:** Open a [GitHub Issue](https://github.com/anomalyco/bobby-oni-mod/issues) and attach your `.po` file

Use a clear title like `Translation: [language] - Material Search Overlay`.
