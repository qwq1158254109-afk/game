using System;
using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class AudioChannelSourceTests
    {
        private GameObject audioObject;
        private AudioSource audioSource;
        private AudioChannelSource channelSource;
        private TestAudioSettingsProvider settingsProvider;

        [SetUp]
        public void SetUp()
        {
            audioObject = new GameObject("AudioChannelSourceTests");
            audioSource = audioObject.AddComponent<AudioSource>();
            channelSource = audioObject.AddComponent<AudioChannelSource>();
            settingsProvider = new TestAudioSettingsProvider();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(audioObject);
        }

        [Test]
        public void SfxChannelScalesSourceBaseVolume()
        {
            settingsProvider.SetVolume(GameAudioChannel.Sfx, 0.4f);

            channelSource.Configure(audioSource, GameAudioChannel.Sfx, 0.5f, settingsProvider);

            Assert.AreEqual(0.2f, audioSource.volume, 0.0001f);
        }

        [Test]
        public void ChannelOnlyRespondsToMatchingVolumeChanges()
        {
            settingsProvider.SetVolume(GameAudioChannel.Sfx, 0.5f);
            channelSource.Configure(audioSource, GameAudioChannel.Sfx, 0.6f, settingsProvider);

            settingsProvider.SetVolume(GameAudioChannel.Music, 0.1f);
            Assert.AreEqual(0.3f, audioSource.volume, 0.0001f);

            settingsProvider.SetVolume(GameAudioChannel.Sfx, 0.25f);
            Assert.AreEqual(0.15f, audioSource.volume, 0.0001f);
        }

        [Test]
        public void MasterVolumeScalesTheSelectedChannel()
        {
            settingsProvider.SetVolume(GameAudioChannel.Sfx, 0.5f);
            channelSource.Configure(audioSource, GameAudioChannel.Sfx, 0.6f, settingsProvider);

            settingsProvider.SetVolume(GameAudioChannel.Master, 0.4f);

            Assert.AreEqual(0.12f, audioSource.volume, 0.0001f);
        }

        private sealed class TestAudioSettingsProvider : IAudioSettingsProvider
        {
            private float masterVolume = 1f;
            private float musicVolume = 1f;
            private float sfxVolume = 1f;

            public event Action<GameAudioChannel, float> VolumeChanged;

            public float GetVolume(GameAudioChannel channel)
            {
                if (channel == GameAudioChannel.Master)
                {
                    return masterVolume;
                }

                float channelVolume = channel == GameAudioChannel.Music ? musicVolume : sfxVolume;
                return masterVolume * channelVolume;
            }

            public void SetVolume(GameAudioChannel channel, float value)
            {
                if (channel == GameAudioChannel.Master)
                {
                    masterVolume = value;
                }
                else if (channel == GameAudioChannel.Music)
                {
                    musicVolume = value;
                }
                else
                {
                    sfxVolume = value;
                }

                VolumeChanged?.Invoke(channel, value);
            }
        }
    }
}
