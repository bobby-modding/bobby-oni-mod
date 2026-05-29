# Material Search Overlay

An Oxygen Not Included mod that lets you search for materials by name and highlights their locations on the map.

## Features

- **Search by name** — type a material name (e.g., `GOLD`, `WATER`, `INSULATION`) into the search box
- **Visual highlight** — matching tiles, buildings, and debris are colored in the element's natural color; non-matching cells are dimmed
- **Mass totals** — select an element from the dropdown to see its total mass broken down by natural tiles, debris, and buildings
- **Debug logging** — optional verbose logging to `Player.log` (toggle in mod options)

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
2. Type a material name into the search field
3. Matching elements appear in the dropdown — click one to see mass totals
4. The map highlights all matches; unmatched cells appear gray

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
