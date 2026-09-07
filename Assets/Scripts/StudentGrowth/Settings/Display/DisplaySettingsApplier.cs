using System;
using System.Collections;

namespace StudentGrowth
{
    public sealed class DisplaySettingsApplier : IDisplaySettingsApplier
    {
        private readonly IDisplayPlatform platform;
        private readonly Func<double> now;
        public DisplaySettingsApplier(IDisplayPlatform platform, Func<double> now) { this.platform = platform; this.now = now; }
        public IEnumerator RestoreAndObserveAsync(IReadOnlyResolvedDisplayMode target, DisplayVerificationPolicy policy, Action<DisplayObservationResult> completed) =>
            RequestAndObserveAsync(target, policy, completed);
        public IEnumerator RequestAndObserveAsync(IReadOnlyResolvedDisplayMode target, DisplayVerificationPolicy policy, Action<DisplayObservationResult> completed)
        {
            bool failed = false;
            try { platform.Request(target); } catch (Exception) { failed = true; }
            if (failed) { completed(new DisplayObservationResult(DisplayVerificationOutcome.NotObserved, requestException: true)); yield break; }
            // Unity applies display requests at a later frame boundary. Never echo the requested mode.
            yield return null; yield return null;
            double deadline = now() + Math.Max(0.01, policy.ObservationSeconds);
            int stable = 0;
            ResolvedDisplayMode actual = null;
            do
            {
                bool matches = false;
                try { actual = platform.Observe(); matches = DisplayModeResolver.Accepts(target, actual, platform.GetCapabilities(), policy.RefreshTolerance); }
                catch (Exception) { actual = null; }
                stable = matches ? stable + 1 : 0;
                if (stable >= Math.Max(1, policy.StableFrames))
                { completed(new DisplayObservationResult(DisplayVerificationOutcome.Acceptable, actual.WithReason(target.FallbackReason))); yield break; }
                yield return null;
            } while (now() < deadline);
            completed(new DisplayObservationResult(actual == null ? DisplayVerificationOutcome.ObservationUnavailable : DisplayVerificationOutcome.ApplyUnacceptable, actual));
        }
    }
}
