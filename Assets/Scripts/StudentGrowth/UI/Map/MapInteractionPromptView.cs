using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public sealed class MapInteractionPromptView : MonoBehaviour
    {
        public UIThemeConfig theme;
        public GameObject panelRoot;
        public PixelPanel panel;
        public CanvasGroup canvasGroup;
        public Text promptText;
        public bool showDefaultWhenIdle;
        public bool debugVisible;
        public bool applyStyleOnAwake = true;

        private void Awake()
        {
            ResolveReferences();
            if (applyStyleOnAwake)
            {
                ApplyStyle();
            }

            ShowDefault();
        }

        private void OnEnable()
        {
            if (applyStyleOnAwake)
            {
                ApplyStyle();
            }
        }

        public void ApplyStyle()
        {
            ResolveReferences();
            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            ApplyCompactLayout();
            if (panel != null)
            {
                panel.theme = resolved;
                panel.variant = GameUiPanelVariant.Dark;
                panel.ApplyStyle();
                if (panel.background != null)
                {
                    GameUiStyle.ApplyPanelSprite(panel.background, resolved.promptBarSprite, GameUiStyle.ResolvePanelColor(resolved, GameUiPanelVariant.Dark));
                }
            }

            GameUiStyle.ApplyPixelText(promptText, resolved, GameUiTextStyle.HintText, true);
            if (promptText != null)
            {
                promptText.alignment = TextAnchor.MiddleCenter;
                promptText.fontSize = 16;
            }
        }

        public void ShowDefault()
        {
            if (!showDefaultWhenIdle)
            {
                SetVisible(false);
                return;
            }

            ShowMessage(MapInteractionPromptFormatter.BuildDefaultPrompt(debugVisible));
        }

        public void ShowTarget(MapInteractionTarget target)
        {
            if (target == null)
            {
                ShowDefault();
                return;
            }

            ShowMessage(BuildPromptForTarget(target));
        }

        public void ShowMessage(string message)
        {
            SetVisible(!string.IsNullOrEmpty(message));
            if (promptText != null)
            {
                promptText.text = message;
            }
        }

        public static string BuildPromptForTarget(MapInteractionTarget target)
        {
            if (target == null)
            {
                return MapInteractionPromptFormatter.BuildDefaultPrompt(false);
            }

            if (!string.IsNullOrEmpty(target.promptText))
            {
                return target.promptText;
            }

            if (target.isExitPortal)
            {
                return MapInteractionPromptFormatter.BuildExitPrompt();
            }

            ActivityData activity = ResolveActivity(target);
            if (activity != null)
            {
                return MapInteractionPromptFormatter.BuildActivityPrompt(
                    ResolveDisplayName(target, activity.displayName),
                    activity.timeCost,
                    MapInteractionPromptFormatter.BuildRewardPreview(activity));
            }

            LocationActionData action = target.locationAction;
            if (action != null)
            {
                return MapInteractionPromptFormatter.BuildActivityPrompt(
                    ResolveDisplayName(target, action.displayName),
                    action.timeCost,
                    MapInteractionPromptFormatter.BuildRewardPreview(action));
            }

            return MapInteractionPromptFormatter.BuildUnavailablePrompt(target.displayName);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(visible);
            }
            else if (promptText != null)
            {
                promptText.gameObject.SetActive(visible);
            }
        }

        private void ResolveReferences()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (panel == null && panelRoot != null)
            {
                panel = panelRoot.GetComponent<PixelPanel>();
                if (panel == null)
                {
                    Image image = panelRoot.GetComponent<Image>();
                    if (image == null)
                    {
                        image = panelRoot.AddComponent<Image>();
                        image.raycastTarget = false;
                    }

                    panel = panelRoot.AddComponent<PixelPanel>();
                    panel.background = image;
                }
            }

            if (canvasGroup == null && panelRoot != null)
            {
                canvasGroup = UiAnimationHelper.EnsureCanvasGroup(panelRoot);
            }
        }

        private void ApplyCompactLayout()
        {
            RectTransform rootRect = panelRoot == null ? transform as RectTransform : panelRoot.transform as RectTransform;
            if (rootRect != null)
            {
                rootRect.anchorMin = new Vector2(0.5f, 0f);
                rootRect.anchorMax = new Vector2(0.5f, 0f);
                rootRect.pivot = new Vector2(0.5f, 0f);
                rootRect.anchoredPosition = new Vector2(0f, 92f);
                rootRect.sizeDelta = new Vector2(560f, 36f);
            }

            if (promptText != null)
            {
                RectTransform textRect = promptText.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(24f, 6f);
                textRect.offsetMax = new Vector2(-24f, -6f);
            }
        }

        private static ActivityData ResolveActivity(MapInteractionTarget target)
        {
            if (target == null)
            {
                return null;
            }

            if (target.fallbackActivity != null)
            {
                return target.fallbackActivity;
            }

            return target.locationAction == null ? null : target.locationAction.linkedActivity;
        }

        private static string ResolveDisplayName(MapInteractionTarget target, string fallback)
        {
            if (target != null && !string.IsNullOrEmpty(target.displayName))
            {
                return target.displayName;
            }

            return string.IsNullOrEmpty(fallback) ? "互动" : fallback;
        }

    }
}
