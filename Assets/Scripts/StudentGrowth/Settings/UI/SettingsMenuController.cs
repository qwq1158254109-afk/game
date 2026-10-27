using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace StudentGrowth
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class SettingsMenuController : MonoBehaviour
    {
        private const string RootName = "SettingsMenuController";

        private static SettingsMenuController instance;

        [SerializeField] private AudioSettingsController audioSettingsController;
        [SerializeField] private SettingsPanelView panelView;

        private bool legacyInputBlocked;
        private bool isOpen;
        private float previousTimeScale = 1f;
        private GameObject fallbackEventSystem;
        private GameObject previouslySelectedObject;
        private SettingsService settingsService;
        private bool closePending;

        public static SettingsMenuController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<SettingsMenuController>();
                }

                if (instance == null)
                {
                    GameObject root = new GameObject(RootName);
                    instance = root.AddComponent<SettingsMenuController>();
                }

                return instance;
            }
        }

        public bool IsOpen => isOpen;
        public event Action Closed;
        public void EnsureMenuInput() => EnsureEventSystem();
        public void ReleaseMenuInput() => DestroyFallbackEventSystem();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureExistsOnPlay()
        {
            _ = Instance;
        }

        /// <summary>
        /// 场景加载后自愈：清掉跨场景残留的 fallback EventSystem。
        ///
        /// 为什么需要这道防线：<see cref="EnsureEventSystem"/> 在早期版本里把 fallback 挂在
        /// DDOL 根之下，一旦创建就跨场景永久存活；切换到自带 EventSystem 的场景后两者并存，
        /// Unity 每帧警告 "There are 2 event systems in the scene"。修改创建逻辑只能防止
        /// 新增残留，无法清理已在运行的老实例，因此在此按场景兜底。
        ///
        /// 只在「场景里已存在另一个 EventSystem」时才动手，绝不误删唯一的那个。
        /// </summary>
        private static void CleanupStrayFallbackEventSystem()
        {
            // 只看「激活的」EventSystem：新输入系统下未激活的模块等于不存在，
            // 不能因为场景里躺着一个没启用的旧 EventSystem 就把兜底的删掉。
            EventSystem[] all = FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude);
            if (all == null || all.Length <= 1)
            {
                return;
            }

            // 先确认「至少存在一个非 fallback 命名的 EventSystem」，再动手删 fallback。
            // 否则若场景唯一的 EventSystem 恰好就是 fallback，会被误删 —— 那是把仅有的输入
            // 模块干掉，比留着重复更糟。
            bool hasRealEventSystem = false;
            for (int i = 0; i < all.Length; i++)
            {
                EventSystem candidate = all[i];
                if (candidate != null
                    && candidate.gameObject != null
                    && candidate.gameObject.name != "SettingsFallbackEventSystem")
                {
                    hasRealEventSystem = true;
                    break;
                }
            }

            if (!hasRealEventSystem)
            {
                return;
            }

            for (int i = 0; i < all.Length; i++)
            {
                EventSystem candidate = all[i];
                if (candidate == null || candidate.gameObject == null)
                {
                    continue;
                }

                if (candidate.gameObject.name == "SettingsFallbackEventSystem")
                {
                    Destroy(candidate.gameObject);
                }
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            audioSettingsController = audioSettingsController != null
                ? audioSettingsController
                : AudioSettingsController.Instance;
            panelView = panelView != null ? panelView : SettingsUiFactory.Create(transform);
            settingsService = audioSettingsController.Service;
            BindView();
            panelView.SetVolumes(
                audioSettingsController.MasterVolume,
                audioSettingsController.MusicVolume,
                audioSettingsController.SfxVolume);
            panelView.SetVisible(false);
        }

        private void Update()
        {
            if (IsKeyPressed(KeyCode.Escape, "escapeKey"))
            {
                PauseMenuRuntime.Instance.TogglePause();
            }

            if (IsKeyPressed(KeyCode.F11, "f11Key"))
            {
                ToggleFullscreen();
            }
        }

        private void OnDestroy()
        {
            settingsService?.Display?.ViewClosed();
            UnbindView();
            if (settingsService != null && settingsService.State == SettingsServiceState.Editing)
                settingsService.CancelEditing();
            if (isOpen)
            {
                Time.timeScale = previousTimeScale;
                GameplayInputGate.Release(this, true);
            }

            DestroyFallbackEventSystem();
            if (instance == this)
            {
                instance = null;
            }
        }

        public void Toggle()
        {
            if (isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (SceneTransitionManager.HasActiveTransition) return;
            if (isOpen)
            {
                return;
            }

            if (settingsService.State == SettingsServiceState.Ready) settingsService.BeginEdit();
            if (settingsService.IsBusy) return;
            closePending = false;
            EnsureEventSystem();
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            GameplayInputGate.Acquire(this);
            isOpen = true;

            panelView.SetVolumes(
                audioSettingsController.MasterVolume,
                audioSettingsController.MusicVolume,
                audioSettingsController.SfxVolume);
            panelView.SetVisible(true);
            RefreshSettingsState();

            EventSystem current = EventSystem.current;
            if (current != null)
            {
                previouslySelectedObject = current.currentSelectedGameObject;
                if (panelView.MusicSlider != null)
                {
                    current.SetSelectedGameObject(panelView.MusicSlider.gameObject);
                }
            }
        }

        public void Close()
        {
            if (!isOpen)
            {
                return;
            }

            if (settingsService.IsBusy) return;
            if (settingsService.State == SettingsServiceState.Editing && settingsService.HasPendingChanges)
            {
                closePending = true; RefreshSettingsState(); return;
            }
            if (settingsService.State == SettingsServiceState.Editing && !settingsService.CancelEditing().Success) return;
            FinishClose();
        }

        private void FinishClose()
        {
            panelView.SetVisible(false);
            audioSettingsController.Save();
            Time.timeScale = previousTimeScale;
            isOpen = false;
            GameplayInputGate.Release(this, true);

            EventSystem current = EventSystem.current;
            if (current != null && previouslySelectedObject != null && previouslySelectedObject.activeInHierarchy)
            {
                current.SetSelectedGameObject(previouslySelectedObject);
            }

            previouslySelectedObject = null;
            DestroyFallbackEventSystem();
            Closed?.Invoke();
        }

        public void ToggleFullscreen()
        {
            if (settingsService == null || settingsService.IsBusy || settingsService.State == SettingsServiceState.Degraded) return;
            if (!isOpen) PauseMenuRuntime.Instance.Coordinator.OpenSettings();
            if (!isOpen || !settingsService.Display.Capabilities.CanObserve) return;
            var intent = settingsService.Pending.DisplayIntent;
            var mode = settingsService.Display.Runtime.ResolvedDisplayMode?.WindowMode ?? intent.WindowMode;
            settingsService.SetPendingDisplay(new DisplayIntent(intent.Width, intent.Height,
                mode == SettingsWindowIntent.Windowed ? SettingsWindowIntent.Borderless : SettingsWindowIntent.Windowed,
                intent.RefreshRateNumerator, intent.RefreshRateDenominator, intent.PreferredDisplayIndex, intent.PreferredDisplayId));
            ApplySettings();
        }

        private void BindView()
        {
            if (panelView == null || audioSettingsController == null)
            {
                return;
            }

            panelView.MasterVolumeChanged += PreviewMaster;
            panelView.MusicVolumeChanged += PreviewMusic;
            panelView.SfxVolumeChanged += PreviewSfx;
            panelView.CloseRequested += Close;
            panelView.ApplyRequested += ApplySettings;
            panelView.CancelRequested += CancelSettings;
            panelView.DefaultsRequested += ResetSettings;
            panelView.RetryRequested += RetrySettings;
            panelView.SafeGraphicsChanged += EditSafeGraphics;
            settingsService.StateChanged += RefreshSettingsState;
            panelView.GetComponentInChildren<DisplaySettingsView>(true)?.Bind(settingsService);
        }

        private void UnbindView()
        {
            if (panelView != null && audioSettingsController != null)
            {
                panelView.MasterVolumeChanged -= PreviewMaster;
                panelView.MusicVolumeChanged -= PreviewMusic;
                panelView.SfxVolumeChanged -= PreviewSfx;
                panelView.CloseRequested -= Close;
                panelView.ApplyRequested -= ApplySettings;
                panelView.CancelRequested -= CancelSettings;
                panelView.DefaultsRequested -= ResetSettings;
                panelView.RetryRequested -= RetrySettings;
                panelView.SafeGraphicsChanged -= EditSafeGraphics;
                settingsService.StateChanged -= RefreshSettingsState;
            }
        }

        private void RefreshSettingsState()
        {
            if (panelView != null && panelView.IsVisible)
            {
                panelView.ShowState(settingsService, closePending);
            }
        }

        private void ApplySettings()
        {
            audioSettingsController.ApplySettingsAsync(result =>
            {
                if (this == null || panelView == null) return;
                if (result.RuntimeAccepted && closePending && !settingsService.IsPersistenceDirty &&
                    settingsService.State == SettingsServiceState.Editing &&
                    (settingsService.Display.LastResult == null || settingsService.Display.LastResult.RecoveryOutcome == DisplayRecoveryOutcome.NotRequired))
                { settingsService.CancelEditing(); FinishClose(); }
                else { closePending = false; RefreshSettingsState(); }
            });
        }

        private void CancelSettings()
        {
            if (settingsService.CancelEditing().Success) FinishClose();
        }
        private void ResetSettings() { closePending = false; settingsService.ResetPendingToDefaults(); }
        private void PreviewMaster(float value) { closePending = false; audioSettingsController.SetMasterVolume(value); }
        private void PreviewMusic(float value) { closePending = false; audioSettingsController.SetMusicVolume(value); }
        private void PreviewSfx(float value) { closePending = false; audioSettingsController.SetSfxVolume(value); }
        private void RetrySettings()
        {
            if (settingsService.State == SettingsServiceState.Degraded)
            {
                if (settingsService.Display.Phase == DisplayTransactionPhase.Degraded) audioSettingsController.RecoverDisplayAsync();
                else if (settingsService.TryRecoverRuntimeState().Success) settingsService.BeginEdit();
            }
            else settingsService.RetryPersistApplied();
        }
        private void EditSafeGraphics(bool vSync, int limit)
        { closePending = false; settingsService.SetPendingSafeGraphics(vSync, limit); }

        private bool IsKeyPressed(KeyCode legacyKey, string inputSystemKey)
        {
            if (!legacyInputBlocked)
            {
                try
                {
                    if (Input.GetKeyDown(legacyKey))
                    {
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                    legacyInputBlocked = true;
                }
            }

            Type keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            object keyboard = keyboardType?.GetProperty("current")?.GetValue(null, null);
            object key = keyboardType?.GetProperty(inputSystemKey)?.GetValue(keyboard, null);
            object value = key?.GetType().GetProperty("wasPressedThisFrame")?.GetValue(key, null);
            return value is bool pressed && pressed;
        }

        /// <summary>
        /// 订阅「每次场景加载后」的自愈。
        ///
        /// 背景（2026-10-14 实报）：原先兜底只挂在 RuntimeInitializeOnLoadMethod(AfterSceneLoad)，
        /// 那是**启动时一次性**钩子——从主菜单切到地图场景后，旧场景里的 fallback 随场景销毁，
        /// 新场景没有 EventSystem，于是「所有 UI 按钮点不动」（书桌确认按钮「点了完全没反应」）。
        /// 改为 sceneLoaded 驱动：每个场景（含启动首场景）加载后都检查/补齐。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallEventSystemSelfHealing()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedSelfHeal;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedSelfHeal;
        }

        private static void OnSceneLoadedSelfHeal(
            UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            CleanupStrayFallbackEventSystem();
            EnsureEventSystemPresent();
        }

        /// <summary>
        /// 场景加载后兜底：没有任何可用 EventSystem 的场景，补一个。
        ///
        /// 为什么需要：项目已切到新输入系统（ProjectSettings.activeInputHandler = 1），
        /// 而多数地图场景与主菜单<b>根本没有放 EventSystem</b>（只有 StageHub 自带一个）。
        /// 症状就是「所有 UI 按钮都点不动」——对话确认/取消、地点节点都失效（2026-10-14 实报）。
        /// </summary>
        private static void EnsureEventSystemPresent()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            CreateFallbackEventSystemObject();
        }

        private static GameObject CreateFallbackEventSystemObject()
        {
            GameObject fallback = new GameObject("SettingsFallbackEventSystem");
            // 不挂 DDOL：生命周期只需覆盖当前场景，换场景后由本钩子按需重建。
            fallback.transform.SetParent(null, false);
            if (Application.isPlaying)
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
                    fallback,
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }

            fallback.AddComponent<EventSystem>();
            Type inputModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null)
            {
                Component inputModule = fallback.AddComponent(inputModuleType);
                inputModuleType.GetMethod("AssignDefaultActions")?.Invoke(inputModule, null);
            }
            else
            {
                fallback.AddComponent<StandaloneInputModule>();
            }

            return fallback;
        }

        private void EnsureEventSystem()
        {
            EventSystem existing = EventSystem.current != null
                ? EventSystem.current
                : FindAnyObjectByType<EventSystem>();
            if (existing != null)
            {
                // 已经有可用的 EventSystem —— 顺手回收自己之前创建的 fallback，
                // 否则它会作为本 DDOL 根的子对象跨场景存活，与新场景自带的
                // EventSystem 并存，触发每帧 "There are 2 event systems in the scene"。
                if (fallbackEventSystem != null && fallbackEventSystem != existing.gameObject)
                {
                    DestroyFallbackEventSystemForce();
                }

                return;
            }

            fallbackEventSystem = CreateFallbackEventSystemObject();
        }

        private void DestroyFallbackEventSystem()
        {
            if (PauseMenuRuntime.OwnsPause) return;
            DestroyFallbackEventSystemForce();
        }

        /// <summary>
        /// 无条件销毁 fallback EventSystem。
        ///
        /// 与 <see cref="DestroyFallbackEventSystem"/> 的区别：后者带 <c>OwnsPause</c> 守卫，
        /// 在暂停菜单持有暂停权时不回收。但「暂停菜单管不管暂停」和「这个 fallback 该不该留」
        /// 是两件事 —— 场景里已经存在可用 EventSystem 时，这个 fallback 无论如何都是多余的，
        /// 留着只会跨场景残留、每帧触发 "There are 2 event systems in the scene"。
        /// </summary>
        private void DestroyFallbackEventSystemForce()
        {
            if (fallbackEventSystem == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(fallbackEventSystem);
            }
            else
            {
                DestroyImmediate(fallbackEventSystem);
            }

            fallbackEventSystem = null;
        }
    }
}
