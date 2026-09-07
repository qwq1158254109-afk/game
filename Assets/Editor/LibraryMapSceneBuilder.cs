using System.Collections.Generic;
using System.Linq;
using StudentGrowth;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LibraryMapSceneBuilder
{
    private const string ShopScenePath = "Assets/Scenes/StudentGrowth/Maps/ShopMapScene.unity";
    private const string LibraryScenePath = "Assets/Scenes/StudentGrowth/Maps/LibraryMapScene.unity";
    private const string CompositePath = "Assets/Art/New2_0/Library/LibraryComposite.png";
    private static bool UseEditableMapBuilder => true;

    [MenuItem("Tools/Student Growth/Build Library Map Scene")]
    public static void BuildScene()
    {
        if (UseEditableMapBuilder)
        {
            StudentGrowthEditor.EditableLibraryMapBuilder.Rebuild();
            return;
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureTexture(CompositePath);
        ConfigureTexture("Assets/Art/New2_0/Library/Source/LibraryShell.png");
        ConfigureTexture("Assets/Art/New2_0/Library/Source/LibraryPropsSheet.png");

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LibraryScenePath) != null)
        {
            Debug.LogWarning("LibraryMapScene already exists; the builder preserved it without overwriting.");
            return;
        }

        if (!AssetDatabase.CopyAsset(ShopScenePath, LibraryScenePath))
        {
            throw new System.InvalidOperationException("Failed to copy the existing map scene template.");
        }

        AssetDatabase.ImportAsset(LibraryScenePath, ImportAssetOptions.ForceSynchronousImport);
        Scene scene = EditorSceneManager.OpenScene(LibraryScenePath, OpenSceneMode.Single);
        GameObject sceneRoot = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "ShopMapSceneRoot");
        GameObject player = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "PlayerRpg");
        GameObject cameraObject = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Main Camera");
        GameObject canvas = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "FullscreenMapCanvas");
        if (sceneRoot == null || player == null || cameraObject == null || canvas == null)
        {
            throw new System.InvalidOperationException("Map template is missing a required root object.");
        }

        sceneRoot.name = "LibraryMapSceneRoot";
        Transform mapRoot = sceneRoot.transform.childCount > 0 ? sceneRoot.transform.GetChild(0) : null;
        if (mapRoot == null)
        {
            throw new System.InvalidOperationException("Map template is missing its RPG content root.");
        }

        mapRoot.name = "LibraryMapRpgRoot";
        for (int i = mapRoot.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(mapRoot.GetChild(i).gameObject);
        }

        GameObject visualRoot = CreateChild("Visual", mapRoot);
        GameObject background = CreateChild("LibraryCompositeBackground", visualRoot.transform);
        SpriteRenderer backgroundRenderer = background.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CompositePath);
        backgroundRenderer.sortingOrder = -100;

        GameObject collisionRoot = CreateChild("Collision", mapRoot);
        AddCollider(collisionRoot.transform, "Wall_Left", new Vector2(-11.45f, 0f), new Vector2(0.7f, 13.4f));
        AddCollider(collisionRoot.transform, "Wall_Right", new Vector2(11.45f, 0f), new Vector2(0.7f, 13.4f));
        AddCollider(collisionRoot.transform, "Wall_Top", new Vector2(0f, 6.55f), new Vector2(22.4f, 0.9f));
        AddCollider(collisionRoot.transform, "Wall_BottomLeft", new Vector2(-7f, -6.25f), new Vector2(8.9f, 0.75f));
        AddCollider(collisionRoot.transform, "Wall_BottomRight", new Vector2(7f, -6.25f), new Vector2(8.9f, 0.75f));
        AddCollider(collisionRoot.transform, "EntrancePillar_Left", new Vector2(-1.8f, -5.75f), new Vector2(0.65f, 1.55f));
        AddCollider(collisionRoot.transform, "EntrancePillar_Right", new Vector2(1.8f, -5.75f), new Vector2(0.65f, 1.55f));

        AddCollider(collisionRoot.transform, "ReceptionDesk", new Vector2(0f, 4.25f), new Vector2(4.6f, 2f));
        AddCollider(collisionRoot.transform, "LoungeChair_Left", new Vector2(-8.25f, 4.15f), new Vector2(1.3f, 1.65f));
        AddCollider(collisionRoot.transform, "LoungeChair_Right", new Vector2(-6f, 4.15f), new Vector2(1.3f, 1.65f));
        AddCollider(collisionRoot.transform, "LoungeTable", new Vector2(-7.1f, 3.25f), new Vector2(1.05f, 0.85f));
        AddCollider(collisionRoot.transform, "ReadingTable_TopRight", new Vector2(6.65f, 4.05f), new Vector2(3f, 1.45f));
        AddCollider(collisionRoot.transform, "Bookshelf_Row1_A", new Vector2(-6.75f, 1.25f), new Vector2(2.55f, 1.7f));
        AddCollider(collisionRoot.transform, "Bookshelf_Row1_B", new Vector2(-3f, 1.25f), new Vector2(2.55f, 1.7f));
        AddCollider(collisionRoot.transform, "Bookshelf_Row1_C", new Vector2(2.75f, 1.25f), new Vector2(2.55f, 1.7f));
        AddCollider(collisionRoot.transform, "Bookshelf_Row1_D", new Vector2(6.5f, 1.25f), new Vector2(2.55f, 1.7f));
        AddCollider(collisionRoot.transform, "ReadingTable_BottomLeft", new Vector2(-7.45f, -2.05f), new Vector2(2.8f, 1.55f));
        AddCollider(collisionRoot.transform, "Bookshelf_Row2", new Vector2(-3f, -2.4f), new Vector2(2.55f, 1.7f));
        AddCollider(collisionRoot.transform, "MobileBookshelf", new Vector2(3f, -2.15f), new Vector2(1.55f, 2.2f));
        AddCollider(collisionRoot.transform, "ReadingTable_BottomRight", new Vector2(6.85f, -2.1f), new Vector2(3.05f, 1.55f));
        AddCollider(collisionRoot.transform, "EntrancePlant_Left", new Vector2(-3.45f, -5.35f), new Vector2(0.8f, 1.3f));
        AddCollider(collisionRoot.transform, "EntrancePlant_Right", new Vector2(2.85f, -5.35f), new Vector2(0.8f, 1.3f));

        GameObject spawnRoot = CreateChild("PlayerSpawnPoints", mapRoot);
        GameObject spawn = CreateChild("LibraryEntrySpawn", spawnRoot.transform);
        spawn.transform.position = new Vector3(0f, -5.35f, 0f);
        spawn.AddComponent<PlayerSpawnPoint>().spawnPointId = "library_entry";

        GameObject cameraBoundsObject = CreateChild("CameraBounds", mapRoot);
        BoxCollider2D cameraBounds = cameraBoundsObject.AddComponent<BoxCollider2D>();
        cameraBounds.size = new Vector2(24f, 16f);
        cameraBounds.isTrigger = true;

        GameObject interactions = CreateChild("InteractableObjects", mapRoot);
        MapInteractionTarget exitTarget = CreateExit(interactions.transform);

        player.transform.position = new Vector3(0f, -5.35f, 0f);
        RpgPlayerController playerController = player.GetComponent<RpgPlayerController>();
        playerController.useMovementBounds = true;
        playerController.movementMin = new Vector2(-10.85f, -5.7f);
        playerController.movementMax = new Vector2(10.85f, 5.75f);
        playerController.canMove = true;
        SpriteRenderer playerRenderer = player.GetComponent<SpriteRenderer>();
        if (playerRenderer != null)
        {
            playerRenderer.sortingOrder = 50;
        }

        Camera sceneCamera = cameraObject.GetComponent<Camera>();
        MapCameraController mapCamera = cameraObject.GetComponent<MapCameraController>();
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        sceneCamera.orthographic = true;
        sceneCamera.orthographicSize = 8f;
        sceneCamera.clearFlags = CameraClearFlags.SolidColor;
        sceneCamera.backgroundColor = Color.black;
        mapCamera.targetCamera = sceneCamera;
        mapCamera.followTarget = player.transform;
        mapCamera.cameraBounds = cameraBounds;
        mapCamera.followEnabled = false;
        mapCamera.useExplicitBounds = false;

        FullscreenMapSceneController fullscreen = sceneRoot.GetComponent<FullscreenMapSceneController>();
        fullscreen.mapId = "location_library";
        fullscreen.displayName = "图书馆";
        fullscreen.defaultSpawnPointId = "library_entry";
        fullscreen.player = player.transform;
        fullscreen.playerController = playerController;
        fullscreen.facingInteractor = player.GetComponent<PlayerFacingInteractor>();
        fullscreen.cameraController = mapCamera;
        fullscreen.cameraBounds = cameraBounds;
        fullscreen.interactionTargets = new[] { exitTarget };
        playerController.interactionFlowController = mapRoot.GetComponent<MapInteractionFlowController>();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, LibraryScenePath))
        {
            throw new System.InvalidOperationException("Failed to save LibraryMapScene.");
        }

        List<EditorBuildSettingsScene> buildScenes = EditorBuildSettings.scenes.ToList();
        if (buildScenes.All(entry => entry.path != LibraryScenePath))
        {
            buildScenes.Insert(Mathf.Min(3, buildScenes.Count), new EditorBuildSettingsScene(LibraryScenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"Created LibraryMapScene with exact reference composite and {collisionRoot.transform.childCount} collision objects.");
    }

    private static MapInteractionTarget CreateExit(Transform parent)
    {
        GameObject exit = CreateChild("LibraryExit", parent);
        exit.transform.position = new Vector3(0f, -6.05f, 0f);
        BoxCollider2D trigger = exit.AddComponent<BoxCollider2D>();
        trigger.size = new Vector2(2.7f, 0.7f);
        trigger.isTrigger = true;

        MapInteractionPoint point = exit.AddComponent<MapInteractionPoint>();
        point.locationId = "location_library";
        point.actionId = "library_exit";
        point.interactionType = MapInteractionType.ReturnToMainUi;
        point.displayName = "返回阶段地图";
        point.description = "离开图书馆，返回阶段地图。";
        point.requiresUnlockedLocation = false;

        MapInteractionTarget target = exit.AddComponent<MapInteractionTarget>();
        target.interactionId = "library_exit";
        target.displayName = "图书馆出口";
        target.promptText = "按 E 返回阶段地图";
        target.interactionPoint = point;
        target.isExitPortal = true;
        target.consumeTimeOnInteraction = false;
        target.requireFacing = false;
        target.interactionRange = 1.15f;
        target.priority = 100;

        MapExitPortal portal = exit.AddComponent<MapExitPortal>();
        portal.destinationId = "StageHubMapScene";
        portal.promptText = "按 E 返回阶段地图";
        return target;
    }

    private static void ConfigureTexture(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            throw new System.InvalidOperationException("Texture importer was not found: " + path);
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    private static GameObject CreateChild(string name, Transform parent)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void AddCollider(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject target = CreateChild(name, parent);
        target.transform.localPosition = position;
        target.AddComponent<BoxCollider2D>().size = size;
    }
}
