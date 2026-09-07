using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StudentGrowth
{
    [DefaultExecutionOrder(90)]
    public sealed class InteractionPromptUI : MonoBehaviour
    {
        private const string RootName = "InteractionPromptUI";

        private static InteractionPromptUI instance;

        public MapInteractionPromptView mapPromptView;
        public MapInteractionPromptView fallbackPromptView;
        public UIThemeConfig theme;
        public Canvas fallbackCanvas;
        public GameObject fallbackPanel;
        public PixelPanel fallbackPanelStyle;
        public CanvasGroup fallbackCanvasGroup;
        public Text fallbackText;

        private object owner;

        public static InteractionPromptUI Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<InteractionPromptUI>();
                }

                if (instance == null)
                {
                    GameObject root = new GameObject(RootName);
                    instance = root.AddComponent<InteractionPromptUI>();
                }

                return instance;
            }
        }

        public static void Show(string message, object promptOwner = null)
        {
            Instance.ShowMessage(message, promptOwner);
        }

        public static void Hide(object promptOwner = null)
        {
            if (instance != null)
            {
                instance.HideMessage(promptOwner);
            }
        }

        public static void HideAll()
        {
            if (instance != null)
            {
                instance.owner = null;
                instance.HideFallbackMessage();
                instance.SetVisible(false);
            }

            if (instance != null)
            {
                instance.ResolvePromptView();
                if (instance.mapPromptView != null)
                {
                    instance.mapPromptView.ShowMessage(string.Empty);
                }
            }
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
            ResolvePromptView();
        }

        private void ShowMessage(string message, object promptOwner)
        {
            owner = promptOwner;
            ResolvePromptView();

            if (mapPromptView != null)
            {
                HideFallbackMessage();
                if (theme != null)
                {
                    mapPromptView.theme = theme;
                }

                mapPromptView.ApplyStyle();
                mapPromptView.ShowMessage(message);
                return;
            }

            MapInteractionPromptView fallback = EnsureFallbackPromptView();
            if (fallback != null)
            {
                fallback.theme = theme;
                fallback.ApplyStyle();
                fallback.ShowMessage(message);
            }
        }

        private void HideMessage(object promptOwner)
        {
            if (promptOwner != null && owner != null && promptOwner != owner)
            {
                return;
            }

            owner = null;
            ResolvePromptView();
            if (mapPromptView != null)
            {
                HideFallbackMessage();
                mapPromptView.ShowDefault();
                return;
            }

            HideFallbackMessage();
        }

        private void ResolvePromptView()
        {
            if (mapPromptView != null && mapPromptView != fallbackPromptView)
            {
                return;
            }

            mapPromptView = null;
            MapInteractionPromptView[] views = FindObjectsByType<MapInteractionPromptView>(FindObjectsInactive.Include);
            for (int i = 0; i < views.Length; i++)
            {
                MapInteractionPromptView view = views[i];
                if (view != null
                    && view != fallbackPromptView
                    && !view.transform.IsChildOf(transform))
                {
                    mapPromptView = view;
                    return;
                }
            }
        }

        private MapInteractionPromptView EnsureFallbackPromptView()
        {
            EnsureFallbackUi();
            return fallbackPromptView;
        }

        private void EnsureFallbackUi()
        {
            if (fallbackPromptView != null)
            {
                SyncFallbackFields();
                ApplyFallbackStyle();
                return;
            }

            if (fallbackPanel != null)
            {
                fallbackPromptView = fallbackPanel.GetComponent<MapInteractionPromptView>();
                if (fallbackPromptView != null)
                {
                    SyncFallbackFields();
                    ApplyFallbackStyle();
                    return;
                }
            }

            EnsureEventSystem();

            if (fallbackCanvas == null)
            {
                fallbackCanvas = MapUiFactory.CreateFullscreenMapCanvas();
                fallbackCanvas.gameObject.name = "InteractionPromptCanvas";
                fallbackCanvas.transform.SetParent(transform, false);
                fallbackCanvas.sortingOrder = 32000;
            }

            RectTransform canvasRect = fallbackCanvas.transform as RectTransform;
            fallbackPromptView = MapUiFactory.CreatePromptView(canvasRect);
            fallbackPromptView.showDefaultWhenIdle = false;
            fallbackPromptView.applyStyleOnAwake = false;
            fallbackPromptView.theme = theme;
            SyncFallbackFields();
            ApplyFallbackStyle();
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (fallbackCanvasGroup != null)
            {
                fallbackCanvasGroup.alpha = visible ? 1f : 0f;
                fallbackCanvasGroup.interactable = false;
                fallbackCanvasGroup.blocksRaycasts = false;
            }

            if (fallbackPanel != null)
            {
                fallbackPanel.SetActive(visible);
            }
        }

        private void ApplyFallbackStyle()
        {
            if (fallbackPromptView != null)
            {
                fallbackPromptView.theme = theme;
                fallbackPromptView.ApplyStyle();
                SyncFallbackFields();
                return;
            }

            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            if (fallbackPanel != null && fallbackPanelStyle == null)
            {
                fallbackPanelStyle = fallbackPanel.GetComponent<PixelPanel>();
                if (fallbackPanelStyle == null)
                {
                    Image image = fallbackPanel.GetComponent<Image>();
                    if (image == null)
                    {
                        image = fallbackPanel.AddComponent<Image>();
                    }

                    image.raycastTarget = false;
                    fallbackPanelStyle = fallbackPanel.AddComponent<PixelPanel>();
                    fallbackPanelStyle.background = image;
                }
            }

            if (fallbackPanel != null && fallbackCanvasGroup == null)
            {
                fallbackCanvasGroup = UiAnimationHelper.EnsureCanvasGroup(fallbackPanel);
            }

            if (fallbackPanelStyle != null)
            {
                fallbackPanelStyle.theme = resolved;
                fallbackPanelStyle.variant = GameUiPanelVariant.Dark;
                fallbackPanelStyle.ApplyStyle();
            }

            GameUiStyle.ApplyPixelText(fallbackText, resolved, GameUiTextStyle.HintText, true);
            if (fallbackText != null)
            {
                fallbackText.alignment = TextAnchor.MiddleCenter;
            }
        }

        private void HideFallbackMessage()
        {
            if (fallbackPromptView != null)
            {
                fallbackPromptView.ShowMessage(string.Empty);
                SyncFallbackFields();
                return;
            }

            SetVisible(false);
        }

        private void SyncFallbackFields()
        {
            if (fallbackPromptView == null)
            {
                return;
            }

            fallbackPanel = fallbackPromptView.panelRoot;
            fallbackPanelStyle = fallbackPromptView.panel;
            fallbackCanvasGroup = fallbackPromptView.canvasGroup;
            fallbackText = fallbackPromptView.promptText;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(eventSystemObject);
        }
    }
}
