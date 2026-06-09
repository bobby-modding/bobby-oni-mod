using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PeterHan.PLib.Core;

namespace CommandPallete
{
    public class CommandPalleteScreen : KScreen
    {
        public static CommandPalleteScreen Instance { get; private set; }

        private TMP_InputField searchField;
        private GameObject contentContainer;
        private TextMeshProUGUI statusText;

        private List<CommandEntry> currentResults = new List<CommandEntry>();
        private int selectedIndex = -1;
        private readonly List<GameObject> resultItems = new List<GameObject>();

        private const int PANEL_WIDTH = 500;
        private const int PANEL_PADDING = 12;
        private const int INPUT_HEIGHT = 28;
        private const int ITEM_HEIGHT = 28;
        private const int MAX_VISIBLE_ITEMS = 12;

        private static readonly Color OVERLAY_BG = new Color32(0, 0, 0, 180);
        private static readonly Color PANEL_BG = new Color32(38, 38, 38, 245);
        private static readonly Color INPUT_BG = new Color32(55, 55, 55, 255);
        private static readonly Color ITEM_NORMAL = new Color32(0, 0, 0, 5);
        private static readonly Color ITEM_HIGHLIGHT = new Color32(72, 100, 145, 100);
        private static readonly Color TEXT_MAIN = Color.white;
        private static readonly Color TEXT_HINT = new Color32(140, 140, 140, 255);
        private static readonly Color TEXT_CATEGORY = new Color32(160, 160, 160, 255);
        private static readonly Color SCROLL_HANDLE = new Color32(100, 100, 100, 255);
        private static readonly Color SCROLL_BG = new Color32(45, 45, 45, 255);

        private static readonly Dictionary<CommandCategory, Color> BADGE_COLORS = new()
        {
            { CommandCategory.Building, new Color32(70, 150, 255, 255) },
            { CommandCategory.Tool, new Color32(100, 200, 100, 255) },
            { CommandCategory.Overlay, new Color32(255, 200, 60, 255) },
            { CommandCategory.Screen, new Color32(255, 120, 120, 255) },
            { CommandCategory.Action, new Color32(180, 140, 255, 255) },
        };

        public static void Open()
        {
            if (Instance != null)
            {
                Instance.Deactivate();
                return;
            }

            var go = new GameObject("CommandPalleteScreen");
            go.SetActive(false);
            var rt = go.AddComponent<RectTransform>();
            var screen = go.AddComponent<CommandPalleteScreen>();
            screen.Activate();
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
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();

            searchField.onValueChanged.AddListener(text => PerformSearch(text));

            searchField.onSelect.AddListener(_ =>
            {
                isEditing = true;
            });

            searchField.onDeselect.AddListener(_ =>
            {
                isEditing = false;
            });

            PerformSearch("");
            searchField.ActivateInputField();
            searchField.Select();
        }

        public override bool IsModal() => true;

        public override float GetSortKey() => MODAL_SCREEN_SORT_KEY;

        protected override void OnShow(bool show)
        {
            base.OnShow(show);
            if (show)
            {
                searchField.ActivateInputField();
                searchField.Select();
            }
        }

        protected override void OnDeactivate()
        {
            Instance = null;
            base.OnDeactivate();
        }

        public override void OnKeyDown(KButtonEvent e)
        {
            if (!e.Consumed && e.TryConsume(Action.Escape))
            {
                if (searchField.text.Length > 0)
                {
                    searchField.text = "";
                    PerformSearch("");
                }
                else
                {
                    Deactivate();
                }
                return;
            }
            if (!e.Consumed && e.TryConsume(Action.DialogSubmit))
            {
                ExecuteSelected();
                return;
            }
            base.OnKeyDown(e);
        }

        private void Update()
        {
            if (!isActiveAndEnabled) return;

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
            {
                ExecuteSelected();
            }
        }

        private void BuildUI()
        {
            var rt = GetComponent<RectTransform>();

            var bg = CreateUIObject("Background", rt);
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = OVERLAY_BG;
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            var bgTrigger = bg.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(_ => Deactivate());
            bgTrigger.triggers.Add(entry);

            var panel = CreateUIObject("Panel", rt);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = PANEL_BG;
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(PANEL_WIDTH, 0);
            panelRt.anchoredPosition = new Vector2(0, 80);

            var panelLayout = panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;
            panelLayout.spacing = 6;
            panelLayout.padding = new RectOffset(PANEL_PADDING, PANEL_PADDING, PANEL_PADDING, PANEL_PADDING);

            var titleObj = CreateUIObject("Title", panel.transform);
            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = (string)CommandPalleteStrings.UI.COMMANDPALETTE.NAME;
            titleText.fontSize = 18;
            titleText.color = TEXT_MAIN;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.raycastTarget = false;

            searchField = BuildSearchField(panel.transform);

            var statusObj = CreateUIObject("Status", panel.transform);
            statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.fontSize = 11;
            statusText.color = TEXT_HINT;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.raycastTarget = false;

            BuildResultsScrollView(panel.transform);
        }

        private TMP_InputField BuildSearchField(Transform parent)
        {
            var inputObj = CreateUIObject("SearchInput", parent);
            var inputImage = inputObj.AddComponent<Image>();
            inputImage.color = INPUT_BG;
            inputImage.type = Image.Type.Sliced;
            var inputLayout = inputObj.AddComponent<LayoutElement>();
            inputLayout.preferredHeight = INPUT_HEIGHT;

            var textArea = CreateUIObject("TextArea", inputObj.transform);
            var textAreaRt = textArea.GetComponent<RectTransform>();
            textAreaRt.anchorMin = Vector2.zero;
            textAreaRt.anchorMax = Vector2.one;
            textAreaRt.offsetMin = new Vector2(8, 2);
            textAreaRt.offsetMax = new Vector2(-8, -2);

            var textObj = CreateUIObject("Text", textArea.transform);
            var textComp = textObj.AddComponent<TextMeshProUGUI>();
            textComp.fontSize = 14;
            textComp.color = TEXT_MAIN;
            textComp.alignment = TextAlignmentOptions.Left;
            var textRt = textComp.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            var placeholderObj = CreateUIObject("Placeholder", textArea.transform);
            var placeholderComp = placeholderObj.AddComponent<TextMeshProUGUI>();
            placeholderComp.text = (string)CommandPalleteStrings.UI.COMMANDPALETTE.SEARCH_PLACEHOLDER;
            placeholderComp.fontSize = 14;
            placeholderComp.color = TEXT_HINT;
            placeholderComp.alignment = TextAlignmentOptions.Left;
            var placeholderRt = placeholderComp.GetComponent<RectTransform>();
            placeholderRt.anchorMin = Vector2.zero;
            placeholderRt.anchorMax = Vector2.one;
            placeholderRt.sizeDelta = Vector2.zero;

            var inputField = inputObj.AddComponent<TMP_InputField>();
            inputField.textComponent = textComp;
            inputField.placeholder = placeholderComp;
            inputField.textViewport = textAreaRt;
            inputField.targetGraphic = inputImage;

            return inputField;
        }

        private void BuildResultsScrollView(Transform parent)
        {
            var scrollObj = CreateUIObject("ScrollView", parent);
            var scrollImage = scrollObj.AddComponent<Image>();
            scrollImage.color = new Color32(0, 0, 0, 30);
            scrollImage.type = Image.Type.Sliced;
            var scrollLayout = scrollObj.AddComponent<LayoutElement>();
            scrollLayout.preferredHeight = MAX_VISIBLE_ITEMS * ITEM_HEIGHT;
            scrollLayout.flexibleHeight = 1;

            var viewport = CreateUIObject("Viewport", scrollObj.transform);
            var viewportRt = viewport.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = new Vector2(-12, 0);

            var viewportMask = viewport.AddComponent<RectMask2D>();

            contentContainer = CreateUIObject("Content", viewport.transform);
            var contentRt = contentContainer.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = Vector2.one;
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.sizeDelta = new Vector2(0, 0);

            var contentLayout = contentContainer.AddComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.spacing = 1;
            contentLayout.padding = new RectOffset(0, 0, 0, 0);

            var contentFitter = contentContainer.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scrollbarObj = CreateUIObject("Scrollbar", scrollObj.transform);
            var scrollbarRt = scrollbarObj.GetComponent<RectTransform>();
            scrollbarRt.anchorMin = new Vector2(1, 0);
            scrollbarRt.anchorMax = Vector2.one;
            scrollbarRt.offsetMin = new Vector2(-10, 0);
            scrollbarRt.offsetMax = Vector2.zero;

            var scrollbarImage = scrollbarObj.AddComponent<Image>();
            scrollbarImage.color = SCROLL_BG;

            var scrollbarHandle = CreateUIObject("Handle", scrollbarObj.transform);
            var handleImage = scrollbarHandle.AddComponent<Image>();
            handleImage.color = SCROLL_HANDLE;
            var handleRt = scrollbarHandle.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0, 0);
            handleRt.anchorMax = new Vector2(1, 1);
            handleRt.offsetMin = Vector2.zero;
            handleRt.offsetMax = Vector2.zero;

            var scrollbar = scrollbarObj.AddComponent<Scrollbar>();
            scrollbar.handleRect = handleRt;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.value = 1f;

            var kScroll = scrollObj.AddComponent<KScrollRect>();
            kScroll.content = contentRt;
            kScroll.viewport = viewportRt;
            kScroll.vertical = true;
            kScroll.horizontal = false;
            kScroll.movementType = ScrollRect.MovementType.Clamped;
            kScroll.verticalScrollbar = scrollbar;
            kScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            kScroll.verticalScrollbarSpacing = 2;
        }

        private void PerformSearch(string query)
        {
            currentResults = CommandIndex.Instance.FuzzySearch(query);
            selectedIndex = currentResults.Count > 0 ? 0 : -1;
            RebuildResultItems();
            UpdateStatusText();
        }

        private void RebuildResultItems()
        {
            foreach (var item in resultItems)
                Destroy(item);
            resultItems.Clear();

            for (int i = 0; i < currentResults.Count; i++)
            {
                var item = CreateResultItem(i);
                resultItems.Add(item);
            }

            var contentRt = contentContainer.GetComponent<RectTransform>();
            var contentSize = contentRt.sizeDelta;
            contentSize.y = currentResults.Count * (ITEM_HEIGHT + 1);
            contentRt.sizeDelta = contentSize;

            UpdateSelection();
        }

        private GameObject CreateResultItem(int index)
        {
            var entry = currentResults[index];
            var item = CreateUIObject("Result_" + index, contentContainer.transform);

            var itemRt = item.GetComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0, 1);
            itemRt.anchorMax = Vector2.one;
            itemRt.pivot = new Vector2(0.5f, 1);
            itemRt.sizeDelta = new Vector2(0, ITEM_HEIGHT);

            var itemLayout = item.AddComponent<LayoutElement>();
            itemLayout.preferredHeight = ITEM_HEIGHT;
            itemLayout.flexibleWidth = 1;

            var itemBg = item.AddComponent<Image>();
            itemBg.color = ITEM_NORMAL;

            var itemLayoutGroup = item.AddComponent<HorizontalLayoutGroup>();
            itemLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
            itemLayoutGroup.childForceExpandWidth = false;
            itemLayoutGroup.childForceExpandHeight = true;
            itemLayoutGroup.spacing = 6;
            itemLayoutGroup.padding = new RectOffset(6, 6, 2, 2);

            var badge = CreateUIObject("Badge", item.transform);
            var badgeImage = badge.AddComponent<Image>();
            if (BADGE_COLORS.TryGetValue(entry.Category, out var badgeColor))
                badgeImage.color = badgeColor;
            else
                badgeImage.color = Color.gray;
            var badgeLayout = badge.AddComponent<LayoutElement>();
            badgeLayout.preferredWidth = 4;
            badgeLayout.preferredHeight = 18;
            badgeLayout.flexibleWidth = 0;

            var nameObj = CreateUIObject("Name", item.transform);
            var nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = entry.DisplayName;
            nameText.fontSize = 14;
            nameText.color = TEXT_MAIN;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.raycastTarget = false;
            var nameLayout = nameObj.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1;

            var catObj = CreateUIObject("Category", item.transform);
            var catText = catObj.AddComponent<TextMeshProUGUI>();
            catText.text = entry.Category.ToString();
            catText.fontSize = 11;
            catText.color = TEXT_CATEGORY;
            catText.alignment = TextAlignmentOptions.Right;
            catText.raycastTarget = false;

            var trigger = item.AddComponent<EventTrigger>();

            var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            int capturedIndex = index;
            clickEntry.callback.AddListener(_ =>
            {
                selectedIndex = capturedIndex;
                ExecuteSelected();
            });
            trigger.triggers.Add(clickEntry);

            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            int capturedEnter = index;
            enterEntry.callback.AddListener(_ =>
            {
                selectedIndex = capturedEnter;
                UpdateSelection();
            });
            trigger.triggers.Add(enterEntry);

            return item;
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
            if (count == 0 && searchField.text.Length > 0)
                statusText.text = (string)CommandPalleteStrings.UI.COMMANDPALETTE.NO_RESULTS;
            else if (count > 0)
                statusText.text = string.Format("{0} result{1} | {2}", count, count != 1 ? "s" : "", hints);
            else
                statusText.text = hints;
        }

        private void ExecuteSelected()
        {
            if (selectedIndex >= 0 && selectedIndex < currentResults.Count)
            {
                var entry = currentResults[selectedIndex];
                Log.Debug("Executing command: {0} ({1})".F(entry.DisplayName, entry.Category));
                entry.Execute();
            }
            Deactivate();
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
