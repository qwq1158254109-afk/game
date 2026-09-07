using System;
using UnityEngine;

namespace StudentGrowth
{
    [Serializable]
    public sealed class AudioSettingsModel
    {
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        public float MasterVolume => masterVolume;
        public float MusicVolume => musicVolume;
        public float SfxVolume => sfxVolume;

        public float GetVolume(GameAudioChannel channel)
        {
            switch (channel)
            {
                case GameAudioChannel.Master:
                    return masterVolume;
                case GameAudioChannel.Music:
                    return musicVolume;
                default:
                    return sfxVolume;
            }
        }

        public bool SetVolume(GameAudioChannel channel, float value)
        {
            if (!Enum.IsDefined(typeof(GameAudioChannel), channel) || float.IsNaN(value) || float.IsInfinity(value)) return false;
            float clamped = Mathf.Clamp01(value);
            if (channel == GameAudioChannel.Master)
            {
                if (Mathf.Approximately(masterVolume, clamped))
                {
                    return false;
                }

                masterVolume = clamped;
                return true;
            }

            if (channel == GameAudioChannel.Music)
            {
                if (Mathf.Approximately(musicVolume, clamped))
                {
                    return false;
                }

                musicVolume = clamped;
                return true;
            }

            if (Mathf.Approximately(sfxVolume, clamped))
            {
                return false;
            }

            sfxVolume = clamped;
            return true;
        }
    }
}
