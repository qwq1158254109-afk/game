using System;
using UnityEngine;

namespace StudentGrowth
{
    public partial class ChallengeController : MonoBehaviour
    {
        private const int DefaultEnemyIntentDamage = 6;
        private const int DefaultSelfBlock = 5;

        public GameSessionState sessionState = new GameSessionState();
        public ChallengeRuntimeState runtimeState = new ChallengeRuntimeState();
        public DeckController deckController;

        private readonly CardEffectResolver cardEffectResolver = new CardEffectResolver();
        private readonly ItemEffectResolver itemEffectResolver = new ItemEffectResolver();
        public BattleResultRecord Result { get; private set; }
        public LegacyIntentSnapshot Intent { get; private set; }
        public string LastStartFailure { get; private set; }

        public void SetSessionState(GameSessionState state)
        {
            sessionState = state ?? new GameSessionState();

            if (deckController != null)
            {
                deckController.SetDeckState(sessionState.deckState);
            }
        }

        public void StartChallenge(ChallengeData challengeData)
        {
            if (SandboxEngine != null) throw new InvalidOperationException("Dispose the isolated P2 battle before using the legacy entry.");
            if (challengeData == null)
            {
                throw new ArgumentNullException("challengeData");
            }

            EnsureDeckController();
            var validation = BattleContentValidator.Validate(challengeData, sessionState.deckState.masterDeck);
            if (validation.HasErrors) throw new InvalidOperationException(validation.ToString());
            deckController.SetDeckState(sessionState.deckState);
            deckController.InitializeBattleDeck(sessionState.deckState.masterDeck);

            runtimeState = new ChallengeRuntimeState
            {
                data = challengeData,
                currentProgress = 0,
                currentTurn = 1,
                currentEnergy = challengeData.startingEnergy,
                isComplete = false,
                isSuccess = false,
                phase = CombatPhase.CombatStart,
                inputLocked = false,
                playerMaxHp = Mathf.Max(1, sessionState.playerState.maxEnergy),
                playerHp = Mathf.Clamp(sessionState.playerState.energy, 1, Mathf.Max(1, sessionState.playerState.maxEnergy)),
                playerBlock = 0
            };
            Result = sessionState.BattleResults.Begin(challengeData);

            itemEffectResolver.ApplyPassiveItems(
                challengeData.isBoss ? ItemTriggerTiming.OnBossStart : ItemTriggerTiming.OnBattleStart,
                sessionState,
                runtimeState,
                deckController);
            EvaluateCompletion();
            if (runtimeState.isComplete) return;
            runtimeState.phase = CombatPhase.PlayerTurnStart;
            deckController.DrawCards(challengeData.startingHandSize);
            RefreshEnemyIntent();
            runtimeState.phase = CombatPhase.PlayerTurn;
        }

        public bool TryStartChallenge(ChallengeData data, out string reason)
        {
            if (SandboxEngine != null) { reason = "请先关闭 P2 隔离战斗"; return false; }
            var validation = BattleContentValidator.Validate(data, sessionState.deckState.masterDeck);
            if (validation.HasErrors) { reason = LastStartFailure = validation.ToString(); return false; }
            LastStartFailure = null;
            StartChallenge(data);
            reason = null;
            return true;
        }

        public bool CanPlayCard(CardData card)
        {
            return CanPlayCardWithoutTarget(card, out _);
        }

        public bool CanPlayCard(CardData card, bool hasEnemyTarget, out string reason)
        {
            if (!CanPlayCardWithoutTarget(card, out reason))
            {
                return false;
            }

            CardTargetType targetType = GetEffectiveTargetType(card);
            if (targetType == CardTargetType.SingleEnemy && !hasEnemyTarget)
            {
                reason = "目标无效";
                return false;
            }

            reason = null;
            return true;
        }

        public void PlayCard(CardData card)
        {
            TryPlayCard(card, true, out _);
        }

        public bool TryPlayCard(CardData card, bool hasEnemyTarget, out string reason)
        {
            if (!CanPlayCard(card, hasEnemyTarget, out reason))
            {
                return false;
            }

            runtimeState.inputLocked = true;
            runtimeState.phase = CombatPhase.CardResolving;
            runtimeState.currentEnergy -= Mathf.Max(0, card.cost);
            ApplyBlockFromCard(card);
            cardEffectResolver.ApplyCard(card, sessionState, runtimeState, deckController);
            deckController.PlayCard(card);
            EvaluateCompletion();

            runtimeState.inputLocked = false;
            if (!runtimeState.isComplete)
            {
                runtimeState.phase = CombatPhase.PlayerTurn;
            }

            return true;
        }

        public void PlayCard(CardData card, bool hasEnemyTarget)
        {
            TryPlayCard(card, hasEnemyTarget, out _);
        }

        private bool CanPlayCardWithoutTarget(CardData card, out string reason)
        {
            if (SandboxEngine != null) { reason = "P2 战斗必须使用实例 ID"; return false; }
            if (!OwnsCurrentBattle())
            {
                reason = "战斗会话已经失效";
                return false;
            }
            if (runtimeState == null || runtimeState.isComplete)
            {
                reason = "战斗已经结束";
                return false;
            }

            if (runtimeState.inputLocked || (runtimeState.phase != CombatPhase.PlayerTurn && runtimeState.phase != CombatPhase.CardSelecting))
            {
                reason = "当前不是玩家回合";
                return false;
            }

            if (card == null)
            {
                reason = "卡牌数据无效";
                return false;
            }

            var validation = BattleContentValidator.ValidateCard(card);
            if (validation.HasErrors) { reason = validation.ToString(); return false; }

            if (deckController == null || !deckController.deckState.hand.Contains(card))
            {
                reason = "卡牌不在手牌中";
                return false;
            }

            if (runtimeState.currentEnergy < Mathf.Max(0, card.cost))
            {
                reason = "能量不足";
                return false;
            }

            reason = null;
            return true;
        }

        public CardTargetType GetEffectiveTargetType(CardData card)
        {
            if (card == null)
            {
                return CardTargetType.None;
            }

            if (card.targetType != CardTargetType.None)
            {
                return card.targetType;
            }

            if (HasEffect(card, CardEffectType.AddChallengeProgress) || HasEffect(card, CardEffectType.AddSubjectProgress))
            {
                return CardTargetType.SingleEnemy;
            }

            if (card.cardType == CardType.Rest
                || HasEffect(card, CardEffectType.GainEnergy)
                || HasEffect(card, CardEffectType.ReducePressure))
            {
                return CardTargetType.Self;
            }

            return CardTargetType.None;
        }

        public void EndTurn()
        {
            if (SandboxEngine != null) return;
            if (!OwnsCurrentBattle() || runtimeState == null || runtimeState.isComplete || runtimeState.inputLocked)
            {
                return;
            }

            EvaluateCompletion();
            if (runtimeState.isComplete)
            {
                return;
            }

            if (runtimeState.currentTurn >= runtimeState.data.maxTurns)
            {
                CompleteChallenge(false);
                return;
            }

            runtimeState.inputLocked = true;
            runtimeState.phase = CombatPhase.PlayerTurnEnd;
            deckController.DiscardHand();
            runtimeState.phase = CombatPhase.EnemyTurnStart;
            runtimeState.phase = CombatPhase.EnemyTurn;
            ResolveEnemyTurn();
            if (runtimeState.isComplete)
            {
                runtimeState.inputLocked = false;
                return;
            }

            runtimeState.phase = CombatPhase.EnemyTurnEnd;
            runtimeState.currentTurn++;
            runtimeState.phase = CombatPhase.PlayerTurnStart;
            runtimeState.playerBlock = 0;
            runtimeState.currentEnergy = runtimeState.data.startingEnergy;
            deckController.DrawCards(runtimeState.data.startingHandSize);
            RefreshEnemyIntent();
            EvaluateCompletion();
            runtimeState.inputLocked = false;
            if (!runtimeState.isComplete)
            {
                runtimeState.phase = CombatPhase.PlayerTurn;
            }
        }

        public void ApplyTurnIntent()
        {
            if (SandboxEngine != null) return;
            if (!OwnsCurrentBattle() || runtimeState == null || runtimeState.isComplete) return;
            if (Intent == null || Intent.Turn != runtimeState.currentTurn) RefreshEnemyIntent();
            foreach (var effect in Intent.Effects)
                cardEffectResolver.ApplyEffect(effect, sessionState, runtimeState, deckController);
        }

        private bool OwnsCurrentBattle()
        {
            return Result != null && Result == sessionState.BattleResults.Current
                && Result.RuntimeSessionId == GameApplicationRuntime.CurrentRuntimeSessionId;
        }

        public void CompleteChallenge(bool success)
        {
            if (SandboxEngine != null) return;
            if (runtimeState == null || runtimeState.data == null || runtimeState.isComplete) return;
            runtimeState.isComplete = true;
            runtimeState.isSuccess = success;
            runtimeState.phase = success ? CombatPhase.Victory : CombatPhase.Defeat;
            runtimeState.inputLocked = true;
            if (Result != null)
            {
                Result.FinalProgress = runtimeState.currentProgress;
                Result.TargetProgress = runtimeState.data.targetProgress;
                Result.TurnsUsed = runtimeState.currentTurn;
                Result.OutcomeReason = success ? "挑战通过" : runtimeState.playerHp <= 0 ? "耐力耗尽" : "未在限定回合内达标";
            }
            if (Result == null || !sessionState.BattleResults.Complete(Result, success))
            {
                runtimeState.isSuccess = false;
                runtimeState.phase = CombatPhase.Defeat;
                LastStartFailure = Result != null && Result.Failure != null ? Result.Failure : "过期的战斗结果，未执行结算。";
                return;
            }
            runtimeState.inputLocked = false;
        }

        private void EvaluateCompletion()
        {
            if (runtimeState == null || runtimeState.data == null || runtimeState.isComplete)
            {
                return;
            }

            if (runtimeState.currentProgress >= runtimeState.data.targetProgress)
            {
                CompleteChallenge(true);
            }
        }

        private void ApplyBlockFromCard(CardData card)
        {
            int block = GetBlockValue(card);
            if (block <= 0)
            {
                return;
            }

            runtimeState.playerBlock += block;
        }

        private int GetBlockValue(CardData card)
        {
            if (card == null)
            {
                return 0;
            }

            if (card.block > 0)
            {
                return card.block;
            }

            if (card.cardType == CardType.Rest || GetEffectiveTargetType(card) == CardTargetType.Self)
            {
                return DefaultSelfBlock;
            }

            return 0;
        }

        private void ResolveEnemyTurn()
        {
            if (Intent == null || Intent.Turn != runtimeState.currentTurn) RefreshEnemyIntent();
            int damage = Intent.Damage;
            ApplyDamageToPlayer(damage);
            ApplyTurnIntent();
            if (runtimeState.playerHp <= 0)
            {
                CompleteChallenge(false);
            }
        }

        private void ApplyDamageToPlayer(int damage)
        {
            if (damage <= 0)
            {
                return;
            }

            int blocked = Mathf.Min(runtimeState.playerBlock, damage);
            runtimeState.playerBlock -= blocked;
            int hpDamage = damage - blocked;
            runtimeState.playerHp = Mathf.Max(0, runtimeState.playerHp - hpDamage);
        }

        private void RefreshEnemyIntent()
        {
            if (runtimeState == null || runtimeState.data == null) return;
            Intent = LegacyIntentSnapshot.Create(runtimeState.data, runtimeState.currentTurn);
            runtimeState.enemyIntentDamage = Intent.Damage;
        }

        private static bool HasEffect(CardData card, CardEffectType effectType)
        {
            if (card == null || card.effects == null)
            {
                return false;
            }

            for (int i = 0; i < card.effects.Count; i++)
            {
                CardEffectData effect = card.effects[i];
                if (effect != null && effect.effectType == effectType)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureDeckController()
        {
            if (deckController != null)
            {
                return;
            }

            deckController = GetComponent<DeckController>();
            if (deckController == null)
            {
                deckController = gameObject.AddComponent<DeckController>();
            }
        }
    }
}
