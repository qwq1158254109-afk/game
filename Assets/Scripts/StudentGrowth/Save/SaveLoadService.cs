using System;
using System.Collections;
using System.Collections.Generic;

namespace StudentGrowth
{
    public sealed class SaveLoadService : ISaveLoadService
    {
        private readonly ISaveSlotRepository repository;
        private readonly SaveGameCodec codec;
        private readonly Func<SaveGameRecord> capture;
        private readonly Func<byte[]> captureThumbnail;
        public bool IsBusy { get; private set; }
        public bool CommitUncertain { get; private set; }
        public GameSessionLifetime Lifetime { get; set; }
        public Func<string> SaveBlockReason { get; set; }
        public SaveSlotSummary Latest(out string notice)
        {
            SaveSlotSummary latest=null; notice=null; bool duplicate=false;
            var seen = new HashSet<long>();
            foreach(SaveSlotSummary s in GetSlotSummaries()) {
                if(!s.IsValid) notice="部分存档无法校验，请在读取面板查看。";
                if(s.IsEmpty || !s.IsValid) continue;
                if(s.Sequence==0) { if(notice==null) notice="旧存档无法确定保存顺序，请手动选择。"; continue; }
                if(!seen.Add(s.Sequence)) duplicate=true;
                if(latest==null || s.Sequence>latest.Sequence) latest=s;
            }
            if(duplicate) { notice="存档顺序冲突，请手动选择。"; return null; }
            return latest;
        }
        public IEnumerator SaveExpectedAsync(SaveSlotId slot,string revision,bool replace,Action<SaveOperationResult> done)
            => SaveCoreAsync(slot,replace,done,null,revision,true,replace);
        public IEnumerator SaveSnapshotAsync(SaveGameRecord snapshot,string runtimeId,Action<SaveOperationResult> done)
        {
            if(Lifetime==null || !Lifetime.Matches(runtimeId) || Lifetime.Leaving) { done?.Invoke(new SaveOperationResult(false,"SAVE_SESSION_EXPIRED")); yield break; }
            yield return SaveCoreAsync(new SaveSlotId(0),true,done,snapshot);
        }
        public SaveLoadService(ISaveSlotRepository repository, SaveGameCodec codec, Func<SaveGameRecord> capture, Func<byte[]> captureThumbnail)
        { this.repository = repository; this.codec = codec; this.capture = capture; this.captureThumbnail = captureThumbnail; }
        public IReadOnlyList<IReadOnlySaveSlotSummary> GetSlotSummaries()
        {
            var slots = new List<IReadOnlySaveSlotSummary>();
            for (int i=0;i<SaveSlotId.Count;i++)
                try { slots.Add(repository.ReadSummary(new SaveSlotId(i))); }
                catch { slots.Add(new SaveSlotSummary(new SaveSlotId(i),false,false,error: "SAVE_SUMMARY_FAILED")); }
            return slots.AsReadOnly();
        }
        public IEnumerator SaveAsync(SaveSlotId slot, Action<SaveOperationResult> completed) => SaveCoreAsync(slot,false,completed);
        public IEnumerator AutoSaveAsync(Action<SaveOperationResult> completed) => SaveCoreAsync(new SaveSlotId(0),true,completed);
        private IEnumerator SaveCoreAsync(SaveSlotId slot, bool automatic, Action<SaveOperationResult> completed,
            SaveGameRecord snapshot=null,string expected=null,bool check=false,bool replace=false)
        {
            string blocked=SaveBlockReason?.Invoke();
            if(!string.IsNullOrEmpty(blocked)){completed?.Invoke(new SaveOperationResult(false,blocked));yield break;}
            if (CommitUncertain || (Lifetime!=null && !Lifetime.IsActive)) { completed?.Invoke(new SaveOperationResult(false,CommitUncertain ? "SAVE_COMMIT_UNCERTAIN" : "SAVE_NO_SESSION")); yield break; }
            if (IsBusy || (slot.IsAutomatic && !automatic))
            { completed?.Invoke(new SaveOperationResult(false,IsBusy ? "SAVE_BUSY" : "SAVE_AUTOMATIC_READ_ONLY")); yield break; }
            IsBusy = true;
            SaveOperationResult result;
            try
            {
                string runtimeId=Lifetime?.RuntimeSessionId;
                var record = snapshot ?? capture(); string json = codec.Encode(record);
                codec.Decode(json); // Reject unresolved content before replacing any slot.
                byte[] thumbnail = null; try { thumbnail = captureThumbnail?.Invoke(); } catch { }
                if(repository is SaveSlotRepository disk && Lifetime!=null)
                    disk.WriteChecked(slot,record,thumbnail,() => Lifetime.Matches(runtimeId),expected,check,replace);
                else repository.Write(slot,json,record.time.currentDay,record.playedSeconds,thumbnail);
                Lifetime?.Saved(runtimeId,record);
                result = new SaveOperationResult(true,thumbnail == null ? "SAVE_THUMBNAIL_UNAVAILABLE" : null);
            }
            catch (Exception ex) { CommitUncertain=ex.Message=="SAVE_COMMIT_UNCERTAIN"; result = new SaveOperationResult(false,Diagnostic(ex,"SAVE_WRITE_FAILED")); }
            finally { IsBusy = false; }
            completed?.Invoke(result);
        }
        public IEnumerator LoadAsync(SaveSlotId slot, Action<LoadOperationResult> completed)
            => LoadExpectedAsync(slot,null,false,completed);
        public IEnumerator LoadExpectedAsync(SaveSlotId slot,string revision,bool check,Action<LoadOperationResult> completed)
        {
            if (IsBusy) { completed?.Invoke(new LoadOperationResult(null,"SAVE_BUSY")); yield break; }
            IsBusy = true; LoadOperationResult result;
            try {
                if(check && repository.ReadSummary(slot).Revision!=revision) throw new InvalidOperationException("SAVE_TARGET_CHANGED");
                var loaded=codec.Decode(repository.ReadRecord(slot));
                if(check && repository.ReadSummary(slot).Revision!=revision) throw new InvalidOperationException("SAVE_TARGET_CHANGED");
                result = new LoadOperationResult(loaded);
            }
            catch (Exception ex) { result = new LoadOperationResult(null,Diagnostic(ex,"SAVE_LOAD_FAILED")); }
            finally { IsBusy = false; }
            completed?.Invoke(result);
        }
        private static string Diagnostic(Exception ex,string fallback) => ex.Message.StartsWith("SAVE_",StringComparison.Ordinal) ? ex.Message : fallback;
    }
}
