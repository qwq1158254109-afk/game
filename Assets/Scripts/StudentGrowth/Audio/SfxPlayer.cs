using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    /// <summary>
    /// 全局音效池：素材放在 Resources/Audio/SFX/&lt;组&gt;/ 下，用 <see cref="Sfx.Play"/> 按相对路径播放。
    /// 每个声道挂 AudioChannelSource，音量自动跟随「设置 → 音效」滑条。
    /// 自动随首个场景创建，常驻不销毁，不需要往场景里拖任何东西。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SfxPlayer : MonoBehaviour
    {
        private const int PoolSize = 12;
        private const string Root = "Audio/SFX/";

        private static SfxPlayer instance;

        private AudioSource[] sources;
        private readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        private int cursor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
            {
                return;
            }

            GameObject host = new GameObject("[Audio] SfxPlayer");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<SfxPlayer>();
            instance.Build();
        }

        private void Build()
        {
            sources = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                GameObject voice = new GameObject("Voice" + i);
                voice.transform.SetParent(transform, false);
                AudioSource source = voice.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                AudioChannelSource channel = voice.AddComponent<AudioChannelSource>();
                channel.Configure(source, GameAudioChannel.Sfx, 1f);
                sources[i] = source;
            }
        }

        internal static void PlayInternal(string clipPath, float volume, float pitch)
        {
            if (instance == null)
            {
                Bootstrap();
            }

            if (instance == null || instance.sources == null || string.IsNullOrEmpty(clipPath))
            {
                return;
            }

            AudioClip clip = instance.Resolve(clipPath);
            if (clip == null)
            {
                return;
            }

            AudioSource source = instance.sources[instance.cursor];
            instance.cursor = (instance.cursor + 1) % instance.sources.Length;
            source.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private AudioClip Resolve(string clipPath)
        {
            if (cache.TryGetValue(clipPath, out AudioClip cached))
            {
                return cached;
            }

            AudioClip clip = Resources.Load<AudioClip>(Root + clipPath);
            cache[clipPath] = clip;
            return clip;
        }
    }
}
