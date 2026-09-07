using UnityEngine;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class SpawnPoint : MonoBehaviour
    {
        public string spawnPointId = "Default";
        public Vector2 facingDirection = Vector2.down;

        public bool Matches(string targetSpawnPointId)
        {
            return string.IsNullOrEmpty(targetSpawnPointId) || spawnPointId == targetSpawnPointId;
        }
    }
}
