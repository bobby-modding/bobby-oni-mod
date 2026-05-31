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

| Context | English (msgid) | Used for |
|---|---|---|
| `...MATERIALSEARCH` | Open Material Search Overlay | Keybind name in controls menu |
| `...OVERLAYS.MATERIALSEARCH.NAME` | MATERIAL SEARCH OVERLAY | Overlay title shown in dropdown |
| `...OVERLAYS.MATERIALSEARCH.DESCRIPTION` | Searches for materials by name and highlights their locations on the map. | Overlay tooltip description |
| `...OVERLAYS.MATERIALSEARCH.BUTTON` | Material Search Overlay | Button label in overlay menu |
| `...OVERLAYS.MATERIALSEARCH.TOOLTIP` | Search for materials by typing their name | Hover tooltip for the button |
| `...OVERLAYS.MATERIALSEARCH.SEARCH_PLACEHOLDER` | Type material name... | Placeholder text in search box |
| `...OVERLAYS.MATERIALSEARCH.MASS_LABEL` | Natural Tile: {0} \| Debris: {1} \| Buildings: {2} | Mass breakdown after selecting an element |
| `...TOOLTIPS.MATCH` | Material matches the search term | Element tooltip when highlighted |
| `...TOOLTIPS.NO_MATCH` | Material does not match the search term | Element tooltip when dimmed |

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
