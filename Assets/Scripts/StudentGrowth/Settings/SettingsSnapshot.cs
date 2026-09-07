using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace StudentGrowth
{
    public enum SettingsWindowIntent { Windowed, Borderless, Exclusive }

    // Immutable values: callers cannot downcast a read-only interface to an internal DTO.
    public sealed class SettingsSnapshot : IEquatable<SettingsSnapshot>
    {
        public float Master { get; }
        public float Music { get; }
        public float Sfx { get; }
        public bool VSync { get; }
        public int FrameRateLimit { get; }
        public string LanguageCode { get; }
        public DisplayIntent DisplayIntent { get; }
        public SettingsWindowIntent WindowIntent => DisplayIntent.WindowMode;
        public int WindowWidth => DisplayIntent.Width;
        public int WindowHeight => DisplayIntent.Height;
        public float TextSpeed { get; }
        public float UiScale { get; }
        public bool Subtitles { get; }

        public SettingsSnapshot(float master = 1f, float music = 1f, float sfx = 1f,
            bool vSync = true, int frameRateLimit = 60, string languageCode = "zh-Hans",
            SettingsWindowIntent windowIntent = SettingsWindowIntent.Windowed,
            int windowWidth = 1280, int windowHeight = 720,
            float textSpeed = 1f, float uiScale = 1f, bool subtitles = true, DisplayIntent displayIntent = null)
        {
            Master = ClampFinite(master, 0, 1); Music = ClampFinite(music, 0, 1); Sfx = ClampFinite(sfx, 0, 1);
            VSync = vSync; FrameRateLimit = frameRateLimit == -1 ? -1 : Math.Max(30, Math.Min(240, frameRateLimit));
            LanguageCode = string.IsNullOrWhiteSpace(languageCode) ? "zh-Hans" : languageCode;
            if (!Enum.IsDefined(typeof(SettingsWindowIntent), windowIntent)) throw new ArgumentOutOfRangeException(nameof(windowIntent));
            DisplayIntent = displayIntent ?? new DisplayIntent(Math.Max(320, Math.Min(16384, windowWidth)), Math.Max(240, Math.Min(16384, windowHeight)), windowIntent);
            TextSpeed = ClampFinite(textSpeed, 0.25f, 4); UiScale = ClampFinite(uiScale, 0.75f, 1.5f); Subtitles = subtitles;
        }

        public SettingsSnapshot WithAudio(float master, float music, float sfx) =>
            new SettingsSnapshot(master, music, sfx, VSync, FrameRateLimit, LanguageCode, WindowIntent, WindowWidth, WindowHeight, TextSpeed, UiScale, Subtitles, DisplayIntent);
        public SettingsSnapshot WithSafeGraphics(bool vSync, int limit) =>
            new SettingsSnapshot(Master, Music, Sfx, vSync, limit, LanguageCode, WindowIntent, WindowWidth, WindowHeight, TextSpeed, UiScale, Subtitles, DisplayIntent);
        public SettingsSnapshot WithLanguage(string language) =>
            new SettingsSnapshot(Master, Music, Sfx, VSync, FrameRateLimit, language, WindowIntent, WindowWidth, WindowHeight, TextSpeed, UiScale, Subtitles, DisplayIntent);
        public SettingsSnapshot WithDisplay(DisplayIntent intent) => new SettingsSnapshot(Master, Music, Sfx, VSync, FrameRateLimit, LanguageCode,
            WindowIntent, WindowWidth, WindowHeight, TextSpeed, UiScale, Subtitles, intent);
        public SettingsSnapshot Copy() => new SettingsSnapshot(Master, Music, Sfx, VSync, FrameRateLimit, LanguageCode,
            WindowIntent, WindowWidth, WindowHeight, TextSpeed, UiScale, Subtitles, DisplayIntent);

        public bool Equals(SettingsSnapshot other) => other != null && Master == other.Master && Music == other.Music &&
            Sfx == other.Sfx && VSync == other.VSync && FrameRateLimit == other.FrameRateLimit && LanguageCode == other.LanguageCode &&
            DisplayIntent.Equals(other.DisplayIntent) &&
            TextSpeed == other.TextSpeed && UiScale == other.UiScale && Subtitles == other.Subtitles;
        public override bool Equals(object obj) => Equals(obj as SettingsSnapshot);
        public override int GetHashCode() => Master.GetHashCode() ^ Music.GetHashCode() ^ Sfx.GetHashCode() ^ LanguageCode.GetHashCode();
        private static float ClampFinite(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value), "Non-finite setting");
            return Math.Max(min, Math.Min(max, value));
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class SettingsDataV2
    {
        [JsonProperty("schemaVersion", Required = Required.Always)] public int SchemaVersion { get; set; } = 2;
        [JsonProperty("writeGeneration", Required = Required.Always)] public long Generation { get; set; }
        [JsonProperty("commitOperationId")] public string OperationId { get; set; }
        [JsonProperty("audio", Required = Required.Always)] public AudioData Audio { get; set; } = new AudioData();
        [JsonProperty("safeGraphics", Required = Required.Always)] public GraphicsData SafeGraphics { get; set; } = new GraphicsData();
        [JsonProperty("displayIntent", Required = Required.Always)] public DisplayData DisplayIntent { get; set; } = new DisplayData();
        [JsonProperty("localization", Required = Required.Always)] public LocaleData Localization { get; set; } = new LocaleData();
        [JsonProperty("accessibility")] public AccessibilityData Accessibility { get; set; } = new AccessibilityData();
    }
    [JsonObject(MemberSerialization.OptIn)] internal sealed class AudioData
    {
        [JsonProperty("master")] public float Master { get; set; } = 1;
        [JsonProperty("music")] public float Music { get; set; } = 1;
        [JsonProperty("sfx")] public float Sfx { get; set; } = 1;
    }
    [JsonObject(MemberSerialization.OptIn)] internal sealed class GraphicsData
    {
        [JsonProperty("vSync")] public bool VSync { get; set; } = true;
        [JsonProperty("frameRateLimit")] public int FrameRateLimit { get; set; } = 60;
    }
    [JsonObject(MemberSerialization.OptIn)] internal sealed class DisplayData
    {
        [JsonProperty("preferredDisplayId")] public string PreferredDisplayId { get; set; } = "";
        [JsonProperty("preferredDisplayIndex")] public int PreferredDisplayIndex { get; set; }
        [JsonProperty("refreshRateNumerator")] public int Numerator { get; set; } = 60;
        [JsonProperty("refreshRateDenominator")] public int Denominator { get; set; } = 1;
        [JsonProperty("windowMode")] public SettingsWindowIntent WindowMode { get; set; }
        [JsonProperty("width")] public int Width { get; set; } = 1280;
        [JsonProperty("height")] public int Height { get; set; } = 720;
    }
    [JsonObject(MemberSerialization.OptIn)] internal sealed class LocaleData
    {
        [JsonProperty("languageCode")] public string LanguageCode { get; set; } = "zh-Hans";
    }
    [JsonObject(MemberSerialization.OptIn)] internal sealed class AccessibilityData
    {
        [JsonProperty("textSpeed")] public float TextSpeed { get; set; } = 1;
        [JsonProperty("uiScale")] public float UiScale { get; set; } = 1;
        [JsonProperty("subtitles")] public bool Subtitles { get; set; } = true;
    }

    internal static class SettingsSerializer
    {
        internal static JsonSerializerSettings Options => new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture, TypeNameHandling = TypeNameHandling.None,
            MissingMemberHandling = MissingMemberHandling.Ignore, NullValueHandling = NullValueHandling.Include,
            ObjectCreationHandling = ObjectCreationHandling.Replace, Formatting = Formatting.Indented, MaxDepth = 32,
            Converters = { new StringEnumConverter { AllowIntegerValues = false } }
        };

        internal static SettingsDataV2 Encode(SettingsSnapshot s, long generation, string operationId) => new SettingsDataV2
        {
            Generation = generation, OperationId = operationId,
            Audio = new AudioData { Master = s.Master, Music = s.Music, Sfx = s.Sfx },
            SafeGraphics = new GraphicsData { VSync = s.VSync, FrameRateLimit = s.FrameRateLimit },
            DisplayIntent = new DisplayData { WindowMode = s.WindowIntent, Width = s.WindowWidth, Height = s.WindowHeight,
                PreferredDisplayId = s.DisplayIntent.PreferredDisplayId, PreferredDisplayIndex = s.DisplayIntent.PreferredDisplayIndex,
                Numerator = s.DisplayIntent.RefreshRateNumerator, Denominator = s.DisplayIntent.RefreshRateDenominator },
            Localization = new LocaleData { LanguageCode = s.LanguageCode },
            Accessibility = new AccessibilityData { TextSpeed = s.TextSpeed, UiScale = s.UiScale, Subtitles = s.Subtitles }
        };

        internal static SettingsSnapshot Decode(SettingsDataV2 d)
        {
            if (d == null || d.SchemaVersion != 2 || d.Generation < 0 || d.Audio == null || d.SafeGraphics == null || d.DisplayIntent == null || d.Localization == null)
                throw new JsonSerializationException("Invalid required settings data");
            var a = d.Accessibility ?? new AccessibilityData();
            return new SettingsSnapshot(d.Audio.Master, d.Audio.Music, d.Audio.Sfx, d.SafeGraphics.VSync,
                d.SafeGraphics.FrameRateLimit, d.Localization.LanguageCode, d.DisplayIntent.WindowMode,
                d.DisplayIntent.Width, d.DisplayIntent.Height, a.TextSpeed, a.UiScale, a.Subtitles,
                new DisplayIntent(d.DisplayIntent.Width, d.DisplayIntent.Height, d.DisplayIntent.WindowMode,
                    d.DisplayIntent.Numerator, d.DisplayIntent.Denominator, d.DisplayIntent.PreferredDisplayIndex, d.DisplayIntent.PreferredDisplayId));
        }

        internal static SettingsDataV2 Parse(string json, out bool future, out bool repaired)
        {
            // Probe the version before interpreting newer enum values or object shapes.
            if (json.Length > 128 * 1024) throw new JsonSerializationException("Settings too large");
            var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            future = root.Value<int?>("schemaVersion") > 2;
            repaired = false;
            if (future) return null;
            var dto = JsonConvert.DeserializeObject<SettingsDataV2>(json, Options);
            var value = Decode(dto);
            // Compare JSON against JSON, not parsed doubles against in-memory Single values.
            // Otherwise valid values such as 0.42f are falsely marked repaired on every startup.
            var canonical = JObject.Parse(JsonConvert.SerializeObject(Encode(value, dto.Generation, dto.OperationId), Options));
            foreach (var group in new[] { "audio", "safeGraphics", "displayIntent", "localization", "accessibility" })
                foreach (var property in ((JObject)canonical[group]).Properties())
                    if (!JToken.DeepEquals(root[group]?[property.Name], property.Value)) repaired = true;
            return dto;
        }
    }
}
