using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StudentGrowth
{
    /// <summary>
    /// 睡觉过场：黑场 → 「第 N 天」卡片 → 淡回场景。
    ///
    /// 触发点是 <see cref="TimeActionController.DayStarted"/>（只在夜晚睡出新一天时发布），
    /// 所以白天小睡、公交站过夜都不会误触发。播放期间占用 <see cref="GameplayInputGate"/>，
    /// 避免玩家在黑场里连按 E 重复睡觉。
    ///
    /// 不用 <see cref="FadeController"/>：那个画布 sortingOrder=32760 且被场景切换复用，
    /// 两边同时淡入会互相抢 alpha。这里自建一个 32765 的画布，只负责这一件事。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SleepTransitionView : MonoBehaviour
    {
        private const float FadeOutSeconds = 0.35f;
        private const float TextFadeSeconds = 0.2f;
        private const float HoldSeconds = 1.0f;
        private const float FadeInSeconds = 0.45f;

        private static SleepTransitionView instance;

        private CanvasGroup blackGroup;
        private CanvasGroup textGroup;
        private Text dayText;
        private Text subText;
        private TimeActionController boundController;
        private bool playing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Object.FindAnyObjectByType<FullscreenMapSceneController>() == null)
            {
                return;
            }

            if (instance == null)
            {
                GameObject host = new GameObject("[UI] SleepTransition");
                instance = host.AddComponent<SleepTransitionView>();
                instance.Build();
            }

            instance.BindController();
        }

        private void OnDestroy()
        {
            UnbindController();
            if (playing)
            {
                playing = false;
                GameplayInputGate.Release(this, true);
            }

            if (instance == this)
            {
                instance = null;
            }
        }

        private void BindController()
        {
            StartCoroutine(BindNextFrame());
        }

        private IEnumerator BindNextFrame()
        {
            // 地图控制器可能在 sceneLoaded 之后才把 TimeActionController 建好，等几帧。
            for (int i = 0; i < 30 && boundController == null; i++)
            {
                TimeActionController controller = Object.FindAnyObjectByType<TimeActionController>();
                if (controller != null)
                {
                    UnbindController();
                    boundController = controller;
                    boundController.DayStarted += OnDayStarted;
                    yield break;
                }

                yield return null;
            }
        }

        private void UnbindController()
        {
            if (boundController != null)
            {
                boundController.DayStarted -= OnDayStarted;
                boundController = null;
            }
        }

        private void OnDayStarted(int day)
        {
            if (playing || blackGroup == null)
            {
                return;
            }

            if (SceneTransitionManager.HasActiveTransition)
            {
                return;
            }

            StartCoroutine(Play(day));
        }

        private IEnumerator Play(int day)
        {
            playing = true;
            GameplayInputGate.Acquire(this);
            blackGroup.blocksRaycasts = true;

            dayText.text = "第 " + day + " 天";
            // 只陈述真实发生的结算：TimeActionController.Sleep 目前推进日期/时段，
            // 另在公交站过夜时扣心情。精力/压力的睡眠结算（+15 / -2）还没实施，
            // 文案不能写成「精力与压力已结算」，那是对玩家说谎。
            subText.text = "新的一天开始了";

            textGroup.alpha = 0f;
            yield return FadeGroup(blackGroup, 0f, 1f, FadeOutSeconds);
            yield return FadeGroup(textGroup, 0f, 1f, TextFadeSeconds);
            yield return new WaitForSecondsRealtime(HoldSeconds);
            yield return FadeGroup(textGroup, 1f, 0f, TextFadeSeconds);
            yield return FadeGroup(blackGroup, 1f, 0f, FadeInSeconds);

            GameplayInputGate.Release(this, true);
            blackGroup.blocksRaycasts = false;
            playing = false;
        }

        private static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null)
            {
                yield break;
            }

            group.alpha = from;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            group.alpha = to;
        }

        private void Build()
        {
            UIThemeConfig theme = GameUiStyle.ResolveTheme(null);

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32765;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform black = CreateRect("Black", transform, Vector2.zero, Vector2.zero);
            Stretch(black);
            Image blackImage = black.gameObject.AddComponent<Image>();
            blackImage.color = Color.black;
            blackImage.raycastTarget = true;
            blackGroup = black.gameObject.AddComponent<CanvasGroup>();
            blackGroup.alpha = 0f;
            // 透明 ≠ 不挡点击：CanvasGroup 默认 blocksRaycasts=true，
            // 不关掉的话这块全屏黑幕会在平时吃掉所有 UI 点击。
            blackGroup.blocksRaycasts = false;
            blackGroup.interactable = false;

            RectTransform textRoot = CreateRect("DayCard", transform, new Vector2(600f, 160f), Vector2.zero);
            textRoot.anchorMin = new Vector2(0.5f, 0.5f);
            textRoot.anchorMax = new Vector2(0.5f, 0.5f);
            textRoot.pivot = new Vector2(0.5f, 0.5f);
            textRoot.anchoredPosition = Vector2.zero;
            textGroup = textRoot.gameObject.AddComponent<CanvasGroup>();
            textGroup.alpha = 0f;
            textGroup.blocksRaycasts = false;
            textGroup.interactable = false;

            dayText = CreateText("Day", textRoot, "第 1 天", GameUiTextStyle.Title, theme, new Vector2(0f, 28f), new Vector2(560f, 56f));
            subText = CreateText("Sub", textRoot, string.Empty, GameUiTextStyle.Body, theme, new Vector2(0f, -30f), new Vector2(600f, 32f));
        }

        private static Text CreateText(string name, Transform parent, string content, GameUiTextStyle style, UIThemeConfig theme, Vector2 pos, Vector2 size)
        {
            RectTransform rect = CreateRect(name, parent, size, pos);
            Text text = rect.gameObject.AddComponent<Text>();
            text.text = content;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            GameUiStyle.ApplyPixelText(text, theme, style, true);
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
