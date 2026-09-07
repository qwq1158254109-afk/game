using UnityEngine;

namespace StudentGrowth
{
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        public string spawnPointId = "home_entry";

        public bool Matches(string targetSpawnPointId)
        {
            return string.IsNullOrEmpty(targetSpawnPointId) || spawnPointId == targetSpawnPointId;
        }
    }
}
