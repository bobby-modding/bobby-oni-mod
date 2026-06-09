using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PeterHan.PLib.Core;
using PeterHan.PLib.UI;

namespace CommandPallete
{
    public class CommandPalleteScreen : KScreen
    {
        public static CommandPalleteScreen Instance { get; private set; }

        private TMP_InputField searchField;
        private GameObject contentContainer;
        private TextMeshProUGUI statusLabel;
        private List<CommandEntry> currentResults = new List<CommandEntry>();
        private int selectedIndex = -1;
        private readonly List<GameObject> resultItems = new List<GameObject>();

        private const int PANEL_WIDTH = 500;
        private const int PANEL_PADDING = 12;
        private const int ITEM_HEIGHT = 28;
        private const int MAX_VISIBLE_ITEMS = 12;

        private static readonly Color OVERLAY_BG = new Color32(0, 0, 0, 180);
        private static readonly Color PANEL_BG = new Color32(38, 38, 38, 245);
        private static readonly Color INPUT_BG = new Color32(55, 55, 55, 255);
        private static readonly Color ITEM_NORMAL = new Color32(0, 0, 0, 5);
        private static readonly Color ITEM_HIGHLIGHT = new Color32(72, 100, 145, 100);
        private static readonly Color SCROLL_BG = new Color32(0, 0, 0, 30);

        private static readonly Dictionary<CommandCategory, Color> BADGE_COLORS = new()
        {
            { CommandCategory.Building, new Color32(70, 150, 255, 255) },
            { CommandCategory.Tool, new Color32(100, 200, 100, 255) },
            { CommandCategory.Overlay, new Color32(255, 200, 60, 255) },
            { CommandCategory.Screen, new Color32(255, 120, 120, 255) },
            { CommandCategory.Action, new Color32(180, 140, 255, 255) },
        };

        public static void Toggle()
        {
            if (Game.Instance == null || Game.IsQuitting())
            {
                Log.Debug("Toggle: Game not active, skipping");
                return;
            }

            if (Instance != null)
            {
                Log.Debug("Toggle: palette already open, closing it");
                Instance.Deactivate();
                return;
            }

            Log.Debug("Toggle: palette not open, proceeding to open");
            CloseManagementScreens();

            var parent = GameScreenManager.Instance.ssOverlayCanvas.gameObject;

            var go = new GameObject("CommandPalleteScreen");
            go.SetActive(false);
            go.AddComponent<RectTransform>();
            var screen = go.AddComponent<CommandPalleteScreen>();
            KScreenManager.AddExistingChild(parent, go);
            Log.Debug("Toggle: created CommandPalleteScreen, parented to ssOverlayCanvas");
            screen.Activate();
        }

        public static void Open()
        {
            Toggle();
        }

        private static void CloseManagementScreens()
        {
            if (ManagementMenu.Instance != null)
                ManagementMenu.Instance.CloseAll();
        }

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Instance = this;
            activateOnSpawn = false;

            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            BuildUI();
            Log.Debug("OnPrefabInit: UI built");
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            Log.Debug("OnSpawn: setting up search field listeners");

            if (searchField != null)
            {
                searchField.onValueChanged.AddListener(text => PerformSearch(text));
                searchField.ActivateInputField();
                searchField.Select();
            }
            PerformSearch("");
            Log.Debug("OnSpawn: search field focused and activated");
        }

        public override bool IsModal() => true;

        public override float GetSortKey() => MODAL_SCREEN_SORT_KEY;

        protected override void OnShow(bool show)
        {
            base.OnShow(show);
            if (show && searchField != null)
            {
                searchField.ActivateInputField();
                searchField.Select();
            }
        }

        protected override void OnDeactivate()
        {
            Log.Debug("OnDeactivate: clearing Instance, palette closing");
            Instance = null;
            base.OnDeactivate();
        }

        public override void OnKeyDown(KButtonEvent e)
        {
            Log.Debug("OnKeyDown: event action={0}, consumed={1}".F(e.GetAction(), e.Consumed));

            if (!e.Consumed && e.TryConsume(Action.Escape))
            {
                if (searchField != null && searchField.text.Length > 0)
                {
                    searchField.text = "";
                    PerformSearch("");
                }
                else
                    Deactivate();
                return;
            }
            if (!e.Consumed && e.TryConsume(Action.DialogSubmit))
            {
                Log.Debug("OnKeyDown: DialogSubmit consumed, executing selected (index={0})"
                    .F(selectedIndex));
                ExecuteSelected();
                return;
            }
            base.OnKeyDown(e);
        }

        private void Update()
        {
            if (!isActiveAndEnabled) return;
            if (isEditing) return;

            int resultCount = currentResults.Count;

            if (resultCount > 0)
            {
                if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    if (selectedIndex < resultCount - 1)
                    {
                        selectedIndex++;
                        UpdateSelection();
                        ScrollToSelected();
                    }
                }
                else if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    if (selectedIndex > 0)
                    {
                        selectedIndex--;
                        UpdateSelection();
                        ScrollToSelected();
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                ExecuteSelected();
        }

        private static GameObject CreateUIGameObject(string name, GameObject parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.localScale = Vector3.one;
            go.layer = LayerMask.NameToLayer("UI");
            return go;
        }

        private void BuildUI()
        {
            // Background overlay — click to close
            var bg = CreateUIGameObject("Background", gameObject);
            var bgRt = bg.rectTransform();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            bgRt.anchoredPosition = Vector2.zero;
            bg.AddComponent<Image>().color = OVERLAY_BG;
            var bgTrigger = bg.AddComponent<EventTrigger>();
            var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener(_ => Deactivate());
            bgTrigger.triggers.Add(clickEntry);

            // Panel — centered container with fixed width, auto height
            var panelObj = CreateUIGameObject("Panel", gameObject);
            var panelRt = panelObj.rectTransform();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(PANEL_WIDTH, 0);
            panelRt.anchoredPosition = new Vector2(0, 80);
            var panelImg = panelObj.AddComponent<Image>();
            panelImg.color = PANEL_BG;

            // Vertical layout — Unity built-in
            var vlg = panelObj.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 6;
            vlg.padding = new RectOffset(PANEL_PADDING, PANEL_PADDING,
                PANEL_PADDING, PANEL_PADDING);

            // Auto-size height to fit children
            var panelFitter = panelObj.AddComponent<ContentSizeFitter>();
            panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Title
            var titleGo = CreateUIGameObject("Title", panelObj);
            titleGo.AddComponent<LayoutElement>().flexibleWidth = 1;
            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = (string)CommandPalleteStrings.UI.COMMANDPALETTE.NAME;
            titleText.font = PUITuning.Fonts.UILightStyle.sdfFont;
            titleText.fontSize = PUITuning.Fonts.UILightStyle.fontSize;
            titleText.color = PUITuning.Fonts.UILightStyle.textColor;
            titleText.fontStyle = PUITuning.Fonts.UILightStyle.style;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.textWrappingMode = TextWrappingModes.NoWrap;

            // Search input via PTextField
            var textField = new PTextField("SearchInput")
            {
                PlaceholderText = (string)CommandPalleteStrings.UI.COMMANDPALETTE.SEARCH_PLACEHOLDER,
                Text = "",
                TextAlignment = TextAlignmentOptions.Left,
                MinWidth = PANEL_WIDTH - 2 * PANEL_PADDING,
                FlexSize = new Vector2(1, 0),
                BackColor = INPUT_BG,
                OnTextChanged = (source, text) => PerformSearch(text)
            };
            searchField = textField.Build().GetComponent<TMP_InputField>();
            searchField.gameObject.SetParent(panelObj);
            searchField.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Status text
            var statusGo = CreateUIGameObject("Status", panelObj);
            statusGo.AddComponent<LayoutElement>().flexibleWidth = 1;
            statusLabel = statusGo.AddComponent<TextMeshProUGUI>();
            statusLabel.text = "\u00A0";
            statusLabel.font = PUITuning.Fonts.UILightStyle.sdfFont;
            statusLabel.fontSize = PUITuning.Fonts.UILightStyle.fontSize;
            statusLabel.color = PUITuning.Fonts.UILightStyle.textColor;
            statusLabel.fontStyle = PUITuning.Fonts.UILightStyle.style;
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.textWrappingMode = TextWrappingModes.NoWrap;

            // Scrollable results area
            var scrollGo = CreateUIGameObject("ScrollView", panelObj);
            scrollGo.AddComponent<LayoutElement>()
                .preferredHeight = MAX_VISIBLE_ITEMS * ITEM_HEIGHT;
            var scrollBg = scrollGo.AddComponent<Image>();
            scrollBg.color = SCROLL_BG;

            var scrollRect = scrollGo.AddComponent<KScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.verticalScrollbarVisibility = KScrollRect.ScrollbarVisibility
                .AutoHideAndExpandViewport;

            // Viewport — clips the content
            var viewport = CreateUIGameObject("Viewport", scrollGo);
            var viewportRt = viewport.rectTransform();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.sizeDelta = Vector2.zero;
            viewportRt.anchoredPosition = Vector2.zero;
            viewport.AddComponent<RectMask2D>().enabled = true;
            scrollRect.viewport = viewportRt;

            // Content — holds result items, top-anchored, grows downward
            var contentGo = CreateUIGameObject("Content", viewport);
            var contentRt = contentGo.rectTransform();
            contentRt.pivot = new Vector2(0.5f, 1.0f);
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;
            scrollRect.content = contentRt;
            contentContainer = contentGo;

            var contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childControlHeight = false;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.spacing = 1;
            contentLayout.childScaleWidth = true;

            var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void PerformSearch(string query)
        {
            Log.Debug("PerformSearch: query='{0}' (length={1})".F(query, query.Length));
            currentResults = CommandIndex.Instance.FuzzySearch(query);
            selectedIndex = currentResults.Count > 0 ? 0 : -1;
            RebuildResultItems();
            UpdateStatusText();
        }

        private void RebuildResultItems()
        {
            Log.Debug("RebuildResultItems: contentContainer={0}, results count={1}"
                .F(contentContainer != null ? contentContainer.name : "null",
                   currentResults.Count));

            foreach (var item in resultItems)
                Destroy(item);
            resultItems.Clear();

            for (int i = 0; i < currentResults.Count; i++)
            {
                var item = CreateResultItem(i);
                resultItems.Add(item);
            }

            Log.Debug("RebuildResultItems: created {0} items".F(resultItems.Count));
            UpdateSelection();
        }

        private GameObject CreateResultItem(int index)
        {
            var entry = currentResults[index];

            var entryBadgeColor = BADGE_COLORS.TryGetValue(entry.Category, out var bc)
                ? bc : Color.gray;

            // Row root
            var rowGo = new GameObject("Result_" + index);
            rowGo.transform.SetParent(contentContainer.transform, false);
            rowGo.AddComponent<RectTransform>();
            rowGo.AddComponent<Image>().color = ITEM_NORMAL;
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.spacing = 6;
            rowLayout.padding = new RectOffset(6, 6, 2, 2);
            rowGo.AddComponent<LayoutElement>()
                .preferredHeight = ITEM_HEIGHT;

            // Badge
            var badge = new GameObject("Badge");
            badge.transform.SetParent(rowGo.transform, false);
            var badgeImg = badge.AddComponent<Image>();
            badgeImg.color = entryBadgeColor;
            badgeImg.raycastTarget = false;
            badge.AddComponent<LayoutElement>().preferredWidth = 4;

            // Name
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(rowGo.transform, false);
            var nameText = nameGo.AddComponent<TextMeshProUGUI>();
            nameText.text = entry.DisplayName;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.font = PUITuning.Fonts.UILightStyle.sdfFont;
            nameText.fontSize = PUITuning.Fonts.UILightStyle.fontSize;
            nameText.color = PUITuning.Fonts.UILightStyle.textColor;
            nameText.fontStyle = PUITuning.Fonts.UILightStyle.style;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.raycastTarget = false;
            nameGo.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Category
            var catGo = new GameObject("Category");
            catGo.transform.SetParent(rowGo.transform, false);
            var catText = catGo.AddComponent<TextMeshProUGUI>();
            catText.text = entry.Category.ToString();
            catText.alignment = TextAlignmentOptions.Right;
            catText.font = PUITuning.Fonts.UILightStyle.sdfFont;
            catText.fontSize = PUITuning.Fonts.UILightStyle.fontSize - 2;
            catText.color = PUITuning.Fonts.UILightStyle.textColor;
            catText.fontStyle = PUITuning.Fonts.UILightStyle.style;
            catText.textWrappingMode = TextWrappingModes.NoWrap;
            catText.raycastTarget = false;
            catGo.AddComponent<LayoutElement>().preferredWidth = 60;

            // Click + hover events
            var trigger = rowGo.AddComponent<EventTrigger>();

            int capturedIndex = index;
            var clickEntry = new EventTrigger.Entry
                { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener(_ =>
            {
                selectedIndex = capturedIndex;
                ExecuteSelected();
            });
            trigger.triggers.Add(clickEntry);

            var enterEntry = new EventTrigger.Entry
                { eventID = EventTriggerType.PointerEnter };
            int capturedEnter = index;
            enterEntry.callback.AddListener(_ =>
            {
                selectedIndex = capturedEnter;
                UpdateSelection();
            });
            trigger.triggers.Add(enterEntry);

            return rowGo;
        }

        private void UpdateSelection()
        {
            for (int i = 0; i < resultItems.Count; i++)
            {
                if (resultItems[i] == null) continue;
                var img = resultItems[i].GetComponent<Image>();
                if (img != null)
                    img.color = (i == selectedIndex) ? ITEM_HIGHLIGHT : ITEM_NORMAL;
            }
        }

        private void ScrollToSelected()
        {
            var kScroll = GetComponentInChildren<KScrollRect>();
            if (kScroll == null || currentResults.Count <= 1) return;

            float normalizedPos = 1f - (float)selectedIndex / (currentResults.Count - 1);
            kScroll.verticalNormalizedPosition = Mathf.Clamp01(normalizedPos);
        }

        private void UpdateStatusText()
        {
            int count = currentResults.Count;
            string hints = "\u2191\u2193 navigate  \u23ce select  Esc close";
            if (count == 0 && searchField != null && searchField.text.Length > 0)
                statusLabel.text = (string)CommandPalleteStrings.UI.COMMANDPALETTE.NO_RESULTS;
            else if (count > 0)
                statusLabel.text = string.Format("{0} result{1} | {2}",
                    count, count != 1 ? "s" : "", hints);
            else
                statusLabel.text = hints;
        }

        private void ExecuteSelected()
        {
            if (selectedIndex >= 0 && selectedIndex < currentResults.Count)
            {
                var entry = currentResults[selectedIndex];
                Log.Debug("ExecuteSelected: executing id='{0}' name='{1}' category={2}"
                    .F(entry.Id, entry.DisplayName, entry.Category));
                entry.Execute();
            }
            else
                Log.Debug("ExecuteSelected: selectedIndex={0} out of range (results={1})"
                    .F(selectedIndex, currentResults.Count));
            Deactivate();
        }
    }
}
