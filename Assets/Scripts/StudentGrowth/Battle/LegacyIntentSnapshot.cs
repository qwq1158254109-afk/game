using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    // Preserve legacy rules: damage comes from the first matching entry, while
    // all matching entries' additional effects execute in their configured order.
    public sealed class LegacyIntentSnapshot
    {
        public int Turn { get; private set; }
        public int Damage { get; private set; }
        public string Title { get; private set; }
        public IReadOnlyList<CardEffectData> Effects => effects.AsReadOnly();
        private readonly List<CardEffectData> effects = new List<CardEffectData>();

        public static LegacyIntentSnapshot Create(ChallengeData data, int turn)
        {
            var result = new LegacyIntentSnapshot { Turn = turn, Damage = 6, Title = "攻击" };
            bool first = true;
            if (data == null || data.intents == null) return result;
            foreach (var intent in data.intents)
            {
                if (intent == null || intent.triggerTurn != turn) continue;
                if (first)
                {
                    int damage = 0;
                    if (intent.effects != null) foreach (var e in intent.effects)
                        if (e != null && (e.effectType == CardEffectType.AddPressure || e.effectType == CardEffectType.LoseEnergy)) damage += Mathf.Abs(e.value);
                    result.Damage = damage > 0 ? damage : 6;
                    result.Title = string.IsNullOrEmpty(intent.displayName) ? "攻击" : intent.displayName;
                    first = false;
                }
                if (intent.effects != null) foreach (var e in intent.effects)
                    if (e != null) result.effects.Add(new CardEffectData { effectType = e.effectType, targetStat = e.targetStat, value = e.value, tag = e.tag, durationTurns = e.durationTurns });
            }
            return result;
        }
    }
}
