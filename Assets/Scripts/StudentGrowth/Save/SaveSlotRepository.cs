using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace StudentGrowth
{
    public interface ISaveSlotRepository
    {
        SaveSlotSummary ReadSummary(SaveSlotId slot);
        string ReadRecord(SaveSlotId slot);
        byte[] ReadThumbnail(SaveSlotId slot);
        void Write(SaveSlotId slot, string record, int day, double seconds, byte[] thumbnail);
    }
    public sealed class SaveSlotRepository : ISaveSlotRepository
    {
        private readonly string root;
        private readonly object writeGate = new object();
        private long sequence;
        private readonly Action<string> checkpoint;
        public SaveSlotRepository(string directory,Action<string> checkpoint=null) { root = Path.GetFullPath(directory); this.checkpoint=checkpoint; }
        private string SlotPath(SaveSlotId slot) => Path.Combine(root,"Slot" + slot.Value.ToString("00"));
        private string Generation(SaveSlotId slot)
        {
            string pointer = Path.Combine(SlotPath(slot),"Current.dat");
            // File.Exists suppresses access errors. Enumerate first so unreadable directories are errors.
            if (Directory.Exists(root)) Directory.GetFileSystemEntries(root);
            if (Directory.Exists(SlotPath(slot))) Directory.GetFileSystemEntries(SlotPath(slot));
            if (!File.Exists(pointer)) return null;
            string value = ReadBounded(pointer,128).Trim();
            if (!Guid.TryParseExact(value,"N",out _)) throw new IOException("SAVE_INVALID_POINTER");
            return Path.Combine(SlotPath(slot),value);
        }
        public SaveSlotSummary ReadSummary(SaveSlotId slot)
        {
            try
            {
                var path = Generation(slot);
                if (path == null) return new SaveSlotSummary(slot,true,true);
                var title = ReadTitle(path);
                var record = ValidateGeneration(path, title);
                return new SaveSlotSummary(slot,false,true,title.Day,title.Seconds,revision: Path.GetFileName(path)) {
                    RunId = record.runId, Sequence = record.saveSequence, SavedAtUtc = record.savedAtUtc,
                    LocationId = record.location?.currentLocationId, ScenePath = record.scenePath, Period = record.time.currentPeriod };
            }
            catch { return new SaveSlotSummary(slot,false,false,error: "SAVE_SLOT_DAMAGED"); }
        }
        public string ReadRecord(SaveSlotId slot)
        {
            var path = Generation(slot); if (path == null) throw new IOException("SAVE_SLOT_EMPTY");
            return JsonConvert.SerializeObject(ValidateGeneration(path,ReadTitle(path)));
        }
        private static SaveGameRecord ValidateGeneration(string path, Title title)
        {
            string json = ReadBounded(Path.Combine(path,"Record.dat"),2 * 1024 * 1024);
            if (Hash(json) != title.Hash) throw new IOException("SAVE_CHECKSUM_MISMATCH");
            var record = JsonConvert.DeserializeObject<SaveGameRecord>(json);
            if (record == null || record.time == null || record.location == null || record.player == null ||
                record.time.currentDay != title.Day || record.playedSeconds != title.Seconds)
                throw new IOException("SAVE_INVALID_SUMMARY");
            if (record.version == 1) { record.runId = "legacy-v1:" + Path.GetFileName(path).ToLowerInvariant(); record.saveSequence=0; record.savedAtUtc=null; }
            else if (record.version != 2 || title.Version != 2 || title.SaveId != Path.GetFileName(path) ||
                !ValidRunId(record.runId) || record.runId != title.RunId || record.saveSequence <= 0 ||
                record.saveSequence != title.Sequence || record.savedAtUtc != title.SavedAtUtc ||
                !DateTimeOffset.TryParse(record.savedAtUtc,out _) || record.scenePath != title.ScenePath ||
                record.location.currentLocationId != title.LocationId || (int)record.time.currentPeriod != title.Period)
                throw new IOException("SAVE_METADATA_MISMATCH");
            return record;
        }
        private static bool ValidRunId(string id) => id!=null && (Guid.TryParseExact(id,"N",out _) ||
            (id.StartsWith("legacy-v1:",StringComparison.Ordinal) && Guid.TryParseExact(id.Substring(10),"N",out _)));
        public void WriteChecked(SaveSlotId slot, SaveGameRecord record, byte[] thumbnail, Func<bool> sessionValid,
            string expectedRevision = null, bool checkRevision = false, bool replaceAutomatic = false)
        {
            lock (writeGate)
            {
                if (!sessionValid()) throw new IOException("SAVE_SESSION_EXPIRED");
                var target = ReadSummary(slot);
                if (!target.IsValid) throw new IOException("SAVE_SLOT_DAMAGED");
                if (checkRevision && target.Revision != expectedRevision) throw new IOException("SAVE_TARGET_CHANGED");
                if (slot.IsAutomatic && !target.IsEmpty && target.RunId != record.runId && !replaceAutomatic)
                    throw new IOException("SAVE_AUTOMATIC_OTHER_RUN");
                for (int i=0;i<SaveSlotId.Count;i++) { var s=ReadSummary(new SaveSlotId(i)); if(s.IsValid) sequence=Math.Max(sequence,s.Sequence); }
                record.version=2; record.saveSequence=checked(++sequence); record.savedAtUtc=DateTime.UtcNow.ToString("O");
                if (string.IsNullOrEmpty(record.runId)) throw new IOException("SAVE_NO_SESSION");
                WriteCore(slot,JsonConvert.SerializeObject(record),record.time.currentDay,record.playedSeconds,thumbnail,sessionValid);
            }
        }
        public byte[] ReadThumbnail(SaveSlotId slot)
        {
            var path = Generation(slot); if (path == null) return null;
            string file = Path.Combine(path,"ScreenShot.png");
            if (!File.Exists(file)) return null;
            if (new FileInfo(file).Length > 512 * 1024) throw new IOException("SAVE_THUMBNAIL_TOO_LARGE");
            return File.ReadAllBytes(file);
        }
        public void Write(SaveSlotId slot, string record, int day, double seconds, byte[] thumbnail)
        { lock(writeGate) WriteCore(slot,record,day,seconds,thumbnail,() => true); }
        private void WriteCore(SaveSlotId slot, string record, int day, double seconds, byte[] thumbnail, Func<bool> sessionValid)
        {
            string slotPath = SlotPath(slot); Directory.CreateDirectory(slotPath);
            string revision = Guid.NewGuid().ToString("N"); string next = Path.Combine(slotPath,revision); Directory.CreateDirectory(next);
            // The immutable generation contains a complete matching record, summary and screenshot.
            // Only atomic pointer promotion exposes it. Interrupted writes leave the prior save intact.
            WriteDurable(Path.Combine(next,"Record.dat"),Encoding.UTF8.GetBytes(record));
            checkpoint?.Invoke("record");
            var data = JsonConvert.DeserializeObject<SaveGameRecord>(record);
            WriteDurable(Path.Combine(next,"RecordTitle.dat"),Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new Title {
                Version=data.version, Day = day, Seconds = seconds, Hash = Hash(record), SaveId=revision, RunId=data.runId,
                Sequence=data.saveSequence,SavedAtUtc=data.savedAtUtc,ScenePath=data.scenePath,LocationId=data.location?.currentLocationId,Period=(int)data.time.currentPeriod })));
            checkpoint?.Invoke("title"); ValidateGeneration(next,ReadTitle(next));
            if (thumbnail != null) WriteDurable(Path.Combine(next,"ScreenShot.png"),thumbnail);
            string temporary = Path.Combine(slotPath,"Current.tmp"); string current = Path.Combine(slotPath,"Current.dat");
            WriteDurable(temporary,Encoding.ASCII.GetBytes(revision));
            if (!sessionValid()) throw new IOException("SAVE_SESSION_EXPIRED");
            checkpoint?.Invoke("before-publish");
            try
            {
                if (File.Exists(current)) File.Replace(temporary,current,Path.Combine(slotPath,"Current.bak"));
                else File.Move(temporary,current);
                checkpoint?.Invoke("after-publish");
            }
            catch
            {
                // A filesystem may report a late error after promotion. Verify the commit point.
                string published;
                try { published=File.Exists(current) ? ReadBounded(current,128) : null; }
                catch { throw new IOException("SAVE_COMMIT_UNCERTAIN"); }
                if (published != revision) throw;
                ValidateGeneration(next,ReadTitle(next));
            }
        }
        private static void WriteDurable(string path, byte[] bytes)
        { using (var stream = new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None)) { stream.Write(bytes,0,bytes.Length); stream.Flush(true); } }
        private static string ReadBounded(string path, long limit)
        { if (new FileInfo(path).Length > limit) throw new IOException("SAVE_FILE_TOO_LARGE"); return File.ReadAllText(path,Encoding.UTF8); }
        private static Title ReadTitle(string path)
        {
            var title = JsonConvert.DeserializeObject<Title>(ReadBounded(Path.Combine(path,"RecordTitle.dat"),4096));
            if (title == null || (title.Version != 1 && title.Version != 2) || title.Day < 1 || double.IsNaN(title.Seconds) || double.IsInfinity(title.Seconds) || title.Seconds < 0 || title.Hash?.Length != 64)
                throw new IOException("SAVE_INVALID_SUMMARY");
            return title;
        }
        private static string Hash(string text)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-",""); }
        private sealed class Title { public int Version = 1; public int Day; public double Seconds; public string Hash;
            public string SaveId,RunId,SavedAtUtc,ScenePath,LocationId; public long Sequence; public int Period; }
    }
}
