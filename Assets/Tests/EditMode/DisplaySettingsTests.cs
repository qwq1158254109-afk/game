using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

namespace StudentGrowth.Tests
{
    public sealed class DisplaySettingsTests
    {
        private double time;
        private Repository repository;
        private Platform platform;
        private SettingsService service;
        private DisplayTransactionCoordinator display;
        private Stack<IEnumerator> work;
        private DisplayIntent candidate;
        [SetUp] public void Setup()
        {
            time = 0; repository = new Repository(); platform = new Platform();
            service = new SettingsService(repository, new ISettingsDomainApplier[0]); service.Initialize(); service.BeginEdit();
            display = new DisplayTransactionCoordinator(service, platform, () => time);
            candidate = new DisplayIntent(1600, 900);
            work = new Stack<IEnumerator>();
        }
        private bool Frame()
        {
            time += 0.1;
            while (work.Count > 0)
            {
                var top = work.Peek();
                if (!top.MoveNext()) { work.Pop(); continue; }
                if (top.Current is IEnumerator nested) { work.Push(nested); continue; }
                return true;
            }
            return false;
        }
        private void Begin() { work.Push(display.ApplyAsync(candidate)); Until(() => display.Phase == DisplayTransactionPhase.Confirming); }
        private void Until(Func<bool> condition)
        { for (int i = 0; i < 1000 && !condition(); i++) Frame(); Assert.IsTrue(condition(), "Coroutine did not reach expected state"); }
        private void Finish() { Until(() => work.Count == 0); }
#if UNITY_EDITOR
        [Test] public void PauseNavigationRejectsDangerousDisplayConfirmation()
        {
            var menu = new PauseMenuCoordinator(service,null,null,()=>false,_=>{},()=>{},()=>{},()=>{},()=>null);
            menu.OpenPause(); Begin();
            Assert.IsFalse(menu.Back().Success);
            Assert.IsFalse(menu.OpenSave().Success);
            Assert.IsFalse(menu.OpenLoad().Success);
            Assert.IsFalse(menu.ContinueGame().Success);
            CommandResult result=null; var title=menu.ReturnToTitleAsync(r=>result=r); while(title.MoveNext()) { }
            Assert.IsFalse(result.Success); Assert.AreEqual(PauseMenuState.Paused,menu.State);
            display.Reject(display.TransactionId); Finish();
            Assert.IsTrue(menu.ContinueGame().Success);
        }
#endif
        [Test] public void IntentAndActualAreDifferentAndFallbackDoesNotOverwritePreference()
        {
            var intent = new DisplayIntent(2560, 1440, SettingsWindowIntent.Borderless, 144, 1, 99, "missing");
            var resolved = new DisplayModeResolver().Resolve(intent, platform.GetCapabilities()).Mode;
            Assert.AreEqual(0, resolved.DisplayIndex); Assert.IsTrue(resolved.IsFallback);
            Assert.AreEqual(2560, intent.Width); Assert.AreEqual("missing", intent.PreferredDisplayId);
            Assert.AreNotEqual(intent.GetType(), resolved.GetType());
        }
        [Test] public void EnumerationFiltersDeduplicatesAndSortsRationalRates()
        {
            var d = new DisplayDeviceCapabilities(0, "A", 1920, 1040, platform.Actual, new[] {
                new ResolvedDisplayMode(0, 1920,1080,120,2,SettingsWindowIntent.Exclusive),
                new ResolvedDisplayMode(0, 1920,1080,60,1,SettingsWindowIntent.Exclusive),
                new ResolvedDisplayMode(0, 800,600,60,1,SettingsWindowIntent.Exclusive),
                new ResolvedDisplayMode(0, 0,600,60,0,SettingsWindowIntent.Exclusive) });
            Assert.AreEqual(2,d.Modes.Count); Assert.AreEqual(800,d.Modes[0].Width);
        }
        [Test] public void ResolutionMatchingClampsWindowAndPreservesIntent()
        {
            var intent = new DisplayIntent(4000, 3000);
            var result = new DisplayModeResolver().Resolve(intent, platform.GetCapabilities()).Mode;
            Assert.AreEqual(1920,result.Width); Assert.AreEqual(1040,result.Height); Assert.AreEqual(4000,intent.Width);
        }
        [Test] public void RequestWaitsTwoFramesBeforeAnyObservation()
        {
            var applier = new DisplaySettingsApplier(platform, () => time); int before = platform.Observations;
            var e = applier.RequestAndObserveAsync(platform.Actual, new DisplayVerificationPolicy(), _ => { });
            Assert.IsTrue(e.MoveNext()); Assert.AreEqual(before, platform.Observations);
            Assert.IsTrue(e.MoveNext()); Assert.AreEqual(before, platform.Observations);
            e.MoveNext(); Assert.Greater(platform.Observations, before);
        }
        [Test] public void ConfirmationCommitsAndSaveFailureRemainsDirty()
        {
            Begin(); repository.Fail = true;
            Assert.AreEqual(1280, service.Applied.WindowWidth); Assert.IsTrue(display.Confirm(display.TransactionId)); Finish();
            Assert.AreEqual(1600, service.Applied.WindowWidth); Assert.IsTrue(service.IsPersistenceDirty);
            Assert.AreEqual(PersistenceOutcome.PersistenceFailed, display.LastResult.PersistenceOutcome);
            Assert.AreEqual(DisplayRecoveryOutcome.NotRequired, display.LastResult.RecoveryOutcome);
        }
        [Test] public void TimeoutAndRecoveryAreReportedTogether()
        {
            Begin(); Finish();
            Assert.AreEqual(DisplayVerificationOutcome.ConfirmationTimedOut, display.LastResult.VerificationOutcome);
            Assert.AreEqual(DisplayRecoveryOutcome.RollbackSucceeded, display.LastResult.RecoveryOutcome);
            Assert.AreEqual(1280, service.Applied.WindowWidth); Assert.AreEqual(0, repository.Saves);
        }
        [Test] public void WrongActualModeFallsBackAndKeepsOriginalIntent()
        {
            platform.IgnoreRequests = true; platform.Actual = new ResolvedDisplayMode(0, 800,600,60,1,SettingsWindowIntent.Windowed);
            work.Push(display.ApplyAsync(candidate)); Finish();
            Assert.AreEqual(DisplayVerificationOutcome.ApplyUnacceptable, display.LastResult.VerificationOutcome);
            Assert.AreEqual(DisplayRecoveryOutcome.RollbackFailed, display.LastResult.RecoveryOutcome);
            Assert.AreEqual(SettingsServiceState.Degraded,service.State);
            platform.IgnoreRequests = false; work.Push(display.RetryRecoveryAsync()); Finish();
            Assert.AreEqual(SettingsServiceState.Editing, service.State); Assert.AreEqual(1280,service.Applied.WindowWidth);
        }
        [Test] public void MissingHardwareRecoveryUsesDesktopAndPreservesIntent()
        {
            Begin(); platform.Fingerprint = "changed"; display.Reject(display.TransactionId); Finish();
            Assert.AreEqual(DisplayRecoveryOutcome.RollbackDegraded, display.LastResult.RecoveryOutcome);
            Assert.AreEqual(1280,service.Applied.WindowWidth);
        }
        [Test] public void FocusPausesConfirmationAndRegainHasFiveSeconds()
        {
            Begin(); Until(() => time > 2); display.SetFocus(false); double remaining = display.RemainingSeconds;
            for(int i=0;i<80;i++) Frame();
            Assert.AreEqual(remaining,display.RemainingSeconds,0.01); Assert.IsFalse(display.Confirm(display.TransactionId));
            display.SetFocus(true); Assert.GreaterOrEqual(display.RemainingSeconds,5); display.Confirm(display.TransactionId); Finish();
        }
        [Test] public void FocusGraceDoesNotRollbackTransientLoss()
        {
            Begin(); display.SetFocus(false); Frame(); display.SetFocus(true);
            Assert.IsFalse(display.RollbackRequired); Assert.IsTrue(display.Confirm(display.TransactionId)); Finish();
        }
        [Test] public void BackgroundThirtySecondsAndPauseRequireRecovery()
        {
            Begin(); display.SetFocus(false); time += 31; Frame(); Finish();
            Assert.AreEqual(DisplayRecoveryOutcome.RollbackSucceeded,display.LastResult.RecoveryOutcome);
            Begin(); display.SetPaused(true); for(int i=0;i<5;i++) Frame(); Assert.IsTrue(display.IsBusy);
            display.SetPaused(false); Finish(); Assert.AreEqual(DisplayRecoveryOutcome.RollbackSucceeded,display.LastResult.RecoveryOutcome);
        }
        [Test] public void DuplicateRequestAndStaleConfirmationAreRejected()
        {
            Begin(); string old = display.TransactionId; DisplayTransactionResult duplicate = null;
            var e = display.ApplyAsync(candidate, completed: r => duplicate = r); Assert.IsFalse(e.MoveNext());
            Assert.AreEqual(DisplayRequestOutcome.RejectedBusy,duplicate.RequestOutcome);
            display.Reject(old); Finish(); Begin(); Assert.IsFalse(display.Confirm(old)); display.Reject(display.TransactionId); Finish();
        }
        [Test] public void ViewCloseAndSceneUnloadRecoverWithoutView()
        {
            Begin(); display.ViewClosed(); Finish(); Assert.AreEqual(DisplayRecoveryOutcome.RollbackSucceeded,display.LastResult.RecoveryOutcome);
            Begin(); display.SceneUnloaded(); Finish(); Assert.AreEqual(DisplayRequestOutcome.CancelledByLifecycle,display.LastResult.RequestOutcome);
        }
        [Test] public void QuitNeverPersistsCandidate()
        {
            Begin(); display.Quit(); Finish(); Assert.AreEqual(0,repository.Saves); Assert.AreEqual(1280,service.Applied.WindowWidth);
            Assert.AreEqual(DisplayRequestOutcome.CancelledByLifecycle,display.LastResult.RequestOutcome);
        }
        [Test] public void SafeApplyCannotCommitDisplayAndDisplayRollbackCannotUndoSafeApply()
        {
            service.SetPendingDisplay(candidate); service.ApplyPending(); Assert.AreEqual(1280, service.Applied.WindowWidth);
            Assert.AreEqual(candidate,service.Pending.DisplayIntent); Begin(); display.Reject(display.TransactionId); Finish();
            Assert.AreEqual(1280,service.Persisted.WindowWidth);
        }
        [Test] public void SafeOnlyAsyncApplyDoesNotStartConfirmation()
        {
            work.Push(service.ApplyPendingAsync(null)); Finish(); Assert.AreEqual(0,platform.Requests); Assert.IsFalse(display.IsBusy);
        }
        [Test] public void VSyncAndFrameCapCommitWithoutCountdownAndSurviveDisplayRejection()
        {
            var safe = new SafeApplier(); service = new SettingsService(repository,new[] { safe }); service.Initialize(); service.BeginEdit();
            display = new DisplayTransactionCoordinator(service,platform,() => time);
            service.SetPendingSafeGraphics(false,120); work.Push(service.ApplyPendingAsync(null)); Finish();
            Assert.AreEqual(0,platform.Requests); Assert.IsFalse(service.Applied.VSync); Assert.AreEqual(120,service.Applied.FrameRateLimit);
            service.SetPendingSafeGraphics(true,144); service.SetPendingDisplay(candidate);
            work.Push(service.ApplyPendingAsync(null)); Until(() => display.CanConfirm);
            display.Reject(display.TransactionId); Finish();
            Assert.IsTrue(service.Applied.VSync); Assert.AreEqual(144,safe.Actual.FrameRateLimit); Assert.AreEqual(144,service.Persisted.FrameRateLimit);
            Assert.AreEqual(1280,service.Applied.WindowWidth);
        }
        [Test] public void RecoveryRecordFailureIsDirtyAndCanBeRetried()
        {
            var store = new RecoveryStore(); display = new DisplayTransactionCoordinator(service,platform,() => time,recoveryRepository: store);
            Begin(); display.Confirm(display.TransactionId); Finish();
            Assert.AreEqual(PersistenceOutcome.PersistenceFailed,display.LastResult.PersistenceOutcome); Assert.IsTrue(service.IsPersistenceDirty);
            store.Fail = false; Assert.IsTrue(service.RetryPersistApplied().Success); Assert.IsFalse(service.IsPersistenceDirty);
        }
        [Test] public void UnavailableObservationNeverReportsSuccessfulSwitch()
        {
            platform.Actual = null; work.Push(display.ApplyAsync(candidate)); platform.IgnoreRequests = true; Finish();
            Assert.AreEqual(DisplayVerificationOutcome.ObservationUnavailable,display.LastResult.VerificationOutcome);
            Assert.AreEqual(DisplayRecoveryOutcome.RollbackFailed,display.LastResult.RecoveryOutcome);
        }
        [Test] public void ReconnectedStableDisplayMatchesOriginalPreference()
        {
            var intent = new DisplayIntent(1920,1080,SettingsWindowIntent.Borderless,60,1,1,"A");
            Assert.AreEqual(DisplayFallbackReason.DisplayUnavailable,new DisplayModeResolver().Resolve(intent,platform.GetCapabilities()).Mode.FallbackReason);
            var desktop = new ResolvedDisplayMode(1,1920,1080,60,1,SettingsWindowIntent.Borderless,"A");
            var caps = new DisplayCapabilities(new[] { new DisplayDeviceCapabilities(1,"A",1920,1040,desktop,new[] { desktop },"A") },
                new[] { SettingsWindowIntent.Borderless,SettingsWindowIntent.Windowed },true,"reconnected");
            var match = new DisplayModeResolver().Resolve(intent,caps).Mode;
            Assert.AreEqual(1,match.DisplayIndex); Assert.IsFalse(match.IsFallback); Assert.AreEqual("A",intent.PreferredDisplayId);
        }
        [Test] public void DelayedOldObservationCannotChangeNewTransaction()
        {
            var callbacks = new CallbackApplier(platform);
            display = new DisplayTransactionCoordinator(service,platform,() => time,applier: callbacks);
            Begin(); var old = callbacks.Callbacks[0]; display.Reject(display.TransactionId); Finish();
            Begin(); old(new DisplayObservationResult(DisplayVerificationOutcome.ObservationUnavailable));
            Assert.IsTrue(display.Confirm(display.TransactionId)); Finish();
            Assert.AreEqual(DisplayVerificationOutcome.Acceptable,display.LastResult.VerificationOutcome);
            Assert.AreEqual(PersistenceOutcome.Persisted,display.LastResult.PersistenceOutcome);
        }
        [Test] public void LastRecoveryTierUsesSafeWindowAfterDesktopFailure()
        {
            platform.Actual = new ResolvedDisplayMode(0,800,600,60,1,SettingsWindowIntent.Windowed);
            platform.IgnoreFirstRequests = 3; work.Push(display.ApplyAsync(candidate)); Finish();
            Assert.AreEqual(4,platform.Requests); Assert.AreEqual(DisplayRecoveryOutcome.RollbackDegraded,display.LastResult.RecoveryOutcome);
            Assert.AreEqual(DisplayFallbackReason.SafeWindow,display.Runtime.ResolvedDisplayMode.FallbackReason);
        }
        [Test] public void DisplayPreferenceRoundTripsThroughSerializer()
        {
            var s = new SettingsSnapshot(displayIntent: new DisplayIntent(2560,1440,SettingsWindowIntent.Exclusive,144000,1001,1,"monitor"));
            string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DisplaySettingsTests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var disk = new SettingsRepository(new SettingsPathProvider(directory), null);
                Assert.IsTrue(disk.Save(s).Success);
                var loaded = disk.Load(); Assert.AreEqual(s,loaded.Snapshot); Assert.IsTrue(loaded.Persisted);
            }
            finally { if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory,true); }
            Assert.AreEqual(s.DisplayIntent,s.WithAudio(0.2f,0.3f,0.4f).DisplayIntent);
        }
        private sealed class Repository : ISettingsRepository
        {
            public bool Fail; public int Saves;
            public SettingsLoadResult Load() => new SettingsLoadResult(new SettingsSnapshot(),true);
            public SettingsSaveResult Save(SettingsSnapshot value) { Saves++; return new SettingsSaveResult(Fail ? SettingsPersistenceOutcome.NotCommitted : SettingsPersistenceOutcome.Committed); }
        }
        private sealed class SafeApplier : ISettingsDomainApplier
        {
            public SettingsSnapshot Actual = new SettingsSnapshot(); public SettingsDomain Domain => SettingsDomain.SafeGraphics;
            public bool Apply(SettingsSnapshot s) { Actual = s.Copy(); return true; }
            public bool Restore(SettingsSnapshot s) => Apply(s);
            public SettingsSnapshot Observe() => Actual;
        }
        private sealed class RecoveryStore : IDisplayRecoveryRepository
        { public bool Fail = true; public bool Save(LastKnownGoodDisplayMode value) => !Fail; }
        private sealed class CallbackApplier : IDisplaySettingsApplier
        {
            private readonly Platform platform;
            public readonly List<Action<DisplayObservationResult>> Callbacks = new List<Action<DisplayObservationResult>>();
            public CallbackApplier(Platform platform) { this.platform = platform; }
            public IEnumerator RequestAndObserveAsync(IReadOnlyResolvedDisplayMode target,DisplayVerificationPolicy p,Action<DisplayObservationResult> completed)
            { Callbacks.Add(completed); platform.Request(target); completed(new DisplayObservationResult(DisplayVerificationOutcome.Acceptable,platform.Actual)); yield break; }
            public IEnumerator RestoreAndObserveAsync(IReadOnlyResolvedDisplayMode target,DisplayVerificationPolicy p,Action<DisplayObservationResult> completed) => RequestAndObserveAsync(target,p,completed);
        }
        private sealed class Platform : IDisplayPlatform
        {
            public int Requests, Observations, IgnoreFirstRequests; public bool IgnoreRequests; public string Fingerprint = "A";
            public ResolvedDisplayMode Actual = new ResolvedDisplayMode(0,1280,720,60,1,SettingsWindowIntent.Windowed);
            public DisplayCapabilities GetCapabilities()
            {
                var desktop = new ResolvedDisplayMode(0,1920,1080,60,1,SettingsWindowIntent.Borderless);
                return new DisplayCapabilities(new[] { new DisplayDeviceCapabilities(0,"A",1920,1040,desktop,new[] { desktop }) },
                    new[] { SettingsWindowIntent.Windowed,SettingsWindowIntent.Borderless,SettingsWindowIntent.Exclusive },true,Fingerprint);
            }
            public ResolvedDisplayMode Observe() { Observations++; return Actual; }
            public void Request(IReadOnlyResolvedDisplayMode m)
            { Requests++; if (!IgnoreRequests && Requests > IgnoreFirstRequests) Actual = new ResolvedDisplayMode(m.DisplayIndex,m.Width,m.Height,m.RefreshRateNumerator,m.RefreshRateDenominator,m.WindowMode,m.DisplayId); }
        }
    }
}
