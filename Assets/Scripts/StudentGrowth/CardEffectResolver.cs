using System;

namespace StudentGrowth
{
    public class CardEffectResolver
    {
        public void ApplyCard(
            CardData card,
            GameSessionState sessionState,
            ChallengeRuntimeState challengeState,
            DeckController deckController = null)
        {
            if (card == null)
            {
                return;
            }

            if (sessionState == null)
            {
                throw new ArgumentNullException("sessionState");
            }

            foreach (CardEffectData effect in card.effects)
            {
                ApplyEffect(effect, sessionState, challengeState, deckController);
            }
        }

        public void ApplyEffect(
            CardEffectData effect,
            GameSessionState sessionState,
            ChallengeRuntimeState challengeState,
            DeckController deckController = null)
        {
            if (effect == null)
            {
                return;
            }

            if (sessionState == null)
            {
                throw new ArgumentNullException("sessionState");
            }

            switch (effect.effectType)
            {
                case CardEffectType.AddChallengeProgress:
                    AddChallengeProgress(challengeState, effect.value);
                    break;
                case CardEffectType.AddSubjectProgress:
                case CardEffectType.AddPlayerStat:
                    sessionState.playerState.AddStat(effect.targetStat, effect.value);
                    break;
                case CardEffectType.ReducePlayerStat:
                    sessionState.playerState.AddStat(effect.targetStat, -effect.value);
                    break;
                case CardEffectType.DrawCards:
                    if (deckController != null)
                    {
                        deckController.DrawCards(effect.value);
                    }
                    break;
                case CardEffectType.GainEnergy:
                    sessionState.playerState.AddStat(PlayerStatKey.Energy, effect.value);
                    break;
                case CardEffectType.LoseEnergy:
                    sessionState.playerState.AddStat(PlayerStatKey.Energy, -effect.value);
                    break;
                case CardEffectType.ReducePressure:
                    sessionState.playerState.AddStat(PlayerStatKey.Pressure, -effect.value);
                    break;
                case CardEffectType.AddPressure:
                    sessionState.playerState.AddStat(PlayerStatKey.Pressure, effect.value);
                    break;
                case CardEffectType.AddTemporaryBuff:
                case CardEffectType.AddStatusCardToDrawPile:
                case CardEffectType.AddStatusCardToDiscardPile:
                case CardEffectType.ExhaustCard:
                case CardEffectType.ModifyNextCardEffect:
                    break;
                default:
                    throw new ArgumentOutOfRangeException("effect.effectType", effect.effectType, "Unsupported card effect type.");
            }

            sessionState.ClampAll();
        }

        private static void AddChallengeProgress(ChallengeRuntimeState challengeState, int value)
        {
            if (challengeState == null)
            {
                return;
            }

            challengeState.currentProgress += value;
            if (challengeState.currentProgress < 0)
            {
                challengeState.currentProgress = 0;
            }
        }
    }
}
