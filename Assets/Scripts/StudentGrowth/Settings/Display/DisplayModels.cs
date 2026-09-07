using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace StudentGrowth
{
    public enum DisplayFallbackReason { None, DisplayUnavailable, UnsupportedWindowMode, DesktopMode, ClosestMode, WorkAreaClamped, SafeWindow }
    public enum DisplayRequestOutcome { NotRequested, Requested, RejectedBusy, CancelledByLifecycle, RequestException }
    public enum DisplayVerificationOutcome { NotObserved, Acceptable, ApplyUnacceptable, ObservationUnavailable, RejectedByPlayer, ConfirmationTimedOut }
    public enum DisplayRecoveryOutcome { NotRequired, RollbackSucceeded, RollbackDegraded, RollbackFailed }
    public enum PersistenceOutcome { NotAttempted, Persisted, PersistenceFailed }
    public enum DisplayTransactionPhase { Idle, Observing, Confirming, Recovering, Degraded }

    public interface IReadOnlyDisplayIntent
    {
        string PreferredDisplayId { get; }
        int PreferredDisplayIndex { get; }
        int Width { get; }
        int Height { get; }
        int RefreshRateNumerator { get; }
        int RefreshRateDenominator { get; }
        SettingsWindowIntent WindowMode { get; }
    }
    public sealed class DisplayIntent : IReadOnlyDisplayIntent, IEquatable<DisplayIntent>
    {
        public string PreferredDisplayId { get; }
        public int PreferredDisplayIndex { get; }
        public int Width { get; }
        public int Height { get; }
        public int RefreshRateNumerator { get; }
        public int RefreshRateDenominator { get; }
        public SettingsWindowIntent WindowMode { get; }
        public DisplayIntent(int width = 1280, int height = 720, SettingsWindowIntent mode = SettingsWindowIntent.Windowed,
            int numerator = 60, int denominator = 1, int displayIndex = 0, string displayId = "")
        {
            if (width <= 0 || height <= 0 || numerator <= 0 || denominator <= 0 || !Enum.IsDefined(typeof(SettingsWindowIntent), mode))
                throw new ArgumentOutOfRangeException(nameof(width));
            Width = Math.Min(width, 16384); Height = Math.Min(height, 16384); WindowMode = mode;
            int divisor = Gcd(numerator, denominator);
            RefreshRateNumerator = numerator / divisor; RefreshRateDenominator = denominator / divisor;
            PreferredDisplayIndex = Math.Max(0, displayIndex); PreferredDisplayId = displayId ?? "";
        }
        private static int Gcd(int a, int b) { while (b != 0) { int n = a % b; a = b; b = n; } return a; }
        public bool Equals(DisplayIntent b) => b != null && Width == b.Width && Height == b.Height && WindowMode == b.WindowMode &&
            PreferredDisplayId == b.PreferredDisplayId && PreferredDisplayIndex == b.PreferredDisplayIndex &&
            RefreshRateNumerator == b.RefreshRateNumerator && RefreshRateDenominator == b.RefreshRateDenominator;
        public override bool Equals(object obj) => Equals(obj as DisplayIntent);
        public override int GetHashCode() => Width ^ Height ^ RefreshRateNumerator ^ PreferredDisplayIndex;
    }
    public interface IReadOnlyResolvedDisplayMode
    {
        string DisplayId { get; }
        int DisplayIndex { get; }
        int Width { get; }
        int Height { get; }
        int RefreshRateNumerator { get; }
        int RefreshRateDenominator { get; }
        SettingsWindowIntent WindowMode { get; }
        bool IsFallback { get; }
        DisplayFallbackReason FallbackReason { get; }
    }
    public sealed class ResolvedDisplayMode : IReadOnlyResolvedDisplayMode
    {
        public string DisplayId { get; }
        public int DisplayIndex { get; }
        public int Width { get; }
        public int Height { get; }
        public int RefreshRateNumerator { get; }
        public int RefreshRateDenominator { get; }
        public SettingsWindowIntent WindowMode { get; }
        public bool IsFallback => FallbackReason != DisplayFallbackReason.None;
        public DisplayFallbackReason FallbackReason { get; }
        public double Hertz => RefreshRateDenominator > 0 ? (double)RefreshRateNumerator / RefreshRateDenominator : 0;
        public ResolvedDisplayMode(int displayIndex, int width, int height, int numerator, int denominator,
            SettingsWindowIntent mode, string displayId = "", DisplayFallbackReason reason = DisplayFallbackReason.None)
        { DisplayIndex = displayIndex; Width = width; Height = height; RefreshRateNumerator = numerator; RefreshRateDenominator = denominator;
            WindowMode = mode; DisplayId = displayId ?? ""; FallbackReason = reason; }
        public ResolvedDisplayMode WithReason(DisplayFallbackReason reason) => new ResolvedDisplayMode(DisplayIndex, Width, Height,
            RefreshRateNumerator, RefreshRateDenominator, WindowMode, DisplayId, reason);
        public override string ToString() => $"{Width}×{Height} {Hertz:0.##} Hz / {WindowMode} / 显示器 {DisplayIndex + 1}";
    }
    public sealed class LastKnownGoodDisplayMode
    {
        public ResolvedDisplayMode Mode { get; }
        public string HardwareFingerprint { get; }
        public LastKnownGoodDisplayMode(ResolvedDisplayMode mode, string fingerprint) { Mode = mode; HardwareFingerprint = fingerprint; }
    }
    public sealed class RuntimeSettingsState
    {
        public ResolvedDisplayMode ResolvedDisplayMode { get; internal set; }
        public LastKnownGoodDisplayMode LastKnownGoodDisplayMode { get; internal set; }
    }
    public sealed class DisplayDeviceCapabilities
    {
        public int Index { get; }
        public string StableId { get; }
        public string Name { get; }
        public int WorkWidth { get; }
        public int WorkHeight { get; }
        public ResolvedDisplayMode Desktop { get; }
        public IReadOnlyList<ResolvedDisplayMode> Modes { get; }
        public DisplayDeviceCapabilities(int index, string name, int workWidth, int workHeight, ResolvedDisplayMode desktop,
            IEnumerable<ResolvedDisplayMode> modes, string stableId = "")
        {
            Index = index; Name = name; StableId = stableId; WorkWidth = workWidth; WorkHeight = workHeight; Desktop = desktop;
            Modes = Array.AsReadOnly(modes.Where(m => m.DisplayIndex == index && m.Width > 0 && m.Height > 0 && m.RefreshRateDenominator > 0 && m.Hertz >= 1 && m.Hertz <= 1000)
                .GroupBy(m => new { m.Width, m.Height, Rate = (decimal)m.RefreshRateNumerator / m.RefreshRateDenominator })
                .Select(g => g.First()).OrderBy(m => m.Width).ThenBy(m => m.Height).ThenBy(m => m.Hertz).ToArray());
        }
    }
    public interface IReadOnlyDisplayCapabilities
    {
        IReadOnlyList<DisplayDeviceCapabilities> Displays { get; }
        IReadOnlyList<SettingsWindowIntent> WindowModes { get; }
        bool CanObserve { get; }
        string Fingerprint { get; }
    }
    public sealed class DisplayCapabilities : IReadOnlyDisplayCapabilities
    {
        public IReadOnlyList<DisplayDeviceCapabilities> Displays { get; }
        public IReadOnlyList<SettingsWindowIntent> WindowModes { get; }
        public bool CanObserve { get; }
        public string Fingerprint { get; }
        public DisplayCapabilities(IEnumerable<DisplayDeviceCapabilities> displays, IEnumerable<SettingsWindowIntent> modes, bool canObserve, string fingerprint)
        { Displays = Array.AsReadOnly(displays.ToArray()); WindowModes = Array.AsReadOnly(modes.ToArray()); CanObserve = canObserve; Fingerprint = fingerprint; }
    }
    public sealed class DisplayVerificationPolicy
    {
        public double ObservationSeconds { get; set; } = 2;
        public int StableFrames { get; set; } = 3;
        public double RefreshTolerance { get; set; } = 0.6;
        public double ConfirmationSeconds { get; set; } = 15;
        public double FocusGraceSeconds { get; set; } = 1.5;
        public double BackgroundSeconds { get; set; } = 30;
    }
    public sealed class DisplayObservationResult
    {
        public DisplayVerificationOutcome Outcome { get; }
        public ResolvedDisplayMode ActualMode { get; }
        public bool RequestException { get; }
        public DisplayObservationResult(DisplayVerificationOutcome outcome, ResolvedDisplayMode actual = null, bool requestException = false)
        { Outcome = outcome; ActualMode = actual; RequestException = requestException; }
    }
    public sealed class DisplayTransactionResult
    {
        public DisplayRequestOutcome RequestOutcome { get; internal set; }
        public DisplayVerificationOutcome VerificationOutcome { get; internal set; }
        public DisplayRecoveryOutcome RecoveryOutcome { get; internal set; }
        public PersistenceOutcome PersistenceOutcome { get; internal set; }
        public ResolvedDisplayMode ActualMode { get; internal set; }
        public string ErrorCode { get; internal set; }
    }
    public interface IDisplaySettingsApplier
    {
        IEnumerator RequestAndObserveAsync(IReadOnlyResolvedDisplayMode target, DisplayVerificationPolicy policy, Action<DisplayObservationResult> completed);
        IEnumerator RestoreAndObserveAsync(IReadOnlyResolvedDisplayMode target, DisplayVerificationPolicy policy, Action<DisplayObservationResult> completed);
    }
    public interface IDisplayPlatform
    {
        DisplayCapabilities GetCapabilities();
        ResolvedDisplayMode Observe();
        void Request(IReadOnlyResolvedDisplayMode mode);
    }
}
