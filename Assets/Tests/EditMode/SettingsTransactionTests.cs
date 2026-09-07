using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace StudentGrowth.Tests
{
    public sealed class SettingsTransactionTests
    {
        private MemoryRepository repository;
        private TestApplier audio;
        private TestApplier locale;
        private SettingsService service;
        private List<string> calls;

        [SetUp] public void SetUp()
        {
            calls = new List<string>(); repository = new MemoryRepository();
            audio = new TestApplier(SettingsDomain.Audio, calls); locale = new TestApplier(SettingsDomain.Localization, calls);
            service = new SettingsService(repository, new[] { audio, locale }); service.Initialize(); calls.Clear();
        }
        [Test] public void CoroutineCommandsExecuteEvenWithoutCompletionCallbacks()
        {
            service.BeginEdit(); service.PreviewAudio(0.3f, 1, 1);
            Assert.IsFalse(service.ApplyPendingAsync(null).MoveNext());
            Assert.AreEqual(0.3f, service.Applied.Master);
            service.PreviewAudio(0.6f, 1, 1);
            Assert.IsFalse(service.CancelEditingAsync(null).MoveNext());
            Assert.AreEqual(SettingsServiceState.Ready, service.State);
            Assert.AreEqual(0.3f, audio.Actual.Master);
            service.BeginEdit(); audio.FailApply = true; audio.FailRestore = true;
            service.PreviewAudio(0.7f, 1, 1);
            service.TryRecoverRuntimeStateAsync(null).MoveNext();
            Assert.AreEqual(SettingsServiceState.Degraded, service.State);
            audio.FailApply = false; audio.FailRestore = false;
            service.TryRecoverRuntimeStateAsync(null).MoveNext();
            Assert.AreEqual(SettingsServiceState.Ready, service.State);
        }
        [Test] public void PreviewDoesNotChangeAppliedOrPersistedAndCancelRestoresEveryDomain()
        {
            service.BeginEdit(); service.PreviewAudio(0.2f, 0.3f, 0.4f); service.PreviewLanguage("en");
            Assert.AreEqual(1f, service.Applied.Master); Assert.AreEqual("zh-Hans", service.Persisted.LanguageCode);
            Assert.AreEqual(0, repository.Saves); Assert.AreEqual(0.2f, service.Runtime[SettingsDomain.Audio].Master);
            Assert.IsTrue(service.CancelEditing().Success);
            Assert.AreEqual(1f, audio.Actual.Master); Assert.AreEqual("zh-Hans", locale.Actual.LanguageCode);
            Assert.AreEqual(0, repository.Saves);
        }
        [Test] public void ApplyNormalizesEveryPreviewBeforeStartingAnyApply()
        {
            service.BeginEdit(); service.PreviewAudio(0.2f, 1, 1); service.PreviewLanguage("en"); calls.Clear();
            Assert.IsTrue(service.ApplyPending().RuntimeAccepted);
            CollectionAssert.AreEqual(new[] { "restore:Localization", "observe:Localization", "restore:Audio", "observe:Audio",
                "apply:Audio", "observe:Audio", "apply:Localization", "observe:Localization" }, calls);
            Assert.AreEqual("en", service.Persisted.LanguageCode); Assert.AreEqual(0.2f, service.Applied.Master);
        }
        [Test] public void FailureDomainIsRestoredEvenWhenApplyPartiallyChangedIt()
        {
            service.BeginEdit(); service.PreviewAudio(0.2f, 1, 1); service.PreviewLanguage("en");
            locale.FailApply = true; calls.Clear();
            Assert.IsFalse(service.ApplyPending().RuntimeAccepted);
            Assert.AreEqual("zh-Hans", locale.Actual.LanguageCode); Assert.AreEqual(1f, audio.Actual.Master);
            Assert.IsTrue(calls.Contains("restore:Localization")); Assert.AreEqual(0, repository.Saves);
            Assert.AreEqual("en", service.Pending.LanguageCode); Assert.AreEqual(SettingsServiceState.Editing, service.State);
        }
        [Test] public void PreviewNormalizationFailureBlocksApplyAndWrites()
        {
            service.BeginEdit(); service.PreviewAudio(0.2f, 1, 1); audio.FailRestore = true; calls.Clear();
            Assert.IsFalse(service.ApplyPending().RuntimeAccepted);
            Assert.AreEqual(SettingsServiceState.Degraded, service.State);
            Assert.IsFalse(calls.Any(c => c.StartsWith("apply:"))); Assert.AreEqual(0, repository.Saves);
            Assert.IsFalse(service.RetryPersistApplied().Success); Assert.IsFalse(service.PreviewAudio(0.1f, 1, 1).Success);
        }
        [Test] public void FailedPreviewIsRestoredAndCannotSilentlyBecomeAccepted()
        {
            service.BeginEdit(); audio.FailApply = true;
            Assert.IsFalse(service.PreviewAudio(0.2f, 1, 1).Success);
            Assert.AreEqual(1f, audio.Actual.Master); Assert.AreEqual(1f, service.Applied.Master);
            Assert.AreEqual(0.2f, service.Pending.Master); Assert.AreEqual(0, repository.Saves);
        }
        [Test] public void PreviewFailureAndRestoreFailureEnterDegraded()
        {
            service.BeginEdit(); audio.FailApply = true; audio.FailRestore = true;
            service.PreviewAudio(0.2f, 1, 1);
            Assert.AreEqual(SettingsDegradedReason.PreviewRestoreFailed, service.DegradedInfo.Reason);
        }
        [Test] public void RollbackContinuesAfterOneRestoreFailsAndCanRecoverLater()
        {
            service.BeginEdit(); service.PreviewAudio(0.2f, 1, 1);
            locale.FailApply = true; locale.FailRestore = true; calls.Clear();
            service.ApplyPending();
            Assert.AreEqual(SettingsServiceState.Degraded, service.State);
            Assert.IsTrue(calls.Contains("restore:Audio")); Assert.AreEqual(0, repository.Saves);
            Assert.IsFalse(service.TryRecoverRuntimeState().Success);
            locale.FailRestore = false; locale.FailApply = false;
            Assert.IsTrue(service.TryRecoverRuntimeState().Success);
            Assert.AreEqual(SettingsServiceState.Ready, service.State); Assert.IsNull(service.DegradedInfo);
        }
        [Test] public void SaveFailureKeepsAcceptedValuesAndRetryNeverCallsAppliers()
        {
            service.BeginEdit(); service.PreviewAudio(0.2f, 1, 1); repository.Fail = true;
            var result = service.ApplyPending();
            Assert.IsTrue(result.RuntimeAccepted); Assert.IsFalse(result.Persistence.Success);
            Assert.AreEqual(0.2f, service.Applied.Master); Assert.AreEqual(1f, service.Persisted.Master);
            Assert.IsTrue(service.IsPersistenceDirty); calls.Clear(); repository.Fail = false;
            Assert.IsTrue(service.RetryPersistApplied().Success); Assert.IsEmpty(calls);
            Assert.IsFalse(service.IsPersistenceDirty);
        }
        [Test] public void LifecycleRetryDoesNotSaveNewPendingPreview()
        {
            service.BeginEdit(); service.PreviewAudio(0.2f, 1, 1); repository.Fail = true; service.ApplyPending();
            service.PreviewAudio(0.7f, 1, 1); repository.Fail = false; service.RetryPersistApplied();
            Assert.AreEqual(0.2f, repository.Last.Master); Assert.AreEqual(0.7f, service.Pending.Master);
            Assert.AreEqual(0.7f, audio.Actual.Master);
        }
        [Test] public void StateSnapshotsHaveNoSettersAndRemainStableAfterSubsequentEdits()
        {
            var saved = service.Applied;
            Assert.IsTrue(typeof(SettingsSnapshot).GetProperties().All(p => p.SetMethod == null));
            service.BeginEdit(); service.PreviewAudio(0.3f, 1, 1); service.ApplyPending();
            Assert.AreEqual(1f, saved.Master); Assert.AreNotSame(service.Applied, service.Persisted);
        }
        [Test] public void NonfiniteInputIsRejectedBeforeAnyApplier()
        {
            service.BeginEdit(); calls.Clear();
            Assert.IsFalse(service.PreviewAudio(float.NaN, 1, 1).Success);
            Assert.IsFalse(service.PreviewAudio(float.PositiveInfinity, 1, 1).Success);
            Assert.IsEmpty(calls);
        }
        [Test] public void UnavailableDomainDoesNotPretendToApplyLanguage()
        {
            var onlyAudio = new SettingsService(repository, new[] { audio }); onlyAudio.Initialize(); onlyAudio.BeginEdit();
            Assert.IsFalse(onlyAudio.PreviewLanguage("en").Success);
        }
        [Test] public void WrongObservationMakesServiceDegradedAtStartup()
        {
            var liar = new TestApplier(SettingsDomain.Audio, calls) { Unobservable = true };
            var s = new SettingsService(repository, new[] { liar }); s.Initialize();
            Assert.AreEqual(SettingsServiceState.Degraded, s.State); Assert.IsNull(s.Runtime[SettingsDomain.Audio]);
            Assert.AreEqual(0, repository.Saves);
        }
        [Test] public void ReadOnlyConfigurationBlocksEveryPersistenceEntryPoint()
        {
            repository.ReadOnly = true;
            var s = new SettingsService(repository, new[] { audio }); s.Initialize(); s.BeginEdit();
            s.PreviewAudio(0.1f, 1, 1); s.ApplyPending(); s.RetryPersistApplied();
            Assert.AreEqual(0, repository.Saves); Assert.IsTrue(s.IsReadOnly);
        }
        [Test] public void EventHandlersCannotReenterApplyOrDestroyTransactionByThrowing()
        {
            int attempts = 0;
            bool reentered = false;
            service.StateChanged += () => { attempts++; reentered |= service.BeginEdit().Success; reentered |= service.ApplyPending().RuntimeAccepted; };
            service.StateChanged += () => throw new Exception("view failure");
            Assert.IsTrue(service.BeginEdit().Success); service.PreviewAudio(0.2f, 1, 1);
            Assert.IsTrue(service.ApplyPending().RuntimeAccepted); Assert.Greater(attempts, 0);
            Assert.IsFalse(reentered);
        }
        [Test] public void ResetThenCancelPreservesAcceptedValues()
        {
            service.BeginEdit(); service.PreviewAudio(0.2f, 1, 1); service.ApplyPending();
            service.ResetPendingToDefaults(); service.CancelEditing();
            Assert.AreEqual(0.2f, audio.Actual.Master); Assert.AreEqual(0.2f, service.Persisted.Master);
        }

        private sealed class MemoryRepository : ISettingsRepository
        {
            internal int Saves; internal bool Fail; internal bool ReadOnly; internal SettingsSnapshot Last = new SettingsSnapshot();
            public SettingsLoadResult Load() => new SettingsLoadResult(Last, true, ReadOnly);
            public SettingsSaveResult Save(SettingsSnapshot s)
            {
                Saves++; if (Fail) return new SettingsSaveResult(SettingsPersistenceOutcome.NotCommitted, "SETTINGS_SAVE_FAILED");
                Last = s; return new SettingsSaveResult(SettingsPersistenceOutcome.Committed);
            }
        }
        private sealed class TestApplier : ISettingsDomainApplier
        {
            public SettingsDomain Domain { get; }
            internal SettingsSnapshot Actual = new SettingsSnapshot();
            internal bool FailApply, FailRestore, Unobservable;
            private readonly List<string> calls;
            internal TestApplier(SettingsDomain domain, List<string> calls) { Domain = domain; this.calls = calls; }
            public bool Apply(SettingsSnapshot s) { calls.Add("apply:" + Domain); Actual = s.Copy(); return !FailApply; }
            public bool Restore(SettingsSnapshot s) { calls.Add("restore:" + Domain); if (FailRestore) return false; Actual = s.Copy(); return true; }
            public SettingsSnapshot Observe() { calls.Add("observe:" + Domain); return Unobservable ? null : Actual.Copy(); }
        }
    }
}
