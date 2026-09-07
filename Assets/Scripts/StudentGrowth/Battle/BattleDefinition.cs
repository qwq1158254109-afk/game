using System;
using System.Collections.Generic;
using System.Linq;

namespace StudentGrowth
{
    public enum BattleRulesVersion { Legacy = 0, Unified = 1 }
    public enum CardEffectSchemaVersion { Legacy = 0, Unified = 1 }
    public enum BattleIntentMode { ByTurn = 0, Loop = 1 }
    public enum BattlePhase { Initializing, PlayerTurnStart, PlayerAction, ResolvingAction, PlayerTurnEnd, EnemyAction, Finished, Faulted, Disposed }
    public enum BattlePile { Draw, Hand, Resolving, Discard, Exhaust }
    public enum BattleFailure { None, WrongSession, WrongBattle, WrongPhase, Busy, StaleInput, AlreadyHandled, CardNotInHand, InsufficientEnergy, InvalidTarget, UnsupportedContent, ConfigurationError, ExecutionFault }
    public enum BattleOutcome { Victory, Defeat, Faulted }
    public enum BattleEndReason { TargetReached, HpDepleted, TurnLimit, ExecutionFault }
    public enum BattleEffectKind { Progress, Block, Draw, Energy, Damage, LegacyStat, Focus, Distraction, WeakDefense }

    public sealed class BattleEffect
    {
        public BattleEffectKind Kind { get; }
        public int Amount { get; }
        public PlayerStatKey Stat { get; }
        public string SourceId { get; }
        public int DurationTurns { get; }
        public BattleEffect(BattleEffectKind kind, int amount, PlayerStatKey stat = PlayerStatKey.Energy, string sourceId = null, int durationTurns = 1)
        { Kind = kind; Amount = amount; Stat = stat; SourceId = sourceId; DurationTurns = durationTurns; }
    }
    public sealed class BattleCardDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public int Cost { get; }
        public CardTargetType Target { get; }
        public bool Exhaust { get; }
        public bool Retain { get; }
        public bool Ethereal { get; }
        public bool IsStudy { get; }
        public IReadOnlyList<BattleEffect> Effects { get; }
        public BattleCardDefinition(string id, string name, int cost, CardTargetType target, IEnumerable<BattleEffect> effects,
            bool exhaust = false, bool retain = false, bool ethereal = false, bool isStudy = false)
        {
            Id = id; Name = name; Cost = cost; Target = target; Exhaust = exhaust; Retain = retain; Ethereal = ethereal; IsStudy = isStudy;
            Effects = Array.AsReadOnly(effects.ToArray());
        }
    }
    public sealed class BattleIntentDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public int Turn { get; }
        public IReadOnlyList<BattleEffect> Effects { get; }
        public BattleIntentDefinition(string id, string name, int turn, IEnumerable<BattleEffect> effects)
        { Id = id; Name = name; Turn = turn; Effects = Array.AsReadOnly(effects.ToArray()); }
    }
    public sealed class BattleDefinition
    {
        public const string CompilerVersion = "p4.1";
        public string Id { get; }
        public string Name { get; }
        public bool IsBoss { get; }
        public BattleRulesVersion Rules { get; }
        public int? HandLimit { get; }
        public int Target { get; }
        public int MaxTurns { get; }
        public int Energy { get; }
        public int DrawCount { get; }
        public int FocusIntensityLimit { get; }
        public BattleIntentMode IntentMode { get; }
        public IReadOnlyList<BattleCardDefinition> Deck { get; }
        public IReadOnlyList<BattleIntentDefinition> Intents { get; }
        public BattleIntentDefinition Fallback { get; }
        public IReadOnlyList<BattleEffect> OpeningEffects { get; }
        public BattleDefinition(string id, string name, bool boss, BattleRulesVersion rules, int? handLimit, int target,
            int maxTurns, int energy, int draw, BattleIntentMode mode, IEnumerable<BattleCardDefinition> deck,
            IEnumerable<BattleIntentDefinition> intents, BattleIntentDefinition fallback, IEnumerable<BattleEffect> opening, int focusIntensityLimit = 10)
        {
            Id=id; Name=name; IsBoss=boss; Rules=rules; HandLimit=handLimit; Target=target; MaxTurns=maxTurns; Energy=energy;
            DrawCount=draw; FocusIntensityLimit=focusIntensityLimit; IntentMode=mode; Deck=Array.AsReadOnly(deck.ToArray()); Intents=Array.AsReadOnly(intents.ToArray());
            Fallback=fallback; OpeningEffects=Array.AsReadOnly(opening.ToArray());
        }
    }
}
