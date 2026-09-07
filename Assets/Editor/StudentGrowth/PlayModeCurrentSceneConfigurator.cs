using UnityEditor;
using UnityEditor.SceneManagement;

namespace StudentGrowthEditor
{
    [InitializeOnLoad]
    internal static class PlayModeCurrentSceneConfigurator
    {
        static PlayModeCurrentSceneConfigurator()
        {
            EditorApplication.delayCall += UseCurrentScene;
        }

        [MenuItem("Tools/Student Growth/Play Mode/Use Current Scene")]
        private static void UseCurrentScene()
        {
            if (EditorSceneManager.playModeStartScene != null)
            {
                EditorSceneManager.playModeStartScene = null;
            }
        }
    }
}
