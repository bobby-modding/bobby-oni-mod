using HarmonyLib;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using STRINGS;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CommandPallete
{
    public enum CommandCategory
    {
        Building,
        Tool,
        Overlay,
        Screen,
        Action
    }

    public class CommandEntry
    {
        public string Id { get; }
        public string DisplayName { get; }
        public CommandCategory Category { get; }
        public System.Action Execute { get; }
        public string[] Keywords { get; }
        public int SortPriority { get; set; }

        public CommandEntry(string id, string displayName, CommandCategory category, System.Action execute, string[] keywords = null, int sortPriority = 0)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
            Execute = execute;
            Keywords = keywords ?? Array.Empty<string>();
            SortPriority = sortPriority;
        }
    }

    public class CommandIndex
    {
        public static readonly CommandIndex Instance = new CommandIndex();
        public List<CommandEntry> AllCommands { get; private set; } = new List<CommandEntry>();

        private CommandIndex() { }

        public void RebuildIndex()
        {
            var commands = new List<CommandEntry>();

            try { AddBuildings(commands); } catch (Exception e) { Log.Debug($"Failed to index buildings: {e.Message}"); }
            try { AddTools(commands); } catch (Exception e) { Log.Debug($"Failed to index tools: {e.Message}"); }
            try { AddOverlays(commands); } catch (Exception e) { Log.Debug($"Failed to index overlays: {e.Message}"); }
            try { AddScreens(commands); } catch (Exception e) { Log.Debug($"Failed to index screens: {e.Message}"); }

            AllCommands = commands;
            Log.Debug($"CommandIndex rebuilt with {AllCommands.Count} commands");
        }

        #region Building Index

        private static void AddBuildings(List<CommandEntry> commands)
        {
            if (Assets.BuildingDefs == null)
                return;

            var planCategoryLookup = new Dictionary<string, List<string>>();
            foreach (var planInfo in TUNING.BUILDINGS.PLANORDER)
            {
                string categoryName = planInfo.category.ToString();
                foreach (var entry in planInfo.buildingAndSubcategoryData)
                {
                    string prefabId = entry.Key;
                    if (!planCategoryLookup.ContainsKey(prefabId))
                        planCategoryLookup[prefabId] = new List<string>();
                    planCategoryLookup[prefabId].Add(categoryName);
                }
            }

            foreach (var def in Assets.BuildingDefs)
            {
                if (def == null) continue;
                if (!def.ShowInBuildMenu || def.Deprecated || def.DebugOnly) continue;
                if (!def.IsAvailable()) continue;

                string prefabId = def.PrefabID;
                string id = "building_" + prefabId;
                string displayName = def.Name;

                var keywords = new List<string>();
                if (planCategoryLookup.TryGetValue(prefabId, out var categories))
                    keywords.AddRange(categories);

                commands.Add(new CommandEntry(id, displayName, CommandCategory.Building, () =>
                {
                    ExecuteBuilding(def);
                }, keywords: keywords.Count > 0 ? keywords.ToArray() : null, sortPriority: 1));
            }
        }

        private static void ExecuteBuilding(BuildingDef def)
        {
            if (PlanScreen.Instance != null)
                PlanScreen.Instance.CloseRecipe();

            if (def.ViewMode.IsValid && OverlayScreen.Instance != null)
                OverlayScreen.Instance.ToggleOverlay(def.ViewMode);

            BuildTool.Instance.Activate(def, def.DefaultElements());
        }

        #endregion

        #region Tool Index

        private static void AddTools(List<CommandEntry> commands)
        {
            var toolEntries = new(string id, string displayName, string toolTypeName)[]
            {
                ("tool_dig", "Dig", "DigTool"),
                ("tool_mop", "Mop", "MopTool"),
                ("tool_sweep", "Sweep", "ClearTool"),
                ("tool_wrangle", "Wrangle", "CaptureTool"),
                ("tool_attack", "Attack", "AttackTool"),
                ("tool_cancel", "Cancel", "CancelTool"),
                ("tool_deconstruct", "Deconstruct", "DeconstructTool"),
                ("tool_emptypipe", "Empty Pipe", "EmptyPipeTool"),
                ("tool_disinfect", "Disinfect", "DisinfectTool"),
                ("tool_harvest", "Harvest", "HarvestTool"),
                ("tool_capture", "Capture", "CaptureTool"),
                ("tool_prioritize", "Prioritize", "PrioritizeTool"),
                ("tool_disconnect", "Disconnect", "DisconnectTool"),
            };

            foreach (var entry in toolEntries)
            {
                string id = entry.id;
                string displayName = entry.displayName;
                string toolTypeName = entry.toolTypeName;

                commands.Add(new CommandEntry(id, displayName, CommandCategory.Tool, () =>
                {
                    var pc = PlayerController.Instance;
                    if (pc?.tools == null) return;
                    foreach (var tool in pc.tools)
                    {
                        if (tool != null && tool.GetType().Name == toolTypeName)
                        {
                            pc.ActivateTool(tool);
                            return;
                        }
                    }
                }));
            }
        }

        #endregion

        #region Overlay Index

        private static readonly(HashedString modeId, string stringKey)[] OverlayEntries =
        {
            (OverlayModes.Oxygen.ID, "STRINGS.UI.OVERLAYS.OXYGEN.BUTTON"),
            (OverlayModes.Power.ID, "STRINGS.UI.OVERLAYS.ELECTRICAL.BUTTON"),
            (OverlayModes.Temperature.ID, "STRINGS.UI.OVERLAYS.TEMPERATURE.BUTTON"),
            (OverlayModes.TileMode.ID, "STRINGS.UI.OVERLAYS.TILEMODE.BUTTON"),
            (OverlayModes.Light.ID, "STRINGS.UI.OVERLAYS.LIGHTING.BUTTON"),
            (OverlayModes.LiquidConduits.ID, "STRINGS.UI.OVERLAYS.LIQUIDPLUMBING.BUTTON"),
            (OverlayModes.GasConduits.ID, "STRINGS.UI.OVERLAYS.GASPLUMBING.BUTTON"),
            (OverlayModes.Decor.ID, "STRINGS.UI.OVERLAYS.DECOR.BUTTON"),
            (OverlayModes.Disease.ID, "STRINGS.UI.OVERLAYS.DISEASE.BUTTON"),
            (OverlayModes.Crop.ID, "STRINGS.UI.OVERLAYS.CROPS.BUTTON"),
            (OverlayModes.Rooms.ID, "STRINGS.UI.OVERLAYS.ROOMS.BUTTON"),
            (OverlayModes.Suit.ID, "STRINGS.UI.OVERLAYS.SUIT.BUTTON"),
            (OverlayModes.Logic.ID, "STRINGS.UI.OVERLAYS.LOGIC.BUTTON"),
            (OverlayModes.SolidConveyor.ID, "STRINGS.UI.OVERLAYS.CONVEYOR.BUTTON"),
            (OverlayModes.Radiation.ID, "STRINGS.UI.OVERLAYS.RADIATION.BUTTON"),
        };

        private static void AddOverlays(List<CommandEntry> commands)
        {
            if (OverlayScreen.Instance == null)
                return;

            commands.Add(new CommandEntry("overlay_none", "Clear Overlay", CommandCategory.Overlay,
                () => OverlayScreen.Instance.ToggleOverlay(OverlayModes.None.ID),
                new[] { "none", "off", "hide" }));

            foreach (var entry in OverlayEntries)
            {
                string displayName = Strings.Get(entry.stringKey);
                string id = "overlay_" + entry.modeId.ToString();
                var capturedModeId = entry.modeId;

                commands.Add(new CommandEntry(id, displayName, CommandCategory.Overlay,
                    () => OverlayScreen.Instance.ToggleOverlay(capturedModeId)));
            }
        }

        #endregion

        #region Screen Index

        private static void AddScreens(List<CommandEntry> commands)
        {
            var mm = ManagementMenu.Instance;
            if (mm == null) return;

            commands.Add(new CommandEntry("screen_priorities",
                Strings.Get("STRINGS.UI.JOBS"), CommandCategory.Screen,
                () => mm.TogglePriorities(),
                new[] { "jobs", "priority" }));

            commands.Add(new CommandEntry("screen_schedule",
                Strings.Get("STRINGS.UI.SCHEDULE"), CommandCategory.Screen,
                () => ToggleManagementScreen(mm, "scheduleInfo"),
                new[] { "schedules" }));

            commands.Add(new CommandEntry("screen_skills",
                Strings.Get("STRINGS.UI.SKILLS"), CommandCategory.Screen,
                () => mm.ToggleSkills(),
                new[] { "skill", "duplicant skills" }));

            commands.Add(new CommandEntry("screen_consumables",
                Strings.Get("STRINGS.UI.CONSUMABLES"), CommandCategory.Screen,
                () => ToggleManagementScreen(mm, "consumablesInfo")));

            commands.Add(new CommandEntry("screen_starmap",
                Strings.Get("STRINGS.UI.STARMAP.MANAGEMENT_BUTTON"), CommandCategory.Screen,
                () => mm.ToggleStarmap(),
                new[] { "map", "space", "cluster" }));

            commands.Add(new CommandEntry("screen_research",
                Strings.Get("STRINGS.UI.RESEARCH"), CommandCategory.Screen,
                () => mm.ToggleResearch(),
                new[] { "tech", "science" }));

            commands.Add(new CommandEntry("screen_database",
                Strings.Get("STRINGS.UI.CODEX.MANAGEMENT_BUTTON"), CommandCategory.Screen,
                () => mm.ToggleCodex(),
                new[] { "codex" }));
        }

        private static void ToggleManagementScreen(ManagementMenu mm, string toggleInfoField)
        {
            var trav = Traverse.Create(mm);
            var toggleInfo = trav.Field(toggleInfoField).GetValue<ManagementMenu.ManagementMenuToggleInfo>();
            if (toggleInfo != null)
                mm.OnButtonClick(toggleInfo);
        }

        #endregion

        #region Fuzzy Search

        public List<CommandEntry> FuzzySearch(string query, int maxResults = -1)
        {
            if (string.IsNullOrWhiteSpace(query) || AllCommands.Count == 0)
                return new List<CommandEntry>();

            if (maxResults <= 0)
            {
                var options = POptions.ReadSettings<CommandPalleteOptions>();
                maxResults = options?.MaxResults ?? 10;
            }

            string lowerQuery = query.ToLowerInvariant();
            var scored = new List<(CommandEntry entry, int score)>();

            foreach (var entry in AllCommands)
            {
                int score = ComputeScore(entry, lowerQuery);
                if (score > 0)
                    scored.Add((entry, score));
            }

            return scored
                .OrderByDescending(s => s.score)
                .ThenBy(s => s.entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .Select(s => s.entry)
                .ToList();
        }

        private static int ComputeScore(CommandEntry entry, string lowerQuery)
        {
            string lowerName = entry.DisplayName.ToLowerInvariant();
            int bestScore = 0;

            if (lowerName == lowerQuery)
                return 100000 + entry.SortPriority;

            if (lowerName.StartsWith(lowerQuery))
                return 90000 + entry.SortPriority + (lowerQuery.Length * 100);

            string[] nameWords = lowerName.Split(' ');
            bool anyWordStartsWith = nameWords.Any(w => w.StartsWith(lowerQuery));
            if (anyWordStartsWith)
                bestScore = Math.Max(bestScore, 80000 + entry.SortPriority);

            foreach (var kw in entry.Keywords)
            {
                string lowerKw = kw.ToLowerInvariant();
                if (lowerKw == lowerQuery)
                    return 85000 + entry.SortPriority;
                if (lowerKw.StartsWith(lowerQuery))
                    bestScore = Math.Max(bestScore, 75000 + entry.SortPriority);
            }

            if (lowerName.Contains(lowerQuery))
                bestScore = Math.Max(bestScore, 50000 + entry.SortPriority + (lowerQuery.Length * 50));

            foreach (var kw in entry.Keywords)
            {
                if (kw.ToLowerInvariant().Contains(lowerQuery))
                    bestScore = Math.Max(bestScore, 40000 + entry.SortPriority);
            }

            if (IsCharacterSkipMatch(lowerQuery, lowerName))
                bestScore = Math.Max(bestScore, 30000 + entry.SortPriority + (lowerQuery.Length * 20));

            int maxDist = Math.Max(1, lowerQuery.Length / 3);
            if (maxDist > 0 && lowerQuery.Length >= 3)
            {
                int dist = LevenshteinDistance(lowerQuery, lowerName);
                if (dist <= maxDist)
                {
                    int levScore = Math.Max(0, 20000 - dist * 1000);
                    bestScore = Math.Max(bestScore, levScore);
                }
            }

            return bestScore;
        }

        private static bool IsCharacterSkipMatch(string query, string target)
        {
            int qi = 0;
            for (int ti = 0; ti < target.Length && qi < query.Length; ti++)
            {
                if (query[qi] == target[ti])
                    qi++;
            }
            return qi == query.Length;
        }

        private static int LevenshteinDistance(string a, string b)
        {
            int m = a.Length, n = b.Length;
            var d = new int[m + 1, n + 1];

            for (int i = 0; i <= m; i++) d[i, 0] = i;
            for (int j = 0; j <= n; j++) d[0, j] = j;

            for (int j = 1; j <= n; j++)
            {
                for (int i = 1; i <= m; i++)
                {
                    int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(
                        d[i - 1, j] + 1,
                        d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[m, n];
        }

        #endregion
    }
}
