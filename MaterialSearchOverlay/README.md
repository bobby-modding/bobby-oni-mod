# Material Search Overlay

An Oxygen Not Included mod that lets you search the colony by name and highlights the matching things on the map.

## Features

- **Search by name** — one search box covers materials, equipment, duplicants, bionic duplicants, boosters, critters, critter eggs, food, plants, seeds, industrial products, and artifacts
- **Own overlay icon** — the overlay bar button and the overlay legend use a custom magnifier icon instead of the vanilla materials overlay icon
- **Type badges** — every dropdown result is labelled with what kind of thing it is, so a name that exists in several categories can still be picked unambiguously
- **Keyboard selection** — after typing a name, the arrow keys step through the dropdown (hold one to keep moving) and Enter pins the highlighted row, exactly as clicking it would; a caption above the results spells the shortcut out
- **Result cap** — a search keeps at most `Maximum Search Results` matches (50 by default). The dropdown rows and the map highlight are exactly the same set, so a one-letter query stays bounded in both places
- **Auto focus** — the search box takes the keyboard the moment the overlay opens, whether it was opened from the overlay menu button or from the keybind, so you can start typing straight away
- **Keybind in tooltip** — the button's hover tooltip ends with the currently assigned key, spelled out the same way vanilla overlays do, and it follows rebinds. A keybind that stacks modifiers is spelled out in full (`[SHIFT + CTRL + F]`); on its own the game drops the modifier names and shows `[+ F]`
- **Visual highlight** — matching entities light up in place; for elements, matching tiles, natural backwalls, buildings, and debris are colored in the element's natural color and everything else is dimmed to a flat grey, so the overlay opens on a grey map and only the matches pick up color
- **Mass totals** — selecting an element reports its total mass broken down by natural tiles, visible natural backwalls, debris, and buildings
- **Count summary** — for everything else the label reports how many of each kind were found in the colony, counting stacked items by their units (299 stored Arbor Acorns read as `299 seeds`, not as the one or few piles they occupy); food instead reports its combined mass and calories (e.g. `20 kg (80,000 kcal)`), or mass only when calorie data is unavailable
- **Filtered hover tooltip** — while the overlay is active, hovering a cell lists only what the search matched; the cell's own element block appears only when that element matched too, so searching `hatch egg` does not also explain the oxygen the egg is sitting in
- **Search-aware clicking** — clicking a cell picks the matching thing straight away instead of cycling through everything else in it, and clicking again walks through the other matches; a cell with no match selects nothing until the search is cleared
- **Tracked highlights** — matched entities are found once when you search, then stay lit up as they walk around; anything created afterwards is picked up by running the search again
- **Debug logging** — optional verbose logging to `Player.log` (toggle in mod options)

## How each kind is matched

| Kind | Matched by | Found by searching for |
|---|---|---|
| Material | Element name | Any element name, e.g. `GOLD`, `WATER`, `INSULATION` |
| Equipment | Prefab name | e.g. `Slick` suit. Worn gear lights up its **wearer**, since worn suits cannot be highlighted themselves |
| Duplicant | Model label | `Standard Duplicant` or `Duplicant` — matches every non bionic duplicant, regardless of their individual names |
| Bionic | Model label | `Bionic Duplicant` — matches every bionic duplicant |
| Booster | Upgrade name | e.g. `Nutrient Loader`. Highlights bionic duplicants wearing it, plus loose boosters in storage |
| Critter | Species name | e.g. `Pacu`, `Puft` |
| Egg | Prefab name, tagged `GameTags.Egg` | e.g. `Hatch Egg`, `Pacu Egg`; matches loose eggs and eggs held in incubators or storage |
| Food | Food name | e.g. `Raw Fish` |
| Plant | Prefab name | e.g. `Mealwood`; matches fully grown plants, crops, and mutant plants |
| Seed | Prefab name (`PlantableSeed` prefabs) | e.g. `Arbor Acorn`, `Thimble Reed Seed`; matches loose seeds and seeds held in storage. Edible seeds such as `Sleet Wheat Grain` stay under Food |
| Industrial | Prefab name | e.g. `Steel`, `Coal`, medicine and medical supplies. Uses the same tag list as the sandbox "Industrial Products" filter — machines are *not* included |
| Artifact | Prefab name | e.g. `Prehistoric Sphere` |

Only explored, revealed space is searched, so the overlay will not uncover anything hidden by fog of war.

Duplicants are matched by model, not by the name you gave them, so the two duplicant kinds are exact complements: `Duplicant` never lights up a bionic duplicant and `Bionic Duplicant` never lights up a normal one. Typing `duplicant` is a substring of both labels and so matches the whole colony.

## Installation

### Steam Workshop *(when published)*

Subscribe on the Steam Workshop.

### Manual

1. Install [PLib](https://steamcommunity.com/sharedfiles/filedetails/?id=2566346743) (required dependency)
2. Download the latest release DLL
3. Place it in `Documents\Klei\OxygenNotIncluded\mods\Dev\MaterialSearchOverlay\`
4. Restart the game and enable the mod in the mod menu

## Usage

1. Open the overlay menu and click **Material Search Overlay** (or press the assigned keybind — the button's tooltip shows which key that is)
2. Type any part of a name into the search field — with auto focus on, the field already has the keyboard when the overlay opens, so typing can start right away; while it has the keyboard, typing letters into it takes over from the camera keys, and closing the overlay hands the keyboard back
3. Matching results appear in the dropdown, each labelled with its kind — click one to pin it, or step through them with `Up`/`Down` and press `Enter`
4. The map is dimmed grey from the moment the overlay opens; every match then lights up in its own colour on top of that grey, so only the matches show colour
5. Hover a cell to see only the matches in it — everything else in that cell stays out of the tooltip
6. Click a cell to select the match directly; click again to step through the other matches in that cell

Closing the overlay clears the search field, so every time you open it the box is empty again.

## Mod options

The PLib **Mods** screen has a **Material Search Overlay** options page. Changes take effect straight away — no restart needed.

| Option | Default | What it does |
|---|---|---|
| Enable Debug Logging | Off | Writes verbose `[PLib/MaterialSearchOverlay]` diagnostics to `Player.log` |
| Show Type Badges | On | Labels every dropdown row with what kind of result it is |
| Maximum Search Results | 50 | How many matches a search keeps, in the dropdown and on the map (they are always the same set). The options dialog clamps the value to 5–100 |
| Focus Search Box On Open | On | Puts the cursor in the search box as soon as the overlay opens, from the menu button or the keybind alike. While the box has the keyboard, the camera keys type into it instead of moving the view |

## Building from source

```bash
dotnet build MaterialSearchOverlay
```

The mod DLL is auto-deployed to your ONI Dev mod folder. See the [root README](../README.md) for full build setup.

## Mod info

| Field | Value |
|---|---|
| **Static ID** | `materialsearchoverlay` |
| **API Version** | 2 |
| **Minimum build** | 469112 |
