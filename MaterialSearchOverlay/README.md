# Material Search Overlay

An Oxygen Not Included mod that lets you search the colony by name and highlights the matching things on the map.

## Features

- **Search by name** — one search box covers materials, equipment, duplicants, bionic duplicants, boosters, critters, food, plants, industrial products, and artifacts
- **Own overlay icon** — the overlay bar button and the overlay legend use a custom magnifier icon instead of the vanilla materials overlay icon
- **Type badges** — every dropdown result is labelled with what kind of thing it is, so a name that exists in several categories can still be picked unambiguously
- **Visual highlight** — matching entities light up in place; for elements, matching tiles, natural backwalls, buildings, and debris are colored in the element's natural color and non-matching cells are dimmed
- **Mass totals** — selecting an element reports its total mass broken down by natural tiles, visible natural backwalls, debris, and buildings
- **Count summary** — for everything else the label reports how many of each kind were found in the colony
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
| Food | Food name | e.g. `Raw Fish` |
| Plant | Prefab name | e.g. `Mealwood`; matches fully grown plants, crops, seeds, and mutant plants |
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

1. Open the overlay menu and click **Material Search Overlay** (or press the assigned keybind)
2. Type any part of a name into the search field
3. Matching results appear in the dropdown, each labelled with its kind — click one to pin it
4. The map highlights all matches; unmatched cells appear gray when an element matched

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
