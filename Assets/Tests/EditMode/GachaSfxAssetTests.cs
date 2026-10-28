using NUnit.Framework;
using UnityEngine;

namespace StudentGrowth.Tests
{
    /// <summary>
    /// 扭蛋演出音效的资产可用性（2026-10-28，T5）。
    ///
    /// 素材是 Tools/make_gacha_sfx.py 程序合成的 wav，路径写死在 Sfx.Capsule 里。
    /// 这组测试锁两件事：
    /// 1. 8 个 clip 都能从 Resources/Audio/SFX/Gacha/ 加载（路径拼错 / 文件被删会立刻红）；
    /// 2. 时长与采样率合理（合成脚本改坏时不会静默产出 0 长度或空音频）。
    /// </summary>
    public class GachaSfxAssetTests
    {
        private static readonly string[] ClipPaths =
        {
            "Gacha/gacha_coin",
            "Gacha/gacha_drop",
            "Gacha/gacha_rattle",
            "Gacha/gacha_tick",
            "Gacha/gacha_pop",
            "Gacha/gacha_flip",
            "Gacha/gacha_fanfare_rare",
            "Gacha/gacha_fanfare_uncommon"
        };

        [Test]
        public void AllGachaClipsAreLoadable()
        {
            foreach (string path in ClipPaths)
            {
                AudioClip clip = Resources.Load<AudioClip>("Audio/SFX/" + path);
                Assert.IsNotNull(clip,
                    "缺少音效 " + path + "（重跑 Tools/make_gacha_sfx.py 生成）");
                Assert.Greater(clip.length, 0.02f, path + " 时长过短，疑似空音频");
                Assert.Less(clip.length, 1.5f, path + " 时长过长，演出会拖沓");
                Assert.AreEqual(44100, clip.frequency, path + " 采样率应为 44100");
            }
        }

        [Test]
        public void RareFanfareIsLongest()
        {
            AudioClip rare = Resources.Load<AudioClip>("Audio/SFX/Gacha/gacha_fanfare_rare");
            AudioClip uncommon = Resources.Load<AudioClip>("Audio/SFX/Gacha/gacha_fanfare_uncommon");
            Assert.IsNotNull(rare);
            Assert.IsNotNull(uncommon);
            Assert.Greater(rare.length, uncommon.length, "稀有的揭晓音乐应该比非凡更长");
        }
    }
}
