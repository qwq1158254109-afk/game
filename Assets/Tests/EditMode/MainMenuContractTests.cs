using System;
using System.IO;
using System.Collections;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
namespace StudentGrowth.Tests
{
    public sealed class MainMenuContractTests
    {
        private string path;
        private SaveSlotRepository repository;
        private SaveGameCodec codec;
        private GameSessionState state;
        private SaveGameRecord record;
        [SetUp] public void Setup() { path=Path.GetFullPath("Temp/MainMenuTests/"+Guid.NewGuid().ToString("N")); repository=new SaveSlotRepository(path); codec=new SaveGameCodec(new SaveContentResolver()); state=new GameSessionState { runId=Guid.NewGuid().ToString("N") }; record=codec.Capture(state,"Assets/Scenes/StudentGrowth/Maps/HomeMapScene.unity",0,new Vector2(2,3),Vector2.down); }
        [TearDown] public void Cleanup() { if(Directory.Exists(path)) Directory.Delete(path,true); }
        private void Write(int slot=0,bool replace=false,string expected=null,bool check=false) => repository.WriteChecked(new SaveSlotId(slot),record,null,()=>true,expected,check,replace);
        [Test] public void AutomaticOwnershipDoesNotTransferThroughManualSave() {
            Write(); string old=repository.ReadSummary(new SaveSlotId(0)).Revision;
            record.runId=Guid.NewGuid().ToString("N"); Write(1);
            Assert.Throws<IOException>(()=>Write()); Assert.AreEqual(old,repository.ReadSummary(new SaveSlotId(0)).Revision);
            Write(0,true,old,true); Assert.AreEqual(record.runId,repository.ReadSummary(new SaveSlotId(0)).RunId);
        }
        [Test] public void ReplacedTargetRequiresNewConfirmation() {
            Write(); var old=repository.ReadSummary(new SaveSlotId(0)).Revision; Write();
            Assert.Throws<IOException>(()=>Write(0,true,old,true));
        }
        [Test] public void LegacyIdentityIsStableAndReadOnly() {
            record.version=1; repository.Write(new SaveSlotId(0),codec.Encode(record),1,0,null);
            var revision=repository.ReadSummary(new SaveSlotId(0)).Revision;
            string file=Path.Combine(path,"Slot00",revision,"Record.dat"), before=File.ReadAllText(file);
            var a=codec.Decode(repository.ReadRecord(new SaveSlotId(0))); var b=codec.Decode(repository.ReadRecord(new SaveSlotId(0)));
            Assert.AreEqual("legacy-v1:"+revision,a.Record.runId); Assert.AreEqual(a.Record.runId,b.Record.runId); Assert.AreEqual(before,File.ReadAllText(file));
        }
        [Test] public void MetadataTamperingAndOrphanAreExcluded() {
            Write(); var summary=repository.ReadSummary(new SaveSlotId(0));
            string file=Path.Combine(path,"Slot00",summary.Revision,"RecordTitle.dat");
            var title=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(file)); title["Sequence"]=99999; File.WriteAllText(file,title.ToString());
            Assert.IsFalse(repository.ReadSummary(new SaveSlotId(0)).IsValid);
            Directory.CreateDirectory(Path.Combine(path,"Slot01",Guid.NewGuid().ToString("N"))); Assert.IsTrue(repository.ReadSummary(new SaveSlotId(1)).IsEmpty);
        }
        [Test] public void SequenceOrdersCommitsInsteadOfGameDay() { record.time.currentDay=9; Write(1); record.time.currentDay=2; Write(2); Assert.Greater(repository.ReadSummary(new SaveSlotId(2)).Sequence,repository.ReadSummary(new SaveSlotId(1)).Sequence); }
        [Test] public void DirtyComparisonIgnoresClockButDetectsDirectMutation() {
            var lifetime=new GameSessionLifetime(); lifetime.Begin(record.runId); lifetime.Saved(lifetime.RuntimeSessionId,record);
            record.playedSeconds=500; record.savedAtUtc=DateTime.UtcNow.ToString("O"); record.saveSequence=123; Assert.IsFalse(lifetime.IsDirty(record));
            record.player.money++; Assert.IsTrue(lifetime.IsDirty(record)); record.player.money--;
            record.location.locationFlags["a"]=true; Assert.IsTrue(lifetime.IsDirty(record));
        }
        [Test] public void DictionaryOrderIsIgnoredAndListOrderIsPreserved() {
            record.location.locationFlags["a"]=true; record.location.locationFlags["b"]=false; string a=GameSnapshotComparison.Normalize(record);
            record.location.locationFlags.Clear(); record.location.locationFlags["b"]=false; record.location.locationFlags["a"]=true;
            Assert.AreEqual(a,GameSnapshotComparison.Normalize(record)); record.cardIds.AddRange(new[]{"a","b"}); a=GameSnapshotComparison.Normalize(record); record.cardIds.Reverse(); Assert.AreNotEqual(a,GameSnapshotComparison.Normalize(record));
        }
        [Test] public void PixelPrecisionAndOldCallbacksRespectSession() {
            var lifetime=new GameSessionLifetime { PixelSize=1.0/16 }; lifetime.Begin(record.runId); string old=lifetime.RuntimeSessionId; lifetime.Saved(old,record);
            record.playerX+=.001f; Assert.IsFalse(lifetime.IsDirty(record)); record.playerX+=.07f; Assert.IsTrue(lifetime.IsDirty(record));
            lifetime.End(old); lifetime.Begin(record.runId); lifetime.Saved(old,record); Assert.IsTrue(lifetime.IsDirty(record)); Assert.IsFalse(lifetime.End(old).Success);
        }
        [Test] public void ExpiredWriterCannotPublish() { Write(); var old=repository.ReadSummary(new SaveSlotId(0)).Revision; Assert.Throws<IOException>(()=>repository.WriteChecked(new SaveSlotId(0),record,null,()=>false)); Assert.AreEqual(old,repository.ReadSummary(new SaveSlotId(0)).Revision); }
        [Test] public void FaultsBeforePublicationKeepOldCommitAndLateErrorResolvesSuccess() {
            Write(1); string old=repository.ReadSummary(new SaveSlotId(1)).Revision;
            foreach(string step in new[]{"record","title","before-publish"}) {
                var faulty=new SaveSlotRepository(path,s=> { if(s==step) throw new IOException("injected"); });
                Assert.Throws<IOException>(()=>faulty.WriteChecked(new SaveSlotId(1),record,null,()=>true));
                Assert.AreEqual(old,repository.ReadSummary(new SaveSlotId(1)).Revision);
            }
            new SaveSlotRepository(path,s=> { if(s=="after-publish") throw new IOException("late injected"); }).WriteChecked(new SaveSlotId(1),record,null,()=>true);
            Assert.AreNotEqual(old,repository.ReadSummary(new SaveSlotId(1)).Revision);
            Assert.IsTrue(repository.ReadSummary(new SaveSlotId(1)).IsValid);
        }
    }
}
