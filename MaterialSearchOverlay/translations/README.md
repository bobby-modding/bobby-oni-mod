# Contributing Translations

This folder contains `.po` translation files for the **Material Search Overlay** mod.  
No programming skills needed — just a text editor (Notepad works fine).

## How to edit an existing translation

1. Find the `.po` file for your language (e.g. `ja.po` for Japanese)
2. Open it in any text editor
3. Each string has this structure:
   ```po
   msgctxt "BobbyModding.MaterialSearchOverlay.MaterialSearchOverlayStrings.UI.OVERLAYS.MATERIALSEARCH.NAME"
   msgid "MATERIAL SEARCH OVERLAY"
   msgstr "MATERIAL SEARCH OVERLAY"
   ```
   — edit only the text inside the quotes on the `msgstr` line
4. **Important:** leave `{0}`, `{1}`, `{2}` exactly as they are — the game replaces them with numbers
5. Save the file (keep it UTF-8)

## Adding a new language

1. Copy an existing `.po` file (e.g. `ja.po`)
2. Rename it to your language code (e.g. `it.po` for Italian)
3. Open it and change the `Language:` header line (e.g. `Language: it`)
4. Translate all `msgstr` lines

## Reference — all strings

Paths below are shortened: `...` means
`BobbyModding.MaterialSearchOverlay.MaterialSearchOverlayStrings`.

> **The shipped `.po` files do not cover the whole table.** They were generated before the
> position list was reworked and before the box tool existed, so they have no entries at all
> for the 16 `LOCATION_*` rows or the 4 `AREA_*` rows below, and their `DESCRIPTION` entry still
> translates the older, longer
> legend text. Those lines show English in game until the `.po` files are regenerated (see
> [Regenerating the .po files](#regenerating-the-po-files)) or the missing blocks are added by hand.

| Context | English (msgid) | Used for |
|---|---|---|
| `...INPUT_BINDINGS.ROOT.MATERIALSEARCH` | Open Material Search Overlay | Keybind name in controls menu |
| `...OVERLAYS.MATERIALSEARCH.NAME` | MATERIAL SEARCH OVERLAY | Overlay title shown in the dropdown |
| `...OVERLAYS.MATERIALSEARCH.DESCRIPTION` | Search for things in your colony (e.g., hatch, iron, meal lice, atmos suit, arbor tree, artifact, booster) | Legend description shown when the overlay is open |
| `...OVERLAYS.MATERIALSEARCH.BUTTON` | Material Search Overlay | Button label in the overlay menu |
| `...OVERLAYS.MATERIALSEARCH.TOOLTIP` | Search for materials, equipment, creatures and more by typing their name {Hotkey} | Hover tooltip for the button (`{Hotkey}` is replaced with the player's keybind) |
| `...OVERLAYS.MATERIALSEARCH.SEARCH_PLACEHOLDER` | Type a name to search... | Placeholder text in the search box |
| `...OVERLAYS.MATERIALSEARCH.SELECTION_HINT` | Up/Down arrows to choose, Enter to select | Caption above the dropdown results |
| `...OVERLAYS.MATERIALSEARCH.MASS_LABEL` | Natural Tile: {0} \|  Debris: {1} \|  Buildings: {2} \|  Backwall: {3} | Mass breakdown after selecting a result |
| `...OVERLAYS.MATERIALSEARCH.COUNT_LABEL` | Found: {0} | Count summary above the position list |
| `...OVERLAYS.MATERIALSEARCH.NO_MATCHES_LABEL` | No matches found | Shown when a search matches nothing |
| `...OVERLAYS.MATERIALSEARCH.LOCATION_TILE_HEADER` | Tile areas ({0}) | Position list section title; `{0}` is the number of tile areas |
| `...OVERLAYS.MATERIALSEARCH.LOCATION_BACKWALL_HEADER` | Backwall areas ({0}) | Position list section title; `{0}` is the number of natural backwall areas |
| `...OVERLAYS.MATERIALSEARCH.LOCATION_DEBRIS_HEADER` | Debris ({0}) | Position list section title; `{0}` is the number of loose items |
| `...OVERLAYS.MATERIALSEARCH.LOCATION_BUILDING_HEADER` | Building ({0}) | Position list section title; `{0}` is the number of distinct building names |
| `...OVERLAYS.MATERIALSEARCH.LOCATION_CONTENTS_HEADER` | Contents ({0}) | Position list section title; `{0}` is the number of distinct container names |
| `...OVERLAYS.MATERIALSEARCH.LOCATION_ENTITIES_HEADER` | Entities ({0}) | Position list section title; `{0}` is the number of distinct creature names |
| `...OVERLAYS.MATERIALSEARCH.LOCATION_AREA_BOUNDS` | X {0}-{1}, Y {2}-{3} | Area row: the world-local bounding box of one connected area |
| `...OVERLAYS.MATERIALSEARCH.LOCATION_GROUP_ROW` | {0} ({1}) | Group row inside the Building, Contents and Entities sections: the display name and how many of it matched |
| `...OVERLAYS.MATERIALSEARCH.LOCATION_ITEM_ROW` | X {0} Y {1} | One row standing for a single object, area cell or backwall cell: its world-local X and Y |
| `...OVERLAYS.MATERIALSEARCH.LOCATION_CELL_ROW` | X {0} Y {1} | One cell of an expanded area: its world-local X and Y (same wording as `LOCATION_ITEM_ROW`) |
| `...OVERLAYS.MATERIALSEARCH.LOCATION_PAIR` | {0} / {1} | The two value columns: selected amount over total amount (`1,000 kg / 5,400 kg`, `240 / 402`) |
| `...OVERLAYS.MATERIALSEARCH.LOCATION_ITEM_ONE` | 1 item | Count column of a row standing for one matched object |
| `...OVERLAYS.MATERIALSEARCH.LOCATION_TILE_SINGLE` | 1 tile | Count column of a single tile inside an expanded area |
| `...OVERLAYS.MATERIALSEARCH.LOCATION_SCROLL_PERCENT` | {0}% | Scroll position shown on each section header (`100%` is the top) |
| `...OVERLAYS.MATERIALSEARCH.LOCATION_TRUNCATED` | … and {0} more | Note that rows were left out because a section hit its build cap |
| `...OVERLAYS.MATERIALSEARCH.LOCATION_EMPTY` | Nothing found in this world | Shown instead of the position list when nothing was found |
| `...OVERLAYS.MATERIALSEARCH.AREA_ADD` | Add area | Box tool button: arms the drag that ticks matching rows |
| `...OVERLAYS.MATERIALSEARCH.AREA_REMOVE` | Remove area | Box tool button: arms the drag that unticks matching rows |
| `...OVERLAYS.MATERIALSEARCH.AREA_ADD_TOOLTIP` | Drag a box over the map to select the matching cells, buildings and items inside it | Hover tooltip of the *Add area* button |
| `...OVERLAYS.MATERIALSEARCH.AREA_REMOVE_TOOLTIP` | Drag a box over the map to deselect the matching cells, buildings and items inside it | Hover tooltip of the *Remove area* button |
| `...OVERLAYS.MATERIALSEARCH.COUNT_MATERIAL` | {0} materials | Count summary per kind |
| `...OVERLAYS.MATERIALSEARCH.COUNT_EQUIPMENT` | {0} equipment | Count summary per kind |
| `...OVERLAYS.MATERIALSEARCH.COUNT_DUPLICANT` | {0} duplicants | Count summary per kind |
| `...OVERLAYS.MATERIALSEARCH.COUNT_BIONIC` | {0} bionic duplicants | Count summary per kind |
| `...OVERLAYS.MATERIALSEARCH.COUNT_BOOSTER` | {0} boosters | Count summary per kind |
| `...OVERLAYS.MATERIALSEARCH.COUNT_CRITTER` | {0} critters | Count summary per kind |
| `...OVERLAYS.MATERIALSEARCH.COUNT_EGG` | {0} eggs | Count summary per kind |
| `...OVERLAYS.MATERIALSEARCH.COUNT_FOOD` | {0} ({1}) | Food total: mass (`{0}`) and calories (`{1}`); the calories are omitted when a match has none |
| `...OVERLAYS.MATERIALSEARCH.COUNT_PLANT` | {0} plants | Count summary per kind |
| `...OVERLAYS.MATERIALSEARCH.COUNT_SEED` | {0} seeds | Count summary per kind |
| `...OVERLAYS.MATERIALSEARCH.COUNT_INDUSTRIAL` | {0} industrial products | Count summary per kind |
| `...OVERLAYS.MATERIALSEARCH.COUNT_ARTIFACT` | {0} artifacts | Count summary per kind |
| `...OVERLAYS.MATERIALSEARCH.TYPES.MATERIAL` | Material | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.EQUIPMENT` | Equipment | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.DUPLICANT` | Duplicant | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.BIONIC` | Bionic | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.BOOSTER` | Booster | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.CRITTER` | Critter | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.EGG` | Egg | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.FOOD` | Food | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.PLANT` | Plant | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.SEED` | Seed | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.INDUSTRIAL` | Industrial | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TYPES.ARTIFACT` | Artifact | Type badge on a dropdown row |
| `...OVERLAYS.MATERIALSEARCH.TOOLTIPS.MATCH` | Material matches the search term | Element tooltip while the overlay is open and the element matches |
| `...OVERLAYS.MATERIALSEARCH.TOOLTIPS.NO_MATCH` | Material does not match the search term | Element tooltip while the overlay is open and the element does not match |

### Notes for translators

- **The example names in `DESCRIPTION` must be translated too.** The search index is built
  from the game's *localized* names, so a player searching `hatch` in a German save looks for
  the German critter name. Leaving the examples in English advertises searches that return
  nothing. Keep the same shape — one example per kind — so the list still reads as a guide
  rather than a wall of text.
- `TYPES.*` are short and appear in a narrow column. If a translation is much longer than the
  English, it will be clipped.
- `{0}` placeholders must be kept exactly. The game refuses to load a translation whose
  placeholders do not match the English string, so a mismatched entry silently falls back to
  English.
- An empty `msgstr` also falls back to English, so you can translate a subset and leave the
  rest blank while you work through them.
- The `LOCATION_*` strings describe the position list. Coordinates are **world-local**: X and
  Y count cells from the bottom-left corner of the current asteroid, so the numbers are short
  and are not translated. Keep the `X` and `Y` prefixes where a language does not add them, and
  keep the `%` in `{0}%` on the same side of the number the rest of the game uses. `1 item` and
  `1 tile` are the singular unit words the game already uses elsewhere — translate them like the
  matching plurals in `COUNT_*`. `LOCATION_TRUNCATED` starts with the ellipsis character `…`,
  which should stay.
- The two `AREA_*` labels sit side by side in one narrow row of the legend, so keep them short
  and obviously opposite each other. Keep the *select* / *deselect* wording of the two tooltips
  distinguishable — that verb is the whole difference between the buttons. The drag itself asks
  nothing of the translator: the `W x H` over `N tiles` readout above the box is the game's own
  drag-tool string, translated by the base game.

## Regenerating the .po files

The complete way to refresh a `.po` file is [PPO](https://github.com/PeterHan888/PPO) from the
PLib toolset: it reads the mod's compiled assembly, lists every `LocString` in it, and writes an
entry for each one. Run it on the built mod dll, translate the new `msgstr`s, and drop the file
back into this folder. That is also how you pick up the position-list strings the shipped files
do not have yet.

Adding a single string by hand works as well:

```po
msgctxt "BobbyModding.MaterialSearchOverlay.MaterialSearchOverlayStrings.UI.OVERLAYS.MATERIALSEARCH.LOCATION_DEBRIS_HEADER"
msgid "Debris ({0})"
msgstr "Trümmers ({0})"
```

Copy the `msgctxt` path exactly as it appears in the table above (only the `...` prefix is
abbreviated there), keep the `msgid` identical to the English, and translate the `msgstr`.

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
2. **Alternative:** Open a [GitHub Issue](https://github.com/bobby-modding/bobby-oni-mod/issues) and attach your `.po` file

Use a clear title like `Translation: [language] - Material Search Overlay`.
