using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public static class MapUiFactory
    {
        public const string FullscreenMapCanvasName = "FullscreenMapCanvas";

        public static Canvas EnsureRuntimeCanvas()
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            return canvas != null ? canvas : CreateFullscreenMapCanvas();
        }

        public static Canvas CreateFullscreenMapCanvas()
        {
            GameObject canvasObject = new GameObject(FullscreenMapCanvasName, typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static FullscreenMapHudView CreateHudView(RectTransform parent)
        {
            GameObject root = new GameObject("FullscreenMapHud", typeof(RectTransform));
            RectTransform rootRect = root.transform as RectTransform;
            rootRect.SetParent(parent, false);
            Stretch(rootRect);
            FullscreenMapHudView hud = root.AddComponent<FullscreenMapHudView>();

            RectTransform locationPanel = CreateHudPanel(
                "StatusHudPanel",
                rootRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -18f),
                new Vector2(432f, 156f),
                GameUiPanelVariant.Dark);
            hud.locationPanel = locationPanel.GetComponent<PixelPanel>();
            hud.locationNameText = CreateText("LocationName", locationPanel, "地点  家里", 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            hud.stageText = CreateText("Stage", locationPanel, "阶段  -\n当前活动  地图探索", 12, FontStyle.Normal, TextAnchor.UpperLeft);
            hud.energyIcon = CreateHudIcon("EnergyIcon", locationPanel);
            hud.energyText = CreateText("Energy", locationPanel, "精力 -", 13, FontStyle.Bold, TextAnchor.MiddleLeft);
            hud.energyBarBack = CreateSolidImage("EnergyBarBack", locationPanel, new Color(0.05f, 0.04f, 0.03f, 0.62f));
            hud.energyBarFill = CreateSolidImage("EnergyBarFill", locationPanel, new Color(0.28f, 0.52f, 0.32f, 1f));
            hud.pressureIcon = CreateHudIcon("PressureIcon", locationPanel);
            hud.pressureText = CreateText("Pressure", locationPanel, "压力 -", 13, FontStyle.Bold, TextAnchor.MiddleLeft);
            hud.pressureBarBack = CreateSolidImage("PressureBarBack", locationPanel, new Color(0.05f, 0.04f, 0.03f, 0.62f));
            hud.pressureBarFill = CreateSolidImage("PressureBarFill", locationPanel, new Color(0.58f, 0.18f, 0.15f, 1f));
            hud.moodIcon = CreateHudIcon("MoodIcon", locationPanel);
            hud.moodText = CreateText("Mood", locationPanel, "心情 -", 13, FontStyle.Bold, TextAnchor.MiddleLeft);
            hud.moodBarBack = CreateSolidImage("MoodBarBack", locationPanel, new Color(0.05f, 0.04f, 0.03f, 0.62f));
            hud.moodBarFill = CreateSolidImage("MoodBarFill", locationPanel, new Color(0.92f, 0.70f, 0.25f, 1f));
            hud.parentRelationIcon = CreateHudIcon("ParentRelationIcon", locationPanel);
            hud.parentRelationText = CreateText("ParentRelation", locationPanel, "亲子 -", 14, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetChildRect(hud.locationNameText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(280f, 24f));
            SetChildRect(hud.stageText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -42f), new Vector2(-36f, 34f));
            SetChildRect(hud.energyIcon, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(332f, -14f), new Vector2(22f, 22f));
            SetChildRect(hud.pressureIcon, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(362f, -14f), new Vector2(22f, 22f));
            SetChildRect(hud.moodIcon, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(392f, -14f), new Vector2(22f, 22f));
            SetChildRect(hud.energyText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -82f), new Vector2(98f, 20f));
            SetChildRect(hud.energyBarBack, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(128f, -88f), new Vector2(270f, 10f));
            SetChildRect(hud.energyBarFill, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(128f, -88f), new Vector2(270f, 10f));
            SetChildRect(hud.pressureText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -106f), new Vector2(98f, 20f));
            SetChildRect(hud.pressureBarBack, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(128f, -112f), new Vector2(270f, 10f));
            SetChildRect(hud.pressureBarFill, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(128f, -112f), new Vector2(270f, 10f));
            SetChildRect(hud.moodText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -130f), new Vector2(98f, 20f));
            SetChildRect(hud.moodBarBack, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(128f, -136f), new Vector2(270f, 10f));
            SetChildRect(hud.moodBarFill, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(128f, -136f), new Vector2(270f, 10f));
            hud.parentRelationIcon.gameObject.SetActive(false);
            hud.parentRelationText.gameObject.SetActive(false);

            RectTransform statsPanel = CreateHudPanel(
                "ResourceHudPanel",
                rootRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -182f),
                new Vector2(264f, 42f),
                GameUiPanelVariant.Dark);
            hud.statsPanel = statsPanel.GetComponent<PixelPanel>();
            hud.moneyIcon = CreateHudIcon("MoneyIcon", statsPanel);
            hud.moneyText = CreateText("Money", statsPanel, "-", 16, FontStyle.Bold, TextAnchor.MiddleLeft);
            hud.timeIcon = CreateHudIcon("TimeIcon", statsPanel);
            hud.timeText = CreateText("Time", statsPanel, "-", 16, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetChildRect(hud.moneyIcon, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(18f, 18f));
            SetChildRect(hud.moneyText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -12f), new Vector2(78f, 18f));
            SetChildRect(hud.timeIcon, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(136f, -12f), new Vector2(18f, 18f));
            SetChildRect(hud.timeText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160f, -12f), new Vector2(78f, 18f));

            RectTransform questPanel = CreateHudPanel(
                "QuestHudPanel",
                rootRect,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-18f, -18f),
                new Vector2(300f, 108f),
                GameUiPanelVariant.Dark);
            hud.questPanel = questPanel.GetComponent<PixelPanel>();
            hud.questTitleText = CreateText("QuestTitle", questPanel, "任务", 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            hud.questMainText = CreateText("QuestMain", questPanel, "主线：推进当前阶段", 14, FontStyle.Normal, TextAnchor.MiddleLeft);
            hud.questSubText = CreateText("QuestSub", questPanel, "支线：完成家中活动", 14, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetChildRect(hud.questTitleText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -14f), new Vector2(-40f, 22f));
            SetChildRect(hud.questMainText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -42f), new Vector2(-40f, 26f));
            SetChildRect(hud.questSubText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -72f), new Vector2(-40f, 28f));

            RectTransform quickbarPanel = CreateHudPanel(
                "QuickbarHudPanel",
                rootRect,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 18f),
                new Vector2(572f, 62f),
                GameUiPanelVariant.Dark);
            hud.quickbarPanel = quickbarPanel.GetComponent<PixelPanel>();
            hud.quickbarSlotTexts = new Text[10];
            hud.quickbarSlotIcons = new Image[10];
            for (int i = 0; i < hud.quickbarSlotTexts.Length; i++)
            {
                CreateQuickbarSlot(quickbarPanel, hud, i);
            }

            RectTransform actionPanel = CreateHudPanel(
                "MapActionHudPanel",
                rootRect,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-18f, 18f),
                new Vector2(420f, 58f),
                GameUiPanelVariant.Dark);
            hud.actionPanel = actionPanel.GetComponent<PixelPanel>();
            hud.statusButton = CreateHudButton("StatusButton", actionPanel, "状态", out hud.statusButtonIcon);
            hud.inventoryButton = CreateHudButton("InventoryButton", actionPanel, "背包", out hud.inventoryButtonIcon);
            hud.deckButton = CreateHudButton("DeckButton", actionPanel, "卡组", out hud.deckButtonIcon);
            hud.settingsButton = CreateHudButton("SettingsButton", actionPanel, "设置", out hud.settingsButtonIcon);
            hud.returnButton = CreateHudButton("ReturnButton", actionPanel, "返回", out hud.returnButtonIcon);
            LayoutActionButtons(hud);
            actionPanel.gameObject.SetActive(false);
            hud.SetOptionalButtonsInteractable(false);
            return hud;
        }

        public static TimeActionHudView CreateTimeActionHudView(
            RectTransform parent,
            UIThemeConfig theme = null,
            bool compactSchoolLayout = false)
        {
            GameObject root = new GameObject("TimeActionHud", typeof(RectTransform));
            RectTransform rootRect = root.transform as RectTransform;
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = new Vector2(-18f, -18f);
            rootRect.sizeDelta = new Vector2(382f, 142f);

            TimeActionHudView view = root.AddComponent<TimeActionHudView>();

            view.dateLogoBackground = CreateHudIcon("DateLogoBackground", rootRect);
            SetChildRect(view.dateLogoBackground, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(140f, 140f));

            view.dayText = CreateText("DayText", rootRect, "-", 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetChildRect(view.dayText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -26f), new Vector2(106f, 28f));

            view.remainingDailyText = CreateText("RemainingDailyText", rootRect, "今日剩余 12/12", 13, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetChildRect(view.remainingDailyText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -64f), new Vector2(112f, 22f));

            view.periodBackground = CreateHudIcon("PeriodBackground", rootRect);
            SetChildRect(view.periodBackground, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(130f, -6f), new Vector2(108f, 108f));

            view.periodIcon = CreateHudIcon("PeriodIcon", rootRect);
            SetChildRect(view.periodIcon, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(158f, -32f), new Vector2(52f, 52f));

            view.periodText = CreateText("PeriodText", rootRect, "早晨", 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetChildRect(view.periodText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(132f, -112f), new Vector2(104f, 24f));

            view.actionPointText = CreateText("ActionPointText", rootRect, "行动值 3/3", 16, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetChildRect(view.actionPointText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(252f, -10f), new Vector2(124f, 24f));

            view.actionPointIcons = new Image[TimeActionState.ActionPointsPerPeriod];
            for (int i = 0; i < view.actionPointIcons.Length; i++)
            {
                Image point = CreateHudIcon("ActionPoint_" + i, rootRect);
                SetChildRect(point, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(250f + i * 42f, -42f), new Vector2(36f, 36f));
                view.actionPointIcons[i] = point;
            }

            view.sleepWarningText = CreateText("SleepWarningText", rootRect, string.Empty, 12, FontStyle.Bold, TextAnchor.UpperLeft);
            SetChildRect(view.sleepWarningText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(250f, -86f), new Vector2(126f, 48f));

            UIThemeConfig resolvedTheme = GameUiStyle.ResolveTheme(theme);
            GameUiStyle.ApplyPixelText(view.dayText, resolvedTheme, GameUiTextStyle.Subtitle, true);
            GameUiStyle.ApplyPixelText(view.periodText, resolvedTheme, GameUiTextStyle.ButtonText, true);
            GameUiStyle.ApplyPixelText(view.actionPointText, resolvedTheme, GameUiTextStyle.StatText, true);
            GameUiStyle.ApplyPixelText(view.remainingDailyText, resolvedTheme, GameUiTextStyle.SmallText, true);
            GameUiStyle.ApplyPixelText(view.sleepWarningText, resolvedTheme, GameUiTextStyle.SmallText, true);
            if (view.sleepWarningText != null)
            {
                view.sleepWarningText.color = resolvedTheme.danger;
            }

            view.ConfigurePresentation(resolvedTheme, compactSchoolLayout, compactSchoolLayout);
            view.Refresh();
            return view;
        }

        public static LocationToastView CreateLocationToastView(RectTransform parent, UIThemeConfig theme = null)
        {
            RectTransform rootRect = CreateHudPanel(
                "LocationToastView",
                parent,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -22f),
                new Vector2(380f, 68f),
                GameUiPanelVariant.Dark);
            LocationToastView view = rootRect.gameObject.AddComponent<LocationToastView>();
            view.panel = rootRect.GetComponent<PixelPanel>();
            view.canvasGroup = rootRect.gameObject.AddComponent<CanvasGroup>();
            view.locationText = CreateText("LocationText", rootRect, "学校", 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            view.stageText = CreateText("StageText", rootRect, string.Empty, 13, FontStyle.Normal, TextAnchor.MiddleCenter);
            SetChildRect(view.locationText, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 10f), new Vector2(-28f, -30f));
            SetChildRect(view.stageText, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -17f), new Vector2(-28f, -38f));
            view.canvasGroup.alpha = 0f;
            view.Configure(theme);
            rootRect.gameObject.SetActive(false);
            return view;
        }

        public static MapInteractionPromptView CreatePromptView(RectTransform parent)
        {
            RectTransform rootRect = CreateHudPanel(
                "MapInteractionPrompt",
                parent,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 92f),
                new Vector2(560f, 36f),
                GameUiPanelVariant.Dark);
            GameObject root = rootRect.gameObject;
            MapInteractionPromptView prompt = root.AddComponent<MapInteractionPromptView>();
            prompt.panelRoot = root;
            prompt.panel = root.GetComponent<PixelPanel>();
            prompt.showDefaultWhenIdle = false;
            prompt.promptText = CreateText("PromptText", rootRect, string.Empty, 16, FontStyle.Normal, TextAnchor.MiddleCenter);
            RectTransform promptRect = prompt.promptText.transform as RectTransform;
            promptRect.anchorMin = Vector2.zero;
            promptRect.anchorMax = Vector2.one;
            promptRect.offsetMin = new Vector2(24f, 6f);
            promptRect.offsetMax = new Vector2(-24f, -6f);
            prompt.ApplyStyle();
            return prompt;
        }

        public static MapFunctionMenuView CreateFunctionMenuView(RectTransform parent)
        {
            GameObject root = new GameObject("MapFunctionMenuView", typeof(RectTransform));
            MapFunctionMenuView menu = root.AddComponent<MapFunctionMenuView>();
            menu.BuildDefaultLayout(parent);
            return menu;
        }

        public static MapInteractionConfirmView CreateInteractionConfirmView(RectTransform parent)
        {
            GameObject root = new GameObject("MapInteractionConfirmView", typeof(RectTransform));
            MapInteractionConfirmView confirm = root.AddComponent<MapInteractionConfirmView>();
            confirm.BuildDefaultLayout(parent);
            return confirm;
        }

        public static MapDialogueView CreateDialogueView(RectTransform parent)
        {
            GameObject root = new GameObject("MapDialogueView", typeof(RectTransform));
            MapDialogueView dialogue = root.AddComponent<MapDialogueView>();
            dialogue.BuildDefaultLayout(parent);
            return dialogue;
        }

        public static MapShopView CreateShopView(RectTransform parent)
        {
            GameObject root = new GameObject("MapShopView", typeof(RectTransform));
            MapShopView shop = root.AddComponent<MapShopView>();
            shop.BuildDefaultLayout(parent);
            return shop;
        }

        public static MapActivityResultView CreateActivityResultView(RectTransform parent)
        {
            GameObject root = new GameObject("MapActivityResultView", typeof(RectTransform));
            MapActivityResultView result = root.AddComponent<MapActivityResultView>();
            result.BuildDefaultLayout(parent);
            return result;
        }

        private static Button CreateHudButton(string name, RectTransform parent, string label, out Image icon)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = buttonObject.transform as RectTransform;
            rect.SetParent(parent, false);
            Image image = buttonObject.AddComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            icon = CreateHudIcon("Icon", rect);
            RectTransform iconRect = icon.transform as RectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(8f, 0f);
            iconRect.sizeDelta = new Vector2(18f, 18f);

            Text text = CreateText("Text", rect, label, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            RectTransform textRect = text.transform as RectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(28f, 0f);
            textRect.offsetMax = new Vector2(-6f, 0f);

            PixelButton pixelButton = buttonObject.AddComponent<PixelButton>();
            pixelButton.background = image;
            pixelButton.labelText = text;
            pixelButton.ApplyStyle();
            return button;
        }

        private static void CreateQuickbarSlot(RectTransform parent, FullscreenMapHudView hud, int index)
        {
            const float slotSize = 44f;
            const float spacing = 8f;
            float x = 14f + index * (slotSize + spacing);

            RectTransform slotRect = CreateHudPanel(
                "QuickbarSlot_" + index,
                parent,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(x, 0f),
                new Vector2(slotSize, 42f),
                GameUiPanelVariant.Dark);

            Image icon = CreateHudIcon("Icon", slotRect);
            SetChildRect(icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(2f, 3f), new Vector2(24f, 24f));

            Image slotBackground = slotRect.GetComponent<Image>();
            if (slotBackground != null)
            {
                slotBackground.raycastTarget = true;
            }

            QuickbarTooltipTarget tooltip = slotRect.gameObject.AddComponent<QuickbarTooltipTarget>();
            tooltip.tooltipText = string.Empty;

            Text label = CreateText("Label", slotRect, string.Empty, 12, FontStyle.Bold, TextAnchor.LowerLeft);
            RectTransform labelRect = label.transform as RectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 4f);
            labelRect.offsetMax = new Vector2(-6f, -4f);

            hud.quickbarSlotIcons[index] = icon;
            hud.quickbarSlotTexts[index] = label;
        }

        private static Image CreateHudIcon(string name, RectTransform parent)
        {
            GameObject iconObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = iconObject.transform as RectTransform;
            rect.SetParent(parent, false);
            Image image = iconObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.enabled = false;
            return image;
        }

        private static Image CreateSolidImage(string name, RectTransform parent, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = imageObject.transform as RectTransform;
            rect.SetParent(parent, false);
            Image image = imageObject.AddComponent<Image>();
            image.sprite = GameUiStyle.GetSolidSprite();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateHudPanel(
            string name,
            RectTransform parent,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            GameUiPanelVariant variant)
        {
            GameObject panelObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = panelObject.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Image image = panelObject.AddComponent<Image>();
            image.raycastTarget = false;

            PixelPanel panel = panelObject.AddComponent<PixelPanel>();
            panel.background = image;
            panel.variant = variant;
            panel.ApplyStyle();
            return rect;
        }

        private static Text CreateText(string name, RectTransform parent, string value, int fontSize, FontStyle fontStyle, TextAnchor anchor)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = textObject.transform as RectTransform;
            rect.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.text = value;
            text.alignment = anchor;
            text.raycastTarget = false;
            GameUiStyle.ApplyTextFromFontSpec(text, null, fontSize, fontStyle);
            return text;
        }

        private static void LayoutActionButtons(FullscreenMapHudView hud)
        {
            SetHudButtonRect(hud.statusButton, 14f, 0f, 72f);
            SetHudButtonRect(hud.inventoryButton, 94f, 0f, 72f);
            SetHudButtonRect(hud.deckButton, 174f, 0f, 72f);
            SetHudButtonRect(hud.settingsButton, 254f, 0f, 72f);
            SetHudButtonRect(hud.returnButton, 334f, 0f, 72f);
        }

        private static void SetChildRect(Text text, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (text == null)
            {
                return;
            }

            RectTransform rect = text.transform as RectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void SetChildRect(Image image, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (image == null)
            {
                return;
            }

            RectTransform rect = image.transform as RectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void SetHudButtonRect(Button button, float x, float y, float width)
        {
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.transform as RectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, 40f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
