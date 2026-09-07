using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class PlayerMovementAudioControllerTests
    {
        private GameObject playerObject;
        private AudioClip runClip;

        [SetUp]
        public void SetUp()
        {
            playerObject = new GameObject("PlayerMovementAudioControllerTests");
            playerObject.AddComponent<RpgPlayerController>();
            runClip = AudioClip.Create("RunTestClip", 1024, 1, 22050, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(runClip);
            Object.DestroyImmediate(playerObject);
        }

        [Test]
        public void ConfigureCreatesLoopingTwoDimensionalRunSource()
        {
            PlayerMovementAudioController audioController =
                playerObject.AddComponent<PlayerMovementAudioController>();

            audioController.Configure(runClip, 0.4f);

            AudioSource source = playerObject.GetComponent<AudioSource>();
            Assert.IsNotNull(source);
            Assert.AreSame(runClip, source.clip);
            Assert.IsTrue(source.loop);
            Assert.IsFalse(source.playOnAwake);
            Assert.AreEqual(0f, source.spatialBlend);
            Assert.AreEqual(0.4f, source.volume);

            AudioChannelSource channelSource = playerObject.GetComponent<AudioChannelSource>();
            Assert.IsNotNull(channelSource);
            Assert.AreEqual(GameAudioChannel.Sfx, channelSource.Channel);
            Assert.AreEqual(0.4f, channelSource.BaseVolume);
        }
    }
}
