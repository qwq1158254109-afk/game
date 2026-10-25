using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth.EditorTools
{
    /// <summary>
    /// 把运行时搭出来的对话 UI 固化成预制体，方便在 Unity 里可视化改布局。
    ///
    /// 用法：菜单 Tools → Student Growth → UI → 生成对话预制体。
    /// 产物：Assets/Resources/StudentGrowth/UI/DialogueView.prefab
    /// 运行时 MapInteractionFlowController 会优先实例化这个预制体，找不到才回退到代码搭建。
    /// </summary>
    public static class DialoguePrefabBuilder
    {
        private const string PrefabPath = "Assets/Resources/StudentGrowth/UI/DialogueView.prefab";
        private const string ThemePath = "StudentGrowth/UI/StudentGrowthPixelTheme";

        [MenuItem("Tools/Student Growth/UI/生成对话预制体")]
        public static void Build()
        {
            UIThemeConfig theme = Resources.Load<UIThemeConfig>(ThemePath);
            if (theme == null)
            {
                Debug.LogWarning("[DialoguePrefab] 找不到主题：" + ThemePath);
            }

            GameObject root = new GameObject("DialogueCanvas", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 215;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            GameObject host = new GameObject("DialogueHost", typeof(RectTransform));
            RectTransform hostRect = host.transform as RectTransform;
            hostRect.SetParent(root.transform, false);
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.offsetMin = Vector2.zero;
            hostRect.offsetMax = Vector2.zero;

            DialogueView view = host.AddComponent<DialogueView>();
            // Awake 会把 panelRoot 兜底成自身，先清空才建得出默认布局。
            view.panelRoot = null;
            view.theme = theme;
            view.BuildDefaultLayout(hostRect);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
            Debug.Log("[DialoguePrefab] 已生成 " + PrefabPath + "，可以直接在 Project 里双击编辑。");
        }
    }
}
