using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth.Tests
{
    public sealed class DisplaySettingsUiTests
    {
        private GameObject owner;
        private SettingsService service;
        private DisplayTransactionCoordinator display;
        private SettingsPanelView view;
        private Stack<IEnumerator> work;
        private double time;
        [SetUp] public void Setup()
        {
            owner = new GameObject("DisplaySettingsUiTests");
            service = new SettingsService(new Repository(),new ISettingsDomainApplier[0]); service.Initialize(); service.BeginEdit();
            display = new DisplayTransactionCoordinator(service,new Platform(),() => time);
            view = SettingsUiFactory.Create(owner.transform); view.GetComponentInChildren<DisplaySettingsView>(true).Bind(service);
            service.StateChanged += Refresh; view.SetVisible(true); Refresh(); work = new Stack<IEnumerator>();
        }
        private void Refresh() { if (view != null) view.ShowState(service,false); }
        [TearDown] public void Cleanup() { service.StateChanged -= Refresh; Object.DestroyImmediate(owner); }
        private void Frame()
        {
            time += 0.1;
            while(work.Count > 0)
            {
                var e = work.Peek(); if (!e.MoveNext()) { work.Pop(); continue; }
                if (e.Current is IEnumerator nested) { work.Push(nested); continue; } return;
            }
        }
        private void StartDisplay()
        {
            service.SetPendingDisplay(new DisplayIntent(1600,900)); work.Push(service.ApplyPendingAsync(null));
            for(int i=0;i<20 && !display.CanConfirm;i++) Frame(); Assert.IsTrue(display.CanConfirm);
        }
        [Test] public void ViewShowsCapabilitiesLocksInputsAndConfirmsThroughService()
        {
            var controls = view.GetComponentInChildren<DisplaySettingsView>(true).GetComponentsInChildren<Dropdown>(true);
            Assert.AreEqual(4,controls.Length); Assert.IsTrue(controls.All(d => d.options.Count > 0));
            StartDisplay(); Assert.IsFalse(view.ApplyButton.interactable); Assert.IsTrue(controls.All(d => !d.interactable));
            var button = view.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<Text>()?.text == "保留此模式");
            button.onClick.Invoke(); for(int i=0;i<20 && work.Count > 0;i++) Frame();
            Assert.AreEqual(1600,service.Applied.WindowWidth); Assert.AreEqual(PersistenceOutcome.Persisted,display.LastResult.PersistenceOutcome);
        }
        [Test] public void DestroyingConfirmationViewLeavesOwnerCoroutineAbleToRecover()
        {
            StartDisplay();
            // Ordinary MonoBehaviour destruction messages are not dispatched in EditMode.
            // The Play smoke runner invokes this same test with real Unity lifecycle messages.
            if (!Application.isPlaying) typeof(DisplaySettingsView).GetMethod("OnDestroy",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(view.GetComponentInChildren<DisplaySettingsView>(true), null);
            Object.DestroyImmediate(view.gameObject);
            for(int i=0;i<50 && work.Count > 0;i++) Frame();
            Assert.AreEqual(0,work.Count); Assert.AreEqual(DisplayRecoveryOutcome.RollbackSucceeded,display.LastResult.RecoveryOutcome);
            Assert.AreEqual(1280,service.Applied.WindowWidth); Assert.AreEqual(SettingsServiceState.Editing,service.State);
        }
        private sealed class Repository : ISettingsRepository
        {
            public SettingsLoadResult Load() => new SettingsLoadResult(new SettingsSnapshot(),true);
            public SettingsSaveResult Save(SettingsSnapshot value) => new SettingsSaveResult(SettingsPersistenceOutcome.Committed);
        }
        private sealed class Platform : IDisplayPlatform
        {
            private ResolvedDisplayMode actual = new ResolvedDisplayMode(0,1280,720,60,1,SettingsWindowIntent.Windowed);
            public DisplayCapabilities GetCapabilities() => new DisplayCapabilities(new[] {
                new DisplayDeviceCapabilities(0,"Test display",1920,1040,new ResolvedDisplayMode(0,1920,1080,60,1,SettingsWindowIntent.Borderless),new[] { actual }) },
                new[] { SettingsWindowIntent.Windowed,SettingsWindowIntent.Borderless },true,"test");
            public ResolvedDisplayMode Observe() => actual;
            public void Request(IReadOnlyResolvedDisplayMode m) { actual = new ResolvedDisplayMode(m.DisplayIndex,m.Width,m.Height,m.RefreshRateNumerator,m.RefreshRateDenominator,m.WindowMode); }
        }
    }
}
