using PeterHan.PLib.Core;
using System.Collections.Generic;
using UnityEngine;


namespace BobbyModding.MaterialSearchOverlay {
    public class MaterialSearchOverlay : OverlayModes.Mode {
        public static readonly HashedString ID = new HashedString("MaterialSearch");

        internal static MaterialSearchOverlay Instance { get; private set; }

        internal static Dictionary<SimHashes, string> ElementNames { get; set; }

        internal static Dictionary<SimHashes, string> ElementDisplayNames { get; set; }

        internal static Dictionary<SimHashes, Color> ElementColors { get; set; }

        internal static readonly Color NON_MATCHING_COLOR = new Color(0.33f, 0.33f, 0.33f);

        internal string SearchText { get; set; }

        internal SimHashes? SelectedElementId { get; set; }

        private static readonly OverlayModes.ColorHighlightCondition[] HIGHLIGHT_CONDITIONS =
            new OverlayModes.ColorHighlightCondition[] {
                new OverlayModes.ColorHighlightCondition(
                    (kmb) => {
                        var pe = kmb as PrimaryElement;
                        if (pe == null) return Color.black;
                        return ElementColors.TryGetValue(pe.ElementID, out var c) ? c : Color.black;
                    },
                    (kmb) => kmb.GetComponent<KBatchedAnimController>()?.IsVisible() ?? false
                )
            };

        internal static Color GetColor(SimDebugView _, int cell) {
            if (Instance == null)
                return NON_MATCHING_COLOR;
            var element = Grid.Element[cell];
            if (element == null)
                return NON_MATCHING_COLOR;
            if (Instance.SelectedElementId.HasValue) {
                if (element.id == Instance.SelectedElementId.Value &&
                        ElementColors.TryGetValue(element.id, out var color))
                    return color;
                return NON_MATCHING_COLOR;
            }
            string search = Instance.SearchText;
            if (string.IsNullOrEmpty(search))
                return NON_MATCHING_COLOR;
            string matchName = null;
            Color matchColor = Color.black;
            bool found = ElementNames.TryGetValue(element.id, out matchName) &&
                    matchName.Contains(search) &&
                    ElementColors.TryGetValue(element.id, out matchColor);
            if (found)
                return matchColor;
            return NON_MATCHING_COLOR;
        }

        private readonly int cameraLayerMask;

        private readonly List<PrimaryElement> layerTargets;

        private readonly int targetLayer;

        public MaterialSearchOverlay() {
            cameraLayerMask = LayerMask.GetMask("MaskedOverlay", "MaskedOverlayBG");
            legendFilters = new Dictionary<string, ToolParameterMenu.ToggleState>();
            layerTargets = new List<PrimaryElement>();
            targetLayer = LayerMask.NameToLayer("MaskedOverlay");
            SearchText = null;
            SelectedElementId = null;
            Instance = this;
            Log.Debug("MaterialSearchOverlay: instance created ID={0}".F(ID));
        }

        public override void Enable() {
            var camera = Camera.main;
            base.Enable();
            CameraController.Instance.ToggleColouredOverlayView(true);
            if (camera != null)
                camera.cullingMask |= cameraLayerMask;
            if (Patches.SearchField != null) {
                Patches.SearchField.SetActive(true);
                Log.Debug("Enable: SearchField set active");
            } else
                Log.Debug("Enable: SearchField is null");
            if (SelectedElementId.HasValue || !string.IsNullOrEmpty(SearchText))
                RefreshHighlights();
        }

        public override void Disable() {
            var camera = Camera.main;
            CameraController.Instance.ToggleColouredOverlayView(false);
            if (camera != null)
                camera.cullingMask &= ~cameraLayerMask;
            if (Patches.SearchField != null)
                Patches.SearchField.SetActive(false);
            if (Patches.DropdownContainer != null) {
                Patches.DropdownContainer.SetActive(false);
                Log.Debug("Disable: DropdownContainer set inactive");
            }
            if (Patches.MassLabel != null)
                Patches.MassLabel.SetActive(false);
            DisableHighlightTypeOverlay(layerTargets);
            base.Disable();
            Log.Debug("Disable: complete");
        }

        internal void RefreshHighlights() {
            DisableHighlightTypeOverlay(layerTargets);
            HashSet<SimHashes> matchingElements = null;
            if (SelectedElementId.HasValue) {
                matchingElements = new HashSet<SimHashes> { SelectedElementId.Value };
            } else if (!string.IsNullOrEmpty(SearchText)) {
                string search = SearchText;
                matchingElements = new HashSet<SimHashes>();
                foreach (var kvp in ElementNames) {
                    if (kvp.Value.Contains(search))
                        matchingElements.Add(kvp.Key);
                }
            }
            if (matchingElements != null && matchingElements.Count > 0) {
                foreach (var building in Components.BuildingCompletes.Items) {
                    var pe = building.GetComponent<PrimaryElement>();
                    if (pe != null && matchingElements.Contains(pe.ElementID))
                        layerTargets.Add(pe);
                }
                foreach (var pickupable in Components.Pickupables.Items) {
                    var pe = pickupable.GetComponent<PrimaryElement>();
                    if (pe != null && matchingElements.Contains(pe.ElementID))
                        layerTargets.Add(pe);
                }
            }
        }

        public override void Update() {
            base.Update();
            if (layerTargets.Count > 0) {
                Grid.GetVisibleExtents(out var min, out var max);
                UpdateHighlightTypeOverlay(min, max, layerTargets, null,
                    HIGHLIGHT_CONDITIONS,
                    OverlayModes.BringToFrontLayerSetting.Constant, targetLayer);
            }
        }

        public override List<LegendEntry> GetCustomLegendData() {
            return new List<LegendEntry>();
        }

        public override string GetSoundName() {
            return "Material";
        }

        public override HashedString ViewMode() {
            return ID;
        }
    }
}
