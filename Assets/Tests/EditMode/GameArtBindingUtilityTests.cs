using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class GameArtBindingUtilityTests
    {
        [Test]
        public void ApplyKnownArtPathSetsCardImagePath()
        {
            CardData cardData = ScriptableObject.CreateInstance<CardData>();
            cardData.cardId = "card_rest_good_sleep";

            bool changed = GameArtBindingUtility.ApplyKnownArtPath(cardData);

            Assert.IsTrue(changed);
            Assert.AreEqual(
                "Assets/Art/CardReferences/FanworkPixel/Generated/PNG/card_rest_good_sleep.png",
                cardData.imagePath);
        }

        [Test]
        public void ApplyKnownArtPathSetsItemImagePath()
        {
            ItemData itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = "item_lucky_pen";

            bool changed = GameArtBindingUtility.ApplyKnownArtPath(itemData);

            Assert.IsTrue(changed);
            Assert.AreEqual("Assets/Art/Pixel/Icons/Items/icon_item_lucky_pen.png", itemData.imagePath);
        }

        [Test]
        public void ApplyKnownArtPathSetsStageBackgroundPath()
        {
            StageData stageData = ScriptableObject.CreateInstance<StageData>();
            stageData.stageId = "stage_01_adaptation";

            bool changed = GameArtBindingUtility.ApplyKnownArtPath(stageData);

            Assert.IsTrue(changed);
            Assert.AreEqual("Assets/Art/Pixel/Backgrounds/bg_school_classroom_rpg_map.png", stageData.imagePath);
        }

        [Test]
        public void ApplyKnownArtPathSetsChallengeImagePath()
        {
            ChallengeData challengeData = ScriptableObject.CreateInstance<ChallengeData>();
            challengeData.challengeId = "boss_final_exam";

            bool changed = GameArtBindingUtility.ApplyKnownArtPath(challengeData);

            Assert.IsTrue(changed);
            Assert.AreEqual(
                "Assets/Art/CardReferences/FanworkPixel/Generated/PNG/card_boss_final_exam.png",
                challengeData.imagePath);
        }
    }
}
