using System;
using System.Collections;
using UnityEngine;

namespace StudentGrowth
{
    public sealed class MapDoorAnimator : MonoBehaviour
    {
        public SpriteRenderer targetRenderer;
        public Sprite[] openFrames = new Sprite[0];
        public Sprite[] closeFrames = new Sprite[0];
        public float framesPerSecond = 12f;
        public bool resetClosedOnEnable = true;
        public string openingPromptText = "开门中……";

        public bool IsPlaying { get; private set; }

        public bool HasOpenAnimation
        {
            get
            {
                ResolveRenderer();
                return targetRenderer != null && HasFrames(openFrames);
            }
        }

        private Sprite closedSprite;

        private void Awake()
        {
            ResolveRenderer();
            CaptureClosedSprite();
        }

        private void OnEnable()
        {
            if (resetClosedOnEnable)
            {
                ResetToClosed();
            }
        }

        public bool TryPlayOpenThen(Action onOpened)
        {
            if (!Application.isPlaying || !HasOpenAnimation)
            {
                onOpened?.Invoke();
                return false;
            }

            if (IsPlaying)
            {
                return true;
            }

            StartCoroutine(PlayFrames(openFrames, onOpened));
            return true;
        }

        public bool TryPlayClose(Action onClosed = null)
        {
            if (!Application.isPlaying || !HasFrames(closeFrames))
            {
                ResetToClosed();
                onClosed?.Invoke();
                return false;
            }

            if (IsPlaying)
            {
                return true;
            }

            StartCoroutine(PlayFrames(closeFrames, onClosed));
            return true;
        }

        public void ResetToClosed()
        {
            ResolveRenderer();
            CaptureClosedSprite();
            if (targetRenderer == null || closedSprite == null)
            {
                return;
            }

            targetRenderer.sprite = closedSprite;
        }

        private IEnumerator PlayFrames(Sprite[] frames, Action onComplete)
        {
            IsPlaying = true;
            ResolveRenderer();
            float delay = 1f / Mathf.Max(1f, framesPerSecond);

            for (int i = 0; i < frames.Length; i++)
            {
                Sprite frame = frames[i];
                if (targetRenderer != null && frame != null)
                {
                    targetRenderer.sprite = frame;
                }

                yield return new WaitForSecondsRealtime(delay);
            }

            IsPlaying = false;
            onComplete?.Invoke();
        }

        private void ResolveRenderer()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        private void CaptureClosedSprite()
        {
            if (closedSprite == null && targetRenderer != null)
            {
                closedSprite = openFrames != null && openFrames.Length > 0 && openFrames[0] != null
                    ? openFrames[0]
                    : targetRenderer.sprite;
            }
        }

        private static bool HasFrames(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
