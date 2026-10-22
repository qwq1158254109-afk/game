using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace StudentGrowth.Tests
{
    public sealed class SchoolMapHudV02Tests
    {
        private const string CanvasPrefabPath = "Assets/Prefabs/StudentGrowth/UI/Map/FullscreenMapCanvas.prefab";
        private const string ThemePath = "Assets/Resources/StudentGrowth/UI/SchoolMapHudTheme.asset";

        private GameObject owner;
        private StageData stage;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("SchoolMapHudV02Tests", typeof(RectTransform));
            stage = ScriptableObject.CreateInstance<StageData>();
            stage.displayName = "适应新学期";
            stage.description = "完成学校探索";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(stage);
        }

        [Test]
        public void SchoolPresentationUsesCompactPanelsAndOnlyRealData()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            UIThemeConfig theme = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(ThemePath);
            GameObject instance = Object.Instantiate(prefab, owner.transform);
            GameObject playerObject = new GameObject("Player");
            playerObject.transform.SetParent(owner.transform);
            RpgPlayerController playerController = playerObject.AddComponent<RpgPlayerController>();
            GameSessionState session = CreateSession();

            FullscreenMapHudView hud = instance.GetComponentInChildren<FullscreenMapHudView>(true);
            hud.ConfigureForMap(session, null, playerController, theme, true);
            hud.Refresh();

            Assert.That(hud.useSchoolPresentation, Is.True);
            Assert.That((hud.locationPanel.transform as RectTransform).sizeDelta, Is.EqualTo(new Vector2(340f, 104f)));
            Assert.That((hud.statsPanel.transform as RectTransform).sizeDelta, Is.EqualTo(new Vector2(340f, 40f)));
            Assert.That(hud.locationNameText.gameObject.activeSelf, Is.False);
            Assert.That(hud.stageText.gameObject.activeSelf, Is.False);
            Assert.That(hud.questPanel.gameObject.activeSelf, Is.False);
            Assert.That(hud.energyText.text, Is.EqualTo("精力 60/60"));
            Assert.That(hud.pressureText.text, Is.EqualTo("压力 20/100"));
            Assert.That(hud.moodText.text, Is.EqualTo("心情 60/100"));
            Assert.That(hud.moneyText.text, Is.EqualTo("金币 100"));
            Assert.That(hud.timeText.text, Is.EqualTo("阶段剩余 6"));
            Assert.That(hud.quickbarSlotTexts.Count(label => label.transform.parent.gameObject.activeSelf), Is.EqualTo(3));
            Assert.That(hud.quickbarSlotTexts[0].text, Is.EqualTo("E 互动"));
            Assert.That(hud.quickbarSlotTexts[1].text, Is.EqualTo("Shift 奔跑"));
            Assert.That(hud.quickbarSlotTexts[2].text, Is.EqualTo("Esc 返回"));
            Assert.That(hud.energyText.font, Is.SameAs(theme.font));
        }

        [Test]
        public void CompactTimeHudUsesSingleEventDrivenView()
        {
            UIThemeConfig theme = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(ThemePath);
            RectTransform parent = owner.transform as RectTransform;
            TimeActionHudView view = MapUiFactory.CreateTimeActionHudView(parent, theme, true);
            GameObject controllerObject = new GameObject("TimeActionController");
            controllerObject.transform.SetParent(owner.transform);
            TimeActionController controller = controllerObject.AddComponent<TimeActionController>();
            GameSessionState session = CreateSession();
            controller.SetSessionState(session);
            view.Configure(session, controller, theme, true, true);

            Assert.That(owner.GetComponentsInChildren<TimeActionHudView>(true).Length, Is.EqualTo(1));
            Assert.That(view.compactSchoolLayout, Is.True);
            Assert.That(view.useEventOnlyRefresh, Is.True);
            Assert.That(view.dateLogoBackground.gameObject.activeSelf, Is.False);
            Assert.That(view.periodBackground.gameObject.activeSelf, Is.False);
            Assert.That((view.transform as RectTransform).sizeDelta, Is.EqualTo(new Vector2(330f, 76f)));
            Assert.That(view.actionPointText.text, Is.EqualTo("今天 3 次"));

            Assert.That(controller.TryConsumeAction(1, out _), Is.True);
            Assert.That(view.actionPointText.text, Is.EqualTo("今天 2 次"));
        }

        [Test]
        public void LocationToastUsesRealLocationAndStageThenCanHide()
        {
            UIThemeConfig theme = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(ThemePath);
            LocationToastView toast = MapUiFactory.CreateLocationToastView(owner.transform as RectTransform, theme);

            toast.Show("学校", stage, "地图探索");

            Assert.That(toast.gameObject.activeSelf, Is.True);
            Assert.That(toast.locationText.text, Is.EqualTo("学校"));
            Assert.That(toast.stageText.text, Is.EqualTo("阶段  适应新学期 · 地图探索"));
            Assert.That(toast.canvasGroup.alpha, Is.EqualTo(1f));

            toast.HideImmediate();
            Assert.That(toast.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void SchoolThemeIsIsolatedAndHasLicensedPixelFont()
        {
            UIThemeConfig school = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(ThemePath);
            UIThemeConfig shared = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(
                "Assets/Resources/StudentGrowth/UI/StudentGrowthPixelTheme.asset");

            Assert.That(school, Is.Not.Null);
            Assert.That(shared, Is.Not.Null);
            Assert.That(school, Is.Not.SameAs(shared));
            Assert.That(school.font, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(school.font),
                Is.EqualTo("Assets/Art/Pixel/UI_New/Fonts/FusionPixel12pxProportionalSC.ttf"));
            Assert.That(school.hudPlateSprite, Is.Null);
            Assert.That(shared.hudPlateSprite, Is.Not.Null);
        }

        [TestCase("location_home")]
        [TestCase("location_school")]
        [TestCase("location_shop")]
        [TestCase("location_park")]
        [TestCase("location_library")]
        [TestCase("location_club")]
        public void ExplorationMapsResolveTheClassroomHudTheme(string mapId)
        {
            GameObject controllerObject = new GameObject("MapController_" + mapId);
            controllerObject.transform.SetParent(owner.transform);
            FullscreenMapSceneController controller = controllerObject.AddComponent<FullscreenMapSceneController>();
            controller.mapId = mapId;
            controller.autoFindRuntimeServices = false;
            controller.createFallbackRuntimeServices = false;
            controller.createFallbackUi = false;

            controller.RefreshHud();

            Assert.That(controller.hudTheme, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(controller.hudTheme), Is.EqualTo(ThemePath));
        }

        [Test]
        public void StageHubDoesNotResolveTheClassroomHudTheme()
        {
            GameObject controllerObject = new GameObject("StageHubController");
            controllerObject.transform.SetParent(owner.transform);
            FullscreenMapSceneController controller = controllerObject.AddComponent<FullscreenMapSceneController>();
            controller.mapId = "stage_hub";
            controller.autoFindRuntimeServices = false;
            controller.createFallbackRuntimeServices = false;
            controller.createFallbackUi = false;

            controller.RefreshHud();

            Assert.That(controller.hudTheme, Is.Null);
        }

        private GameSessionState CreateSession()
        {
            GameSessionState session = new GameSessionState();
            session.playerState.energy = 60;
            session.playerState.maxEnergy = 60;
            session.playerState.pressure = 20;
            session.playerState.mood = 60;
            session.playerState.money = 100;
            session.stageState.currentStage = stage;
            session.stageState.remainingTime = 6;
            session.timeActionState.currentDay = 1;
            session.timeActionState.currentPeriod = TimePeriod.Morning;
            session.timeActionState.currentActionPoints = TimeActionState.ActionPointsPerPeriod;
            return session;
        }
    }
}
