using System;

namespace StudentGrowth
{
    [Serializable]
    public sealed class LocationSceneBinding
    {
        public string locationId;
        public string sceneName;
        public string defaultSpawnPointId;
        public string returnSpawnPointId;

        public bool Matches(string targetLocationId)
        {
            return !string.IsNullOrEmpty(locationId) && locationId == targetLocationId;
        }
    }
}
