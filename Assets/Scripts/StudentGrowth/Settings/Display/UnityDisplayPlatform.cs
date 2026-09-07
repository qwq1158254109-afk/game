using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StudentGrowth
{
    public sealed class UnityDisplayPlatform : IDisplayPlatform
    {
        private readonly List<DisplayInfo> layout = new List<DisplayInfo>();
        private AsyncOperation move;
        private IReadOnlyResolvedDisplayMode pendingResolution;
        private int resolutionFrame = -10;
        public DisplayCapabilities GetCapabilities()
        {
            bool standalone = !Application.isEditor && (Application.platform == RuntimePlatform.WindowsPlayer ||
                Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.OSXPlayer);
            layout.Clear(); Screen.GetDisplayLayout(layout);
            var devices = new List<DisplayDeviceCapabilities>();
            for (int i = 0; i < layout.Count; i++)
            {
                var d = layout[i]; int index = i;
                var desktop = Mode(index, d.width, d.height, d.refreshRate, SettingsWindowIntent.Borderless);
                // Reserve space for OS window decorations in addition to the taskbar/work-area exclusion.
                Resolution[] available;
                try { available = d.resolutions; }
                catch (NotSupportedException) { available = i == 0 ? Screen.resolutions : new Resolution[0]; }
                var modes = available.Select(r => Mode(index, r.width, r.height, r.refreshRateRatio, SettingsWindowIntent.Exclusive));
                devices.Add(new DisplayDeviceCapabilities(i, d.name, Math.Max(1, d.workArea.width - 32), Math.Max(1, d.workArea.height - 80), desktop, modes));
            }
            var windowModes = new List<SettingsWindowIntent> { SettingsWindowIntent.Windowed, SettingsWindowIntent.Borderless };
            if (Application.platform == RuntimePlatform.WindowsPlayer && devices.Any(d => d.Modes.Count > 0)) windowModes.Add(SettingsWindowIntent.Exclusive);
            // DisplayInfo.name is a label, NOT a stable device identity. Index remains a weak preference.
            string fingerprint = string.Join("|", layout.Select(d => d.name + ":" + d.width + "x" + d.height + ":" + d.refreshRate + ":" + d.workArea));
            return new DisplayCapabilities(devices, windowModes, standalone, fingerprint);
        }
        public ResolvedDisplayMode Observe()
        {
            if (Application.isEditor || (move != null && !move.isDone)) return null;
            if (pendingResolution != null)
            {
                var next = pendingResolution; pendingResolution = null;
                // Re-resolve the queued request after any prior OS move finishes. An old move
                // must not complete behind a subsequently verified recovery request.
                Request(next); return null;
            }
            if (Time.frameCount - resolutionFrame < 2) return null;
            var caps = GetCapabilities(); if (!caps.CanObserve) return null;
            var current = Screen.mainWindowDisplayInfo;
            int index = layout.FindIndex(d => d.Equals(current));
            if (index < 0 || Screen.width <= 0 || Screen.height <= 0) return null;
            var mode = Screen.fullScreenMode;
            if (mode != FullScreenMode.Windowed && mode != FullScreenMode.FullScreenWindow && mode != FullScreenMode.ExclusiveFullScreen) return null;
            return Mode(index, Screen.width, Screen.height, current.refreshRate,
                mode == FullScreenMode.Windowed ? SettingsWindowIntent.Windowed : mode == FullScreenMode.ExclusiveFullScreen ? SettingsWindowIntent.Exclusive : SettingsWindowIntent.Borderless);
        }
        public void Request(IReadOnlyResolvedDisplayMode target)
        {
            if (move != null && !move.isDone) { pendingResolution = target; return; }
            var caps = GetCapabilities();
            if (!caps.CanObserve || !caps.WindowModes.Contains(target.WindowMode) || target.DisplayIndex < 0 || target.DisplayIndex >= layout.Count)
                throw new InvalidOperationException("Display capability unavailable");
            var d = layout[target.DisplayIndex];
            if (!Screen.mainWindowDisplayInfo.Equals(d))
            {
                move = Screen.MoveMainWindowTo(in d, new Vector2Int(d.workArea.x + 16, d.workArea.y + 40));
                pendingResolution = target; return;
            }
            pendingResolution = null; SetResolution(target); resolutionFrame = Time.frameCount;
        }
        private static void SetResolution(IReadOnlyResolvedDisplayMode target)
        {
            Screen.SetResolution(target.Width, target.Height,
                target.WindowMode == SettingsWindowIntent.Windowed ? FullScreenMode.Windowed : target.WindowMode == SettingsWindowIntent.Exclusive ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.FullScreenWindow,
                new RefreshRate { numerator = (uint)target.RefreshRateNumerator, denominator = (uint)target.RefreshRateDenominator });
        }
        private static ResolvedDisplayMode Mode(int i, int w, int h, RefreshRate rate, SettingsWindowIntent mode) =>
            new ResolvedDisplayMode(i, w, h, (int)rate.numerator, (int)rate.denominator, mode);
    }
}
