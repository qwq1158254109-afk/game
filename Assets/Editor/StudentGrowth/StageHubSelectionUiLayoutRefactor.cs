using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth.Editor
{
    public static class StageHubSelectionUiLayoutRefactor
    {
        private const string PrefabPath = "Assets/Prefabs/StudentGrowth/UI/StageHub/StageHubSelectionCanvas.prefab";

        [MenuItem("Tools/Student Growth/Refactor Stage Hub Selection UI")]
        public static void Apply()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ApplyToRoot(root);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("StageHubSelectionCanvas UI 布局重构已应用。", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void ApplyToRoot(GameObject prefabRoot)
        {
            if (prefabRoot == null)
            {
                throw new System.ArgumentNullException(nameof(prefabRoot));
            }

            Canvas canvas = prefabRoot.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.pixelPerfect = true;
            }

            StageHubSelectionController controller = prefabRoot.GetComponent<StageHubSelectionController>();
            BuildTimeActionHud(prefabRoot);
            BuildUnifiedLocationDetails(prefabRoot, controller);
            LayoutStatusPanel(prefabRoot);
            LayoutControlHints(prefabRoot);
            LayoutMapNodesAndRoutes(prefabRoot, controller);
            DisableOverlayFrame(prefabRoot);
        }

        private static void BuildTimeActionHud(GameObject root)
        {
            Transform layer = root.transform.Find("Layer_04_TopLeftTimeInfo");
            RectTransform host = FindDeep(layer, "Panel_TimeInfo") as RectTransform;
            if (host == null)
            {
                throw new MissingReferenceException("缺少 Layer_04_TopLeftTimeInfo/Panel_TimeInfo。");
            }

            SetRect(host, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -32f), new Vector2(340f, 116f));
            Image hostImage = host.GetComponent<Image>();
            if (hostImage != null)
            {
                hostImage.raycastTarget = false;
            }

            TimeActionHudView view = host.GetComponent<TimeActionHudView>();
            if (view == null)
            {
                view = host.gameObject.AddComponent<TimeActionHudView>();
            }

            RectTransform iconArea = GetOrCreateRect(host, "PeriodIconArea");
            RectTransform infoArea = GetOrCreateRect(host, "TimeInfoArea");
            RectTransform dayRow = GetOrCreateRect(infoArea, "DayPeriodRow");
            RectTransform actionRow = GetOrCreateRect(infoArea, "ActionPointRow");

            Text dayText = GetText(FindDeep(host, "DayText")) ?? GetText(FindDeep(host, "Text_TimePeriod"));
            Text periodText = GetText(FindDeep(host, "PeriodText")) ?? GetText(FindDeep(host, "Text_Time"));
            Text actionText = GetText(FindDeep(host, "ActionPointText")) ?? GetText(FindDeep(host, "Text_CurrentLocation"));
            dayText = PrepareExistingText(dayText, "DayText", dayRow, "第 1 天", 18, TextAnchor.MiddleLeft);
            periodText = PrepareExistingText(periodText, "PeriodText", dayRow, "早晨", 18, TextAnchor.MiddleLeft);
            actionText = PrepareExistingText(actionText, "ActionPointText", actionRow, "今天 3 次", 16, TextAnchor.MiddleLeft);

            Image dateLogo = GetOrCreateImage(iconArea, "DateLogoBackground");
            Image periodBackground = GetOrCreateImage(iconArea, "PeriodBackground");
            Image periodIcon = GetOrCreateImage(iconArea, "PeriodIcon");
            dateLogo.transform.SetSiblingIndex(0);
            periodBackground.transform.SetSiblingIndex(1);
            periodIcon.transform.SetSiblingIndex(2);

            Image[] points = new Image[TimeActionState.MaxDailyActionPoints];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = GetOrCreateImage(actionRow, "ActionPoint_" + i);
            }

            Text remaining = GetText(FindDeep(host, "RemainingDailyText"));
            if (remaining == null)
            {
                remaining = CloneText(dayText, infoArea, "RemainingDailyText", "阶段剩 6 次", 12, TextAnchor.MiddleLeft);
            }
            remaining.gameObject.SetActive(false);

            Text warning = GetText(FindDeep(host, "SleepWarningText"));
            if (warning == null)
            {
                warning = CloneText(dayText, infoArea, "SleepWarningText", string.Empty, 11, TextAnchor.MiddleLeft);
            }
            warning.color = new Color(1f, 0.52f, 0.42f, 1f);

            view.periodIconArea = iconArea;
            view.timeInfoArea = infoArea;
            view.dayPeriodRow = dayRow;
            view.actionPointRow = actionRow;
            view.dateLogoBackground = dateLogo;
            view.periodBackground = periodBackground;
            view.periodIcon = periodIcon;
            view.dayText = dayText;
            view.periodText = periodText;
            view.actionPointText = actionText;
            view.actionPointIcons = points;
            view.remainingDailyText = remaining;
            view.sleepWarningText = warning;
            view.SetTopLeftLayout();
        }

        private static void BuildUnifiedLocationDetails(GameObject root, StageHubSelectionController controller)
        {
            RectTransform info = FindDeep(root.transform, "Panel_LocationInfo") as RectTransform;
            RectTransform residents = FindDeep(root.transform, "Panel_Residents") as RectTransform
                ?? FindDeep(root.transform, "ExtraInfoArea") as RectTransform;
            if (info == null || residents == null)
            {
                throw new MissingReferenceException("缺少地点详情或居民事件面板。");
            }

            SetRect(info, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -32f), new Vector2(460f, 730f));
            Image infoBackground = info.GetComponent<Image>();
            Image statusPanelBackground = FindDeep(root.transform, "Panel_Status") == null
                ? null
                : FindDeep(root.transform, "Panel_Status").GetComponent<Image>();
            if (infoBackground != null && statusPanelBackground != null)
            {
                infoBackground.sprite = statusPanelBackground.sprite;
                infoBackground.type = Image.Type.Sliced;
            }
            residents.SetParent(info, false);
            residents.name = "ExtraInfoArea";
            SetRect(residents, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -420f), new Vector2(412f, 134f));
            Image residentsBackground = residents.GetComponent<Image>();
            if (residentsBackground != null)
            {
                residentsBackground.enabled = false;
                residentsBackground.raycastTarget = false;
            }

            StageHubLocationInfoView view = info.GetComponent<StageHubLocationInfoView>();
            Transform technicalSceneText = FindDeep(info, "Text_TargetScene");
            if (technicalSceneText != null)
            {
                Object.DestroyImmediate(technicalSceneText.gameObject);
            }

            if (view.availabilityReasonText == null)
            {
                view.availabilityReasonText = GetText(FindDeep(info, "Text_LocationState"));
            }

            if (view.availabilityReasonText != null)
            {
                view.availabilityReasonText.name = "Text_AvailabilityReason";
            }

            Text title = view.locationNameText;
            PixelText titleStyle = title.GetComponent<PixelText>();
            if (titleStyle != null)
            {
                titleStyle.textStyle = GameUiTextStyle.Body;
                titleStyle.ApplyStyle();
            }
            title.fontSize = 18;
            title.fontStyle = FontStyle.Bold;
            title.resizeTextForBestFit = false;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -16f), new Vector2(278f, 44f));
            title.alignment = TextAnchor.MiddleLeft;
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;

            RectTransform badgePanel = GetOrCreateRect(info, "Panel_StatusBadge");
            Image badgeBackground = badgePanel.GetComponent<Image>();
            if (badgeBackground == null)
            {
                badgeBackground = badgePanel.gameObject.AddComponent<Image>();
            }
            badgeBackground.sprite = info.GetComponent<Image>() == null ? null : info.GetComponent<Image>().sprite;
            badgeBackground.type = Image.Type.Sliced;
            badgeBackground.color = new Color(0.12f, 0.15f, 0.2f, 0.92f);
            badgeBackground.raycastTarget = false;
            SetRect(badgePanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(314f, -18f), new Vector2(122f, 36f));

            Text badge = GetText(FindDeep(badgePanel, "Text_StatusBadge"));
            if (badge == null)
            {
                badge = CloneText(title, badgePanel, "Text_StatusBadge", "已开放", 15, TextAnchor.MiddleCenter);
            }
            StretchWithOffsets(badge.rectTransform, 8f, 4f);
            view.statusBadgeText = badge;

            RectTransform previewPanel = FindDeep(info, "Panel_Preview") as RectTransform;
            SetRect(previewPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(320f, 180f));
            RectTransform previewImage = view.previewImage.rectTransform;
            StretchWithOffsets(previewImage, 18f, 18f);
            view.previewImage.preserveAspect = true;

            SetRect(view.descriptionText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -276f), new Vector2(412f, 70f));
            SetRect(view.availabilityReasonText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -352f), new Vector2(412f, 48f));
            view.availabilityReasonText.text = string.Empty;
            view.availabilityReasonText.fontSize = 14;
            view.availabilityReasonText.fontStyle = FontStyle.Normal;
            view.availabilityReasonText.color = new Color(1f, 0.48f, 0.32f, 1f);

            Image divider = GetOrCreateImage(info, "Divider");
            divider.sprite = GameUiStyle.GetSolidSprite();
            divider.color = new Color(0.3f, 0.72f, 0.68f, 0.55f);
            divider.preserveAspect = false;
            SetRect(divider.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -408f), new Vector2(412f, 2f));

            Text residentsTitle = GetText(FindDeep(residents, "Text_ResidentsTitle"));
            residentsTitle.text = "地点情报";
            SetRect(residentsTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(412f, 28f));
            RectTransform residentsGrid = FindDeep(residents, "Residents_Grid") as RectTransform;
            SetRect(residentsGrid, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -34f), new Vector2(412f, 36f));
            RectTransform eventsPanel = FindDeep(residents, "Panel_Events") as RectTransform;
            SetRect(eventsPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -76f), new Vector2(412f, 40f));

            SetRect(view.resultMessageText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -576f), new Vector2(412f, 42f));
            RectTransform confirmRect = view.confirmButton.transform as RectTransform;
            SetRect(confirmRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(300f, 46f));
            view.confirmButtonText = view.confirmButton.GetComponentInChildren<Text>(true);

            if (controller != null)
            {
                controller.locationInfoView = view;
            }
        }

        private static void LayoutStatusPanel(GameObject root)
        {
            RectTransform panel = FindDeep(root.transform, "Panel_Status") as RectTransform;
            SetRect(panel, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(32f, 24f), new Vector2(340f, 190f));
            Text title = GetText(FindDeep(panel, "Text_StatusTitle"));
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(308f, 28f));

            for (int i = 0; i < 3; i++)
            {
                RectTransform row = FindDeep(panel, "Status_Row_0" + (i + 1)) as RectTransform;
                SetRect(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -50f - i * 36f), new Vector2(308f, 28f));
                RectTransform value = FindDeep(row, "Text_Value") as RectTransform;
                SetRect(value, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(108f, 24f));
                Text valueText = GetText(value);
                if (valueText != null)
                {
                    PixelText pixelText = valueText.GetComponent<PixelText>();
                    if (pixelText != null)
                    {
                        pixelText.textStyle = GameUiTextStyle.StatText;
                        pixelText.ApplyStyle();
                    }
                    valueText.fontSize = 18;
                    valueText.resizeTextForBestFit = false;
                    valueText.verticalOverflow = VerticalWrapMode.Truncate;
                }
                RectTransform back = FindDeep(row, "Bar_Back") as RectTransform;
                RectTransform fill = FindDeep(row, "Bar_Fill") as RectTransform;
                SetRect(back, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(118f, 0f), new Vector2(180f, 12f));
                SetRect(fill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(118f, 0f), new Vector2(180f, 12f));
            }

            RectTransform money = FindDeep(panel, "Status_Row_04") as RectTransform;
            SetRect(money, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -158f), new Vector2(308f, 24f));
            Text moneyText = GetText(money);
            if (moneyText != null)
            {
                PixelText pixelText = moneyText.GetComponent<PixelText>();
                if (pixelText != null)
                {
                    pixelText.textStyle = GameUiTextStyle.StatText;
                    pixelText.ApplyStyle();
                }
                moneyText.fontSize = 18;
                moneyText.resizeTextForBestFit = false;
                moneyText.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }

        private static void LayoutControlHints(GameObject root)
        {
            RectTransform panel = FindDeep(root.transform, "Panel_ControlHint") as RectTransform;
            SetRect(panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(760f, 52f));
        }

        private static void LayoutMapNodesAndRoutes(GameObject root, StageHubSelectionController controller)
        {
            Vector2[] positions =
            {
                new Vector2(-500f, 170f),
                new Vector2(-175f, 170f),
                new Vector2(150f, 170f),
                new Vector2(-420f, -140f),
                new Vector2(-105f, -140f),
                new Vector2(210f, -140f)
            };

            StageHubLocationNodeView[] nodes = controller == null
                ? root.GetComponentsInChildren<StageHubLocationNodeView>(true)
                : controller.nodes;
            for (int i = 0; i < nodes.Length && i < positions.Length; i++)
            {
                StageHubLocationNodeView node = nodes[i];
                RectTransform nodeRect = node.transform as RectTransform;
                SetRect(nodeRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), positions[i], new Vector2(240f, 210f));
                SetCentered(node.nodeSprite.rectTransform, new Vector2(0f, 10f), new Vector2(220f, 165f));
                node.nodeSprite.preserveAspect = true;
                SetCentered(FindDeep(node.transform, "Image_NodeShadow") as RectTransform, new Vector2(0f, -44f), new Vector2(205f, 86f));
                SetCentered(node.selectedOutline.rectTransform, new Vector2(0f, -44f), new Vector2(212f, 110f));
                SetCentered(node.locationPin.rectTransform, new Vector2(78f, 76f), new Vector2(36f, 52f));
                SetCentered(node.selectionSparkles.rectTransform, new Vector2(92f, 56f), new Vector2(68f, 54f));
                SetCentered(node.lockMarker.rectTransform, new Vector2(72f, -52f), new Vector2(72f, 30f));
                SetCentered(node.nameLabelBackground.rectTransform, new Vector2(0f, -82f), new Vector2(196f, 38f));
                node.locationNameText.fontSize = 18;
                node.locationNameText.resizeTextForBestFit = false;
                node.locationNameText.horizontalOverflow = HorizontalWrapMode.Wrap;
                node.locationNameText.verticalOverflow = VerticalWrapMode.Truncate;
            }

            Transform routes = root.transform.Find("Layer_01_MapRoutes");
            int[,] connections = { { 0, 1 }, { 1, 2 }, { 0, 3 }, { 1, 4 }, { 2, 5 }, { 3, 4 }, { 4, 5 } };
            for (int i = 0; i < connections.GetLength(0); i++)
            {
                Transform route = routes.Find("Route_0" + (i + 1));
                LayoutRoute(route, positions[connections[i, 0]], positions[connections[i, 1]]);
            }

            RectTransform pageTitle = FindDeep(root.transform, "Selection_Label") as RectTransform;
            SetRect(pageTitle, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(300f, 48f));
        }

        private static void LayoutRoute(Transform route, Vector2 nodeA, Vector2 nodeB)
        {
            if (route == null)
            {
                return;
            }

            Vector2 direction = (nodeB - nodeA).normalized;
            Vector2 start = nodeA + direction * 112f;
            Vector2 end = nodeB - direction * 112f;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            for (int i = 0; i < route.childCount; i++)
            {
                RectTransform dash = route.GetChild(i) as RectTransform;
                float t = (i + 1f) / (route.childCount + 1f);
                dash.anchorMin = new Vector2(0.5f, 0.5f);
                dash.anchorMax = new Vector2(0.5f, 0.5f);
                dash.pivot = new Vector2(0.5f, 0.5f);
                dash.anchoredPosition = Vector2.Lerp(start, end, t);
                dash.sizeDelta = new Vector2(20f, 4f);
                dash.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private static void DisableOverlayFrame(GameObject root)
        {
            Transform overlay = root.transform.Find("Layer_09_OverlayFrame");
            if (overlay != null)
            {
                overlay.gameObject.SetActive(false);
            }
        }

        private static Text PrepareExistingText(Text text, string name, RectTransform parent, string value, int size, TextAnchor alignment)
        {
            if (text == null)
            {
                throw new MissingReferenceException("缺少用于迁移的时间 HUD 文本：" + name);
            }

            text.name = name;
            text.rectTransform.SetParent(parent, false);
            ConfigureText(text, value, size, alignment);
            return text;
        }

        private static Text CloneText(Text template, RectTransform parent, string name, string value, int size, TextAnchor alignment)
        {
            GameObject clone = Object.Instantiate(template.gameObject, parent, false);
            clone.name = name;
            Text text = clone.GetComponent<Text>();
            ConfigureText(text, value, size, alignment);
            return text;
        }

        private static void ConfigureText(Text text, string value, int size, TextAnchor alignment)
        {
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.gameObject.SetActive(true);
        }

        private static RectTransform GetOrCreateRect(RectTransform parent, string name)
        {
            Transform existing = FindDeep(parent, name);
            if (existing != null)
            {
                RectTransform existingRect = existing as RectTransform;
                if (existingRect.parent != parent)
                {
                    existingRect.SetParent(parent, false);
                }
                return existingRect;
            }

            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = child.transform as RectTransform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image GetOrCreateImage(RectTransform parent, string name)
        {
            Transform existing = FindDeep(parent, name);
            if (existing != null)
            {
                RectTransform existingRect = existing as RectTransform;
                if (existingRect.parent != parent)
                {
                    existingRect.SetParent(parent, false);
                }
                Image existingImage = existing.GetComponent<Image>();
                if (existingImage != null)
                {
                    existingImage.raycastTarget = false;
                    existingImage.preserveAspect = true;
                    return existingImage;
                }
            }

            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = child.transform as RectTransform;
            rect.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.enabled = false;
            return image;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Text GetText(Transform transform)
        {
            return transform == null ? null : transform.GetComponent<Text>();
        }

        private static void SetCentered(RectTransform rect, Vector2 position, Vector2 size)
        {
            SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void StretchWithOffsets(RectTransform rect, float horizontal, float vertical)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
            rect.localScale = Vector3.one;
        }
    }
}
