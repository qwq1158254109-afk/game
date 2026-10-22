using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    /// <summary>
    /// 道具栏：右下角 5 格，按 1~5 直接使用；图标 + 数量 + 键位提示。
    /// 自动取背包里前 5 种道具（手动摆放是后续待办）。
    /// 2026-10-22：配色/字体改走 UIThemeConfig，与背包面板、地图 HUD 统一。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemBarView : MonoBehaviour
    {
        private const int SlotCount = 5;
        private const int SlotSize = 58;
        private const int Gap = 8;
        private const float BottomOffset = 96f;
        private const float RightOffset = 24f;

        private static ItemBarView instance;

        private UIThemeConfig theme;
        private GameSessionState session;
        private readonly List<Image> backgrounds = new List<Image>();
        private readonly List<Image> icons = new List<Image>();
        private readonly List<Text> counts = new List<Text>();
        private readonly List<ItemData> bound = new List<ItemData>();

        public static ItemBarView Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            GameObject host = new GameObject("[UI] ItemBar");
            instance = host.AddComponent<ItemBarView>();
            instance.Build();
            return instance;
        }

        public void Bind(GameSessionState state)
        {
            session = state;
            Refresh();
        }

        public ItemData ItemAt(int index)
        {
            return index >= 0 && index < bound.Count ? bound[index] : null;
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
            theme = GameUiStyle.ResolveTheme(null);

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            for (int i = 0; i < SlotCount; i++)
            {
                float x = -(SlotCount - 1 - i) * (SlotSize + Gap) - RightOffset;
                RectTransform slot = CreateRect("Slot" + i, transform, new Vector2(SlotSize, SlotSize));
                slot.anchorMin = new Vector2(1f, 0f);
                slot.anchorMax = new Vector2(1f, 0f);
                slot.pivot = new Vector2(1f, 0f);
                slot.anchoredPosition = new Vector2(x + SlotSize, BottomOffset);

                Image slotImage = slot.gameObject.AddComponent<Image>();
                ApplySlotSprite(slotImage, false);

                int index = i;
                Button button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = slotImage;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => InventoryController.UseSlot(index));

                RectTransform iconRect = CreateRect("Icon", slot, new Vector2(SlotSize - 14f, SlotSize - 14f));
                Image icon = iconRect.gameObject.AddComponent<Image>();
                icon.enabled = false;
                icon.raycastTarget = false;
                icon.preserveAspect = true;

                Text key = CreateText("Key", slot, (i + 1).ToString(), new Vector2(-SlotSize * 0.5f + 9f, SlotSize * 0.5f - 10f));
                Text count = CreateText("Count", slot, string.Empty, new Vector2(SlotSize * 0.5f - 9f, -SlotSize * 0.5f + 10f));

                backgrounds.Add(slotImage);
                icons.Add(icon);
                counts.Add(count);
                _ = key;
            }

            InventoryModel.Changed += Refresh;
        }

        private void Refresh()
        {
            IReadOnlyList<InventoryEntry> entries = InventoryModel.Entries(session);
            bound.Clear();
            for (int i = 0; i < entries.Count && bound.Count < SlotCount; i++)
            {
                bound.Add(entries[i].item);
            }

            for (int i = 0; i < SlotCount; i++)
            {
                bool used = i < bound.Count && bound[i] != null;
                icons[i].enabled = used && bound[i].icon != null;
                icons[i].sprite = used ? bound[i].icon : null;
                counts[i].text = string.Empty;
                ApplySlotSprite(backgrounds[i], used);
            }

            // 数量只在同款 >1 时显示
            for (int i = 0; i < bound.Count; i++)
            {
                int count = InventoryModel.CountOf(session, bound[i]);
                counts[i].text = count > 1 ? count.ToString() : string.Empty;
            }
        }

        private void ApplySlotSprite(Image target, bool used)
        {
            if (theme.quickbarSlotSprite != null)
            {
                GameUiStyle.ApplyPanelSprite(target, theme.quickbarSlotSprite, theme.panelDarkBackground);
                target.color = used ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                return;
            }

            GameUiStyle.ApplyPanelSprite(target, null, theme.panelDarkBackground);
            target.color = used
                ? theme.panelDarkBackground
                : new Color(theme.panelDarkBackground.r, theme.panelDarkBackground.g, theme.panelDarkBackground.b, 0.45f);
        }

        private Text CreateText(string name, Transform parent, string content, Vector2 pos)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(28f, 18f));
            rect.anchoredPosition = pos;

            Text text = rect.gameObject.AddComponent<Text>();
            text.text = content;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            GameUiStyle.ApplyPixelText(text, theme, GameUiTextStyle.SmallText, true);
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }
    }
}
