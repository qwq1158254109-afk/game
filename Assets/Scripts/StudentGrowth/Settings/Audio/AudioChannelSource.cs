using UnityEngine;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioChannelSource : MonoBehaviour
    {
        [SerializeField] private AudioSource targetSource;
        [SerializeField] private GameAudioChannel channel = GameAudioChannel.Sfx;
        [SerializeField, Range(0f, 1f)] private float baseVolume = 1f;

        private IAudioSettingsProvider settingsProvider;
        private bool subscribed;

        public AudioSource TargetSource => targetSource;
        public GameAudioChannel Channel => channel;
        public float BaseVolume => baseVolume;

        private void Awake()
        {
            ResolveSource();
            ResolveDefaultProvider();
            Subscribe();
            ApplyVolume();
        }

        private void OnEnable()
        {
            ResolveSource();
            ResolveDefaultProvider();
            Subscribe();
            ApplyVolume();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(
            AudioSource source,
            GameAudioChannel audioChannel,
            float sourceBaseVolume,
            IAudioSettingsProvider provider = null)
        {
            Unsubscribe();
            targetSource = source != null ? source : GetComponent<AudioSource>();
            channel = audioChannel;
            baseVolume = Mathf.Clamp01(sourceBaseVolume);
            settingsProvider = provider;
            ResolveDefaultProvider();
            Subscribe();
            ApplyVolume();
        }

        public void ApplyVolume()
        {
            ResolveSource();
            if (targetSource == null)
            {
                return;
            }

            float channelVolume = settingsProvider == null ? 1f : settingsProvider.GetVolume(channel);
            targetSource.volume = baseVolume * Mathf.Clamp01(channelVolume);
        }

        private void HandleVolumeChanged(GameAudioChannel changedChannel, float value)
        {
            if (changedChannel == channel || changedChannel == GameAudioChannel.Master)
            {
                ApplyVolume();
            }
        }

        private void ResolveSource()
        {
            if (targetSource == null)
            {
                targetSource = GetComponent<AudioSource>();
            }
        }

        private void ResolveDefaultProvider()
        {
            if (settingsProvider == null && Application.isPlaying)
            {
                settingsProvider = AudioSettingsController.Instance;
            }
        }

        private void Subscribe()
        {
            if (subscribed || settingsProvider == null || !isActiveAndEnabled)
            {
                return;
            }

            settingsProvider.VolumeChanged += HandleVolumeChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || settingsProvider == null)
            {
                return;
            }

            settingsProvider.VolumeChanged -= HandleVolumeChanged;
            subscribed = false;
        }
    }
}
