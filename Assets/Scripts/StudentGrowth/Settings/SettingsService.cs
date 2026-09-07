using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace StudentGrowth
{
    [Flags] public enum SettingsDomain { None = 0, Audio = 1, Localization = 2, Accessibility = 4, SafeGraphics = 8, Display = 16 }
    public enum SettingsServiceState { Uninitialized, Ready, Editing, NormalizingPreview, ApplyingSafeTransaction, Recovering, Degraded, ApplyingDisplayTransaction }
    public enum SettingsDegradedReason { PreviewRestoreFailed, TransactionRollbackFailed, RuntimeObservationUnavailable, SafeDefaultsApplyFailed, UnexpectedLifecycleInterruption }

    public sealed class SettingsDegradedInfo
    {
        public SettingsDegradedReason Reason { get; }
        public SettingsDomain FailedDomains { get; }
        public SettingsDegradedInfo(SettingsDegradedReason reason, SettingsDomain domains) { Reason = reason; FailedDomains = domains; }
    }
    public sealed class SettingsCommandResult
    {
        public bool Success { get; }
        public string ErrorCode { get; }
        public SettingsCommandResult(bool success, string errorCode = null) { Success = success; ErrorCode = errorCode; }
    }
    public sealed class SettingsApplySummary
    {
        public bool RuntimeAccepted { get; }
        public SettingsSaveResult Persistence { get; }
        public string ErrorCode { get; }
        public SettingsApplySummary(bool accepted, SettingsSaveResult persistence = null, string errorCode = null)
        { RuntimeAccepted = accepted; Persistence = persistence; ErrorCode = errorCode; }
    }
    public interface ISettingsDomainApplier
    {
        SettingsDomain Domain { get; }
        bool Apply(SettingsSnapshot settings);
        bool Restore(SettingsSnapshot settings);
        // Must read the runtime implementation, not echo the requested DTO.
        SettingsSnapshot Observe();
    }
    public interface ISettingsStateReader
    {
        SettingsSnapshot Defaults { get; }
        SettingsSnapshot Persisted { get; }
        SettingsSnapshot Applied { get; }
        SettingsSnapshot Pending { get; }
        bool HasPendingChanges { get; }
        bool IsPersistenceDirty { get; }
        IReadOnlyDictionary<SettingsDomain, SettingsSnapshot> Runtime { get; }
    }
    public interface ISettingsService : ISettingsStateReader
    {
        SettingsServiceState State { get; }
        SettingsDegradedInfo DegradedInfo { get; }
        event Action StateChanged;
        SettingsCommandResult BeginEdit();
        SettingsCommandResult PreviewAudio(float master, float music, float sfx);
        SettingsCommandResult PreviewLanguage(string languageCode);
        IEnumerator ApplyPendingAsync(Action<SettingsApplySummary> completed);
        IEnumerator CancelEditingAsync(Action<SettingsCommandResult> completed);
        SettingsSaveResult RetryPersistApplied();
        IEnumerator TryRecoverRuntimeStateAsync(Action<SettingsCommandResult> completed);
    }

    // Pure service. It is owned by the existing AudioSettingsController; no additional global manager.
    public sealed class SettingsService : ISettingsService
    {
        private readonly ISettingsRepository repository;
        private readonly List<ISettingsDomainApplier> appliers;
        private readonly Dictionary<SettingsDomain, SettingsSnapshot> runtime = new Dictionary<SettingsDomain, SettingsSnapshot>();
        private SettingsDomain previewed;
        private SettingsSnapshot previewBase;
        private bool hasPersisted;
        private bool notifying;
        public DisplayTransactionCoordinator Display { get; private set; }
        public void AttachDisplay(DisplayTransactionCoordinator display) { Display = display; }
        public SettingsServiceState State { get; private set; }
        public SettingsDegradedInfo DegradedInfo { get; private set; }
        public SettingsSnapshot Defaults { get; }
        public SettingsSnapshot Persisted { get; private set; }
        public SettingsSnapshot Applied { get; private set; }
        public SettingsSnapshot Pending { get; private set; }
        public bool IsReadOnly { get; private set; }
        public string Notice { get; private set; }
        public SettingsPersistenceOutcome LastPersistenceOutcome { get; private set; }
        public bool HasPendingChanges => !Pending.Equals(Applied);
        public bool IsPersistenceDirty => !hasPersisted || !Applied.Equals(Persisted) || (Display != null && Display.IsRecoveryPersistenceDirty);
        public SettingsDomain PreviewedDomains => previewed;
        public bool IsBusy => State != SettingsServiceState.Ready && State != SettingsServiceState.Editing && State != SettingsServiceState.Degraded;
        public IReadOnlyDictionary<SettingsDomain, SettingsSnapshot> Runtime =>
            new ReadOnlyDictionary<SettingsDomain, SettingsSnapshot>(new Dictionary<SettingsDomain, SettingsSnapshot>(runtime));
        public event Action StateChanged;

        public SettingsService(ISettingsRepository repository, IEnumerable<ISettingsDomainApplier> appliers, SettingsSnapshot defaults = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.appliers = appliers.OrderBy(a => (int)a.Domain).ToList();
            if (this.appliers.Select(a => a.Domain).Distinct().Count() != this.appliers.Count ||
                this.appliers.Any(a => a.Domain != SettingsDomain.Audio && a.Domain != SettingsDomain.Localization && a.Domain != SettingsDomain.Accessibility && a.Domain != SettingsDomain.SafeGraphics))
                throw new ArgumentException("Each applier must own one unique supported domain");
            Defaults = (defaults ?? new SettingsSnapshot()).Copy();
            Applied = Defaults.Copy(); Pending = Defaults.Copy(); Persisted = Defaults.Copy(); previewBase = Defaults.Copy();
        }

        public void Initialize()
        {
            if (State != SettingsServiceState.Uninitialized) return;
            State = SettingsServiceState.Recovering;
            SettingsLoadResult load;
            try { load = repository.Load(); }
            catch (Exception) { load = new SettingsLoadResult(Defaults.Copy(), false, true, "SETTINGS_LOAD_UNAVAILABLE"); }
            IsReadOnly = load.ReadOnly; Notice = load.Notice; hasPersisted = load.Persisted;
            Applied = load.Snapshot.Copy(); Persisted = load.Snapshot.Copy(); Pending = Applied.Copy(); previewBase = Applied.Copy();
            var failed = ApplyAndObserveAll(Applied);
            if (failed != SettingsDomain.None)
            {
                // Unknown initial runtime cannot be represented as a successful startup.
                EnterDegraded(SettingsDegradedReason.RuntimeObservationUnavailable, failed); return;
            }
            State = SettingsServiceState.Ready;
            if (IsPersistenceDirty && !IsReadOnly)
            {
                var saved = PersistApplied();
                if (saved.Success) Notice = load.Notice;
            }
            Publish();
        }

        public SettingsCommandResult BeginEdit()
        {
            if (notifying || State != SettingsServiceState.Ready) return Reject("SETTINGS_NOT_READY");
            Pending = Applied.Copy(); previewBase = Applied.Copy(); previewed = SettingsDomain.None;
            State = SettingsServiceState.Editing; Publish(); return Accept();
        }
        public SettingsCommandResult PreviewAudio(float master, float music, float sfx)
        {
            try { return Preview(SettingsDomain.Audio, Pending.WithAudio(master, music, sfx)); }
            catch (ArgumentException) { return Reject("SETTINGS_INVALID_VALUE"); }
        }
        public SettingsCommandResult PreviewLanguage(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode)) return Reject("SETTINGS_INVALID_LANGUAGE");
            return Preview(SettingsDomain.Localization, Pending.WithLanguage(languageCode));
        }
        private SettingsCommandResult Preview(SettingsDomain domain, SettingsSnapshot next)
        {
            if (notifying || State != SettingsServiceState.Editing) return Reject("SETTINGS_NOT_EDITING");
            var applier = appliers.Find(a => a.Domain == domain);
            if (applier == null) return Reject("SETTINGS_DOMAIN_UNAVAILABLE");
            Pending = next.Copy(); previewed |= domain; // Mark before invoking code that may partially fail.
            State = SettingsServiceState.NormalizingPreview;
            if (!InvokeAndObserve(applier, Pending, false))
            {
                if (!InvokeAndObserve(applier, previewBase, true))
                { EnterDegraded(SettingsDegradedReason.PreviewRestoreFailed, domain); return Reject("SETTINGS_PREVIEW_RESTORE_FAILED"); }
                State = SettingsServiceState.Editing; Publish(); return Reject("SETTINGS_PREVIEW_FAILED");
            }
            State = SettingsServiceState.Editing; Notice = null; Publish(); return Accept();
        }
        public SettingsCommandResult SetPendingSafeGraphics(bool vSync, int limit)
        {
            if (notifying || State != SettingsServiceState.Editing) return Reject("SETTINGS_NOT_EDITING");
            if (!appliers.Any(a => a.Domain == SettingsDomain.SafeGraphics)) return Reject("SETTINGS_DOMAIN_UNAVAILABLE");
            Pending = Pending.WithSafeGraphics(vSync, limit); Publish(); return Accept();
        }
        public SettingsCommandResult ResetPendingToDefaults()
        {
            if (notifying || State != SettingsServiceState.Editing) return Reject("SETTINGS_NOT_EDITING");
            // Only implemented domains are reset; display intent and unavailable locale/content are preserved.
            Pending = Pending.WithAudio(Defaults.Master, Defaults.Music, Defaults.Sfx)
                .WithSafeGraphics(Defaults.VSync, Defaults.FrameRateLimit);
            Publish(); return Accept();
        }

        public SettingsApplySummary ApplyPending()
        {
            if (notifying || State != SettingsServiceState.Editing) return new SettingsApplySummary(false, errorCode: "SETTINGS_NOT_EDITING");
            State = SettingsServiceState.NormalizingPreview; Publish();
            if (!RestorePreview()) return new SettingsApplySummary(false, errorCode: "SETTINGS_PREVIEW_RESTORE_FAILED");
            State = SettingsServiceState.ApplyingSafeTransaction; Publish();
            var touched = new List<ISettingsDomainApplier>();
            foreach (var applier in appliers)
            {
                touched.Add(applier);
                if (InvokeAndObserve(applier, Pending, false)) continue;
                var failed = RestoreAll(touched, Applied);
                if (failed != SettingsDomain.None) EnterDegraded(SettingsDegradedReason.TransactionRollbackFailed, failed);
                else { State = SettingsServiceState.Editing; Notice = "SETTINGS_APPLY_FAILED"; Publish(); }
                return new SettingsApplySummary(false, errorCode: "SETTINGS_APPLY_FAILED");
            }
            Applied = Pending.WithDisplay(Applied.DisplayIntent); previewBase = Applied.Copy();
            // Keep the busy state during IO, preventing listener reentrancy and overlapping commits.
            var saved = PersistApplied();
            State = SettingsServiceState.Editing; Publish();
            return new SettingsApplySummary(true, saved);
        }
        internal bool BeginDisplayTransaction()
        {
            if (notifying || (State != SettingsServiceState.Editing && State != SettingsServiceState.Ready)) return false;
            State = SettingsServiceState.ApplyingDisplayTransaction; Publish(); return true;
        }
        internal SettingsSaveResult ConfirmDisplay(DisplayIntent intent)
        {
            Applied = Applied.WithDisplay(intent); Pending = Pending.WithDisplay(intent); previewBase = Applied.Copy();
            return PersistApplied();
        }
        internal void EndDisplayTransaction(bool degraded)
        {
            Pending = Pending.WithDisplay(Applied.DisplayIntent);
            if (degraded) EnterDegraded(SettingsDegradedReason.TransactionRollbackFailed, SettingsDomain.Display);
            else { DegradedInfo = null; State = SettingsServiceState.Editing; Publish(); }
        }
        public SettingsCommandResult SetPendingDisplay(DisplayIntent intent)
        {
            if (notifying || State != SettingsServiceState.Editing || intent == null) return Reject("SETTINGS_NOT_EDITING");
            Pending = Pending.WithDisplay(intent); Publish(); return Accept();
        }
        public SettingsCommandResult CancelEditing()
        {
            if (notifying || State != SettingsServiceState.Editing) return Reject("SETTINGS_NOT_EDITING");
            State = SettingsServiceState.NormalizingPreview; Publish();
            if (!RestorePreview()) return Reject("SETTINGS_PREVIEW_RESTORE_FAILED");
            Pending = Applied.Copy(); previewBase = Applied.Copy(); State = SettingsServiceState.Ready; Publish(); return Accept();
        }
        public SettingsSaveResult RetryPersistApplied()
        {
            if (notifying || (State != SettingsServiceState.Ready && State != SettingsServiceState.Editing))
                return new SettingsSaveResult(SettingsPersistenceOutcome.NotCommitted, "SETTINGS_RUNTIME_UNSAFE");
            if (!IsPersistenceDirty && !IsReadOnly) return new SettingsSaveResult(SettingsPersistenceOutcome.Committed);
            var previous = State; State = SettingsServiceState.ApplyingSafeTransaction;
            var saved = PersistApplied();
            if (Display != null && !Display.RetryPersistRecovery() && saved.Success) saved = new SettingsSaveResult(SettingsPersistenceOutcome.NotCommitted, "DISPLAY_RECOVERY_SAVE_FAILED");
            State = previous; Publish(); return saved;
        }
        private SettingsSaveResult PersistApplied()
        {
            SettingsSaveResult result;
            try { result = IsReadOnly ? new SettingsSaveResult(SettingsPersistenceOutcome.ReadOnly, "SETTINGS_READ_ONLY") : repository.Save(Applied.Copy()); }
            catch (Exception) { result = new SettingsSaveResult(SettingsPersistenceOutcome.Indeterminate, "SETTINGS_SAVE_UNCERTAIN"); }
            LastPersistenceOutcome = result.Outcome;
            if (result.Success) { Persisted = Applied.Copy(); hasPersisted = true; }
            if (result.Outcome == SettingsPersistenceOutcome.ReadOnly) IsReadOnly = true;
            Notice = result.ErrorCode;
            return result;
        }
        public SettingsCommandResult TryRecoverRuntimeState()
        {
            if (notifying || State != SettingsServiceState.Degraded) return Reject("SETTINGS_NOT_DEGRADED");
            if (DegradedInfo != null && (DegradedInfo.FailedDomains & SettingsDomain.Display) != 0) return Reject("DISPLAY_ASYNC_RECOVERY_REQUIRED");
            State = SettingsServiceState.Recovering; Publish();
            var failed = RestoreAll(appliers, Applied);
            if (failed != SettingsDomain.None) { EnterDegraded(SettingsDegradedReason.TransactionRollbackFailed, failed); return Reject("SETTINGS_RECOVERY_FAILED"); }
            DegradedInfo = null; previewed = SettingsDomain.None; Pending = Applied.Copy(); previewBase = Applied.Copy();
            State = SettingsServiceState.Ready; Notice = null; Publish(); return Accept();
        }
        public IEnumerator ApplyPendingAsync(Action<SettingsApplySummary> completed)
        { var result = ApplyPending(); if (result.RuntimeAccepted && Display != null && !Pending.DisplayIntent.Equals(Applied.DisplayIntent))
                yield return Display.ApplyAsync(Pending.DisplayIntent); completed?.Invoke(result); }
        public IEnumerator CancelEditingAsync(Action<SettingsCommandResult> completed)
        { var result = CancelEditing(); completed?.Invoke(result); yield break; }
        public IEnumerator TryRecoverRuntimeStateAsync(Action<SettingsCommandResult> completed)
        { var result = TryRecoverRuntimeState(); completed?.Invoke(result); yield break; }

        private bool RestorePreview()
        {
            var failed = RestoreAll(appliers.Where(a => (previewed & a.Domain) != 0), previewBase);
            if (failed != SettingsDomain.None) { EnterDegraded(SettingsDegradedReason.PreviewRestoreFailed, failed); return false; }
            previewed = SettingsDomain.None; return true;
        }
        private SettingsDomain RestoreAll(IEnumerable<ISettingsDomainApplier> domains, SettingsSnapshot target)
        {
            SettingsDomain failed = SettingsDomain.None;
            foreach (var a in domains.Reverse()) if (!InvokeAndObserve(a, target, true)) failed |= a.Domain;
            return failed;
        }
        private SettingsDomain ApplyAndObserveAll(SettingsSnapshot target)
        {
            SettingsDomain failed = SettingsDomain.None;
            foreach (var a in appliers) if (!InvokeAndObserve(a, target, false)) failed |= a.Domain;
            return failed;
        }
        private bool InvokeAndObserve(ISettingsDomainApplier applier, SettingsSnapshot target, bool restore)
        {
            try
            {
                bool success = restore ? applier.Restore(target) : applier.Apply(target);
                var observation = applier.Observe();
                runtime[applier.Domain] = observation?.Copy();
                return success && Matches(applier.Domain, target, observation);
            }
            catch (Exception) { runtime[applier.Domain] = null; return false; }
        }
        private static bool Matches(SettingsDomain domain, SettingsSnapshot a, SettingsSnapshot b)
        {
            if (b == null) return false;
            switch (domain)
            {
                case SettingsDomain.Audio: return Math.Abs(a.Master - b.Master) < 0.0001f && Math.Abs(a.Music - b.Music) < 0.0001f && Math.Abs(a.Sfx - b.Sfx) < 0.0001f;
                case SettingsDomain.Localization: return a.LanguageCode == b.LanguageCode;
                case SettingsDomain.SafeGraphics: return a.VSync == b.VSync && a.FrameRateLimit == b.FrameRateLimit;
                case SettingsDomain.Accessibility: return a.UiScale == b.UiScale && a.TextSpeed == b.TextSpeed && a.Subtitles == b.Subtitles;
                default: return false;
            }
        }
        private void EnterDegraded(SettingsDegradedReason reason, SettingsDomain failed)
        { State = SettingsServiceState.Degraded; DegradedInfo = new SettingsDegradedInfo(reason, failed); Notice = "SETTINGS_RUNTIME_UNSAFE"; Publish(); }
        private void Publish()
        {
            notifying = true;
            try
            {
                if (StateChanged != null) foreach (Action handler in StateChanged.GetInvocationList())
                    try { handler(); } catch (Exception) { /* View failures cannot alter transaction semantics. */ }
            }
            finally { notifying = false; }
        }
        private static SettingsCommandResult Accept() => new SettingsCommandResult(true);
        private static SettingsCommandResult Reject(string code) => new SettingsCommandResult(false, code);
    }
}
