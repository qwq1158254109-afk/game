using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class LocationToastView : MonoBehaviour
    {
        public PixelPanel panel;
        public CanvasGroup canvasGroup;
        public Text locationText;
        public Text stageText;
        public UIThemeConfig theme;
        public float fadeDuration = 0.18f;
        public float visibleDuration = 2.4f;

        private Coroutine presentationRoutine;

        private void OnDisable()
        {
            if (presentationRoutine != null)
            {
                StopCoroutine(presentationRoutine);
                presentationRoutine = null;
            }
        }

        public void Configure(UIThemeConfig config)
        {
            theme = config;
            ApplyStyle();
        }

        public void ApplyStyle()
        {
            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            if (panel != null)
            {
                panel.theme = resolved;
                panel.variant = GameUiPanelVariant.Dark;
                panel.ApplyStyle();
                if (panel.background != null)
                {
                    GameUiStyle.ApplyPanelSprite(panel.background, resolved.hudPlateSprite, resolved.panelDarkBackground);
                    panel.background.raycastTarget = false;
                }
            }

            GameUiStyle.ApplyPixelText(locationText, resolved, GameUiTextStyle.ButtonText, true);
            GameUiStyle.ApplyPixelText(stageText, resolved, GameUiTextStyle.SmallText, true);
            if (locationText != null)
            {
                locationText.alignment = TextAnchor.MiddleCenter;
                locationText.raycastTarget = false;
            }

            if (stageText != null)
            {
                stageText.alignment = TextAnchor.MiddleCenter;
                stageText.raycastTarget = false;
                stageText.color = resolved.hintText;
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        public void Show(string locationName, StageData stage, string activityName = null)
        {
            if (locationText != null)
            {
                locationText.text = string.IsNullOrWhiteSpace(locationName) ? "当前地点" : locationName.Trim();
            }

            if (stageText != null)
            {
                string stageName = stage == null || string.IsNullOrWhiteSpace(stage.displayName)
                    ? string.Empty
                    : "阶段  " + stage.displayName.Trim();
                string activity = string.IsNullOrWhiteSpace(activityName) ? string.Empty : " · " + activityName.Trim();
                stageText.text = stageName + activity;
            }

            gameObject.SetActive(true);
            if (!Application.isPlaying)
            {
                ShowImmediate();
                return;
            }

            if (presentationRoutine != null)
            {
                StopCoroutine(presentationRoutine);
            }

            presentationRoutine = StartCoroutine(Present());
        }

        public void ShowImmediate()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        public void HideImmediate()
        {
            if (presentationRoutine != null)
            {
                StopCoroutine(presentationRoutine);
                presentationRoutine = null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            gameObject.SetActive(false);
        }

        private IEnumerator Present()
        {
            yield return Fade(0f, 1f);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, visibleDuration));
            yield return Fade(1f, 0f);
            presentationRoutine = null;
            gameObject.SetActive(false);
        }

        private IEnumerator Fade(float from, float to)
        {
            if (canvasGroup == null || fadeDuration <= 0f)
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = to;
                }

                yield break;
            }

            float elapsed = 0f;
            canvasGroup.alpha = from;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }

            canvasGroup.alpha = to;
        }
    }
}
