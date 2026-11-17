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

        /// <summary>
        /// Whether each dropdown row carries a badge naming its kind. Read from the options
        /// both at load and whenever the options page applies a change.
        /// </summary>
        internal static bool ShowTypeBadges { get; private set; } = true;

        /// <summary>
        /// Whether the overlay puts the keyboard in the search box as soon as it opens.
        /// </summary>
        internal static bool AutoFocusSearch { get; private set; } = true;

        /// <summary>
        /// Pushes a settings object into the statics the rest of the mod reads. Called once
        /// at load and again from <see cref="MaterialSearchOverlayOptions.OnOptionsChanged"/>
        /// so an option change needs no restart.
        /// </summary>
        internal static void ApplyOptions(MaterialSearchOverlayOptions options) {
            if (options == null)
                return;
            Log.DebugEnabled = options.EnableDebugLogging;
            ShowTypeBadges = options.ShowTypeBadges;
            SearchIndex.MaxResults = options.MaxSearchResults;
            AutoFocusSearch = options.AutoFocusSearch;
            Log.Debug("ApplyOptions: debug={0} badges={1} maxResults={2} autoFocus={3}".F(
                Log.DebugEnabled, ShowTypeBadges, SearchIndex.MaxResults, AutoFocusSearch));
        }

        /// <summary>Empties the search box, if the legend has built one yet.</summary>
        internal static void ClearSearchField() {
            OverlayLegend_OnSpawn_Patch.ClearSearchField();
        }

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
            // duplicants, boosters, critters, critter eggs, food, plants, industrial products
            // and artifacts from the same text box as the elements.
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
            ApplyOptions(POptions.ReadSettings<MaterialSearchOverlayOptions>() ??
                new MaterialSearchOverlayOptions());
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
            /// <summary>Background tint of a row nothing points at.</summary>
            private static readonly Color NORMAL_ROW_COLOR = new Color(0f, 0f, 0f, 0.01f);

            /// <summary>
            /// Background tint of the row the last pointer enter or arrow key landed on. It is
            /// also the row Enter would commit, so it is deliberately sticky: leaving the row
            /// with the mouse does not clear it.
            /// </summary>
            private static readonly Color HIGHLIGHTED_ROW_COLOR = new Color(1f, 1f, 1f,
                0.15f);

            /// <summary>
            /// One built dropdown row: the entry it stands for, and the background image whose
            /// colour shows whether it is the highlighted one. Rows are destroyed and rebuilt
            /// on every keystroke, so this holds no state beyond the current dropdown.
            /// </summary>
            private sealed class DropdownRow {
                internal SearchEntry Entry;

                internal Image Background;
            }

            /// <summary>The rows of the dropdown currently on screen, in display order.</summary>
            private static readonly List<DropdownRow> rows = new List<DropdownRow>();

            /// <summary>
            /// Index into <see cref="rows"/> of the row Enter would commit, or -1 while
            /// nothing has been stepped onto. Typing resets it, so Enter does nothing until an
            /// arrow key (or the mouse) picks a row.
            /// </summary>
            private static int highlightedRow = -1;

            /// <summary>
            /// How long an arrow has to stay held before the highlight starts stepping by
            /// itself, so a single tap moves exactly one row.
            /// </summary>
            private const float REPEAT_DELAY = 0.4f;

            /// <summary>Gap between the automatic steps once the delay has passed.</summary>
            private const float REPEAT_INTERVAL = 0.05f;

            /// <summary>1 while Down repeats, -1 while Up repeats, 0 while neither does.</summary>
            private static int heldDirection;

            /// <summary>Unscaled time at which the next automatic step is due.</summary>
            private static float nextRepeatTime;

            private static Transform DropdownItemsParent;

            /// <summary>
            /// The realized search box, or null before the legend has been built. Exposed to
            /// the overlay itself so closing can clear the query.
            /// </summary>
            internal static TMP_InputField SearchInput { get; private set; }

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
                // A respawned legend (new world) gets a fresh field and dropdown; any row
                // tracked from the previous one points at a destroyed object.
                rows.Clear();
                highlightedRow = -1;
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
                        // Enter commits the highlighted row exactly like a click does. TMP
                        // finishes the edit and deactivates the field right after this
                        // listener returns, so the dropdown is closed here rather than from
                        // any key handling of our own; nothing highlighted - the state after
                        // every keystroke - leaves the press unhandled.
                        input.onSubmit.AddListener(_ => {
                            if (highlightedRow < 0 || highlightedRow >= rows.Count)
                                return;
                            var entry = rows[highlightedRow].Entry;
                            Log.Debug("onSubmit: committing row {0} '{1}'".F(highlightedRow,
                                entry?.DisplayName ?? "null"));
                            if (entry != null)
                                OnResultSelected(entry);
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

            /// <summary>
            /// Puts the keyboard in the search box, so opening the overlay is enough to start
            /// typing. Must run after the legend is shown: TMP refuses to activate a field
            /// whose GameObject is inactive, and the legend is still hidden while the overlay's
            /// own <c>Enable</c> runs.
            /// </summary>
            internal static void FocusSearchField() {
                var input = SearchInput;
                if (input == null || !input.gameObject.activeInHierarchy) {
                    Log.Debug("FocusSearchField: skipped, box not active");
                    return;
                }
                Log.Debug("FocusSearchField: focusing '{0}'".F(input.name));
                // Select first so the EventSystem routes typing here, then let TMP take the
                // edit; TMP defers the actual activation to the next frame.
                input.Select();
                input.ActivateInputField();
                input.caretPosition = input.text.Length;
            }

            /// <summary>
            /// Empties the box without firing <c>onValueChanged</c>, so closing the overlay
            /// leaves nothing stale behind for the next open to show or to re-list.
            /// </summary>
            internal static void ClearSearchField() {
                var input = SearchInput;
                if (input != null && !string.IsNullOrEmpty(input.text))
                    input.SetTextWithoutNotify("");
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
                // The rows went with the children: a highlight left over from the previous
                // query must not be committed against a dropdown that no longer holds it.
                rows.Clear();
                highlightedRow = -1;
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
                    rows.Add(CreateDropdownRow(entry));
                CreateHintRow();
                Patches.DropdownContainer.SetActive(true);
                Log.Debug("PopulateDropdown EXIT: showing {0} rows".F(matches.Count));
            }

            /// <summary>
            /// Adds the "arrow keys and Enter" caption above the results. Built with the rows
            /// rather than once alongside the panel, because every populating pass destroys
            /// all children of the list before filling it again. It deliberately sits outside
            /// <see cref="rows"/>: only real results may be highlighted or committed.
            /// </summary>
            private static void CreateHintRow() {
                var hint = new GameObject("SelectionHint");
                hint.transform.SetParent(DropdownItemsParent, false);
                hint.transform.SetSiblingIndex(0);
                var text = hint.AddComponent<TextMeshProUGUI>();
                text.text = MSearch.SELECTION_HINT;
                text.fontSize = 11;
                // Dimmer than a result name, so it reads as a caption and not as a row that
                // can be picked.
                text.color = new Color(0.72f, 0.72f, 0.72f);
                text.alignment = TextAlignmentOptions.Left;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.raycastTarget = false;
                var layout = hint.AddComponent<LayoutElement>();
                layout.flexibleWidth = 1;
                layout.minHeight = 14;
            }

            private static DropdownRow CreateDropdownRow(SearchEntry entry) {
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
                bg.color = NORMAL_ROW_COLOR;
                var built = new DropdownRow {
                    Entry = entry,
                    Background = bg
                };
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
                    SetHighlight(rows.IndexOf(built));
                });
                trigger.triggers.Add(enter);
                // No pointer exit handler on purpose. The highlight marks the row Enter would
                // commit, so it stays where the pointer left it instead of going dark before
                // the user can press Enter.
                return built;
            }

            /// <summary>
            /// Moves the highlight onto one row, or clears it for -1, repainting the rows on
            /// the way. Pointer enter and the arrow keys share this one highlight, so there is
            /// never a hovered row and a keyboard row lit at the same time.
            /// </summary>
            private static void SetHighlight(int index) {
                highlightedRow = rows.Count == 0 ? -1 : Mathf.Clamp(index, -1,
                    rows.Count - 1);
                for (int i = 0; i < rows.Count; i++)
                    rows[i].Background.color = i == highlightedRow ? HIGHLIGHTED_ROW_COLOR :
                        NORMAL_ROW_COLOR;
            }

            /// <summary>
            /// Steps the highlight by one row, wrapping at both ends. With nothing highlighted,
            /// which is the state after every keystroke, either arrow steps onto the first row
            /// so that the first key press gives Enter something to commit.
            /// </summary>
            private static void MoveHighlight(int delta) {
                if (rows.Count == 0)
                    return;
                SetHighlight(highlightedRow < 0 ? 0 : (highlightedRow + delta + rows.Count) %
                    rows.Count);
                Log.Debug("MoveHighlight: row {0}/{1} '{2}'".F(highlightedRow,
                    rows.Count - 1, rows[highlightedRow].Entry?.DisplayName ?? "null"));
            }

            /// <summary>
            /// Steps the dropdown highlight while an arrow key is held. Called from a postfix
            /// on the field's per frame selected-object update: that runs only while the search
            /// field is focused, which is exactly the window in which the user is typing and
            /// could press Enter, and it runs after TMP has handled the frame's keys.
            /// <para>
            /// The repeat is timed here rather than taken from the OS: Unity only reports the
            /// initial press through <c>Input.GetKeyDown</c>, repeat events depend on the
            /// player's keyboard repeat setting and repeat rate, and TMP spends those events
            /// moving the caret. A short delay followed by a fixed interval gives the same feel
            /// on every machine, and keeps working when keyboard repeat is switched off.
            /// </para>
            /// </summary>
            internal static void HandleNavigationKey(TMP_InputField field) {
                if (field == null || field != SearchInput || rows.Count == 0 ||
                        Patches.DropdownContainer == null || !Patches.DropdownContainer.
                        activeInHierarchy) {
                    heldDirection = 0;
                    return;
                }
                bool down = Input.GetKey(KeyCode.DownArrow);
                bool up = Input.GetKey(KeyCode.UpArrow);
                // Both keys held is not a direction; treating it as "none" means letting one
                // go starts a fresh press rather than a half way repeat.
                int direction = down == up ? 0 : (down ? 1 : -1);
                if (direction == 0) {
                    if (heldDirection != 0) {
                        // The key was just let go. TMP may have left the caret at the start of
                        // the query on its last repeat, so put it back before the player types.
                        heldDirection = 0;
                        RestoreCaret(field);
                    }
                    return;
                }
                if (direction != heldDirection) {
                    // First step lands immediately, the rest wait for the repeat delay.
                    heldDirection = direction;
                    nextRepeatTime = Time.unscaledTime + REPEAT_DELAY;
                    StepHighlight(field, direction);
                    return;
                }
                // TMP keeps applying its own caret jump for as long as the key repeats, so the
                // caret is put back every frame, not only when the highlight actually moves.
                RestoreCaret(field);
                if (Time.unscaledTime < nextRepeatTime)
                    return;
                nextRepeatTime = Time.unscaledTime + REPEAT_INTERVAL;
                StepHighlight(field, direction);
            }

            /// <summary>Moves the highlight one row and leaves the query caret at the end.</summary>
            private static void StepHighlight(TMP_InputField field, int direction) {
                MoveHighlight(direction);
                RestoreCaret(field);
            }

            /// <summary>
            /// Puts the caret after the query. TMP treats Up/Down as caret jumps in a single
            /// line field, so without this the next character typed would land in front of the
            /// text - or, on the release of an Up held long enough to repeat, at the start.
            /// </summary>
            private static void RestoreCaret(TMP_InputField field) {
                int end = field.text.Length;
                if (field.caretPosition != end)
                    field.caretPosition = end;
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
                case TargetKind.Egg: return MSearchTypes.EGG;
                case TargetKind.Food: return MSearchTypes.FOOD;
                case TargetKind.Plant: return MSearchTypes.PLANT;
                case TargetKind.Seed: return MSearchTypes.SEED;
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
                // The rows stay in the inactive container until the next populating pass
                // destroys them, but the highlight must not outlive the selection: a second
                // Enter should not commit a row from a dropdown that is no longer showing.
                rows.Clear();
                highlightedRow = -1;
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
                AppendCount(parts, TargetKind.Egg);
                AppendCount(parts, TargetKind.Equipment);
                AppendFoodTotals(parts);
                AppendCount(parts, TargetKind.Plant);
                AppendCount(parts, TargetKind.Seed);
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
                if (count <= 0)
                    return;
                // Countable items are merged into stacks, so the unit count routinely differs
                // from the number of objects found. Worth a log line when someone reports a
                // figure that does not match the storage UI.
                int objects = SearchHighlighter.ObjectCountFor(kind);
                if (objects != count)
                    Log.Debug("Count summary: {0} = {1} units across {2} objects".F(kind, count,
                        objects));
                parts.Add(string.Format(CountLabel(kind), count));
            }

            /// <summary>
            /// Food reports its combined mass and calories instead of a count, which says far
            /// more about a larder than the number of individual units in it.
            /// </summary>
            private static void AppendFoodTotals(List<string> parts) {
                if (SearchHighlighter.CountFor(TargetKind.Food) <= 0)
                    return;
                SearchHighlighter.FoodTotals(out float mass, out float calories,
                    out bool allHaveCalories);
                string massText = GameUtil.GetFormattedMass(mass);
                parts.Add(allHaveCalories ?
                    string.Format(MSearch.COUNT_FOOD, massText,
                        GameUtil.GetFormattedCalories(calories)) :
                    massText);
            }

            private static LocString CountLabel(TargetKind kind) {
                switch (kind) {
                case TargetKind.Material: return MSearch.COUNT_MATERIAL;
                case TargetKind.Equipment: return MSearch.COUNT_EQUIPMENT;
                case TargetKind.Duplicant: return MSearch.COUNT_DUPLICANT;
                case TargetKind.Bionic: return MSearch.COUNT_BIONIC;
                case TargetKind.Booster: return MSearch.COUNT_BOOSTER;
                case TargetKind.Critter: return MSearch.COUNT_CRITTER;
                case TargetKind.Egg: return MSearch.COUNT_EGG;
                case TargetKind.Plant: return MSearch.COUNT_PLANT;
                case TargetKind.Seed: return MSearch.COUNT_SEED;
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
                    // Loose items can sit outside the grid (a rocket interior, a stray spawn),
                    // and PosToCell reports those as an invalid cell rather than a real one.
                    if (!Grid.IsValidCell(cell) || (int)Grid.WorldIdx[cell] != worldId)
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
                    // Buildings can be off-grid too (rocket interiors, worldgen leftovers),
                    // and PosToCell reports those as an invalid cell rather than a real one.
                    if (!Grid.IsValidCell(cell) || (int)Grid.WorldIdx[cell] != worldId)
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

        /// <summary>
        /// Names a keybind that uses more than one modifier. What the game reads from a bind
        /// is a set of bits, one per modifier, but its formatter tests that field with a
        /// switch holding one case per single modifier, so a bind with two of them set
        /// matches none of them and the name comes back empty: the key bound here as Shift
        /// plus Ctrl draws "[+ F]" in the overlay button's hover text instead of
        /// "[SHIFT + CTRL + F]". The rewrite replaces the game's own name at the point where
        /// the game already puts it, joins the set bits in the order that same switch tests
        /// them and keeps the same shape (upper case included), so the hover text of the
        /// other {Hotkey} strings gains the names it was missing as well and not only the
        /// one this overlay draws.
        /// </summary>
        /// <remarks>
        /// Only binds with two or more bits set are rewritten; none and one are shapes the
        /// game spells correctly, so leaving those to vanilla keeps its wording, its gamepad
        /// branch and its translated key names, and its default bindings are all single
        /// modifier, so a stock game is shown nothing different.
        /// </remarks>
        [HarmonyPatch(typeof(GameUtil), "GetActionString")]
        public static class GameUtil_GetActionString_Patch {
            private static bool missing_names_logged;

            internal static void Postfix(Action action, ref string __result) {
                // Gamepad draws a glyph and NumActions is answered before the game reads
                // anything; both are shapes this has nothing to correct.
                if (KInputManager.currentControllerIsGamepad || action == Action.NumActions)
                    return;
                // The game's own ActionToBinding reads the same table but throws on an action
                // that has no entry in it, and a postfix has nothing to catch that at.
                var binding = GameInputMapping.FindEntry(action);
                int modifiers = (int)binding.mModifier;
                // Zero or one bit set: a shape the game already names.
                if (modifiers == 0 || (modifiers & (modifiers - 1)) == 0)
                    return;
                __result = (JoinModifiers(modifiers) + " + " +
                    GameUtil.GetKeycodeLocalized(binding.mKeyCode)).ToUpper();
                if (!missing_names_logged) {
                    missing_names_logged = true;
                    Log.Debug("GetActionString: {0} has {1} modifiers and the game named none of them, now {2}".
                        F(action, CountBits(modifiers), __result));
                }
            }

            /// <summary>
            /// The set modifier bits named in the order the game's own switch tests them,
            /// joined with " + ". The names are the game's own translatable ones, so a
            /// language that spells a single modifier a certain way spells it that way here
            /// too.
            /// </summary>
            private static string JoinModifiers(int modifiers) {
                var names = new List<string>();
                if ((modifiers & (int)Modifier.Shift) != 0)
                    names.Add(STRINGS.INPUT.SHIFT.ToString());
                if ((modifiers & (int)Modifier.Ctrl) != 0)
                    names.Add(STRINGS.INPUT.CTRL.ToString());
                if ((modifiers & (int)Modifier.CapsLock) != 0)
                    names.Add(GameUtil.GetKeycodeLocalized(KKeyCode.CapsLock));
                if ((modifiers & (int)Modifier.Alt) != 0)
                    names.Add(STRINGS.INPUT.ALT.ToString());
                if ((modifiers & (int)Modifier.Backtick) != 0)
                    names.Add(GameUtil.GetKeycodeLocalized(KKeyCode.BackQuote));
                return string.Join(" + ", names);
            }

            private static int CountBits(int value) {
                int count = 0;
                while (value != 0) {
                    value &= value - 1;
                    count++;
                }
                return count;
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

        /// <summary>
        /// Focuses the search box when this overlay opens. TMP cannot activate a field whose
        /// GameObject is inactive, and the legend is still hidden while the overlay's own
        /// <c>Enable</c> runs, so the legend being shown is the first point that can work.
        /// Both ways in arrive here: the overlay menu button and the keybind both go through
        /// <c>OverlayScreen.ToggleOverlay</c>, which shows the legend after enabling the mode.
        /// </summary>
        [HarmonyPatch(typeof(OverlayLegend), "SetLegend", typeof(OverlayModes.Mode),
            typeof(bool))]
        public static class OverlayLegend_SetLegend_Patch {
            internal static void Postfix(OverlayModes.Mode mode) {
                if (AutoFocusSearch && mode != null && mode.ViewMode() ==
                        MaterialSearchOverlay.ID)
                    OverlayLegend_OnSpawn_Patch.FocusSearchField();
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

        /// <summary>
        /// Drops every hovered object the search did not match. This is the game's own filter
        /// point for overlay hover text: whatever survives it is what the tooltip draws a
        /// shadow bar for, including the name, status items and temperature, so none of that
        /// has to be reimplemented.
        /// </summary>
        [HarmonyPatch(typeof(SelectToolHoverTextCard), "ShouldShowSelectableInCurrentOverlay")]
        public static class SelectToolHoverTextCard_ShouldShowSelectable_Patch {
            internal static void Postfix(KSelectable selectable, ref bool __result) {
                if (!__result || selectable == null)
                    return;
                // Vanilla lets selectables without a prefab through; keep that, so nothing
                // that never took part in the search starts being hidden.
                if (selectable.GetComponent<KPrefabID>() == null)
                    return;
                var overlay = MaterialSearchOverlay.Instance;
                if (overlay == null || OverlayScreen.Instance == null ||
                        OverlayScreen.Instance.GetMode() != MaterialSearchOverlay.ID)
                    return;
                __result = overlay.ShouldShowHoverEntry(selectable);
            }
        }

        /// <summary>
        /// Teaches the hover card that this overlay does not want the per cell element block
        /// unless the cell's own element matched. Registered lazily rather than in the card's
        /// OnSpawn, so it does not matter when the tool was created and a rebuilt tool picks
        /// the filter up again.
        /// </summary>
        [HarmonyPatch(typeof(SelectToolHoverTextCard), "UpdateHoverElements")]
        public static class SelectToolHoverTextCard_UpdateHoverElements_Patch {
            internal static void Prefix(Dictionary<HashedString, Func<bool>> ___overlayFilterMap) {
                if (___overlayFilterMap.ContainsKey(MaterialSearchOverlay.ID))
                    return;
                ___overlayFilterMap[MaterialSearchOverlay.ID] =
                    MaterialSearchOverlay.ShouldShowHoverCell;
                Log.Debug("Hover filter registered for {0}".F(MaterialSearchOverlay.ID));
            }
        }

        /// <summary>
        /// Makes clicking and hovering pick the searched thing. The tool builds the list of
        /// candidates under the cursor from several sources, and only the collider pass of that
        /// list consults a condition: the hover override and every visible status icon are added
        /// before it, unfiltered. A seed showing a status icon was therefore still clickable
        /// when the search had not matched it - the tooltip filtered it, the click did not, and
        /// the seed won the sort on its instance id. Re-picking from the finished, already
        /// sorted list applies the tooltip's rule to every source at once, and keeps the game's
        /// own ordering, cycling and selection sounds: click once to get the match, click again
        /// to walk through the other matches in the cell.
        /// </summary>
        /// <remarks>
        /// The filtering happens here rather than on <c>StatusItemRenderer.GetIntersections</c>
        /// because that method is two lines long and Mono inlines it into the tool, so patching
        /// it changes nothing at runtime. A postfix on the closed generic is safe: the method is
        /// far too large to inline, and the tool always closes it over <see cref="KSelectable"/>.
        /// </remarks>
        [HarmonyPatch]
        public static class InterfaceTool_GetObjectUnderCursor_Patch {
            private static MethodBase TargetMethod() {
                var target = AccessTools.Method(typeof(InterfaceTool), "GetObjectUnderCursor");
                var closed = target != null && target.IsGenericMethodDefinition ?
                    target.MakeGenericMethod(typeof(KSelectable)) : target;
                Log.Debug("Click filter target: {0}".F(closed));
                return closed;
            }

            internal static void Postfix(InterfaceTool __instance, bool cycleSelection,
                    Component previous_selection, ref KSelectable __result,
                    List<InterfaceTool.Intersection> ___intersections) {
                if (!(__instance is SelectTool))
                    return;
                var overlay = MaterialSearchOverlay.Instance;
                if (overlay == null || !overlay.HoverFilterActive ||
                        OverlayScreen.Instance == null ||
                        OverlayScreen.Instance.GetMode() != MaterialSearchOverlay.ID)
                    return;
                __result = Pick(___intersections, cycleSelection, previous_selection, overlay);
            }

            /// <summary>
            /// Returns the kept entry at the game's own cycling position, or null when the cell
            /// holds nothing the search matched. The list arrives sorted by the game, so walking
            /// it in order keeps the vanilla top of the stack first behaviour. Two passes and no
            /// allocations, since the hover path runs this once per frame.
            /// </summary>
            private static KSelectable Pick(List<InterfaceTool.Intersection> intersections,
                    bool cycleSelection, Component previous_selection,
                    MaterialSearchOverlay overlay) {
                if (intersections == null || intersections.Count == 0)
                    return null;
                int kept = 0;
                int previous_index = -1;
                for (int i = 0; i < intersections.Count; i++) {
                    var selectable = intersections[i].component as KSelectable;
                    if (!ShouldKeep(selectable, overlay))
                        continue;
                    if (selectable == previous_selection)
                        previous_index = kept;
                    kept++;
                }
                if (kept == 0)
                    return null;
                // Same rule the game uses: cycle only when the previous pick survived the
                // filter, otherwise start at the top of the stack.
                int wanted = cycleSelection && previous_index >= 0 ?
                    (previous_index + 1) % kept : 0;
                int index = 0;
                for (int i = 0; i < intersections.Count; i++) {
                    var selectable = intersections[i].component as KSelectable;
                    if (!ShouldKeep(selectable, overlay))
                        continue;
                    if (index == wanted)
                        return selectable;
                    index++;
                }
                return null;
            }

            /// <summary>
            /// The rule the tooltip already uses: the proxies are the game's way of selecting a
            /// tile, so they follow the cell block (an element search can still be inspected, a
            /// creature search cannot), and everything else follows what the overlay lights up.
            /// </summary>
            private static bool ShouldKeep(KSelectable selectable,
                    MaterialSearchOverlay overlay) {
                if (selectable == null)
                    return false;
                if (selectable.GetComponent<CellSelectionObject>() != null ||
                        selectable.GetComponent<BackwallSelectionObject>() != null)
                    return MaterialSearchOverlay.ShouldShowHoverCell();
                return overlay.ShouldShowHoverEntry(selectable);
            }
        }

        /// <summary>
        /// Feeds the arrow keys to the search dropdown. This sits on TMP's own per frame
        /// selected-object update rather than on its key handling because that handler returns
        /// the protected nested <c>EditState</c>, which a prefix in this assembly cannot
        /// declare, so the press cannot be swallowed before TMP sees it. A postfix needs
        /// nothing swallowed: the arrow keys are unbound in the base game, the update runs only
        /// while the field is focused, and it runs after TMP has moved the caret - which is
        /// exactly where the caret has to be put back.
        /// </summary>
        [HarmonyPatch(typeof(TMP_InputField), "OnUpdateSelected")]
        public static class TMPInputField_OnUpdateSelected_Patch {
            internal static void Postfix(TMP_InputField __instance) {
                OverlayLegend_OnSpawn_Patch.HandleNavigationKey(__instance);
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
