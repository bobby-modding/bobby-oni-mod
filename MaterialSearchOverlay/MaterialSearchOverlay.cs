using PeterHan.PLib.Core;
using System;
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

        /// <summary>
        /// The colour to highlight something built out of <paramref name="id"/> in, so a copper
        /// smelter lights up copper-red to match the ore rather than in the one flat colour
        /// that a whole kind has to share. Falls back to the generic material colour, which is
        /// what the kind swatch would have used.
        /// </summary>
        internal static Color ElementSwatchFor(SimHashes id) {
            if (ElementColors != null && ElementColors.TryGetValue(id, out var color))
                return color;
            return SearchIndex.SwatchFor(TargetKind.Material);
        }

        /// <summary>
        /// Returned by the cell overlay when the search has no element matches at all, which
        /// is the normal case for a creature or duplicant search. Leaves the map untouched
        /// instead of dimming every tile.
        /// </summary>
        internal static readonly Color NO_CELL_OVERLAY = Color.clear;

        /// <summary>
        /// Immutable per refresh snapshot of the active search. Written on the main thread by
        /// <see cref="RefreshHighlights"/> and read from the worker thread that runs
        /// <see cref="GetColor"/>, so the worker never touches live game state.
        /// </summary>
        private static SearchMatches snapshot = SearchMatches.FromQuery(null);

        internal SearchMatches Matches { get; private set; } = SearchMatches.FromQuery(null);

        /// <summary>The dropdown row the user pinned, or null while the search is free text.</summary>
        internal SearchEntry SelectedEntry { get; private set; }

        internal string SearchText { get; set; }

        /// <summary>
        /// Per cell snapshot of the natural backwall element that matched the current search.
        /// Written on the main thread by <see cref="RefreshBackwallData"/>, read from the worker
        /// thread that runs <see cref="GetColor"/>, so BackwallManager is never touched there.
        /// A default value means "no matching backwall at this cell".
        /// </summary>
        private static SimHashes[] backwallMatches;

        private static readonly Dictionary<SimHashes, float> BackwallMasses =
            new Dictionary<SimHashes, float>();

        private readonly Dictionary<GameObject, Color> targetColors =
            new Dictionary<GameObject, Color>();

        private readonly List<KMonoBehaviour> layerTargets = new List<KMonoBehaviour>();

        /// <summary>
        /// Reused scratch space for the dead-colour sweep so that per frame upkeep does not
        /// allocate. The sweep runs over a dictionary bounded by the current search, so the
        /// walk is short and there is nothing to gain from tracking removals incrementally.
        /// </summary>
        private readonly List<GameObject> deadColorKeys = new List<GameObject>();

        private readonly OverlayModes.ColorHighlightCondition[] highlightConditions;

        public MaterialSearchOverlay() {
            cameraLayerMask = LayerMask.GetMask("MaskedOverlay", "MaskedOverlayBG");
            targetLayer = LayerMask.NameToLayer("MaskedOverlay");
            SearchText = null;
            SelectedEntry = null;
            // One condition covering every kind: the colour of a highlighted entity is looked
            // up by GameObject, so a single mixed list of targets can carry a different colour
            // per entity. The game asks about whichever component it is drawing, and one entity
            // can be reached through several of them, so the lookup resolves the GameObject
            // rather than trusting to be handed back the exact instance that matched.
            highlightConditions = new OverlayModes.ColorHighlightCondition[] {
                new OverlayModes.ColorHighlightCondition(
                    kmb => kmb != null && targetColors.TryGetValue(kmb.gameObject, out var color)
                        ? color : Color.clear,
                    kmb => kmb != null && targetColors.ContainsKey(kmb.gameObject))
            };
            Instance = this;
            Log.Debug("MaterialSearchOverlay: instance created ID={0}".F(ID));
        }

        internal static Color GetColor(SimDebugView _, int cell) {
            if (Instance == null)
                return NO_CELL_OVERLAY;
            // Read the snapshot reference exactly once: the main thread swaps it wholesale
            // on refresh, so a single read is all that is safe to do off-thread.
            var matches = snapshot;
            if (matches == null || matches.Elements.Count == 0)
                return NO_CELL_OVERLAY;
            var element = Grid.Element[cell];
            if (element == null)
                return NON_MATCHING_COLOR;
            if (matches.Elements.Contains(element.id) &&
                    ElementColors.TryGetValue(element.id, out var color))
                return color;
            return GetBackwallColor(cell);
        }

        /// <summary>
        /// Returns the element color of the matching natural backwall at this cell, or
        /// <see cref="NON_MATCHING_COLOR"/> if there is none. Safe to call from the
        /// SimDebugView worker thread - it only reads the main thread snapshot.
        /// </summary>
        private static Color GetBackwallColor(int cell) {
            var match = backwallMatches;
            if (match == null || (uint)cell >= (uint)match.Length)
                return NON_MATCHING_COLOR;
            var id = match[cell];
            if (id == default)
                return NON_MATCHING_COLOR;
            return ElementColors.TryGetValue(id, out var color) ? color :
                NON_MATCHING_COLOR;
        }

        /// <summary>
        /// Visible natural backwall mass for an element, in kg, as last scanned by
        /// <see cref="RefreshBackwallData"/>.
        /// </summary>
        internal static float BackwallMass(SimHashes elementId) {
            return BackwallMasses.TryGetValue(elementId, out var mass) ? mass : 0f;
        }

        /// <summary>
        /// Scans the active world for visible natural backwall matching the search, filling the
        /// per cell highlight snapshot and the per element mass totals.
        /// </summary>
        /// <remarks>
        /// A cell counts as visible natural backwall when it belongs to the active world, has
        /// been revealed, is not covered by a solid tile, and the sim reports a solid backwall
        /// whose element is in the match set. This mirrors the vanilla check used by the dig
        /// tool (WorldDamage.GetApproximateDigTime). Must run on the main thread:
        /// BackwallManager points into native sim memory that is nulled out during world loads.
        /// </remarks>
        internal static void RefreshBackwallData(HashSet<SimHashes> matchingElements) {
            BackwallMasses.Clear();
            if (backwallMatches == null || backwallMatches.Length != Grid.CellCount)
                backwallMatches = new SimHashes[Grid.CellCount];
            else
                Array.Clear(backwallMatches, 0, backwallMatches.Length);
            if (matchingElements == null || matchingElements.Count == 0)
                return;
            for (int cell = 0; cell < Grid.CellCount; cell++) {
                if (!Grid.IsActiveWorld(cell))
                    continue;
                if (Grid.Visible[cell] <= 20)
                    continue;
                // A solid tile covers the backwall behind it, so it is not visible
                if (Grid.Solid[cell])
                    continue;
                if (!BackwallManager.HasBackwall(cell))
                    continue;
                var element = BackwallManager.At(cell).Element;
                if (element == null || !matchingElements.Contains(element.id))
                    continue;
                backwallMatches[cell] = element.id;
                BackwallMasses.TryGetValue(element.id, out var mass);
                BackwallMasses[element.id] = mass + BackwallManager.At(cell).Mass;
            }
        }

        private readonly int cameraLayerMask;

        private readonly int targetLayer;

        public override void Enable() {
            var camera = Camera.main;
            base.Enable();
            CameraController.Instance.ToggleColouredOverlayView(true);
            if (camera != null)
                camera.cullingMask |= cameraLayerMask;
            // Prefabs, boosters and foods all need a loaded world: at db init Game.Instance is
            // null, which makes the game's DLC filter reject every prefab. First open of the
            // overlay is the earliest point they can be read.
            SearchIndex.EnsureWorldBuilt();
            if (Patches.SearchField != null) {
                Patches.SearchField.SetActive(true);
                Log.Debug("Enable: SearchField set active");
            } else
                Log.Debug("Enable: SearchField is null");
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
            SearchHighlighter.Reset(layerTargets, targetColors);
            Matches = SearchMatches.FromQuery(null);
            snapshot = Matches;
            SelectedEntry = null;
            SearchText = null;
            RefreshBackwallData(null);
            base.Disable();
            Log.Debug("Disable: complete");
        }

        /// <summary>
        /// Free text search. Any previous dropdown selection is dropped so the whole match
        /// set is highlighted rather than the single pinned entry.
        /// </summary>
        internal void SetSearchText(string text) {
            SearchText = text;
            SelectedEntry = null;
            RefreshHighlights();
        }

        /// <summary> Pins one dropdown row. </summary>
        internal void SelectEntry(SearchEntry entry) {
            SelectedEntry = entry;
            SearchText = entry.DisplayName;
            RefreshHighlights();
        }

        /// <summary>
        /// Rebuilds the cell overlay, the backwall scan and the entity highlight sets from
        /// the current search or selection. Main thread only. This is a snapshot: matched
        /// entities are resolved once here and then tracked individually, so anything created
        /// afterwards needs a new search or selection to appear.
        /// </summary>
        internal void RefreshHighlights() {
            Matches = SelectedEntry != null ?
                SearchMatches.FromSelection(SelectedEntry) :
                SearchMatches.FromQuery(SearchText);
            snapshot = Matches;
            SearchHighlighter.Reset(layerTargets, targetColors);
            RefreshBackwallData(Matches.Elements);
            SearchHighlighter.Collect(Matches, layerTargets, targetColors);
            Log.Debug("RefreshHighlights: {0} elements, {1} entities (select={2}, text='{3}')".
                F(Matches.Elements.Count, layerTargets.Count,
                    SelectedEntry?.DisplayName ?? "none", SearchText ?? "null"));
        }

        /// <summary>
        /// Drops targets destroyed since the search resolved. Matched objects are held
        /// individually and followed wherever they go, so the sets can only shrink again when
        /// dead references are swept out. Cheap enough to run per frame, unlike the rescan it
        /// replaces: it never looks for new matches, it only discards dead ones.
        /// </summary>
        private void RemoveDestroyedTargets(List<KMonoBehaviour> targets,
                Dictionary<GameObject, Color> colors) {
            for (int i = targets.Count - 1; i >= 0; i--) {
                if (targets[i] == null)
                    targets.RemoveAt(i);
            }
            // A destroyed component cannot report the GameObject it belonged to, so the dead
            // colours are swept by looking for destroyed keys instead of by index.
            // ReferenceEquals first: a destroyed Unity object reports itself null through the
            // overloaded operator, and that is the case worth collecting here.
            deadColorKeys.Clear();
            foreach (var go in colors.Keys)
                if (!ReferenceEquals(go, null) && go == null)
                    deadColorKeys.Add(go);
            for (int i = 0; i < deadColorKeys.Count; i++)
                colors.Remove(deadColorKeys[i]);
        }

        public override void Update() {
            base.Update();
            if (Matches == null || Matches.IsEmpty)
                return;
            if (layerTargets.Count == 0)
                return;
            RemoveDestroyedTargets(layerTargets, targetColors);
            Grid.GetVisibleExtents(out var min, out var max);
            UpdateHighlightTypeOverlay(min, max, layerTargets, null,
                highlightConditions,
                OverlayModes.BringToFrontLayerSetting.Constant, targetLayer);
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
