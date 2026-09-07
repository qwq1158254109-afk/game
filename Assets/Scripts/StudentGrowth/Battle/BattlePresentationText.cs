using System.Collections.Generic;
using System.Linq;

namespace StudentGrowth
{
    // Display formatting stays outside rules; both views consume the compiled effect sequence.
    public static class BattlePresentationText
    {
        public static string ActualChanges(IEnumerable<BattleEvent> changes)
        {
            var parts=new List<string>();
            foreach(var e in changes)
            {
                if(e.Kind=="Status"){parts.Add(e.Reason);continue;}
                if(e.Kind=="Draw"){parts.Add("抽牌 "+e.After);continue;}
                if((e.Kind!="Value"&&e.Kind!="LegacyStat")||e.After==e.Before)continue;
                // Energy payment is not a card effect; the cost has its own label.
                if(e.Kind=="Value"&&e.Field=="Energy"&&e.After<e.Before)continue;
                string field=e.Field=="Progress"?"进度":e.Field=="Block"?"格挡":e.Field=="Hp"?"耐力":e.Field=="Energy"?
                    (e.Kind=="LegacyStat"?"长期体力":"行动能量"):e.Field=="MaxEnergy"?"体力上限":e.Field=="Pressure"?"压力":e.Field;
                parts.Add(field+" "+Signed(e.After-e.Before));
            }
            return parts.Count==0?"本次无数值变化":string.Join("；",parts);
        }
        public static string Effects(IEnumerable<BattleEffect> effects)=>string.Join("；",effects.Select(Effect));
        private static string Effect(BattleEffect e)
        {
            switch(e.Kind)
            {
                case BattleEffectKind.Progress:return "进度 "+Signed(e.Amount);
                case BattleEffectKind.Block:return "格挡 +"+e.Amount;
                case BattleEffectKind.Draw:return "抽牌 "+e.Amount;
                case BattleEffectKind.Energy:return "行动能量 +"+e.Amount;
                case BattleEffectKind.Damage:return "耐力伤害 "+e.Amount;
                case BattleEffectKind.Focus:return "专注 +"+e.Amount+"（本场）";
                case BattleEffectKind.Distraction:return "分心：学习推进 ×"+Percent(e.Amount)+"，"+e.DurationTurns+" 回合";
                case BattleEffectKind.WeakDefense:return "脆弱防线：卡牌格挡 ×"+Percent(e.Amount)+"，"+e.DurationTurns+" 回合";
                case BattleEffectKind.LegacyStat:return MapInteractionPromptFormatter.FormatStatName(e.Stat)+" "+Signed(e.Amount);
                default:return "不支持";
            }
        }
        private static string Signed(int value)=>value>=0?"+"+value:value.ToString();
        private static string Percent(int points)=>(points/100m).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+"%";
        public static string StatusName(BattleStatusKind kind)=>kind==BattleStatusKind.Focus?"专注":kind==BattleStatusKind.Distraction?"分心":"脆弱防线";
        public static string Status(BattleStatus status,int turn)
        {
            if(status.IsPersistent)return "专注 +"+status.Intensity+" · 本场";
            return StatusName(status.Kind)+" ×"+Percent(status.Intensity)+" · "+status.RemainingTurns(turn)+" 回合"
                +(turn<status.ActiveFromPlayerTurn?"（下回合起）":"");
        }
        public static string StatusDetail(BattleStatus status,int turn)
            =>Status(status,turn)+"\n"+(status.IsPersistent?"每个学习卡推进效果先加专注，再应用分心倍率，最后向下取整。战斗结束移除。"
                :(status.Kind==BattleStatusKind.Distraction?"只修正学习卡的非负推进效果。":"只修正卡牌获得的格挡。")
                +"同名取较强削弱，并延长至较晚到期回合。\n有效玩家回合："+status.ActiveFromPlayerTurn+"—"+status.ExpiresAfterPlayerTurn+"；末尾移除。");
        public static string Failure(BattleFailure failure)
        {
            switch(failure)
            {
                case BattleFailure.None:return "";
                case BattleFailure.InsufficientEnergy:return "行动能量不足";
                case BattleFailure.InvalidTarget:return "请拖到正确目标";
                case BattleFailure.StaleInput:return "手牌状态已更新，请重新操作";
                case BattleFailure.AlreadyHandled:return "该操作已经处理";
                case BattleFailure.WrongSession:
                case BattleFailure.WrongBattle:return "战斗会话已失效";
                case BattleFailure.ExecutionFault:return "规则执行异常，已停止战斗";
                case BattleFailure.CardNotInHand:return "这张牌已不在手牌中";
                default:return "当前无法操作："+failure;
            }
        }
    }
}
