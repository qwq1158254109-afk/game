using System.IO;
using System.Linq;
using StudentGrowth;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class MainMenuSceneBuilder
{
    public static void Build()
    {
        var home=EditorSceneManager.OpenPreviewScene(GameApplicationRuntime.HomePath);
        Texture2D texture=null;
        try {
            var cameras=home.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).ToArray();
            var camera=cameras.FirstOrDefault();
            if(camera==null) throw new System.Exception("Home camera missing");
            foreach(var root in home.GetRootGameObjects()) {
                foreach(var ui in root.GetComponentsInChildren<Canvas>(true)) ui.enabled=false;
                foreach(var player in root.GetComponentsInChildren<RpgPlayerController>(true)) player.gameObject.SetActive(false);
                // The authored family character has no NPC component; use its stable interaction ID.
                foreach(var interaction in root.GetComponentsInChildren<MapInteractionTarget>(true))
                    if(interaction.interactionId=="activity_home_family_dinner") interaction.gameObject.SetActive(false);
            }
            camera.enabled=false; camera.orthographic=true; camera.orthographicSize=8;
            camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(home);
            camera.transform.position=new Vector3(0,0,-20); camera.aspect=16f/9;
            var target=new RenderTexture(1920,1080,24); var previous=RenderTexture.active;
            try { camera.targetTexture=target; camera.Render(); RenderTexture.active=target; texture=new Texture2D(1920,1080,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,1920,1080),0,0); texture.Apply(); }
            finally { camera.targetTexture=null; RenderTexture.active=previous; target.Release(); Object.DestroyImmediate(target); }
            Directory.CreateDirectory("Assets/Art/Pixel/UI_New/MainMenu");
            File.WriteAllBytes("Assets/Art/Pixel/UI_New/MainMenu/StudentRoom.png",texture.EncodeToPNG());
        } finally { if(texture!=null) Object.DestroyImmediate(texture); EditorSceneManager.ClosePreviewScene(home); }
        AssetDatabase.Refresh();
        var path="Assets/Art/Pixel/UI_New/MainMenu/StudentRoom.png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.filterMode=FilterMode.Point; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.mipmapEnabled=false; importer.SaveAndReimport();
        var original=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        try {
            MainMenuView.Create(GameUiStyle.ResolveTheme(null).ResolveFont(),AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            EditorSceneManager.SaveScene(scene,GameApplicationRuntime.MainMenuPath);
            var scenes=EditorBuildSettings.scenes.Where(s=>s.path!=GameApplicationRuntime.MainMenuPath).ToList(); scenes.Insert(0,new EditorBuildSettingsScene(GameApplicationRuntime.MainMenuPath,true)); EditorBuildSettings.scenes=scenes.ToArray();
        } finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene,true); }
    }
}
