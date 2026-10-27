using UnityEngine;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class MapSpriteOcclusionFader : MonoBehaviour
    {
        public SpriteRenderer[] targetRenderers = new SpriteRenderer[0];
        public SpriteRenderer playerRenderer;
        public float fadedAlpha = 0.45f;
        public float fadeSpeed = 8f;
        public Vector2 boundsPadding = new Vector2(0.15f, 0.1f);

        private float[] originalAlphas = new float[0];

        private void Awake()
        {
            EnsureRenderers();
            CaptureOriginalAlphas();
        }

        private void LateUpdate()
        {
            EnsureRenderers();
            EnsurePlayerRenderer();

            bool shouldFade = playerRenderer != null && CanDrawOverPlayer() && IsOverlappingPlayer();
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                SpriteRenderer renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                float originalAlpha = i < originalAlphas.Length ? originalAlphas[i] : 1f;
                float targetAlpha = shouldFade ? Mathf.Min(originalAlpha, fadedAlpha) : originalAlpha;
                Color color = renderer.color;
                color.a = Mathf.MoveTowards(color.a, targetAlpha, fadeSpeed * Time.deltaTime);
                renderer.color = color;
            }
        }

        private void OnDisable()
        {
            RestoreOriginalAlphas();
        }

        private void EnsureRenderers()
        {
            if (targetRenderers != null && targetRenderers.Length > 0)
            {
                return;
            }

            targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            CaptureOriginalAlphas();
        }

        private void CaptureOriginalAlphas()
        {
            if (targetRenderers == null)
            {
                originalAlphas = new float[0];
                return;
            }

            originalAlphas = new float[targetRenderers.Length];
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                originalAlphas[i] = targetRenderers[i] != null ? targetRenderers[i].color.a : 1f;
            }
        }

        private void RestoreOriginalAlphas()
        {
            if (targetRenderers == null)
            {
                return;
            }

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                SpriteRenderer renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Color color = renderer.color;
                color.a = i < originalAlphas.Length ? originalAlphas[i] : 1f;
                renderer.color = color;
            }
        }

        private void EnsurePlayerRenderer()
        {
            if (playerRenderer != null)
            {
                return;
            }

            RpgPlayerController player = FindAnyObjectByType<RpgPlayerController>();
            if (player != null)
            {
                playerRenderer = player.spriteRenderer != null
                    ? player.spriteRenderer
                    : player.GetComponentInChildren<SpriteRenderer>();
            }
        }

        private bool IsOverlappingPlayer()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                return false;
            }

            Bounds occluderBounds = default(Bounds);
            bool hasBounds = false;
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                SpriteRenderer renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    occluderBounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                occluderBounds.Encapsulate(renderer.bounds);
            }

            if (!hasBounds)
            {
                return false;
            }

            occluderBounds.Expand(new Vector3(boundsPadding.x, boundsPadding.y, 0f));
            return occluderBounds.Intersects(playerRenderer.bounds);
        }

        private bool CanDrawOverPlayer()
        {
            if (targetRenderers == null)
            {
                return false;
            }

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                SpriteRenderer renderer = targetRenderers[i];
                if (renderer != null && renderer.sortingOrder >= playerRenderer.sortingOrder)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
