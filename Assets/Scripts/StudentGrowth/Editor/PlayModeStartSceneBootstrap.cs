using UnityEditor;
using UnityEditor.SceneManagement;

namespace StudentGrowth.Editor
{
    /// <summary>
    /// 编辑器的「播放」固定从游戏入口开始（MainMenuScene），而不是从当前打开的场景开始。
    ///
    /// 需求（2026-10-14）：点 Play 就进游戏的第一屏（主菜单），别落在上次打开的中途场景。
    /// 想临时从别的场景调试时：Scene 视图 Play 按钮旁的下拉里切换 / 清除
    /// “Play Mode Start Scene”；本脚本只在「未设置过」时补上，不会覆盖你手动选的场景。
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeStartSceneBootstrap
    {
        private const string EntryScenePath = "Assets/Scenes/StudentGrowth/MainMenuScene.unity";

        static PlayModeStartSceneBootstrap()
        {
            EditorApplication.delayCall += ApplyIfUnset;
        }

        private static void ApplyIfUnset()
        {
            if (EditorSceneManager.playModeStartScene != null)
            {
                return;
            }

            SceneAsset entry = AssetDatabase.LoadAssetAtPath<SceneAsset>(EntryScenePath);
            if (entry == null)
            {
                return;
            }

            EditorSceneManager.playModeStartScene = entry;
            UnityEngine.Debug.Log("[PlayMode] 已将「播放起始场景」设为 " + EntryScenePath);
        }
    }
}
