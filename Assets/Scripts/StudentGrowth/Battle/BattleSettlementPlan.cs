using System;
using System.Collections.Generic;
using System.Reflection;

namespace StudentGrowth
{
    public enum BattleRewardTiming { Participation, Victory, Failure, Stage, InheritActivity }
    public sealed class BattleSettlementPlan
    {
        internal BattleCheckpoint Final { get; }
        public IReadOnlyList<string> Rewards { get; }
        internal BattleSettlementPlan(BattleCheckpoint final,List<string> rewards)
        { Final=final;Rewards=rewards.AsReadOnly(); }
        internal static BattleSettlementPlan Prepare(GameSessionState source,EngineBattleResult result,
            ChallengeData challenge,ActivityData activity,bool stageAlreadyCleared,StageData nextStage)
        {
            var copy=BattleCheckpoint.Copy(source);
            // Set all final values together; SetStat would clamp against an intermediate cap.
            foreach(var f in typeof(PlayerState).GetFields(BindingFlags.Public|BindingFlags.Instance))
                if(Enum.TryParse(f.Name,true,out PlayerStatKey key))f.SetValue(copy.playerState,result.Final.LegacyStats[key]);
            var descriptions=new List<string>();var resolver=new RewardResolver();
            bool success=result.Outcome==BattleOutcome.Victory;
            bool boss=challenge.isBoss && copy.stageState.currentStage?.bossChallenge==challenge;
            void Apply(IEnumerable<RewardData> rewards)
            {
                if(rewards==null)return;
                foreach(var reward in rewards)
                { resolver.ApplyReward(reward,copy);if(reward!=null)descriptions.Add(MapInteractionPromptFormatter.BuildRewardPreview(new List<RewardData>{reward})); }
            }
            if(!boss||!stageAlreadyCleared)
            {
                if(activity!=null)
                {
                    foreach(var reward in activity.fixedRewards)
                    {
                        if(reward==null)continue;
                        var timing=reward.battleTiming==BattleRewardTiming.InheritActivity?activity.battleRewardTiming:reward.battleTiming;
                        if(timing==BattleRewardTiming.Participation || success&&timing==BattleRewardTiming.Victory
                            || !success&&timing==BattleRewardTiming.Failure || boss&&success&&timing==BattleRewardTiming.Stage)
                            Apply(new[]{reward});
                    }
                    string key="battle:"+activity.activityId+":"+(success?"victory":"defeat");
                    copy.locationState.actionUseCount.TryGetValue(key,out int attempts);copy.locationState.actionUseCount[key]=attempts+1;
                }
                Apply(challenge.isBoss?(success?challenge.bossClearRewards:challenge.bossFailureRewards)
                    :(success?challenge.successRewards:challenge.failureRewards));
                if(boss)
                {
                    copy.stageState.bossDefeated=success;
                    if(success)
                    {
                        var stage=copy.stageState.currentStage;
                        copy.stageState.clearedStageCount++;copy.stageState.defeatedBossCount++;
                        if(!copy.clearedStageIds.Contains(stage.stageId))copy.clearedStageIds.Add(stage.stageId);
                        if(!copy.defeatedBossIds.Contains(challenge.challengeId))copy.defeatedBossIds.Add(challenge.challengeId);
                        Apply(stage.clearStageRewards);
                        if(stage.isFinalStage)copy.endingReached=true;
                        else if(nextStage!=null)
                        {
                            copy.itemInventory.ClearStageOnlyItems();copy.stageState.currentStage=nextStage;
                            copy.stageState.remainingTime=Math.Max(0,nextStage.timeLimit);copy.stageState.bossUnlocked=false;copy.stageState.bossDefeated=false;
                            Apply(nextStage.enterStageRewards);
                        }
                        else copy.endingReached=true;
                    }
                    // A loss is retryable, including the final stage. It claims no completion key.
                }
            }
            copy.ClampAll();
            return new BattleSettlementPlan(new BattleCheckpoint(copy),descriptions);
        }
    }
}
