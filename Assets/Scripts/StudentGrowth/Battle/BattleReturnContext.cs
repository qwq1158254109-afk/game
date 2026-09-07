using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudentGrowth
{
    // Battles are modal overlays on their source scene, so returning never unloads that scene.
    public sealed class BattleReturnContext
    {
        public string LocationId { get; private set; }
        public string ScenePath { get; private set; }
        public string SafeAnchorId { get; private set; }
        public Vector2 Position { get; private set; }
        public Vector2 Facing { get; private set; }
        private FullscreenMapSceneController map;
        private Action returned;
        internal Func<bool> RestoreProbe;
        public static BattleReturnContext Capture(Action returned=null)
        {
            var map=UnityEngine.Object.FindAnyObjectByType<FullscreenMapSceneController>();
            var context=new BattleReturnContext {map=map,returned=returned,ScenePath=SceneManager.GetActiveScene().path,
                LocationId=map!=null?map.mapId:"stage_menu",SafeAnchorId=map!=null?map.defaultSpawnPointId:"stage_menu"};
            if(map!=null)
            {
                if(map.playerController==null||string.IsNullOrEmpty(map.mapId)||string.IsNullOrEmpty(map.defaultSpawnPointId))
                    throw new InvalidOperationException("地图缺少返回凭证。");
                context.Position=map.playerController.transform.position;context.Facing=map.playerController.FacingDirection;
            }
            else if(returned==null)throw new InvalidOperationException("没有可返回的来源地图。");
            return context;
        }
        public bool TryRestore()
        {
            if(RestoreProbe!=null&&!RestoreProbe())return false;
            if(SceneManager.GetActiveScene().path!=ScenePath)return false;
            if(map!=null)
            {
                if(map.mapId!=LocationId||map.playerController==null)return false;
                var position=Position;
                if(!IsSafe(position))
                {
                    PlayerSpawnPoint anchor=null;
                    foreach(var candidate in UnityEngine.Object.FindObjectsByType<PlayerSpawnPoint>())
                        if(candidate.spawnPointId==SafeAnchorId){anchor=candidate;break;}
                    if(anchor==null||!IsSafe(anchor.transform.position))return false;
                    position=anchor.transform.position;
                }
                map.playerController.TeleportTo(position);map.playerController.SetFacingDirection(Facing);map.RefreshHud();
            }
            else if(LocationId!="stage_menu")return false;
            returned?.Invoke();return true;
        }
        private bool IsSafe(Vector2 point)
        {
            if(float.IsNaN(point.x)||float.IsNaN(point.y)||float.IsInfinity(point.x)||float.IsInfinity(point.y))return false;
            foreach(var hit in Physics2D.OverlapCircleAll(point,.08f))
                if(!hit.isTrigger&&hit.transform!=map.playerController.transform&&!hit.transform.IsChildOf(map.playerController.transform)
                    &&hit!=map.cameraBounds)return false;
            return true;
        }
    }
}
