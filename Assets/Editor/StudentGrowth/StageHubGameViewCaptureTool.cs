using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StudentGrowth.Editor
{
    public static class StageHubGameViewCaptureTool
    {
        private const string ScenePath = "Assets/Scenes/StudentGrowth/Maps/StageHubMapScene.unity";
        private const string PrefabPath = "Assets/Prefabs/StudentGrowth/UI/StageHub/StageHubSelectionCanvas.prefab";
        private const string OutputDirectory = "docs/qa/stagehub-ui";
        private const int LayoutWaitFrames = 120;

        private static readonly CaptureTarget[] Targets =
        {
            new CaptureTarget(1920, 1080),
            new CaptureTarget(2560, 1440),
            new CaptureTarget(1366, 768),
            new CaptureTarget(1920, 1200)
        };

        private static readonly List<CaptureRecord> Records = new List<CaptureRecord>();
        private static EditorWindow gameView;
        private static PropertyInfo selectedSizeIndexProperty;
        private static string previousScenePath;
        private static int previousSizeIndex = -1;
        private static int targetIndex;
        private static int waitFrames;
        private static string pendingTempPath;
        private static bool captureRequested;
        private static bool active;
        private static bool restorePending;
        private static string failureMessage;
        private static bool playModeSettingsOverridden;
        private static bool finalTextRefreshPending;
        private static int textRefreshPassesRemaining;
        private static bool previousEnterPlayModeOptionsEnabled;
        private static EnterPlayModeOptions previousEnterPlayModeOptions;

        [MenuItem("Tools/Student Growth/QA/Capture Stage Hub Fixed Resolutions")]
        public static void CaptureAll()
        {
            if (active)
            {
                Debug.LogWarning("[StageHubCapture] 截图任务已在运行。");
                return;
            }

            if (SceneManager.GetActiveScene().isDirty)
            {
                Debug.LogError("[StageHubCapture] 当前场景存在未保存修改。请先保存或撤销，再执行截图，避免覆盖编辑状态。");
                return;
            }

            try
            {
                PrepareGameView();
                previousScenePath = SceneManager.GetActiveScene().path;
                previousSizeIndex = GetSelectedSizeIndex();
                EnsureAllFixedResolutionPresets();
                OverridePlayModeSettings();

                Records.Clear();
                failureMessage = string.Empty;
                targetIndex = 0;
                waitFrames = 0;
                pendingTempPath = string.Empty;
                captureRequested = false;
                restorePending = false;
                active = true;

                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                EditorApplication.update += Tick;
                EditorApplication.isPlaying = true;
                Debug.Log("[StageHubCapture] 已启动四分辨率 Game View 截图任务。");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ResetState();
            }
        }

        [MenuItem("Tools/Student Growth/QA/Capture Stage Hub Fixed Resolutions", true)]
        private static bool CanCaptureAll()
        {
            return !active && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void Tick()
        {
            try
            {
                if (restorePending)
                {
                    RestoreEditorStateWhenReady();
                    return;
                }

                if (!EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying)
                {
                    return;
                }

                if (targetIndex >= Targets.Length)
                {
                    CompleteCapture();
                    return;
                }

                CaptureTarget target = Targets[targetIndex];
                if (waitFrames == 0 && !captureRequested)
                {
                    int sizeIndex = FindFixedResolutionIndex(target.Width, target.Height);
                    if (sizeIndex < 0)
                    {
                        throw new InvalidOperationException("无法找到 Game View 预设：" + target.PresetName);
                    }

                    SetSelectedSizeIndex(sizeIndex);
                    gameView.Focus();
                    gameView.Repaint();
                    waitFrames = LayoutWaitFrames;
                    finalTextRefreshPending = true;
                    textRefreshPassesRemaining = 3;
                    Debug.Log($"[StageHubCapture] 已选择 {target.PresetName}，等待 Canvas 刷新。");
                    return;
                }

                if (waitFrames > 0)
                {
                    waitFrames--;
                    if (waitFrames == 0)
                    {
                        if (finalTextRefreshPending)
                        {
                            RefreshTextGeometry();
                            textRefreshPassesRemaining--;
                            finalTextRefreshPending = textRefreshPassesRemaining > 0;
                            waitFrames = 5;
                        }
                        else
                        {
                            RequestScreenshot(target);
                        }
                    }
                    return;
                }

                if (captureRequested)
                {
                    TryFinalizeScreenshot(target);
                }
            }
            catch (Exception exception)
            {
                failureMessage = exception.ToString();
                Debug.LogError("[StageHubCapture] 截图失败：" + exception.Message);
                BeginRestore();
            }
        }

        private static void RequestScreenshot(CaptureTarget target)
        {
            string tempDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/StageHubCapture"));
            Directory.CreateDirectory(tempDirectory);
            pendingTempPath = Path.Combine(tempDirectory, target.FileName + ".tmp.png");
            DeleteIfExists(pendingTempPath);
            ScreenCapture.CaptureScreenshot(pendingTempPath, 1);
            captureRequested = true;
        }

        private static void RefreshTextGeometry()
        {
            Text[] texts = UnityEngine.Object.FindObjectsByType<Text>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text.font != null && !string.IsNullOrEmpty(text.text))
                {
                    text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
                }
            }

            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].SetAllDirty();
            }

            Canvas.ForceUpdateCanvases();
            gameView.Repaint();
        }

        private static void TryFinalizeScreenshot(CaptureTarget target)
        {
            if (!File.Exists(pendingTempPath) || new FileInfo(pendingTempPath).Length < 24)
            {
                return;
            }

            ReadPngDimensions(pendingTempPath, out int actualWidth, out int actualHeight);
            if (actualWidth != target.Width || actualHeight != target.Height)
            {
                DeleteIfExists(pendingTempPath);
                throw new InvalidOperationException(
                    $"{target.PresetName} 输出尺寸不匹配，期望 {target.Width}x{target.Height}，实际 {actualWidth}x{actualHeight}。");
            }

            string outputDirectory = GetProjectPath(OutputDirectory);
            Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(outputDirectory, target.FileName + ".png");
            File.Copy(pendingTempPath, outputPath, true);
            DeleteIfExists(pendingTempPath);
            Records.Add(new CaptureRecord(target, actualWidth, actualHeight, outputPath));
            Debug.Log($"[StageHubCapture] PASS {target.PresetName} -> {actualWidth}x{actualHeight} -> {outputPath}");

            targetIndex++;
            waitFrames = 0;
            captureRequested = false;
            finalTextRefreshPending = false;
            textRefreshPassesRemaining = 0;
            pendingTempPath = string.Empty;
        }

        private static void CompleteCapture()
        {
            WriteManifest();
            Debug.Log("[StageHubCapture] COMPLETED：四张固定分辨率截图均通过实际 PNG 尺寸校验。");
            BeginRestore();
        }

        private static void BeginRestore()
        {
            DeleteIfExists(pendingTempPath);
            restorePending = true;
            captureRequested = false;
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }

        private static void RestoreEditorStateWhenReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            {
                return;
            }

            try
            {
                if (previousSizeIndex >= 0)
                {
                    SetSelectedSizeIndex(previousSizeIndex);
                }

                if (!string.IsNullOrEmpty(previousScenePath)
                    && previousScenePath != ScenePath
                    && File.Exists(GetProjectPath(previousScenePath)))
                {
                    EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
                }
            }
            finally
            {
                string finalFailure = failureMessage;
                ResetState();
                if (!string.IsNullOrEmpty(finalFailure))
                {
                    Debug.LogError("[StageHubCapture] FAILED\n" + finalFailure);
                }
            }
        }

        private static void PrepareGameView()
        {
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                throw new InvalidOperationException("当前 Unity 版本未找到 UnityEditor.GameView。");
            }

            gameView = EditorWindow.GetWindow(gameViewType);
            selectedSizeIndexProperty = gameViewType.GetProperty(
                "selectedSizeIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (selectedSizeIndexProperty == null)
            {
                throw new InvalidOperationException("当前 Unity 版本未暴露 Game View selectedSizeIndex。");
            }
        }

        private static void OverridePlayModeSettings()
        {
            previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions | EnterPlayModeOptions.DisableDomainReload;
            playModeSettingsOverridden = true;
        }

        private static void RestorePlayModeSettings()
        {
            if (!playModeSettingsOverridden)
            {
                return;
            }

            EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
            playModeSettingsOverridden = false;
        }

        private static void EnsureAllFixedResolutionPresets()
        {
            for (int i = 0; i < Targets.Length; i++)
            {
                CaptureTarget target = Targets[i];
                if (FindFixedResolutionIndex(target.Width, target.Height) < 0)
                {
                    AddFixedResolution(target);
                }
            }
        }

        private static int FindFixedResolutionIndex(int width, int height)
        {
            object group = GetStandaloneSizeGroup();
            Type groupType = group.GetType();
            int builtinCount = (int)groupType.GetMethod("GetBuiltinCount").Invoke(group, null);
            int customCount = (int)groupType.GetMethod("GetCustomCount").Invoke(group, null);
            MethodInfo getGameViewSize = groupType.GetMethod("GetGameViewSize");
            for (int i = 0; i < builtinCount + customCount; i++)
            {
                object size = getGameViewSize.Invoke(group, new object[] { i });
                Type sizeType = size.GetType();
                int candidateWidth = Convert.ToInt32(sizeType.GetProperty("width").GetValue(size));
                int candidateHeight = Convert.ToInt32(sizeType.GetProperty("height").GetValue(size));
                if (candidateWidth == width && candidateHeight == height)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void AddFixedResolution(CaptureTarget target)
        {
            object group = GetStandaloneSizeGroup();
            Type gameViewSizeType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSize");
            ConstructorInfo constructor = gameViewSizeType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)[0];
            ParameterInfo[] parameters = constructor.GetParameters();
            object fixedResolution = Enum.ToObject(parameters[0].ParameterType, 1);
            object size = constructor.Invoke(new object[] { fixedResolution, target.Width, target.Height, "StageHub " + target.PresetName });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        }

        private static object GetStandaloneSizeGroup()
        {
            Assembly editorAssembly = typeof(EditorWindow).Assembly;
            Type sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
            Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singletonType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            MethodInfo getGroup = sizesType.GetMethod("GetGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Type groupEnumType = getGroup.GetParameters()[0].ParameterType;
            return getGroup.Invoke(sizes, new[] { Enum.ToObject(groupEnumType, 0) });
        }

        private static int GetSelectedSizeIndex()
        {
            return Convert.ToInt32(selectedSizeIndexProperty.GetValue(gameView));
        }

        private static void SetSelectedSizeIndex(int index)
        {
            selectedSizeIndexProperty.SetValue(gameView, index);
        }

        private static void ReadPngDimensions(string path, out int width, out int height)
        {
            byte[] header = new byte[24];
            using (FileStream stream = File.OpenRead(path))
            {
                if (stream.Read(header, 0, header.Length) != header.Length
                    || header[0] != 137 || header[1] != 80 || header[2] != 78 || header[3] != 71)
                {
                    throw new InvalidDataException("截图不是有效的 PNG 文件：" + path);
                }
            }

            width = ReadBigEndianInt32(header, 16);
            height = ReadBigEndianInt32(header, 20);
        }

        private static int ReadBigEndianInt32(byte[] bytes, int offset)
        {
            return bytes[offset] << 24
                | bytes[offset + 1] << 16
                | bytes[offset + 2] << 8
                | bytes[offset + 3];
        }

        private static void WriteManifest()
        {
            StringBuilder manifest = new StringBuilder();
            manifest.AppendLine("# Stage Hub UI 固定分辨率验收基线");
            manifest.AppendLine();
            manifest.AppendLine("- Unity 版本：`" + Application.unityVersion + "`");
            manifest.AppendLine("- 场景：`" + ScenePath + "`");
            manifest.AppendLine("- 截图时间：`" + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz") + "`");
            manifest.AppendLine("- 工程版本（Prefab 依赖哈希）：`" + AssetDatabase.GetAssetDependencyHash(PrefabPath) + "`");
            manifest.AppendLine("- 截图校验：通过（实际 PNG 宽高与目标值完全一致）");
            manifest.AppendLine("- EditMode：见本次 Unity Test Runner 验收结果");
            manifest.AppendLine("- PlayMode：见本次 Unity Test Runner 验收结果");
            manifest.AppendLine();
            manifest.AppendLine("| 文件 | Game View 预设 | 目标尺寸 | 实际尺寸 | 结果 |");
            manifest.AppendLine("| --- | --- | --- | --- | --- |");
            for (int i = 0; i < Records.Count; i++)
            {
                CaptureRecord record = Records[i];
                manifest.AppendLine($"| `{Path.GetFileName(record.OutputPath)}` | `{record.Target.PresetName}` | `{record.Target.Width}×{record.Target.Height}` | `{record.ActualWidth}×{record.ActualHeight}` | PASS |");
            }

            string directory = GetProjectPath(OutputDirectory);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "manifest.md"), manifest.ToString(), new UTF8Encoding(false));
        }

        private static string GetProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }

        private static void DeleteIfExists(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void ResetState()
        {
            EditorApplication.update -= Tick;
            RestorePlayModeSettings();
            active = false;
            restorePending = false;
            captureRequested = false;
            pendingTempPath = string.Empty;
            previousScenePath = string.Empty;
            previousSizeIndex = -1;
            gameView = null;
            selectedSizeIndexProperty = null;
        }

        private sealed class CaptureTarget
        {
            public CaptureTarget(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public int Width { get; }
            public int Height { get; }
            public string PresetName => Width + "x" + Height;
            public string FileName => "stagehub_" + PresetName;
        }

        private sealed class CaptureRecord
        {
            public CaptureRecord(CaptureTarget target, int actualWidth, int actualHeight, string outputPath)
            {
                Target = target;
                ActualWidth = actualWidth;
                ActualHeight = actualHeight;
                OutputPath = outputPath;
            }

            public CaptureTarget Target { get; }
            public int ActualWidth { get; }
            public int ActualHeight { get; }
            public string OutputPath { get; }
        }
    }
}
