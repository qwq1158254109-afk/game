using System;
using System.Collections.Generic;
using System.Text;

namespace StudentGrowth
{
    public sealed class BattleContentValidation
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public bool HasErrors => Errors.Count > 0;
        public void Merge(BattleContentValidation other) { Errors.AddRange(other.Errors); Warnings.AddRange(other.Warnings); }
        public override string ToString() { return string.Join("\n", HasErrors ? Errors : Warnings); }
    }

    public static class BattleContentValidator
    {
        public static BattleContentValidation Validate(ChallengeData challenge, IEnumerable<CardData> cards)
        {
            var result = new BattleContentValidation();
            if (challenge == null) { result.Errors.Add("ChallengeData：挑战配置缺失。"); return result; }
            string path = "Challenge[" + challenge.challengeId + "]";
            if (challenge.battleRulesVersion != BattleRulesVersion.Legacy)
                result.Errors.Add(path + ".battleRulesVersion：新规则仅在 P2 隔离入口运行，正式提交待 P3。");
            if (challenge.targetProgress <= 0) result.Errors.Add(path + ".targetProgress 必须大于 0。");
            if (challenge.maxTurns < 1) result.Errors.Add(path + ".maxTurns 必须至少为 1。");
            if (challenge.startingEnergy < 0 || challenge.startingHandSize < 0) result.Errors.Add(path + "：初始能量和抽牌不能为负数。");
            CheckId(challenge.challengeId, path, challenge.requireSupportedBattleContent, result);
            var turns = new HashSet<int>();
            if (challenge.intents != null) foreach (var intent in challenge.intents)
            {
                if (intent == null) { result.Warnings.Add(path + ".intents：空项沿用旧规则跳过。"); continue; }
                if (!turns.Add(intent.triggerTurn)) result.Warnings.Add(path + ".intents[turn=" + intent.triggerTurn + "]：旧规则以首项计算攻击，顺序执行全部附加效果。");
                CheckEffects(intent.effects, path + ".intent[" + intent.intentId + "]", challenge.requireSupportedBattleContent, result);
            }
            if (cards != null) foreach (var card in cards) result.Merge(ValidateCard(card, challenge.requireSupportedBattleContent));
            return result;
        }

        public static BattleContentValidation ValidateCard(CardData card, bool strict = false)
        {
            var result = new BattleContentValidation();
            if (card == null) { result.Errors.Add("CardData：牌组包含空卡牌引用。"); return result; }
            strict |= card.requireSupportedBattleContent;
            string path = "Card[" + card.cardId + "]";
            if (card.effectSchemaVersion != CardEffectSchemaVersion.Legacy)
                result.Errors.Add(path + ".effectSchemaVersion：新格式必须由 BattleCompiler 解释，不能进入旧执行路径。");
            CheckId(card.cardId, path, strict, result);
            if (!Enum.IsDefined(typeof(CardTargetType), card.targetType) || card.targetType == CardTargetType.AllEnemies || card.targetType == CardTargetType.RandomEnemy)
                result.Errors.Add(path + ".targetType：当前仅支持自己、单挑战目标、无目标。");
            if (card.cost < 0) AddUnsupported(result, path + ".cost：旧规则将负费用按 0 处理，新内容不允许。", strict);
            if (card.retain || card.ethereal || card.discardCount != 0)
                result.Errors.Add(path + "：retain/ethereal/discardCount 在 P1 未实现。");
            CheckEffects(card.effects, path, strict, result);
            return result;
        }

        private static void CheckEffects(IEnumerable<CardEffectData> effects, string path, bool strict, BattleContentValidation result)
        {
            if (effects == null) { result.Errors.Add(path + ".effects：列表缺失。"); return; }
            int index = 0;
            foreach (var effect in effects)
            {
                string field = path + ".effects[" + index++ + "]";
                if (effect == null) { result.Warnings.Add(field + "：空项跳过。"); continue; }
                if (!Enum.IsDefined(typeof(CardEffectType), effect.effectType)) result.Errors.Add(field + "：未知效果枚举。");
                else if ((int)effect.effectType >= (int)CardEffectType.AddTemporaryBuff)
                    result.Errors.Add(field + ": " + effect.effectType + " 尚未实现，不能启动。");
                if ((effect.effectType == CardEffectType.AddPlayerStat || effect.effectType == CardEffectType.ReducePlayerStat
                    || effect.effectType == CardEffectType.AddSubjectProgress) && !Enum.IsDefined(typeof(PlayerStatKey), effect.targetStat))
                    result.Errors.Add(field + ".targetStat：未知学生属性。");
            }
        }
        private static void CheckId(string id, string path, bool strict, BattleContentValidation result)
        { if (string.IsNullOrWhiteSpace(id)) AddUnsupported(result, path + "：缺少稳定 ID。", strict); }
        private static void AddUnsupported(BattleContentValidation result, string message, bool strict)
        { (strict ? result.Errors : result.Warnings).Add(message); }
    }
}
