using System;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    // Runtime integration on the existing home desk; original planning action stays available.
    public sealed class BattleMapBridge : MonoBehaviour
    {
        private FullscreenMapSceneController map;
        private Button bossButton;
        private static ActivityData deskActivity;
        private static ActivityData strategyActivity;
        public static ActivityData GetStrategyActivity(MapInteractionTarget target)
        {
            if(target==null||target.interactionId!="activity_home_desk_plan")return null;
            var challenge=Resources.Load<BattleChallengeAsset>("StudentGrowth/Battle/P4/challenge_strategy_practice");
            if(challenge==null)return null;
            if(strategyActivity==null)
            {
                strategyActivity=ScriptableObject.CreateInstance<ActivityData>();
                strategyActivity.activityId="activity_home_strategy_practice";strategyActivity.displayName="策略练习";
                strategyActivity.activityType=ActivityType.CardBattle;strategyActivity.timeCost=1;
                strategyActivity.description="借用练习牌组，组合专注、推进、格挡和行动能量。消耗 1 时间，胜利数学 +2；练习牌不带出战斗。";
                strategyActivity.linkedChallenge=challenge;
            }
            return strategyActivity;
        }
        public void Configure(FullscreenMapSceneController source){map=source;}
        public static ActivityData GetDeskActivity(MapInteractionTarget target)
        {
            if(target==null || target.interactionId!="activity_home_desk_plan")return null;
            if(deskActivity==null)
            {
                deskActivity=ScriptableObject.CreateInstance<ActivityData>();deskActivity.activityId="activity_home_desk_challenge";
                deskActivity.displayName="书桌习题挑战";deskActivity.activityType=ActivityType.CardBattle;deskActivity.timeCost=1;
                deskActivity.description="用卡牌完成一组习题。消耗 1 时间，胜利获得数学成长。";
                var challenge=ScriptableObject.CreateInstance<BattleChallengeAsset>();challenge.challengeId="challenge_home_exercises";
                challenge.displayName="习题难题";challenge.targetProgress=24;challenge.maxTurns=3;challenge.startingEnergy=3;challenge.startingHandSize=5;
                challenge.successRewards.Add(new RewardData{rewardType=RewardType.AddPlayerStat,targetStat=PlayerStatKey.Math,value=2});
                deskActivity.linkedChallenge=challenge;
            }
            return deskActivity;
        }
        private void Update()
        {
            if(map==null)return;
            bool visible=map.stageController!=null && map.stageController.ShouldStartBoss()&&!map.sessionState.BattleFlow.BlocksSave;
            if(bossButton==null && visible)
            {
                var root=new GameObject("MapBattleEntry",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
                root.transform.SetParent(transform,false);var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=35;
                var rect=new GameObject("StageBossEntry",typeof(RectTransform),typeof(Image),typeof(Button)).GetComponent<RectTransform>();rect.SetParent(root.transform,false);
                rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one;rect.anchoredPosition=new Vector2(-24,-150);rect.sizeDelta=new Vector2(240,60);
                bossButton=rect.GetComponent<Button>();bossButton.targetGraphic=rect.GetComponent<Image>();
                BattleArtStyler.Button(bossButton,Resources.Load<BattleUIConfig>(BattleUIConfig.ResourcePath));
                var label=new GameObject("Label",typeof(RectTransform),typeof(Text)).GetComponent<Text>();label.transform.SetParent(rect,false);
                label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.sizeDelta=Vector2.zero;
                label.text="进入阶段挑战";label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;GameUiStyle.ApplyTextFromFontSpec(label,null,22,FontStyle.Bold);label.color=Color.white;
                bossButton.onClick.AddListener(StartBoss);
            }
            if(bossButton!=null)bossButton.gameObject.SetActive(visible);
        }
        public void StartBoss()
        {
            if(map==null||!map.stageController.ShouldStartBoss())return;
            if(!map.sessionState.BattleFlow.Start(Guid.NewGuid().ToString("N"),map.sessionState.stageState.currentStage.bossChallenge,
                null,map.stageController,null,map.challengeController,BattleReturnContext.Capture(),(uint)Environment.TickCount,out var error))
                map.promptView?.ShowMessage(error);
        }
    }
}
