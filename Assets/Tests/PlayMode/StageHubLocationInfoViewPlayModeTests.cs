using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StudentGrowth.Tests
{
    public sealed class StageHubLocationInfoViewPlayModeTests
    {
        private GameObject root;
        private StageHubLocationInfoView view;
        private StageMapLocationEntry entry;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("StageHubLocationInfoViewTest", typeof(RectTransform));
            view = root.AddComponent<StageHubLocationInfoView>();
            view.locationNameText = CreateText("LocationName");
            view.descriptionText = CreateText("Description");
            view.availabilityReasonText = CreateText("AvailabilityReason");
            view.statusBadgeText = CreateText("StatusBadge");
            view.confirmButtonText = CreateText("ConfirmText");
            view.resultMessageText = CreateText("ResultMessage");

            GameObject buttonObject = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(root.transform, false);
            view.confirmButton = buttonObject.GetComponent<Button>();

            entry = new StageMapLocationEntry
            {
                locationId = "location_home",
                displayName = "家里",
                description = "恢复精力、沟通家庭",
                sceneName = "HomeMapScene"
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator PresentationPriorityAndFourButtonRulesRemainConsistent()
        {
            view.Refresh(null, entry, StageMapLocationStatus.Unlocked, null, string.Empty, false, true);
            yield return null;
            Assert.That(view.statusBadgeText.text, Is.EqualTo("已开放"));
            Assert.That(view.confirmButtonText.text, Is.EqualTo("确认进入"));
            Assert.That(view.confirmButton.interactable, Is.True);
            Assert.That(view.availabilityReasonText.gameObject.activeSelf, Is.False);

            view.Refresh(null, entry, StageMapLocationStatus.Unlocked, null, string.Empty, true, true);
            yield return null;
            Assert.That(view.statusBadgeText.text, Is.EqualTo("当前所在地点"));
            Assert.That(view.confirmButtonText.text, Is.EqualTo("进入"));
            Assert.That(view.confirmButton.interactable, Is.True);

            view.Refresh(null, entry, StageMapLocationStatus.Unlocked, null, string.Empty, true, false);
            yield return null;
            Assert.That(view.statusBadgeText.text, Is.EqualTo("当前所在地点"));
            Assert.That(view.confirmButtonText.text, Is.EqualTo("已在此处"));
            Assert.That(view.confirmButton.interactable, Is.False);

            view.Refresh(null, entry, StageMapLocationStatus.Locked, null, "完成学校事件后开放。", true, true);
            yield return null;
            Assert.That(view.statusBadgeText.text, Is.EqualTo("已锁定"));
            Assert.That(view.confirmButtonText.text, Is.EqualTo("尚未开放"));
            Assert.That(view.confirmButton.interactable, Is.False);
            Assert.That(view.availabilityReasonText.gameObject.activeSelf, Is.True);
            Assert.That(view.availabilityReasonText.text, Is.EqualTo("完成学校事件后开放。"));

            view.Refresh(null, entry, StageMapLocationStatus.Completed, null, string.Empty, false, true);
            yield return null;
            Assert.That(view.statusBadgeText.text, Is.EqualTo("已完成"));
            Assert.That(view.confirmButtonText.text, Is.EqualTo("确认进入"));
            Assert.That(view.confirmButton.interactable, Is.True);
            Assert.That(view.availabilityReasonText.gameObject.activeSelf, Is.False);
        }

        private Text CreateText(string name)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(root.transform, false);
            return textObject.GetComponent<Text>();
        }
    }
}
