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

        /// <summary>
        /// Painted over every cell the current search does not highlight, including the whole
        /// map while the search is still empty. Opaque, so the overlay always opens on the same
        /// grey backdrop and a matching tile stands out against it.
        /// </summary>
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

        /// <summary>
        /// The current per cell backwall match snapshot, for readers that need to turn it into
        /// positions. Null before the first scan.
        /// </summary>
        internal static SimHashes[] BackwallSnapshot => backwallMatches;

        private static readonly Dictionary<SimHashes, float> BackwallMasses =
            new Dictionary<SimHashes, float>();

        private readonly Dictionary<GameObject, Color> targetColors =
            new Dictionary<GameObject, Color>();

        private readonly List<KMonoBehaviour> layerTargets = new List<KMonoBehaviour>();

        /// <summary>
        /// The entities the current search lit up, in the order the highlighter found them.
        /// The position list turns these into rows; it only reads the set, never changes it.
        /// </summary>
        internal List<KMonoBehaviour> Targets => layerTargets;

        /// <summary>
        /// Scratch set handed to the game's per-frame highlight pass. Since U60 that helper
        /// owns the set it walks and deletes everything offscreen from it, so the resolved
        /// match set can never be passed directly: matches have to survive panning away and
        /// back. The set is refilled from <see cref="layerTargets"/> every frame for the same
        /// reason.
        /// </summary>
        private readonly HybridListHashSet<KMonoBehaviour> frameTargets =
            new HybridListHashSet<KMonoBehaviour>();

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
            targetLayerMask = LayerMask.GetMask("MaskedOverlay");
            SearchText = null;
            SelectedEntry = null;
            // One condition covering every kind: the colour of a highlighted entity is looked
            // up by GameObject, so a single mixed list of targets can carry a different colour
            // per entity. The game asks about whichever component it is drawing, and one entity
            // can be reached through several of them, so the lookup resolves the GameObject
            // rather than trusting to be handed back the exact instance that matched.
            highlightConditions = new OverlayModes.ColorHighlightCondition[] {
                new OverlayModes.ColorHighlightCondition(
                    kmb => kmb != null && !LocationSelection.IsTargetHidden(kmb.gameObject) &&
                        targetColors.TryGetValue(kmb.gameObject, out var color) ? color :
                        Color.clear,
                    kmb => kmb != null && !LocationSelection.IsTargetHidden(kmb.gameObject) &&
                        targetColors.ContainsKey(kmb.gameObject))
            };
            Instance = this;
            Log.Debug("MaterialSearchOverlay: instance created ID={0}".F(ID));
        }

        internal static Color GetColor(SimDebugView _, int cell) {
            if (Instance == null || ElementColors == null)
                return NON_MATCHING_COLOR;
            // Read the snapshot reference exactly once: the main thread swaps it wholesale
            // on refresh, so a single read is all that is safe to do off-thread.
            var matches = snapshot;
            if (matches == null || matches.Elements.Count == 0)
                return NON_MATCHING_COLOR;
            var element = Grid.Element[cell];
            if (element == null)
                return NON_MATCHING_COLOR;
            if (matches.Elements.Contains(element.id) &&
                    ElementColors.TryGetValue(element.id, out var color) &&
                    LocationSelection.IsTileVisible(cell))
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
            if (id == default || !LocationSelection.IsBackwallVisible(cell))
                return NON_MATCHING_COLOR;
            return ElementColors.TryGetValue(id, out var color) ? color :
                NON_MATCHING_COLOR;
        }

        /// <summary>
        /// True while a search has resolved to something. The hover tooltip filter is a no-op
        /// otherwise, so opening the overlay with an empty search leaves the vanilla tooltip
        /// exactly as it was.
        /// </summary>
        internal bool HoverFilterActive => Matches != null && !Matches.IsEmpty;

        /// <summary>
        /// Whether a hovered object keeps its entry in the tooltip. Only the things the search
        /// matched: <see cref="targetColors"/> is the same set the overlay lights up, so what
        /// glows is exactly what the tooltip lists. A container lit up because of what it holds
        /// is in that set too (the highlighter adds the building rather than the stored item),
        /// so the bin still explains itself.
        /// </summary>
        internal bool ShouldShowHoverEntry(KSelectable selectable) {
            if (!HoverFilterActive || selectable == null)
                return true;
            return targetColors.ContainsKey(selectable.gameObject);
        }

        /// <summary>
        /// The colour the overlay paints one highlighted entity in. The position list uses it
        /// for its row swatch, so a row and the thing it points at on the map always agree.
        /// False when the object is not in the current highlight set.
        /// </summary>
        internal bool TryGetTargetColor(GameObject go, out Color color) {
            color = Color.white;
            return go != null && targetColors.TryGetValue(go, out color);
        }

        /// <summary>
        /// Whether the per cell element block belongs in the tooltip, asked by the game through
        /// the hover card's overlay filter map. Only when the cell's own element matched, so
        /// searching for a creature does not also explain the oxygen it is sitting in. A
        /// matching natural backwall counts as well, because the cell block is where the game
        /// labels it. Main thread only: it reads the live mouse cell.
        /// </summary>
        internal static bool ShouldShowHoverCell() {
            var overlay = Instance;
            if (overlay == null || !overlay.HoverFilterActive)
                return true;
            var camera = Camera.main;
            if (camera == null)
                return true;
            int cell = Grid.PosToCell(camera.ScreenToWorldPoint(KInputManager.GetMousePos()));
            if (!Grid.IsValidCell(cell))
                return true;
            var element = Grid.Element[cell];
            if (element != null && overlay.Matches.Elements.Contains(element.id))
                return true;
            return HasMatchingBackwall(cell);
        }

        /// <summary>True when the natural backwall at this cell matched the current search.</summary>
        private static bool HasMatchingBackwall(int cell) {
            var match = backwallMatches;
            return match != null && (uint)cell < (uint)match.Length && match[cell] != default;
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
        /// been revealed, and the sim reports a solid backwall whose element is in the match
        /// set. A solid tile in front of it does not disqualify the cell: the game still shows
        /// that backwall, and the overlay paints over the tile anyway, so the player can plan
        /// where to dig rather than only after digging. Must run on the main thread:
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

        /// <summary>
        /// Layer bit for <see cref="targetLayer"/>. Highlighted entities are moved onto that
        /// layer to be drawn over everything else, which also takes them out of the select
        /// tool's collider pass: its default mask does not include it. The mask has to be
        /// widened while the overlay is up or the things the search lit up cannot be clicked.
        /// </summary>
        private readonly int targetLayerMask;

        public override void Enable() {
            var camera = Camera.main;
            base.Enable();
            CameraController.Instance.ToggleColouredOverlayView(true);
            if (camera != null)
                camera.cullingMask |= cameraLayerMask;
            // Same approach as the conduit overlay: keep the tool's own layers so tiles and
            // buildings still report, and add the highlight layer back in. Everything the
            // search did not match is rejected by the click filter afterwards.
            if (SelectTool.Instance != null)
                SelectTool.Instance.SetLayerMask(SelectTool.Instance.GetDefaultLayerMask() |
                    targetLayerMask);
            // Prefabs, boosters and foods all need a loaded world: at db init Game.Instance is
            // null, which makes the game's DLC filter reject every prefab. First open of the
            // overlay is the earliest point they can be read.
            SearchIndex.EnsureWorldBuilt();
            // A fresh open starts with no pinned result: any pause left over from a previous
            // session must not keep refusing unpause until the player picks something again.
            ForcedPause.Reset();
            MatchLocationUI.Hide();
            // A direction key latched in an earlier session would keep the camera sliding
            // under the list until the next key press, so start from a clean pan state.
            CameraPan.ReleaseHeldKeys();
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
            if (SelectTool.Instance != null)
                SelectTool.Instance.ClearLayerMask();
            if (Patches.SearchField != null) {
                // Give the keyboard back before hiding the box: a field that is merely
                // deactivated can stay as the EventSystem's selection, and the camera would
                // then treat every movement key as "typing" for the rest of the session.
                Patches.DeselectSearchField();
                Patches.SearchField.SetActive(false);
                // The query has just been dropped from the match set, so the box must not
                // keep showing it: an auto-focused reopen would otherwise list rows for a
                // search that is no longer lit up on the map.
                Patches.ClearSearchField();
            }
            // The per frame release stops once the box loses focus, so a key that was still
            // held when the overlay closes has to be let go here: otherwise the camera keeps
            // panning after the map is back and the overlay is gone.
            CameraPan.ReleaseHeldKeys();
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
            // The list is a snapshot of a pinned selection, so it goes with the selection. The
            // pause it caused stays behind on purpose: the player can look around after closing
            // and resume with the normal pause key.
            MatchLocationUI.Hide();
            ForcedPause.End();
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
            // Whatever list is on screen belongs to the previous selection; a new query
            // invalidates it even before the new results are known.
            MatchLocationUI.Hide();
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
            for (int i = 0; i < deadColorKeys.Count; i++) {
                var key = deadColorKeys[i];
                SearchHighlighter.ForgetTarget(key);
                colors.Remove(key);
            }
        }

        public override void Update() {
            base.Update();
            // Runs before the entity upkeep below, which is skipped while the search is empty:
            // the pause has to be held for as long as the overlay is open, not only while
            // there are highlights to draw.
            ForcedPause.Guard();
            if (Matches == null || Matches.IsEmpty)
                return;
            if (layerTargets.Count == 0)
                return;
            RemoveDestroyedTargets(layerTargets, targetColors);
            Grid.GetVisibleExtents(out var min, out var max);
            frameTargets.Clear();
            frameTargets.Add(layerTargets);
            // Conditional, not Constant: an entity the position list has unticked is still a
            // target here so it can be ticked again without a rescan, but it must go back to
            // the normal layer. Objects on the highlight layer are drawn in their own colours
            // by the overlay view, so leaving an unticked one there made it show its real
            // colour instead of the grey everything unhighlighted gets.
            RemoveOffscreenAndUpdateHighlights(min, max, frameTargets,
                highlightConditions,
                OverlayModes.BringToFrontLayerSetting.Conditional, targetLayer);
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
