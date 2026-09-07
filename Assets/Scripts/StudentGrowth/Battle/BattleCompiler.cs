using System;
using System.Collections.Generic;
using System.Linq;

namespace StudentGrowth
{
    public sealed class BattlePreparation
    {
        public BattleDefinition Definition { get; internal set; }
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public bool Success => Definition != null && Errors.Count == 0;
    }

    public static class BattleCompiler
    {
        public static BattlePreparation Prepare(ChallengeData challenge, IEnumerable<CardData> cards, IEnumerable<BattleEffect> opening = null, IEnumerable<ItemData> passiveItems = null)
        {
            var result = new BattlePreparation();
            if (challenge == null) { result.Errors.Add("Challenge: missing reference"); return result; }
            string path = "Challenge[" + challenge.challengeId + "]";
            Check(!string.IsNullOrWhiteSpace(challenge.challengeId), path+".challengeId: required", result);
            Check(Enum.IsDefined(typeof(BattleRulesVersion), challenge.battleRulesVersion), path+".battleRulesVersion: unsupported", result);
            Check(challenge.targetProgress>0 && challenge.maxTurns>=1 && challenge.startingEnergy>=0 && challenge.startingHandSize>=0,
                path+".targetProgress/maxTurns/startingEnergy/startingHandSize: invalid range", result);
            bool unified = challenge.battleRulesVersion == BattleRulesVersion.Unified;
            Check(challenge.focusIntensityLimit>=1 && challenge.focusIntensityLimit<=10000,path+".focusIntensityLimit: expected 1..10000",result);
            Check(!unified || challenge.unifiedHandLimit>=1, path+".unifiedHandLimit: must be positive", result);
            var deck = new List<BattleCardDefinition>();
            var definitions = new Dictionary<string, CardData>();
            var compiled = new Dictionary<string, BattleCardDefinition>();
            if (cards == null) result.Errors.Add(path+".deck: missing reference");
            else foreach (var card in cards)
            {
                if (card == null) { result.Errors.Add(path+".deck: null card reference"); continue; }
                if (!string.IsNullOrWhiteSpace(card.cardId) && definitions.TryGetValue(card.cardId, out var previous))
                {
                    if (previous != card) result.Errors.Add("Card["+card.cardId+"].cardId: duplicate definition ID on different assets");
                    else deck.Add(compiled[card.cardId]);
                    continue;
                }
                var definition = CompileCard(card, result);
                deck.Add(definition);
                if (!string.IsNullOrWhiteSpace(card.cardId)) { definitions.Add(card.cardId, card); compiled.Add(card.cardId, definition); }
            }
            var intents = new List<BattleIntentDefinition>();
            BattleIntentDefinition fallback;
            if (!unified)
            {
                // Explicit compatibility policy: first matching damage, all matching extras in original order.
                foreach (int turn in (challenge.intents ?? new List<ChallengeIntentData>()).Where(i=>i!=null).Select(i=>i.triggerTurn).Distinct())
                {
                    var snapshot=LegacyIntentSnapshot.Create(challenge,turn);
                    var effects=new List<BattleEffect>{new BattleEffect(BattleEffectKind.Damage,snapshot.Damage)};
                    effects.AddRange(CompileEffects(snapshot.Effects, false, path+".intents[turn="+turn+"]", result));
                    intents.Add(new BattleIntentDefinition(challenge.challengeId+":turn:"+turn,snapshot.Title,turn,effects));
                }
                fallback=new BattleIntentDefinition("legacy_default_attack","攻击",0,new[]{new BattleEffect(BattleEffectKind.Damage,6)});
                if ((challenge.intents??new List<ChallengeIntentData>()).Where(i=>i!=null).GroupBy(i=>i.triggerTurn).Any(g=>g.Count()>1))
                    result.Warnings.Add(path+".intents: audited legacy first-damage/all-effects ordering retained");
            }
            else
            {
                Check(Enum.IsDefined(typeof(BattleIntentMode),challenge.intentMode),path+".intentMode: unsupported",result);
                foreach (var intent in challenge.intents??new List<ChallengeIntentData>())
                    if(intent==null)result.Errors.Add(path+".intents: null reference"); else intents.Add(CompileIntent(intent,path,result));
                fallback=challenge.fallbackIntent==null?null:CompileIntent(challenge.fallbackIntent,path+".fallbackIntent",result);
                if(challenge.intentMode==BattleIntentMode.Loop)Check(intents.Count>0,path+".intents: Loop cannot be empty",result);
                else
                {
                    Check(!intents.GroupBy(i=>i.Turn).Any(g=>g.Count()>1),path+".intents.triggerTurn: duplicate turn",result);
                    Check(fallback!=null,path+".fallbackIntent: explicit action or Wait required",result);
                }
                Check(!intents.GroupBy(i=>i.Id).Any(g=>g.Count()>1),path+".intents.intentId: duplicate ID",result);
            }
            var initialList=new List<BattleEffect>(opening??Enumerable.Empty<BattleEffect>());
            foreach(var item in passiveItems??Enumerable.Empty<ItemData>())
            {
                if(item==null){result.Errors.Add(path+".passiveItems: null reference");continue;}
                if(item.itemType!=ItemType.Passive||item.triggerTiming!=(challenge.isBoss?ItemTriggerTiming.OnBossStart:ItemTriggerTiming.OnBattleStart))continue;
                Check(!string.IsNullOrWhiteSpace(item.itemId),path+".passiveItems.itemId: required",result);
                foreach(var effect in CompileEffects(item.effects,false,"Item["+item.itemId+"]",result))
                    initialList.Add(new BattleEffect(effect.Kind,effect.Amount,effect.Stat,item.itemId,effect.DurationTurns));
            }
            var initial=initialList.ToArray();
            if(initial.Any(e=>e==null || !Enum.IsDefined(typeof(BattleEffectKind),e.Kind)))result.Errors.Add(path+".opening: unsupported effect");
            foreach(var effect in initial.Where(e=>e!=null))ValidateStatus(effect.Kind,effect.Amount,effect.DurationTurns,path+".opening",result);
            if(result.Errors.Count==0)result.Definition=new BattleDefinition(challenge.challengeId,challenge.displayName,challenge.isBoss,
                challenge.battleRulesVersion,unified?(int?)challenge.unifiedHandLimit:null,challenge.targetProgress,challenge.maxTurns,
                challenge.startingEnergy,challenge.startingHandSize,unified?challenge.intentMode:BattleIntentMode.ByTurn,deck,intents,fallback,initial,challenge.focusIntensityLimit);
            return result;
        }

        private static BattleCardDefinition CompileCard(CardData card, BattlePreparation result)
        {
            string path="Card["+card.cardId+"]";
            Check(!string.IsNullOrWhiteSpace(card.cardId),path+".cardId: required",result);
            Check(card.cost>=0,path+".cost: negative / X cost unsupported",result);
            Check(Enum.IsDefined(typeof(CardEffectSchemaVersion),card.effectSchemaVersion),path+".effectSchemaVersion: unsupported",result);
            bool unified=card.effectSchemaVersion==CardEffectSchemaVersion.Unified;
            var target=card.targetType;
            if(!unified && target==CardTargetType.None)
            {
                if(Has(card,CardEffectType.AddChallengeProgress)||Has(card,CardEffectType.AddSubjectProgress))target=CardTargetType.SingleEnemy;
                else if(card.cardType==CardType.Rest||Has(card,CardEffectType.GainEnergy)||Has(card,CardEffectType.ReducePressure))target=CardTargetType.Self;
            }
            Check(target==CardTargetType.None||target==CardTargetType.Self||target==CardTargetType.SingleEnemy,path+".targetType: unsupported",result);
            Check(card.discardCount==0,path+".discardCount: selection unsupported",result);
            if(!unified && (card.retain||card.ethereal))result.Errors.Add(path+".retain/ethereal: migrate explicitly to Unified; no audited legacy exception");
            var effects=new List<BattleEffect>();
            if(!unified)
            {
                int block=card.block>0?card.block:card.cardType==CardType.Rest||target==CardTargetType.Self?5:0;
                if(block>0)effects.Add(new BattleEffect(BattleEffectKind.Block,block));
                if(card.damage!=0 || card.drawCount!=0)result.Warnings.Add(path+".damage/drawCount: display-only legacy scalars ignored; effects are authoritative");
            }
            else if(card.damage!=0||card.block!=0||card.drawCount!=0||card.exhaustAfterUse)
                result.Warnings.Add(path+": Unified ignores legacy scalar fields and exhaustAfterUse");
            effects.AddRange(CompileEffects(card.effects,unified,path,result));
            return new BattleCardDefinition(card.cardId,card.displayName,card.cost,target,effects,
                card.exhaust||(!unified&&card.exhaustAfterUse),unified&&card.retain,unified&&card.ethereal,card.cardType==CardType.Study);
        }
        private static bool Has(CardData card,CardEffectType effect)=>card.effects!=null&&card.effects.Any(e=>e!=null&&e.effectType==effect);
        private static BattleIntentDefinition CompileIntent(ChallengeIntentData intent,string path,BattlePreparation result)
        {
            Check(!string.IsNullOrWhiteSpace(intent.intentId),path+".intentId: required",result);
            var effects=CompileEffects(intent.effects,true,path+".intent["+intent.intentId+"]",result);
            if(effects.Any(e=>e.Kind!=BattleEffectKind.Damage && e.Kind!=BattleEffectKind.Distraction && e.Kind!=BattleEffectKind.WeakDefense))
                result.Errors.Add(path+".intent: only explicit Damage / Distraction / WeakDefense / empty Wait supported");
            return new BattleIntentDefinition(intent.intentId,intent.displayName,intent.triggerTurn,effects);
        }
        public static IReadOnlyList<BattleEffect> CompileEffects(IEnumerable<CardEffectData> source,bool unified,string path,BattlePreparation result)
        {
            var effects=new List<BattleEffect>();
            if(source==null){result.Errors.Add(path+".effects: missing list");return effects.AsReadOnly();}
            int index=0;
            foreach(var e in source)
            {
                string field=path+".effects["+index+++"]";
                if(e==null){result.Errors.Add(field+": null effect");continue;}
                if(unified&&e.value<0){result.Errors.Add(field+".value: negative Unified amount");continue;}
                BattleEffectKind kind; int amount=e.value; PlayerStatKey stat=e.targetStat;
                switch(e.effectType)
                {
                    case CardEffectType.AddChallengeProgress:kind=BattleEffectKind.Progress;break;
                    case CardEffectType.DrawCards:kind=BattleEffectKind.Draw;break;
                    case CardEffectType.GainBlock:kind=BattleEffectKind.Block;break;
                    case CardEffectType.GainBattleEnergy:kind=BattleEffectKind.Energy;break;
                    case CardEffectType.DamageBattleHp:kind=BattleEffectKind.Damage;break;
                    case CardEffectType.GainFocus:kind=BattleEffectKind.Focus;break;
                    case CardEffectType.ApplyDistraction:kind=BattleEffectKind.Distraction;break;
                    case CardEffectType.ApplyWeakDefense:kind=BattleEffectKind.WeakDefense;break;
                    case CardEffectType.GainEnergy:kind=BattleEffectKind.LegacyStat;stat=PlayerStatKey.Energy;break;
                    case CardEffectType.LoseEnergy:kind=BattleEffectKind.LegacyStat;stat=PlayerStatKey.Energy;amount=-amount;break;
                    case CardEffectType.AddPressure:kind=BattleEffectKind.LegacyStat;stat=PlayerStatKey.Pressure;break;
                    case CardEffectType.ReducePressure:kind=BattleEffectKind.LegacyStat;stat=PlayerStatKey.Pressure;amount=-amount;break;
                    case CardEffectType.AddPlayerStat:
                    case CardEffectType.AddSubjectProgress:kind=BattleEffectKind.LegacyStat;break;
                    case CardEffectType.ReducePlayerStat:kind=BattleEffectKind.LegacyStat;amount=-amount;break;
                    default:result.Errors.Add(field+": "+e.effectType+" unsupported; migrate or remove explicitly");continue;
                }
                if(kind==BattleEffectKind.LegacyStat)
                {
                    Check(Enum.IsDefined(typeof(PlayerStatKey),stat),field+".targetStat: invalid",result);
                    result.Warnings.Add(field+": legacy long-term scope computes on isolated copy only");
                }
                ValidateStatus(kind,amount,e.durationTurns,field,result);
                effects.Add(new BattleEffect(kind,amount,stat,durationTurns:e.durationTurns));
            }
            return effects.AsReadOnly();
        }
        private static void Check(bool valid,string error,BattlePreparation result){if(!valid)result.Errors.Add(error);}
        private static void ValidateStatus(BattleEffectKind kind,int amount,int duration,string path,BattlePreparation result)
        {
            if(kind==BattleEffectKind.Focus)Check(amount>=1&&amount<=10000,path+".value: focus requires 1..10000",result);
            if(kind==BattleEffectKind.Distraction||kind==BattleEffectKind.WeakDefense)
            {
                Check(amount>=0&&amount<=10000,path+".value: status multiplier requires 0..10000 basis points",result);
                Check(duration>=1&&duration<=99,path+".durationTurns: expected 1..99",result);
            }
        }
    }
}
