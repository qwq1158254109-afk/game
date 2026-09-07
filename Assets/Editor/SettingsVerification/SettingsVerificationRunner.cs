using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using StudentGrowth;

// Reproducible, opt-in editor verification. No test starts without menu action or workspace request file.
[InitializeOnLoad]
public static class SettingsVerificationRunner
{
    private const string RequestPath = "Temp/SettingsVerification.request";
    private const string ReportDirectory = "Docs/Settings/2026-09-05";
    private static bool running;
    private const string SmokeRequest = "Temp/SettingsPlayVerification.request";
    private const string SmokeActive = "Temp/SettingsPlayVerification.active";
    private static double smokeStart;
    static SettingsVerificationRunner()
    {
        EditorApplication.update += CheckRequest;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    private static void CheckRequest()
    {
        if (!running && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(SmokeRequest))
        {
            File.Delete(SmokeRequest);
            File.WriteAllText(SmokeActive, Guid.NewGuid().ToString("N"));
            EditorApplication.isPlaying = true;
            return;
        }
        if (running || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath)) return;
        File.Delete(RequestPath);
        Run();
    }

    private static void OnPlayMode(PlayModeStateChange change)
    {
        if (File.Exists("Temp/PauseSaveVerification.active") || File.Exists("Temp/MainMenuVerification.active")
            || File.Exists("Temp/BattleP1Play.active") || File.Exists("Temp/BattleP2Play.active") || File.Exists("Temp/BattleP3Play.active") || File.Exists("Temp/BattleP4Play.active")) return;
        if (!File.Exists(SmokeActive)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            smokeStart = EditorApplication.timeSinceStartup;
            EditorApplication.update += RunPlaySmoke;
        }
        else if (change == PlayModeStateChange.EnteredEditMode) File.Delete(SmokeActive);
    }

    private static void RunPlaySmoke()
    {
        if (EditorApplication.timeSinceStartup - smokeStart < 1) return;
        EditorApplication.update -= RunPlaySmoke;
        Directory.CreateDirectory(ReportDirectory);
        try
        {
            var audio = AudioSettingsController.Instance;
            var service = audio.Service;
            var menu = SettingsMenuController.Instance;
            float previousScale = Time.timeScale;
            float previousMaster = service.Applied.Master;
            menu.Open();
            Check(menu.IsOpen && Time.timeScale == 0, "Menu must pause gameplay");
            var view = menu.GetComponentInChildren<SettingsPanelView>(true);
            view.MasterSlider.value = 0.37f;
            Check(Math.Abs(audio.MasterVolume - 0.37f) < 0.001f, "Preview must reach audio runtime");
            Check(service.Applied.Master == previousMaster, "Preview cannot commit");
            menu.Close();
            Check(menu.IsOpen, "Unapplied close must request a decision");
            view.CancelButton.onClick.Invoke();
            Check(!menu.IsOpen && Time.timeScale == previousScale, "Cancel must unpause");
            Check(audio.MasterVolume == previousMaster, "Cancel must restore audio");
            menu.Open(); view.MasterSlider.value = 0.42f; view.ApplyButton.onClick.Invoke();
            Check(Math.Abs(service.Persisted.Master - 0.42f) < 0.001f && !service.IsPersistenceDirty, "Apply must persist");
            string id = File.ReadAllText(SmokeActive).Trim();
            var paths = new SettingsPathProvider(Path.GetFullPath("Temp/SettingsPlayTests/" + id));
            var reloaded = new SettingsRepository(paths, null).Load();
            Check(reloaded.Persisted && Math.Abs(reloaded.Snapshot.Master - 0.42f) < 0.001f, "Disk reload must match");
            menu.Close(); Check(!menu.IsOpen && Time.timeScale == previousScale, "Close must restore pause state");
            var testAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "StudentGrowth.EditMode.Tests");
            var displayFixtureType = testAssembly.GetType("StudentGrowth.Tests.DisplaySettingsUiTests", true);
            foreach (var test in displayFixtureType.GetMethods().Where(m => HasAttribute(m, "NUnit.Framework.TestAttribute")))
            {
                var fixture = Activator.CreateInstance(displayFixtureType);
                try { displayFixtureType.GetMethod("Setup").Invoke(fixture, null); test.Invoke(fixture, null); }
                finally { displayFixtureType.GetMethod("Cleanup").Invoke(fixture, null); }
            }
            File.WriteAllText(ReportDirectory + "/Unity-Play-smoke.txt", "PASS: menu entry, pause, audio preview, close decision, cancel restore, apply save, disk reload, close unpause; display UI confirmation and actual View destruction/recovery with simulated display platform.\nIsolatedRoot=" + paths.DirectoryPath + "\nUTC=" + DateTime.UtcNow.ToString("O"));
        }
        catch (Exception ex) { File.WriteAllText(ReportDirectory + "/Unity-Play-smoke.txt", "FAIL: " + ex); }
        finally { EditorApplication.isPlaying = false; }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [MenuItem("Tools/Student Growth/Verify Settings Transactions")]
    public static void Run()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        Directory.CreateDirectory(ReportDirectory);
        // These selected fixtures use ordinary Test/SetUp/TearDown only. Invoke them in the
        // actual Editor runtime without TestRunner's scene-save/restore step, preserving dirty scenes.
        var xml = new XElement("settings-editor-tests", new XAttribute("unity", Application.unityVersion));
        int passed = 0, failed = 0;
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "StudentGrowth.EditMode.Tests");
        foreach (var type in assembly.GetTypes().Where(t => t.Namespace == "StudentGrowth.Tests" &&
            (t.Name.StartsWith("PauseSave", StringComparison.Ordinal) || t.Name.StartsWith("DisplaySettings", StringComparison.Ordinal) || t.Name.StartsWith("Settings", StringComparison.Ordinal) || t.Name.StartsWith("Audio", StringComparison.Ordinal) || t.Name.StartsWith("PlayerMovementAudio", StringComparison.Ordinal))))
        {
            foreach (var test in type.GetMethods().Where(m => HasAttribute(m, "NUnit.Framework.TestAttribute")))
            {
                object fixture = null; Exception failure = null;
                try
                {
                    fixture = Activator.CreateInstance(type);
                    foreach (var setup in type.GetMethods().Where(m => HasAttribute(m, "NUnit.Framework.SetUpAttribute"))) setup.Invoke(fixture, null);
                    test.Invoke(fixture, null);
                }
                catch (Exception ex) { failure = ex.InnerException ?? ex; }
                finally
                {
                    if (fixture != null) foreach (var cleanup in type.GetMethods().Where(m => HasAttribute(m, "NUnit.Framework.TearDownAttribute")))
                        try { cleanup.Invoke(fixture, null); } catch (Exception ex) { failure = failure ?? ex.InnerException ?? ex; }
                }
                if (failure == null) passed++; else failed++;
                xml.Add(new XElement("test", new XAttribute("name", type.FullName + "." + test.Name),
                    new XAttribute("result", failure == null ? "Passed" : "Failed"), failure?.ToString()));
            }
        }
        xml.SetAttributeValue("passed", passed); xml.SetAttributeValue("failed", failed);
        xml.Save(ReportDirectory + "/Unity-Editor-NUnit-results.xml");
        File.WriteAllText(ReportDirectory + "/Unity-verification.txt", "Unity=" + Application.unityVersion +
            "\nRunner=Selected ordinary NUnit fixtures invoked in Editor (no scene save)\nPassed=" + passed + "\nFailed=" + failed + "\nUTC=" + DateTime.UtcNow.ToString("O"));
        try { CaptureMenu(); }
        catch (Exception ex) { File.WriteAllText(ReportDirectory + "/capture-error.txt", ex.ToString()); }
        finally { running = false; }
    }
    private static bool HasAttribute(MemberInfo member, string name) => member.GetCustomAttributes(true).Any(a => a.GetType().FullName == name);
    private static void CaptureMenu()
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        Texture2D pixels = null;
        Camera camera = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            var root = new GameObject("SettingsVerificationPreview");
            SceneManager.MoveGameObjectToScene(root, preview);
            var cameraObject = new GameObject("PreviewCamera"); SceneManager.MoveGameObjectToScene(cameraObject, preview);
            camera = cameraObject.AddComponent<Camera>();
            camera.scene = preview; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
            target = new RenderTexture(1920, 1080, 24); camera.targetTexture = target;
            var view = SettingsUiFactory.Create(root.transform);
            var canvas = view.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
            var example = new StudentGrowth.SettingsService(new PreviewRepository(), new ISettingsDomainApplier[0]);
            example.Initialize(); example.BeginEdit();
            new DisplayTransactionCoordinator(example, new UnityDisplayPlatform(), () => EditorApplication.timeSinceStartup);
            view.GetComponentInChildren<DisplaySettingsView>(true).Bind(example);
            view.SetVisible(true); view.ShowState(example, false);
            Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = target; pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
            File.WriteAllBytes(ReportDirectory + "/settings-menu.png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (camera != null) camera.targetTexture = null;
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
    private sealed class PreviewRepository : ISettingsRepository
    {
        public SettingsLoadResult Load() => new SettingsLoadResult(new SettingsSnapshot(0.8f, 0.4f, 0.6f), true);
        public SettingsSaveResult Save(SettingsSnapshot settings) => new SettingsSaveResult(SettingsPersistenceOutcome.Committed);
    }
}
