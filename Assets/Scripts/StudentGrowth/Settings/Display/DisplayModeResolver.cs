using System;
using System.Linq;

namespace StudentGrowth
{
    public sealed class DisplayResolutionResult
    {
        public ResolvedDisplayMode Mode { get; }
        public string ErrorCode { get; }
        public DisplayResolutionResult(ResolvedDisplayMode mode, string error = null) { Mode = mode; ErrorCode = error; }
    }
    public interface IDisplayModeResolver
    {
        DisplayResolutionResult Resolve(IReadOnlyDisplayIntent intent, IReadOnlyDisplayCapabilities capabilities);
    }
    public sealed class DisplayModeResolver : IDisplayModeResolver
    {
        public DisplayResolutionResult Resolve(IReadOnlyDisplayIntent intent, IReadOnlyDisplayCapabilities capabilities)
        {
            if (!capabilities.CanObserve || capabilities.Displays.Count == 0) return new DisplayResolutionResult(null, "ObservationUnavailable");
            var device = !string.IsNullOrEmpty(intent.PreferredDisplayId)
                ? capabilities.Displays.FirstOrDefault(d => d.StableId == intent.PreferredDisplayId)
                : capabilities.Displays.FirstOrDefault(d => d.Index == intent.PreferredDisplayIndex);
            var reason = DisplayFallbackReason.None;
            if (device == null) { device = capabilities.Displays[0]; reason = DisplayFallbackReason.DisplayUnavailable; }
            var mode = intent.WindowMode;
            if (!capabilities.WindowModes.Contains(mode)) { mode = SettingsWindowIntent.Windowed; reason = DisplayFallbackReason.UnsupportedWindowMode; }
            var desktop = device.Desktop;
            int width = intent.Width, height = intent.Height;
            int numerator = intent.RefreshRateNumerator, denominator = intent.RefreshRateDenominator;
            if (mode == SettingsWindowIntent.Borderless)
            {
                width = desktop.Width; height = desktop.Height; numerator = desktop.RefreshRateNumerator; denominator = desktop.RefreshRateDenominator;
                if (reason == DisplayFallbackReason.None && (width != intent.Width || height != intent.Height || Math.Abs(desktop.Hertz - (double)intent.RefreshRateNumerator / intent.RefreshRateDenominator) > 0.6)) reason = DisplayFallbackReason.DesktopMode;
            }
            else if (mode == SettingsWindowIntent.Windowed)
            {
                width = Math.Min(width, device.WorkWidth); height = Math.Min(height, device.WorkHeight);
                numerator = desktop.RefreshRateNumerator; denominator = desktop.RefreshRateDenominator;
                if (reason == DisplayFallbackReason.None && (width != intent.Width || height != intent.Height)) reason = DisplayFallbackReason.WorkAreaClamped;
            }
            else
            {
                var best = device.Modes.OrderBy(m => Math.Abs((double)m.Width / m.Height - (double)intent.Width / intent.Height))
                    .ThenBy(m => Math.Abs((long)m.Width * m.Height - (long)intent.Width * intent.Height))
                    .ThenBy(m => Math.Abs(m.Hertz - (double)intent.RefreshRateNumerator / intent.RefreshRateDenominator)).FirstOrDefault();
                if (best == null) { best = desktop; mode = SettingsWindowIntent.Borderless; reason = DisplayFallbackReason.DesktopMode; }
                else if (reason == DisplayFallbackReason.None && (best.Width != width || best.Height != height || Math.Abs(best.Hertz - (double)numerator / denominator) > 0.6)) reason = DisplayFallbackReason.ClosestMode;
                width = best.Width; height = best.Height; numerator = best.RefreshRateNumerator; denominator = best.RefreshRateDenominator;
            }
            if (width <= 0 || height <= 0 || denominator <= 0 || numerator <= 0) return new DisplayResolutionResult(null, "InvalidCapabilities");
            return new DisplayResolutionResult(new ResolvedDisplayMode(device.Index, width, height, numerator, denominator, mode, device.StableId, reason));
        }

        public static bool Accepts(IReadOnlyResolvedDisplayMode target, ResolvedDisplayMode actual, DisplayCapabilities caps, double tolerance)
        {
            if (actual == null || target == null || actual.DisplayIndex != target.DisplayIndex || actual.DisplayId != target.DisplayId || actual.WindowMode != target.WindowMode ||
                actual.Width != target.Width || actual.Height != target.Height || actual.RefreshRateDenominator <= 0 || target.RefreshRateDenominator <= 0) return false;
            var device = caps.Displays.FirstOrDefault(d => d.Index == actual.DisplayIndex);
            return device != null && (actual.WindowMode != SettingsWindowIntent.Windowed || (actual.Width <= device.WorkWidth && actual.Height <= device.WorkHeight)) &&
                Math.Abs(actual.Hertz - (double)target.RefreshRateNumerator / target.RefreshRateDenominator) <= tolerance;
        }
    }
}
