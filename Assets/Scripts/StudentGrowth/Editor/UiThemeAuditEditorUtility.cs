using System;
using System.IO;
using System.Text;
using StudentGrowth;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StudentGrowth.Editor
{
    public static class UiThemeAuditEditorUtility
    {
        private const string ThemeAssetPath = "Assets/Resources/StudentGrowth/UI/StudentGrowthPixelTheme.asset";
        private const string AuditReportPath = "Docs/UI_THEME_AUDIT_LATEST.md";
        private const int AuditSuccessExitCode = 0;
        private const int AuditFailureExitCode = 1;
        private static readonly string[] MapScenePaths =
        {
            "Assets/Scenes/StudentGrowth/Maps/HomeMapScene.unity",
            "Assets/Scenes/StudentGrowth/Maps/ShopMapScene.unity",
            "Assets/Scenes/StudentGrowth/Maps/StageHubMapScene.unity"
        };
        private static readonly StudentGrowthMvpRuntimeUi.ThemeAuditPreviewMode[] LegacyRuntimePreviewModes =
        {
            StudentGrowthMvpRuntimeUi.ThemeAuditPreviewMode.Stage,
            StudentGrowthMvpRuntimeUi.ThemeAuditPreviewMode.HomeLocation,
            StudentGrowthMvpRuntimeUi.ThemeAuditPreviewMode.BossIntro,
            StudentGrowthMvpRuntimeUi.ThemeAuditPreviewMode.ChallengeBattle,
            StudentGrowthMvpRuntimeUi.ThemeAuditPreviewMode.BossResult,
            StudentGrowthMvpRuntimeUi.ThemeAuditPreviewMode.Ending
        };

        [MenuItem("Tools/Student Growth/UI/Run Theme Audit")]
        public static void RunThemeAudit()
        {
            UiThemeAuditSummary summary = BuildAuditSummary();
            Debug.Log(summary.BuildLog());
        }

        [MenuItem("Tools/Student Growth/UI/Export Theme Audit Markdown")]
        public static void ExportThemeAuditMarkdown()
        {
            UiThemeAuditSummary summary = BuildAuditSummary();
            WriteAuditMarkdown(summary);
        }

        [MenuItem("Tools/Student Growth/UI/Validate Theme Audit")]
        public static void ValidateThemeAudit()
        {
            UiThemeAuditSummary summary = BuildAuditSummary();
            WriteAuditMarkdown(summary);

            if (summary.Issues.Count == 0)
            {
                Debug.Log("Student Growth UI theme audit passed.\n" + summary.BuildLog());
                EditorUtility.DisplayDialog("Student Growth UI", "Theme audit passed.\n\nReport: " + AuditReportPath, "OK");
                return;
            }

            Debug.LogError("Student Growth UI theme audit failed.\n" + summary.BuildLog());
            EditorUtility.DisplayDialog(
                "Student Growth UI",
                "Theme audit failed with " + summary.Issues.Count + " issue(s).\n\nReport: " + AuditReportPath,
                "OK");
        }

        public static void ValidateThemeAuditForBatchMode()
        {
            UiThemeAuditSummary summary = BuildAuditSummary();
            WriteAuditMarkdown(summary);

            if (summary.Issues.Count == 0)
            {
                Debug.Log("Student Growth UI theme audit passed.\n" + summary.BuildLog());
                ExitBatchMode(AuditSuccessExitCode);
                return;
            }

            Debug.LogError("Student Growth UI theme audit failed.\n" + summary.BuildLog());
            ExitBatchMode(AuditFailureExitCode);
        }

        public static UiThemeAuditSummary CreateThemeAuditSummary()
        {
            return BuildAuditSummary();
        }

        [MenuItem("Tools/Student Growth/UI/Auto-Fix Current Scene Theme Components")]
        public static void AutoFixCurrentSceneThemeComponents()
        {
            UIThemeConfig theme = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(ThemeAssetPath);
            if (theme == null)
            {
                EditorUtility.DisplayDialog("Student Growth UI", "Theme asset missing:\n" + ThemeAssetPath, "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "Student Growth UI",
                "Add or refresh PixelButton / PixelPanel / PixelText components in currently loaded scene canvases?\n\nPrefab assets will not be modified.",
                "Run",
                "Cancel"))
            {
                return;
            }

            UiThemeFixSummary summary = new UiThemeFixSummary();
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null)
                {
                    continue;
                }

                int changedCount = FixUiRoot(canvas.gameObject, theme, summary, true);
                if (changedCount > 0)
                {
                    Scene scene = canvas.gameObject.scene;
                    if (scene.IsValid())
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                    }
                }
            }

            Debug.Log(summary.BuildLog());
        }

        [MenuItem("Tools/Student Growth/UI/Auto-Fix Prefab Theme Components")]
        public static void AutoFixPrefabThemeComponents()
        {
            UIThemeConfig theme = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(ThemeAssetPath);
            if (theme == null)
            {
                EditorUtility.DisplayDialog("Student Growth UI", "Theme asset missing:\n" + ThemeAssetPath, "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "Student Growth UI",
                "Add or refresh PixelButton / PixelPanel / PixelText components in prefab assets under Assets/?\n\nAssets/Deprecated/ will be skipped.",
                "Run",
                "Cancel"))
            {
                return;
            }

            UiThemeFixSummary summary = new UiThemeFixSummary();
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (ShouldSkipPrefabPath(path))
                {
                    continue;
                }

                summary.prefabsScanned++;
                GameObject prefabRoot = null;
                try
                {
                    prefabRoot = PrefabUtility.LoadPrefabContents(path);
                    int changedCount = FixUiRoot(prefabRoot, theme, summary, false);
                    if (changedCount > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                        summary.prefabsChanged++;
                    }
                }
                catch (Exception exception)
                {
                    summary.AddPrefabError(path, exception);
                }
                finally
                {
                    if (prefabRoot != null)
                    {
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(summary.BuildLog());
        }

        [MenuItem("Tools/Student Growth/UI/Add Runtime Audit Reporter To Scene Canvases")]
        public static void AddRuntimeAuditReporterToSceneCanvases()
        {
            UIThemeConfig theme = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(ThemeAssetPath);
            if (theme == null)
            {
                EditorUtility.DisplayDialog("Student Growth UI", "Theme asset missing:\n" + ThemeAssetPath, "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "Student Growth UI",
                "Add UiThemeRuntimeAuditReporter to currently loaded scene canvases?\n\nReporters default to manual audit and will not log automatically on Play.",
                "Add",
                "Cancel"))
            {
                return;
            }

            UiThemeRuntimeAuditAttachSummary summary = new UiThemeRuntimeAuditAttachSummary();
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null)
                {
                    continue;
                }

                summary.canvasesScanned++;
                UiThemeRuntimeAuditReporter reporter = canvas.GetComponent<UiThemeRuntimeAuditReporter>();
                if (reporter == null)
                {
                    reporter = Undo.AddComponent<UiThemeRuntimeAuditReporter>(canvas.gameObject);
                    summary.reportersAdded++;
                }
                else
                {
                    Undo.RecordObject(reporter, "Refresh UI Theme Runtime Audit Reporter");
                    summary.reportersRefreshed++;
                }

                reporter.Configure(
                    theme,
                    "Scene:" + canvas.gameObject.scene.name,
                    false,
                    true,
                    1,
                    false);

                EditorUtility.SetDirty(reporter);
                Scene scene = canvas.gameObject.scene;
                if (scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            Debug.Log(summary.BuildLog());
        }

        private static UiThemeAuditSummary BuildAuditSummary()
        {
            UiThemeAuditSummary summary = new UiThemeAuditSummary();
            AuditThemeAsset(summary);
            AuditLoadedSceneObjects(summary);
            AuditLegacyRuntimePreview(summary);
            AuditMapSceneAssets(summary);
            AuditPrefabAssets(summary);
            return summary;
        }

        private static void WriteAuditMarkdown(UiThemeAuditSummary summary)
        {
            string directory = Path.GetDirectoryName(AuditReportPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(AuditReportPath, summary == null ? string.Empty : summary.BuildMarkdown());
            AssetDatabase.Refresh();
            Debug.Log("Student Growth UI theme audit exported: " + AuditReportPath);
        }

        private static void ExitBatchMode(int exitCode)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static void AuditThemeAsset(UiThemeAuditSummary summary)
        {
            UIThemeConfig theme = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(ThemeAssetPath);
            if (theme == null)
            {
                summary.AddIssue("Theme asset missing: " + ThemeAssetPath);
                return;
            }

            summary.themeFound = true;
            CheckSprite(summary, "panelSprite", theme.panelSprite);
            CheckSprite(summary, "buttonSprite", theme.buttonSprite);
            CheckSprite(summary, "buttonSelectedSprite", theme.buttonSelectedSprite);
            CheckSprite(summary, "hudPlateSprite", theme.hudPlateSprite);
            CheckSprite(summary, "dialoguePanelSprite", theme.dialoguePanelSprite);
            CheckSprite(summary, "shopPanelSprite", theme.shopPanelSprite);
            CheckSprite(summary, "inventoryPanelSprite", theme.inventoryPanelSprite);
            CheckSprite(summary, "battlePanelSprite", theme.battlePanelSprite);
            CheckSprite(summary, "resultPanelSprite", theme.resultPanelSprite);
            CheckSprite(summary, "moneyIcon", theme.moneyIcon);
            CheckSprite(summary, "timeIcon", theme.timeIcon);
            CheckSprite(summary, "energyIcon", theme.energyIcon);
            CheckSprite(summary, "moodIcon", theme.moodIcon);
            CheckSprite(summary, "stressIcon", theme.stressIcon);
            CheckSprite(summary, "relationIcon", theme.relationIcon);
            CheckSprite(summary, "inventoryIcon", theme.inventoryIcon);
            CheckSprite(summary, "cardsIcon", theme.cardsIcon);
            CheckSprite(summary, "settingsIcon", theme.settingsIcon);
            CheckSprite(summary, "backIcon", theme.backIcon);
            CheckSprite(summary, "confirmIcon", theme.confirmIcon);
            CheckSprite(summary, "cancelIcon", theme.cancelIcon);
        }

        private static void AuditLoadedSceneObjects(UiThemeAuditSummary summary)
        {
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null)
                {
                    continue;
                }

                UiThemeAuditUtility.AuditUiRoot(summary, canvas.gameObject, "Scene:" + canvas.gameObject.scene.name);
            }
        }

        private static void AuditLegacyRuntimePreview(UiThemeAuditSummary summary)
        {
            UIThemeConfig theme = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(ThemeAssetPath);
            if (theme == null)
            {
                return;
            }

            for (int i = 0; i < LegacyRuntimePreviewModes.Length; i++)
            {
                AuditLegacyRuntimePreviewMode(summary, theme, LegacyRuntimePreviewModes[i]);
            }
        }

        private static void AuditLegacyRuntimePreviewMode(
            UiThemeAuditSummary summary,
            UIThemeConfig theme,
            StudentGrowthMvpRuntimeUi.ThemeAuditPreviewMode previewMode)
        {
            EventSystem[] existingEventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
            GameObject previewRoot = null;

            try
            {
                previewRoot = new GameObject("StudentGrowth Legacy Runtime UI Audit Preview " + previewMode);
                previewRoot.hideFlags = HideFlags.HideAndDontSave;

                StudentGrowthMvpRuntimeUi runtimeUi = previewRoot.AddComponent<StudentGrowthMvpRuntimeUi>();
                runtimeUi.BuildThemeAuditPreview(theme, previewMode);
                UiThemeAuditUtility.AuditUiRoot(summary, previewRoot, "LegacyRuntimePreview:" + previewMode);
            }
            catch (Exception exception)
            {
                summary.AddIssue("Legacy runtime UI preview audit failed for " + previewMode + ": " + exception.Message);
                UnityEngine.Debug.LogException(exception);
            }
            finally
            {
                if (previewRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(previewRoot);
                }

                EventSystem[] eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
                for (int i = 0; i < eventSystems.Length; i++)
                {
                    EventSystem eventSystem = eventSystems[i];
                    if (eventSystem != null && !ContainsEventSystem(existingEventSystems, eventSystem))
                    {
                        UnityEngine.Object.DestroyImmediate(eventSystem.gameObject);
                    }
                }
            }
        }

        private static bool ContainsEventSystem(EventSystem[] systems, EventSystem target)
        {
            if (systems == null || target == null)
            {
                return false;
            }

            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AuditMapSceneAssets(UiThemeAuditSummary summary)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            for (int i = 0; i < MapScenePaths.Length; i++)
            {
                string path = MapScenePaths[i];
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    summary.AddIssue("Map scene missing: " + path);
                    continue;
                }

                Scene scene = FindLoadedSceneByPath(path);
                bool openedForAudit = !scene.IsValid();
                if (openedForAudit)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }

                AuditSceneCanvases(summary, scene, "SceneAsset:" + path);

                if (openedForAudit && scene.IsValid())
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                EditorSceneManager.SetActiveScene(activeScene);
            }
        }

        private static void AuditSceneCanvases(UiThemeAuditSummary summary, Scene scene, string scope)
        {
            if (!scene.IsValid())
            {
                summary.AddIssue("Could not open scene for UI audit: " + scope);
                return;
            }

            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            int canvasCount = 0;
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || canvas.gameObject.scene != scene)
                {
                    continue;
                }

                canvasCount++;
                UiThemeAuditUtility.AuditUiRoot(summary, canvas.gameObject, scope + ":" + canvas.gameObject.name);
            }

            if (canvasCount == 0)
            {
                summary.AddIssue("Map scene has no Canvas for UI audit: " + scope);
            }
        }

        private static Scene FindLoadedSceneByPath(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.path == path)
                {
                    return scene;
                }
            }

            return default;
        }

        private static void AuditPrefabAssets(UiThemeAuditSummary summary)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (ShouldSkipPrefabPath(path))
                {
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                UiThemeAuditUtility.AuditUiRoot(summary, prefab, "Prefab:" + path);
            }
        }

        private static int FixUiRoot(GameObject root, UIThemeConfig theme, UiThemeFixSummary summary, bool useUndo)
        {
            if (root == null || theme == null)
            {
                return 0;
            }

            int changedCount = 0;
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (FixButton(buttons[i], theme, summary, useUndo))
                {
                    changedCount++;
                }
            }

            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (FixPanelImage(images[i], theme, summary, useUndo))
                {
                    changedCount++;
                }
            }

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (FixText(texts[i], theme, summary, useUndo))
                {
                    changedCount++;
                }
            }

            return changedCount;
        }

        private static bool FixButton(Button button, UIThemeConfig theme, UiThemeFixSummary summary, bool useUndo)
        {
            if (button == null)
            {
                return false;
            }

            summary.buttonsScanned++;
            PixelButton pixelButton = button.GetComponent<PixelButton>();
            if (pixelButton == null)
            {
                pixelButton = AddComponent<PixelButton>(button.gameObject, useUndo);
                summary.pixelButtonsAdded++;
            }
            else
            {
                RecordObject(pixelButton, "Refresh PixelButton Theme", useUndo);
            }

            Image background = button.targetGraphic as Image;
            if (background == null)
            {
                background = button.GetComponent<Image>();
            }

            RecordObject(button, "Refresh Button Theme", useUndo);
            if (background != null)
            {
                RecordObject(background, "Refresh Button Theme", useUndo);
            }

            Text labelText = button.GetComponentInChildren<Text>(true);
            if (labelText != null)
            {
                RecordObject(labelText, "Refresh Button Theme", useUndo);
            }

            pixelButton.theme = theme;
            pixelButton.background = background;
            pixelButton.labelText = labelText;
            pixelButton.animateScale = true;
            pixelButton.ApplyStyle();

            EditorUtility.SetDirty(button);
            EditorUtility.SetDirty(pixelButton);
            if (background != null)
            {
                EditorUtility.SetDirty(background);
            }

            if (pixelButton.labelText != null)
            {
                EditorUtility.SetDirty(pixelButton.labelText);
            }

            summary.buttonsStyled++;
            return true;
        }

        private static bool FixPanelImage(Image image, UIThemeConfig theme, UiThemeFixSummary summary, bool useUndo)
        {
            if (image == null || !UiThemeAuditUtility.LooksLikePanel(image.gameObject.name))
            {
                return false;
            }

            summary.panelCandidatesScanned++;
            PixelPanel pixelPanel = image.GetComponent<PixelPanel>();
            if (pixelPanel == null)
            {
                pixelPanel = AddComponent<PixelPanel>(image.gameObject, useUndo);
                summary.pixelPanelsAdded++;
            }
            else
            {
                RecordObject(pixelPanel, "Refresh PixelPanel Theme", useUndo);
            }

            RecordObject(image, "Refresh PixelPanel Theme", useUndo);
            Text titleText = image.GetComponentInChildren<Text>(true);
            if (titleText != null)
            {
                RecordObject(titleText, "Refresh PixelPanel Theme", useUndo);
            }

            pixelPanel.theme = theme;
            pixelPanel.background = image;
            pixelPanel.titleText = titleText;
            pixelPanel.variant = ResolvePanelVariant(image);
            pixelPanel.ApplyStyle();

            EditorUtility.SetDirty(pixelPanel);
            EditorUtility.SetDirty(image);
            if (pixelPanel.titleText != null)
            {
                EditorUtility.SetDirty(pixelPanel.titleText);
            }

            summary.panelsStyled++;
            return true;
        }

        private static bool FixText(Text text, UIThemeConfig theme, UiThemeFixSummary summary, bool useUndo)
        {
            if (text == null)
            {
                return false;
            }

            summary.textsScanned++;
            PixelText pixelText = text.GetComponent<PixelText>();
            if (pixelText == null)
            {
                pixelText = AddComponent<PixelText>(text.gameObject, useUndo);
                summary.pixelTextsAdded++;
            }
            else
            {
                RecordObject(pixelText, "Refresh PixelText Theme", useUndo);
            }

            RecordObject(text, "Refresh PixelText Theme", useUndo);
            pixelText.theme = theme;
            pixelText.target = text;
            pixelText.textStyle = ResolveTextStyle(text);
            pixelText.inverse = ShouldUseInverseText(text);
            pixelText.ApplyStyle();

            EditorUtility.SetDirty(text);
            EditorUtility.SetDirty(pixelText);
            summary.textsStyled++;
            return true;
        }

        private static T AddComponent<T>(GameObject target, bool useUndo) where T : Component
        {
            return useUndo ? Undo.AddComponent<T>(target) : target.AddComponent<T>();
        }

        private static void RecordObject(UnityEngine.Object target, string label, bool useUndo)
        {
            if (useUndo && target != null)
            {
                Undo.RecordObject(target, label);
            }
        }

        private static GameUiTextStyle ResolveTextStyle(Text text)
        {
            if (text != null && text.GetComponentInParent<Button>() != null)
            {
                return GameUiTextStyle.ButtonText;
            }

            return text == null
                ? GameUiTextStyle.Body
                : GameUiStyle.ResolveTextStyle(text.fontSize, text.fontStyle);
        }

        private static bool ShouldUseInverseText(Text text)
        {
            if (text == null)
            {
                return false;
            }

            if (text.GetComponentInParent<Button>() != null)
            {
                return true;
            }

            PixelPanel panel = text.GetComponentInParent<PixelPanel>();
            return panel != null
                && (panel.variant == GameUiPanelVariant.Dark
                    || panel.variant == GameUiPanelVariant.Success
                    || panel.variant == GameUiPanelVariant.Warning);
        }

        private static GameUiPanelVariant ResolvePanelVariant(Image image)
        {
            if (image == null)
            {
                return GameUiPanelVariant.Standard;
            }

            Color color = image.color;
            return color.r < 0.16f && color.g < 0.18f && color.b < 0.18f
                ? GameUiPanelVariant.Dark
                : GameUiPanelVariant.Standard;
        }

        private static void CheckSprite(UiThemeAuditSummary summary, string label, Sprite sprite)
        {
            summary.themeSpriteChecks++;
            if (sprite == null)
            {
                summary.AddIssue("Theme sprite missing: " + label);
            }
        }

        private static bool ShouldSkipPrefabPath(string path)
        {
            return string.IsNullOrEmpty(path) || path.StartsWith("Assets/Deprecated/");
        }

        private sealed class UiThemeFixSummary
        {
            public int prefabsScanned;
            public int prefabsChanged;
            public int prefabErrors;
            public int buttonsScanned;
            public int pixelButtonsAdded;
            public int buttonsStyled;
            public int panelCandidatesScanned;
            public int pixelPanelsAdded;
            public int panelsStyled;
            public int textsScanned;
            public int pixelTextsAdded;
            public int textsStyled;
            private readonly StringBuilder prefabErrorDetails = new StringBuilder();

            public void AddPrefabError(string path, Exception exception)
            {
                prefabErrors++;
                prefabErrorDetails.Append(path);
                prefabErrorDetails.Append(": ");
                prefabErrorDetails.AppendLine(exception == null ? "Unknown error" : exception.Message);
            }

            public string BuildLog()
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("Student Growth UI Theme Auto-Fix");
                builder.AppendLine("Prefabs scanned: " + prefabsScanned);
                builder.AppendLine("Prefabs changed: " + prefabsChanged);
                builder.AppendLine("Prefab errors: " + prefabErrors);
                builder.AppendLine("Buttons scanned: " + buttonsScanned);
                builder.AppendLine("PixelButton components added: " + pixelButtonsAdded);
                builder.AppendLine("Buttons styled: " + buttonsStyled);
                builder.AppendLine("Panel-like images scanned: " + panelCandidatesScanned);
                builder.AppendLine("PixelPanel components added: " + pixelPanelsAdded);
                builder.AppendLine("Panels styled: " + panelsStyled);
                builder.AppendLine("Texts scanned: " + textsScanned);
                builder.AppendLine("PixelText components added: " + pixelTextsAdded);
                builder.AppendLine("Texts styled: " + textsStyled);
                if (prefabErrorDetails.Length > 0)
                {
                    builder.AppendLine("Prefab error details:");
                    builder.Append(prefabErrorDetails);
                }

                return builder.ToString();
            }
        }

        private sealed class UiThemeRuntimeAuditAttachSummary
        {
            public int canvasesScanned;
            public int reportersAdded;
            public int reportersRefreshed;

            public string BuildLog()
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("Student Growth UI Runtime Audit Reporter Setup");
                builder.AppendLine("Canvases scanned: " + canvasesScanned);
                builder.AppendLine("Reporters added: " + reportersAdded);
                builder.AppendLine("Reporters refreshed: " + reportersRefreshed);
                return builder.ToString();
            }
        }
    }
}
