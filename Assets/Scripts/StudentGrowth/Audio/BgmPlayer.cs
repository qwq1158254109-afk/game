using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudentGrowth
{
    /// <summary>
    /// 背景音乐播放器：双声道交叉淡入淡出，音量跟随「设置 → BGM」。
    /// 场景切换时按 SceneTracks 表自动换曲；表里没有的场景保持当前曲目不中断。
    /// BGM 文件放 Resources/Audio/BGM/&lt;名&gt;。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BgmPlayer : MonoBehaviour
    {
        private const string Root = "Audio/BGM/";
        private const float DefaultFade = 0.9f;

        /// <summary>场景名 → BGM 资源名。素材补进来后在这里登记即可。</summary>
        private static readonly Dictionary<string, string> SceneTracks = new Dictionary<string, string>
        {
            // { "MainMenu", "title" },
            // { "HomeLivingRoomScene", "home_day" },
            // { "SchoolMapScene", "school" },
        };

        private static BgmPlayer instance;

        private AudioSource sourceA;
        private AudioSource sourceB;
        private AudioSource active;
        private string currentPath;
        private Coroutine fadeRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
            {
                return;
            }

            GameObject host = new GameObject("[Audio] BgmPlayer");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<BgmPlayer>();
            instance.Build();
            SceneManager.sceneLoaded += instance.HandleSceneLoaded;
        }

        private void Build()
        {
            sourceA = CreateVoice("BgmA");
            sourceB = CreateVoice("BgmB");
            active = sourceA;
        }

        private AudioSource CreateVoice(string name)
        {
            GameObject voice = new GameObject(name);
            voice.transform.SetParent(transform, false);
            AudioSource source = voice.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            AudioChannelSource channel = voice.AddComponent<AudioChannelSource>();
            channel.Configure(source, GameAudioChannel.Music, 1f);
            return source;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (SceneTracks.TryGetValue(scene.name, out string track))
            {
                Play(track);
            }
        }

        public static void Play(string clipPath, float fade = DefaultFade)
        {
            if (instance == null || string.IsNullOrEmpty(clipPath))
            {
                return;
            }

            if (instance.currentPath == clipPath && instance.active != null && instance.active.isPlaying)
            {
                return;
            }

            AudioClip clip = Resources.Load<AudioClip>(Root + clipPath);
            if (clip == null)
            {
                return;
            }

            instance.currentPath = clipPath;
            instance.CrossFade(clip, fade);
        }

        public static void Stop(float fade = DefaultFade)
        {
            if (instance == null)
            {
                return;
            }

            instance.currentPath = null;
            instance.FadeOut(fade);
        }

        private void CrossFade(AudioClip clip, float fade)
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            fadeRoutine = StartCoroutine(CrossFadeRoutine(clip, Mathf.Max(0.01f, fade)));
        }

        private IEnumerator CrossFadeRoutine(AudioClip clip, float duration)
        {
            AudioSource from = active;
            AudioSource to = active == sourceA ? sourceB : sourceA;
            to.clip = clip;
            to.volume = 0f;
            to.Play();
            active = to;

            float fromStart = from != null ? from.volume : 0f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                if (to != null)
                {
                    to.volume = k;
                }

                if (from != null)
                {
                    from.volume = fromStart * (1f - k);
                }

                yield return null;
            }

            if (to != null)
            {
                to.volume = 1f;
            }

            if (from != null)
            {
                from.Stop();
                from.volume = 0f;
            }

            fadeRoutine = null;
        }

        private void FadeOut(float duration)
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            fadeRoutine = StartCoroutine(FadeOutRoutine(Mathf.Max(0.01f, duration)));
        }

        private IEnumerator FadeOutRoutine(float duration)
        {
            AudioSource from = active;
            float start = from != null ? from.volume : 0f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (from != null)
                {
                    from.volume = start * (1f - t / duration);
                }

                yield return null;
            }

            if (from != null)
            {
                from.Stop();
                from.volume = 0f;
            }

            fadeRoutine = null;
        }
    }
}
