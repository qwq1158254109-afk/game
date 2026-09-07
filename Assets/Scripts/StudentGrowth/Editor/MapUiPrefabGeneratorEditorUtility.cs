using System.IO;
using StudentGrowth;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudentGrowth.Editor
{
    public static class MapUiPrefabGeneratorEditorUtility
    {
        private const string ThemeAssetPath = "Assets/Resources/StudentGrowth/UI/StudentGrowthPixelTheme.asset";
        private const string PrefabDirectory = "Assets/Prefabs/StudentGrowth/UI/Map";
        private const string CanvasPrefabPath = PrefabDirectory + "/FullscreenMapCanvas.prefab";
        private const string HudPrefabPath = PrefabDirectory + "/FullscreenMapHud.prefab";
        private const string PromptPrefabPath = PrefabDirectory + "/MapInteractionPrompt.prefab";
        private const string FunctionMenuPrefabPath = PrefabDirectory + "/MapFunctionMenuView.prefab";
        private const string ConfirmPrefabPath = PrefabDirectory + "/MapInteractionConfirmView.prefab";
        private const string DialoguePrefabPath = PrefabDirectory + "/MapDialogueView.prefab";
        private const string ShopPrefabPath = PrefabDirectory + "/MapShopView.prefab";
        private const string ResultPrefabPath = PrefabDirectory + "/MapActivityResultView.prefab";
        private static readonly string[] MapScenePaths =
        {
            "Assets/Scenes/StudentGrowth/Maps/HomeMapScene.unity",
            "Assets/Scenes/StudentGrowth/Maps/ShopMapScene.unity",
            "Assets/Scenes/StudentGrowth/Maps/StageHubMapScene.unity"
        };

        [MenuItem("Tools/Student Growth/UI/Generate Map UI Prefabs")]
        public static void GenerateMapUiPrefabs()
        {
            UIThemeConfig theme = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(ThemeAssetPath);
            if (theme == null)
            {
                Debug.LogError("Student Growth map UI prefab generation failed. Theme asset missing: " + ThemeAssetPath);
                return;
            }

            EnsurePrefabDirectory();

            MapUiPrefabGenerationSummary summary = new MapUiPrefabGenerationSummary();
            GenerateCanvasPrefab(theme, summary);
            GenerateSingleViewPrefab(HudPrefabPath, theme, summary, MapUiFactory.CreateHudView);
            GenerateSingleViewPrefab(PromptPrefabPath, theme, summary, MapUiFactory.CreatePromptView);
            GenerateSingleViewPrefab(FunctionMenuPrefabPath, theme, summary, MapUiFactory.CreateFunctionMenuView);
            GenerateSingleViewPrefab(ConfirmPrefabPath, theme, summary, MapUiFactory.CreateInteractionConfirmView);
            GenerateSingleViewPrefab(DialoguePrefabPath, theme, summary, MapUiFactory.CreateDialogueView);
            GenerateSingleViewPrefab(ShopPrefabPath, theme, summary, MapUiFactory.CreateShopView);
            GenerateSingleViewPrefab(ResultPrefabPath, theme, summary, MapUiFactory.CreateActivityResultView);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(summary.BuildLog());
        }

        [MenuItem("Tools/Student Growth/UI/Bind Map UI Prefabs To Map Scenes")]
        public static void BindMapUiPrefabsToMapScenes()
        {
            GameObject canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            UIThemeConfig theme = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(ThemeAssetPath);
            if (canvasPrefab == null)
            {
                Debug.LogError("Student Growth map UI scene binding failed. Canvas prefab missing: " + CanvasPrefabPath);
                return;
            }

            MapUiSceneBindingSummary summary = new MapUiSceneBindingSummary();
            for (int i = 0; i < MapScenePaths.Length; i++)
            {
                BindScene(MapScenePaths[i], canvasPrefab, theme, summary);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(summary.BuildLog());
        }

        private static void GenerateCanvasPrefab(UIThemeConfig theme, MapUiPrefabGenerationSummary summary)
        {
            Canvas canvas = null;
            try
            {
                canvas = MapUiFactory.CreateFullscreenMapCanvas();
                RectTransform canvasRect = canvas.transform as RectTransform;
                MapUiFactory.CreateHudView(canvasRect);
                MapUiFactory.CreatePromptView(canvasRect);
                MapUiFactory.CreateFunctionMenuView(canvasRect);
                MapUiFactory.CreateInteractionConfirmView(canvasRect);
                MapUiFactory.CreateDialogueView(canvasRect);
                MapUiFactory.CreateShopView(canvasRect);
                MapUiFactory.CreateActivityResultView(canvasRect);
                ApplyTheme(canvas.gameObject, theme);
                SavePrefab(canvas.gameObject, CanvasPrefabPath, summary);
            }
            finally
            {
                if (canvas != null)
                {
                    Object.DestroyImmediate(canvas.gameObject);
                }
            }
        }

        private static void GenerateSingleViewPrefab<TView>(
            string path,
            UIThemeConfig theme,
            MapUiPrefabGenerationSummary summary,
            System.Func<RectTransform, TView> createView)
            where TView : Component
        {
            Canvas canvas = null;
            try
            {
                canvas = MapUiFactory.CreateFullscreenMapCanvas();
                RectTransform canvasRect = canvas.transform as RectTransform;
                TView view = createView(canvasRect);
                ApplyTheme(view.gameObject, theme);
                SavePrefab(view.gameObject, path, summary);
            }
            finally
            {
                if (canvas != null)
                {
                    Object.DestroyImmediate(canvas.gameObject);
                }
            }
        }

        private static void BindScene(
            string scenePath,
            GameObject canvasPrefab,
            UIThemeConfig theme,
            MapUiSceneBindingSummary summary)
        {
            if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath))
            {
                summary.AddIssue("Scene missing: " + scenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            summary.scenesScanned++;

            GameObject canvasRoot = FindSceneCanvasRoot();
            if (canvasRoot == null)
            {
                canvasRoot = PrefabUtility.InstantiatePrefab(canvasPrefab, scene) as GameObject;
                if (canvasRoot == null)
                {
                    summary.AddIssue("Could not instantiate map UI canvas prefab in scene: " + scenePath);
                    return;
                }

                canvasRoot.name = MapUiFactory.FullscreenMapCanvasName;
                summary.canvasesAdded++;
            }
            else
            {
                summary.canvasesReused++;
            }

            ApplyTheme(canvasRoot, theme);
            MapUiSceneViews views = MapUiSceneViews.Resolve(canvasRoot);
            if (!views.HasRequiredViews)
            {
                summary.AddIssue("Map UI canvas is missing one or more required views in scene: " + scenePath);
            }

            bool changed = false;
            changed |= BindSceneControllers(scenePath, views, summary);
            changed |= BindInteractionFlowControllers(views, summary);
            changed |= BindPlayerControllers(views.promptView, summary);
            changed |= BindPromptBridges(views.promptView, theme, summary);

            if (changed || summary.canvasesAdded > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            if (scene.isDirty)
            {
                EditorSceneManager.SaveScene(scene);
                summary.scenesChanged++;
            }
        }

        private static bool BindSceneControllers(
            string scenePath,
            MapUiSceneViews views,
            MapUiSceneBindingSummary summary)
        {
            bool changed = false;
            FullscreenMapSceneController[] controllers = Object.FindObjectsByType<FullscreenMapSceneController>(
                FindObjectsInactive.Include);

            if (controllers.Length == 0)
            {
                summary.AddIssue("FullscreenMapSceneController missing in scene: " + scenePath);
                return false;
            }

            for (int i = 0; i < controllers.Length; i++)
            {
                FullscreenMapSceneController controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                changed |= Assign(ref controller.hudView, views.hudView);
                changed |= Assign(ref controller.promptView, views.promptView);
                changed |= Assign(ref controller.functionMenuView, views.functionMenuView);
                changed |= Assign(ref controller.confirmView, views.confirmView);
                changed |= Assign(ref controller.dialogueView, views.dialogueView);
                changed |= Assign(ref controller.shopView, views.shopView);
                changed |= Assign(ref controller.resultView, views.resultView);
                if (controller.createFallbackUi)
                {
                    controller.createFallbackUi = false;
                    changed = true;
                }

                EditorUtility.SetDirty(controller);
                summary.sceneControllersBound++;
            }

            return changed;
        }

        private static bool BindInteractionFlowControllers(MapUiSceneViews views, MapUiSceneBindingSummary summary)
        {
            bool changed = false;
            MapInteractionFlowController[] flowControllers = Object.FindObjectsByType<MapInteractionFlowController>(
                FindObjectsInactive.Include);

            for (int i = 0; i < flowControllers.Length; i++)
            {
                MapInteractionFlowController flow = flowControllers[i];
                if (flow == null)
                {
                    continue;
                }

                changed |= Assign(ref flow.promptView, views.promptView);
                changed |= Assign(ref flow.confirmView, views.confirmView);
                changed |= Assign(ref flow.dialogueView, views.dialogueView);
                changed |= Assign(ref flow.shopView, views.shopView);
                changed |= Assign(ref flow.resultView, views.resultView);
                EditorUtility.SetDirty(flow);
                summary.flowControllersBound++;
            }

            return changed;
        }

        private static bool BindPlayerControllers(MapInteractionPromptView promptView, MapUiSceneBindingSummary summary)
        {
            bool changed = false;
            RpgPlayerController[] playerControllers = Object.FindObjectsByType<RpgPlayerController>(
                FindObjectsInactive.Include);

            for (int i = 0; i < playerControllers.Length; i++)
            {
                RpgPlayerController controller = playerControllers[i];
                if (controller == null)
                {
                    continue;
                }

                changed |= Assign(ref controller.promptView, promptView);
                EditorUtility.SetDirty(controller);
                summary.playerControllersBound++;
            }

            return changed;
        }

        private static bool BindPromptBridges(
            MapInteractionPromptView promptView,
            UIThemeConfig theme,
            MapUiSceneBindingSummary summary)
        {
            bool changed = false;
            InteractionPromptUI[] promptBridges = Object.FindObjectsByType<InteractionPromptUI>(
                FindObjectsInactive.Include);

            for (int i = 0; i < promptBridges.Length; i++)
            {
                InteractionPromptUI promptBridge = promptBridges[i];
                if (promptBridge == null)
                {
                    continue;
                }

                changed |= Assign(ref promptBridge.mapPromptView, promptView);
                changed |= Assign(ref promptBridge.theme, theme);
                EditorUtility.SetDirty(promptBridge);
                summary.promptBridgesBound++;
            }

            return changed;
        }

        private static GameObject FindSceneCanvasRoot()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas != null && canvas.gameObject.name == MapUiFactory.FullscreenMapCanvasName)
                {
                    return canvas.gameObject;
                }
            }

            return null;
        }

        private static bool Assign<T>(ref T target, T value) where T : Object
        {
            if (target == value)
            {
                return false;
            }

            target = value;
            return true;
        }

        private static void SavePrefab(GameObject root, string path, MapUiPrefabGenerationSummary summary)
        {
            if (root == null)
            {
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            summary.prefabsGenerated++;
            summary.AddPath(path);
        }

        private static void ApplyTheme(GameObject root, UIThemeConfig theme)
        {
            if (root == null || theme == null)
            {
                return;
            }

            FullscreenMapHudView[] hudViews = root.GetComponentsInChildren<FullscreenMapHudView>(true);
            for (int i = 0; i < hudViews.Length; i++)
            {
                hudViews[i].theme = theme;
                hudViews[i].ApplyStyle();
                hudViews[i].Refresh();
            }

            MapInteractionPromptView[] promptViews = root.GetComponentsInChildren<MapInteractionPromptView>(true);
            for (int i = 0; i < promptViews.Length; i++)
            {
                promptViews[i].theme = theme;
                promptViews[i].ApplyStyle();
            }

            MapFunctionMenuView[] functionMenus = root.GetComponentsInChildren<MapFunctionMenuView>(true);
            for (int i = 0; i < functionMenus.Length; i++)
            {
                functionMenus[i].theme = theme;
                functionMenus[i].ApplyStyle();
            }

            MapInteractionConfirmView[] confirmViews = root.GetComponentsInChildren<MapInteractionConfirmView>(true);
            for (int i = 0; i < confirmViews.Length; i++)
            {
                confirmViews[i].theme = theme;
                confirmViews[i].ApplyStyle();
            }

            MapDialogueView[] dialogueViews = root.GetComponentsInChildren<MapDialogueView>(true);
            for (int i = 0; i < dialogueViews.Length; i++)
            {
                dialogueViews[i].theme = theme;
                dialogueViews[i].ApplyStyle();
            }

            MapShopView[] shopViews = root.GetComponentsInChildren<MapShopView>(true);
            for (int i = 0; i < shopViews.Length; i++)
            {
                shopViews[i].theme = theme;
                shopViews[i].ApplyStyle();
            }

            MapActivityResultView[] resultViews = root.GetComponentsInChildren<MapActivityResultView>(true);
            for (int i = 0; i < resultViews.Length; i++)
            {
                resultViews[i].theme = theme;
                resultViews[i].ApplyStyle();
            }

            PixelPanel[] panels = root.GetComponentsInChildren<PixelPanel>(true);
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i].theme = theme;
                panels[i].ApplyStyle();
            }

            PixelButton[] buttons = root.GetComponentsInChildren<PixelButton>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].theme = theme;
                buttons[i].ApplyStyle();
            }

            PixelText[] texts = root.GetComponentsInChildren<PixelText>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].theme = theme;
                texts[i].ApplyStyle();
            }

            for (int i = 0; i < hudViews.Length; i++)
            {
                hudViews[i].ApplyStyle();
                hudViews[i].Refresh();
            }
        }

        private static void EnsurePrefabDirectory()
        {
            if (!Directory.Exists(PrefabDirectory))
            {
                Directory.CreateDirectory(PrefabDirectory);
            }
        }

        private sealed class MapUiPrefabGenerationSummary
        {
            public int prefabsGenerated;
            private readonly System.Text.StringBuilder paths = new System.Text.StringBuilder();

            public void AddPath(string path)
            {
                paths.AppendLine("- " + path);
            }

            public string BuildLog()
            {
                return "Student Growth Map UI Prefab Generation\n"
                    + "Prefabs generated: " + prefabsGenerated + "\n"
                    + paths;
            }
        }

        private sealed class MapUiSceneViews
        {
            public FullscreenMapHudView hudView;
            public MapInteractionPromptView promptView;
            public MapFunctionMenuView functionMenuView;
            public MapInteractionConfirmView confirmView;
            public MapDialogueView dialogueView;
            public MapShopView shopView;
            public MapActivityResultView resultView;

            public bool HasRequiredViews
            {
                get
                {
                    return hudView != null
                        && promptView != null
                        && functionMenuView != null
                        && confirmView != null
                        && dialogueView != null
                        && shopView != null
                        && resultView != null;
                }
            }

            public static MapUiSceneViews Resolve(GameObject root)
            {
                return new MapUiSceneViews
                {
                    hudView = root == null ? null : root.GetComponentInChildren<FullscreenMapHudView>(true),
                    promptView = root == null ? null : root.GetComponentInChildren<MapInteractionPromptView>(true),
                    functionMenuView = root == null ? null : root.GetComponentInChildren<MapFunctionMenuView>(true),
                    confirmView = root == null ? null : root.GetComponentInChildren<MapInteractionConfirmView>(true),
                    dialogueView = root == null ? null : root.GetComponentInChildren<MapDialogueView>(true),
                    shopView = root == null ? null : root.GetComponentInChildren<MapShopView>(true),
                    resultView = root == null ? null : root.GetComponentInChildren<MapActivityResultView>(true)
                };
            }
        }

        private sealed class MapUiSceneBindingSummary
        {
            public int scenesScanned;
            public int scenesChanged;
            public int canvasesAdded;
            public int canvasesReused;
            public int sceneControllersBound;
            public int flowControllersBound;
            public int playerControllersBound;
            public int promptBridgesBound;
            private readonly System.Text.StringBuilder issues = new System.Text.StringBuilder();

            public void AddIssue(string issue)
            {
                if (!string.IsNullOrEmpty(issue))
                {
                    issues.AppendLine("- " + issue);
                }
            }

            public string BuildLog()
            {
                System.Text.StringBuilder builder = new System.Text.StringBuilder();
                builder.AppendLine("Student Growth Map UI Scene Binding");
                builder.AppendLine("Scenes scanned: " + scenesScanned);
                builder.AppendLine("Scenes changed: " + scenesChanged);
                builder.AppendLine("Canvases added: " + canvasesAdded);
                builder.AppendLine("Canvases reused: " + canvasesReused);
                builder.AppendLine("Scene controllers bound: " + sceneControllersBound);
                builder.AppendLine("Flow controllers bound: " + flowControllersBound);
                builder.AppendLine("Player controllers bound: " + playerControllersBound);
                builder.AppendLine("Prompt bridges bound: " + promptBridgesBound);
                if (issues.Length > 0)
                {
                    builder.AppendLine("Issues:");
                    builder.Append(issues);
                }

                return builder.ToString();
            }
        }
    }
}
