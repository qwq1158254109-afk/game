using UnityEngine;

namespace StudentGrowth
{
    [CreateAssetMenu(menuName = "StudentGrowth/Map/Map Scene Config")]
    public sealed class MapSceneConfigData : ScriptableObject
    {
        public string mapId;
        public string displayName;
        public string sceneName;
        public string defaultSpawnPointId = "home_entry";
        public string returnSpawnPointId = "from_home_door";
        public bool showInventoryButton = true;
        public bool showDeckButton = true;
        public bool allowReturnToStageMap = true;
        public Vector2 cameraMin = new Vector2(-3.2f, -1.8f);
        public Vector2 cameraMax = new Vector2(3.2f, 1.8f);
    }
}
