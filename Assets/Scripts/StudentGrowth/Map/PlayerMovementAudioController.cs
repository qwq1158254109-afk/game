using UnityEngine;

namespace StudentGrowth
{
    /// <summary>
    /// 脚步音：按移动状态定时播放脚素材（走路 / 跑步两档节奏）。
    /// 之前是循环播一个 Run.ogg，机械感很重；现在从 10 个脚步里随机挑 + 随机音高。
    /// 音量走「设置 → 音效」通道（SfxPlayer 池内已挂 AudioChannelSource）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RpgPlayerController))]
    public sealed class PlayerMovementAudioController : MonoBehaviour
    {
        [SerializeField] private RpgPlayerController playerController;
        [SerializeField, Range(0.05f, 1f)] private float stepIntervalWalk = 0.42f;
        [SerializeField, Range(0.05f, 1f)] private float stepIntervalRun = 0.28f;

        private float stepTimer;

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = GetComponent<RpgPlayerController>();
            }
        }

        private void Update()
        {
            if (playerController == null || !playerController.canMove || !playerController.IsMoving)
            {
                stepTimer = 0f;
                return;
            }

            bool running = playerController.IsRunning;
            float interval = running ? stepIntervalRun : stepIntervalWalk;
            stepTimer -= Time.deltaTime;
            if (stepTimer > 0f)
            {
                return;
            }

            Sfx.World.Step(running);
            stepTimer = interval;
        }
    }
}
