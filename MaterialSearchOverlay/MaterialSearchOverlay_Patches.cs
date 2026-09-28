using HarmonyLib;
using PeterHan.PLib.Actions;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Detours;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

using StatusItemOverlays = StatusItem.StatusItemOverlays;

// The strings class nests one level per lookup group, so these aliases keep the
// per kind lookups in the dropdown and label code readable.
using MSearch = BobbyModding.MaterialSearchOverlay.MaterialSearchOverlayStrings.UI.OVERLAYS.MATERIALSEARCH;
using MSearchTypes = BobbyModding.MaterialSearchOverlay.MaterialSearchOverlayStrings.UI.OVERLAYS.MATERIALSEARCH.TYPES;

namespace BobbyModding.MaterialSearchOverlay {
    public sealed class Patches : KMod.UserMod2 {
        private const BindingFlags INSTANCE_ALL = PPatchTools.BASE_FLAGS | BindingFlags.
            Instance;

        private delegate void RegisterMode(OverlayScreen screen, OverlayModes.Mode mode);

        private static PAction OpenOverlay;

        internal static GameObject SearchField { get; private set; }

        internal static GameObject DropdownContainer { get; private set; }

        internal static GameObject MassLabel { get; private set; }

        private static bool ShowTypeBadges = true;

        private static readonly Type OVERLAY_TYPE = typeof(OverlayMenu).GetNestedType(
            "OverlayToggleInfo", INSTANCE_ALL);

        private static readonly RegisterMode REGISTER_MODE = typeof(OverlayScreen).
            Detour<RegisterMode>();

        /// <summary>
        /// Resource name of the mod's own overlay icon, matching the LogicalName the csproj
        /// embeds it under. Kept in step with that attribute by hand, as it is the one thing
        /// that cannot be checked by the compiler.
        /// </summary>
        private const string ICON_RESOURCE =
            "BobbyModding.MaterialSearchOverlay.overlay_material_search.png";

        /// <summary>
        /// Vanilla icon to fall back on, so a missing or unreadable image leaves a working
        /// button instead of a blank one.
        /// </summary>
        private const string FALLBACK_ICON = "overlay_materials";

        [PLibMethod(RunAt.BeforeDbInit)]
        internal static void RegisterOverlayIcon() {
            var sprite = LoadIcon();
            if (sprite == null) {
                PUtil.LogWarning("Unable to load {0} - falling back to {1}".F(ICON_RESOURCE,
                    FALLBACK_ICON));
                sprite = Assets.GetSprite(FALLBACK_ICON);
            }
            if (sprite == null) {
                PUtil.LogWarning("Unable to register the overlay icon");
                return;
            }
            // PUIUtils names a loaded sprite after its resource file. The name is what both
            // the overlay bar toggle and the overlay legend look it up by, so it has to be
            // the one they were built to ask for.
            sprite.name = MaterialSearchOverlayStrings.OVERLAY_ICON;
            // Indexer, not Add: a second db init in one process would throw on the duplicate.
            Assets.Sprites[MaterialSearchOverlayStrings.OVERLAY_ICON] = sprite;
            Log.Debug("RegisterOverlayIcon: '{0}' {1}x{2} from {3}".F(sprite.name,
                sprite.rect.width, sprite.rect.height, ICON_RESOURCE));
        }

        /// <summary>
        /// Reads the embedded icon, or null if it is missing or unreadable.
        /// </summary>
        /// <remarks>
        /// PUIUtils throws on both of those rather than returning null, and this runs from a
        /// PLib lifecycle callback: an exception escaping here would unwind out of
        /// <c>Db.Initialize</c> and take the whole database init with it. A missing icon is
        /// not worth that, so it is caught and reported as a warning.
        /// </remarks>
        private static Sprite LoadIcon() {
            try {
                var sprite = PUIUtils.LoadSprite(ICON_RESOURCE);
                // LoadImage reports unreadable data by leaving the texture at its initial
                // 2x2 size rather than by failing, which would otherwise register a
                // two pixel icon as if it had loaded.
                if (sprite != null && sprite.rect.width < 4) {
                    PUtil.LogWarning("{0} is not a readable image".F(ICON_RESOURCE));
                    return null;
                }
                return sprite;
            } catch (Exception e) {
                PUtil.LogExcWarn(e);
                return null;
            }
        }

        [PLibMethod(RunAt.AfterDbInit)]
        internal static void AfterDbInit() {
            var elements = ElementLoader.elements;
            var names = new Dictionary<SimHashes, string>(elements.Count);
            var displayNames = new Dictionary<SimHashes, string>(elements.Count);
            var colors = new Dictionary<SimHashes, Color>(elements.Count);
            foreach (var element in elements) {
                if (element?.tag == null || !element.tag.IsValid ||
                        names.ContainsKey(element.id))
                    continue;
                string rawName = element.name;
                string cleanName = SearchEntry.Clean(rawName);
                Log.Debug("AfterDbInit: {0} '{1}' → '{2}'".F(element.id, rawName, cleanName));
                names[element.id] = cleanName.ToUpperInvariant();
                displayNames[element.id] = cleanName;
                colors[element.id] = element.substance?.uiColour ??
                    Color.gray;
            }
            MaterialSearchOverlay.ElementNames = names;
            MaterialSearchOverlay.ElementDisplayNames = displayNames;
            MaterialSearchOverlay.ElementColors = colors;
            Log.Debug("AfterDbInit: loaded {0} elements, e.g. 'Sand'→'{1}'".F(names.Count,
                names.TryGetValue(SimHashes.Sand, out var s) ? s : "MISSING"));
            // Everything else is indexed in one pass so the dropdown can match equipment,
            // duplicants, boosters, critters, food, plants, industrial products and
            // artifacts from the same text box as the elements.
            SearchIndex.Build();
        }

        /// <summary>
        /// Fires on every world load, so the save-dependent half of the search index is
        /// re-armed each time. Without this, loading a second save in the same session would
        /// keep showing the first save's DLC-filtered equipment, boosters and foods, because
        /// AfterDbInit only ever runs once per process.
        /// </summary>
        [PLibMethod(RunAt.OnStartGame)]
        internal static void OnStartGame() {
            SearchIndex.InvalidateWorld();
        }

        private static KIconToggleMenu.ToggleInfo CreateOverlayInfo(string text,
                string iconName, HashedString simView, Action openKey, string tooltip) {
            const int KNOWN_PARAMS = 7;
            KIconToggleMenu.ToggleInfo info = null;
            ConstructorInfo[] cs;
            if (OVERLAY_TYPE == null || (cs = OVERLAY_TYPE.GetConstructors(INSTANCE_ALL)).
                    Length != 1)
                PUtil.LogWarning("Unable to add MaterialSearchOverlay - missing constructor");
            else {
                var cons = cs[0];
                var toggleParams = cons.GetParameters();
                int paramCount = toggleParams.Length;
                if (paramCount < KNOWN_PARAMS)
                    PUtil.LogWarning("Unable to add MaterialSearchOverlay - parameters missing");
                else {
                    object[] args = new object[paramCount];
                    args[0] = text;
                    args[1] = iconName;
                    args[2] = simView;
                    args[3] = "";
                    args[4] = openKey;
                    args[5] = tooltip;
                    args[6] = text;
                    for (int i = KNOWN_PARAMS; i < paramCount; i++) {
                        var op = toggleParams[i];
                        if (op.IsOptional)
                            args[i] = op.DefaultValue;
                        else {
                            PUtil.LogWarning("Unable to add MaterialSearchOverlay - new parameters");
                            args[i] = null;
                        }
                    }
                    info = cons.Invoke(args) as KIconToggleMenu.ToggleInfo;
                }
            }
            return info;
        }

        public override void OnLoad(Harmony harmony) {
            base.OnLoad(harmony);
            PUtil.InitLibrary();
            new PPatchManager(harmony).RegisterPatchClass(typeof(Patches));
            LocString.CreateLocStringKeys(typeof(MaterialSearchOverlayStrings));
            Localization.RegisterForTranslation(typeof(MaterialSearchOverlayStrings));
            OpenOverlay = new PActionManager().CreateAction(MaterialSearchOverlayStrings.
                OVERLAY_ACTION, MaterialSearchOverlayStrings.INPUT_BINDINGS.ROOT.MATERIALSEARCH);
            new PLocalization().Register();
            new PVersionCheck().Register(this, new SteamVersionChecker());
            var opts = POptions.ReadSettings<MaterialSearchOverlayOptions>() ??
                new MaterialSearchOverlayOptions();
            Log.DebugEnabled = opts.EnableDebugLogging;
            ShowTypeBadges = opts.ShowTypeBadges;
            new POptions().RegisterOptions(this, typeof(MaterialSearchOverlayOptions));
            if (PPatchTools.TryGetFieldValue<IDictionary<HashedString, StatusItemOverlays>>(
                    typeof(StatusItem), "overlayBitfieldMap", out var overlayBits)) {
                overlayBits.Add(MaterialSearchOverlay.ID, StatusItemOverlays.Farming);
                Log.Debug("OnLoad: overlayBitfieldMap registered");
            } else
                Log.Debug("OnLoad: overlayBitfieldMap NOT FOUND");
        }

        [HarmonyPatch(typeof(OverlayLegend), "OnSpawn")]
        public static class OverlayLegend_OnSpawn_Patch {
            private static Transform DropdownItemsParent;

            private static TMP_InputField SearchInput;

            private static bool suppressDropdown;

            internal static void Prefix(ICollection<OverlayLegend.OverlayInfo> ___overlayInfoList) {
                int before = ___overlayInfoList.Count;
                ___overlayInfoList.Add(new OverlayLegend.OverlayInfo {
                    infoUnits = new List<OverlayLegend.OverlayInfoUnit>(1) {
                        new OverlayLegend.OverlayInfoUnit(
                            Assets.GetSprite(MaterialSearchOverlayStrings.OVERLAY_ICON),
                            "STRINGS.UI.OVERLAYS.MATERIALSEARCH.DESCRIPTION",
                            Color.white, Color.white)
                    },
                    isProgrammaticallyPopulated = true,
                    mode = MaterialSearchOverlay.ID,
                    name = "STRINGS.UI.OVERLAYS.MATERIALSEARCH.NAME",
                });
                Log.Debug("Prefix: overlayInfoList {0}→{1} items, added mode={2}".F(before, before + 1, MaterialSearchOverlay.ID));
            }

            internal static void Postfix(OverlayLegend __instance) {
                Log.Debug("Postfix: OverlayLegend instance={0}".F(__instance.name));
                var field = new PTextField("MaterialSearchInput") {
                    PlaceholderText = MaterialSearchOverlayStrings.UI.OVERLAYS.MATERIALSEARCH.SEARCH_PLACEHOLDER,
                    FlexSize = Vector2.right,
                    Text = ""
                }.AddOnRealize(obj => {
                    obj.transform.SetParent(__instance.transform, false);
                    Log.Debug("Postfix AddOnRealize: parent={0}".F(__instance.transform.name));
                    var input = obj.GetComponent<TMP_InputField>();
                    if (input != null) {
                        SearchInput = input;
                        Log.Debug("Postfix AddOnRealize: TMP_InputField found, SearchInput stored");
                        input.onValueChanged.AddListener(text => {
                            string trimmed = text.Trim();
                            string searchText = string.IsNullOrEmpty(trimmed) ? null : trimmed.ToUpperInvariant();
                            Log.Debug("onValueChanged: text='{0}' trimmed='{1}' searchText='{2}' suppressDropdown={3}".F(
                                text, trimmed, searchText ?? "null", suppressDropdown));
                            if (MaterialSearchOverlay.Instance != null) {
                                MaterialSearchOverlay.Instance.SetSearchText(searchText);
                                Game.Instance.ForceOverlayUpdate();
                                Log.Debug("onValueChanged: ForceOverlayUpdate called");
                            }
                            if (MassLabel != null)
                                MassLabel.SetActive(false);
                            if (!suppressDropdown)
                                PopulateDropdown(text);
                        });
                        input.onSelect.AddListener(_ => {
                            Log.Debug("onSelect: input.text='{0}'".F(input.text));
                            if (!string.IsNullOrEmpty(input.text))
                                PopulateDropdown(input.text);
                        });

                    } else
                        Log.Debug("Postfix AddOnRealize: TMP_InputField IS NULL");
                });
                var go = field.Build();
                go.SetActive(false);
                SearchField = go;
                Log.Debug("Postfix: SearchField built, active={0}".F(go.activeInHierarchy));

                var massLabelObj = new GameObject("MassLabel");
                massLabelObj.transform.SetParent(__instance.transform, false);
                var massText = massLabelObj.AddComponent<TextMeshProUGUI>();
                massText.fontSize = 13;
                massText.color = Color.white;
                massText.alignment = TextAlignmentOptions.Left;
                massText.raycastTarget = false;
                var massLayout = massLabelObj.AddComponent<LayoutElement>();
                massLayout.flexibleWidth = 1;
                // Room for a mass breakdown plus a per kind count line
                massLayout.minHeight = 52;
                massLabelObj.SetActive(false);
                MassLabel = massLabelObj;
                Log.Debug("Postfix: MassLabel built");

                var labelPanel = new PPanel("DropdownItems") {
                    Direction = PanelDirection.Vertical,
                    FlexSize = new Vector2(1, 0),
                    Spacing = 1
                }.AddOnRealize(panel => {
                    DropdownItemsParent = panel.transform;
                    Log.Debug("DropdownItems AddOnRealize: parent set");
                });
                var sgo = labelPanel.Build();
                sgo.transform.SetParent(__instance.transform, false);
                sgo.SetActive(false);
                Patches.DropdownContainer = sgo;
                Log.Debug("Postfix: DropdownContainer built, active={0}".F(sgo.activeInHierarchy));
            }

            private static void PopulateDropdown(string text) {
                Log.Debug("PopulateDropdown ENTER: text='{0}'".F(text));
                if (DropdownItemsParent == null) {
                    Log.Debug("PopulateDropdown: DropdownItemsParent is null, returning early");
                    return;
                }
                Patches.DropdownContainer.SetActive(false);
                for (int i = DropdownItemsParent.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(DropdownItemsParent.
                        GetChild(i).gameObject);
                Log.Debug("PopulateDropdown: children destroyed");
                if (string.IsNullOrEmpty(text?.Trim())) {
                    Log.Debug("PopulateDropdown: text empty, returning early");
                    return;
                }
                string upper = text.Trim().ToUpperInvariant();
                var matches = SearchIndex.Match(upper);
                Log.Debug("PopulateDropdown: found {0} matches for '{1}'".F(matches.Count, upper));
                if (matches.Count == 0) {
                    Log.Debug("PopulateDropdown: no matches, returning early");
                    return;
                }
                foreach (var entry in matches)
                    CreateDropdownRow(entry);
                Patches.DropdownContainer.SetActive(true);
                Log.Debug("PopulateDropdown EXIT: showing {0} rows".F(matches.Count));
            }

            private static void CreateDropdownRow(SearchEntry entry) {
                string name = entry.DisplayName;
                Color swatch = entry.Swatch;
                Log.Debug("CreateDropdownRow: {0} '{1}' swatch={2}".F(entry.Kind, name, swatch));
                var row = new GameObject("Row_" + name);
                row.transform.SetParent(DropdownItemsParent, false);
                var layout = row.AddComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.spacing = 5;
                layout.padding = new RectOffset(4, 4, 2, 2);
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                var bg = row.AddComponent<Image>();
                bg.color = new Color(0, 0, 0, 0.01f);
                var swatchObj = new GameObject("Swatch");
                swatchObj.transform.SetParent(row.transform, false);
                var swatchImg = swatchObj.AddComponent<Image>();
                swatchImg.color = swatch;
                swatchImg.raycastTarget = false;
                var swatchLayout = swatchObj.AddComponent<LayoutElement>();
                swatchLayout.preferredWidth = 14;
                swatchLayout.preferredHeight = 14;
                // Skip the badge when it would just repeat the row name, as the plain
                // "Duplicant" row would otherwise read "Duplicant | Duplicant".
                var typeLabel = TypeLabel(entry.Kind);
                if (ShowTypeBadges && !string.Equals(typeLabel, name, StringComparison.Ordinal))
                    AddTypeBadge(row, entry.Kind, typeLabel);
                var label = new GameObject("Name");
                label.transform.SetParent(row.transform, false);
                var tmpText = label.AddComponent<TextMeshProUGUI>();
                tmpText.text = name;
                tmpText.fontSize = 13;
                tmpText.color = Color.white;
                tmpText.alignment = TextAlignmentOptions.Left;
                tmpText.raycastTarget = false;
                var labelLayout = label.AddComponent<LayoutElement>();
                labelLayout.flexibleWidth = 1;
                var trigger = row.AddComponent<EventTrigger>();
                var click = new EventTrigger.Entry {
                    eventID = EventTriggerType.PointerClick
                };
                click.callback.AddListener(_ => {
                    Log.Debug("PointerClick: '{0}' calling SelectEntry".F(name));
                    OnResultSelected(entry);
                });
                trigger.triggers.Add(click);
                var enter = new EventTrigger.Entry {
                    eventID = EventTriggerType.PointerEnter
                };
                enter.callback.AddListener(_ => {
                    Log.Debug("PointerEnter: name='{0}'".F(name));
                    bg.color = new Color(1, 1, 1, 0.15f);
                });
                trigger.triggers.Add(enter);
                var exit = new EventTrigger.Entry {
                    eventID = EventTriggerType.PointerExit
                };
                exit.callback.AddListener(_ => {
                    Log.Debug("PointerExit: name='{0}'".F(name));
                    bg.color = new Color(0, 0, 0, 0.01f);
                });
                trigger.triggers.Add(exit);
            }

            /// <summary>
            /// Short kind label on a dropdown row, so "Fish" as a food and "Pacu" as a
            /// creature are distinguishable when several kinds share a name.
            /// </summary>
            private static void AddTypeBadge(GameObject row, TargetKind kind, string label) {
                var badge = new GameObject("Type");
                badge.transform.SetParent(row.transform, false);
                var badgeText = badge.AddComponent<TextMeshProUGUI>();
                badgeText.text = label;
                badgeText.fontSize = 10;
                badgeText.color = SearchIndex.SwatchFor(kind);
                badgeText.alignment = TextAlignmentOptions.Left;
                badgeText.raycastTarget = false;
                var badgeLayout = badge.AddComponent<LayoutElement>();
                badgeLayout.preferredWidth = 58;
            }

            private static string TypeLabel(TargetKind kind) {
                switch (kind) {
                case TargetKind.Material: return MSearchTypes.MATERIAL;
                case TargetKind.Equipment: return MSearchTypes.EQUIPMENT;
                case TargetKind.Duplicant: return MSearchTypes.DUPLICANT;
                case TargetKind.Bionic: return MSearchTypes.BIONIC;
                case TargetKind.Booster: return MSearchTypes.BOOSTER;
                case TargetKind.Critter: return MSearchTypes.CRITTER;
                case TargetKind.Food: return MSearchTypes.FOOD;
                case TargetKind.Plant: return MSearchTypes.PLANT;
                case TargetKind.Industrial: return MSearchTypes.INDUSTRIAL;
                default: return MSearchTypes.ARTIFACT;
                }
            }

            private static void OnResultSelected(SearchEntry entry) {
                Log.Debug("OnResultSelected ENTER: entry={0}".F(
                    entry?.DisplayName ?? "null"));
                suppressDropdown = true;
                if (MaterialSearchOverlay.Instance != null) {
                    MaterialSearchOverlay.Instance.SelectEntry(entry);
                    ShowResultLabel();
                    Log.Debug("OnResultSelected: calling ForceOverlayUpdate");
                    Game.Instance.ForceOverlayUpdate();
                    Log.Debug("OnResultSelected: ForceOverlayUpdate returned");
                } else
                    Log.Debug("OnResultSelected: Instance IS NULL");
                if (SearchInput != null) {
                    Log.Debug("OnResultSelected: SearchInput.text BEFORE='{0}'".F(SearchInput.text));
                    SearchInput.SetTextWithoutNotify(entry?.DisplayName);
                    Log.Debug("OnResultSelected: SearchInput.text AFTER='{0}'".F(SearchInput.text));
                } else
                    Log.Debug("OnResultSelected: SearchInput IS NULL");
                Log.Debug("OnResultSelected: hiding dropdown");
                Patches.DropdownContainer?.SetActive(false);
                suppressDropdown = false;
                Log.Debug("OnResultSelected EXIT");
            }

            /// <summary>
            /// Elements keep the mass breakdown they always had. Everything else only reports
            /// how many were found per kind, which is far more useful than a mass figure for
            /// a duplicant.
            /// </summary>
            private static void ShowResultLabel() {
                var overlay = MaterialSearchOverlay.Instance;
                if (overlay == null || MassLabel == null)
                    return;
                var matches = overlay.Matches;
                if (matches == null || matches.IsEmpty) {
                    SetLabelText(MSearch.NO_MATCHES_LABEL);
                    return;
                }
                // A query can resolve to both an element and a live entity, e.g. a plant
                // whose name is also a food. Both lines are kept in that case rather than
                // letting one hide the other.
                var lines = new List<string>(2);
                if (matches.Elements.Count > 0) {
                    float backwallMass = 0f;
                    foreach (var elementId in matches.Elements)
                        backwallMass += MaterialSearchOverlay.BackwallMass(elementId);
                    lines.Add(FormatMassLabel(CalculateTotalMass(matches.Elements),
                        CalculateDebrisMass(matches.Elements),
                        CalculateBuildingMass(matches.Elements), backwallMass));
                }
                string counts = FormatCountLabel();
                if (counts != null)
                    lines.Add(counts);
                SetLabelText(lines.Count > 0 ? string.Join("\n", lines) :
                    MSearch.NO_MATCHES_LABEL);
            }

            private static void SetLabelText(string text) {
                MassLabel.GetComponent<TextMeshProUGUI>().text = text;
                MassLabel.SetActive(true);
            }

            private static string FormatCountLabel() {
                var parts = new List<string>(6);
                AppendCount(parts, TargetKind.Duplicant);
                AppendCount(parts, TargetKind.Bionic);
                AppendCount(parts, TargetKind.Booster);
                AppendCount(parts, TargetKind.Critter);
                AppendCount(parts, TargetKind.Equipment);
                AppendCount(parts, TargetKind.Food);
                AppendCount(parts, TargetKind.Plant);
                AppendCount(parts, TargetKind.Industrial);
                AppendCount(parts, TargetKind.Artifact);
                if (parts.Count == 0) {
                    // Something matched the search but none of it exists in the colony yet,
                    // so there is nothing to report a count for.
                    return null;
                }
                return string.Format(MSearch.COUNT_LABEL, string.Join(", ", parts));
            }

            private static void AppendCount(List<string> parts, TargetKind kind) {
                int count = SearchHighlighter.CountFor(kind);
                if (count > 0)
                    parts.Add(string.Format(CountLabel(kind), count));
            }

            private static LocString CountLabel(TargetKind kind) {
                switch (kind) {
                case TargetKind.Material: return MSearch.COUNT_MATERIAL;
                case TargetKind.Equipment: return MSearch.COUNT_EQUIPMENT;
                case TargetKind.Duplicant: return MSearch.COUNT_DUPLICANT;
                case TargetKind.Bionic: return MSearch.COUNT_BIONIC;
                case TargetKind.Booster: return MSearch.COUNT_BOOSTER;
                case TargetKind.Critter: return MSearch.COUNT_CRITTER;
                case TargetKind.Food: return MSearch.COUNT_FOOD;
                case TargetKind.Plant: return MSearch.COUNT_PLANT;
                case TargetKind.Industrial: return MSearch.COUNT_INDUSTRIAL;
                default: return MSearch.COUNT_ARTIFACT;
                }
            }

            /// <summary>
            /// The game only accepts a translation whose placeholders match the English
            /// string, so these patterns can be formatted directly.
            /// </summary>
            private static string FormatMassLabel(float naturalMass, float debrisMass,
                    float buildingMass, float backwallMass) {
                return string.Format(MSearch.MASS_LABEL,
                    GameUtil.GetFormattedMass(naturalMass),
                    GameUtil.GetFormattedMass(debrisMass),
                    GameUtil.GetFormattedMass(buildingMass),
                    GameUtil.GetFormattedMass(backwallMass));
            }

            private static float CalculateTotalMass(HashSet<SimHashes> elementIds) {
                int worldId = ClusterManager.Instance.activeWorldId;
                float mass = 0f;
                for (int cell = 0; cell < Grid.CellCount; cell++) {
                    if ((int)Grid.WorldIdx[cell] != worldId)
                        continue;
                    if (Grid.Visible[cell] <= 20)
                        continue;
                    var element = Grid.Element[cell];
                    if (element != null && elementIds.Contains(element.id))
                        mass += Grid.Mass[cell];
                }
                return mass;
            }

            private static float CalculateDebrisMass(HashSet<SimHashes> elementIds) {
                int worldId = ClusterManager.Instance.activeWorldId;
                float mass = 0f;
                var pickupables = Components.Pickupables.Items;
                for (int i = 0; i < pickupables.Count; i++) {
                    var pe = pickupables[i].GetComponent<PrimaryElement>();
                    if (pe == null || !elementIds.Contains(pe.ElementID))
                        continue;
                    int cell = Grid.PosToCell(pickupables[i]);
                    if ((int)Grid.WorldIdx[cell] != worldId)
                        continue;
                    // Explored space only, same as the other two walks.
                    if (Grid.Visible[cell] <= 20)
                        continue;
                    mass += pe.Mass;
                }
                return mass;
            }

            private static float CalculateBuildingMass(HashSet<SimHashes> elementIds) {
                int worldId = ClusterManager.Instance.activeWorldId;
                float mass = 0f;
                var buildings = Components.BuildingCompletes.Items;
                for (int i = 0; i < buildings.Count; i++) {
                    var pe = buildings[i].GetComponent<PrimaryElement>();
                    if (pe == null || !elementIds.Contains(pe.ElementID))
                        continue;
                    int cell = Grid.PosToCell(buildings[i]);
                    if ((int)Grid.WorldIdx[cell] != worldId)
                        continue;
                    // Explored space only, matching what the highlighter can light up. All
                    // three walks are filtered the same way, so a mass figure and the set of
                    // things it describes never disagree.
                    if (Grid.Visible[cell] <= 20)
                        continue;
                    mass += pe.Mass;
                }
                return mass;
            }
        }

        [HarmonyPatch(typeof(OverlayMenu), "InitializeToggles")]
        public static class OverlayMenu_InitializeToggles_Patch {
            internal static void Postfix(ICollection<KIconToggleMenu.ToggleInfo> ___overlayToggleInfos) {
                LocString.CreateLocStringKeys(typeof(MaterialSearchOverlayStrings.UI));
                var action = OpenOverlay?.GetKAction() ?? PAction.MaxAction;
                Log.Debug("InitializeToggles: action={0}".F(action));
                var info = CreateOverlayInfo(MaterialSearchOverlayStrings.UI.OVERLAYS.
                    MATERIALSEARCH.BUTTON, MaterialSearchOverlayStrings.OVERLAY_ICON,
                    MaterialSearchOverlay.ID, action, MaterialSearchOverlayStrings.UI.
                    OVERLAYS.MATERIALSEARCH.TOOLTIP);
                if (info != null) {
                    ___overlayToggleInfos?.Add(info);
                    Log.Debug("InitializeToggles: toggle info added");
                } else
                    Log.Debug("InitializeToggles: toggle info CREATION FAILED");
            }
        }

        [HarmonyPatch(typeof(OverlayScreen), "RegisterModes")]
        public static class OverlayScreen_RegisterModes_Patch {
            internal static void Postfix(OverlayScreen __instance) {
                Log.Debug("RegisterModes: Creating MaterialSearchOverlay, invoking REGISTER_MODE");
                REGISTER_MODE.Invoke(__instance, new MaterialSearchOverlay());
                Log.Debug("RegisterModes: done");
            }
        }

        [HarmonyPatch(typeof(SimDebugView), "OnPrefabInit")]
        public static class SimDebugView_OnPrefabInit_Patch {
            internal static void Postfix(IDictionary<HashedString, Func<SimDebugView, int, Color>> ___getColourFuncs) {
                ___getColourFuncs[MaterialSearchOverlay.ID] = MaterialSearchOverlay.GetColor;
                Log.Debug("OnPrefabInit: getColourFuncs[{0}] registered, count={1}".F(MaterialSearchOverlay.ID, ___getColourFuncs.Count));
            }
        }
    }

    internal static class Log {
        internal static bool DebugEnabled { get; set; } = true;

        internal static void Debug(string message) {
            if (DebugEnabled)
                PUtil.LogDebug(message);
        }
    }
}
