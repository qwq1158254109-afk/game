using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudentGrowth
{
    public sealed class UnityGameSaveSource
    {
        private readonly SaveGameCodec codec;
        public double PlayedSeconds { get; set; }
        public UnityGameSaveSource(SaveGameCodec codec) { this.codec = codec; }
        public static GameSessionState CurrentSession()
        {
            var map = UnityEngine.Object.FindAnyObjectByType<FullscreenMapSceneController>();
            if (map != null && map.sessionState != null) return map.sessionState;
            var stage = UnityEngine.Object.FindAnyObjectByType<StageController>();
            return stage != null ? stage.sessionState : GameBootstrapper.SharedSession;
        }
        public SaveGameRecord Capture()
        {
            if(CurrentSession()?.BattleFlow.BlocksSave==true)throw new InvalidOperationException("SAVE_BATTLE_FLOW_BUSY");
            if (SceneTransitionManager.HasActiveTransition || AudioSettingsController.DisplayTransitionBlocked) throw new InvalidOperationException("SAVE_TRANSITION_BUSY");
            foreach (var challenge in UnityEngine.Object.FindObjectsByType<ChallengeController>(FindObjectsInactive.Exclude))
            {
                if (challenge.SandboxEngine != null) throw new InvalidOperationException("SAVE_BATTLE_SANDBOX_UNCOMMITTED");
                if (challenge.Result != null && (challenge.Result.Consumption == BattleResultConsumption.Faulted
                    || challenge.Result.Consumption == BattleResultConsumption.Applying))
                    throw new InvalidOperationException("SAVE_BATTLE_RESULT_INCOMPLETE");
                if (challenge.runtimeState?.data != null && !challenge.runtimeState.isComplete) throw new InvalidOperationException("SAVE_BATTLE_UNSUPPORTED");
            }
            var flow = UnityEngine.Object.FindAnyObjectByType<MapInteractionFlowController>();
            if (flow != null && (flow.CurrentState == MapInteractionFlowState.Executing || flow.CurrentState == MapInteractionFlowState.Confirming))
                throw new InvalidOperationException("SAVE_INTERACTION_BUSY");
            var player = UnityEngine.Object.FindAnyObjectByType<RpgPlayerController>();
            return codec.Capture(CurrentSession(),SceneManager.GetActiveScene().path,PlayedSeconds,
                player == null ? (Vector2?)null : (Vector2)player.transform.position,player == null ? Vector2.down : player.FacingDirection);
        }
        public byte[] CaptureThumbnail()
        {
            Camera camera = Camera.main;
            if (camera == null) return null;
            var previous = camera.targetTexture; var active = RenderTexture.active; var target = new RenderTexture(288,162,24);
            Texture2D pixels = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(288,162,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,288,162),0,0); pixels.Apply();
                return pixels.EncodeToPNG();
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                target.Release(); UnityEngine.Object.Destroy(target); if (pixels != null) UnityEngine.Object.Destroy(pixels);
            }
        }
    }
}
