using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    [DefaultExecutionOrder(-95)]
    public sealed class FadeController : MonoBehaviour
    {
        private const string RootName = "SceneTransitionFadeController";

        private static FadeController instance;

        public float defaultDuration = 0.35f;
        public Canvas canvas;
        public CanvasGroup canvasGroup;
        public Image fadeImage;

        public static FadeController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<FadeController>();
                }

                if (instance == null)
                {
                    GameObject root = new GameObject(RootName);
                    instance = root.AddComponent<FadeController>();
                }

                return instance;
            }
        }

        public IEnumerator FadeIn(float duration = -1f)
        {
            yield return FadeTo(1f, duration);
        }

        public IEnumerator FadeOut(float duration = -1f)
        {
            yield return FadeTo(0f, duration);
        }

        public void SetAlpha(float alpha)
        {
            EnsureCanvas();
            SetImageAlpha(alpha);
            canvasGroup.blocksRaycasts = alpha > 0.001f;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureCanvas();
            SetAlpha(0f);
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            EnsureCanvas();
            duration = duration < 0f ? defaultDuration : duration;
            float startAlpha = fadeImage == null ? 0f : fadeImage.color.a;
            canvasGroup.blocksRaycasts = true;

            if (duration <= 0f)
            {
                SetImageAlpha(targetAlpha);
                canvasGroup.blocksRaycasts = targetAlpha > 0.001f;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetImageAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
                yield return null;
            }

            SetImageAlpha(targetAlpha);
            canvasGroup.blocksRaycasts = targetAlpha > 0.001f;
        }

        private void EnsureCanvas()
        {
            if (canvas != null && canvasGroup != null && fadeImage != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("GlobalCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            canvasGroup = canvasObject.AddComponent<CanvasGroup>();

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject imageObject = new GameObject("FadeBlack", typeof(RectTransform));
            RectTransform imageRect = imageObject.transform as RectTransform;
            imageRect.SetParent(canvasObject.transform, false);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            fadeImage = imageObject.AddComponent<Image>();
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.raycastTarget = true;
        }

        private void SetImageAlpha(float alpha)
        {
            if (fadeImage == null)
            {
                return;
            }

            Color color = fadeImage.color;
            color.a = Mathf.Clamp01(alpha);
            fadeImage.color = color;
        }
    }
}
