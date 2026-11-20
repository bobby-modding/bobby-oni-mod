using PeterHan.PLib.Core;
using PeterHan.PLib.UI;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using MSearch = BobbyModding.MaterialSearchOverlay.MaterialSearchOverlayStrings.UI.OVERLAYS.MATERIALSEARCH;

namespace BobbyModding.MaterialSearchOverlay {
    /// <summary>What a box drag does to the rows of the pinned position list.</summary>
    internal enum DragAction {
        Add,
        Remove
    }

    /// <summary>
    /// The map side of the "Add area" and "Remove area" buttons. It is an ordinary
    /// <see cref="DragTool"/> in box mode whose drag is turned into row ticks instead of a
    /// sim edit: every matching cell the box covers is handed to
    /// <see cref="MatchLocationUI.ApplyDragSelection"/>, which owns the cell to row index of
    /// the pinned search.
    /// <para>
    /// The component cannot be authored in a prefab, so it is created on first use and
    /// parented to <see cref="PlayerController"/> like a vanilla tool. Only the private
    /// <see cref="DragTool"/> visualizer fields are read by reflection: the box renderer and
    /// the "W x H (N cells)" area text are borrowed from a vanilla tool instance, with a
    /// generated box as the fallback, because a box tool without an <c>areaVisualizer</c>
    /// never reports the cells it covers.
    /// </para>
    /// <para>
    /// <c>viewMode</c> is left at <see cref="OverlayModes.None"/>, so arming this tool does
    /// not make the game switch the material search overlay off. Disarming goes through
    /// <see cref="PlayerController.ToolDeactivated"/>, which is the same path a vanilla tool
    /// takes when it hands the mouse back to the select tool.
    /// </para>
    /// </summary>
    internal sealed class AreaDragTool : DragTool {
        private static readonly FieldInfo AREA_VISUALIZER_FIELD = typeof(DragTool).GetField(
            "areaVisualizer", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo AREA_TEXT_FIELD = typeof(DragTool).GetField(
            "areaVisualizerTextPrefab", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo AREA_COLOUR_FIELD = typeof(DragTool).GetField(
            "areaColour", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo BOX_CURSOR_FIELD = typeof(DragTool).GetField(
            "boxCursor", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Tint of the drag box while it is adding rows.</summary>
        private static readonly Color ADD_COLOR = new Color(0.45f, 1f, 0.45f, 0.45f);

        /// <summary>Tint of the drag box while it is removing rows.</summary>
        private static readonly Color REMOVE_COLOR = new Color(1f, 0.4f, 0.4f, 0.45f);

        /// <summary>The live tool, or null until the first box is armed.</summary>
        internal static AreaDragTool Instance { get; private set; }

        /// <summary>
        /// The cells the current box covers. Box mode delivers each cell once on mouse up, but
        /// a set keeps a future brush-like mode from ticking a cell twice.
        /// </summary>
        private readonly HashSet<int> pending = new HashSet<int>();

        private Sprite fallbackSprite;

        /// <summary>
        /// The drag box's own material. The borrowed clone still points at the donor prefab's
        /// material, and painting that would also repaint the donor tool's own box, so the tint
        /// is written to a copy only this box reads.
        /// </summary>
        private Material tintMaterial;

        /// <summary>Whether the next drag adds rows or removes them.</summary>
        internal DragAction CurrentAction { get; private set; } = DragAction.Add;

        /// <summary>
        /// What a drag would do right now, or null when this tool is not the active tool.
        /// This is what the two legend buttons paint their armed state from.
        /// </summary>
        internal static DragAction? ArmedAction {
            get {
                var tool = Instance;
                var controller = PlayerController.Instance;
                if (tool == null || controller == null || controller.ActiveTool != tool)
                    return null;
                return tool.CurrentAction;
            }
        }

        /// <summary>
        /// Arms the tool with <paramref name="action"/>, creating it first if this is the
        /// first box of the session. Returns false when there is no loaded game to attach to.
        /// </summary>
        internal static bool Arm(DragAction action) {
            if (Game.Instance == null || PlayerController.Instance == null)
                return false;
            var tool = EnsureInstance();
            if (tool == null)
                return false;
            tool.CurrentAction = action;
            tool.pending.Clear();
            tool.ApplyTint();
            if (PlayerController.Instance.ActiveTool != tool)
                PlayerController.Instance.ActivateTool(tool);
            Log.Debug("AreaDragTool: armed {0}".F(action));
            return true;
        }

        /// <summary>
        /// Returns the mouse to the select tool. Safe to call while the tool is not active or
        /// while the game is shutting down.
        /// </summary>
        internal static void Disarm() {
            var tool = Instance;
            var controller = PlayerController.Instance;
            if (tool == null || controller == null)
                return;
            tool.pending.Clear();
            // DragTool.OnLeftClickDown disables the UI event system and only the mouse up
            // re-enables it, so a tool that is disarmed mid-drag has to clean that up itself.
            if (Game.Instance != null && KScreenManager.Instance != null)
                tool.CancelDragging();
            if (ReferenceEquals(controller.ActiveTool, tool) && SelectTool.Instance != null)
                controller.ToolDeactivated(tool);
            AreaDragBar.Refresh();
        }

        private static AreaDragTool EnsureInstance() {
            var existing = Instance;
            if (existing != null)
                return existing;
            var controller = PlayerController.Instance;
            if (controller == null)
                return null;
            // AddComponent while the controller is active runs Awake and OnPrefabInit right
            // away, so the tool is fully initialised before the game ever activates it. It is
            // parked inactive afterwards, exactly like PlayerController parks its own tools.
            var obj = new GameObject("MaterialSearchAreaDragTool");
            obj.transform.SetParent(controller.transform, false);
            var tool = obj.AddComponent<AreaDragTool>();
            obj.SetActive(false);
            return tool;
        }

        protected override void OnPrefabInit() {
            base.OnPrefabInit();
            Instance = this;
            SetupVisuals();
            Log.Debug("AreaDragTool: tool created");
        }

        /// <summary>
        /// Borrows the box visualizer and its "W x H (N cells)" text from a vanilla drag
        /// tool. Without an area visualizer, <see cref="DragTool.OnLeftClickUp"/> returns
        /// before it reports any cell, so the fallback has to produce one even on its own.
        /// </summary>
        private void SetupVisuals() {
            DragTool donor = DebugTool.Instance;
            if (donor == null)
                donor = DigTool.Instance;
            if (donor == null)
                donor = FindFirstObjectByType<DigTool>();
            if (donor != null) {
                var template = GetPrivate<GameObject>(AREA_VISUALIZER_FIELD, donor);
                if (template != null) {
                    // Same sequence as DragTool.OnPrefabInit: clone, park it, adopt it, and
                    // read its renderer. The clone is parented without moving it, so the box
                    // appears where the drag is rather than where the donor tool stands.
                    var clone = Util.KInstantiate(template);
                    if (clone != null) {
                        clone.SetActive(false);
                        clone.transform.SetParent(transform);
                        var renderer = clone.GetComponent<SpriteRenderer>();
                        areaVisualizerSpriteRenderer = renderer;
                        SetPrivate(AREA_VISUALIZER_FIELD, clone);
                    }
                }
                SetPrivate(AREA_TEXT_FIELD, GetPrivate<GameObject>(AREA_TEXT_FIELD, donor));
                SetPrivate(BOX_CURSOR_FIELD, GetPrivate<Texture2D>(BOX_CURSOR_FIELD, donor));
            }
            if (GetPrivate<GameObject>(AREA_VISUALIZER_FIELD, this) == null) {
                // No donor is still playable: a white box is enough to see the drag and to
                // let the base class report the cells. Only the area text is lost with it.
                var box = new GameObject("AreaVisualizer");
                box.transform.SetParent(transform, false);
                var renderer = box.AddComponent<SpriteRenderer>();
                renderer.sprite = FallbackSprite();
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = new Vector2(1f, 1f);
                box.SetActive(false);
                SetPrivate(AREA_VISUALIZER_FIELD, box);
                areaVisualizerSpriteRenderer = renderer;
                Log.Debug("AreaDragTool: no donor tool, using the generated drag box");
            }
            ApplyTint();
        }

        private Sprite FallbackSprite() {
            if (fallbackSprite != null)
                return fallbackSprite;
            var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false) {
                name = "MaterialSearchAreaDragBox",
                filterMode = FilterMode.Point,
                // Tiled draw mode needs a repeating texture; a 1x1 white one makes the sprite
                // a solid rectangle that stretches to whatever size the base class asks for.
                wrapMode = TextureWrapMode.Repeat
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            return fallbackSprite;
        }

        /// <summary>Paints the box, and the area text, in the colour of the current action.</summary>
        private void ApplyTint() {
            Color color = CurrentAction == DragAction.Add ? ADD_COLOR : REMOVE_COLOR;
            SetPrivate(AREA_COLOUR_FIELD, (Color32) color);
            var renderer = areaVisualizerSpriteRenderer;
            if (renderer == null)
                return;
            var material = renderer.material;
            if (material == null)
                return;
            // The vanilla box material belongs to the donor prefab, so a first use of it here
            // would leave the dig and debug tools tinted green as well.
            if (!ReferenceEquals(material, tintMaterial)) {
                tintMaterial = new Material(material);
                tintMaterial.name = "MaterialSearchAreaDragBox";
                renderer.material = tintMaterial;
            }
            tintMaterial.color = color;
        }

        private static T GetPrivate<T>(FieldInfo field, object instance) where T : class {
            if (field == null || instance == null)
                return null;
            return field.GetValue(instance) as T;
        }

        private void SetPrivate(FieldInfo field, object value) {
            if (field == null)
                return;
            try {
                field.SetValue(this, value);
            } catch (System.Exception e) {
                Log.Debug("AreaDragTool: could not set {0}: {1}".F(field.Name, e));
            }
        }

        protected override void OnActivateTool() {
            base.OnActivateTool();
            pending.Clear();
            ApplyTint();
            AreaDragBar.Refresh();
        }

        protected override void OnDeactivateTool(InterfaceTool new_tool) {
            pending.Clear();
            base.OnDeactivateTool(new_tool);
            // The player may have picked a build or dig tool instead of pressing the button,
            // so the armed marker has to clear from here as well.
            AreaDragBar.Refresh();
        }

        public override void OnKeyDown(KButtonEvent e) {
            // Esc and the vanilla cancel key both put the mouse back where it was. The tool
            // stays armed otherwise, so a vein can be worked with several boxes in a row.
            if (e.TryConsume(Action.Escape) || e.TryConsume(Action.BuildingCancel)) {
                Disarm();
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnDragTool(int cell, int distFromOrigin) {
            if (Grid.IsValidCell(cell))
                pending.Add(cell);
        }

        protected override void OnDragComplete(Vector3 cursorDown, Vector3 cursorUp) {
            if (pending.Count == 0)
                return;
            MatchLocationUI.ApplyDragSelection(pending, CurrentAction == DragAction.Add);
            Log.Debug("AreaDragTool: {0} {1} cells".F(pending.Count, CurrentAction));
            pending.Clear();
        }
    }

    /// <summary>
    /// The "Add area" / "Remove area" buttons in the overlay legend, and the armed marker
    /// they paint. They only exist while a pinned result has rows to work on: the buttons are
    /// built once with the legend and shown and hidden by <see cref="MatchLocationUI"/>.
    /// </summary>
    internal static class AreaDragBar {
        /// <summary>Background of the add button while its tool is armed.</summary>
        private static readonly Color ADD_TINT = new Color(0.32f, 0.62f, 0.32f);

        /// <summary>Background of the remove button while its tool is armed.</summary>
        private static readonly Color REMOVE_TINT = new Color(0.68f, 0.28f, 0.28f);

        private static GameObject bar;

        private static KImage addImage, removeImage;

        private static ColorStyleSetting addNormal, removeNormal, addArmed, removeArmed;

        /// <summary>Builds the two buttons inside a freshly spawned legend.</summary>
        internal static void Build(Transform parent) {
            if (parent == null)
                return;
            // A respawned legend destroyed the previous buttons with it; start from scratch.
            bar = null;
            addImage = removeImage = null;
            addNormal = removeNormal = null;
            addArmed = removeArmed = null;

            var add = new PButton("AreaAddButton") {
                Text = MSearch.AREA_ADD,
                ToolTip = MSearch.AREA_ADD_TOOLTIP,
                FlexSize = new Vector2(1f, 0f)
            };
            add.OnClick = _ => Toggle(DragAction.Add);
            add.AddOnRealize(obj => {
                addImage = obj.GetComponent<KImage>();
                addNormal = addImage != null ? addImage.colorStyleSetting : null;
            });

            var remove = new PButton("AreaRemoveButton") {
                Text = MSearch.AREA_REMOVE,
                ToolTip = MSearch.AREA_REMOVE_TOOLTIP,
                FlexSize = new Vector2(1f, 0f)
            };
            remove.OnClick = _ => Toggle(DragAction.Remove);
            remove.AddOnRealize(obj => {
                removeImage = obj.GetComponent<KImage>();
                removeNormal = removeImage != null ? removeImage.colorStyleSetting : null;
            });

            var panel = new PPanel("AreaDragBar") {
                Direction = PanelDirection.Horizontal,
                Spacing = 4,
                FlexSize = new Vector2(1f, 0f)
            };
            panel.AddChild(add);
            panel.AddChild(remove);
            var obj = panel.Build();
            obj.transform.SetParent(parent, false);
            obj.SetActive(false);
            bar = obj;
            Log.Debug("AreaDragBar: built");
        }

        /// <summary>
        /// Shows the buttons for a pinned result, or hides them and disarms the tool. The bar
        /// outlives a single search, so a stale armed tool can never survive a new pin.
        /// </summary>
        internal static void SetVisible(bool visible) {
            if (!visible)
                AreaDragTool.Disarm();
            var obj = bar;
            if (obj != null)
                obj.SetActive(visible);
            if (visible)
                Refresh();
        }

        /// <summary>Repaints the armed marker from the active tool's current action.</summary>
        internal static void Refresh() {
            var armed = AreaDragTool.ArmedAction;
            if (addArmed == null)
                addArmed = MakeStyle(ADD_TINT);
            if (removeArmed == null)
                removeArmed = MakeStyle(REMOVE_TINT);
            Apply(addImage, addNormal, addArmed, armed == DragAction.Add);
            Apply(removeImage, removeNormal, removeArmed, armed == DragAction.Remove);
        }

        private static void Toggle(DragAction action) {
            // The same button again is how a player puts the mouse back; the other one just
            // changes what the next box does.
            if (AreaDragTool.ArmedAction == action) {
                AreaDragTool.Disarm();
                return;
            }
            // Moving the mouse to the map must not keep typing into the search box, or the
            // camera would spend every key press on the field instead of on panning.
            Patches.DeselectSearchField();
            if (!AreaDragTool.Arm(action))
                Log.Debug("AreaDragBar: no loaded game to arm the tool in");
            Refresh();
        }

        private static void Apply(KImage image, ColorStyleSetting normal,
                ColorStyleSetting armed, bool isArmed) {
            if (image == null)
                return;
            var style = isArmed ? armed : normal;
            if (style == null)
                return;
            image.colorStyleSetting = style;
            image.ApplyColorStyleSetting();
        }

        /// <summary>
        /// A button background style in one hue, so an armed button reads as on at a glance
        /// without changing the button's shape or text.
        /// </summary>
        private static ColorStyleSetting MakeStyle(Color color) {
            var style = ScriptableObject.CreateInstance<ColorStyleSetting>();
            style.activeColor = color;
            style.inactiveColor = Darken(color, 0.72f);
            style.hoverColor = Darken(color, 0.86f);
            style.disabledColor = new Color(0.42f, 0.42f, 0.4f);
            style.disabledActiveColor = new Color(0.5f, 0.5f, 0.5f);
            style.disabledhoverColor = new Color(0.5f, 0.5f, 0.5f);
            return style;
        }

        private static Color Darken(Color color, float factor) {
            return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
        }
    }
}
