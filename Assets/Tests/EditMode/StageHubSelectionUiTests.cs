using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace StudentGrowth.Tests
{
    public sealed class StageHubSelectionUiTests
    {
        private const string CanvasPrefabPath = "Assets/Prefabs/StudentGrowth/UI/StageHub/StageHubSelectionCanvas.prefab";

        [Test]
        public void StageHubCanvasKeepsRequiredLayerOrder()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            string[] expected =
            {
                "Layer_00_Background",
                "Layer_01_MapRoutes",
                "Layer_02_LocationNodes",
                "Layer_03_SelectionFX",
                "Layer_04_TopLeftTimeInfo",
                "Layer_05_BottomLeftPlayerStatus",
                "Layer_06_RightLocationInfo",
                "Layer_07_RightResidentsEvents",
                "Layer_08_BottomControlHint",
                "Layer_09_OverlayFrame"
            };
            string[] actual = Enumerable.Range(0, prefab.transform.childCount)
                .Select(index => prefab.transform.GetChild(index).name)
                .ToArray();

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void StageHubCanvasHasSixUniqueNodesAndFourStateButtons()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            StageHubLocationNodeView[] nodes = prefab.GetComponentsInChildren<StageHubLocationNodeView>(true);
            Button[] buttons = prefab.GetComponentsInChildren<Button>(true);

            Assert.That(nodes.Length, Is.EqualTo(6));
            Assert.That(nodes.Select(node => node.locationId).Distinct().Count(), Is.EqualTo(6));
            Assert.That(nodes.All(node => node.nodeSprite != null && node.nodeSprite.sprite != null), Is.True);
            Assert.That(buttons.Length, Is.EqualTo(7));
            Assert.That(buttons.All(button => button.transition == Selectable.Transition.SpriteSwap), Is.True);
            Assert.That(buttons.All(button => button.spriteState.highlightedSprite != null), Is.True);
            Assert.That(buttons.All(button => button.spriteState.pressedSprite != null), Is.True);
            Assert.That(buttons.All(button => button.spriteState.disabledSprite != null), Is.True);
        }

        [Test]
        public void KeyboardGridNavigationUsesExplicitTwoByThreeLayout()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                StageHubSelectionController controller = instance.GetComponent<StageHubSelectionController>();
                controller.SelectNode(controller.nodes[0]);
                controller.MoveSelection(Vector2Int.right);
                Assert.That(controller.SelectedNode, Is.SameAs(controller.nodes[1]));

                controller.MoveSelection(Vector2Int.down);
                Assert.That(controller.SelectedNode, Is.SameAs(controller.nodes[4]));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void StageHubCanvasUsesSinglePrefabHudAndPixelPerfectCanvas()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            Canvas canvas = prefab.GetComponent<Canvas>();
            TimeActionHudView[] huds = prefab.GetComponentsInChildren<TimeActionHudView>(true);

            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.pixelPerfect, Is.True);
            Assert.That(huds.Length, Is.EqualTo(1));
            Assert.That(huds[0].transform.parent.name, Is.EqualTo("Layer_04_TopLeftTimeInfo"));

            RectTransform hudRect = huds[0].transform as RectTransform;
            Assert.That(hudRect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(hudRect.anchorMax, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(hudRect.anchoredPosition, Is.EqualTo(new Vector2(32f, -32f)));
            Assert.That(hudRect.sizeDelta, Is.EqualTo(new Vector2(340f, 116f)));
            Assert.That(huds[0].dayText, Is.Not.Null);
            Assert.That(huds[0].dayText.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void StageHubPanelsMatchRefactoredLayoutAndKeepReferences()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            StageHubSelectionController controller = prefab.GetComponent<StageHubSelectionController>();
            StageHubLocationInfoView details = controller.locationInfoView;

            Assert.That(details, Is.Not.Null);
            Assert.That((details.transform as RectTransform).sizeDelta, Is.EqualTo(new Vector2(460f, 730f)));
            Assert.That(details.statusBadgeText, Is.Not.Null);
            Assert.That(details.availabilityReasonText, Is.Not.Null);
            Assert.That(details.confirmButtonText, Is.Not.Null);
            Assert.That(FindDeep(details.transform, "Text_TargetScene"), Is.Null);
            Assert.That(FindDeep(details.transform, "Text_LocationState"), Is.Null);
            Assert.That(FindDeep(details.transform, "Text_AvailabilityReason"), Is.Not.Null);
            Assert.That(details.GetType().GetField("sceneText"), Is.Null);
            Assert.That(details.GetType().GetField("stateText"), Is.Null);
            Assert.That(details.GetComponent<Image>().sprite?.name, Is.Not.EqualTo("modern_panel_info"));
            Assert.That(controller.residentsText, Is.Not.Null);
            Assert.That(controller.eventsText, Is.Not.Null);
            Assert.That(controller.residentsText.transform.IsChildOf(details.transform), Is.True);
            Assert.That(controller.eventsText.transform.IsChildOf(details.transform), Is.True);

            RectTransform status = prefab.transform.Find("Layer_05_BottomLeftPlayerStatus/Panel_Status") as RectTransform;
            RectTransform hints = prefab.transform.Find("Layer_08_BottomControlHint/Panel_ControlHint") as RectTransform;
            Assert.That(status.sizeDelta, Is.EqualTo(new Vector2(340f, 190f)));
            Assert.That(status.anchoredPosition, Is.EqualTo(new Vector2(32f, 24f)));
            StageHubSelectionController stageHubController = prefab.GetComponent<StageHubSelectionController>();
            Text[] statusValues =
            {
                stageHubController.energyText,
                stageHubController.pressureText,
                stageHubController.moodText,
                stageHubController.moneyText
            };
            Assert.That(statusValues.All(text => text != null && text.fontSize <= 18), Is.True,
                "状态文本字号必须适配 24px 行高，避免在 1920 宽分辨率整行被裁切。");
            Assert.That(statusValues.All(text => text.GetComponent<PixelText>()?.textStyle == GameUiTextStyle.StatText), Is.True,
                "PixelText 必须使用 StatText，避免运行时 OnEnable 把字号恢复为 22px Subtitle。");
            Assert.That(hints.sizeDelta, Is.EqualTo(new Vector2(760f, 52f)));
            Assert.That(hints.anchoredPosition, Is.EqualTo(new Vector2(0f, 24f)));
            Assert.That(prefab.transform.Find("Layer_09_OverlayFrame").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void CompatibilityLayersAndNodeOwnedSelectionFeedbackRemainIntact()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            Transform layer03 = prefab.transform.Find("Layer_03_SelectionFX");
            Transform layer07 = prefab.transform.Find("Layer_07_RightResidentsEvents");
            StageHubSelectionController controller = prefab.GetComponent<StageHubSelectionController>();

            Assert.That(layer03, Is.Not.Null);
            Assert.That(layer07, Is.Not.Null);
            Assert.That(layer07.childCount, Is.EqualTo(0));
            Assert.That(typeof(StageHubSelectionController).GetFields()
                .Any(field => field.Name.IndexOf("selectionFx", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);

            foreach (StageHubLocationNodeView node in controller.nodes)
            {
                Assert.That(node.selectedOutline.transform.IsChildOf(node.transform), Is.True);
                Assert.That(node.locationPin.transform.IsChildOf(node.transform), Is.True);
                Assert.That(node.selectionSparkles.transform.IsChildOf(node.transform), Is.True);
            }
        }

        [Test]
        public void StageHubPrefabHasNoMissingScripts()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            int missingCount = prefab.GetComponentsInChildren<Transform>(true)
                .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
            Assert.That(missingCount, Is.EqualTo(0));
        }

        [Test]
        public void CurrentLocationUsesLocationIdAndHandlesEmptyValues()
        {
            GameObject root = new GameObject("Controller", typeof(StageHubSelectionController));
            try
            {
                StageHubSelectionController controller = root.GetComponent<StageHubSelectionController>();
                controller.sessionState = new GameSessionState();

                Assert.That(controller.IsCurrentLocation(null), Is.False);
                Assert.That(controller.IsCurrentLocation("location_home"), Is.False);

                controller.sessionState.locationState.currentLocationId = "location_home";
                Assert.That(controller.IsCurrentLocation("location_home"), Is.True);
                Assert.That(controller.IsCurrentLocation("HomeMapScene"), Is.False);

                controller.sessionState.locationState.currentLocationId = "location_school";
                Assert.That(controller.IsCurrentLocation("location_home"), Is.False);
                Assert.That(controller.IsCurrentLocation("location_school"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LayoutRefactorIsIdempotentWithoutChangingProductionPrefab()
        {
            byte[] productionBefore = File.ReadAllBytes(CanvasPrefabPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                MethodInfo applyToRoot = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("StudentGrowth.Editor.StageHubSelectionUiLayoutRefactor"))
                    .Where(type => type != null)
                    .Select(type => type.GetMethod("ApplyToRoot", BindingFlags.Public | BindingFlags.Static))
                    .FirstOrDefault(method => method != null);
                Assert.That(applyToRoot, Is.Not.Null, "找不到布局迁移工具的 ApplyToRoot 入口。");

                applyToRoot.Invoke(null, new object[] { instance });
                string firstSnapshot = BuildHierarchySnapshot(instance.transform);
                int firstHudCount = instance.GetComponentsInChildren<TimeActionHudView>(true).Length;
                int firstBadgeCount = CountNamed(instance.transform, "Panel_StatusBadge");
                int firstDividerCount = CountNamed(instance.transform, "Divider");

                applyToRoot.Invoke(null, new object[] { instance });
                string secondSnapshot = BuildHierarchySnapshot(instance.transform);

                Assert.That(secondSnapshot, Is.EqualTo(firstSnapshot));
                Assert.That(firstHudCount, Is.EqualTo(1));
                Assert.That(instance.GetComponentsInChildren<TimeActionHudView>(true).Length, Is.EqualTo(1));
                Assert.That(firstBadgeCount, Is.EqualTo(1));
                Assert.That(CountNamed(instance.transform, "Panel_StatusBadge"), Is.EqualTo(1));
                Assert.That(firstDividerCount, Is.EqualTo(1));
                Assert.That(CountNamed(instance.transform, "Divider"), Is.EqualTo(1));
                Assert.That(instance.transform.Find("Layer_07_RightResidentsEvents").childCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            byte[] productionAfter = File.ReadAllBytes(CanvasPrefabPath);
            Assert.That(productionAfter, Is.EqualTo(productionBefore), "幂等测试不得改写正式 Prefab。");
        }

        [Test]
        public void StageHubNodesUseUniformIntegerPixelLayout()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            StageHubLocationNodeView[] nodes = prefab.GetComponent<StageHubSelectionController>().nodes;

            Assert.That(nodes.Length, Is.EqualTo(6));
            foreach (StageHubLocationNodeView node in nodes)
            {
                RectTransform rect = node.transform as RectTransform;
                Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(240f, 210f)));
                Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
                Assert.That(node.nodeSprite.preserveAspect, Is.True);
                Assert.That(rect.anchoredPosition.x, Is.EqualTo(Mathf.Round(rect.anchoredPosition.x)));
                Assert.That(rect.anchoredPosition.y, Is.EqualTo(Mathf.Round(rect.anchoredPosition.y)));
            }
        }

        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(1366, 768)]
        [TestCase(1920, 1200)]
        public void TargetResolutionsKeepHudPanelsInsideAndClearOfMapNodes(int width, int height)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            CanvasScaler scaler = prefab.GetComponent<CanvasScaler>();
            Vector2 canvasSize = ResolveReferenceCanvasSize(width, height, scaler);

            Rect time = ResolveScreenRect(
                prefab.transform.Find("Layer_04_TopLeftTimeInfo/Panel_TimeInfo") as RectTransform,
                canvasSize);
            Rect status = ResolveScreenRect(
                prefab.transform.Find("Layer_05_BottomLeftPlayerStatus/Panel_Status") as RectTransform,
                canvasSize);
            Rect details = ResolveScreenRect(
                prefab.transform.Find("Layer_06_RightLocationInfo/Panel_LocationInfo") as RectTransform,
                canvasSize);
            Rect hints = ResolveScreenRect(
                prefab.transform.Find("Layer_08_BottomControlHint/Panel_ControlHint") as RectTransform,
                canvasSize);

            Assert.That(time.xMin, Is.GreaterThanOrEqualTo(20f));
            Assert.That(time.yMax, Is.LessThanOrEqualTo(canvasSize.y - 20f));
            Assert.That(status.xMin, Is.GreaterThanOrEqualTo(20f));
            Assert.That(status.yMin, Is.GreaterThanOrEqualTo(20f));
            Assert.That(details.xMax, Is.LessThanOrEqualTo(canvasSize.x - 20f));
            Assert.That(details.yMax, Is.LessThanOrEqualTo(canvasSize.y - 20f));
            Assert.That(hints.yMin, Is.GreaterThanOrEqualTo(20f));

            StageHubLocationNodeView[] nodes = prefab.GetComponent<StageHubSelectionController>().nodes;
            foreach (StageHubLocationNodeView node in nodes)
            {
                Rect nodeRect = ResolveScreenRect(node.transform as RectTransform, canvasSize);
                Assert.That(details.Overlaps(nodeRect), Is.False, node.name + " overlaps details at " + width + "x" + height);
                Assert.That(hints.Overlaps(nodeRect), Is.False, node.name + " overlaps hints at " + width + "x" + height);
                Assert.That(status.Overlaps(nodeRect), Is.False, node.name + " overlaps status at " + width + "x" + height);
                Assert.That(time.Overlaps(nodeRect), Is.False, node.name + " overlaps time HUD at " + width + "x" + height);
            }
        }

        private static Vector2 ResolveReferenceCanvasSize(int width, int height, CanvasScaler scaler)
        {
            float widthScale = width / scaler.referenceResolution.x;
            float heightScale = height / scaler.referenceResolution.y;
            float logWidth = Mathf.Log(widthScale, 2f);
            float logHeight = Mathf.Log(heightScale, 2f);
            float scale = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight));
            return new Vector2(width / scale, height / scale);
        }

        private static Rect ResolveScreenRect(RectTransform rect, Vector2 canvasSize)
        {
            Vector2 anchor = Vector2.Scale(rect.anchorMin, canvasSize);
            Vector2 bottomLeft = anchor + rect.anchoredPosition - Vector2.Scale(rect.pivot, rect.sizeDelta);
            return new Rect(bottomLeft, rect.sizeDelta);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null || root.name == name)
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

        private static int CountNamed(Transform root, string name)
        {
            int count = root.name == name ? 1 : 0;
            for (int i = 0; i < root.childCount; i++)
            {
                count += CountNamed(root.GetChild(i), name);
            }

            return count;
        }

        private static string BuildHierarchySnapshot(Transform root)
        {
            StringBuilder builder = new StringBuilder();
            Queue<Transform> pending = new Queue<Transform>();
            pending.Enqueue(root);
            while (pending.Count > 0)
            {
                Transform current = pending.Dequeue();
                builder.Append(current.name).Append('|')
                    .Append(current.GetSiblingIndex()).Append('|')
                    .Append(current.gameObject.activeSelf).Append('|')
                    .Append(current.childCount);
                if (current is RectTransform rect)
                {
                    builder.Append('|').Append(rect.anchorMin)
                        .Append('|').Append(rect.anchorMax)
                        .Append('|').Append(rect.pivot)
                        .Append('|').Append(rect.anchoredPosition)
                        .Append('|').Append(rect.sizeDelta);
                }

                builder.AppendLine();
                for (int i = 0; i < current.childCount; i++)
                {
                    pending.Enqueue(current.GetChild(i));
                }
            }

            return builder.ToString();
        }
    }
}
