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

        private void EnsureEventSystem()
        {
            EventSystem existing = EventSystem.current != null
                ? EventSystem.current
                : FindAnyObjectByType<EventSystem>();
            if (existing != null)
            {
                return;
            }

            fallbackEventSystem = new GameObject("SettingsFallbackEventSystem");
            fallbackEventSystem.transform.SetParent(transform, false);
            fallbackEventSystem.AddComponent<EventSystem>();
            Type inputModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null)
            {
                Component inputModule = fallbackEventSystem.AddComponent(inputModuleType);
                inputModuleType.GetMethod("AssignDefaultActions")?.Invoke(inputModule, null);
            }
            else fallbackEventSystem.AddComponent<StandaloneInputModule>();
        }

        private void DestroyFallbackEventSystem()
        {
            if (PauseMenuRuntime.OwnsPause) return;
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
