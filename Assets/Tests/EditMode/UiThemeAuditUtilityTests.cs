using System.Collections.Generic;
using NUnit.Framework;
using StudentGrowth;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth.Tests
{
    public sealed class UiThemeAuditUtilityTests
    {
        private readonly List<GameObject> createdGameObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject createdGameObject in createdGameObjects)
            {
                Object.DestroyImmediate(createdGameObject);
            }

            createdGameObjects.Clear();
        }

        [Test]
        public void AuditUiRoot_ReportsMissingThemeComponents()
        {
            GameObject root = CreateGameObject("CanvasRoot");
            GameObject buttonObject = CreateChild(root, "StartButton");
            buttonObject.AddComponent<Image>();
            buttonObject.AddComponent<Button>();
            GameObject panelObject = CreateChild(root, "StatusPanel");
            panelObject.AddComponent<Image>();
            GameObject textObject = CreateChild(root, "StatusText");
            textObject.AddComponent<Text>();
            UiThemeAuditSummary summary = new UiThemeAuditSummary();

            UiThemeAuditUtility.AuditUiRoot(summary, root, "Test");

            Assert.AreEqual(1, summary.buttonCount);
            Assert.AreEqual(1, summary.panelCandidateCount);
            Assert.AreEqual(1, summary.textCount);
            Assert.AreEqual(0, summary.pixelTextCount);
            Assert.AreEqual(3, summary.Issues.Count);
            Assert.IsTrue(summary.Issues[0].Contains("button missing PixelButton"));
            Assert.IsTrue(summary.Issues[1].Contains("panel-like image missing PixelPanel"));
            Assert.IsTrue(summary.Issues[2].Contains("text missing PixelText"));
        }

        [Test]
        public void AuditUiRoot_PassesWhenThemeComponentsArePresent()
        {
            GameObject root = CreateGameObject("CanvasRoot");
            GameObject buttonObject = CreateChild(root, "StartButton");
            Image buttonImage = buttonObject.AddComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            PixelButton pixelButton = buttonObject.AddComponent<PixelButton>();
            pixelButton.background = buttonImage;
            pixelButton.animateScale = false;
            button.targetGraphic = buttonImage;

            GameObject panelObject = CreateChild(root, "StatusPanel");
            Image panelImage = panelObject.AddComponent<Image>();
            PixelPanel pixelPanel = panelObject.AddComponent<PixelPanel>();
            pixelPanel.background = panelImage;

            GameObject textObject = CreateChild(root, "StatusText");
            Text text = textObject.AddComponent<Text>();
            PixelText pixelText = textObject.AddComponent<PixelText>();
            pixelText.target = text;
            UiThemeAuditSummary summary = new UiThemeAuditSummary();

            UiThemeAuditUtility.AuditUiRoot(summary, root, "Test");

            Assert.AreEqual(1, summary.buttonCount);
            Assert.AreEqual(1, summary.panelCandidateCount);
            Assert.AreEqual(1, summary.textCount);
            Assert.AreEqual(1, summary.pixelTextCount);
            Assert.AreEqual(0, summary.Issues.Count);
        }

        [Test]
        public void LooksLikePanel_MatchesCommonUiContainerNames()
        {
            Assert.IsTrue(UiThemeAuditUtility.LooksLikePanel("ShopWindow"));
            Assert.IsTrue(UiThemeAuditUtility.LooksLikePanel("InteractionPrompt"));
            Assert.IsTrue(UiThemeAuditUtility.LooksLikePanel("StatusHud"));
            Assert.IsFalse(UiThemeAuditUtility.LooksLikePanel("CoinIcon"));
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private static GameObject CreateChild(GameObject parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }
    }
}
