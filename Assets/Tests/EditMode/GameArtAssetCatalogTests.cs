using System.IO;
using NUnit.Framework;
using StudentGrowth;

namespace StudentGrowth.Tests
{
    public class GameArtAssetCatalogTests
    {
        [Test]
        public void KnownCardImagePathExists()
        {
            bool found = GameArtAssetCatalog.TryGetCardImagePath("card_study_class_focus", out string path);

            Assert.IsTrue(found);
            Assert.AreEqual("Assets/Art/CardReferences/FanworkPixel/Generated/PNG/card_study_class_focus.png", path);
            Assert.IsTrue(File.Exists(path), path);
        }

        [Test]
        public void KnownItemIconPathExists()
        {
            bool found = GameArtAssetCatalog.TryGetItemIconPath("item_energy_drink", out string path);

            Assert.IsTrue(found);
            Assert.IsTrue(File.Exists(path), path);
        }

        [Test]
        public void KnownStatIconPathExists()
        {
            bool found = GameArtAssetCatalog.TryGetStatIconPath(PlayerStatKey.Math, out string path);

            Assert.IsTrue(found);
            Assert.IsTrue(File.Exists(path), path);
        }

        [Test]
        public void KnownStageBackgroundPathExists()
        {
            bool found = GameArtAssetCatalog.TryGetStageBackgroundPath("stage_03_final_sprint", out string path);

            Assert.IsTrue(found);
            Assert.IsTrue(File.Exists(path), path);
        }

        [Test]
        public void KnownChallengeImageAndIconPathsExist()
        {
            bool foundImage = GameArtAssetCatalog.TryGetChallengeImagePath("boss_month_exam", out string imagePath);
            bool foundIcon = GameArtAssetCatalog.TryGetChallengeIconPath("boss_month_exam", out string iconPath);

            Assert.IsTrue(foundImage);
            Assert.IsTrue(File.Exists(imagePath), imagePath);
            Assert.IsTrue(foundIcon);
            Assert.IsTrue(File.Exists(iconPath), iconPath);
        }
    }
}
