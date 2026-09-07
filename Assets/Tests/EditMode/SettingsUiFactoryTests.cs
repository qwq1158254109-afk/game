using NUnit.Framework;
using StudentGrowth;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth.Tests
{
    public class SettingsUiFactoryTests
    {
        private GameObject owner;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("SettingsUiFactoryTests");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(owner);
        }

        [Test]
        public void FactoryCreatesReferenceStyleMasterMusicAndSfxSliders()
        {
            SettingsPanelView view = SettingsUiFactory.Create(owner.transform);

            Assert.IsNotNull(view);
            Assert.IsNotNull(view.MasterSlider);
            Assert.IsNotNull(view.MusicSlider);
            Assert.IsNotNull(view.SfxSlider);
            Assert.AreNotSame(view.MasterSlider, view.MusicSlider);
            Assert.AreNotSame(view.MusicSlider, view.SfxSlider);
            Assert.IsFalse(view.IsVisible);

            view.SetVolumes(0.8f, 0.3f, 0.7f);

            Assert.AreEqual(0.8f, view.MasterSlider.value, 0.0001f);
            Assert.AreEqual(0.3f, view.MusicSlider.value, 0.0001f);
            Assert.AreEqual(0.7f, view.SfxSlider.value, 0.0001f);
            Assert.AreEqual(1350f, ((RectTransform)view.MasterSlider.transform).sizeDelta.x);
            Assert.IsNull(view.MasterSlider.fillRect);
            Assert.IsNotNull(view.MasterSlider.transform.Find("TrackBorder"));
            Assert.IsNotNull(view.MasterSlider.transform.Find("Handle Slide Area/Handle"));
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(34f, view.MasterSlider.handleRect.rect.width, 0.001f);
            Assert.AreEqual(34f, view.MasterSlider.handleRect.rect.height, 0.001f);

            Text[] labels = view.GetComponentsInChildren<Text>(true);
            Assert.IsTrue(System.Array.Exists(labels, label => label.text.Contains("F11")));
        }

        [Test]
        public void TransactionButtonsEmitCommandsAndDoNotSaveByThemselves()
        {
            SettingsPanelView view = SettingsUiFactory.Create(owner.transform);
            int applied = 0, cancelled = 0, retries = 0;
            view.ApplyRequested += () => applied++;
            view.CancelRequested += () => cancelled++;
            view.RetryRequested += () => retries++;
            view.ApplyButton.onClick.Invoke(); view.CancelButton.onClick.Invoke(); view.RetryButton.onClick.Invoke();
            Assert.AreEqual(1, applied); Assert.AreEqual(1, cancelled); Assert.AreEqual(1, retries);
            Assert.IsNotNull(view.GetComponentInChildren<Dropdown>(true));
            Assert.IsNotNull(view.GetComponentInChildren<Toggle>(true));
        }

        [Test]
        public void DegradedStateDisablesEditingButOffersRecovery()
        {
            SettingsPanelView view = SettingsUiFactory.Create(owner.transform);
            var service = new SettingsService(new UiRepository(), new[] { new BrokenApplier() });
            service.Initialize(); view.ShowState(service, false);
            Assert.AreEqual(SettingsServiceState.Degraded, service.State);
            Assert.IsFalse(view.MasterSlider.interactable); Assert.IsFalse(view.ApplyButton.interactable);
            Assert.IsTrue(view.RetryButton.interactable); StringAssert.Contains("恢复", view.StatusText);
        }

        private sealed class UiRepository : ISettingsRepository
        {
            public SettingsLoadResult Load() => new SettingsLoadResult(new SettingsSnapshot(), true);
            public SettingsSaveResult Save(SettingsSnapshot settings) => new SettingsSaveResult(SettingsPersistenceOutcome.Committed);
        }
        private sealed class BrokenApplier : ISettingsDomainApplier
        {
            public SettingsDomain Domain => SettingsDomain.Audio;
            public bool Apply(SettingsSnapshot settings) => false;
            public bool Restore(SettingsSnapshot settings) => false;
            public SettingsSnapshot Observe() => null;
        }
    }
}
