using System;
using System.Collections.Generic;
using System.Linq;

namespace StudentGrowth
{
    public sealed class CardInstance
    {
        public string InstanceId { get; }
        public string DefinitionId => Definition.Id;
        public BattleCardDefinition Definition { get; }
        public BattlePile Pile { get; }
        public int TurnCostDelta { get; }
        public int Cost => (int)Math.Max(0, Math.Min(int.MaxValue, (long)Definition.Cost+TurnCostDelta));
        internal CardInstance(string id,BattleCardDefinition definition,BattlePile pile,int delta)
        {InstanceId=id;Definition=definition;Pile=pile;TurnCostDelta=delta;}
    }
    public sealed class BattleEvent
    {
        public string Kind { get; }
        public string Source { get; }
        public string Field { get; }
        public int Before { get; }
        public int After { get; }
        public string Reason { get; }
        internal BattleEvent(string kind,string source,string field,int before,int after,string reason="")
        {Kind=kind;Source=source;Field=field;Before=before;After=after;Reason=reason;}
    }
    public sealed class BattleSnapshot
    {
        public string RuntimeSessionId { get; }
        public string BattleId { get; }
        public BattleDefinition Definition { get; }
        public int Revision { get; }
        public BattlePhase Phase { get; }
        public int Turn { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public int Block { get; }
        public int Energy { get; }
        public int Progress { get; }
        public int RandomDraws { get; }
        public uint Seed { get; }
        public IReadOnlyList<CardInstance> Cards { get; }
        public IReadOnlyDictionary<PlayerStatKey,int> LegacyStats { get; }
        public BattleIntentDefinition Intent { get; }
        public IReadOnlyList<BattleEvent> Events { get; }
        public IReadOnlyList<BattleStatus> Statuses { get; }
        public IReadOnlyList<CardInstance> InPile(BattlePile pile)=>Array.AsReadOnly(Cards.Where(c=>c.Pile==pile).ToArray());
        internal BattleSnapshot(string session,string battle,BattleDefinition definition,int revision,BattlePhase phase,int turn,int hp,int maxHp,
            int block,int energy,int progress,uint seed,int randomDraws,IEnumerable<CardInstance> cards,IReadOnlyDictionary<PlayerStatKey,int> legacy,
            BattleIntentDefinition intent,IEnumerable<BattleEvent> events,IEnumerable<BattleStatus> statuses)
        {
            RuntimeSessionId=session;BattleId=battle;Definition=definition;Revision=revision;Phase=phase;Turn=turn;Hp=hp;MaxHp=maxHp;
            Block=block;Energy=energy;Progress=progress;Seed=seed;RandomDraws=randomDraws;Cards=Array.AsReadOnly(cards.ToArray());
            LegacyStats=legacy;Intent=intent;Events=Array.AsReadOnly(events.ToArray());Statuses=Array.AsReadOnly(statuses.ToArray());
        }
    }
    public sealed class EngineBattleResult
    {
        public BattleOutcome Outcome { get; }
        public BattleEndReason Reason { get; }
        public BattleSnapshot Final { get; }
        public string FailureDetail { get; }
        public string ConfigurationVersion => BattleDefinition.CompilerVersion;
        public string SourceKind => Final.Definition.IsBoss ? "StageBoss" : "NormalChallenge";
        internal EngineBattleResult(BattleOutcome outcome,BattleEndReason reason,BattleSnapshot final,string failure="")
        {Outcome=outcome;Reason=reason;Final=final;FailureDetail=failure;}
    }
    public sealed class BattleRequest
    {
        public string RuntimeSessionId { get; }
        public string BattleId { get; }
        public int ExpectedRevision { get; }
        public string RequestId { get; }
        public string InstanceId { get; }
        public string TargetId { get; }
        public BattleRequest(string session,string battle,int revision,string request,string instance=null,string target=null)
        {RuntimeSessionId=session;BattleId=battle;ExpectedRevision=revision;RequestId=request;InstanceId=instance;TargetId=target;}
    }
    public sealed class BattleActionReceipt
    {
        public bool Accepted { get; }
        public BattleFailure Failure { get; }
        public BattleSnapshot State { get; }
        public EngineBattleResult Result { get; }
        internal BattleActionReceipt(bool accepted,BattleFailure failure,BattleSnapshot state,EngineBattleResult result)
        {Accepted=accepted;Failure=failure;State=state;Result=result;}
    }
    public sealed class BattleCardPreview
    {
        public BattleSnapshot State { get; }
        public CardInstance Card { get; }
        public BattleFailure Failure { get; }
        public bool Playable => Failure==BattleFailure.None;
        public int Cost => Card?.Cost??0;
        public int ProgressChange { get; }
        public int BlockChange { get; }
        public string Summary { get; }
        internal BattleCardPreview(BattleSnapshot state,CardInstance card,BattleFailure failure,int progress,int block,string summary)
        {State=state;Card=card;Failure=failure;ProgressChange=progress;BlockChange=block;Summary=summary;}
    }
}
