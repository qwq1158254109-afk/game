using System.Collections;
using UnityEngine;

namespace StudentGrowth
{
    public static class UiAnimationHelper
    {
        public static CanvasGroup EnsureCanvasGroup(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            CanvasGroup group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.AddComponent<CanvasGroup>();
        }

        public static Coroutine FadeTo(MonoBehaviour runner, CanvasGroup group, float targetAlpha, float duration)
        {
            if (runner == null || group == null)
            {
                return null;
            }

            return runner.StartCoroutine(FadeRoutine(group, targetAlpha, Mathf.Max(0f, duration)));
        }

        public static Coroutine ScaleTo(MonoBehaviour runner, RectTransform target, Vector3 targetScale, float duration)
        {
            if (runner == null || target == null)
            {
                return null;
            }

            return runner.StartCoroutine(ScaleRoutine(target, targetScale, Mathf.Max(0f, duration)));
        }

        public static Coroutine MoveAnchoredTo(MonoBehaviour runner, RectTransform target, Vector2 targetPosition, float duration)
        {
            if (runner == null || target == null)
            {
                return null;
            }

            return runner.StartCoroutine(MoveAnchoredRoutine(target, targetPosition, Mathf.Max(0f, duration)));
        }

        public static void SetVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
            {
                return;
            }

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private static IEnumerator FadeRoutine(CanvasGroup group, float targetAlpha, float duration)
        {
            float startAlpha = group.alpha;
            if (duration <= 0f)
            {
                group.alpha = targetAlpha;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Smooth01(elapsed / duration);
                group.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            group.alpha = targetAlpha;
        }

        private static IEnumerator ScaleRoutine(RectTransform target, Vector3 targetScale, float duration)
        {
            Vector3 startScale = target.localScale;
            if (duration <= 0f)
            {
                target.localScale = targetScale;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Smooth01(elapsed / duration);
                target.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
                yield return null;
            }

            target.localScale = targetScale;
        }

        private static IEnumerator MoveAnchoredRoutine(RectTransform target, Vector2 targetPosition, float duration)
        {
            Vector2 startPosition = target.anchoredPosition;
            if (duration <= 0f)
            {
                target.anchoredPosition = targetPosition;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Smooth01(elapsed / duration);
                target.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, t);
                yield return null;
            }

            target.anchoredPosition = targetPosition;
        }

        private static float Smooth01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }
}
