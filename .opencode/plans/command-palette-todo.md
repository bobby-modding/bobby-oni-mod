# CommandPallete — Implementation Plan

**Legend:** `[MVP]` = core shipping, `[NICE]` = polish/enhancement, `[FUTURE]` = post-v1

---

## Phase 0: Project Scaffolding ✅

- ✅ `[MVP]` 0.1 Verify project builds: `dotnet build CommandPallete` — confirm the skeleton compiles as-is
- ✅ `[MVP]` 0.2 Add `PLib` NuGet package reference to `.csproj` (match `MaterialSearchOverlay` pattern: `4.22.0`, `PrivateAssets=all`, ILRepack)
- ✅ `[MVP]` 0.3 Add `ILRepack.targets` file (copy from `MaterialSearchOverlay/ILRepack.targets`, adjust assembly name)
- ✅ `[MVP]` 0.4 Create `CommandPallete_Strings.cs` — `LocString` constants for mod name, description, UI labels, action binding name
- ✅ `[MVP]` 0.5 Create `CommandPallete_Options.cs` — `[ModInfo]` + `[Option]` attributes (hotkey binding, max results, fuzzy match threshold)
- ✅ `[MVP]` 0.6 Wire up `PUtil.InitLibrary()`, `PPatchManager`, `PLocalization`, `POptions`, `PVersionCheck` in `CommandPallete_Patches.OnLoad()`
- ✅ `[MVP]` 0.7 Verify build with PLib: `dotnet build CommandPallete`

## Phase 1: Command Index ✅

- ✅ `[MVP]` 1.1 Create `CommandPallete_Index.cs` — define `CommandEntry` class:
      - `Id` (string), `DisplayName` (string, localized), `Category` (enum: Building/Tool/Overlay/Screen/Action)
      - `Execute` delegate (`System.Action` — game's `Action` enum conflicts)
      - `Keywords` (string[]) — alternative search terms
      - `SortPriority` (int) — for ranking
- ✅ `[MVP]` 1.2 Create `CommandPallete_Index.cs` — `CommandIndex` singleton with:
      - `List<CommandEntry> AllCommands`
      - `void RebuildIndex()` — called on `OnAllModsLoaded` + game load
      - `List<CommandEntry> FuzzySearch(string query)` — returns ranked matches
- ✅ `[MVP]` 1.3 **Buildings index:** iterate `Assets.BuildingDefs`, create `CommandEntry` per building that calls `BuildTool.Instance.Activate(def)`
      - Filter out hidden buildings (`!ShowInBuildMenu`), deprecated, debug-only
      - Use `def.Name` (localized) as display name
      - Add `BUILDINGS.PLANORDER` category names as keywords
- ✅ `[MVP]` 1.4 **Tools index:** hardcode entries for Dig, Mop, Sweep, Wrangle, Attack, Cancel, Deconstruct, Empty Pipe, Disinfect, Harvest, Capture, Prioritize, Disconnect
      - Each calls `PlayerController.Instance.ActivateTool(matchingTool)` found by type name at execution time
- ✅ `[MVP]` 1.5 **Overlays index:** entries for all 15 visible overlays + "Clear Overlay"
      - Each calls `OverlayScreen.Instance.ToggleOverlay(modeId)`
      - Localized names via `Strings.Get("STRINGS.UI.OVERLAYS.*.BUTTON")`
- ✅ `[MVP]` 1.6 **Screens index:** hardcode entries for Priorities, Schedule, Skills, Consumables, Starmap, Research, Database
      - Public toggle methods for Priorities/Skills/Research/Codex/Starmap
      - `Traverse` for Schedule/Consumables private fields → `OnButtonClick()`
- ✅ `[MVP]` 1.7 Implement basic fuzzy matching (client-side, on every keystroke):
      - Simple scorer: exact prefix match > substring match > character-skip match
      - Levenshtein distance for typos (e.g., "aqtunr" → "Thermo Aquatuner")
      - Returns top-N results (configurable, default 10)
- `[NICE]` 1.8 Add "Spawn [Element]" commands when Sandbox mode is active — iterate `ElementLoader.elements`
- `[NICE]` 1.9 Add debug commands (Instant Build, Toggle Debug, etc.) when Debug mode is enabled

## Phase 2: Command Palette UI ✅

- ✅ `[MVP]` 2.1 Create `CommandPallete_Screen.cs` — class extending `KScreen`:
      - `TMP_InputField` for search input (programmatic build, no prefab)
      - `KScrollRect` + vertical layout for results list (built at runtime)
      - Each result row: colored category badge + label + category text
      - `static Open()` method — creates GameObject, adds component, calls `Activate()`
      - Added `UnityEngine.InputLegacyModule` reference to `.csproj` for `Input.GetKeyDown`
- ✅ `[MVP]` 2.2 Wire text input `onValueChanged` → `CommandIndex.FuzzySearch()` → `RebuildResultItems()`
- ✅ `[MVP]` 2.3 **Keyboard navigation:**
      - `Up/Down` arrows in `Update()` (keyboard nav while input is focused)
      - `Enter` (`Action.DialogSubmit`) in `OnKeyDown` + `KeyCode.Return` fallback in `Update()`
      - `Escape` clears text if non-empty, closes palette if empty
      - Input auto-focused on open via `ActivateInputField()`
      - Mouse: click executes result, hover moves selection highlight
- ✅ `[MVP]` 2.4 Style UI to match ONI native aesthetic:
      - Dark panel (`rgb(38,38,38,245)`) on semi-transparent black overlay (`rgb(0,0,0,180)`)
      - Category badge colors: Building=blue, Tool=green, Overlay=gold, Screen=red, Action=purple
      - `TextMeshProUGUI` — 14pt main text, 11pt category/hint, white/gray palette
      - `KScrollRect` with themed dark scrollbar (`AutoHideAndExpandViewport`)
      - Modal behavior: `IsModal() = true`, `GetSortKey() = MODAL_SCREEN_SORT_KEY`
- `[NICE]` 2.5 **Recent / Most Used list:** When palette opens with empty search, show last 10 used commands (track in `POptions` settings JSON)
- `[NICE]` 2.6 Fade/scale open animation — match `KScreen` overlay style (like `OverlayMenu` or `ManagementMenu`)
- `[NICE]` 2.7 Category filtering — type `@build` or `@overlay` prefix to filter by category
- `[NICE]` 2.8 Show building category path (e.g., "Base → Ladders → Plastic Ladder") as subtitle in result rows

## Phase 3: Command Execution

- `[MVP]` 3.1 **Execute building:** `BuildTool.Instance.Activate(def, selectedElements, facade)` — use default elements (first available material)
      - Close existing PlanScreen recipe first (`PlanScreen.Instance.CloseRecipe()`)
      - Auto-switch to correct overlay (`def.ViewMode`)
- `[MVP]` 3.2 **Execute tool:** iterate `PlayerController.Instance.tools`, match by `tool.name` → `ActivateTool()`
      - Handle special cases: Cancel tool, Deconstruct, etc.
- `[MVP]` 3.3 **Execute overlay:** `OverlayScreen.Instance.ToggleOverlay(modeId)` — with null check on `OverlayScreen.Instance`
      - Close palette after execution
- `[MVP]` 3.4 **Execute screen:** call appropriate `ManagementMenu.Instance.Toggle*()` method or `GameScreenManager.Instance.StartScreen()`
      - Handle screens that are already open (close them instead? or bring to front)
- `[MVP]` 3.5 After command execution: close palette, return to game with appropriate tool/overlay active
- `[NICE]` 3.6 **Context-sensitive actions:** If a building/dupe is selected, show "Copy Settings", "Disable", "Move To", "Prioritize" etc.
      - Use `SelectTool.Instance.selected` to detect selection

## Phase 4: Keyboard Input & Hotkey

- `[MVP]` 4.1 Register custom keybinding via `PActionManager.CreateAction()` in `OnLoad()`
      - Default: `Ctrl+Shift+P` (or `Shift+Space` if conflicts)
      - Add binding string to `Strings.cs`
- `[MVP]` 4.2 Patch `GameScreenManager` or `PlayerController` to listen for the custom action keypress
      - Or: register a global `IInputHandler` via `KScreen` that sits above other screens
      - Simpler: patch `OnKeyDown` on the main `KScreen` layer to catch the action
- `[MVP]` 4.3 On hotkey press: toggle palette visibility (open if closed, close if open)
      - Close any open management screens before showing palette
      - Block game input while palette is active (modal-like behavior)
- `[NICE]` 4.4 Rebindable hotkey via standard ONI keybindings menu → PLib's `PActionManager` handles this automatically

## Phase 5: Caching, Performance & Localization

- `[MVP]` 5.1 Rebuild command index on `OnAllModsLoaded` + when loading a save (`Game.OnLoad`)
      - Use `IReadOnlyList<Mod>` from `OnAllModsLoaded` to discover mod-provided buildings
- `[MVP]` 5.2 Search works against the pre-built in-memory index — no allocations per keystroke beyond result list
- `[MVP]` 5.3 Localization: all command display names come from game's `Strings.Get()` — no hardcoded English strings
      - Building names: `def.Name` (already localized)
      - Tool names: `Strings.Get("STRINGS.UI.TOOLS." + toolName.ToUpper())`
      - Overlay names: use existing localized display names from `OverlayMenu.OverlayToggleInfo`
- `[MVP]` 5.4 Add `translations/` directory with `po-template.json` for community translations
      - Register via `PLocalization.Register()` with `POFilePaths`

## Phase 6: Mod API (Extensibility)

- `[FUTURE]` 6.1 Create `CommandPallete_API` static class:
      - `public static void RegisterCommand(string id, string displayName, Action callback, string[] keywords = null, CommandCategory category = CommandCategory.Action)`
      - `public static void UnregisterCommand(string id)`
- `[FUTURE]` 6.2 Document API in `mod.yaml` description and publish a simple example
- `[FUTURE]` 6.3 Handle duplicate IDs gracefully (last-register wins, with debug log warning)

## Phase 7: Polish & QA

- `[NICE]` 7.1 Mouse support: clicking a result executes it (in addition to keyboard nav)
- `[NICE]` 7.2 Highlight matching characters in result labels (like VS Code palette)
- `[NICE]` 7.3 Show tooltip on hover with building description (`def.Description`)
- `[NICE]` 7.4 Add option to toggle overlay switching on/off separately
- `[NICE]` 7.5 Add option for max visible results
- `[NICE]` 7.6 Edge case: palette + plan screen open at same time — close plan screen first
- `[NICE]` 7.7 Edge case: palette + management screen open — close management screen first
- `[NICE]` 7.8 Edge case: no buildings unlocked yet (early game) — show message instead of empty
- `[MVP]` 7.9 Final build check: `dotnet build CommandPallete` — zero errors
- `[MVP]` 7.10 Manual test in-game: open palette, search, execute every command type (build/tool/overlay/screen)

---

## File Structure (final)

```
CommandPallete/
├── CommandPallete.csproj
├── CommandPallete.sln
├── ILRepack.targets
├── mod.yaml
├── mod_info.yaml
├── CommandPallete_Preview.png
├── CommandPallete_Patches.cs       # UserMod2 entry, PLib init
├── CommandPallete_Strings.cs       # LocString constants
├── CommandPallete_Options.cs       # ModInfo + Option attributes
├── CommandPallete_Index.cs         # CommandEntry, CommandIndex, fuzzy search
├── CommandPallete_Screen.cs        # KScreen subclass, UI + keyboard nav
├── translations/
│   └── po-template.json
Properties/
└── AssemblyInfo.cs
```

## Dependency Graph

```
Phase 0 ──► Phase 1 ──► Phase 2 ──► Phase 3 ──► Phase 4 ──► Phase 5 ──► Phase 7
                 │            │            │                        │
                 └──── Phase 6 (independent, FUTURE) ──────────────┘
```

**MVP cut line:** 0.1–0.7 → 1.1–1.7 → 2.1–2.4 → 3.1–3.5 → 4.1–4.3 → 5.1–5.4 → 7.9–7.10
