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

namespace BobbyModding.MaterialSearchOverlay {
    public sealed class Patches : KMod.UserMod2 {
        private const BindingFlags INSTANCE_ALL = PPatchTools.BASE_FLAGS | BindingFlags.
            Instance;

        private delegate void RegisterMode(OverlayScreen screen, OverlayModes.Mode mode);

        private static PAction OpenOverlay;

        internal static GameObject SearchField { get; private set; }

        internal static GameObject DropdownContainer { get; private set; }

        internal static GameObject MassLabel { get; private set; }

        private static readonly Type OVERLAY_TYPE = typeof(OverlayMenu).GetNestedType(
            "OverlayToggleInfo", INSTANCE_ALL);

        private static readonly RegisterMode REGISTER_MODE = typeof(OverlayScreen).
            Detour<RegisterMode>();

        [PLibMethod(RunAt.AfterDbInit)]
        internal static void AfterDbInit() {
            Assets.Sprites.Add(MaterialSearchOverlayStrings.OVERLAY_ICON,
                Assets.GetSprite("overlay_materials"));
            var elements = ElementLoader.elements;
            var names = new Dictionary<SimHashes, string>(elements.Count);
            var displayNames = new Dictionary<SimHashes, string>(elements.Count);
            var colors = new Dictionary<SimHashes, Color>(elements.Count);
            foreach (var element in elements) {
                if (element?.tag == null || !element.tag.IsValid ||
                        names.ContainsKey(element.id))
                    continue;
                string rawName = element.name;
                string cleanName = STRINGS.UI.StripLinkFormatting(rawName);
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
            LocString.CreateLocStringKeys(typeof(MaterialSearchOverlayStrings.INPUT_BINDINGS));
            OpenOverlay = new PActionManager().CreateAction(MaterialSearchOverlayStrings.
                OVERLAY_ACTION, MaterialSearchOverlayStrings.INPUT_BINDINGS.ROOT.MATERIALSEARCH);
            new PLocalization().Register();
            new PVersionCheck().Register(this, new SteamVersionChecker());
            var opts = POptions.ReadSettings<MaterialSearchOverlayOptions>() ??
                new MaterialSearchOverlayOptions();
            Log.DebugEnabled = opts.EnableDebugLogging;
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

            private static readonly int MAX_DROPDOWN_ITEMS = 25;

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
                                MaterialSearchOverlay.Instance.SelectedElementId = null;
                                MaterialSearchOverlay.Instance.SearchText = searchText;
                                MaterialSearchOverlay.Instance.RefreshHighlights();
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
                massLayout.minHeight = 20;
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
                var names = MaterialSearchOverlay.ElementNames;
                var matches = new List<(SimHashes id, string upper)>(
                    Mathf.Min(MAX_DROPDOWN_ITEMS, names.Count));
                foreach (var kvp in names) {
                    if (kvp.Value.Contains(upper))
                        matches.Add((kvp.Key, kvp.Value));
                }
                Log.Debug("PopulateDropdown: found {0} matches for '{1}'".F(matches.Count, upper));
                if (matches.Count == 0) {
                    Log.Debug("PopulateDropdown: no matches, returning early");
                    return;
                }
                matches.Sort((a, b) => a.upper.CompareTo(b.upper));
                if (matches.Count > MAX_DROPDOWN_ITEMS)
                    matches = matches.GetRange(0, MAX_DROPDOWN_ITEMS);
                var displays = MaterialSearchOverlay.ElementDisplayNames;
                var colors = MaterialSearchOverlay.ElementColors;
                foreach (var (id, upName) in matches) {
                    string displayName = displays.TryGetValue(id, out var dn) ? dn :
                        upName;
                    Color swatch = colors.TryGetValue(id, out var c) ? c :
                        Color.gray;
                    CreateDropdownRow(id, displayName, swatch);
                }
                Patches.DropdownContainer.SetActive(true);
                Log.Debug("PopulateDropdown EXIT: showing {0} items".F(matches.Count));
            }

            private static void CreateDropdownRow(SimHashes id, string name,
                    Color swatchColor) {
                Log.Debug("CreateDropdownRow: id={0} name='{1}' swatch={2}".F(id, name, swatchColor));
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
                var swatch = new GameObject("Swatch");
                swatch.transform.SetParent(row.transform, false);
                var swatchImg = swatch.AddComponent<Image>();
                swatchImg.color = swatchColor;
                swatchImg.raycastTarget = false;
                var swatchLayout = swatch.AddComponent<LayoutElement>();
                swatchLayout.preferredWidth = 14;
                swatchLayout.preferredHeight = 14;
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
                    Log.Debug("PointerClick: id={0} name='{1}' calling OnElementSelected".F(id, name));
                    OnElementSelected(id, name);
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

            private static void OnElementSelected(SimHashes id, string name) {
                Log.Debug("OnElementSelected ENTER: id={0} name='{1}'".F(id, name));
                Log.Debug("OnElementSelected: setting suppressDropdown=true");
                suppressDropdown = true;
                if (MaterialSearchOverlay.Instance != null) {
                    MaterialSearchOverlay.Instance.SelectedElementId = id;
                    MaterialSearchOverlay.Instance.RefreshHighlights();
                    float naturalMass = CalculateTotalMass(id);
                    float debrisMass = CalculateDebrisMass(id);
                    float buildingMass = CalculateBuildingMass(id);
                    string massStr = string.Format("Natural Tile: {0} |  Debris: {1} |  Buildings: {2}",
                        GameUtil.GetFormattedMass(naturalMass),
                        GameUtil.GetFormattedMass(debrisMass),
                        GameUtil.GetFormattedMass(buildingMass));
                    Log.Debug("OnElementSelected: natural={0} debris={1} buildings={2} formatted='{3}'".F(
                        naturalMass, debrisMass, buildingMass, massStr));
                    if (MassLabel != null) {
                        MassLabel.GetComponent<TextMeshProUGUI>().text = massStr;
                        MassLabel.SetActive(true);
                    }
                    Log.Debug("OnElementSelected: calling ForceOverlayUpdate");
                    Game.Instance.ForceOverlayUpdate();
                    Log.Debug("OnElementSelected: ForceOverlayUpdate returned");
                } else
                    Log.Debug("OnElementSelected: Instance IS NULL");
                if (SearchInput != null) {
                    Log.Debug("OnElementSelected: SearchInput.text BEFORE='{0}'".F(SearchInput.text));
                    SearchInput.SetTextWithoutNotify(name);
                    Log.Debug("OnElementSelected: SearchInput.text AFTER='{0}'".F(SearchInput.text));
                } else
                    Log.Debug("OnElementSelected: SearchInput IS NULL");
                Log.Debug("OnElementSelected: hiding dropdown");
                Patches.DropdownContainer?.SetActive(false);
                Log.Debug("OnElementSelected: setting suppressDropdown=false");
                suppressDropdown = false;
                Log.Debug("OnElementSelected EXIT");
            }

            private static float CalculateTotalMass(SimHashes elementId) {
                int worldId = ClusterManager.Instance.activeWorldId;
                float mass = 0f;
                for (int cell = 0; cell < Grid.CellCount; cell++) {
                    if ((int)Grid.WorldIdx[cell] != worldId)
                        continue;
                    if (Grid.Visible[cell] <= 20)
                        continue;
                    if (Grid.Element[cell]?.id == elementId)
                        mass += Grid.Mass[cell];
                }
                return mass;
            }

            private static float CalculateDebrisMass(SimHashes elementId) {
                int worldId = ClusterManager.Instance.activeWorldId;
                float mass = 0f;
                foreach (var pickupable in Components.Pickupables.Items) {
                    var pe = pickupable.GetComponent<PrimaryElement>();
                    if (pe == null || pe.ElementID != elementId)
                        continue;
                    int cell = Grid.PosToCell(pickupable);
                    if ((int)Grid.WorldIdx[cell] != worldId)
                        continue;
                    mass += pe.Mass;
                }
                return mass;
            }

            private static float CalculateBuildingMass(SimHashes elementId) {
                int worldId = ClusterManager.Instance.activeWorldId;
                float mass = 0f;
                foreach (var building in Components.BuildingCompletes.Items) {
                    var pe = building.GetComponent<PrimaryElement>();
                    if (pe == null || pe.ElementID != elementId)
                        continue;
                    int cell = Grid.PosToCell(building);
                    if ((int)Grid.WorldIdx[cell] != worldId)
                        continue;
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
