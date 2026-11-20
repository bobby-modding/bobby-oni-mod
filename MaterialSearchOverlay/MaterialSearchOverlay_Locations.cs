using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BobbyModding.MaterialSearchOverlay {
    /// <summary>Where one position row came from.</summary>
    internal enum MatchLocationSource {
        /// <summary>A connected run of matching tiles.</summary>
        Tile,
        /// <summary>A connected run of matching natural backwall.</summary>
        Backwall,
        Debris,
        Building,
        Container,
        Entity
    }

    /// <summary>
    /// One entry in the position list: either a connected area of matching cells (tiles or
    /// natural backwall), or a single matched item. Areas can be expanded into their cells by
    /// the UI, which is why they keep the whole cell list instead of only a centre.
    /// <para>
    /// Coordinates are world local: X and Y count from the bottom-left corner of the active
    /// asteroid, not from the corner of the whole cluster grid, so a rocket interior or a
    /// distant planetoid still reports small, meaningful numbers.
    /// </para>
    /// </summary>
    internal sealed class MatchLocation {
        internal MatchLocationSource Source;

        internal TargetKind Kind;

        internal Color Swatch;

        /// <summary>Display name shown on the row, e.g. "Copper Ore" or "Storage Bin".</summary>
        internal string Name;

        /// <summary>
        /// The cell the camera moves to when the row is clicked. For an area this is the
        /// centre of its bounding box; for an item it is the cell it stood on when the search
        /// resolved (the game is paused by then, so it has not moved since).
        /// </summary>
        internal int Cell;

        /// <summary>
        /// The live object an item row points at, or null for an area. Kept so a click can
        /// read its current cell if anything managed to move it anyway, and so a destroyed
        /// object can be skipped.
        /// </summary>
        internal GameObject Target;

        /// <summary>Matching cells of an area, or null for a single item.</summary>
        internal int[] Cells;

        /// <summary>
        /// Mass of the entry in kilograms: the sum of every matching cell's mass for an area,
        /// or the item's own <c>PrimaryElement</c> mass. Zero for things without one, such as
        /// critters and duplicants.
        /// </summary>
        internal float Mass;

        /// <summary>World-local bounds of an area. Unused for items.</summary>
        internal int MinX, MinY, MaxX, MaxY;

        /// <summary>World-local position of an item. Unused for areas.</summary>
        internal int LocalX, LocalY;

        /// <summary>
        /// Offset of the active world inside the cluster grid, kept per entry so an area can
        /// turn its cells into local coordinates when it is expanded, long after the scan.
        /// </summary>
        internal int WorldOffsetX, WorldOffsetY;

        internal bool IsArea => Cells != null;

        internal int TileCount => Cells?.Length ?? 0;

        /// <summary>
        /// Which tiles of an area are ticked in the position list, one entry per element of
        /// <see cref="Cells"/>. Null means every tile is ticked, which is the usual case: the
        /// array is only allocated once a single tile is unchecked, so a huge area costs
        /// nothing until the player edits it.
        /// </summary>
        internal bool[] CellSelected;

        /// <summary>How many of the area's tiles are currently ticked.</summary>
        internal int CellOnCount;

        /// <summary>Mass of the ticked tiles, shown in the area's own mass column.</summary>
        internal float SelectedMass;

        /// <summary>Turns a grid cell of this entry's world into a world-local coordinate.</summary>
        internal void ToLocal(int cell, out int x, out int y) {
            Grid.CellToXY(cell, out int gx, out int gy);
            x = gx - WorldOffsetX;
            y = gy - WorldOffsetY;
        }
    }

    /// <summary>
    /// Turns the current match set into the positions shown in the legend. Runs once per
    /// pinned selection, on the main thread, and only reads what the overlay already resolved:
    /// the same cells the overlay colours, the same entities it lights up.
    /// </summary>
    internal static class MatchLocationCollector {
        /// <summary>
        /// Builds every position for <paramref name="matches"/>. Tile areas come first,
        /// largest first, then backwall areas, then items sorted by name and position. Entries
        /// cover the active world only and skip anything still hidden by fog of war, exactly
        /// like the highlight itself.
        /// </summary>
        internal static List<MatchLocation> Build(SearchMatches matches, SearchEntry entry) {
            var result = new List<MatchLocation>();
            var world = ClusterManager.Instance?.activeWorld;
            if (matches == null || matches.IsEmpty || world == null || !Grid.IsValidCell(0))
                return result;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int tileCells = CollectElementAreas(matches, entry, world, result);
            int backwallCells = CollectBackwallAreas(matches, entry, world, result);
            int items = CollectItems(matches, entry, world, result);
            watch.Stop();
            Log.Debug("MatchLocations: {0} tiles in {1} areas, {2} backwall cells, {3} items in {4}ms".
                F(tileCells, CountSource(result, MatchLocationSource.Tile), backwallCells, items,
                    watch.ElapsedMilliseconds));
            return result;
        }

        private static int CountSource(List<MatchLocation> locations,
                MatchLocationSource source) {
            int count = 0;
            for (int i = 0; i < locations.Count; i++)
                if (locations[i].Source == source)
                    count++;
            return count;
        }

        /// <summary>
        /// Walks every valid grid cell of <paramref name="world"/> exactly once. World offsets
        /// can put a world anywhere in the cluster grid, so the rectangle is converted cell by
        /// cell rather than assuming the world starts at its offset's corner.
        /// </summary>
        private static void ForEachWorldCell(WorldContainer world, Action<int> action) {
            int offsetX = world.WorldOffset.x;
            int offsetY = world.WorldOffset.y;
            int width = world.Width;
            int height = world.Height;
            for (int y = 0; y < height; y++) {
                int gy = offsetY + y;
                if (gy < 0 || gy >= Grid.HeightInCells)
                    continue;
                for (int x = 0; x < width; x++) {
                    int gx = offsetX + x;
                    if (gx < 0 || gx >= Grid.WidthInCells)
                        continue;
                    int cell = Grid.XYToCell(gx, gy);
                    if (Grid.IsValidCell(cell))
                        action(cell);
                }
            }
        }

        /// <summary>
        /// Scans the active world for visible cells whose element matched, then groups them
        /// into 8-connected areas. Diagonal neighbours count as connected so a vein touching
        /// corner to corner is one row rather than two.
        /// </summary>
        private static int CollectElementAreas(SearchMatches matches, SearchEntry entry,
                WorldContainer world, List<MatchLocation> into) {
            if (matches.Elements.Count == 0)
                return 0;
            var remaining = new HashSet<int>();
            // Bounded to the active asteroid's own rectangle rather than the whole cluster
            // grid: a cluster holds several worlds and most of the grid is empty there, so
            // walking it would cost several times more for the same cells.
            ForEachWorldCell(world, cell => {
                if (Grid.Visible[cell] <= 20)
                    return;
                var element = Grid.Element[cell];
                if (element != null && matches.Elements.Contains(element.id))
                    remaining.Add(cell);
            });
            int cells = remaining.Count;
            FloodFill(remaining, entry, world, MatchLocationSource.Tile, into);
            return cells;
        }

        /// <summary>
        /// Groups the matching natural backwall cells the overlay already resolved. That
        /// snapshot is built with the same rules the overlay colours by (active world,
        /// revealed, solid backwall in the match set), so the list and the map never disagree.
        /// </summary>
        private static int CollectBackwallAreas(SearchMatches matches, SearchEntry entry,
                WorldContainer world, List<MatchLocation> into) {
            var backwall = MaterialSearchOverlay.BackwallSnapshot;
            if (backwall == null || backwall.Length == 0)
                return 0;
            var remaining = new HashSet<int>();
            ForEachWorldCell(world, cell => {
                if ((uint)cell < (uint)backwall.Length && backwall[cell] != default)
                    remaining.Add(cell);
            });
            int count = remaining.Count;
            FloodFill(remaining, entry, world, MatchLocationSource.Backwall, into);
            return count;
        }

        /// <summary>
        /// Walks the entities the overlay lit up. Everything is filtered to the active world
        /// and revealed cells again, because the element collectors light up loose ore and
        /// buildings regardless of where they are.
        /// </summary>
        private static int CollectItems(SearchMatches matches, SearchEntry entry,
                WorldContainer world, List<MatchLocation> into) {
            var targets = MaterialSearchOverlay.Instance?.Targets;
            if (targets == null || targets.Count == 0)
                return 0;
            // Containers listed under Contents are there for what they hold, so the item
            // itself must not be listed a second time as its own entity.
            var containers = CollectContainers(targets);
            var items = new List<MatchLocation>();
            var seen = new HashSet<GameObject>();
            for (int i = 0; i < targets.Count; i++) {
                var target = targets[i];
                if (target == null)
                    continue;
                var go = target.gameObject;
                if (go == null || !seen.Add(go))
                    continue;
                SearchHighlighter.TryGetTargetInfo(go, out var kind, out var source);
                if (IsInListedContainer(go, containers))
                    continue;
                int cell = Grid.PosToCell(target);
                if (!Grid.IsValidCell(cell) || !Grid.IsActiveWorld(cell) ||
                        Grid.Visible[cell] <= 20)
                    continue;
                var selectable = go.GetComponent<KSelectable>();
                string name = selectable != null ? selectable.GetName() : null;
                if (string.IsNullOrEmpty(name))
                    name = entry?.DisplayName ?? go.name;
                var location = new MatchLocation {
                    Source = ToLocationSource(source),
                    Kind = kind,
                    Swatch = SwatchFor(go, kind, entry),
                    Name = name,
                    Cell = cell,
                    Target = go,
                    // A Contents row points at the storage building that stands in for its
                    // stored items, so its mass has to be the mass of what is inside. Using
                    // the building's own construction mass would report every container as
                    // heavy as the box, not as heavy as the food.
                    Mass = source == HighlightSource.Container ?
                        StoredMass(go, kind, matches) : PrimaryMass(go),
                    WorldOffsetX = world.WorldOffset.x,
                    WorldOffsetY = world.WorldOffset.y
                };
                location.ToLocal(cell, out location.LocalX, out location.LocalY);
                items.Add(location);
            }
            // Readable order: what it is first, then what it came from, then top to bottom.
            items.Sort(CompareItems);
            into.AddRange(items);
            return items.Count;
        }

        private static MatchLocationSource ToLocationSource(HighlightSource source) {
            switch (source) {
            case HighlightSource.Debris: return MatchLocationSource.Debris;
            case HighlightSource.Building: return MatchLocationSource.Building;
            case HighlightSource.Container: return MatchLocationSource.Container;
            default: return MatchLocationSource.Entity;
            }
        }

        /// <summary>
        /// The colour of an item's swatch: the colour the overlay already paints that object
        /// in, so a row and its highlight always agree. Falls back to the element's own colour
        /// and then to the pinned row's colour for anything not currently highlighted.
        /// </summary>
        private static Color SwatchFor(GameObject go, TargetKind kind, SearchEntry entry) {
            var overlay = MaterialSearchOverlay.Instance;
            if (overlay != null && overlay.TryGetTargetColor(go, out var painted))
                return painted;
            var pe = go.GetComponent<PrimaryElement>();
            if (pe != null && MaterialSearchOverlay.ElementColors != null &&
                    MaterialSearchOverlay.ElementColors.ContainsKey(pe.ElementID))
                return MaterialSearchOverlay.ElementSwatchFor(pe.ElementID);
            return entry?.Swatch ?? SearchIndex.SwatchFor(kind);
        }

        /// <summary>
        /// The mass an item contributes to its row and to the section total. Critters and
        /// duplicants carry no <c>PrimaryElement</c> and report zero, which makes the caller
        /// omit the mass rather than print a misleading 0 kg.
        /// </summary>
        private static float PrimaryMass(GameObject go) {
            var pe = go.GetComponent<PrimaryElement>();
            return pe != null ? pe.Mass : 0f;
        }

        /// <summary>
        /// The storage buildings the highlighter lit up because of what they hold. These are
        /// the rows the Contents section is built from.
        /// </summary>
        private static HashSet<GameObject> CollectContainers(List<KMonoBehaviour> targets) {
            var containers = new HashSet<GameObject>();
            for (int i = 0; i < targets.Count; i++) {
                var target = targets[i];
                var go = target != null ? target.gameObject : null;
                if (go == null)
                    continue;
                SearchHighlighter.TryGetTargetInfo(go, out _, out var source);
                if (source == HighlightSource.Container)
                    containers.Add(go);
            }
            return containers;
        }

        /// <summary>
        /// Whether an object is sitting inside one of the containers listed under Contents.
        /// A stored Nutrient Bar is already described by its Ration Box's row, so listing it
        /// again as a free entity would duplicate it and its mass in the legend.
        /// </summary>
        private static bool IsInListedContainer(GameObject go,
                HashSet<GameObject> containers) {
            if (containers.Count == 0)
                return false;
            var pickupable = go.GetComponent<Pickupable>();
            var storage = pickupable != null ? pickupable.storage : null;
            return storage != null && containers.Contains(storage.gameObject);
        }

        /// <summary>
        /// Mass of the matching items a storage building holds. A Contents row points at the
        /// container because the stored item itself is hidden inside it, but the row is about
        /// the contents: counting the container's own construction mass would make a Ration
        /// Box full of Nutrient Bars report the box. Only items that the search actually
        /// matched are counted, so unrelated cargo in the same storage stays out.
        /// </summary>
        private static float StoredMass(GameObject building, TargetKind kind,
                SearchMatches matches) {
            if (!(building.GetComponent<IStorage>() is IStorage storage))
                return 0f;
            var items = storage.GetItems();
            if (items == null)
                return 0f;
            float mass = 0f;
            for (int i = 0; i < items.Count; i++) {
                var item = items[i];
                if (item == null || !StoredKindMatches(item, kind, matches))
                    continue;
                mass += PrimaryMass(item);
            }
            return mass;
        }

        /// <summary>
        /// Whether one stored object is of the kind the container row stands for. Mirrors the
        /// highlighter's own prefab matching, including the two kinds that are not reached
        /// through <see cref="SearchMatches.PrefabKind"/>: boosters, which match by tag set,
        /// and artifacts, which have no tag of their own.
        /// </summary>
        private static bool StoredKindMatches(GameObject item, TargetKind kind,
                SearchMatches matches) {
            var kPrefabID = item.GetComponent<KPrefabID>();
            if (kPrefabID == null)
                return false;
            var itemKind = matches.PrefabKind(kPrefabID.PrefabTag);
            if (itemKind != null)
                return itemKind == kind;
            switch (kind) {
            case TargetKind.Booster:
                return matches.Boosters.Contains(kPrefabID.PrefabTag);
            case TargetKind.Artifact:
                return kPrefabID.HasTag(GameTags.Artifact) ||
                    kPrefabID.HasTag(GameTags.Keepsake);
            default:
                return false;
            }
        }

        private static int CompareItems(MatchLocation a, MatchLocation b) {
            int order = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            if (order != 0)
                return order;
            order = a.Source.CompareTo(b.Source);
            if (order != 0)
                return order;
            order = b.LocalY.CompareTo(a.LocalY);
            return order != 0 ? order : a.LocalX.CompareTo(b.LocalX);
        }

        /// <summary>
        /// Mass of one matching cell, in kilograms. Natural tiles carry it in the simulated
        /// grid, natural backwall in its own native array; both are the same unit the mass
        /// labels use, so rows can print a cell's share of its area's total directly.
        /// </summary>
        internal static float CellMass(MatchLocationSource source, int cell) {
            return source == MatchLocationSource.Backwall ?
                BackwallManager.At(cell).Mass : Grid.Mass[cell];
        }

        /// <summary>
        /// Splits <paramref name="remaining"/> into 8-connected areas and appends one entry per
        /// area. The set is consumed as cells are claimed, so no separate visited set is
        /// needed and the cost stays proportional to the number of matching cells.
        /// </summary>
        private static void FloodFill(HashSet<int> remaining, SearchEntry entry,
                WorldContainer world, MatchLocationSource source,
                List<MatchLocation> into) {
            if (remaining.Count == 0)
                return;
            var queue = new Queue<int>();
            var area = new List<int>();
            while (remaining.Count > 0) {
                int start = 0;
                foreach (int cell in remaining) {
                    start = cell;
                    break;
                }
                remaining.Remove(start);
                area.Clear();
                queue.Clear();
                queue.Enqueue(start);
                while (queue.Count > 0) {
                    int cell = queue.Dequeue();
                    area.Add(cell);
                    Grid.CellToXY(cell, out int x, out int y);
                    for (int dy = -1; dy <= 1; dy++) {
                        for (int dx = -1; dx <= 1; dx++) {
                            if (dx == 0 && dy == 0)
                                continue;
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= Grid.WidthInCells ||
                                    ny >= Grid.HeightInCells)
                                continue;
                            int neighbour = Grid.XYToCell(nx, ny);
                            if (remaining.Remove(neighbour))
                                queue.Enqueue(neighbour);
                        }
                    }
                }
                into.Add(CreateArea(area, entry, world, source));
            }
            // Biggest first: the vein worth visiting should not be buried under slivers.
            int first = into.Count - CountSource(into, source);
            if (into.Count - first > 1)
                into.Sort(first, into.Count - first, AreaComparer.Instance);
        }

        private static MatchLocation CreateArea(List<int> cells, SearchEntry entry,
                WorldContainer world, MatchLocationSource source) {
            int minGX = int.MaxValue, minGY = int.MaxValue;
            int maxGX = int.MinValue, maxGY = int.MinValue;
            float mass = 0f;
            for (int i = 0; i < cells.Count; i++) {
                int cell = cells[i];
                Grid.CellToXY(cell, out int x, out int y);
                if (x < minGX) minGX = x;
                if (x > maxGX) maxGX = x;
                if (y < minGY) minGY = y;
                if (y > maxGY) maxGY = y;
                // Backwall has its own mass array; natural tiles use the simulated cell mass.
                // Both are kilograms, the unit the mass label above the list already uses.
                mass += CellMass(source, cell);
            }
            int offsetX = world.WorldOffset.x;
            int offsetY = world.WorldOffset.y;
            int centre = Grid.XYToCell((minGX + maxGX) / 2, (minGY + maxGY) / 2);
            return new MatchLocation {
                Source = source,
                Kind = TargetKind.Material,
                Swatch = entry?.Swatch ?? SearchIndex.SwatchFor(TargetKind.Material),
                Name = entry?.DisplayName ?? "Material",
                Cell = Grid.IsValidCell(centre) ? centre : cells[0],
                Cells = cells.ToArray(),
                Mass = mass,
                MinX = minGX - offsetX,
                MaxX = maxGX - offsetX,
                MinY = minGY - offsetY,
                MaxY = maxGY - offsetY,
                WorldOffsetX = offsetX,
                WorldOffsetY = offsetY
            };
        }

        private sealed class AreaComparer : IComparer<MatchLocation> {
            internal static readonly AreaComparer Instance = new AreaComparer();

            public int Compare(MatchLocation a, MatchLocation b) {
                int order = b.TileCount.CompareTo(a.TileCount);
                if (order != 0)
                    return order;
                order = a.MinY.CompareTo(b.MinY);
                return order != 0 ? order : a.MinX.CompareTo(b.MinX);
            }
        }

        /// <summary>
        /// Moves the camera to <paramref name="cell"/> without changing the zoom level, and
        /// makes sure the game is still paused before it gets there.
        /// </summary>
        internal static void PanToCell(int cell) {
            var camera = CameraController.Instance;
            if (camera == null || !Grid.IsValidCell(cell))
                return;
            ForcedPause.Guard();
            // The current size, not the target, so a click during a zoom in progress keeps
            // whatever zoom the player is looking at instead of finishing the animation.
            camera.SetTargetPos(Grid.CellToPosCCC(cell, Grid.SceneLayer.Move),
                camera.OrthographicSize, true);
            // SetTargetPos can unpause as a side effect of the cinema camera's
            // "unpause on next move" flag, so the guard is re-run straight after it.
            ForcedPause.Guard();
            Log.Debug("PanToCell: cell={0}".F(cell));
        }

        // ── The game's own info panel ──────────────────────────────────────────────────────────────────
        // A row that stands for exactly one thing - one cell, one item - can be clicked
        // to make the game really select it: the same selection the player gets by
        // clicking that thing with the mouse, so the panel the game opens is the real
        // one, and its actions work on the real thing. Cells are selected through the
        // game's own cell proxies, which report the cell they stand on: a proxy has to
        // be parked on the cell before it is selected, because selecting it is what
        // makes it keep that spot instead of following the mouse.
        internal static GameObject panelTarget;

        internal static void OpenCellPanel(int cell, MatchLocationSource source) {
            if (!Grid.IsValidCell(cell) || SelectTool.Instance == null)
                return;
            var proxy = source == MatchLocationSource.Backwall ?
                BackwallSelectionObject.Instance?.gameObject : FindTileProxy();
            if (proxy == null) {
                Log.Debug("OpenCellPanel diag: cell={0} source={1} proxy=none (nothing to select)".
                    F(cell, source));
                return;
            }
            var before = SelectTool.Instance.selected;
            if (source != MatchLocationSource.Backwall && before != null &&
                    before.GetComponent<BackwallSelectionObject>() != null) {
                // The backwall proxy is the one the game would pick for a cell that also has a
                // backwall; a tile row must take the panel back onto the tile, so the backwall
                // never keeps it.
                Log.Debug("OpenCellPanel diag: tile row while the backwall proxy was selected");
            }
            var sel = proxy.GetComponent<KSelectable>();
            if (sel == null) {
                Log.Debug("OpenCellPanel diag: proxy={0} has no selectable".F(proxy.name));
                return;
            }
            // Park it on the cell before selecting it: the proxy reads the cell out of
            // its own position, and the selection message makes it adopt that spot.
            var centre = Grid.CellToPosCCC(cell, Grid.SceneLayer.Move);
            var transform = proxy.transform;
            transform.position = new Vector3(centre.x, centre.y, transform.position.z);
            var current = SelectTool.Instance.selected;
            // Rows that stand for one thing use the game's own selection, and with it the
            // game's own click sound. Asking for the selection that is already in place
            // changes nothing, so that one is refreshed - and clicked - by hand instead.
            if (current != sel)
                SelectTool.Instance.Select(sel);
            if (SelectTool.Instance.selected != sel) {
                Log.Debug("OpenCellPanel diag: cell={0} source={1} selection failed".F(cell, source));
                return;
            }
            panelTarget = proxy;
            if (current == sel) {
                if (DetailsScreen.Instance != null)
                    DetailsScreen.Instance.Refresh(proxy);
                Patches.PlaySelectClick(SelectTool.Instance.GetComponent<SelectToolHoverTextCard>());
            }
            var coords = Grid.CellToXY(cell);
            Log.Debug("OpenCellPanel diag: cell={0} ({1},{2}) source={3} tile={4} backwall={5} | ".
                F(cell, coords.x, coords.y, source, MaterialSearchOverlay.ElementName(
                    Grid.Element[cell]), BackwallManager.HasBackwall(cell) ?
                        MaterialSearchOverlay.ElementName(BackwallManager.At(cell).Element) :
                        "none") +
                "proxy={0} proxyName='{1}' selectedBefore={2} selectedAfter={3} panelTarget={4}".F(
                    proxy.name, sel.GetName(), before == null ? "null" : before.name,
                    SelectTool.Instance.selected == null ? "null" :
                        SelectTool.Instance.selected.name, panelTarget.name));
        }

        internal static void OpenItemPanel(GameObject target) {
            if (target == null || SelectTool.Instance == null)
                return;
            // A destroyed object has no components left on it, which is what the null
            // selectability reports: the row is stale, so panning alone is enough.
            var sel = target.GetComponent<KSelectable>();
            if (sel == null) {
                Log.Debug("OpenItemPanel diag: {0} has no selectable (stale row)".F(target.name));
                return;
            }
            var current = SelectTool.Instance.selected;
            if (current != sel)
                SelectTool.Instance.Select(sel);
            if (SelectTool.Instance.selected == sel) {
                panelTarget = target;
                if (current == sel)
                    Patches.PlaySelectClick(
                        SelectTool.Instance.GetComponent<SelectToolHoverTextCard>());
                Log.Debug("OpenItemPanel diag: {0} selectedBefore={1} selectedAfter={2}".F(
                    target.name, current == null ? "null" : current.name,
                    SelectTool.Instance.selected == null ? "null" :
                        SelectTool.Instance.selected.name));
            } else {
                Log.Debug("OpenItemPanel diag: {0} selection failed".F(target.name));
            }
        }

        // Closes the panel the last row click opened, and nothing else's: a selection
        // the player made themselves is left alone, so switching the overlay off never
        // steals it. The panel is only ever closed while it still stands for our target.
        internal static void CloseInfoPanel() {
            if (panelTarget != null && SelectTool.Instance != null && DetailsScreen.Instance != null &&
                    SelectTool.Instance.selected?.gameObject == panelTarget) {
                DetailsScreen.Instance.DeselectAndClose();
                Log.Debug("CloseInfoPanel: closed the panel for {0}".F(panelTarget.name));
            }
            panelTarget = null;
        }

        /// <summary>
        /// The tile-cell rows go through one of the game's two cell proxies, which it keeps
        /// as an alternating pair so the two kinds of cell click never collide: the one that
        /// is not already the selection is used, since asking for the selection that is
        /// already in place changes nothing.
        /// </summary>
        private static GameObject FindTileProxy() {
            var go = GameObject.Find("WorldSelectionCollider");
            if (go == null)
                return null;
            var proxy = go.GetComponent<CellSelectionObject>();
            if (proxy == null)
                return null;
            if (SelectTool.Instance?.selected == proxy.Selectable) {
                var other = proxy.alternateSelectionObject;
                return other != null ? other.gameObject : null;
            }
            return go;
        }
    }
}
