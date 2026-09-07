using UnityEngine;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RpgPlayerController))]
    public sealed class PlayerMovementAudioController : MonoBehaviour
    {
        [SerializeField] private RpgPlayerController playerController;
        [SerializeField] private AudioSource runAudioSource;
        [SerializeField] private AudioChannelSource audioChannelSource;
        [SerializeField] private AudioClip runClip;
        [SerializeField, Range(0f, 1f)] private float runVolume = 0.275f;

        private void Awake()
        {
            EnsureComponents();
            ApplyAudioSettings();
        }

        private void Update()
        {
            bool shouldPlay = playerController != null
                && playerController.IsRunning
                && runClip != null;

            if (shouldPlay)
            {
                if (!runAudioSource.isPlaying)
                {
                    runAudioSource.Play();
                }

                return;
            }

            StopRunAudio();
        }

        private void OnDisable()
        {
            StopRunAudio();
        }

        public void Configure(AudioClip clip, float volume = 0.275f)
        {
            runClip = clip;
            runVolume = Mathf.Clamp01(volume);
            EnsureComponents();
            ApplyAudioSettings();
        }

        private void EnsureComponents()
        {
            if (playerController == null)
            {
                playerController = GetComponent<RpgPlayerController>();
            }

            if (runAudioSource == null)
            {
                runAudioSource = gameObject.AddComponent<AudioSource>();
            }

            if (audioChannelSource == null)
            {
                audioChannelSource = GetComponent<AudioChannelSource>();
            }

            if (audioChannelSource == null)
            {
                audioChannelSource = gameObject.AddComponent<AudioChannelSource>();
            }
        }

        private void ApplyAudioSettings()
        {
            if (runAudioSource == null)
            {
                return;
            }

            runAudioSource.clip = runClip;
            runAudioSource.playOnAwake = false;
            runAudioSource.loop = true;
            runAudioSource.spatialBlend = 0f;
            audioChannelSource.Configure(runAudioSource, GameAudioChannel.Sfx, runVolume);
        }

        private void StopRunAudio()
        {
            if (runAudioSource != null && runAudioSource.isPlaying)
            {
                runAudioSource.Stop();
            }
        }
    }
}
