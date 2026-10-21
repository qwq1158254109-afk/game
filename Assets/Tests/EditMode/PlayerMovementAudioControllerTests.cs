using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    /// <summary>
    /// 脚步音改走全局 SfxPlayer 池之后，玩家身上不再挂循环 AudioSource。
    /// 这里守两件事：① 不再自建 AudioSource（避免旧行为回归）② 没有素材时播放不抛异常。
    /// </summary>
    public class PlayerMovementAudioControllerTests
    {
        private GameObject playerObject;

        [SetUp]
        public void SetUp()
        {
            playerObject = new GameObject("PlayerMovementAudioControllerTests");
            playerObject.AddComponent<RpgPlayerController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(playerObject);
        }

        [Test]
        public void DoesNotCreateItsOwnAudioSource()
        {
            playerObject.AddComponent<PlayerMovementAudioController>();

            AudioSource source = playerObject.GetComponent<AudioSource>();
            Assert.IsNull(source, "脚步音应由 SfxPlayer 池统一播放，玩家身上不应再挂 AudioSource");
        }

        [Test]
        public void StepPlaybackDoesNotThrowWhenClipsAreMissing()
        {
            Assert.DoesNotThrow(() => Sfx.World.Step(false));
            Assert.DoesNotThrow(() => Sfx.World.Step(true));
        }
    }
}
