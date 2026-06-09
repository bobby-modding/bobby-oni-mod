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
        private LocText statusLabel;
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

            PerformSearch("");
            if (searchField != null)
            {
                searchField.ActivateInputField();
                searchField.Select();
            }
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

        private void BuildUI()
        {
            // Background overlay — click to close
            var bg = PUIElements.CreateUI(gameObject, "Background", true,
                PUIAnchoring.Stretch, PUIAnchoring.Stretch);
            bg.AddComponent<Image>().color = OVERLAY_BG;
            var bgTrigger = bg.AddComponent<EventTrigger>();
            var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener(_ => Deactivate());
            bgTrigger.triggers.Add(clickEntry);

            // Panel — centered container
            var panelObj = PUIElements.CreateUI(gameObject, "Panel", true,
                PUIAnchoring.Stretch, PUIAnchoring.Stretch);
            var panelRt = panelObj.rectTransform();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(PANEL_WIDTH, 0);
            panelRt.anchoredPosition = new Vector2(0, 80);
            panelObj.AddComponent<Image>().color = PANEL_BG;

            // Panel vertical layout
            panelObj.AddComponent<BoxLayoutGroup>().Params = new BoxLayoutParams
            {
                Direction = PanelDirection.Vertical,
                Alignment = TextAnchor.UpperCenter,
                Spacing = 6,
                Margin = new RectOffset(PANEL_PADDING, PANEL_PADDING,
                    PANEL_PADDING, PANEL_PADDING)
            };

            // Title
            new PLabel("Title")
            {
                Text = (string)CommandPalleteStrings.UI.COMMANDPALETTE.NAME,
                TextStyle = PUITuning.Fonts.UILightStyle,
                TextAlignment = TextAnchor.MiddleCenter,
                FlexSize = new Vector2(1, 0),
                DynamicSize = true
            }.Build().SetParent(panelObj);

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

            // Status text
            var label = new PLabel("Status")
            {
                Text = "",
                TextStyle = PUITuning.Fonts.UILightStyle,
                TextAlignment = TextAnchor.MiddleCenter,
                FlexSize = new Vector2(1, 0),
                DynamicSize = true
            };
            statusLabel = label.Build().GetComponent<LocText>();
            statusLabel.gameObject.SetParent(panelObj);

            // Scrollable results: PScrollPane with PPanel child
            var resultsPanel = new PPanel("ResultsContent")
            {
                Direction = PanelDirection.Vertical,
                Alignment = TextAnchor.UpperCenter,
                Spacing = 1,
                DynamicSize = true,
                FlexSize = Vector2.zero
            };
            resultsPanel.AddOnRealize(obj => contentContainer = obj);

            var scrollPane = new PScrollPane("ScrollView")
            {
                Child = resultsPanel,
                ScrollVertical = true,
                AlwaysShowVertical = true,
                FlexSize = new Vector2(1, 1),
                BackColor = SCROLL_BG
            };
            var scrollGo = scrollPane.Build();
            scrollGo.SetParent(panelObj);
            scrollGo.AddComponent<LayoutElement>()
                .preferredHeight = MAX_VISIBLE_ITEMS * ITEM_HEIGHT;
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
            foreach (var item in resultItems)
                Destroy(item);
            resultItems.Clear();

            for (int i = 0; i < currentResults.Count; i++)
            {
                var item = CreateResultItem(i);
                resultItems.Add(item);
            }

            UpdateSelection();
        }

        private GameObject CreateResultItem(int index)
        {
            var entry = currentResults[index];

            var entryBadgeColor = BADGE_COLORS.TryGetValue(entry.Category, out var bc)
                ? bc : Color.gray;

            var row = new PPanel("Result_" + index)
            {
                Direction = PanelDirection.Horizontal,
                Alignment = TextAnchor.MiddleLeft,
                Spacing = 6,
                Margin = new RectOffset(6, 6, 2, 2),
                BackColor = ITEM_NORMAL,
                FlexSize = new Vector2(1, 0),
                DynamicSize = true
            };

            // Badge
            row.AddChild(new PPanel("Badge")
            {
                BackColor = entryBadgeColor,
                FlexSize = new Vector2(0, 0),
                DynamicSize = false
            });

            // Name
            row.AddChild(new PLabel("Name")
            {
                Text = entry.DisplayName,
                TextStyle = PUITuning.Fonts.UILightStyle,
                TextAlignment = TextAnchor.MiddleLeft,
                FlexSize = new Vector2(1, 0),
                DynamicSize = true
            });

            // Category
            row.AddChild(new PLabel("Category")
            {
                Text = entry.Category.ToString(),
                TextStyle = PUITuning.Fonts.UILightStyle,
                TextAlignment = TextAnchor.MiddleRight,
                FlexSize = new Vector2(0, 0),
                DynamicSize = true
            });

            var rowGo = row.Build();
            rowGo.SetParent(contentContainer);

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
