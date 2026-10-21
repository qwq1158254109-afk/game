using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudentGrowth
{
    /// <summary>
    /// 房间切换路由（房间 ≠ 地点）：黑场 → 保存共享会话 → 加载目标房间场景
    /// → 重绑玩家并传送到目标出生点 → 淡出。
    /// 场景必须已加入 Build Settings。
    /// </summary>
    public static class RoomDoorRouter
    {
        private static bool busy;

        public static void Enter(RoomDoor door)
        {
            if (door == null || busy || string.IsNullOrEmpty(door.targetSceneName))
            {
                return;
            }

            busy = true;
            // 开门音移到 RoomDoor：要在门开始动的那一刻响，而不是黑场前才响。
            FadeController.Instance.StartCoroutine(Transition(door));
        }

        private static IEnumerator Transition(RoomDoor door)
        {
            FadeController fade = FadeController.Instance;
            yield return fade.FadeIn(0.18f);   // FadeIn = 变黑

            FullscreenMapSceneController source = Object.FindAnyObjectByType<FullscreenMapSceneController>();
            if (source != null && source.sessionState != null)
            {
                GameBootstrapper.SetSharedSession(source.sessionState);
            }

            string spawnId = door.targetSpawnPointId;
            SceneManager.LoadScene(door.targetSceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;                 // 等新场景初始化（含控制器绑定玩家）

            RpgPlayerController survivor = PlayerManager.Instance != null
                ? PlayerManager.Instance.ResolvePlayerAfterSceneLoad()
                : null;

            FullscreenMapSceneController[] maps = Object.FindObjectsByType<FullscreenMapSceneController>(FindObjectsSortMode.None);
            for (int i = 0; i < maps.Length; i++)
            {
                FullscreenMapSceneController map = maps[i];
                if (map == null)
                {
                    continue;
                }

                if (survivor != null)
                {
                    map.RebindPlayer(survivor);
                }

                if (!string.IsNullOrEmpty(spawnId))
                {
                    map.MovePlayerToSpawn(spawnId);
                }
            }

            // 落地朝向：一律朝向房间内部（背离刚进来的那扇门）。
            // 不设的话玩家会保留进门时的朝向（通常是朝着门），而 RoomDoor 的
            // 「已在区内→朝门走就触发」判定就会在落地瞬间把玩家又送回去。
            FaceRoomInterior(survivor);

            yield return fade.FadeOut(0.25f);  // FadeOut = 恢复透明
            Sfx.World.DoorClose();             // 到房间后关门音
            busy = false;
        }

        /// <summary>把玩家朝向摆到「指向房间中心」的方向，避免落地即朝门。</summary>
        private static void FaceRoomInterior(RpgPlayerController player)
        {
            if (player == null)
            {
                return;
            }

            Vector2 position = player.transform.position;
            Vector2 inward = new Vector2(-position.x, -position.y);
            if (inward.sqrMagnitude < 0.01f)
            {
                inward = Vector2.down;
            }

            player.SetFacingDirection(inward);
        }
    }
}
