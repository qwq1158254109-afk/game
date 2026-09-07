using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class CardEffectResolverTests
    {
        [Test]
        public void StudyCardAddsChallengeProgress()
        {
            CardData card = ScriptableObject.CreateInstance<CardData>();
            card.effects.Add(new CardEffectData
            {
                effectType = CardEffectType.AddChallengeProgress,
                value = 8
            });
            GameSessionState sessionState = new GameSessionState();
            ChallengeRuntimeState challengeState = new ChallengeRuntimeState();
            CardEffectResolver resolver = new CardEffectResolver();

            resolver.ApplyCard(card, sessionState, challengeState);

            Assert.AreEqual(8, challengeState.currentProgress);
        }

        [Test]
        public void RestCardCanRestoreEnergyAndReducePressure()
        {
            CardData card = ScriptableObject.CreateInstance<CardData>();
            card.effects.Add(new CardEffectData
            {
                effectType = CardEffectType.GainEnergy,
                value = 10
            });
            card.effects.Add(new CardEffectData
            {
                effectType = CardEffectType.ReducePressure,
                value = 5
            });
            GameSessionState sessionState = new GameSessionState();
            sessionState.playerState.SetStat(PlayerStatKey.Energy, 30);
            sessionState.playerState.SetStat(PlayerStatKey.Pressure, 20);
            CardEffectResolver resolver = new CardEffectResolver();

            resolver.ApplyCard(card, sessionState, new ChallengeRuntimeState());

            Assert.AreEqual(40, sessionState.playerState.energy);
            Assert.AreEqual(15, sessionState.playerState.pressure);
        }

        [Test]
        public void DrawCardsEffectUsesDeckController()
        {
            GameObject gameObject = new GameObject("CardEffectResolverTests");
            DeckController deckController = gameObject.AddComponent<DeckController>();
            CardData cardToDraw = ScriptableObject.CreateInstance<CardData>();
            deckController.deckState.drawPile.Add(cardToDraw);

            CardData drawCard = ScriptableObject.CreateInstance<CardData>();
            drawCard.effects.Add(new CardEffectData
            {
                effectType = CardEffectType.DrawCards,
                value = 1
            });

            CardEffectResolver resolver = new CardEffectResolver();

            resolver.ApplyCard(drawCard, new GameSessionState(), new ChallengeRuntimeState(), deckController);

            Assert.Contains(cardToDraw, deckController.deckState.hand);
            Object.DestroyImmediate(gameObject);
        }
    }
}
