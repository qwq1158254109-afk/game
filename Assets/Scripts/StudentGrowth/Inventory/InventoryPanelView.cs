using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    /// <summary>
    /// 背包面板：5×4 网格 + 右侧详情 + 使用/丢弃。
    /// 全部用代码搭建（不依赖预制体），主题色沿用地图 UI 的深棕底 + 米色字。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryPanelView : MonoBehaviour
    {
        private const int Columns = 5;
        private const int Rows = 4;
        private const int SlotSize = 96;
        private const int SlotGap = 10;
        private const string GridTitle = "背包";

        private static readonly Color PanelColor = new Color(0.16f, 0.11f, 0.07f, 0.96f);
        private static readonly Color SlotColor = new Color(0.28f, 0.19f, 0.12f, 1f);
        private static readonly Color SlotSelected = new Color(0.45f, 0.32f, 0.18f, 1f);
        private static readonly Color TextColor = new Color(0.93f, 0.89f, 0.80f, 1f);

        private static InventoryPanelView instance;

        private GameSessionState session;
        private GameObject root;
        private readonly List<Button> slots = new List<Button>();
        private readonly List<Image> slotIcons = new List<Image>();
        private readonly List<Text> slotCounts = new List<Text>();
        private Text detailName;
        private Text detailInfo;
        private Text emptyHint;
        private Button useButton;
        private Button dropButton;
        private ItemData selected;

        public bool IsOpen => root != null && root.activeSelf;

        public static InventoryPanelView Instance => instance;

        public static InventoryPanelView Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            GameObject host = new GameObject("[UI] InventoryPanel");
            instance = host.AddComponent<InventoryPanelView>();
            instance.Build();
            return instance;
        }

        public void Open(GameSessionState state)
        {
            session = state;
            if (root != null)
            {
                root.SetActive(true);
            }

            selected = null;
            Refresh();
        }

        public void Close()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        public void Toggle(GameSessionState state)
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open(state);
            }
        }

        private void OnDestroy()
        {
            InventoryModel.Changed -= Refresh;
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            root = CreateRect("Window", transform, new Vector2(720f, 520f), Vector2.zero).gameObject;
            Image bg = root.AddComponent<Image>();
            bg.color = PanelColor;

            CreateText("Title", root.transform, GridTitle, 26, new Vector2(0f, 226f), new Vector2(680f, 36f), TextAnchor.MiddleLeft);

            GameObject grid = CreateRect("Grid", root.transform, new Vector2(Columns * (SlotSize + SlotGap), Rows * (SlotSize + SlotGap)), new Vector2(-120f, 24f)).gameObject;
            for (int i = 0; i < Columns * Rows; i++)
            {
                int index = i;
                int col = i % Columns;
                int row = i / Columns;
                Vector2 pos = new Vector2(
                    col * (SlotSize + SlotGap) - (Columns - 1) * (SlotSize + SlotGap) * 0.5f,
                    -(row * (SlotSize + SlotGap) - (Rows - 1) * (SlotSize + SlotGap) * 0.5f));
                CreateSlot(grid.transform, pos, index);
            }

            // 右侧详情
            GameObject detail = CreateRect("Detail", root.transform, new Vector2(250f, 380f), new Vector2(220f, 30f)).gameObject;
            CreateTextOn(detail.transform, "占位", string.Empty, 18, new Vector2(0f, 170f), new Vector2(240f, 60f), TextAnchor.UpperLeft, out detailName);
            CreateTextOn(detail.transform, "Info", string.Empty, 15, new Vector2(0f, 40f), new Vector2(240f, 200f), TextAnchor.UpperLeft, out detailInfo);

            useButton = CreateButton("使用", root.transform, new Vector2(220f, -170f), new Vector2(110f, 40f), OnUse);
            dropButton = CreateButton("丢弃", root.transform, new Vector2(220f, -220f), new Vector2(110f, 40f), OnDrop);
            CreateButton("关闭", root.transform, new Vector2(0f, -226f), new Vector2(120f, 40f), Close);

            CreateTextOn(root.transform, "Empty", "背包是空的。", 18, new Vector2(-120f, 0f), new Vector2(520f, 40f), TextAnchor.MiddleCenter, out emptyHint);

            root.SetActive(false);
            InventoryModel.Changed += Refresh;
        }

        private void CreateSlot(Transform parent, Vector2 pos, int index)
        {
            GameObject slot = CreateRect("Slot" + index, parent, new Vector2(SlotSize, SlotSize), pos).gameObject;
            Image image = slot.AddComponent<Image>();
            image.color = SlotColor;
            Button button = slot.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => SelectSlot(index));

            GameObject iconGo = CreateRect("Icon", slot.transform, new Vector2(SlotSize - 24f, SlotSize - 24f), Vector2.zero).gameObject;
            Image icon = iconGo.AddComponent<Image>();
            icon.enabled = false;

            CreateTextOn(slot.transform, "Count", string.Empty, 14, new Vector2(SlotSize * 0.5f - 16f, -SlotSize * 0.5f + 12f), new Vector2(48f, 20f), TextAnchor.MiddleRight, out Text count);

            slots.Add(button);
            slotIcons.Add(icon);
            slotCounts.Add(count);
        }

        private void SelectSlot(int index)
        {
            IReadOnlyList<InventoryEntry> entries = InventoryModel.Entries(session);
            selected = index >= 0 && index < entries.Count ? entries[index].item : null;
            Refresh();
        }

        private void Refresh()
        {
            if (root == null)
            {
                return;
            }

            IReadOnlyList<InventoryEntry> entries = InventoryModel.Entries(session);
            if (selected != null && InventoryModel.CountOf(session, selected) == 0)
            {
                selected = null;
            }

            if (selected == null && entries.Count > 0)
            {
                selected = entries[0].item;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                bool used = i < entries.Count;
                InventoryEntry entry = used ? entries[i] : default;
                Sprite icon = used ? ResolveIcon(entry.item) : null;

                slotIcons[i].enabled = icon != null;
                slotIcons[i].sprite = icon;
                slotCounts[i].text = used && entry.count > 1 ? entry.count.ToString() : string.Empty;
                slots[i].image.color = used && entry.item == selected ? SlotSelected : SlotColor;
                slots[i].interactable = used;
            }

            emptyHint.enabled = entries.Count == 0;

            bool hasSelection = selected != null;
            detailName.text = hasSelection ? DisplayName(selected) : "未选择";
            detailInfo.text = hasSelection ? BuildDetail(selected) : "点上面的格子查看道具。";
            useButton.interactable = hasSelection && CanUseOnMap(selected);
            dropButton.interactable = hasSelection;
        }

        private string BuildDetail(ItemData item)
        {
            string desc = string.IsNullOrEmpty(item.description) ? "（无描述）" : item.description;
            int count = InventoryModel.CountOf(session, item);
            return string.Format("{0}\n\n数量：{1}\n价格：{2}", desc, count, item.price);
        }

        private void OnUse()
        {
            if (selected == null)
            {
                return;
            }

            string message;
            bool ok = InventoryModel.TryUse(session, selected, out message);
            detailInfo.text = message;
            if (ok)
            {
                Sfx.Ui.Confirm();
            }
            else
            {
                Sfx.Ui.Error();
            }

            Refresh();
        }

        private void OnDrop()
        {
            if (selected == null)
            {
                return;
            }

            InventoryModel.Drop(session, selected);
            Sfx.Ui.Back();
            Refresh();
        }

        // ── 小工具 ────────────────────────────────────────────────
        private static bool CanUseOnMap(ItemData item)
        {
            return item != null && (item.itemType == ItemType.Consumable || item.canUseInStageScreen);
        }

        private static Sprite ResolveIcon(ItemData item)
        {
            return item != null ? item.icon : null;
        }

        private static string DisplayName(ItemData item)
        {
            if (item == null)
            {
                return "未知道具";
            }

            return string.IsNullOrEmpty(item.displayName) ? item.itemId : item.displayName;
        }

        private static Font UiFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return rect;
        }

        private static Text CreateText(string name, Transform parent, string content, int size, Vector2 pos, Vector2 rectSize, TextAnchor anchor)
        {
            Text text;
            CreateTextOn(parent, name, content, size, pos, rectSize, anchor, out text);
            return text;
        }

        private static void CreateTextOn(Transform parent, string name, string content, int size, Vector2 pos, Vector2 rectSize, TextAnchor anchor, out Text text)
        {
            RectTransform rect = CreateRect(name, parent, rectSize, pos);
            text = rect.gameObject.AddComponent<Text>();
            text.font = UiFont;
            text.text = content;
            text.fontSize = size;
            text.color = TextColor;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private Button CreateButton(string label, Transform parent, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rect = CreateRect("Btn_" + label, parent, size, pos);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = SlotColor;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            Text text;
            CreateTextOn(rect, "Label", label, 18, Vector2.zero, size, TextAnchor.MiddleCenter, out text);
            return button;
        }
    }
}
