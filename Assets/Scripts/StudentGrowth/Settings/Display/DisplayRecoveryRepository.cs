using System;
using System.IO;
using Newtonsoft.Json;

namespace StudentGrowth
{
    public interface IDisplayRecoveryRepository
    {
        bool Save(LastKnownGoodDisplayMode recovery);
    }
    // Recovery is intentionally separate from the player's settings intent. A startup observation
    // establishes the session recovery point; this record is diagnostic/recovery data, never an intent.
    public sealed class DisplayRecoveryRepository : IDisplayRecoveryRepository
    {
        private readonly string path;
        public DisplayRecoveryRepository(string directory) { path = Path.Combine(directory, "DisplayRecovery.json"); }
        public bool Save(LastKnownGoodDisplayMode recovery)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".tmp";
                var m = recovery.Mode;
                string json = JsonConvert.SerializeObject(new Record { Fingerprint = recovery.HardwareFingerprint,
                    DisplayIndex = m.DisplayIndex, Width = m.Width, Height = m.Height,
                    Numerator = m.RefreshRateNumerator, Denominator = m.RefreshRateDenominator, Mode = m.WindowMode }, Formatting.Indented);
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { var bytes = System.Text.Encoding.UTF8.GetBytes(json); stream.Write(bytes,0,bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary,path,path + ".bak"); else File.Move(temporary,path);
                return true;
            }
            catch (Exception) { return false; }
        }
        private sealed class Record
        {
            public int Version = 1;
            public string Fingerprint;
            public int DisplayIndex, Width, Height, Numerator, Denominator;
            public SettingsWindowIntent Mode;
        }
    }
}
