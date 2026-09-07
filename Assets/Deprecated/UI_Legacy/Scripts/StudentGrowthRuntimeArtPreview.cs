using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    [Obsolete("Deprecated preview UI. Keep only as a historical visual reference; do not attach to production scenes.")]
    public class StudentGrowthRuntimeArtPreview : MonoBehaviour
    {
        private const string PreviewRootName = "StudentGrowth Runtime UI";
        private const float OuterMargin = 28f;
        private const float PanelGap = 20f;
        private const float TopStatusBarHeight = 92f;
        private const float BottomCardPanelHeight = 260f;
        private const float LeftStatsPanelWidth = 340f;
        private const float RightTaskPanelWidth = 380f;
        private const float PanelBorderThickness = 3f;

        private static readonly string[] HandCardIds =
        {
            "card_study_class_focus",
            "card_study_math_practice",
            "card_rest_good_sleep",
            "card_social_help_classmate",
            "card_family_talk_parent"
        };

        private static readonly string[] ItemShortcutIds =
        {
            "item_energy_drink",
            "item_lucky_pen",
            "item_wrong_book",
            "item_parent_note"
        };

        private readonly Color boardFill = new Color(0.055f, 0.12f, 0.105f, 0.96f);
        private readonly Color panelFill = new Color(0.075f, 0.095f, 0.095f, 0.96f);
        private readonly Color deskFill = new Color(0.18f, 0.125f, 0.085f, 0.96f);
        private readonly Color greenBorder = new Color(0.43f, 0.65f, 0.52f, 1f);
        private readonly Color amberBorder = new Color(0.76f, 0.64f, 0.36f, 1f);
        private readonly Color paperFill = new Color(0.73f, 0.66f, 0.48f, 0.95f);
        private readonly Color textLight = new Color(0.86f, 0.98f, 0.82f, 1f);
        private readonly Color textMuted = new Color(0.63f, 0.77f, 0.66f, 1f);
        private readonly Color textAmber = new Color(0.96f, 0.82f, 0.52f, 1f);
        private readonly Color warningRed = new Color(0.62f, 0.23f, 0.22f, 1f);

        private Font runtimeFont;
        private Sprite solidSprite;

        private static void CreatePreviewIfSceneHasNoStudentGrowthView()
        {
            if (GameObject.Find(PreviewRootName) != null)
            {
                return;
            }

            GameObject root = new GameObject(PreviewRootName);
            root.AddComponent<StudentGrowthRuntimeArtPreview>();
        }

        private void Start()
        {
            BuildCanvasInterface();
        }

        private void BuildCanvasInterface()
        {
            Canvas canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 0;

            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            RectTransform rootRect = gameObject.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            RectTransform backgroundLayer = CreateFullLayer("BackgroundLayer", rootRect);
            RectTransform mainPanelLayer = CreateFullLayer("MainPanelLayer", rootRect);
            RectTransform contentLayer = CreateFullLayer("ContentLayer", rootRect);
            RectTransform overlayLayer = CreateFullLayer("OverlayLayer", rootRect);

            BuildBackgroundLayer(backgroundLayer);

            RectTransform topStatusBar = CreatePanel(
                "TopStatusBar",
                mainPanelLayer,
                new Color(0.055f, 0.075f, 0.075f, 0.97f),
                greenBorder);
            SetTopStretch(topStatusBar, OuterMargin, OuterMargin, OuterMargin, TopStatusBarHeight);

            RectTransform bottomCardPanel = CreatePanel("BottomCardPanel", mainPanelLayer, deskFill, greenBorder);
            SetBottomStretch(bottomCardPanel, OuterMargin, OuterMargin, OuterMargin, BottomCardPanelHeight);

            float middleTop = OuterMargin + TopStatusBarHeight + PanelGap;
            float middleBottom = OuterMargin + BottomCardPanelHeight + PanelGap;

            RectTransform leftStatsPanel = CreatePanel("LeftStatsPanel", mainPanelLayer, boardFill, greenBorder);
            SetLeftStretch(leftStatsPanel, OuterMargin, middleTop, middleBottom, LeftStatsPanelWidth);

            RectTransform rightTaskPanel = CreatePanel("RightTaskPanel", mainPanelLayer, boardFill, amberBorder);
            SetRightStretch(rightTaskPanel, OuterMargin, middleTop, middleBottom, RightTaskPanelWidth);

            RectTransform centerScenePanel = CreatePanel(
                "CenterScenePanel",
                mainPanelLayer,
                new Color(0.055f, 0.075f, 0.075f, 0.96f),
                greenBorder);
            SetStretch(
                centerScenePanel,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(OuterMargin + LeftStatsPanelWidth + PanelGap, middleBottom),
                new Vector2(-(OuterMargin + RightTaskPanelWidth + PanelGap), -middleTop));

            RectTransform characterArea = CreateEmptyRect("CharacterArea", contentLayer);
            CopyRectTransform(centerScenePanel, characterArea);

            RectTransform cardArea = CreateEmptyRect("CardArea", contentLayer);
            SetBottomStretch(cardArea, OuterMargin + 300f, 260f, OuterMargin + 34f, 188f);

            RectTransform itemArea = CreateEmptyRect("ItemArea", contentLayer);
            SetBottomStretch(itemArea, OuterMargin + 22f, 1380f, OuterMargin + 46f, 170f);

            BuildTopStatusBar(topStatusBar);
            BuildLeftStatsPanel(leftStatsPanel);
            BuildCenterScenePanel(centerScenePanel, characterArea);
            BuildRightTaskPanel(rightTaskPanel);
            BuildBottomCardPanel(bottomCardPanel, cardArea, itemArea);
            BuildOverlayLayer(overlayLayer);
        }

        private void BuildBackgroundLayer(RectTransform backgroundLayer)
        {
            Image background = CreateImage("DeepPixelBackground", backgroundLayer, GetSolidSprite());
            background.color = new Color(0.025f, 0.038f, 0.04f, 1f);
            StretchToParent(background.rectTransform);

            Image dimPattern = CreateImage("SubtleGrid", backgroundLayer, GetSolidSprite());
            dimPattern.color = new Color(0.07f, 0.11f, 0.095f, 0.32f);
            SetStretch(dimPattern.rectTransform, Vector2.zero, Vector2.one, new Vector2(OuterMargin, OuterMargin), new Vector2(-OuterMargin, -OuterMargin));
        }

        private void BuildTopStatusBar(RectTransform topStatusBar)
        {
            CreateText("GameTitle", topStatusBar, "Student Growth", 38, FontStyle.Bold, textLight, TextAnchor.UpperLeft, new Vector2(32f, -14f), new Vector2(420f, 38f));
            CreateText("DayLabel", topStatusBar, "第 6 周  /  月考压力  /  08:10", 18, FontStyle.Normal, textMuted, TextAnchor.UpperLeft, new Vector2(34f, -56f), new Vector2(520f, 26f));

            CreateTopChip(topStatusBar, "EnergyChip", PlayerStatKey.Energy, "精力 7/10", -520f);
            CreateTopChip(topStatusBar, "CoinChip", PlayerStatKey.Money, "金币 18", -350f);
            CreateTopChip(topStatusBar, "PressureChip", PlayerStatKey.Pressure, "压力 42", -180f);
            CreateButtonVisual("SettingsButton", topStatusBar, "设置", new Vector2(-62f, -46f), new Vector2(96f, 44f), false);
        }

        private void CreateTopChip(RectTransform parent, string objectName, PlayerStatKey statKey, string label, float anchoredX)
        {
            RectTransform chip = CreatePanel(objectName, parent, new Color(0.08f, 0.12f, 0.115f, 0.92f), new Color(0.32f, 0.48f, 0.40f, 1f));
            SetTopRight(chip, new Vector2(anchoredX, -46f), new Vector2(148f, 44f));

            if (GameArtAssetCatalog.TryGetStatIconPath(statKey, out string iconPath))
            {
                Image icon = CreateImage(objectName + "Icon", chip, LoadSpriteFromAssetPath(iconPath));
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                SetLeftMiddle(icon.rectTransform, new Vector2(14f, 0f), new Vector2(28f, 28f));
            }

            CreateText(objectName + "Text", chip, label, 17, FontStyle.Normal, textLight, TextAnchor.MiddleLeft, new Vector2(48f, 0f), new Vector2(90f, 28f));
        }

        private void BuildLeftStatsPanel(RectTransform leftStatsPanel)
        {
            CreateText("StudentStatusTitle", leftStatsPanel, "学生状态", 26, FontStyle.Bold, textLight, TextAnchor.UpperLeft, new Vector2(22f, -22f), new Vector2(180f, 36f));

            RectTransform portraitAnchor = CreateEmptyRect("CharacterPortraitAnchor", leftStatsPanel);
            SetTopCenter(portraitAnchor, new Vector2(0f, -152f), new Vector2(190f, 220f));
            if (GameArtAssetCatalog.CharacterPortraitPaths.TryGetValue("char_player_student", out string playerPath))
            {
                Image portrait = CreateImage("PlayerCharacterPortrait", portraitAnchor, LoadSpriteFromAssetPath(playerPath));
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;
                StretchToParent(portrait.rectTransform);
            }

            CreateText("PlayerName", leftStatsPanel, "普通学生", 20, FontStyle.Bold, textAmber, TextAnchor.UpperCenter, new Vector2(0f, -218f), new Vector2(220f, 28f));
            CreateText("PlayerStatus", leftStatsPanel, "目标：稳定成长，控制压力", 15, FontStyle.Normal, textMuted, TextAnchor.UpperCenter, new Vector2(0f, -252f), new Vector2(260f, 24f));

            float rowY = -318f;
            CreateStatRow(leftStatsPanel, "ChineseRow", PlayerStatKey.Chinese, "语文", "36", rowY);
            CreateStatRow(leftStatsPanel, "MathRow", PlayerStatKey.Math, "数学", "42", rowY - 46f);
            CreateStatRow(leftStatsPanel, "EnglishRow", PlayerStatKey.English, "英语", "31", rowY - 92f);
            CreateStatRow(leftStatsPanel, "MoodRow", PlayerStatKey.Mood, "心情", "良好", rowY - 138f);
            CreateStatRow(leftStatsPanel, "ParentRow", PlayerStatKey.ParentRelation, "亲子", "普通", rowY - 184f);
            CreateStatRow(leftStatsPanel, "SocialRow", PlayerStatKey.Social, "社交", "22", rowY - 230f);
        }

        private void CreateStatRow(RectTransform parent, string objectName, PlayerStatKey statKey, string label, string value, float y)
        {
            RectTransform row = CreatePanel(objectName, parent, new Color(0.045f, 0.08f, 0.072f, 0.84f), new Color(0.18f, 0.31f, 0.25f, 0.92f));
            SetTopStretch(row, 20f, 20f, -y - 18f, 34f);

            if (GameArtAssetCatalog.TryGetStatIconPath(statKey, out string path))
            {
                Image icon = CreateImage(objectName + "Icon", row, LoadSpriteFromAssetPath(path));
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                SetLeftMiddle(icon.rectTransform, new Vector2(12f, 0f), new Vector2(24f, 24f));
            }

            CreateText(objectName + "Label", row, label, 16, FontStyle.Normal, textLight, TextAnchor.MiddleLeft, new Vector2(46f, 0f), new Vector2(110f, 24f));
            CreateText(objectName + "Value", row, value, 16, FontStyle.Bold, textAmber, TextAnchor.MiddleRight, new Vector2(-18f, 0f), new Vector2(92f, 24f));
        }

        private void BuildCenterScenePanel(RectTransform centerScenePanel, RectTransform characterArea)
        {
            CreateText("CenterSceneTitle", centerScenePanel, "课堂观察", 26, FontStyle.Bold, textLight, TextAnchor.UpperLeft, new Vector2(24f, -22f), new Vector2(220f, 36f));
            CreateText("CurrentEventLabel", centerScenePanel, "当前事件：月考前复习窗口", 17, FontStyle.Normal, textMuted, TextAnchor.UpperRight, new Vector2(-24f, -28f), new Vector2(340f, 28f));

            RectTransform sceneWindow = CreatePanel("ClassroomSceneWindow", centerScenePanel, new Color(0.025f, 0.045f, 0.043f, 0.95f), greenBorder);
            SetStretch(sceneWindow, Vector2.zero, Vector2.one, new Vector2(24f, 92f), new Vector2(-24f, -72f));

            if (GameArtAssetCatalog.BackgroundPaths.TryGetValue("school_classroom", out string classroomPath))
            {
                Image classroom = CreateImage("ClassroomBackground", sceneWindow, LoadSpriteFromAssetPath(classroomPath));
                classroom.preserveAspect = true;
                classroom.raycastTarget = false;
                classroom.color = new Color(0.82f, 0.92f, 0.86f, 0.86f);
                StretchToParent(classroom.rectTransform, 14f);
            }

            RectTransform eventCard = CreatePanel("CurrentEventPaper", sceneWindow, paperFill, amberBorder);
            SetTopRight(eventCard, new Vector2(-28f, -38f), new Vector2(280f, 88f));
            CreateText("CurrentEventPaperText", eventCard, "今日重点\n刷题训练已推荐", 16, FontStyle.Bold, new Color(0.20f, 0.18f, 0.12f, 1f), TextAnchor.MiddleCenter, Vector2.zero, new Vector2(236f, 56f));

            RectTransform playerAnchor = CreateEmptyRect("PlayerCharacter", characterArea);
            SetStretch(playerAnchor, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-92f, 328f), new Vector2(92f, 512f));
            if (GameArtAssetCatalog.CharacterPortraitPaths.TryGetValue("char_player_student", out string playerPath))
            {
                Image player = CreateImage("PlayerCharacterImage", playerAnchor, LoadSpriteFromAssetPath(playerPath));
                player.preserveAspect = true;
                player.raycastTarget = false;
                StretchToParent(player.rectTransform);
            }

            CreateText("PlayerSceneLabel", sceneWindow, "Player", 14, FontStyle.Normal, textAmber, TextAnchor.LowerCenter, new Vector2(0f, 34f), new Vector2(120f, 22f));
        }

        private void BuildRightTaskPanel(RectTransform rightTaskPanel)
        {
            CreateText("TaskPanelTitle", rightTaskPanel, "当前目标", 26, FontStyle.Bold, textAmber, TextAnchor.UpperLeft, new Vector2(22f, -22f), new Vector2(180f, 36f));
            CreateText("TaskList", rightTaskPanel, "1. 数学达到 50\n2. 压力保持低于 70\n3. 准备月考 Boss", 18, FontStyle.Normal, textLight, TextAnchor.UpperLeft, new Vector2(24f, -88f), new Vector2(320f, 112f));

            RectTransform bossPanel = CreatePanel("BossWarningPanel", rightTaskPanel, new Color(0.17f, 0.06f, 0.055f, 0.88f), warningRed);
            SetTopStretch(bossPanel, 20f, 20f, 224f, 134f);
            if (GameArtAssetCatalog.TryGetChallengeIconPath("boss_month_exam", out string bossPath))
            {
                Image bossIcon = CreateImage("BossWarningIcon", bossPanel, LoadSpriteFromAssetPath(bossPath));
                bossIcon.preserveAspect = true;
                bossIcon.raycastTarget = false;
                SetLeftMiddle(bossIcon.rectTransform, new Vector2(18f, 0f), new Vector2(88f, 88f));
            }

            CreateText("BossWarningTitle", bossPanel, "Boss 预警", 20, FontStyle.Bold, new Color(1f, 0.78f, 0.58f, 1f), TextAnchor.MiddleLeft, new Vector2(124f, 24f), new Vector2(188f, 28f));
            CreateText("BossWarningText", bossPanel, "月考压力将在阶段末触发\n建议保留减压与学习牌", 14, FontStyle.Normal, new Color(0.92f, 0.78f, 0.66f, 1f), TextAnchor.MiddleLeft, new Vector2(124f, -20f), new Vector2(210f, 48f));

            RectTransform resultPanel = CreatePanel("ResultFeedbackPanel", rightTaskPanel, new Color(0.045f, 0.075f, 0.068f, 0.88f), greenBorder);
            SetBottomStretch(resultPanel, 20f, 20f, 24f, 132f);
            CreateText("ResultFeedbackTitle", resultPanel, "反馈", 19, FontStyle.Bold, textLight, TextAnchor.UpperLeft, new Vector2(18f, -16f), new Vector2(160f, 28f));
            CreateText("ResultFeedbackBody", resultPanel, "课堂听讲可进入。\n刷题训练有额外数学收益。", 15, FontStyle.Normal, textMuted, TextAnchor.UpperLeft, new Vector2(18f, -52f), new Vector2(300f, 58f));
        }

        private void BuildBottomCardPanel(RectTransform bottomCardPanel, RectTransform cardArea, RectTransform itemArea)
        {
            CreateText("BottomPanelTitle", bottomCardPanel, "行动卡牌", 22, FontStyle.Bold, textLight, TextAnchor.UpperLeft, new Vector2(298f, -20f), new Vector2(180f, 32f));
            CreateText("ItemAreaTitle", bottomCardPanel, "道具", 20, FontStyle.Bold, textLight, TextAnchor.UpperLeft, new Vector2(26f, -22f), new Vector2(120f, 28f));

            HorizontalLayoutGroup cardLayout = cardArea.gameObject.AddComponent<HorizontalLayoutGroup>();
            cardLayout.childAlignment = TextAnchor.MiddleCenter;
            cardLayout.spacing = 22f;
            cardLayout.padding = new RectOffset(0, 0, 0, 0);
            cardLayout.childControlWidth = false;
            cardLayout.childControlHeight = false;
            cardLayout.childForceExpandWidth = false;
            cardLayout.childForceExpandHeight = false;

            for (int i = 0; i < HandCardIds.Length; i++)
            {
                CreateCardView(cardArea, HandCardIds[i], i == 1);
            }

            GridLayoutGroup itemGrid = itemArea.gameObject.AddComponent<GridLayoutGroup>();
            itemGrid.cellSize = new Vector2(62f, 62f);
            itemGrid.spacing = new Vector2(12f, 12f);
            itemGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            itemGrid.constraintCount = 2;
            itemGrid.childAlignment = TextAnchor.UpperLeft;
            itemGrid.padding = new RectOffset(0, 0, 0, 0);

            foreach (string itemId in ItemShortcutIds)
            {
                if (!GameArtAssetCatalog.TryGetItemIconPath(itemId, out string itemPath))
                {
                    continue;
                }

                RectTransform slot = CreatePanel("ItemShortcut_" + itemId, itemArea, new Color(0.06f, 0.08f, 0.075f, 0.95f), greenBorder);
                LayoutElement layoutElement = slot.gameObject.AddComponent<LayoutElement>();
                layoutElement.preferredWidth = 62f;
                layoutElement.preferredHeight = 62f;
                Image icon = CreateImage("Icon", slot, LoadSpriteFromAssetPath(itemPath));
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                StretchToParent(icon.rectTransform, 8f);
            }

            CreateButtonVisual("ConfirmActionButton", bottomCardPanel, "确认行动", new Vector2(-150f, -92f), new Vector2(168f, 50f), true);
            CreateButtonVisual("EndTurnButton", bottomCardPanel, "结束回合", new Vector2(-150f, -154f), new Vector2(168f, 50f), false);
        }

        private void CreateCardView(RectTransform cardArea, string cardId, bool selected)
        {
            RectTransform card = CreatePanel(
                "Card_" + cardId,
                cardArea,
                new Color(0.07f, 0.06f, 0.048f, 0.98f),
                selected ? amberBorder : new Color(0.42f, 0.30f, 0.20f, 1f));
            LayoutElement layoutElement = card.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 190f;
            layoutElement.preferredHeight = 244f;

            RectTransform imageArea = CreatePanel("ImageArea", card, new Color(0.025f, 0.04f, 0.04f, 0.94f), greenBorder);
            SetTopStretch(imageArea, 12f, 12f, 14f, 134f);

            if (GameArtAssetCatalog.TryGetCardImagePath(cardId, out string cardPath))
            {
                Image cardImage = CreateImage("CardImage", imageArea, LoadSpriteFromAssetPath(cardPath));
                cardImage.preserveAspect = true;
                cardImage.raycastTarget = false;
                StretchToParent(cardImage.rectTransform, 6f);
            }

            RectTransform costIcon = CreatePanel("CostIcon", card, new Color(0.14f, 0.25f, 0.20f, 0.98f), amberBorder);
            SetTopLeft(costIcon, new Vector2(18f, -18f), new Vector2(32f, 32f));
            CreateText("CostText", costIcon, "1", 16, FontStyle.Bold, textAmber, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(24f, 24f));

            CardDisplayText displayText = GetCardDisplayText(cardId);
            CreateText("TitleText", card, displayText.title, 17, FontStyle.Bold, textLight, TextAnchor.UpperCenter, new Vector2(0f, -148f), new Vector2(160f, 24f));
            CreateText("DescriptionText", card, displayText.description, 13, FontStyle.Normal, textMuted, TextAnchor.UpperCenter, new Vector2(0f, -178f), new Vector2(156f, 50f));

            if (selected)
            {
                RectTransform highlight = CreatePanel("SelectedHighlight", card, new Color(1f, 0.84f, 0.24f, 0.08f), amberBorder);
                StretchToParent(highlight, 4f);
            }
        }

        private void BuildOverlayLayer(RectTransform overlayLayer)
        {
            RectTransform selectedHint = CreatePanel("SelectedHighlight", overlayLayer, new Color(0.73f, 0.60f, 0.22f, 0.90f), amberBorder);
            SetBottomRight(selectedHint, new Vector2(-222f, 108f), new Vector2(210f, 34f));
            CreateText("SelectedHintText", selectedHint, "已选：刷题训练", 15, FontStyle.Bold, new Color(0.14f, 0.11f, 0.04f, 1f), TextAnchor.MiddleCenter, Vector2.zero, new Vector2(190f, 24f));
        }

        private void CreateButtonVisual(string objectName, RectTransform parent, string label, Vector2 anchoredPosition, Vector2 size, bool highlighted)
        {
            RectTransform button = CreatePanel(
                objectName,
                parent,
                highlighted ? new Color(0.19f, 0.22f, 0.12f, 0.96f) : new Color(0.08f, 0.105f, 0.10f, 0.96f),
                highlighted ? amberBorder : greenBorder);
            SetBottomRight(button, anchoredPosition, size);
            CreateText(objectName + "Text", button, label, 17, FontStyle.Bold, highlighted ? textAmber : textLight, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(size.x - 16f, size.y - 12f));
        }

        private RectTransform CreateFullLayer(string objectName, RectTransform parent)
        {
            RectTransform rectTransform = CreateEmptyRect(objectName, parent);
            StretchToParent(rectTransform);
            return rectTransform;
        }

        private RectTransform CreateEmptyRect(string objectName, RectTransform parent)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        private RectTransform CreatePanel(string objectName, RectTransform parent, Color fillColor, Color borderColor)
        {
            RectTransform panel = CreateEmptyRect(objectName, parent);
            Image image = panel.gameObject.AddComponent<Image>();
            image.sprite = GetSolidSprite();
            image.type = Image.Type.Simple;
            image.color = fillColor;
            image.raycastTarget = false;

            CreateBorder(panel, borderColor);
            return panel;
        }

        private void CreateBorder(RectTransform parent, Color color)
        {
            CreateBorderSegment("BorderTop", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -PanelBorderThickness), Vector2.zero, color);
            CreateBorderSegment("BorderBottom", parent, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, PanelBorderThickness), color);
            CreateBorderSegment("BorderLeft", parent, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(PanelBorderThickness, 0f), color);
            CreateBorderSegment("BorderRight", parent, new Vector2(1f, 0f), Vector2.one, new Vector2(-PanelBorderThickness, 0f), Vector2.zero, color);
        }

        private void CreateBorderSegment(string objectName, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            Image image = CreateImage(objectName, parent, GetSolidSprite());
            image.color = color;
            image.raycastTarget = false;
            SetStretch(image.rectTransform, anchorMin, anchorMax, offsetMin, offsetMax);
        }

        private Image CreateImage(string objectName, RectTransform parent, Sprite sprite)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform));
            RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            Image image = imageObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(
            string objectName,
            RectTransform parent,
            string text,
            int fontSize,
            FontStyle fontStyle,
            Color color,
            TextAnchor anchor,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform));
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            ApplyTextTransformAnchor(rectTransform, anchor);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Text textComponent = textObject.AddComponent<Text>();
            textComponent.text = text;
            textComponent.font = GetRuntimeFont();
            textComponent.fontSize = fontSize;
            textComponent.fontStyle = fontStyle;
            textComponent.alignment = anchor;
            textComponent.color = color;
            textComponent.raycastTarget = false;
            textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComponent.verticalOverflow = VerticalWrapMode.Truncate;
            return textComponent;
        }

        private static void ApplyTextTransformAnchor(RectTransform rectTransform, TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    rectTransform.anchorMin = new Vector2(0f, 1f);
                    rectTransform.anchorMax = new Vector2(0f, 1f);
                    rectTransform.pivot = new Vector2(0f, 1f);
                    break;
                case TextAnchor.UpperCenter:
                    rectTransform.anchorMin = new Vector2(0.5f, 1f);
                    rectTransform.anchorMax = new Vector2(0.5f, 1f);
                    rectTransform.pivot = new Vector2(0.5f, 1f);
                    break;
                case TextAnchor.UpperRight:
                    rectTransform.anchorMin = new Vector2(1f, 1f);
                    rectTransform.anchorMax = new Vector2(1f, 1f);
                    rectTransform.pivot = new Vector2(1f, 1f);
                    break;
                case TextAnchor.MiddleLeft:
                    rectTransform.anchorMin = new Vector2(0f, 0.5f);
                    rectTransform.anchorMax = new Vector2(0f, 0.5f);
                    rectTransform.pivot = new Vector2(0f, 0.5f);
                    break;
                case TextAnchor.MiddleCenter:
                    rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    break;
                case TextAnchor.MiddleRight:
                    rectTransform.anchorMin = new Vector2(1f, 0.5f);
                    rectTransform.anchorMax = new Vector2(1f, 0.5f);
                    rectTransform.pivot = new Vector2(1f, 0.5f);
                    break;
                case TextAnchor.LowerLeft:
                    rectTransform.anchorMin = Vector2.zero;
                    rectTransform.anchorMax = Vector2.zero;
                    rectTransform.pivot = Vector2.zero;
                    break;
                case TextAnchor.LowerCenter:
                    rectTransform.anchorMin = new Vector2(0.5f, 0f);
                    rectTransform.anchorMax = new Vector2(0.5f, 0f);
                    rectTransform.pivot = new Vector2(0.5f, 0f);
                    break;
                case TextAnchor.LowerRight:
                    rectTransform.anchorMin = new Vector2(1f, 0f);
                    rectTransform.anchorMax = new Vector2(1f, 0f);
                    rectTransform.pivot = new Vector2(1f, 0f);
                    break;
            }
        }

        private Font GetRuntimeFont()
        {
            if (runtimeFont != null)
            {
                return runtimeFont;
            }

            runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return runtimeFont;
        }

        private Sprite GetSolidSprite()
        {
            if (solidSprite != null)
            {
                return solidSprite;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return solidSprite;
        }

        private static Sprite LoadSpriteFromAssetPath(string assetPath)
        {
            string fullPath = GetFullPath(assetPath);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                Debug.LogWarning("Student Growth UI could not find sprite file: " + assetPath);
                return null;
            }

            byte[] bytes = File.ReadAllBytes(fullPath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.name = Path.GetFileNameWithoutExtension(assetPath);

            if (!texture.LoadImage(bytes))
            {
                Destroy(texture);
                return null;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private static string GetFullPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return string.Empty;
            }

            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return assetPath;
            }

            return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        }

        private static void CopyRectTransform(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.offsetMin = source.offsetMin;
            target.offsetMax = source.offsetMax;
            target.pivot = source.pivot;
        }

        private static void StretchToParent(RectTransform rectTransform, float padding = 0f)
        {
            SetStretch(rectTransform, Vector2.zero, Vector2.one, new Vector2(padding, padding), new Vector2(-padding, -padding));
        }

        private static void SetStretch(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void SetTopStretch(RectTransform rectTransform, float left, float right, float top, float height)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.offsetMin = new Vector2(left, -top - height);
            rectTransform.offsetMax = new Vector2(-right, -top);
        }

        private static void SetBottomStretch(RectTransform rectTransform, float left, float right, float bottom, float height)
        {
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.offsetMin = new Vector2(left, bottom);
            rectTransform.offsetMax = new Vector2(-right, bottom + height);
        }

        private static void SetLeftStretch(RectTransform rectTransform, float left, float top, float bottom, float width)
        {
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 0.5f);
            rectTransform.offsetMin = new Vector2(left, bottom);
            rectTransform.offsetMax = new Vector2(left + width, -top);
        }

        private static void SetRightStretch(RectTransform rectTransform, float right, float top, float bottom, float width)
        {
            rectTransform.anchorMin = new Vector2(1f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 0.5f);
            rectTransform.offsetMin = new Vector2(-right - width, bottom);
            rectTransform.offsetMax = new Vector2(-right, -top);
        }

        private static void SetTopLeft(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        private static void SetTopRight(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        private static void SetBottomRight(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(1f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(1f, 0f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        private static void SetTopCenter(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        private static void SetLeftMiddle(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rectTransform.anchorMax = new Vector2(0f, 0.5f);
            rectTransform.pivot = new Vector2(0f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        private readonly struct CardDisplayText
        {
            public CardDisplayText(string title, string description)
            {
                this.title = title;
                this.description = description;
            }

            public readonly string title;
            public readonly string description;
        }

        private static CardDisplayText GetCardDisplayText(string cardId)
        {
            switch (cardId)
            {
                case "card_study_class_focus":
                    return new CardDisplayText("课堂专注", "进度 +6\n抽 1 张牌");
                case "card_study_math_practice":
                    return new CardDisplayText("刷数学题", "数学 +8\n压力 +2");
                case "card_rest_good_sleep":
                    return new CardDisplayText("好好睡觉", "精力 +3\n压力 -4");
                case "card_social_help_classmate":
                    return new CardDisplayText("同学互助", "社交 +4\n进度 +3");
                case "card_family_talk_parent":
                    return new CardDisplayText("和父母沟通", "亲子 +5\n压力 -3");
                default:
                    return new CardDisplayText("行动卡", "等待数据");
            }
        }
    }
}
