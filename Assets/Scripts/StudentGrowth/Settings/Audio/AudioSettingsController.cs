using System;
using System.IO;
using UnityEngine;

namespace StudentGrowth
{
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class AudioSettingsController : MonoBehaviour, IAudioSettingsProvider
    {
        private const string RootName = "AudioSettingsController";
        private const string MasterVolumeKey = "settings.audio.masterVolume";
        private const string MusicVolumeKey = "settings.audio.musicVolume";
        private const string SfxVolumeKey = "settings.audio.sfxVolume";

        private static AudioSettingsController instance;

        [SerializeField] private AudioSettingsModel model = new AudioSettingsModel();
        private SettingsService service;
        public static bool DisplayTransitionBlocked => instance != null && instance.service?.Display != null && instance.service.Display.IsBusy;
        public SettingsService Service { get { EnsureService(); return service; } }

        public static AudioSettingsController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<AudioSettingsController>();
                }

                if (instance == null)
                {
                    GameObject root = new GameObject(RootName);
                    instance = root.AddComponent<AudioSettingsController>();
                }

                return instance;
            }
        }

        public event Action<GameAudioChannel, float> VolumeChanged;

        public float MasterVolume => model.MasterVolume;
        public float MusicVolume => model.MusicVolume;
        public float SfxVolume => model.SfxVolume;

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
            if (model == null)
            {
                model = new AudioSettingsModel();
            }

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
                EnsureService();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            service?.Display?.SetPaused(paused);
            if (paused)
            {
                Save();
            }
        }

        private void OnApplicationQuit()
        {
            service?.Display?.Quit();
            Save();
        }

        private void OnApplicationFocus(bool focused)
        {
            service?.Display?.SetFocus(focused);
            if (!focused) Save();
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
            service?.Display?.Quit();
            if (instance == this)
            {
                instance = null;
            }
        }

        public float GetVolume(GameAudioChannel channel)
        {
            if (channel == GameAudioChannel.Master)
            {
                return model.MasterVolume;
            }

            return model.MasterVolume * model.GetVolume(channel);
        }

        public void SetMasterVolume(float value)
        {
            SetVolume(GameAudioChannel.Master, value);
        }

        public void SetMusicVolume(float value)
        {
            SetVolume(GameAudioChannel.Music, value);
        }

        public void SetSfxVolume(float value)
        {
            SetVolume(GameAudioChannel.Sfx, value);
        }

        public void SetVolume(GameAudioChannel channel, float value)
        {
            if (!Enum.IsDefined(typeof(GameAudioChannel), channel) || float.IsNaN(value) || float.IsInfinity(value)) return;
            var settings = Service;
            bool ownSession = settings.State == SettingsServiceState.Ready;
            if (ownSession && !settings.BeginEdit().Success) return;
            var pending = settings.Pending;
            var result = settings.PreviewAudio(channel == GameAudioChannel.Master ? value : pending.Master,
                channel == GameAudioChannel.Music ? value : pending.Music,
                channel == GameAudioChannel.Sfx ? value : pending.Sfx);
            if (ownSession)
            {
                if (result.Success) settings.ApplyPending();
                if (settings.State == SettingsServiceState.Editing) settings.CancelEditing();
            }
        }

        public void Save()
        {
            if (service != null && service.IsPersistenceDirty) service.RetryPersistApplied();
        }

        private void EnsureService()
        {
            if (service != null) return;
            var defaults = new SettingsSnapshot(vSync: QualitySettings.vSyncCount != 0,
                frameRateLimit: Application.targetFrameRate);
            string directory = Path.Combine(Application.persistentDataPath, "Settings");
            Func<SettingsSnapshot> importer = () => ImportLegacy(defaults);
#if UNITY_EDITOR
            // Batch Test Runner must never read/write the developer's real preferences.
            string smokeMarker = Path.Combine(Application.dataPath, "../Temp/SettingsPlayVerification.active");
            if (File.Exists(smokeMarker) && Guid.TryParseExact(File.ReadAllText(smokeMarker).Trim(), "N", out var smokeId))
            {
                directory = Path.Combine(Application.dataPath, "../Temp/SettingsPlayTests", smokeId.ToString("N"));
                importer = () => null;
            }
            else if (Application.isBatchMode)
            {
                directory = Path.Combine(Application.dataPath, "../Temp/SettingsBatch", Guid.NewGuid().ToString("N"));
                importer = () => null;
            }
#endif
            var repository = new SettingsRepository(new SettingsPathProvider(directory), importer, defaults);
            service = new SettingsService(repository,
                new ISettingsDomainApplier[] { new AudioApplier(this), new SafeGraphicsApplier() }, defaults);
            service.Initialize();
            var display = new DisplayTransactionCoordinator(service, new UnityDisplayPlatform(), () => Time.realtimeSinceStartupAsDouble, recoveryRepository: new DisplayRecoveryRepository(directory));
            display.SetFocus(Application.isFocused);
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
            if (Application.isPlaying && display.Capabilities.CanObserve) StartCoroutine(display.ApplyAsync(service.Applied.DisplayIntent, true));
        }

        private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene) => service?.Display?.SceneUnloaded();

        public void ApplySettingsAsync(Action<SettingsApplySummary> completed) => StartCoroutine(Service.ApplyPendingAsync(completed));
        public void RecoverDisplayAsync() => StartCoroutine(Service.Display.RetryRecoveryAsync());

        private static SettingsSnapshot ImportLegacy(SettingsSnapshot defaults)
        {
            if (!PlayerPrefs.HasKey(MasterVolumeKey) && !PlayerPrefs.HasKey(MusicVolumeKey) && !PlayerPrefs.HasKey(SfxVolumeKey)) return null;
            return defaults.WithAudio(ReadLegacy(MasterVolumeKey), ReadLegacy(MusicVolumeKey), ReadLegacy(SfxVolumeKey));
        }

        private static float ReadLegacy(string key)
        {
            float value = PlayerPrefs.GetFloat(key, 1f);
            return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);
        }

        private void ApplyRuntime(SettingsSnapshot snapshot)
        {
            // Update every channel before notifying listeners so they observe one consistent snapshot.
            bool master = model.SetVolume(GameAudioChannel.Master, snapshot.Master);
            bool music = model.SetVolume(GameAudioChannel.Music, snapshot.Music);
            bool sfx = model.SetVolume(GameAudioChannel.Sfx, snapshot.Sfx);
            if (master) VolumeChanged?.Invoke(GameAudioChannel.Master, snapshot.Master);
            if (music) VolumeChanged?.Invoke(GameAudioChannel.Music, snapshot.Music);
            if (sfx) VolumeChanged?.Invoke(GameAudioChannel.Sfx, snapshot.Sfx);
        }

        private sealed class AudioApplier : ISettingsDomainApplier
        {
            private readonly AudioSettingsController owner;
            public AudioApplier(AudioSettingsController owner) { this.owner = owner; }
            public SettingsDomain Domain => SettingsDomain.Audio;
            public bool Apply(SettingsSnapshot settings) { owner.ApplyRuntime(settings); return true; }
            public bool Restore(SettingsSnapshot settings) => Apply(settings);
            public SettingsSnapshot Observe() => new SettingsSnapshot(owner.MasterVolume, owner.MusicVolume, owner.SfxVolume);
        }

        private sealed class SafeGraphicsApplier : ISettingsDomainApplier
        {
            public SettingsDomain Domain => SettingsDomain.SafeGraphics;
            public bool Apply(SettingsSnapshot settings)
            {
                QualitySettings.vSyncCount = settings.VSync ? 1 : 0;
                Application.targetFrameRate = settings.FrameRateLimit;
                return true;
            }
            public bool Restore(SettingsSnapshot settings) => Apply(settings);
            public SettingsSnapshot Observe() => new SettingsSnapshot(vSync: QualitySettings.vSyncCount != 0,
                frameRateLimit: Application.targetFrameRate);
        }
    }
}
