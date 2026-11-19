using PeterHan.PLib.Core;
using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using MSearch = BobbyModding.MaterialSearchOverlay.MaterialSearchOverlayStrings.UI.OVERLAYS.MATERIALSEARCH;

namespace BobbyModding.MaterialSearchOverlay {
    /// <summary>
    /// The position list in the overlay legend: one section per kind of place the search
    /// found, one row per connected area or item, and every row's check box decides whether
    /// that place stays lit on the map.
    /// <para>
    /// Built once when a dropdown result is pinned and never refreshed afterwards. The game is
    /// paused from that moment on, so the snapshot cannot go stale while it is on screen. It
    /// is deliberately not a map: the overlay already is one, and a list answers "where" with
    /// coordinates the player can reason about.
    /// </para>
    /// <para>
    /// Each section has its own scroll pane, capped by the player's
    /// <c>Maximum Location Rows</c> option, so a big vein cannot push the legend off screen.
    /// Section headers and groups collapse, areas expand into their own tiles, and the check
    /// boxes filter the map highlight through <see cref="LocationSelection"/>.
    /// </para>
    /// </summary>
    internal static class MatchLocationUI {
        /// <summary>Height of one list row, in pixels.</summary>
        private const float ROW_HEIGHT = 18.0f;

        /// <summary>Height of a section header, which also carries the scroll percentage.</summary>
        private const float HEADER_HEIGHT = 19.0f;

        /// <summary>Horizontal indent applied per nesting level.</summary>
        private const int INDENT = 14;

        /// <summary>Width of the triangle and check box controls.</summary>
        private const float CONTROL_WIDTH = 16.0f;

        /// <summary>Width of the scroll percentage on a section header.</summary>
        private const float PERCENT_WIDTH = 36.0f;

        /// <summary>Breathing room between a value column and the column before it.</summary>
        private const float NUMBER_GAP = 4.0f;

        /// <summary>Width a value column keeps even when every value in it is blank.</summary>
        private const float NUMBER_MIN_WIDTH = 26.0f;

        /// <summary>
        /// Tiles built when an area is expanded. A large vein can hold thousands, and a row
        /// per cell would cost more than the paused game should spend; the rest are reported
        /// with a truncated row rather than silently dropped.
        /// </summary>
        private const int MAX_AREA_ROWS = 500;

        /// <summary>Instances built inside one expanded group, capped for the same reason.</summary>
        private const int MAX_GROUP_ROWS = 500;

        /// <summary>Rows built in one section, so six open sections cannot stall the game.</summary>
        private const int MAX_SECTION_ROWS = 500;

        /// <summary>Rows each section shows at once when the option has never been applied.</summary>
        private const int DEFAULT_VISIBLE_ROWS = 5;

        /// <summary>Smallest and largest value the options page accepts for that option.</summary>
        internal const int MIN_VISIBLE_ROWS = 2;
        internal const int MAX_VISIBLE_ROWS = 12;

        private static readonly Color ROW_COLOR = new Color(0f, 0f, 0f, 0.01f);

        private static readonly Color ROW_HOVER_COLOR = new Color(1f, 1f, 1f, 0.13f);

        private static readonly Color HEADER_ROW_COLOR = new Color(1f, 1f, 1f, 0.07f);

        private static readonly Color DIVIDER_COLOR = new Color(1f, 1f, 1f, 0.14f);

        private static readonly Color HEADER_COLOR = new Color(0.82f, 0.82f, 0.82f);

        private static readonly Color LABEL_COLOR = new Color(0.92f, 0.92f, 0.92f);

        private static readonly Color CONTROL_COLOR = new Color(0.86f, 0.86f, 0.86f, 0.95f);

        private static readonly Color DIM_COLOR = new Color(1f, 1f, 1f, 0.45f);

        /// <summary>The skeleton of the whole list, one child per section.</summary>
        private static GameObject panel;

        private static Transform rootParent;

        private static OverlayLegend legend;

        private static MethodInfo configureHeight;

        /// <summary>Every entry of the pinned selection, in collector order.</summary>
        private static readonly List<MatchLocation> leaves = new List<MatchLocation>();

        /// <summary>The section models built from <see cref="leaves"/>.</summary>
        private static readonly List<LocationSection> sections = new List<LocationSection>();

        /// <summary>Entries whose check box is ticked, i.e. still highlighted on the map.</summary>
        private static readonly HashSet<MatchLocation> selected = new HashSet<MatchLocation>();

        /// <summary>Areas the player has expanded into their tiles.</summary>
        private static readonly HashSet<MatchLocation> expandedAreas =
            new HashSet<MatchLocation>();

        /// <summary>Groups the player has folded away. Groups start expanded.</summary>
        private static readonly HashSet<string> collapsedGroups = new HashSet<string>();

        /// <summary>Sections the player has folded away. Sections start expanded.</summary>
        private static readonly HashSet<MatchLocationSource> collapsedSections =
            new HashSet<MatchLocationSource>();

        /// <summary>Check boxes currently on screen, so a tick can repaint without a rebuild.</summary>
        private static readonly List<CheckWidget> checkWidgets = new List<CheckWidget>();

        /// <summary>Mass and count columns on screen, repainted in place on every tick.</summary>
        private static readonly List<NumberWidget> numberWidgets = new List<NumberWidget>();

        /// <summary>Scroll state of each section of the current build.</summary>
        private static readonly List<SectionView> views = new List<SectionView>();

        /// <summary>Where each section was scrolled to, in pixels, across rebuilds.</summary>
        private static readonly Dictionary<MatchLocationSource, float> scrollOffsets =
            new Dictionary<MatchLocationSource, float>();

        /// <summary>How many rows each section shows before it scrolls, from the options page.</summary>
        private static int visibleRows = DEFAULT_VISIBLE_ROWS;

        private static Sprite triangleSprite, boxSprite, solidSprite;

        /// <summary>
        /// Creates the empty list inside a freshly spawned legend. Called once per legend
        /// instance; a legend rebuilt for a new world arrives here again with its old list
        /// already destroyed.
        /// </summary>
        internal static void Init(OverlayLegend instance) {
            legend = instance;
            if (instance == null || panel != null)
                return;
            // A plain vertical panel; the legend's own layout gives it its width, and each
            // section adds a fixed header plus its own scroll pane below.
            var root = new PPanel("LocationList") {
                Direction = PanelDirection.Vertical,
                FlexSize = new Vector2(1f, 0f),
                Spacing = 0
            }.AddOnRealize(obj => {
                rootParent = obj.transform;
                Log.Debug("MatchLocationUI: list root realized");
            });
            var go = root.Build();
            go.transform.SetParent(instance.transform, false);
            go.SetActive(false);
            panel = go;
            Log.Debug("MatchLocationUI: list built");
        }

        /// <summary>
        /// Applies the <c>Maximum Location Rows</c> option. Safe to call before the legend has
        /// built its list, and resizes the open sections when the player changes the option.
        /// </summary>
        internal static void SetVisibleRows(int rows) {
            int clamped = Mathf.Clamp(rows, MIN_VISIBLE_ROWS, MAX_VISIBLE_ROWS);
            if (clamped == visibleRows)
                return;
            visibleRows = clamped;
            if (panel == null || !panel.activeSelf)
                return;
            Rebuild(resetScroll: false);
            Log.Debug("MatchLocationUI: rows per section set to {0}".F(visibleRows));
        }

        /// <summary>
        /// Shows the positions from one pinned selection. Replaces whatever the previous
        /// selection showed and resets every expansion, collapse, scroll and check box.
        /// </summary>
        internal static void Show(List<MatchLocation> found) {
            expandedAreas.Clear();
            collapsedGroups.Clear();
            collapsedSections.Clear();
            scrollOffsets.Clear();
            selected.Clear();
            leaves.Clear();
            if (found != null && found.Count > 0) {
                for (int i = 0; i < found.Count; i++) {
                    var location = found[i];
                    if (location == null)
                        continue;
                    leaves.Add(location);
                    // Everything starts selected: the map highlight is the search itself
                    // until the player starts unticking rows.
                    if (location.IsArea) {
                        location.CellSelected = null;
                        location.CellOnCount = location.TileCount;
                        location.SelectedMass = location.Mass;
                    }
                    selected.Add(location);
                }
            }
            BuildSections();
            RecalculateSelectedTotals();
            if (panel == null) {
                Log.Debug("MatchLocationUI: Show skipped, no list yet");
                return;
            }
            panel.SetActive(true);
            LocationSelection.Clear();
            Rebuild(resetScroll: true);
            Log.Debug("MatchLocationUI: showing {0} entries in {1} sections".F(leaves.Count,
                sections.Count));
        }

        /// <summary>
        /// Empties and hides the list, e.g. once the player starts typing again or the overlay
        /// closes. The selection filter is dropped with it, so a stale mask can never hide
        /// cells of a later search.
        /// </summary>
        internal static void Hide() {
            bool active = panel != null && panel.activeSelf;
            if (leaves.Count == 0 && sections.Count == 0 && !active) {
                LocationSelection.Clear();
                return;
            }
            leaves.Clear();
            sections.Clear();
            selected.Clear();
            expandedAreas.Clear();
            collapsedGroups.Clear();
            collapsedSections.Clear();
            scrollOffsets.Clear();
            views.Clear();
            checkWidgets.Clear();
            numberWidgets.Clear();
            if (panel != null) {
                ClearRoot();
                panel.SetActive(false);
            }
            LocationSelection.Clear();
            RefreshLegendHeight();
            Log.Debug("MatchLocationUI: hidden");
        }

        /// <summary>
        /// Rebuilds every section header and body. <paramref name="resetScroll"/> puts every
        /// section back at the top, which is what a fresh selection wants; expanding a row
        /// keeps the player where they were reading instead.
        /// </summary>
        private static void Rebuild(bool resetScroll) {
            if (rootParent == null)
                return;
            SaveScrollOffsets();
            ClearRoot();
            views.Clear();
            checkWidgets.Clear();
            numberWidgets.Clear();
            if (sections.Count == 0)
                AddTextRow(rootParent, (string) MSearch.LOCATION_EMPTY, 0, HEADER_COLOR);
            else
                for (int i = 0; i < sections.Count; i++)
                    AddSectionUI(sections[i], i == 0);
            // Column widths are measured from the text, so every row lines up no matter how
            // long the numbers are in this particular search.
            SizeNumberColumns();
            RefreshLegendHeight();
            RestoreScrollOffsets(resetScroll);
        }

        private static void ClearRoot() {
            if (rootParent == null)
                return;
            for (int i = rootParent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(rootParent.GetChild(i).gameObject);
        }

        private static void SaveScrollOffsets() {
            for (int i = 0; i < views.Count; i++) {
                var view = views[i];
                if (view.Scroll != null && view.Scroll.content != null)
                    scrollOffsets[view.Source] = view.Scroll.content.anchoredPosition.y;
            }
        }

        private static void RestoreScrollOffsets(bool reset) {
            for (int i = 0; i < views.Count; i++) {
                var view = views[i];
                var scroll = view.Scroll;
                if (scroll == null || scroll.content == null)
                    continue;
                // The sections have just been rebuilt, so let their content settle at its new
                // height before an old pixel offset is clamped against it.
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                if (reset) {
                    scroll.verticalNormalizedPosition = 1f;
                } else if (scrollOffsets.TryGetValue(view.Source, out float offset)) {
                    scroll.content.anchoredPosition = new Vector2(
                        scroll.content.anchoredPosition.x, offset);
                    scroll.verticalNormalizedPosition = Mathf.Clamp01(
                        scroll.verticalNormalizedPosition);
                }
                UpdatePercent(view);
            }
        }

        /// <summary>Builds the six section models from the pinned entries.</summary>
        private static void BuildSections() {
            sections.Clear();
            AddSection(MatchLocationSource.Tile, MSearch.LOCATION_TILE_HEADER, false);
            AddSection(MatchLocationSource.Backwall, MSearch.LOCATION_BACKWALL_HEADER, false);
            AddSection(MatchLocationSource.Debris, MSearch.LOCATION_DEBRIS_HEADER, false);
            AddSection(MatchLocationSource.Building, MSearch.LOCATION_BUILDING_HEADER, true);
            AddSection(MatchLocationSource.Container, MSearch.LOCATION_CONTENTS_HEADER, true);
            AddSection(MatchLocationSource.Entity, MSearch.LOCATION_ENTITIES_HEADER, true);
        }

        /// <summary>
        /// Fills one section from the entries of <paramref name="source"/>. Flat sections list
        /// their areas or items directly; grouped sections merge same-named entries into a
        /// group row that expands to the instances underneath.
        /// </summary>
        private static void AddSection(MatchLocationSource source, LocString title,
                bool grouped) {
            var section = new LocationSection {
                Source = source,
                Title = title,
                Grouped = grouped
            };
            var groups = grouped ? new Dictionary<string, LocationGroup>(
                StringComparer.OrdinalIgnoreCase) : null;
            for (int i = 0; i < leaves.Count; i++) {
                var location = leaves[i];
                if (location.Source != source)
                    continue;
                section.Mass += location.Mass;
                if (!grouped) {
                    section.Leaves.Add(location);
                    // An area is worth its tiles; an item row is worth one item, and has no
                    // cells to count.
                    section.ItemCount += location.IsArea ? location.TileCount : 1;
                    continue;
                }
                section.ItemCount++;
                string key = source + "|" + location.Name;
                if (!groups.TryGetValue(key, out var group)) {
                    group = new LocationGroup {
                        Key = key,
                        Name = location.Name
                    };
                    groups.Add(key, group);
                    section.Groups.Add(group);
                }
                group.Items.Add(location);
                group.Mass += location.Mass;
            }
            if (section.Leaves.Count == 0 && section.Groups.Count == 0)
                return;
            // The header counts what its own rows show: areas or items for a flat section,
            // distinct names for a grouped one, exactly like the pinned dropdown counts.
            section.Count = grouped ? section.Groups.Count : section.Leaves.Count;
            sections.Add(section);
        }

        /// <summary>
        /// Adds one section to the list: its header, then its scrollable body. The body is a
        /// real scroll pane of its own, so a long section scrolls without moving the others.
        /// </summary>
        private static void AddSectionUI(LocationSection section, bool first) {
            if (!first)
                AddDivider();
            bool collapsed = collapsedSections.Contains(section.Source);
            var header = NewPlainRow(rootParent, "Header_" + section.Source, HEADER_HEIGHT,
                HEADER_ROW_COLOR, 0);
            AddArrow(header, !collapsed, () => ToggleSection(section.Source), HEADER_HEIGHT);
            AddCheckbox(header, HEADER_HEIGHT, () => SectionState(section),
                () => ToggleSectionSelection(section));
            string text = string.Format(section.Title, section.Count);
            AddLabel(header, text, 12, HEADER_COLOR, 1, HEADER_HEIGHT, true);
            AddMassColumn(header, HEADER_HEIGHT, HEADER_COLOR, () => section.SelectedMass,
                () => section.Mass);
            AddCountColumn(header, HEADER_HEIGHT, HEADER_COLOR,
                () => section.SelectedCount, () => section.ItemCount);
            // Left in place even for a folded section, and simply left blank: the label is
            // what keeps this header's numbers above the rows' numbers instead of a scroll
            // percentage's width to their right.
            var percent = AddPercentLabel(header, HEADER_HEIGHT);
            if (collapsed)
                return;
            // The rows scroll together, so the pane measures its content and clips it.
            var rows = new PPanel("SectionRows_" + section.Source) {
                Direction = PanelDirection.Vertical,
                FlexSize = new Vector2(1f, 0f),
                Spacing = 1
            };
            var builder = new PScrollPane("SectionScroll_" + section.Source) {
                Child = rows,
                ScrollVertical = true,
                ScrollHorizontal = false,
                AlwaysShowVertical = true,
                FlexSize = new Vector2(1f, 0f)
            };
            var pane = builder.Build();
            pane.transform.SetParent(rootParent, false);
            // A transparent Image across the pane keeps the mouse wheel working over the
            // gaps between rows; without a raycast target there, the wheel falls through to
            // the legend and zooms the camera instead.
            var hit = pane.GetComponent<Image>();
            if (hit == null)
                hit = pane.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            // The pane owns its height. A LayoutElement wins over the scroll pane's own
            // layout group by priority, so the rows no longer pass their full height up to
            // the legend and the section scrolls instead of growing the window.
            var paneLayout = pane.GetComponent<LayoutElement>();
            if (paneLayout == null)
                paneLayout = pane.AddComponent<LayoutElement>();
            paneLayout.layoutPriority = 2;
            paneLayout.flexibleWidth = 1f;
            paneLayout.flexibleHeight = 0f;
            var scroll = pane.GetComponent<ScrollRect>();
            var view = new SectionView {
                Source = section.Source,
                Scroll = scroll,
                Percent = percent
            };
            views.Add(view);
            int added = 0;
            if (scroll != null && scroll.content != null)
                BuildSectionRows(section, scroll.content, ref added);
            float body = 2f + Mathf.Max(1, Mathf.Min(added, visibleRows)) *
                (ROW_HEIGHT + 1f);
            paneLayout.minHeight = body;
            paneLayout.preferredHeight = body;
            if (scroll != null)
                scroll.onValueChanged.AddListener(_ => UpdatePercent(view));
            UpdatePercent(view);
        }

        private static void BuildSectionRows(LocationSection section, Transform parent,
                ref int added) {
            int total = CountSectionRows(section);
            if (section.Grouped) {
                for (int i = 0; i < section.Groups.Count && added < MAX_SECTION_ROWS; i++) {
                    var group = section.Groups[i];
                    AddGroupRow(group, parent);
                    added++;
                    if (collapsedGroups.Contains(group.Key))
                        continue;
                    int shown = Mathf.Min(group.Items.Count, MAX_GROUP_ROWS);
                    for (int j = 0; j < shown && added < MAX_SECTION_ROWS; j++) {
                        AddItemRow(group.Items[j], parent, 2);
                        added++;
                    }
                    if (group.Items.Count > shown && added < MAX_SECTION_ROWS) {
                        AddTruncatedRow(parent, group.Items.Count - shown, 2);
                        added++;
                    }
                }
            } else {
                for (int i = 0; i < section.Leaves.Count && added < MAX_SECTION_ROWS; i++) {
                    var leaf = section.Leaves[i];
                    if (leaf.IsArea)
                        AddAreaRow(leaf, parent, ref added);
                    else {
                        AddItemRow(leaf, parent, 1);
                        added++;
                    }
                }
            }
            if (total > added) {
                AddTruncatedRow(parent, total - added, 1);
                added++;
            }
        }

        /// <summary>Rows <see cref="BuildSectionRows"/> will add, including caps and headers.</summary>
        private static int CountSectionRows(LocationSection section) {
            int total = 0;
            if (section.Grouped) {
                for (int i = 0; i < section.Groups.Count; i++) {
                    var group = section.Groups[i];
                    total++;
                    if (collapsedGroups.Contains(group.Key))
                        continue;
                    int shown = Mathf.Min(group.Items.Count, MAX_GROUP_ROWS);
                    total += shown;
                    if (group.Items.Count > shown)
                        total++;
                }
            } else {
                for (int i = 0; i < section.Leaves.Count; i++) {
                    var leaf = section.Leaves[i];
                    total++;
                    if (leaf.IsArea && expandedAreas.Contains(leaf)) {
                        int shown = Mathf.Min(leaf.TileCount, MAX_AREA_ROWS);
                        total += shown;
                        if (leaf.TileCount > shown)
                            total++;
                    }
                }
            }
            return total;
        }

        /// <summary>
        /// One area: its bounding box, mass and tile count, a toggle that unfolds its tiles,
        /// and a tri-state check box for the whole area. Clicking the row pans to it.
        /// </summary>
        private static void AddAreaRow(MatchLocation location, Transform parent,
                ref int added) {
            bool isExpanded = expandedAreas.Contains(location);
            var row = NewPlainRow(parent, "Area", ROW_HEIGHT, ROW_COLOR, 1);
            AddArrow(row, isExpanded, () => ToggleArea(location), ROW_HEIGHT);
            AddCheckbox(row, ROW_HEIGHT, () => AreaState(location),
                () => ToggleAreaSelection(location));
            string text = string.Format(MSearch.LOCATION_AREA_BOUNDS, location.MinX,
                location.MaxX, location.MinY, location.MaxY);
            AddLabel(row, text, 11, LABEL_COLOR, 1, ROW_HEIGHT);
            AddMassColumn(row, ROW_HEIGHT, LABEL_COLOR,
                () => location.SelectedMass, () => location.Mass);
            AddCountColumn(row, ROW_HEIGHT, LABEL_COLOR,
                () => location.CellOnCount, () => location.TileCount);
            AddTailSpacer(row, ROW_HEIGHT);
            AddClick(row, location.Cell);
            added++;
            if (!isExpanded)
                return;
            var cells = location.Cells;
            if (cells == null)
                return;
            int shown = Mathf.Min(cells.Length, MAX_AREA_ROWS);
            for (int i = 0; i < shown && added < MAX_SECTION_ROWS; i++) {
                // Captured per row: the loop variable is one shared variable, so every tile's
                // check box would otherwise act on the last tile drawn.
                int index = i;
                int cell = cells[i];
                location.ToLocal(cell, out int cx, out int cy);
                var cellRow = NewPlainRow(parent, "Tile", ROW_HEIGHT, ROW_COLOR, 2);
                AddCheckbox(cellRow, ROW_HEIGHT, () => CellState(location, index),
                    () => ToggleCellSelection(location, index));
                AddLabel(cellRow, string.Format(MSearch.LOCATION_CELL_ROW, cx, cy), 10,
                    DIM_COLOR, 1, ROW_HEIGHT);
                // A tile's own share of its area's mass, so one coordinate reports the mass
                // that will come out of it rather than only the area total.
                float cellMass = MatchLocationCollector.CellMass(location.Source, cell);
                AddValueColumn(cellRow, true, ROW_HEIGHT, DIM_COLOR,
                    () => LeafMass(cellMass));
                AddValueColumn(cellRow, false, ROW_HEIGHT, DIM_COLOR,
                    () => (string) MSearch.LOCATION_TILE_SINGLE);
                AddTailSpacer(cellRow, ROW_HEIGHT);
                AddClickCellPanel(cellRow, cell, location.Source);
                added++;
            }
            if (cells.Length > shown && added < MAX_SECTION_ROWS) {
                AddTruncatedRow(parent, cells.Length - shown, 2);
                added++;
            }
        }

        /// <summary>One named group, e.g. "Mesh Tile (12)", which expands to its instances.</summary>
        private static void AddGroupRow(LocationGroup group, Transform parent) {
            bool isExpanded = !collapsedGroups.Contains(group.Key);
            var row = NewPlainRow(parent, "Group", ROW_HEIGHT, ROW_COLOR, 1);
            AddArrow(row, isExpanded, () => ToggleGroup(group.Key), ROW_HEIGHT);
            AddCheckbox(row, ROW_HEIGHT, () => GroupState(group),
                () => ToggleGroupSelection(group));
            string text = string.Format(MSearch.LOCATION_GROUP_ROW, group.Name,
                group.Items.Count);
            AddLabel(row, text, 12, LABEL_COLOR, 1, ROW_HEIGHT);
            AddMassColumn(row, ROW_HEIGHT, LABEL_COLOR, () => group.SelectedMass,
                () => group.Mass);
            AddCountColumn(row, ROW_HEIGHT, LABEL_COLOR, () => group.SelectedCount,
                () => group.Items.Count);
            AddTailSpacer(row, ROW_HEIGHT);
            if (group.Items.Count > 0)
                AddClick(row, group.Items[0].Cell);
        }

        /// <summary>One matched object: where it stands and what it weighs.</summary>
        private static void AddItemRow(MatchLocation location, Transform parent, int depth) {
            var row = NewPlainRow(parent, "Item", ROW_HEIGHT, ROW_COLOR, depth);
            AddCheckbox(row, ROW_HEIGHT, () => LeafState(location),
                () => ToggleLeafSelection(location));
            string text = string.Format(MSearch.LOCATION_ITEM_ROW, location.LocalX,
                location.LocalY);
            AddLabel(row, text, 11, LABEL_COLOR, 1, ROW_HEIGHT);
            AddValueColumn(row, true, ROW_HEIGHT, LABEL_COLOR,
                () => LeafMass(location.Mass));
            AddValueColumn(row, false, ROW_HEIGHT, LABEL_COLOR,
                () => (string) MSearch.LOCATION_ITEM_ONE);
            AddTailSpacer(row, ROW_HEIGHT);
            AddClickItemPanel(row, location.Cell, location.Target);
        }

        /// <summary>A note that rows were left out, aligned with the rows it follows.</summary>
        private static void AddTruncatedRow(Transform parent, int remaining, int depth) {
            AddTextRow(parent, string.Format(MSearch.LOCATION_TRUNCATED, remaining), depth,
                DIM_COLOR);
        }

        /// <summary>A row with no check box, only a spacer where one would be.</summary>
        private static void AddTextRow(Transform parent, string text, int depth,
                Color color) {
            var row = NewPlainRow(parent, "Note", ROW_HEIGHT, ROW_COLOR, depth);
            AddControlSpacer(row, ROW_HEIGHT);
            AddLabel(row, text, 10, color, 1, ROW_HEIGHT);
            AddTailSpacer(row, ROW_HEIGHT);
        }

        private static void AddDivider() {
            if (rootParent == null)
                return;
            var row = new GameObject("Divider");
            row.transform.SetParent(rootParent, false);
            var image = row.AddComponent<Image>();
            image.color = DIVIDER_COLOR;
            image.raycastTarget = false;
            var element = row.AddComponent<LayoutElement>();
            element.minHeight = 1f;
            element.preferredHeight = 1f;
            element.flexibleWidth = 1f;
        }

        private static void ToggleArea(MatchLocation location) {
            if (!expandedAreas.Remove(location))
                expandedAreas.Add(location);
            Rebuild(resetScroll: false);
        }

        private static void ToggleGroup(string key) {
            if (!collapsedGroups.Remove(key))
                collapsedGroups.Add(key);
            Rebuild(resetScroll: false);
        }

        private static void ToggleSection(MatchLocationSource section) {
            if (!collapsedSections.Remove(section))
                collapsedSections.Add(section);
            Rebuild(resetScroll: false);
        }

        private static string FormatMass(float mass) {
            return GameUtil.GetFormattedMass(mass);
        }

        /// <summary>
        /// A row's mass for the mass column. Items and tiles always show their own value;
        /// aggregate rows show "selected / total" so an unticked part of a section is visible
        /// in the numbers as well as on the map. Blank when there is no mass to report at all,
        /// which keeps a section of critters from claiming "0 kg".
        /// </summary>
        /// <remarks>
        /// Both halves use the unit the total would pick on its own. Formatting them
        /// independently would print "0 kg / 5.4 t" for an unticked area, and the mixed pair
        /// would also be wider than the "total / total" the column is measured against.
        /// </remarks>
        private static string MassPair(float selectedMass, float totalMass) {
            if (totalMass <= 0f)
                return "";
            var format = totalMass >= 5000f ? GameUtil.MetricMassFormat.Tonne :
                GameUtil.MetricMassFormat.Kilogram;
            return string.Format(MSearch.LOCATION_PAIR,
                GameUtil.GetFormattedMass(selectedMass, GameUtil.TimeSlice.None, format),
                GameUtil.GetFormattedMass(totalMass, GameUtil.TimeSlice.None, format));
        }

        /// <summary>A row's tile or item count as "selected / total".</summary>
        private static string CountPair(int selectedCount, int totalCount) {
            return string.Format(MSearch.LOCATION_PAIR, selectedCount, totalCount);
        }

        /// <summary>The mass of one item or one tile, or blank when it has none.</summary>
        private static string LeafMass(float mass) {
            return mass > 0f ? FormatMass(mass) : "";
        }

        // ---------------------------------------------------------------- selection

        private enum CheckState {
            None,
            Partial,
            All
        }

        /// <summary>
        /// A check box on screen. It keeps the live state as a delegate rather than a copy,
        /// so unticking one leaf only has to re-run the delegates of the visible boxes
        /// instead of rebuilding the list and losing every scroll position.
        /// </summary>
        private sealed class CheckWidget {
            internal Image Fill;

            internal Func<CheckState> State;

            internal void Refresh() {
                var state = State != null ? State() : CheckState.None;
                bool active = state != CheckState.None;
                var fill = Fill;
                if (fill == null || fill.gameObject == null)
                    return;
                fill.gameObject.SetActive(active);
                if (active)
                    fill.rectTransform.sizeDelta = state == CheckState.All ?
                        new Vector2(9f, 9f) : new Vector2(4f, 4f);
            }
        }

        /// <summary>
        /// A mass or count column on screen. Like <see cref="CheckWidget"/> it keeps the live
        /// value as a delegate, so a tick only repaints the visible numbers instead of
        /// rebuilding the list and losing every scroll position.
        /// </summary>
        private sealed class NumberWidget {
            internal TextMeshProUGUI Label;

            internal LayoutElement Element;

            /// <summary>Whether this label belongs to the mass column rather than the count one.</summary>
            internal bool Mass;

            /// <summary>What the label shows right now.</summary>
            internal Func<string> Text;

            /// <summary>
            /// The widest text this label can ever show, so the column can be sized once even
            /// when the current selection makes every number shorter.
            /// </summary>
            internal Func<string> Widest;

            internal void Refresh() {
                var label = Label;
                if (label == null)
                    return;
                var text = Text != null ? Text() : null;
                label.text = text ?? "";
            }
        }

        private static CheckState ToState(int total, int selectedCount) {
            if (total <= 0 || selectedCount <= 0)
                return CheckState.None;
            return selectedCount >= total ? CheckState.All : CheckState.Partial;
        }

        private static CheckState SectionState(LocationSection section) {
            int total = 0, count = 0;
            if (section.Grouped) {
                for (int i = 0; i < section.Groups.Count; i++) {
                    var items = section.Groups[i].Items;
                    total += items.Count;
                    for (int j = 0; j < items.Count; j++)
                        if (selected.Contains(items[j]))
                            count++;
                }
            } else {
                // A flat section stands for individual tiles when its rows are areas, and for
                // individual items when they are debris or critters, so a single unchecked
                // tile in a huge area still reads as a partial section.
                for (int i = 0; i < section.Leaves.Count; i++) {
                    var leaf = section.Leaves[i];
                    if (leaf.IsArea) {
                        total += leaf.TileCount;
                        count += selected.Contains(leaf) ? leaf.CellOnCount : 0;
                    } else {
                        total++;
                        if (selected.Contains(leaf))
                            count++;
                    }
                }
            }
            return ToState(total, count);
        }

        private static CheckState GroupState(LocationGroup group) {
            int total = group.Items.Count, count = 0;
            for (int i = 0; i < total; i++)
                if (selected.Contains(group.Items[i]))
                    count++;
            return ToState(total, count);
        }

        private static CheckState LeafState(MatchLocation leaf) {
            return selected.Contains(leaf) ? CheckState.All : CheckState.None;
        }

        /// <summary>
        /// An area's box reflects its tiles, so unchecking one coordinate turns the area into
        /// a partial tick rather than leaving it looking fully selected.
        /// </summary>
        private static CheckState AreaState(MatchLocation area) {
            if (!selected.Contains(area))
                return CheckState.None;
            return area.CellOnCount >= area.TileCount ? CheckState.All : CheckState.Partial;
        }

        private static CheckState CellState(MatchLocation area, int index) {
            return IsCellSelected(area, index) ? CheckState.All : CheckState.None;
        }

        /// <summary>Whether one specific tile of an area is ticked right now.</summary>
        private static bool IsCellSelected(MatchLocation area, int index) {
            if (!selected.Contains(area))
                return false;
            var cells = area.CellSelected;
            return cells == null || cells[index];
        }

        /// <summary>
        /// Ticks or unticks every leaf of a section. A partially selected section selects
        /// the rest, which is what a click on a tri-state box is expected to do.
        /// </summary>
        private static void ToggleSectionSelection(LocationSection section) {
            bool on = SectionState(section) != CheckState.All;
            if (section.Grouped) {
                for (int i = 0; i < section.Groups.Count; i++) {
                    var items = section.Groups[i].Items;
                    for (int j = 0; j < items.Count; j++)
                        SetLeafSelected(items[j], on);
                }
            } else {
                for (int i = 0; i < section.Leaves.Count; i++)
                    SetLeafSelected(section.Leaves[i], on);
            }
            CommitSelection();
        }

        private static void ToggleGroupSelection(LocationGroup group) {
            bool on = GroupState(group) != CheckState.All;
            for (int i = 0; i < group.Items.Count; i++)
                SetLeafSelected(group.Items[i], on);
            CommitSelection();
        }

        /// <summary>
        /// Ticks or unticks one area. A partially ticked area selects the rest instead of
        /// dropping the tiles the player already chose.
        /// </summary>
        private static void ToggleAreaSelection(MatchLocation area) {
            SetAreaSelected(area, AreaState(area) != CheckState.All);
            CommitSelection();
        }

        /// <summary>
        /// Ticks or unticks one coordinate in an expanded area. Unticking the last tile of an
        /// area clears the area's own box as well.
        /// </summary>
        private static void ToggleCellSelection(MatchLocation area, int index) {
            SetCellSelected(area, index, !IsCellSelected(area, index));
            CommitSelection();
        }

        private static void ToggleLeafSelection(MatchLocation leaf) {
            SetLeafSelected(leaf, !selected.Contains(leaf));
            CommitSelection();
        }

        private static void SetLeafSelected(MatchLocation leaf, bool on) {
            if (leaf.IsArea) {
                SetAreaSelected(leaf, on);
                return;
            }
            if (on)
                selected.Add(leaf);
            else
                selected.Remove(leaf);
        }

        /// <summary>Ticks or unticks an entire area, tile by tile.</summary>
        private static void SetAreaSelected(MatchLocation area, bool on) {
            if (on) {
                selected.Add(area);
                area.CellSelected = null;
                area.CellOnCount = area.TileCount;
                area.SelectedMass = area.Mass;
            } else {
                selected.Remove(area);
                area.CellSelected = null;
                area.CellOnCount = 0;
                area.SelectedMass = 0f;
            }
        }

        /// <summary>
        /// Ticks or unticks one tile of an area, keeping the area's tile count and mass in
        /// step with the boxes. Ticking a tile of an area that was fully off leaves only that
        /// tile on, which is the natural reading of clicking one coordinate.
        /// </summary>
        private static void SetCellSelected(MatchLocation area, int index, bool on) {
            int cell = area.Cells[index];
            float cellMass = MatchLocationCollector.CellMass(area.Source, cell);
            if (!selected.Contains(area)) {
                if (!on)
                    return;
                // The area was fully off: start it over with this one tile ticked.
                var cells = new bool[area.TileCount];
                cells[index] = true;
                selected.Add(area);
                area.CellSelected = cells;
                area.CellOnCount = 1;
                area.SelectedMass = cellMass;
                return;
            }
            var mask = area.CellSelected;
            if (mask == null) {
                if (!on) {
                    if (area.TileCount <= 1) {
                        SetAreaSelected(area, false);
                        return;
                    }
                    // First edit of a fully ticked area: remember every tile, then drop one.
                    mask = new bool[area.TileCount];
                    for (int i = 0; i < mask.Length; i++)
                        mask[i] = true;
                    mask[index] = false;
                    area.CellSelected = mask;
                    area.CellOnCount = area.TileCount - 1;
                    area.SelectedMass = area.Mass - cellMass;
                }
                return;
            }
            if (mask[index] == on)
                return;
            mask[index] = on;
            if (on) {
                area.CellOnCount++;
                area.SelectedMass += cellMass;
                // Everything is ticked again: drop the array so the area is cheap again.
                if (area.CellOnCount >= area.TileCount) {
                    area.CellSelected = null;
                    area.SelectedMass = area.Mass;
                }
            } else {
                area.CellOnCount--;
                area.SelectedMass -= cellMass;
                if (area.CellOnCount <= 0)
                    SetAreaSelected(area, false);
            }
        }

        /// <summary>
        /// Publishes the ticked rows to the map and refreshes the columns in place. Structural
        /// changes are not needed: the rows, their text and their totals are the same, only
        /// the highlight and the selected halves of the values change.
        /// </summary>
        private static void CommitSelection() {
            ApplySelection();
            RecalculateSelectedTotals();
            for (int i = 0; i < checkWidgets.Count; i++)
                checkWidgets[i].Refresh();
            for (int i = 0; i < numberWidgets.Count; i++)
                numberWidgets[i].Refresh();
        }

        /// <summary>
        /// Re-tallies the selected half of every section and group column. Runs after each
        /// tick, before the columns repaint, so the numbers always describe the same selection
        /// the map is painting.
        /// </summary>
        private static void RecalculateSelectedTotals() {
            for (int i = 0; i < sections.Count; i++) {
                var section = sections[i];
                float mass = 0f;
                int count = 0;
                if (section.Grouped) {
                    for (int g = 0; g < section.Groups.Count; g++) {
                        var group = section.Groups[g];
                        float groupMass = 0f;
                        int groupCount = 0;
                        for (int j = 0; j < group.Items.Count; j++) {
                            var item = group.Items[j];
                            if (!selected.Contains(item))
                                continue;
                            groupMass += item.Mass;
                            groupCount++;
                        }
                        group.SelectedMass = groupMass;
                        group.SelectedCount = groupCount;
                        mass += groupMass;
                        count += groupCount;
                    }
                } else {
                    for (int j = 0; j < section.Leaves.Count; j++) {
                        var leaf = section.Leaves[j];
                        if (!selected.Contains(leaf))
                            continue;
                        if (leaf.IsArea) {
                            // An area reports only its ticked tiles, which may be fewer than
                            // the whole area after unchecking one coordinate.
                            mass += leaf.SelectedMass;
                            count += leaf.CellOnCount;
                        } else {
                            mass += leaf.Mass;
                            count++;
                        }
                    }
                }
                section.SelectedMass = mass;
                section.SelectedCount = count;
            }
        }

        /// <summary>
        /// Turns the ticked leaves into the masks and the hidden set the overlay paints
        /// through. A null part means "nothing unticked there", which keeps the normal
        /// highlight path cheap.
        /// </summary>
        private static void ApplySelection() {
            bool hideTiles = false, hideBackwalls = false;
            var hidden = new HashSet<GameObject>();
            for (int i = 0; i < leaves.Count; i++) {
                var leaf = leaves[i];
                switch (leaf.Source) {
                case MatchLocationSource.Tile:
                    // A mask is needed as soon as one tile is missing, even when no area row
                    // is fully unchecked.
                    if (!IsAreaFullySelected(leaf))
                        hideTiles = true;
                    break;
                case MatchLocationSource.Backwall:
                    if (!IsAreaFullySelected(leaf))
                        hideBackwalls = true;
                    break;
                default:
                    // Areas have no object; every item row does, and the overlay light
                    // follows that object rather than its cell.
                    if (!selected.Contains(leaf) && leaf.Target != null)
                        hidden.Add(leaf.Target);
                    break;
                }
            }
            bool[] tileMask = hideTiles ? BuildTileMask(MatchLocationSource.Tile) : null;
            bool[] backwallMask = hideBackwalls ?
                BuildTileMask(MatchLocationSource.Backwall) : null;
            LocationSelection.Apply(tileMask, backwallMask,
                hidden.Count > 0 ? hidden : null);
        }

        private static bool IsAreaFullySelected(MatchLocation area) {
            return selected.Contains(area) && area.CellOnCount >= area.TileCount;
        }

        private static bool[] BuildTileMask(MatchLocationSource source) {
            var mask = new bool[Grid.CellCount];
            for (int i = 0; i < leaves.Count; i++) {
                var leaf = leaves[i];
                if (leaf.Source != source || !leaf.IsArea || !selected.Contains(leaf))
                    continue;
                var cells = leaf.Cells;
                var cellSelected = leaf.CellSelected;
                for (int j = 0; j < cells.Length; j++) {
                    // Null means the whole area is ticked; otherwise only the ticked tiles
                    // stay lit.
                    if (cellSelected != null && !cellSelected[j])
                        continue;
                    int cell = cells[j];
                    if ((uint) cell < (uint) mask.Length)
                        mask[cell] = true;
                }
            }
            return mask;
        }

        // ---------------------------------------------------------------- widgets

        private static GameObject NewPlainRow(Transform parent, string name, float height,
                Color background, int depth) {
            var row = new GameObject(name);
            row.transform.SetParent(parent, false);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 3;
            layout.padding = new RectOffset(4 + depth * INDENT, 4, 0, 0);
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var element = row.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleWidth = 1;
            var bg = row.AddComponent<Image>();
            bg.color = background;
            // Hover and click are handled by a tiny component rather than an EventTrigger:
            // EventTrigger implements IScrollHandler, and would swallow the mouse wheel over
            // every row before the section's own scroll pane ever saw it.
            var handler = row.AddComponent<LocationRowEvents>();
            handler.Background = bg;
            handler.NormalColor = background;
            handler.HoverColor = ROW_HOVER_COLOR;
            return row;
        }

        private static TextMeshProUGUI AddLabel(GameObject row, string text,
                float fontSize, Color color, float flexWidth, float height,
                bool bold = false) {
            var obj = new GameObject("Label");
            obj.transform.SetParent(row.transform, false);
            var label = obj.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Left;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.raycastTarget = false;
            // Rows have a fixed height, so a long name must shorten rather than wrap into
            // the row below it.
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            var element = obj.AddComponent<LayoutElement>();
            element.flexibleWidth = flexWidth;
            element.minHeight = height - 2;
            return label;
        }

        private static TextMeshProUGUI AddPercentLabel(GameObject row, float height) {
            var obj = new GameObject("ScrollPercent");
            obj.transform.SetParent(row.transform, false);
            var label = obj.AddComponent<TextMeshProUGUI>();
            label.fontSize = 10;
            label.color = DIM_COLOR;
            label.alignment = TextAlignmentOptions.Right;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var element = obj.AddComponent<LayoutElement>();
            element.preferredWidth = PERCENT_WIDTH;
            element.minHeight = height - 2;
            return label;
        }

        private static void AddControlSpacer(GameObject row, float height) {
            var obj = new GameObject("Spacer");
            obj.transform.SetParent(row.transform, false);
            var element = obj.AddComponent<LayoutElement>();
            element.preferredWidth = CONTROL_WIDTH;
            element.preferredHeight = height;
        }

        /// <summary>
        /// The empty strip a body row keeps at its far right, the width of the section
        /// header's scroll percentage. Without it the values in a body row would run one
        /// percentage wider than the header's values, and the section's own scrollbar would
        /// sit on top of the last column instead of in the strip beside it.
        /// </summary>
        private static void AddTailSpacer(GameObject row, float height) {
            var obj = new GameObject("Tail");
            obj.transform.SetParent(row.transform, false);
            var element = obj.AddComponent<LayoutElement>();
            element.preferredWidth = PERCENT_WIDTH;
            element.preferredHeight = height;
        }

        // ---------------------------------------------------------------- columns

        /// <summary>
        /// A mass column: "selected / total" for a section, group or area, and the single
        /// value for an item or one tile.
        /// </summary>
        private static void AddMassColumn(GameObject row, float height, Color color,
                Func<float> selectedMass, Func<float> totalMass) {
            AddNumberColumn(row, true, height, color,
                () => MassPair(selectedMass(), totalMass()),
                () => MassPair(totalMass(), totalMass()));
        }

        /// <summary>A count column: "selected / total" tiles or items.</summary>
        private static void AddCountColumn(GameObject row, float height, Color color,
                Func<int> selectedCount, Func<int> totalCount) {
            AddNumberColumn(row, false, height, color,
                () => CountPair(selectedCount(), totalCount()),
                () => CountPair(totalCount(), totalCount()));
        }

        /// <summary>
        /// A column that never shows a selection split, for a row that is itself one item: its
        /// own mass or the "1 tile" note on an expanded tile.
        /// </summary>
        private static void AddValueColumn(GameObject row, bool massColumn, float height,
                Color color, Func<string> text) {
            AddNumberColumn(row, massColumn, height, color, text, text);
        }

        private static void AddNumberColumn(GameObject row, bool massColumn, float height,
                Color color, Func<string> text, Func<string> widest) {
            var obj = new GameObject(massColumn ? "Mass" : "Count");
            obj.transform.SetParent(row.transform, false);
            var label = obj.AddComponent<TextMeshProUGUI>();
            label.fontSize = 10;
            label.color = color;
            label.alignment = TextAlignmentOptions.Right;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            // Sized by SizeNumberColumns once every row of the build exists; the ellipsis is
            // only a safety net for a number that somehow grew after being measured.
            label.overflowMode = TextOverflowModes.Ellipsis;
            var element = obj.AddComponent<LayoutElement>();
            element.minHeight = height - 2;
            var widget = new NumberWidget {
                Label = label,
                Element = element,
                Mass = massColumn,
                Text = text,
                Widest = widest ?? text
            };
            widget.Refresh();
            numberWidgets.Add(widget);
        }

        /// <summary>
        /// Gives both value columns one width for the whole list, measured from the widest text
        /// each label can show. Measuring beats a hard coded width here: the columns stay put
        /// across rows and sections, and a search that finds tonnes rather than kilograms does
        /// not turn every number into an ellipsis.
        /// </summary>
        private static void SizeNumberColumns() {
            float mass = NUMBER_MIN_WIDTH, count = NUMBER_MIN_WIDTH;
            for (int i = 0; i < numberWidgets.Count; i++) {
                var widget = numberWidgets[i];
                var label = widget.Label;
                if (label == null || widget.Element == null)
                    continue;
                float width = 0f;
                try {
                    string text = widget.Widest != null ? widget.Widest() : label.text;
                    width = label.GetPreferredValues(text ?? "", 0f, 0f).x;
                } catch (Exception e) {
                    Log.Debug("MatchLocationUI: column measure failed: {0}".F(e));
                }
                if (widget.Mass)
                    mass = Mathf.Max(mass, width);
                else
                    count = Mathf.Max(count, width);
            }
            for (int i = 0; i < numberWidgets.Count; i++) {
                var widget = numberWidgets[i];
                if (widget.Element == null)
                    continue;
                widget.Element.preferredWidth =
                    (widget.Mass ? mass : count) + NUMBER_GAP;
            }
        }

        /// <summary>
        /// The expand/collapse triangle of a section, group or area. It has its own Image and
        /// trigger so the click lands on the control and not on the row's pan handler: the
        /// event system stops at the closest ancestor that handles the click.
        /// </summary>
        private static void AddArrow(GameObject row, bool expanded, System.Action onClick,
                float height) {
            var obj = NewControl(row, "Arrow", height);
            var triangle = NewControlImage(obj.transform, "Triangle", TriangleSprite(),
                HEADER_COLOR, 9f, 9f);
            triangle.rectTransform.localRotation = Quaternion.Euler(0f, 0f,
                expanded ? -90f : 0f);
            AddButtonClick(obj, onClick);
        }

        /// <summary>
        /// The tri-state check box. The outer box is a hollow square, the inner fill is either
        /// a full square (everything under it selected) or a dot (some of it selected).
        /// </summary>
        private static void AddCheckbox(GameObject row, float height,
                Func<CheckState> state, System.Action onClick) {
            var obj = NewControl(row, "Check", height);
            NewControlImage(obj.transform, "Box", BoxSprite(), CONTROL_COLOR, 13f, 13f);
            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(obj.transform, false);
            var fill = fillObj.AddComponent<Image>();
            fill.sprite = SolidSprite();
            fill.color = CONTROL_COLOR;
            fill.raycastTarget = false;
            var rect = fillObj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(9f, 9f);
            AddButtonClick(obj, onClick);
            var widget = new CheckWidget {
                Fill = fill,
                State = state
            };
            checkWidgets.Add(widget);
            widget.Refresh();
        }

        /// <summary>
        /// The clickable background of a control. Fully transparent, but still a raycast
        /// target so the click is stopped here instead of falling through to the row.
        /// </summary>
        private static GameObject NewControl(GameObject row, string name, float height) {
            var obj = new GameObject(name);
            obj.transform.SetParent(row.transform, false);
            var image = obj.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
            var element = obj.AddComponent<LayoutElement>();
            element.preferredWidth = CONTROL_WIDTH;
            element.preferredHeight = height;
            return obj;
        }

        /// <summary>
        /// An Image sized in the middle of its parent. Controls are direct children of a row
        /// layout group, so anything drawn inside them is positioned by hand rather than by
        /// the group.
        /// </summary>
        private static Image NewControlImage(Transform parent, string name, Sprite sprite,
                Color color, float width, float height) {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
            return image;
        }

        /// <summary>A click on the row pans to its cell. Headers and notes never get one.</summary>
        private static void AddClick(GameObject row, int cell) {
            var handler = row.GetComponent<LocationRowEvents>();
            if (handler == null)
                handler = row.AddComponent<LocationRowEvents>();
            handler.OnClick = () => MatchLocationCollector.PanToCell(cell);
        }

        /// <summary>
        /// A row standing for exactly one cell: the click pans and also makes the game select
        /// that cell, which opens the game's own panel for it and lets its actions work on
        /// the real cell. A row standing for an area or a group has no one thing to select,
        /// so those only pan.
        /// <param name="source">Whether the cell is a tile or a natural backwall, which picks
        /// the kind of cell proxy the game selects through.</param>
        /// </summary>
        private static void AddClickCellPanel(GameObject row, int cell, MatchLocationSource source) {
            var handler = row.GetComponent<LocationRowEvents>();
            if (handler == null)
                handler = row.AddComponent<LocationRowEvents>();
            handler.OnClick = () => {
                MatchLocationCollector.PanToCell(cell);
                MatchLocationCollector.OpenCellPanel(cell, source);
            };
        }

        /// <summary>
        /// A row standing for exactly one object: the click pans and selects that object, so
        /// the game opens its own panel for it. A storage building's panel is its own, which
        /// lists what is inside it - what the row was built from.
        /// </summary>
        private static void AddClickItemPanel(GameObject row, int cell, GameObject target) {
            var handler = row.GetComponent<LocationRowEvents>();
            if (handler == null)
                handler = row.AddComponent<LocationRowEvents>();
            handler.OnClick = () => {
                MatchLocationCollector.PanToCell(cell);
                MatchLocationCollector.OpenItemPanel(target);
            };
        }

        private static void AddButtonClick(GameObject obj, System.Action onClick) {
            var handler = obj.GetComponent<LocationButtonEvents>();
            if (handler == null)
                handler = obj.AddComponent<LocationButtonEvents>();
            handler.OnClick = onClick;
        }

        /// <summary>
        /// Writes the section's scroll position on its header. 100% is the top, and it also
        /// reads 100% when the whole section fits and there is nothing to scroll.
        /// </summary>
        private static void UpdatePercent(SectionView view) {
            var label = view.Percent;
            var scroll = view.Scroll;
            if (label == null || scroll == null)
                return;
            float value = 1f;
            var content = scroll.content;
            var viewport = scroll.viewport;
            if (content != null && viewport != null &&
                    content.rect.height > viewport.rect.height + 0.5f) {
                value = scroll.verticalNormalizedPosition;
                if (float.IsNaN(value))
                    value = 1f;
            }
            label.text = string.Format(MSearch.LOCATION_SCROLL_PERCENT,
                Mathf.RoundToInt(Mathf.Clamp01(value) * 100f));
        }

        // ---------------------------------------------------------------- sprites

        private static Sprite TriangleSprite() {
            if (triangleSprite == null)
                triangleSprite = BuildTriangle();
            return triangleSprite;
        }

        private static Sprite BoxSprite() {
            if (boxSprite == null)
                boxSprite = BuildBox();
            return boxSprite;
        }

        private static Sprite SolidSprite() {
            if (solidSprite == null)
                solidSprite = BuildSolid();
            return solidSprite;
        }

        /// <summary>
        /// A small right-pointing triangle. Drawn rather than borrowed from the game font:
        /// the localized fonts are not guaranteed to carry arrow glyphs, and a missing glyph
        /// would show up as an empty box in the middle of every row.
        /// </summary>
        private static Sprite BuildTriangle() {
            const int size = 9;
            var tex = NewTexture(size);
            int centre = size / 2;
            for (int x = 0; x < size; x++) {
                int half = Mathf.RoundToInt(x * (size / 2f) / (size - 1));
                for (int y = centre - half; y <= centre + half; y++)
                    if (y >= 0 && y < size)
                        tex.SetPixel(x, y, Color.white);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f));
        }

        /// <summary>The hollow square of an unchecked box.</summary>
        private static Sprite BuildBox() {
            const int size = 13;
            var tex = NewTexture(size);
            for (int i = 0; i < size; i++) {
                tex.SetPixel(i, 0, Color.white);
                tex.SetPixel(i, size - 1, Color.white);
                tex.SetPixel(0, i, Color.white);
                tex.SetPixel(size - 1, i, Color.white);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f));
        }

        private static Sprite BuildSolid() {
            const int size = 4;
            var tex = NewTexture(size);
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    tex.SetPixel(x, y, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f));
        }

        private static Texture2D NewTexture(int size) {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(0f, 0f, 0f, 0f);
            tex.SetPixels(pixels);
            return tex;
        }

        // ---------------------------------------------------------------- layout

        /// <summary>
        /// Grows or shrinks the legend to fit the list. The game computes that height once
        /// when it hands the legend its contents and never again, so adding sections
        /// afterwards needs the same private step run again.
        /// </summary>
        private static void RefreshLegendHeight() {
            var instance = legend;
            if (instance == null)
                return;
            if (configureHeight == null)
                configureHeight = typeof(OverlayLegend).GetMethod("ConfigureUIHeight",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            if (configureHeight == null)
                return;
            try {
                configureHeight.Invoke(instance, null);
            } catch (Exception e) {
                Log.Debug("MatchLocationUI: ConfigureUIHeight failed: {0}".F(e));
            }
        }

        // ---------------------------------------------------------------- models

        /// <summary>One section of the list, as built from the pinned entries.</summary>
        private sealed class LocationSection {
            internal MatchLocationSource Source;

            internal LocString Title;

            internal bool Grouped;

            /// <summary>Areas or items of a flat section, in collector order.</summary>
            internal readonly List<MatchLocation> Leaves = new List<MatchLocation>();

            /// <summary>Same-named entries of a grouped section, in first-seen order.</summary>
            internal readonly List<LocationGroup> Groups = new List<LocationGroup>();

            internal float Mass;

            /// <summary>Number of areas (flat) or groups (grouped) the title counts.</summary>
            internal int Count;

            /// <summary>
            /// What the count column totals: tiles for a flat section, instances for a grouped
            /// one. Kept apart from <see cref="Count"/> because the title counts rows, not tiles.
            /// </summary>
            internal int ItemCount;

            /// <summary>Mass of the ticked rows, and of all rows, for the header's columns.</summary>
            internal float SelectedMass;

            internal int SelectedCount;
        }

        /// <summary>One display name inside a grouped section.</summary>
        private sealed class LocationGroup {
            internal string Key;

            internal string Name;

            internal readonly List<MatchLocation> Items = new List<MatchLocation>();

            internal float Mass;

            internal float SelectedMass;

            internal int SelectedCount;
        }

        /// <summary>The scroll state of one section of the current build.</summary>
        private sealed class SectionView {
            internal MatchLocationSource Source;

            internal ScrollRect Scroll;

            internal TextMeshProUGUI Percent;
        }
    }

    /// <summary>
    /// Hover and click handling for one list row. Deliberately not an <c>EventTrigger</c>:
    /// that implements <c>IScrollHandler</c>, so a mouse wheel over a row would be swallowed
    /// before the section's own scroll pane ever saw it. Only pointer enter, exit and click
    /// are claimed here, which leaves the wheel free to scroll the section.
    /// </summary>
    internal sealed class LocationRowEvents : MonoBehaviour, IPointerEnterHandler,
            IPointerExitHandler, IPointerClickHandler {
        internal Image Background;

        internal Color NormalColor;

        internal Color HoverColor;

        internal System.Action OnClick;

        public void OnPointerEnter(PointerEventData eventData) {
            if (Background != null)
                Background.color = HoverColor;
        }

        public void OnPointerExit(PointerEventData eventData) {
            if (Background != null)
                Background.color = NormalColor;
        }

        public void OnPointerClick(PointerEventData eventData) {
            if (eventData.button == PointerEventData.InputButton.Left && OnClick != null)
                OnClick();
        }
    }

    /// <summary>
    /// Click handling for the triangle and the check box of a row. The click stops here, so
    /// tick a box never pans the camera to the row underneath it.
    /// </summary>
    internal sealed class LocationButtonEvents : MonoBehaviour, IPointerClickHandler {
        internal System.Action OnClick;

        public void OnPointerClick(PointerEventData eventData) {
            if (eventData.button == PointerEventData.InputButton.Left && OnClick != null)
                OnClick();
        }
    }
}
