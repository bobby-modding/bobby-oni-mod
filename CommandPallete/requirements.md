Creating a "Command Palette" mod for *Oxygen Not Included* (ONI) is a fantastic idea. As colonies grow and mod lists expand, navigating ONI's deep build menus and overlays can become tedious. A tool similar to Visual Studio Code's `Ctrl+Shift+P` would be a massive quality-of-life improvement.

Here is a comprehensive list of suggested requirements and specifications if you are designing and developing this mod.

### 1. Functional Requirements (Core Features)

These define what the player can actually *do* with the command palette.

* **Universal Search & Build:** Typing the name of any building (e.g., "Thermo Aquatuner" or "Ladder") should immediately equip the build tool for that structure and bring up the material selection menu.
* **Quick Tools & Errands:** Include commands for standard bottom-right actions: *Dig, Mop, Sweep, Wrangle, Attack, Cancel, Deconstruct, and Empty Pipe*.
* **Overlay Switching:** Allow players to type the name of an overlay to switch to it instantly (e.g., *Power, Plumbing, Ventilation, Automation, Radiation*).
* **UI Panel Navigation:** Commands to instantly open management screens: *Priorities, Schedule, Skills, Consumables, Starmap, or Research*.
* **Sandbox/Debug Integration:** If the player has Debug or Sandbox mode enabled, include commands for spawning elements (e.g., "Spawn Water", "Spawn Duplicant"), triggering events, or toggling instant-build.
* **Context-Sensitive Actions:** If a duplicant or building is currently selected, show context-specific commands (e.g., if a generator is selected, "Disable Building" or "Copy Settings").

### 2. UI/UX Requirements

To ensure the mod feels good to use and blends in with the game.

* **Fuzzy Matching:** The search algorithm must forgive typos and partial matches. Typing "aqtunr" should still bring up the Thermo Aquatuner.
* **Recent/Most Used Commands:** When the palette is opened (before typing), it should display a list of the 5-10 most recently used or most frequently used commands.
* **Native Aesthetic:** The UI should use ONI's native UI prefabs, fonts, and colors to feel like an official feature.
* **Customizable Hotkey:** Default to `Ctrl+Shift+P` or `Shift+Space`, but allow the player to rebind it via the standard ONI keybindings menu.
* **Keyboard Navigation:** The player must be able to use the Up/Down arrow keys to scroll through search results and press `Enter` to execute, entirely mouse-free.

### 3. Technical & System Requirements

The underlying architecture you will need to implement the mod.

* **Harmony (Dependency):** Essential for injecting your code into ONI's vanilla assemblies. You'll need Harmony to intercept keyboard inputs without conflicting with standard game controls, and to patch into the `GameScreenManager` or `PlayerController` to spawn your UI.
* **PLib (Dependency):** Useful for creating and managing your mod's UI elements, especially if you want to reuse ONI's native prefabs and styles.
* **Caching / Indexing:** ONI has hundreds of buildings and materials. The mod must index all available commands, buildings, and overlays *once* when a save file is loaded. Searching through the list dynamically on every keystroke will cause stuttering.
* **Mod API (Extensibility):** Expose a public method (e.g., `CommandPaletteAPI.RegisterCommand(string name, Action callback)`) so other mod creators can easily inject their own mod-specific actions into your palette.
* **Localization Support:** Ensure the command index pulls from the game's localization strings so it automatically works for players using non-English language packs.