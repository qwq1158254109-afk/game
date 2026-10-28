using System.Collections;
using System.Reflection;
using NUnit.Framework;
using StudentGrowth;
using UnityEditor;
using UnityEngine;

namespace StudentGrowth.Tests
{
    /// <summary>
    /// 送礼界面的图标缓存（2026-10-28，T2）。
    ///
    /// 锁的契约：`GiftPickerView.LoadIconSprite` 的静态缓存必须**验活**。
    /// 缓存命中直接返回的话，域重载 / 场景切换 / UnloadUnusedAssets 之后
    /// 缓存里留下的是「已销毁的 Sprite」（Unity 伪 null），字典照样命中，
    /// 图标从此永久消失 —— 用户报的「有时候无法加载」就是这条路径。
    ///
    /// 三条：
    /// 1. 正常路径能读到非空 Sprite；
    /// 2. 缓存被塞进已销毁 Sprite 后，再读能恢复成新的非空 Sprite；
    /// 3. 读不存在的资产返回 null，且**不写缓存**（一次临时失败不能变成永久缺图）。
    /// </summary>
    public class GiftPickerIconCacheTests
    {
        private const BindingFlags StaticPrivate = BindingFlags.NonPublic | BindingFlags.Static;

        private static MethodInfo loadMethod;
        private static FieldInfo cacheField;

        [SetUp]
        public void Setup()
        {
            loadMethod = typeof(GiftPickerView).GetMethod("LoadIconSprite", StaticPrivate);
            cacheField = typeof(GiftPickerView).GetField("iconCache", StaticPrivate);
            Assert.IsNotNull(loadMethod, "GiftPickerView.LoadIconSprite 找不到（签名变了？）");
            Assert.IsNotNull(cacheField, "GiftPickerView.iconCache 找不到（字段改名了？）");
            Cache().Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Cache().Clear();
        }

        [Test]
        public void LoadIconSprite_ReadsExistingAsset()
        {
            string path = FindExistingPng();
            if (path == null)
            {
                Assert.Ignore("Assets/Art 下没有可用的 PNG，跳过。");
            }

            Sprite sprite = Load(path);

            Assert.IsNotNull(sprite, "存在的 PNG 应该读出 Sprite：" + path);
            Assert.IsTrue(Cache().Contains(path), "成功读到之后应写入缓存");
        }

        [Test]
        public void LoadIconSprite_RecoversAfterCachedSpriteDestroyed()
        {
            string path = FindExistingPng();
            if (path == null)
            {
                Assert.Ignore("Assets/Art 下没有可用的 PNG，跳过。");
            }

            Sprite first = Load(path);
            Assert.IsNotNull(first, "首次读取应成功");

            // 造一个「已销毁的 Sprite」塞进缓存，模拟域重载 / UnloadUnusedAssets 之后的伪 null。
            Texture2D doomedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Sprite doomed = Sprite.Create(doomedTexture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f, 100f);
            Object.DestroyImmediate(doomed);
            Object.DestroyImmediate(doomedTexture);

            Cache()[path] = doomed;

            Sprite recovered = Load(path);

            Assert.IsNotNull(recovered, "缓存里是已销毁 Sprite 时应失效重读，而不是返回伪 null");
            Assert.IsFalse(ReferenceEquals(recovered, doomed), "恢复出来的不能是那个已销毁对象");
        }

        [Test]
        public void LoadIconSprite_DoesNotCacheMissingAsset()
        {
            string missing = "Assets/Art/__not_exist__/gift_picker_probe.png";

            Sprite sprite = Load(missing);

            Assert.IsNull(sprite, "不存在的路径应返回 null");
            Assert.IsFalse(Cache().Contains(missing), "读失败不能写缓存，否则一次临时失败会永久缺图");
        }

        // ── 反射小工具 ──────────────────────────────────────────────

        private static Sprite Load(string path)
        {
            return (Sprite)loadMethod.Invoke(null, new object[] { path });
        }

        private static IDictionary Cache()
        {
            return (IDictionary)cacheField.GetValue(null);
        }

        /// <summary>从 Assets/Art 里挑一张真实存在的 PNG，避免测试写死某个可能被挪走的文件名。</summary>
        private static string FindExistingPng()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".png"))
                {
                    return path;
                }
            }

            return null;
        }
    }
}
