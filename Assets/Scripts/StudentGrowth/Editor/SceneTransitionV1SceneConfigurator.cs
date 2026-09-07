using StudentGrowth;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudentGrowth.Editor
{
    public static class SceneTransitionV1SceneConfigurator
    {
        private const string HomeScenePath = "Assets/Scenes/StudentGrowth/Maps/HomeMapScene.unity";
        private const string ShopScenePath = "Assets/Scenes/StudentGrowth/Maps/ShopMapScene.unity";

        [MenuItem("StudentGrowth/Scene Routing/Configure Stage Hub Only")]
        public static void ConfigureStageHubOnly()
        {
            ConfigureMapScene(
                HomeScenePath,
                "location_home",
                "exit_home",
                "Door_Home_To_Shop",
                "Spawn_FromShop");
            ConfigureMapScene(
                ShopScenePath,
                "location_shop",
                "exit_shop",
                "Door_Shop_To_Home",
                "Spawn_FromHome");

            AssetDatabase.SaveAssets();
            Debug.Log("Stage Hub-only scene routing configured for HomeMapScene and ShopMapScene.");
        }

        // Kept for callers of the previous editor API. It now applies the Stage Hub-only topology.
        public static void ConfigureHomeShopScenes()
        {
            ConfigureStageHubOnly();
        }

        private static void ConfigureMapScene(
            string scenePath,
            string locationId,
            string interactionId,
            params string[] directTransitionObjectNames)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EnableStageExitInteraction(scene, "ExitDoorObject", locationId, interactionId);

            for (int i = 0; i < directTransitionObjectNames.Length; i++)
            {
                RemoveSceneObject(scene, directTransitionObjectNames[i]);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnableStageExitInteraction(
            Scene scene,
            string objectName,
            string locationId,
            string interactionId)
        {
            GameObject exitObject = FindSceneObject(scene, objectName);
            if (exitObject == null)
            {
                Debug.LogWarning($"Could not configure Stage Hub exit because '{objectName}' is missing in '{scene.path}'.");
                return;
            }

            MapInteractionPoint point = exitObject.GetComponent<MapInteractionPoint>();
            if (point != null)
            {
                point.enabled = true;
                point.locationId = locationId;
                point.actionId = string.Empty;
                point.interactionType = MapInteractionType.ReturnToMainUi;
                point.displayName = "返回阶段地图";
                point.description = "按 E：返回阶段地图｜不消耗时间";
                point.requiresUnlockedLocation = false;
                EditorUtility.SetDirty(point);
            }

            MapInteractionTarget target = exitObject.GetComponent<MapInteractionTarget>();
            if (target != null)
            {
                target.enabled = true;
                target.interactionId = interactionId;
                target.displayName = "返回阶段地图";
                target.promptText = "按 E：返回阶段地图｜不消耗时间";
                target.locationAction = null;
                target.fallbackActivity = null;
                target.interactionPoint = point;
                target.isExitPortal = true;
                target.consumeTimeOnInteraction = false;
                target.requireFacing = false;
                target.interactionRange = 1.45f;
                target.priority = 20;
                EditorUtility.SetDirty(target);
            }

            MapExitPortal exitPortal = exitObject.GetComponent<MapExitPortal>();
            if (exitPortal != null)
            {
                exitPortal.enabled = true;
                exitPortal.destinationId = "stage_hub";
                exitPortal.promptText = "按 E：返回阶段地图";
                EditorUtility.SetDirty(exitPortal);
            }

            EditorUtility.SetDirty(exitObject);
        }

        private static void RemoveSceneObject(Scene scene, string objectName)
        {
            GameObject sceneObject = FindSceneObject(scene, objectName);
            if (sceneObject != null)
            {
                Object.DestroyImmediate(sceneObject);
            }
        }

        private static GameObject FindSceneObject(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < transforms.Length; j++)
                {
                    if (transforms[j].name == objectName)
                    {
                        return transforms[j].gameObject;
                    }
                }
            }

            return null;
        }
    }
}
