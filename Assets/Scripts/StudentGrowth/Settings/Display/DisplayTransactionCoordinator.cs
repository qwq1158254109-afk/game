using System;
using System.Collections;
using System.Collections.Generic;

namespace StudentGrowth
{
    // Scene-independent, pure state machine. The existing settings owner drives its coroutine.
    public sealed class DisplayTransactionCoordinator
    {
        private readonly SettingsService service;
        private readonly IDisplayPlatform platform;
        private readonly IDisplaySettingsApplier applier;
        private readonly IDisplayModeResolver resolver;
        private readonly Func<double> now;
        private readonly IDisplayRecoveryRepository recoveryRepository;
        public bool IsRecoveryPersistenceDirty { get; private set; }
        private bool focused = true, paused, rollbackRequired, quit, confirmed, rejected;
        private double requestTime, focusLostTime, lastTick;
        public DisplayVerificationPolicy Policy { get; }
        public RuntimeSettingsState Runtime { get; } = new RuntimeSettingsState();
        public DisplayTransactionPhase Phase { get; private set; }
        public string TransactionId { get; private set; }
        public DisplayTransactionResult LastResult { get; private set; }
        public double RemainingSeconds { get; private set; }
        public bool IsBusy => Phase == DisplayTransactionPhase.Observing || Phase == DisplayTransactionPhase.Confirming || Phase == DisplayTransactionPhase.Recovering;
        public bool CanConfirm => Phase == DisplayTransactionPhase.Confirming && focused && !paused && !rollbackRequired && !quit;
        public bool RollbackRequired => rollbackRequired;
        public event Action Changed;
        public DisplayCapabilities Capabilities { get { try { return platform.GetCapabilities(); } catch { return new DisplayCapabilities(new DisplayDeviceCapabilities[0], new SettingsWindowIntent[0], false, ""); } } }

        public DisplayTransactionCoordinator(SettingsService service, IDisplayPlatform platform, Func<double> now,
            DisplayVerificationPolicy policy = null, IDisplaySettingsApplier applier = null, IDisplayRecoveryRepository recoveryRepository = null)
        {
            this.service = service; this.platform = platform; this.now = now; Policy = policy ?? new DisplayVerificationPolicy();
            this.recoveryRepository = recoveryRepository;
            this.applier = applier ?? new DisplaySettingsApplier(platform, now); resolver = new DisplayModeResolver();
            service.AttachDisplay(this);
            var actual = Observe();
            if (actual != null) Remember(actual);
        }
        private ResolvedDisplayMode Observe() { try { return platform.Observe(); } catch { return null; } }
        private void Remember(ResolvedDisplayMode actual)
        {
            Runtime.ResolvedDisplayMode = actual;
            Runtime.LastKnownGoodDisplayMode = new LastKnownGoodDisplayMode(actual, Capabilities.Fingerprint);
            IsRecoveryPersistenceDirty = recoveryRepository != null;
        }
        public bool RetryPersistRecovery()
        {
            if (recoveryRepository == null || !IsRecoveryPersistenceDirty) return true;
            try { IsRecoveryPersistenceDirty = !recoveryRepository.Save(Runtime.LastKnownGoodDisplayMode); }
            catch { IsRecoveryPersistenceDirty = true; }
            return !IsRecoveryPersistenceDirty;
        }
        public bool Confirm(string id)
        { if (id != TransactionId || !CanConfirm) return false; confirmed = true; return true; }
        public bool Reject(string id)
        { if (id != TransactionId || !IsBusy || Phase == DisplayTransactionPhase.Recovering) return false; rejected = true; rollbackRequired = true; return true; }
        public void ViewClosed() { if (IsBusy) { rollbackRequired = true; rejected = true; } }
        public void SceneUnloaded() { if (IsBusy) { rollbackRequired = true; LastResult.RequestOutcome = DisplayRequestOutcome.CancelledByLifecycle; } }
        public void SetFocus(bool value)
        {
            if (focused == value) return;
            focused = value;
            if (!value) focusLostTime = now();
            else
            {
                if (IsBusy && now() - focusLostTime >= Policy.BackgroundSeconds) rollbackRequired = true;
                RemainingSeconds = Math.Max(5, RemainingSeconds);
                lastTick = now();
            }
            Publish();
        }
        public void SetPaused(bool value) { paused = value; if (value && IsBusy) rollbackRequired = true; lastTick = now(); Publish(); }
        public void Quit()
        {
            quit = true;
            if (IsBusy) { LastResult.RequestOutcome = DisplayRequestOutcome.CancelledByLifecycle; TransactionId = null; Phase = DisplayTransactionPhase.Idle; }
            // Never commit a candidate or wait for frame-based recovery during process shutdown.
        }
        public IEnumerator ApplyAsync(DisplayIntent intent, bool startup = false, Action<DisplayTransactionResult> completed = null)
        {
            if (quit || IsBusy || Phase == DisplayTransactionPhase.Degraded || !service.BeginDisplayTransaction())
            { completed?.Invoke(new DisplayTransactionResult { RequestOutcome = DisplayRequestOutcome.RejectedBusy }); yield break; }
            bool wasReady = startup;
            TransactionId = Guid.NewGuid().ToString("N"); string id = TransactionId;
            LastResult = new DisplayTransactionResult();
            rollbackRequired = rejected = confirmed = false;
            requestTime = now(); lastTick = requestTime;
            if (!focused) focusLostTime = requestTime;
            var caps = Capabilities;
            if (Runtime.LastKnownGoodDisplayMode == null)
            {
                var current = Observe(); if (current != null) Remember(current);
            }
            var target = resolver.Resolve(intent, caps);
            if (target.Mode == null || Runtime.LastKnownGoodDisplayMode == null)
            {
                LastResult.VerificationOutcome = DisplayVerificationOutcome.ObservationUnavailable;
                LastResult.ErrorCode = "ObservationUnavailable";
                Finish(false, wasReady, completed); yield break;
            }
            Phase = DisplayTransactionPhase.Observing; LastResult.RequestOutcome = DisplayRequestOutcome.Requested; Publish();
            DisplayObservationResult observation = null;
            yield return applier.RequestAndObserveAsync(target.Mode, Policy, value => { if (id == TransactionId && !quit) observation = value; });
            if (quit || id != TransactionId) yield break;
            LastResult.VerificationOutcome = observation?.Outcome ?? DisplayVerificationOutcome.ObservationUnavailable;
            if (observation != null && observation.RequestException) LastResult.RequestOutcome = DisplayRequestOutcome.RequestException;
            Runtime.ResolvedDisplayMode = observation?.ActualMode;
            if (observation?.Outcome == DisplayVerificationOutcome.Acceptable && !rollbackRequired)
            {
                Phase = DisplayTransactionPhase.Confirming; RemainingSeconds = Policy.ConfirmationSeconds; lastTick = now(); Publish();
                while (!startup && !confirmed && !rollbackRequired && !quit)
                {
                    double time = now();
                    if (!focused && time - focusLostTime >= Policy.BackgroundSeconds) rollbackRequired = true;
                    if (focused || time - requestTime < Policy.FocusGraceSeconds) RemainingSeconds -= Math.Max(0, time - lastTick);
                    lastTick = time;
                    if (RemainingSeconds <= 0) { LastResult.VerificationOutcome = DisplayVerificationOutcome.ConfirmationTimedOut; rollbackRequired = true; }
                    // Detect external mode changes / monitor disconnect even while waiting for input.
                    if (focused && !DisplayModeResolver.Accepts(target.Mode, Observe(), Capabilities, Policy.RefreshTolerance))
                    { LastResult.VerificationOutcome = DisplayVerificationOutcome.ApplyUnacceptable; rollbackRequired = true; }
                    Publish(); yield return null;
                }
                if (quit || id != TransactionId) yield break;
                if (!rollbackRequired)
                {
                    // Recheck at the commit boundary: confirmation cannot approve a now-invalid display.
                    var actual = Observe();
                    if (DisplayModeResolver.Accepts(target.Mode, actual, Capabilities, Policy.RefreshTolerance))
                    {
                        Remember(actual.WithReason(target.Mode.FallbackReason));
                        if (!startup)
                        {
                            bool saved = service.ConfirmDisplay(intent).Success;
                            bool recoverySaved = RetryPersistRecovery();
                            LastResult.PersistenceOutcome = saved && recoverySaved ? PersistenceOutcome.Persisted : PersistenceOutcome.PersistenceFailed;
                        }
                        else RetryPersistRecovery();
                        LastResult.ActualMode = Runtime.ResolvedDisplayMode;
                        Finish(false, wasReady, completed); yield break;
                    }
                    LastResult.VerificationOutcome = DisplayVerificationOutcome.ApplyUnacceptable;
                }
            }
            if (rejected) LastResult.VerificationOutcome = DisplayVerificationOutcome.RejectedByPlayer;
            // A paused application must restore on resume before accepting any input.
            while (paused && !quit) yield return null;
            if (quit) yield break;
            yield return RecoverAsync(id);
            if (!quit) Finish(LastResult.RecoveryOutcome == DisplayRecoveryOutcome.RollbackFailed, wasReady, completed);
        }
        public IEnumerator RetryRecoveryAsync()
        {
            if (Phase != DisplayTransactionPhase.Degraded || quit) yield break;
            TransactionId = Guid.NewGuid().ToString("N"); LastResult = new DisplayTransactionResult();
            yield return RecoverAsync(TransactionId);
            if (!quit) Finish(LastResult.RecoveryOutcome == DisplayRecoveryOutcome.RollbackFailed, false, null);
        }
        private IEnumerator RecoverAsync(string id)
        {
            Phase = DisplayTransactionPhase.Recovering; Publish();
            var caps = Capabilities;
            var candidates = new List<ResolvedDisplayMode>();
            var good = Runtime.LastKnownGoodDisplayMode;
            bool compatible = good != null && good.HardwareFingerprint == caps.Fingerprint;
            if (compatible) candidates.Add(good.Mode);
            var current = Observe();
            var device = System.Linq.Enumerable.FirstOrDefault(caps.Displays, d => d.Index == (current?.DisplayIndex ?? 0));
            if (device == null && caps.Displays.Count > 0) device = caps.Displays[0];
            if (device != null)
            {
                candidates.Add(new ResolvedDisplayMode(device.Index, device.Desktop.Width, device.Desktop.Height,
                    device.Desktop.RefreshRateNumerator, device.Desktop.RefreshRateDenominator, SettingsWindowIntent.Borderless, device.StableId, DisplayFallbackReason.DesktopMode));
                candidates.Add(new ResolvedDisplayMode(device.Index, Math.Min(1280, device.WorkWidth), Math.Min(720, device.WorkHeight),
                    device.Desktop.RefreshRateNumerator, device.Desktop.RefreshRateDenominator, SettingsWindowIntent.Windowed, device.StableId, DisplayFallbackReason.SafeWindow));
            }
            for (int i = 0; i < candidates.Count; i++)
            {
                DisplayObservationResult observation = null;
                yield return applier.RestoreAndObserveAsync(candidates[i], Policy, value => { if (id == TransactionId && !quit) observation = value; });
                if (quit || id != TransactionId) yield break;
                Runtime.ResolvedDisplayMode = observation?.ActualMode;
                if (observation?.Outcome != DisplayVerificationOutcome.Acceptable) continue;
                Remember(observation.ActualMode);
                RetryPersistRecovery();
                LastResult.ActualMode = observation.ActualMode;
                LastResult.RecoveryOutcome = compatible && i == 0 ? DisplayRecoveryOutcome.RollbackSucceeded : DisplayRecoveryOutcome.RollbackDegraded;
                yield break;
            }
            LastResult.ActualMode = Runtime.ResolvedDisplayMode;
            LastResult.RecoveryOutcome = DisplayRecoveryOutcome.RollbackFailed; LastResult.ErrorCode = "DISPLAY_RECOVERY_FAILED";
        }
        private void Finish(bool degraded, bool startup, Action<DisplayTransactionResult> completed)
        {
            Phase = degraded ? DisplayTransactionPhase.Degraded : DisplayTransactionPhase.Idle;
            rollbackRequired = false; TransactionId = null;
            service.EndDisplayTransaction(degraded);
            if (startup && !degraded) service.CancelEditing();
            Publish(); completed?.Invoke(LastResult);
        }
        private void Publish()
        {
            if (Changed == null) return;
            foreach (Action handler in Changed.GetInvocationList()) try { handler(); } catch { /* A destroyed view cannot strand recovery. */ }
        }
    }
}
