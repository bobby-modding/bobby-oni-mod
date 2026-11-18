# Material Search Overlay — In-Game Test Checklist

**Mod version:** 2.2.0 · **Target build:** U60-763023 · **DLC:** all installed
**Goal:** prove every searchable kind works in game, with one sample name per category.

**How to use this file:**

- Work top to bottom. Every item is one action and one expected result.
- `[x]` = pass · `[!]` = fail (write the item ID) · `[-]` = not applicable / not testable.
- If anything fails, grab the matching `[PLib/MaterialSearchOverlay]` lines from `Player.log`.
- Report failures with the template at the bottom.

---

## 0. Setup (once per test session)

- [ ] **SET-1** Build and deploy the mod:
  ```bash
  dotnet build MaterialSearchOverlay
  ```
  Expected: build succeeds and the DLL is copied into the Dev mods folder.
- [ ] **SET-2** Start ONI → **Mods** → enable **Material Search Overlay** → restart when prompted.
- [ ] **SET-3** Enable debug logging: **Mods → Material Search Overlay → Options → Enable Debug Logging = ON** → restart.
- [ ] **SET-4** Note the log file location:
  - Linux: `~/.config/unity3d/Klei/Oxygen Not Included/Player.log`
  - Windows: `%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included\Player.log`
- [ ] **SET-5** Start a **new game** with:
  - all DLCs enabled,
  - **Sandbox** enabled (the sandbox toggle can be unlocked mid-game if you forgot),
  - any asteroid (Terra is fine).
- [ ] **SET-6** Unlock sandbox tools and confirm the **Spawn** tool works: pick `Copper Ore`, click the map once, see a 100 kg chunk appear.
- [ ] **SET-7** Open `Player.log` and confirm these lines (numbers will differ):
  - `Loaded sprite: BobbyModding.MaterialSearchOverlay.overlay_material_search.png (64x64, ...)`
  - `SearchIndex: built ... entries: ...`
  - no `Exception`, `NullReferenceException`, or `MissingMethodException` lines.
  - **Note:** with debug logging on, the log also dumps every element name (`AfterDbInit: ...`). That is expected and is not an error.
- [ ] **SET-8** Spawn the sample items from **Appendix B** before starting the category tests.

> Categories that sandbox cannot spawn: **Artifact** (see ART). Everything else in Appendix B is spawnable with the Sandbox **Spawn** tool.

---

## 1. Smoke test (run on every build, ~5 minutes)

- [ ] **ST-1** The overlay bar (top-right, same row as vanilla overlays) shows **Material Search Overlay** with the custom **magnifier icon**, not the vanilla materials icon.
- [ ] **ST-2** Hovering the icon shows the tooltip: *"Search for materials, equipment, creatures and more by typing their name"* followed by the assigned key (same style as vanilla overlay tooltips; while nothing is bound it shows the game's unbound placeholder).
- [ ] **ST-3** Bind the keybind: **Options → Key Bindings → Open Material Search Overlay**; assign a key; pressing it opens the overlay. Hover the button again: the tooltip now ends with the key you assigned; rebind to a different key and the tooltip follows it (check after the buttons next refresh, e.g. after toggling the overlay).
- [ ] **ST-3b** Rebind to a key that stacks two modifiers: hold Shift and Ctrl while assigning, so the game writes the pair into `keybindings.json`. Expected: the tooltip spells all three names, e.g. `...typing their name [SHIFT + CTRL + F]`. Vanilla drops the modifier names in this case and shows `[+ F]`, so `[+ F]` is a failure. With debug logging on, `Player.log` also carries one `[PLib/MaterialSearchOverlay] GetActionString: ... named none of them, now [SHIFT + CTRL + F]` line.
- [ ] **ST-4** Click the overlay button. Expected: the button is highlighted, the map switches to the overlay, and the legend opens.
- [ ] **ST-5** The legend shows the magnifier icon, the title **MATERIAL SEARCH OVERLAY**, and a description listing the searchable kinds. No raw `STRINGS.` keys and no `{0}` placeholders are visible.
- [ ] **ST-6** The search box is visible in the legend panel with the placeholder **"Type a name to search..."**. It is visible only while this overlay is active.
- [ ] **ST-7** Type `hatch`. Expected: a dropdown appears with rows, each showing a colour square, a coloured kind badge, and a name. Rows whose name starts with "Hatch" are near the top.
- [ ] **ST-8** Hover a row. Expected: the row background brightens.
- [ ] **ST-9** Click the row whose name starts with `Hatch` and has the **Critter** badge. Expected:
  - the dropdown closes,
  - the search box now reads that row's name,
  - matching critters light up green,
  - a label appears: `Found: N critters` (N = number of hatches in the colony).
- [ ] **ST-10** Close the overlay. Expected: the map returns to normal, all highlights are gone, and the legend/search box disappear; any text you had typed is discarded.
- [ ] **ST-11** Re-open the overlay. Expected: the search box is **empty** — even if you had typed something before closing — no dropdown, no label, no leftover highlights.
- [ ] **ST-12** Switch to another overlay (e.g. Oxygen), then back. Expected: the search box and label are hidden in the other overlay and the search is empty when you return.

---

## 2. Search box mechanics

- [ ] **SB-1 Case-insensitive:** `hatch` and `HATCH` return the same rows and highlight the same things.
- [ ] **SB-2 Partial names:** `hat` finds the critter row starting with `Hatch` and `Hatch Egg`; `iron` finds `Iron Ore`.
- [ ] **SB-3 Whitespace:** `   hatch   ` behaves exactly like `hatch`.
- [ ] **SB-4 Empty box:** delete the text. Expected: dropdown closes, highlights clear, label disappears.
- [ ] **SB-5 No match:** type `zzzz`. Expected: no dropdown, no highlight, no label, no error; any previous highlight is cleared.
- [ ] **SB-6 Ranking:** type `hatch`. Expected: names that exactly match or start with the query come before names that merely contain it; each group is alphabetical.
- [ ] **SB-7 Result cap:** type a single letter, e.g. `e`. Expected: at most `Maximum Search Results` rows (50 by default), the game stays responsive, and no exception appears in the log. If the list runs off the legend and rows cannot be reached, note it as a layout issue.
- [ ] **SB-8 Multi-kind badges:** type `steel`. Expected: at least **two** rows named `Steel` — one **Material** badge, one **Industrial** badge — with different swatch colours.
- [ ] **SB-9 Typing vs selecting:** with `hatch` typed, everything matching (critters, eggs, etc.) is highlighted and **no label** is shown. Click one row: only that kind stays highlighted and the label appears.
- [ ] **SB-10 Keyboard selection:** type `copper ore`. Expected: a dim caption **"Up/Down arrows to choose, Enter to select"** sits above the rows and no row is highlighted yet. Press `Down` — the first row brightens. `Down` again moves one row lower, `Up` moves back, `Up` from the first row wraps to the last row. Press `Enter`: the dropdown closes, the box shows that row's name, the map shows that material, and the mass label appears — identical to clicking the row.
- [ ] **SB-11 Keyboard edge cases:** with `copper ore` typed and nothing highlighted, `Enter` selects nothing (the box simply loses focus). Highlight a row, then type another character: the dropdown rebuilds with no highlight and the new character is appended to the **end** of the query, not the front. Arrow keys pressed while the box is not focused do nothing, and pressing them with no results (`zzzz`) does nothing either.
- [ ] **SB-12 Holding an arrow:** with `e` typed (the full cap, 50 rows by default), press and **hold** `Down`. Expected: the highlight steps one row immediately, pauses briefly, then keeps stepping down without further presses until the key is released; `Up` does the same upwards. A quick tap still moves exactly one row. After releasing, type a character: it appears at the **end** of the query.

---

## 3. Category tests (one sample per searchable kind)

Each test uses the same pattern: **search → pick the row with the listed badge → check map → check label → check hover/click**.

### 3.1 MAT — Material (`Copper Ore`)

- [ ] **MAT-1 Setup:** have at least three loose `Copper Ore` chunks (Sandbox Spawn → `Copper Ore`, 100 kg each). Have at least one copper ore tile or building on screen if possible.
- [ ] **MAT-2** Search `Copper Ore`; select the row with the **Material** badge.
- [ ] **MAT-3 Expected map:** copper ore tiles, debris, and buildings are tinted in the element's copper colour; every non-matching cell is dimmed grey.
- [ ] **MAT-4 Expected label:** one line with four figures:
  `Natural Tile: <mass> | Debris: <mass> | Buildings: <mass> | Backwall: <mass>`
  With three spawned chunks the **Debris** figure is about **300 kg** (100 kg per chunk). Figures with no matching objects read `0 kg`.
- [ ] **MAT-5 Hover:** hovering an ore chunk lists the ore; hovering a non-matching tile does not list ore.
- [ ] **MAT-6 Click:** clicking an ore chunk selects it.
- [ ] **MAT-7 Backwall (conditional):** search `Sandstone`. If the map has visible natural sandstone backwall, columns of it are tinted and **Backwall** is greater than 0 kg. Backwall covered by a solid tile is not counted. If the map has no backwall, mark `[-]`.
- [ ] **MAT-8 Fail if:** the map is not dimmed, non-copper tiles are tinted, a mass figure is blank/negative/`NaN`, the label is missing, or clicking selects something unrelated.

### 3.2 EQP — Equipment (`Atmo Suit`)

- [ ] **EQP-1 Setup:** the quickest way is a colony where a duplicant already wears an `Atmo Suit` (a suit dock needs research, power, and an oxygen pipe, which is a lot of setup for a fresh sandbox). If you have no worn suit, spawn one `Atmo Suit` (Sandbox Spawn → **Equipment**) and leave it loose; build a powered, oxygen-fed **Atmo Suit Dock** only if you also want to test the worn case. If the worn case is not reachable, mark that part `[-]`.
- [ ] **EQP-2** Search `Atmo Suit`; select the row with the **Equipment** badge.
- [ ] **EQP-3 Expected map:**
  - the loose suit glows light blue,
  - a duplicant **wearing** a suit glows (the worn suit itself usually cannot be drawn, so the wearer represents it),
  - a dock holding a suit glows,
  - an empty dock does not glow.
- [ ] **EQP-4 Expected label:** `Found: N equipment` where N counts loose suits plus suit-wearing duplicants. Suits sitting in a dock or storage are represented by the container and are not counted again.
- [ ] **EQP-5 Fail if:** a worn suit lights nothing, the wearer is not lit, a dock is counted as an extra suit, or the count changes when a suit is moved into storage.

### 3.3 DUP — Duplicant (`Standard Duplicant`)

- [ ] **DUP-1 Setup:** the colony has at least one standard duplicant and at least one bionic duplicant (Sandbox Spawn → **Special** → `Duplicant` / `Bionic Duplicant`).
- [ ] **DUP-2** Search `Standard Duplicant`; select the row named **Duplicant**.
  - The row has **no badge** because the name and the badge would both read "Duplicant". This is expected.
- [ ] **DUP-3 Expected map:** only non-bionic duplicants glow gold/yellow. Bionics stay unlit.
- [ ] **DUP-4 Expected label:** `Found: N duplicants` where N = number of standard duplicants in the colony.
- [ ] **DUP-5 Fail if:** a bionic glows, a standard dupe is missed, or the count includes bionics.

### 3.4 BIO — Bionic Duplicant (`Bionic Duplicant`)

- [ ] **BIO-1** Search `Bionic Duplicant`; select the row named **Bionic Duplicant** with the **Bionic** badge.
- [ ] **BIO-2 Expected map:** only bionic duplicants glow purple. Standard duplicants stay unlit.
- [ ] **BIO-3 Expected label:** `Found: N bionic duplicants` where N = number of bionics in the colony.
- [ ] **BIO-4 Fail if:** standard dupes glow, or bionics that are standing on screen are not highlighted.

### 3.5 BST — Booster (`Advanced Medical Booster`)

- [ ] **BST-1 Setup:** spawn `Advanced Medical Booster` (Sandbox Spawn → **Bionic Upgrades**). Leave one loose and, if possible, install one on a bionic duplicant or place one in storage.
- [ ] **BST-2** Search `Advanced Medical Booster`; select the row with the **Booster** badge.
- [ ] **BST-3 Expected map:**
  - loose boosters glow orange,
  - a container holding a booster glows,
  - a bionic duplicant with the booster installed glows.
- [ ] **BST-4 Expected label:** `Found: N boosters` = loose boosters + boosters installed in duplicants. A single installed booster must count **once**, not once as an item and again as a wearer.
- [ ] **BST-5 Fail if:** the booster cannot be found, the installed booster doubles the count, or the duplicant wearing it is unlit.

### 3.6 CRT — Critter (`Hatch`)

- [ ] **CRT-1 Setup:** spawn a `Hatch` (Sandbox Spawn → **Creature** → search `Hatch`).
- [ ] **CRT-2** Search `Hatch`; select the row whose name starts with `Hatch` and has the **Critter** badge.
- [ ] **CRT-3 Expected map:** the hatch glows green. Walk it around or wait: the highlight follows it and does **not** stay behind.
- [ ] **CRT-4 Expected label:** `Found: N critters` where N = number of hatches in the colony.
- [ ] **CRT-5 Fail if:** the highlight is left behind when the critter moves, another species lights up, or the critter cannot be found.

### 3.7 EGG — Critter Egg (`Hatch Egg`)

- [ ] **EGG-1 Setup:** spawn `Hatch Egg` (Sandbox Spawn → **Critter Egg**). Leave one egg loose and put one in a storage bin (wait for a dupe to haul it).
- [ ] **EGG-2** Search `Hatch Egg`; select the row with the **Egg** badge.
- [ ] **EGG-3 Expected map:** the loose egg glows cream; the storage bin (or incubator) holding an egg also glows.
- [ ] **EGG-4 Expected label:** with one loose egg and one stored egg, the label reads `Found: 1 egg`. The stored egg is represented by the glowing container and is not added to the count. This is the expected stored-item behaviour.
- [ ] **EGG-5 Fail if:** the loose egg is missed, the container is not highlighted, or egg rows duplicate.

### 3.8 FOD — Food (`Meal Lice`)

- [ ] **FOD-1 Setup:** spawn `Meal Lice` (Sandbox Spawn → **Food**) loose, and put some in a ration box or refrigerator (wait for a dupe to haul it).
- [ ] **FOD-2** Search `Meal Lice`; select the row with the **Food** badge.
- [ ] **FOD-3 Expected map:** loose food glows yellow; the fridge/ration box holding it glows.
- [ ] **FOD-4 Expected label:** **mass and calories**, not an item count, e.g. `Found: 1.6 kg (3,200 kcal)`. Your numbers depend on how much loose food you spawned; food held inside a fridge/ration box lights the container and is not added to these figures.
- [ ] **FOD-5 Zero-calorie check:** search `Sleet Wheat Grain` (a cooking ingredient). Expected label: **mass only**, with no `(kcal)` part.
- [ ] **FOD-6 Fail if:** the label shows a plain item count, calories are missing for normal food, or stored food does not light its container.

### 3.9 PLT — Plant (`Mealwood`)

- [ ] **PLT-1 Setup:** spawn a `Mealwood` plant (Sandbox Spawn → **Plants**).
- [ ] **PLT-2** Search `Mealwood`; select the row with the **Plant** badge.
- [ ] **PLT-3 Expected map:** the plant glows green; a farmed plant, a wild plant, and a mutant plant all count as plants. Harvested Meal Lice are **not** plants.
- [ ] **PLT-4 Expected label:** `Found: N plants` where N = number of plant objects, not the number of crops on them.
- [ ] **PLT-5 Fail if:** the plant is not lit, the seed/food rows are the only ones found, or one plant is counted multiple times.

### 3.10 SED — Seed (`Arbor Acorn`)

- [ ] **SED-1 Setup:** spawn `Arbor Acorn` five times (Sandbox Spawn → **Seeds**) and keep them loose.
- [ ] **SED-2** Search `Arbor Acorn`; select the row with the **Seed** badge.
- [ ] **SED-3 Expected map:** the loose acorns glow yellow-green.
- [ ] **SED-4 Expected label:** `Found: 5 seeds`. Five spawn clicks = five units, even if the game merges them into fewer pile objects. The log prints `Count summary: Seed = 5 units across N objects` when merging happens.
- [ ] **SED-5 Stored seeds:** build a storage bin, wait for a dupe to haul two acorns into it, and re-select the row. Expected: the bin glows, the two stored acorns are no longer loose, and the label drops to `Found: 3 seeds`. Stored items light their container and are not counted as loose items.
- [ ] **SED-6 Fail if:** the count is the number of pile objects (e.g. `1 seed`) instead of units, or the stored acorns are not found.

### 3.11 IND — Industrial (`Steel`)

- [ ] **IND-1 Setup:** spawn loose `Steel` (Sandbox Spawn → **Industrial Products**).
- [ ] **IND-2** Search `Steel`. Expected: at least two rows — `Steel` **Material** (the element) and `Steel` **Industrial** (the item). Select the **Industrial** row.
- [ ] **IND-3 Expected map:** the loose steel item glows grey.
- [ ] **IND-4 Expected label:** `Found: N industrial products`.
- [ ] **IND-5 Machines are not industrial:** search `Metal Refinery` (or `Manual Generator`). Expected: **no Industrial row**; machines are buildings, not industrial products.
- [ ] **IND-6 Fail if:** `Steel` shows only one row, a machine appears with an Industrial badge, or the item is not highlighted.

### 3.12 ART — Artifact (`Artifact`)

> Neither the sandbox spawner nor the debug spawner lists artifacts. Run this block on a save that already has one (e.g. a Spaced Out colony that has recovered a space artifact), then return to the sandbox. If none is available, mark the whole block `[-]`.

- [ ] **ART-1 Setup:** have one artifact lying loose, one in storage/museum, and (if possible) one still in space.
- [ ] **ART-2** Search `Artifact`; select the general **Artifact** row (badge **Artifact**).
- [ ] **ART-3 Expected map:** every artifact glows pink, wherever it is — loose on the floor, displayed, or held in storage (the holder building glows).
- [ ] **ART-4 Expected label:** `Found: N artifacts`.
- [ ] **ART-5 Named artifact:** search a specific name, e.g. `Wrist Watch`; select its **Artifact** row. Only that artifact glows.
- [ ] **ART-6 Fail if:** the general row is missing, stored artifacts are not found, or the named row lights unrelated items.

---

## 4. Element-specific checks (uses the MAT setup)

- [ ] **EL-1 Mass breakdown:** all four parts of the material label are visible and separated by `|`; numbers use the game's mass units (kg/t) and never show blank, `NaN`, or a negative value.
- [ ] **EL-2 Backwall counting:** with a visible sandstone backwall, the **Backwall** figure is greater than 0. Cover part of it with a solid tile: the covered cells are not tinted and the figure drops after re-selecting the row.
- [ ] **EL-3 Fog of war:** while part of the map is still unexplored, search `Copper Ore` and note the mass. Hidden cells are not tinted and are not counted. Paint over a large unexplored area with the Sandbox **FOW** tool (it only reveals, it cannot re-hide), then re-select the row: the figure increases (or stays the same if nothing new was revealed).
- [ ] **EL-4 Dimming always on:** open the overlay with an empty search box — the whole map is already dimmed grey (sprites such as buildings and duplicants still show). Searching `Copper Ore` keeps the grey backdrop with the ore tinted; searching `Hatch` leaves the grey backdrop with only the hatches lit up.
- [ ] **EL-5 Natural colours:** `Copper Ore` is copper/orange, `Water` is blue, non-matching cells are grey.

---

## 5. Entity tracking and counts

- [ ] **EN-1 Moving targets:** search a critter or duplicant, then let it walk for 30+ seconds. The highlight follows it the whole time.
- [ ] **EN-2 Destroyed targets:** search a critter, then delete it with the Sandbox critter tool. Its highlight disappears.
- [ ] **EN-3 New targets:** after searching, spawn another Hatch. It does **not** glow until you select the row again (or retype/re-select). This is expected: the search is a snapshot.
- [ ] **EN-4 Stored items:** put a seed or an egg into storage. The container glows; the item itself is not visible to highlight.
- [ ] **EN-5 Worn equipment:** a worn suit lights the wearer, not a suit sprite.

---

## 6. Hover tooltip and click filtering

- [ ] **HC-1** Search `Hatch Egg`; hover the egg. Expected: the tooltip lists the egg, and the cell's oxygen/gas block is **not** shown (that element did not match).
- [ ] **HC-2** With `Hatch Egg` still selected, hover an empty tile or a non-matching object. Expected: no egg entry in the tooltip.
- [ ] **HC-3** Search `Copper Ore`; hover a copper ore tile. Expected: the cell block appears (the cell's element matched).
- [ ] **HC-4** Click the egg. Expected: the egg is selected directly, not the gas/other object in its cell.
- [ ] **HC-5** With `Copper Ore` selected, put an ore chunk on the same cell as a copper ore building (or two matching objects in one cell) and click repeatedly. Expected: the first click picks a match, the next click cycles to the other match.
- [ ] **HC-6** With a search active, click a non-matching object/cell. Expected: nothing is selected.
- [ ] **HC-7** Turn the overlay off. Expected: vanilla hover tooltips and clicking work exactly as before.
- [ ] **HC-8** Search `Sandstone`; hover a cell whose visible natural backwall is sandstone. Expected: the cell stays inspectable because the backwall matched.
- [ ] **HC-9 Fail if:** non-matching objects still appear in the tooltip, a non-match steals the click, or the overlay-off tooltip is changed.

---

## 6b. Position list and pinned pause *(new in 2.3.0)*

- [ ] **PL-1 Only after a selection:** open the overlay and type `Copper Ore`. While typing, no position list is shown. Click a dropdown row (or press Enter): the list appears below the mass/count label.
- [ ] **PL-2 Hidden again by typing:** with a list showing, type one more character in the search box, then click back into the box. Expected: the list disappears both times and only comes back when a row is pinned again.
- [ ] **PL-3 Coordinates:** spawn a single piece of `Copper Ore` debris near the bottom-left of the map. Select the `Copper Ore` row. Expected: an item row ending in its X/Y, both small non-negative numbers counting from the asteroid's bottom-left corner. Click it: the camera lands on that debris.
- [ ] **PL-4 Tile areas:** a copper ore vein is one `Tile areas (N)` row showing the centre coordinates and `(N cells)`. Press `+`: one row per cell appears directly under the area row, coordinates descend through the area, and the button becomes `-`. Pressing `-` collapses it again.
- [ ] **PL-5 Cell paging:** find or sandbox-paint a matching tile area with more than 100 cells, expand it. Expected: exactly 100 cell rows plus a `Page 1 of N` row with a `Next` button; `Next` moves through the pages and `Previous` goes back. The area's own row stays where it was.
- [ ] **PL-6 Backwall areas:** with a visible matching backwall (e.g. `Sandstone`), a `Backwall areas (N)` section appears, grouped and expandable exactly like tiles.
- [ ] **PL-7 Items:** a query matching loose items and buildings (`Hatch Egg`, `Steel`, an artifact) shows `Items (N)` with one row per object, sorted by name; loose debris reads `(debris)`, a finished building `(building)`, and a container holding a match `(contents)`. Duplicants and critters read `at X, Y` with no source word.
- [ ] **PL-8 Camera pan:** click a tile area row, a cell row, a backwall row, and an item row in turn. Each click moves the camera to that spot and does **not** change the zoom level.
- [ ] **PL-9 Forced pause:** select any row, then immediately check the speed controls — the game is paused. Press the pause hotkey (default space): nothing happens, the game stays paused and the pause button stays lit. Click the pause button, then the play button: also nothing.
- [ ] **PL-10 Still paused after close:** close the overlay while a result is pinned. Expected: the game is still paused. Press the pause key once: the game resumes at normal speed.
- [ ] **PL-11 Already paused:** pause the game yourself, then open the overlay and select a row. Close it. Expected: still paused, and one press of the pause key resumes (not two).
- [ ] **PL-12 Snapshot, not live:** with a result pinned, spawn a matching item with the Sandbox tools (spawning works while paused). Expected: the list does not change and no extra row appears. Select the row again: the new item shows up.
- [ ] **PL-13 Load/quit:** pin a result, then load a save or quit to the main menu. Expected: no crash, the new session starts at whatever speed the save has, and the pause key works normally.
- [ ] **PL-14 Logs:** with debug logging on, selecting a row logs `ForcedPause: begin, IsPaused=True`, `MatchLocations: ... tiles in ... areas, ... backwall cells, ... items in <ms>ms`, `MatchLocationUI: showing N locations`, and `PanToCell: cell=...` on each click. Pressing pause while pinned logs `TogglePause blocked: a search result is pinned`.
- [ ] **PL-15 Fail if:** the list shows cells outside the active asteroid or unexplored space, coordinates are cluster-global instead of world-local, the list refreshes on its own, the zoom changes on a row click, or the game can be unpaused while the overlay is open with a pinned result.

---

## 7. Overlay and save lifecycle

- [ ] **LC-1 Switching overlays:** switch to another overlay and back. Search text, dropdown, label, and highlights are cleared.
- [ ] **LC-2 Close/re-open:** closing and re-opening the overlay starts clean — the search text is cleared, and nothing from the old search is stuck on the map.
- [ ] **LC-3 Same-session save switch:** load save A, search a few samples, return to the main menu, load save B, then open the overlay and search again. Expected:
  - rows reflect save B,
  - no duplicated rows (e.g. the general `Artifact` row appears exactly once, not twice),
  - the log shows `SearchIndex: world entries added ...` again.
- [ ] **LC-4 DLC-off save:** create a base-game save (DLCs disabled) and open the overlay. Expected: no crash. DLC-only content must either be absent or show rows that highlight nothing; the `Artifact` general row may exist with nothing lit.
- [ ] **LC-5 Rapid toggling:** switch overlays quickly while typing. Expected: no exceptions and no stuck highlights.

---

## 8. Options

- [ ] **OP-1 Defaults:** on a fresh config, **Enable Debug Logging = OFF**, **Show Type Badges = ON**, **Maximum Search Results = 50**, **Focus Search Box On Open = ON**.
- [ ] **OP-2 Badges off:** turn **Show Type Badges** off and search `steel` right away — no restart needed. Rows show the swatch and name but no badge text; clicking still works.
- [ ] **OP-3 Logging off:** turn **Enable Debug Logging** off and search once. No new debug-level `[PLib/MaterialSearchOverlay]` lines appear (the one-time mod-init INFO lines still do).
- [ ] **OP-4 Persistence:** all four options keep their values after a restart.
- [ ] **OP-5** The mod options page opens without errors and shows the GitHub link.
- [ ] **OP-6 Result cap live:** set **Maximum Search Results** to 10 in the options dialog, then (without restarting) search `e`. Expected: the dropdown caps at **10 rows**, and the map highlights only the matching entries in that same capped list — not more than the dropdown shows. Raise it back to 100 and retype: up to 100 rows now.
- [ ] **OP-7 Auto focus on:** with **Focus Search Box On Open = ON** (the default), open the overlay from the menu button — the box already has the keyboard, so typing works without clicking it first. Then close it and reopen via the **keybind**: focused the same way.
- [ ] **OP-8 Auto focus off:** set **Focus Search Box On Open = OFF** and reopen the overlay. Expected: the box does not grab the keyboard on open — nothing types until you click into the box.

---

## 9. Localization

- [ ] **LOC-1** Switch the game language to German, Russian, and Simplified Chinese (restart/reload if the game requires it). Check:
  - the overlay button tooltip (its key at the end should still name the assigned binding),
  - the search placeholder,
  - the overlay legend title/description,
  - the kind badges,
  - the mass and count labels,
  - the no-match / count text,
  - the position list headers, area/cell/item rows and pager buttons.
- [ ] **LOC-2** No raw `STRINGS.` keys, no `{0}` / `{1}` placeholders, no missing-glyph boxes, and no text clipped out of the dropdown or label.
- [ ] **LOC-3** Counts still show the right number in each language (e.g. `Found: 3 critters` translated, with `3` present).

---

## 10. Logs and performance

- [ ] **LOG-1 Startup:** log contains the mod init, the 64×64 sprite load, the keybind registration, and the overlay/legend setup lines. No exceptions.
- [ ] **LOG-2 Index at db init:** `SearchIndex: built ... entries: Material=..., Equipment=0, ...`. Expected: Material, Critter, Duplicant, Bionic and the general Artifact row are already non-zero; the world-dependent kinds (Equipment, Industrial, Plant, Seed, Booster, Egg, Food) are `0` until the first overlay open.
- [ ] **LOG-3 First overlay open:** `SearchIndex: world entries added in <ms>ms, <N> entries total: ...` and `BuildPrefabs: scanned=...` plus `BuildBoosters: UpgradesData=..., added=..., DLC3 subscribed=True`.
- [ ] **LOG-4 Every search:** `RefreshHighlights: N elements, M entities (select=..., text='...')` — numbers change with the search.
- [ ] **LOG-5 Stacked counts:** when units differ from objects, `Count summary: <kind> = <units> units across <objects> objects` is logged.
- [ ] **LOG-6 Errors:** no `Exception`, `NullReferenceException`, `MissingMethodException`, or Harmony patch failure lines anywhere.
- [ ] **PERF-1** First overlay open after loading a save finishes quickly (well under a second on a normal PC). Opening it again after that is instant.
- [ ] **PERF-2** Typing quickly does not stutter, and a one-letter query is capped at `Maximum Search Results` rows (50 by default).

---

## 11. Known expected behaviour (do not report as a bug)

- Only the **active world** and **explored/revealed space** are searched. Hidden cells and other planetoids are ignored until revealed/active.
- A search is a **snapshot**: existing matches are followed as they move, but newly spawned objects need a re-search or re-selection to appear.
- Typing free text highlights **all** matching kinds; only clicking a dropdown row pins one entry and shows its count/mass label.
- Duplicants are matched by **model**, not by the player-given name. `Standard Duplicant` and `Bionic Duplicant` are exact complements.
- Worn equipment highlights the **wearer**; items inside storage highlight the **container**, because the stored item is not rendered.
- Materials show **mass only**; there is no material count line.
- Edible seeds (e.g. `Sleet Wheat Grain`) stay under **Food**; only non-food `PlantableSeed` prefabs are under **Seed**.
- The general `Artifact` row exists even in games without artifacts; it simply highlights nothing.
- Some DLC-gated rows (e.g. `Bionic Duplicant`) can appear in a save where that content is unavailable; they highlight nothing.
- The overlay legend keeps the vanilla colour rows; the search box, mass/count label, dropdown and position list are added above them.
- The dropdown highlight starts on no row after every keystroke; the first `Up`/`Down` steps onto the first row, and both ends wrap. Hovering a row moves the same highlight, and it stays on that row after the pointer leaves.
- With more rows than fit in the legend panel (at high caps — e.g. `e` returns the full `Maximum Search Results` of 50), the keyboard highlight can step below the visible area; the list does not auto-scroll to follow it yet.
- While the search box has the keyboard (auto focus, or after clicking into it), letters and camera keys type into it instead of moving the view or firing shortcuts; the overlay button still closes the overlay, and closing hands the keyboard back.
- The position list appears only for a **pinned dropdown result**, never for free text, and shows the **active world** only, revealed cells only.
- Coordinates in the position list are **world-local**: `0, 0` is the bottom-left cell of the current asteroid, not of the cluster grid. A rocket interior therefore reports its own small coordinates.
- The position list is a **snapshot** taken when the result is pinned; it is never refreshed while on screen, which is also why the colony is paused for as long as it is open.
- The colony **stays paused** after the overlay closes. If it was already paused before the result was pinned, that earlier pause is part of the same count and still needs its one press to release.
- Areas are **8-connected**: cells that only touch at a corner belong to the same area. An area row shows the centre of its bounding box, which need not be a matching cell itself.
- Artifacts cannot be spawned with the sandbox Spawn tool.
- Debug logging is off by default and is very verbose when on.

---

## 12. Sign-off

```text
Date:
Tester:
Mod version:
Game build:
Save used:
Passed sections:
Failed item IDs:
Notes:
```

---

## 13. Failure report template

```text
Item ID:        e.g. MAT-4
Mod version:    2.2.0
Game build:     U60-763023
DLC:            all / base game
Save:           fresh sandbox / other

Search query:
Row selected:   name + badge

What I did:
What I expected:
What actually happened:
Screenshot/GIF:

Player.log excerpt (search for [PLib/MaterialSearchOverlay]):
...
```

---

## Appendix A — One-page query sheet

Search each query and pick the row with the listed badge.

| ID | Query | Badge to pick | Expected label |
|---|---|---|---|
| MAT | `Copper Ore` | Material | `Natural Tile: … \| Debris: … \| Buildings: … \| Backwall: …` |
| MAT+ | `Sandstone` | Material | same four-part mass line, Backwall > 0 if backwall exists |
| EQP | `Atmo Suit` | Equipment | `Found: N equipment` |
| DUP | `Standard Duplicant` | (no badge — row is named `Duplicant`) | `Found: N duplicants` |
| BIO | `Bionic Duplicant` | Bionic | `Found: N bionic duplicants` |
| BST | `Advanced Medical Booster` | Booster | `Found: N boosters` |
| CRT | `Hatch` | Critter | `Found: N critters` |
| EGG | `Hatch Egg` | Egg | `Found: N eggs` |
| FOD | `Meal Lice` | Food | `Found: <mass> (<kcal>)` |
| FOD+ | `Sleet Wheat Grain` | Food | `Found: <mass>` (no kcal) |
| PLT | `Mealwood` | Plant | `Found: N plants` |
| SED | `Arbor Acorn` | Seed | `Found: N seeds` |
| IND | `Steel` | Industrial (second `Steel` row) | `Found: N industrial products` |
| ART | `Artifact` | Artifact | `Found: N artifacts` |
| — | `zzzz` | none | no dropdown, no highlight |
| — | `e` | any | at most the result cap (50 by default) rows, responsive |

---

## Appendix B — Sandbox spawn shopping list

Sandbox **Spawn** tool: choose the filter, type the name in the search box, click the map to place.

| Category | Filter | Spawn |
|---|---|---|
| Material | Ore Chunks | `Copper Ore` (3 clicks = ~300 kg debris) |
| Equipment | Equipment | `Atmo Suit`; for the worn case use a colony that already has a suit-wearing dupe |
| Duplicant | Special | `Duplicant` |
| Bionic | Special | `Bionic Duplicant` |
| Booster | Bionic Upgrades | `Advanced Medical Booster` |
| Critter | Creature | `Hatch` |
| Egg | Critter Egg | `Hatch Egg` |
| Food | Food | `Meal Lice` |
| Plant | Plants | `Mealwood` |
| Seed | Seeds | `Arbor Acorn` (5 clicks) |
| Industrial | Industrial Products | `Steel` |
| Artifact | — | **not spawnable** — use a save that already has an artifact |

Also useful:

- **Sandbox FOW tool** — reveal map areas for the fog-of-war test (it cannot re-hide them).
- **Sandbox Critter tool** — delete a critter for the destroyed-target test.
- **Sandbox Brush / element tools** — add or remove natural tiles and backwall.

---

## Appendix C — Label formats

| Label | Format | Notes |
|---|---|---|
| Material mass | `Natural Tile: {0} \| Debris: {1} \| Buildings: {2} \| Backwall: {3}` | Only after selecting a Material row. All figures in kg/t. |
| Any count | `Found: {0}` | Prefix for all non-material counts. |
| Equipment | `{0} equipment` | Loose equipment + worn equipment (wearer counted). |
| Duplicants | `{0} duplicants` | Standard model only. |
| Bionic | `{0} bionic duplicants` | Bionic model only. |
| Boosters | `{0} boosters` | Loose + installed; installed counted once via the wearer. |
| Critters | `{0} critters` | Live creatures only. |
| Eggs | `{0} eggs` | Loose eggs; stored eggs light their container. |
| Food | `{0} ({1})` | Mass and calories; `{1}` omitted when no calorie data exists. |
| Plants | `{0} plants` | Plant objects, not crops. |
| Seeds | `{0} seeds` | Units, not piles. |
| Industrial | `{0} industrial products` | Sandbox-equivalent industrial tags; machines excluded. |
| Artifacts | `{0} artifacts` | Includes artifacts held in containers. |

---

## Appendix D — What each kind actually matches

This is the complete scope of the search, even though the checklist tests one sample per kind.

| Kind | Indexed from | Included |
|---|---|---|
| Material | Element loader | Every element (solid, liquid, gas) by display name |
| Equipment | Prefab registry | Any prefab with an `Equippable` component |
| Duplicant | Duplicant models | Standard model only |
| Bionic | Duplicant models | Bionic model only |
| Booster | Bionic upgrade config | Every booster prefab registered by the Bionic Booster Pack |
| Critter | Creature species tags | Every critter species |
| Egg | Prefabs tagged `Egg` | Critter eggs (not Egg Shell or Raw Egg) |
| Food | Edibles manager | Every food and cooking ingredient, including zero-calorie ones |
| Plant | Prefabs with `Harvestable`/`WiltCondition` | Wild, farmed, and mutant plants; codex-hidden prefabs excluded |
| Seed | Prefabs with `PlantableSeed` | Non-food seeds; edible seeds stay under Food |
| Industrial | Industrial tags | Industrial products/ingredients, tech components, medicine, medical supplies, charged batteries, molt shells, Table Salt |
| Artifact | `Artifact`/`Keepsake` prefabs + general row | Loose, stored, displayed, and space artifacts; keepsakes included |
