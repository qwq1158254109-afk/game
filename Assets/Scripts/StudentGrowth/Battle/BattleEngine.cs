using System;
using System.Collections.Generic;
using System.Linq;

namespace StudentGrowth
{
    // Synchronous rules only. No scene lookup, UI, coroutine, clock or global random access.
    public sealed class BattleEngine
    {
        private readonly BattleDefinition definition;
        private readonly string sessionId, battleId;
        private readonly Dictionary<BattlePile,List<string>> piles=new Dictionary<BattlePile,List<string>>();
        private readonly Dictionary<string,BattleCardDefinition> definitions=new Dictionary<string,BattleCardDefinition>();
        private readonly Dictionary<string,int> costs=new Dictionary<string,int>();
        private readonly List<BattleEvent> events=new List<BattleEvent>();
        private readonly HashSet<string> handled=new HashSet<string>();
        private readonly List<BattleStatus> statuses=new List<BattleStatus>();
        private IBattleRandom random;
        private LegacyEffectState legacy;
        private BattlePhase phase;
        private BattleIntentDefinition intent;
        private int revision,turn=1,hp,maxHp,block,energy,progress;
        public EngineBattleResult Result { get; private set; }
        public string EnemyTargetId => "challenge:"+definition.Id;
        public const string SelfTargetId="player";

        public BattleEngine(BattleDefinition definition,PlayerState baseline,string runtimeSessionId,string battleId,IBattleRandom random)
        {
            if(definition==null||baseline==null||random==null||string.IsNullOrEmpty(runtimeSessionId)||string.IsNullOrEmpty(battleId))
                throw new ArgumentException("Prepared definition, baseline, identifiers and random source are required.");
            if(baseline.maxEnergy<1)throw new ArgumentException("playerMaxHp must be positive");
            this.definition=definition;sessionId=runtimeSessionId;this.battleId=battleId;this.random=random.Copy();
            legacy=new LegacyEffectState(baseline);maxHp=baseline.maxEnergy;hp=Math.Max(1,Math.Min(maxHp,baseline.energy));energy=definition.Energy;
            foreach(BattlePile pile in Enum.GetValues(typeof(BattlePile)))piles.Add(pile,new List<string>());
            int sequence=0;
            foreach(var card in definition.Deck)
            {
                string id="card:"+sequence++;definitions.Add(id,card);costs.Add(id,0);piles[BattlePile.Draw].Add(id);
            }
            phase=BattlePhase.Initializing;
            try
            {
                Shuffle();
                if(definition.Rules==BattleRulesVersion.Unified)Draw(definition.DrawCount,"initial");
                Apply(definition.OpeningEffects,"opening");
                if(!CheckTerminal()&&definition.Rules==BattleRulesVersion.Legacy)Draw(definition.DrawCount,"initial");
                if(!CheckTerminal()){SelectIntent();phase=BattlePhase.PlayerAction;}
                AssertPiles();
            }
            catch(Exception ex){Fault(ex);}
        }
        private BattleEngine(BattleEngine source)
        {
            definition=source.definition;sessionId=source.sessionId;battleId=source.battleId;random=source.random.Copy();legacy=source.legacy.Copy();
            phase=source.phase;intent=source.intent;revision=source.revision;turn=source.turn;hp=source.hp;maxHp=source.maxHp;
            block=source.block;energy=source.energy;progress=source.progress;Result=source.Result;
            foreach(var p in source.piles)piles.Add(p.Key,new List<string>(p.Value));
            foreach(var pair in source.definitions)definitions.Add(pair.Key,pair.Value);
            foreach(var pair in source.costs)costs.Add(pair.Key,pair.Value);
            events.AddRange(source.events);
            statuses.AddRange(source.statuses);
        }
        public BattleSnapshot Snapshot()
        {
            var cards=new List<CardInstance>();
            foreach(var pile in piles)foreach(string id in pile.Value)cards.Add(new CardInstance(id,definitions[id],pile.Key,costs[id]));
            return new BattleSnapshot(sessionId,battleId,definition,revision,phase,turn,hp,maxHp,block,energy,progress,random.Seed,
                random.Draws,cards,legacy.Snapshot(),intent,events,statuses);
        }
        private BattleFailure Validate(BattleRequest request)
        {
            if(request==null||request.RuntimeSessionId!=sessionId)return BattleFailure.WrongSession;
            if(request.BattleId!=battleId)return BattleFailure.WrongBattle;
            if(string.IsNullOrWhiteSpace(request.RequestId))return BattleFailure.ConfigurationError;
            if(handled.Contains(request.RequestId))return BattleFailure.AlreadyHandled;
            if(request.ExpectedRevision!=revision)return BattleFailure.StaleInput;
            if(phase==BattlePhase.ResolvingAction||phase==BattlePhase.EnemyAction)return BattleFailure.Busy;
            if(phase!=BattlePhase.PlayerAction)return BattleFailure.WrongPhase;
            return BattleFailure.None;
        }
        private BattleFailure ValidateCard(BattleRequest request)
        {
            var failure=Validate(request);if(failure!=BattleFailure.None)return failure;
            if(request.InstanceId==null||!piles[BattlePile.Hand].Contains(request.InstanceId))return BattleFailure.CardNotInHand;
            var card=definitions[request.InstanceId];
            if(Cost(request.InstanceId)>energy)return BattleFailure.InsufficientEnergy;
            string target=card.Target==CardTargetType.SingleEnemy?EnemyTargetId:card.Target==CardTargetType.Self?SelfTargetId:null;
            if(request.TargetId!=target)return BattleFailure.InvalidTarget;
            return BattleFailure.None;
        }
        public BattleActionReceipt TryPlayCard(BattleRequest request)
        {
            var error=ValidateCard(request);if(error!=BattleFailure.None)return Reject(error);
            return Execute(request,()=>
            {
                string id=request.InstanceId;var card=definitions[id];phase=BattlePhase.ResolvingAction;
                Change("Energy",energy,energy-Cost(id),id,v=>energy=v);MoveCard(id,BattlePile.Resolving,"PlayStarted");
                Apply(card.Effects,id,card);
                MoveCard(id,card.Exhaust?BattlePile.Exhaust:BattlePile.Discard,"PlayResolved");
            });
        }
        public BattleActionReceipt TryEndTurn(BattleRequest request)
        {
            var error=Validate(request);if(error!=BattleFailure.None)return Reject(error);
            return Execute(request,()=>
            {
                if(hp<=0||progress>=definition.Target)return;
                if(turn>=definition.MaxTurns){phase=BattlePhase.Finished;return;}
                phase=BattlePhase.PlayerTurnEnd;
                var endHand=piles[BattlePile.Hand].ToArray();
                foreach(string id in endHand)
                {
                    if(!piles[BattlePile.Hand].Contains(id))continue;
                    var card=definitions[id];
                    if(card.Ethereal)MoveCard(id,BattlePile.Exhaust,"Ethereal");
                    else if(!card.Retain)MoveCard(id,BattlePile.Discard,"EndTurnCleanup");
                }
                foreach(string id in costs.Keys.ToArray())costs[id]=0;
                ExpireStatuses();
                phase=BattlePhase.EnemyAction;Apply(intent.Effects,intent.Id);
                if(hp<=0||progress>=definition.Target)return;
                phase=BattlePhase.PlayerTurnStart;turn++;block=0;energy=definition.Energy;
                Draw(definition.DrawCount,"TurnStart");SelectIntent();
            });
        }
        public BattleActionReceipt TrySetTurnCostDelta(BattleRequest request,int delta)
        {
            var error=Validate(request);if(error!=BattleFailure.None)return Reject(error);
            if(request.InstanceId==null||!definitions.ContainsKey(request.InstanceId))return Reject(BattleFailure.CardNotInHand);
            return Execute(request,()=>{costs[request.InstanceId]=delta;});
        }
        private BattleActionReceipt Execute(BattleRequest request,Action action)
        {
            handled.Add(request.RequestId);
            try
            {
                action();AssertPiles();revision++;
                if(!CheckTerminal())
                {
                    if(phase==BattlePhase.Finished)Finish(BattleOutcome.Defeat,BattleEndReason.TurnLimit);
                    else phase=BattlePhase.PlayerAction;
                }
                return new BattleActionReceipt(true,BattleFailure.None,Snapshot(),Result);
            }
            catch(Exception ex)
            {
                foreach(string id in piles[BattlePile.Resolving].ToArray())MoveCard(id,BattlePile.Exhaust,"FaultCleanup");
                revision++;Fault(ex);return new BattleActionReceipt(true,BattleFailure.ExecutionFault,Snapshot(),Result);
            }
        }
        private BattleActionReceipt Reject(BattleFailure failure)=>new BattleActionReceipt(false,failure,Snapshot(),Result);
        private int Cost(string id)=>(int)Math.Max(0,Math.Min(int.MaxValue,(long)definitions[id].Cost+costs[id]));
        private void Apply(IEnumerable<BattleEffect> effects,string source,BattleCardDefinition card=null)
        {
            foreach(var effect in effects)
            {
                if(hp<=0)break;
                int amount=effect.Amount;
                string actor=effect.SourceId??source;
                switch(effect.Kind)
                {
                    case BattleEffectKind.Progress:
                        if(card!=null && card.IsStudy && amount>=0)
                            amount=Modified(amount,ActiveIntensity(BattleStatusKind.Focus,0),ActiveIntensity(BattleStatusKind.Distraction,10000));
                        int next=checked(progress+amount);next=Math.Max(0,next);
                        if(definition.Rules==BattleRulesVersion.Unified)next=Math.Min(definition.Target,next);
                        Change("Progress",progress,next,actor,v=>progress=v);break;
                    case BattleEffectKind.Block:
                        if(card!=null)amount=Modified(Math.Max(0,amount),0,ActiveIntensity(BattleStatusKind.WeakDefense,10000));
                        Change("Block",block,checked(block+Math.Max(0,amount)),actor,v=>block=v);break;
                    case BattleEffectKind.Focus:ApplyStatus(BattleStatusKind.Focus,effect,actor);break;
                    case BattleEffectKind.Distraction:ApplyStatus(BattleStatusKind.Distraction,effect,actor);break;
                    case BattleEffectKind.WeakDefense:ApplyStatus(BattleStatusKind.WeakDefense,effect,actor);break;
                    case BattleEffectKind.Energy:Change("Energy",energy,checked(energy+Math.Max(0,amount)),actor,v=>energy=v);break;
                    case BattleEffectKind.Draw:Draw(amount,actor);break;
                    case BattleEffectKind.Damage:
                        int hit=Math.Max(0,amount),shield=Math.Min(block,hit);
                        Change("Block",block,block-shield,actor,v=>block=v);
                        Change("Hp",hp,Math.Max(0,hp-(hit-shield)),actor,v=>hp=v);break;
                    case BattleEffectKind.LegacyStat:
                        var before=legacy.Snapshot();legacy.Add(effect.Stat,amount);var after=legacy.Snapshot();
                        foreach(var stat in before.Keys)if(before[stat]!=after[stat])events.Add(new BattleEvent("LegacyStat",actor,stat.ToString(),before[stat],after[stat]));
                        break;
                    default:throw new InvalidOperationException("Unsupported compiled effect");
                }
            }
        }
        private int ActiveIntensity(BattleStatusKind kind,int fallback)
            =>statuses.FirstOrDefault(s=>s.Kind==kind&&s.IsActive(turn))?.Intensity??fallback;
        private static int Modified(int amount,int addition,int basisPoints)
            =>checked((int)(((long)amount+addition)*basisPoints/10000));
        private void ApplyStatus(BattleStatusKind kind,BattleEffect effect,string source)
        {
            int from=phase==BattlePhase.PlayerTurnEnd||phase==BattlePhase.EnemyAction?checked(turn+1):turn;
            var previous=statuses.FirstOrDefault(s=>s.Kind==kind);
            int intensity=kind==BattleStatusKind.Focus
                ?(int)Math.Min(definition.FocusIntensityLimit,(long)(previous?.Intensity??0)+effect.Amount)
                :Math.Min(previous?.Intensity??10000,effect.Amount);
            int expires=kind==BattleStatusKind.Focus?int.MaxValue:Math.Max(previous?.ExpiresAfterPlayerTurn??0,checked(from+effect.DurationTurns-1));
            var next=new BattleStatus(kind,intensity,Math.Min(previous?.ActiveFromPlayerTurn??from,from),expires);
            if(previous!=null)statuses.Remove(previous);statuses.Add(next);
            string description=kind==BattleStatusKind.Focus?"专注 +"+(intensity-(previous?.Intensity??0))+"（合计 "+intensity+"）":BattlePresentationText.Status(next,turn);
            events.Add(new BattleEvent("Status",source,kind.ToString(),previous?.Intensity??0,intensity,description));
        }
        private void ExpireStatuses()
        {
            foreach(var status in statuses.Where(s=>!s.IsPersistent&&s.ExpiresAfterPlayerTurn<=turn).ToArray())
            {statuses.Remove(status);events.Add(new BattleEvent("StatusExpired","turn",status.Kind.ToString(),status.Intensity,0));}
        }
        private void Change(string field,int before,int after,string source,Action<int> write)
        {write(after);events.Add(new BattleEvent("Value",source,field,before,after));}
        private void Draw(int count,string source)
        {
            int drawn=0;
            for(int i=0;i<count;i++)
            {
                if(definition.HandLimit.HasValue&&piles[BattlePile.Hand].Count>=definition.HandLimit.Value)break;
                if(piles[BattlePile.Draw].Count==0)
                {
                    if(piles[BattlePile.Discard].Count==0)break;
                    foreach(string id in piles[BattlePile.Discard].ToArray())MoveCard(id,BattlePile.Draw,"Reshuffle");
                    Shuffle();
                }
                MoveCard(piles[BattlePile.Draw][0],BattlePile.Hand,"Draw");drawn++;
            }
            events.Add(new BattleEvent("Draw",source,"Cards",0,drawn));
        }
        private void MoveCard(string id,BattlePile destination,string reason)
        {
            var source=piles.Single(p=>p.Value.Contains(id));source.Value.Remove(id);piles[destination].Add(id);
            events.Add(new BattleEvent("Move",id,"Pile",(int)source.Key,(int)destination,reason));
        }
        private void Shuffle()
        {
            var draw=piles[BattlePile.Draw];
            for(int i=draw.Count-1;i>0;i--){int j=random.Next(i+1);string tmp=draw[i];draw[i]=draw[j];draw[j]=tmp;}
        }
        private void SelectIntent()
        {
            intent=definition.IntentMode==BattleIntentMode.Loop?definition.Intents[(turn-1)%definition.Intents.Count]
                :definition.Intents.FirstOrDefault(i=>i.Turn==turn)??definition.Fallback;
            if(intent==null)throw new InvalidOperationException("No prepared intent/fallback");
        }
        private bool CheckTerminal()
        {
            if(Result!=null)return true;
            if(hp<=0){Finish(BattleOutcome.Defeat,BattleEndReason.HpDepleted);return true;}
            if(progress>=definition.Target){Finish(BattleOutcome.Victory,BattleEndReason.TargetReached);return true;}
            return false;
        }
        private void Finish(BattleOutcome outcome,BattleEndReason reason)
        {phase=BattlePhase.Finished;statuses.Clear();Result=new EngineBattleResult(outcome,reason,Snapshot());}
        private void Fault(Exception exception)
        {phase=BattlePhase.Faulted;statuses.Clear();Result=new EngineBattleResult(BattleOutcome.Faulted,BattleEndReason.ExecutionFault,Snapshot(),exception.GetType().Name+": "+exception.Message);}
        private void AssertPiles()
        {
            var ids=piles.Values.SelectMany(p=>p).ToArray();
            if(ids.Length!=definitions.Count||ids.Distinct().Count()!=ids.Length||piles[BattlePile.Resolving].Count>1)
                throw new InvalidOperationException("Card conservation violated");
        }
        public BattleCardPreview GetCardPreview(string instanceId)
        {
            var state=Snapshot();var card=state.Cards.FirstOrDefault(c=>c.InstanceId==instanceId);
            if(card==null)return new BattleCardPreview(state,null,BattleFailure.CardNotInHand,0,0,"");
            string target=card.Definition.Target==CardTargetType.SingleEnemy?EnemyTargetId:card.Definition.Target==CardTargetType.Self?SelfTargetId:null;
            var request=new BattleRequest(sessionId,battleId,revision,"preview",instanceId,target);
            var clone=new BattleEngine(this);
            var validation=clone.ValidateCard(request);int change=0,blockChange=0;
            string summary=BattlePresentationText.Effects(card.Definition.Effects);
            if(validation==BattleFailure.None||validation==BattleFailure.InsufficientEnergy)
            {
                // Insufficient energy affects playability, not the state-derived effect preview.
                clone.energy=Math.Max(clone.energy,card.Cost);
                var receipt=clone.TryPlayCard(request);
                change=receipt.State.Progress-progress;blockChange=receipt.State.Block-block;
                if(receipt.Failure!=BattleFailure.None)validation=receipt.Failure;
                else summary=BattlePresentationText.ActualChanges(receipt.State.Events.Skip(state.Events.Count));
            }
            return new BattleCardPreview(state,card,validation,change,blockChange,summary);
        }
        public BattleSnapshot GetIntentPreview()
        {
            var clone=new BattleEngine(this);
            if(intent!=null&&Result==null&&phase==BattlePhase.PlayerAction&&turn<definition.MaxTurns)
            {clone.ExpireStatuses();clone.phase=BattlePhase.EnemyAction;clone.Apply(intent.Effects,intent.Id);}
            return clone.Snapshot();
        }
        public void Dispose(){statuses.Clear();phase=BattlePhase.Disposed;}
    }
}
