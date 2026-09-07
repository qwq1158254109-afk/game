using System;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class SettingsPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Text masterValueText;
        [SerializeField] private Text musicValueText;
        [SerializeField] private Text sfxValueText;
        [SerializeField] private Button closeButton;

        private bool initialized;
        private bool suppressSliderEvents;
        private Button applyButton;
        private Button cancelButton;
        private Button defaultsButton;
        private Button retryButton;
        private Text statusText;
        private Toggle vSyncToggle;
        private Dropdown frameRateDropdown;
        private bool suppressGraphicsEvents;
        private readonly int[] frameRateValues = { 30, 60, 120, 144, 240, -1 };

        public event Action<float> MasterVolumeChanged;
        public event Action<float> MusicVolumeChanged;
        public event Action<float> SfxVolumeChanged;
        public event Action CloseRequested;
        public event Action ApplyRequested;
        public event Action CancelRequested;
        public event Action DefaultsRequested;
        public event Action RetryRequested;
        public event Action<bool, int> SafeGraphicsChanged;
        public Button ApplyButton => applyButton;
        public Button CancelButton => cancelButton;
        public Button RetryButton => retryButton;
        public string StatusText => statusText == null ? string.Empty : statusText.text;

        public Slider MasterSlider => masterSlider;
        public Slider MusicSlider => musicSlider;
        public Slider SfxSlider => sfxSlider;
        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

        public void InitializeTransactions(Button apply, Button cancel, Button defaults, Button retry, Text status,
            Toggle vSync, Dropdown frameRate)
        {
            applyButton = apply; cancelButton = cancel; defaultsButton = defaults; retryButton = retry; statusText = status;
            vSyncToggle = vSync; frameRateDropdown = frameRate;
            apply.onClick.AddListener(HandleApply); cancel.onClick.AddListener(HandleCancel);
            defaults.onClick.AddListener(HandleDefaults); retry.onClick.AddListener(HandleRetry);
            vSync.onValueChanged.AddListener(HandleVSync); frameRate.onValueChanged.AddListener(HandleFrameRate);
        }

        public void ShowState(SettingsService service, bool closePending)
        {
            var pending = service.Pending;
            SetVolumes(pending.Master, pending.Music, pending.Sfx);
            bool editing = service.State == SettingsServiceState.Editing;
            if (closeButton != null) closeButton.interactable = !service.IsBusy;
            bool degraded = service.State == SettingsServiceState.Degraded;
            if (masterSlider != null) masterSlider.interactable = editing;
            if (musicSlider != null) musicSlider.interactable = editing;
            if (sfxSlider != null) sfxSlider.interactable = editing;
            if (applyButton != null) applyButton.interactable = editing;
            if (cancelButton != null) cancelButton.interactable = editing;
            if (defaultsButton != null) defaultsButton.interactable = editing;
            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(degraded || service.IsPersistenceDirty);
                retryButton.interactable = degraded || (!service.IsBusy && !service.IsReadOnly);
                retryButton.GetComponentInChildren<Text>().text = degraded ? "尝试恢复" : "重试保存";
            }
            if (vSyncToggle != null && frameRateDropdown != null)
            {
                suppressGraphicsEvents = true;
                vSyncToggle.isOn = pending.VSync;
                int index = Array.IndexOf(frameRateValues, pending.FrameRateLimit);
                frameRateDropdown.value = index < 0 ? 1 : index;
                vSyncToggle.interactable = editing; frameRateDropdown.interactable = editing && !pending.VSync;
                suppressGraphicsEvents = false;
            }
            if (statusText != null)
                statusText.text = degraded ? "未能完整恢复设置，请尝试恢复；也可以返回游戏。" :
                    closePending ? "存在未应用修改：应用后返回，或取消修改后返回；调整滑杆可继续编辑。" :
                    service.IsReadOnly ? "配置暂时只读，本次修改仅在当前运行中生效。" :
                    service.Notice == "SETTINGS_SAVE_UNCERTAIN" ? "暂时无法确认保存结果，请重试保存。" :
                    service.IsPersistenceDirty ? "设置已应用，但尚未保存。" :
                    service.Notice == "SETTINGS_APPLY_FAILED" ? "应用失败，已恢复之前的设置；可调整后重试。" :
                    service.Notice == "SETTINGS_BACKUP_RESTORED" ? "设置文件异常，已从备份恢复。" :
                    service.Notice == "SETTINGS_DEFAULTS_RESTORED" ? "设置文件异常，已恢复默认值。" :
                    service.Notice == "SETTINGS_INTERRUPTED_COMMIT_RECOVERED" ? "已恢复上次中断的设置保存。" :
                    service.HasPendingChanges ? "正在预览；点击应用保存，取消可恢复之前的设置。" : "设置已就绪。";
        }

        private void HandleApply() => ApplyRequested?.Invoke();
        private void HandleCancel() => CancelRequested?.Invoke();
        private void HandleDefaults() => DefaultsRequested?.Invoke();
        private void HandleRetry() => RetryRequested?.Invoke();
        private void HandleVSync(bool value) { if (!suppressGraphicsEvents) SafeGraphicsChanged?.Invoke(value, frameRateValues[frameRateDropdown.value]); }
        private void HandleFrameRate(int index) { if (!suppressGraphicsEvents) SafeGraphicsChanged?.Invoke(vSyncToggle.isOn, frameRateValues[index]); }

        public void Initialize(
            GameObject root,
            CanvasGroup group,
            Slider master,
            Slider music,
            Slider sfx,
            Text masterValue,
            Text musicValue,
            Text sfxValue,
            Button close)
        {
            if (initialized)
            {
                UnwireControls();
            }

            panelRoot = root;
            canvasGroup = group;
            masterSlider = master;
            musicSlider = music;
            sfxSlider = sfx;
            masterValueText = masterValue;
            musicValueText = musicValue;
            sfxValueText = sfxValue;
            closeButton = close;
            WireControls();
            initialized = true;
            SetVisible(false);
        }

        public void SetVolumes(float masterVolume, float musicVolume, float sfxVolume)
        {
            suppressSliderEvents = true;
            if (masterSlider != null)
            {
                masterSlider.value = Mathf.Clamp01(masterVolume);
            }

            if (musicSlider != null)
            {
                musicSlider.value = Mathf.Clamp01(musicVolume);
            }

            if (sfxSlider != null)
            {
                sfxSlider.value = Mathf.Clamp01(sfxVolume);
            }

            suppressSliderEvents = false;
            RefreshValueLabels();
        }

        public void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(visible);
            }
        }

        private void OnDestroy()
        {
            UnwireControls();
            if (applyButton != null) applyButton.onClick.RemoveListener(HandleApply);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(HandleCancel);
            if (defaultsButton != null) defaultsButton.onClick.RemoveListener(HandleDefaults);
            if (retryButton != null) retryButton.onClick.RemoveListener(HandleRetry);
            if (vSyncToggle != null) vSyncToggle.onValueChanged.RemoveListener(HandleVSync);
            if (frameRateDropdown != null) frameRateDropdown.onValueChanged.RemoveListener(HandleFrameRate);
        }

        private void WireControls()
        {
            if (masterSlider != null)
            {
                masterSlider.onValueChanged.AddListener(HandleMasterSliderChanged);
            }

            if (musicSlider != null)
            {
                musicSlider.onValueChanged.AddListener(HandleMusicSliderChanged);
            }

            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.AddListener(HandleSfxSliderChanged);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleCloseClicked);
            }
        }

        private void UnwireControls()
        {
            if (masterSlider != null)
            {
                masterSlider.onValueChanged.RemoveListener(HandleMasterSliderChanged);
            }

            if (musicSlider != null)
            {
                musicSlider.onValueChanged.RemoveListener(HandleMusicSliderChanged);
            }

            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.RemoveListener(HandleSfxSliderChanged);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
            }
        }

        private void HandleMasterSliderChanged(float value)
        {
            SetPercentageText(masterValueText, "主音量：", value);
            if (!suppressSliderEvents)
            {
                MasterVolumeChanged?.Invoke(value);
            }
        }

        private void HandleMusicSliderChanged(float value)
        {
            SetPercentageText(musicValueText, "BGM 音量：", value);
            if (!suppressSliderEvents)
            {
                MusicVolumeChanged?.Invoke(value);
            }
        }

        private void HandleSfxSliderChanged(float value)
        {
            SetPercentageText(sfxValueText, "音效音量：", value);
            if (!suppressSliderEvents)
            {
                SfxVolumeChanged?.Invoke(value);
            }
        }

        private void HandleCloseClicked()
        {
            CloseRequested?.Invoke();
        }

        private void RefreshValueLabels()
        {
            SetPercentageText(masterValueText, "主音量：", masterSlider == null ? 1f : masterSlider.value);
            SetPercentageText(musicValueText, "BGM 音量：", musicSlider == null ? 1f : musicSlider.value);
            SetPercentageText(sfxValueText, "音效音量：", sfxSlider == null ? 1f : sfxSlider.value);
        }

        private static void SetPercentageText(Text target, string prefix, float value)
        {
            if (target != null)
            {
                target.text = prefix + Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
            }
        }
    }
}
