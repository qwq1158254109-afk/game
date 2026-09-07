using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace StudentGrowth.Tests
{
    public sealed class SettingsRepositoryTests
    {
        private MemoryFiles files;
        private SettingsPathProvider paths;
        private SettingsRepository repo;
        [SetUp] public void SetUp()
        {
            files = new MemoryFiles(); paths = new SettingsPathProvider(Path.Combine(Path.GetTempPath(), "SettingsRepoTests"));
            repo = NewRepo();
            Assert.IsTrue(repo.Save(new SettingsSnapshot(0.8f)).Success);
        }
        private SettingsRepository NewRepo(Func<SettingsSnapshot> importer = null) => new SettingsRepository(paths, importer, files: files);
        [Test] public void RoundtripAndBackupPreservePreviousAcceptedValues()
        {
            Assert.IsTrue(repo.Save(new SettingsSnapshot(0.2f)).Success);
            Assert.AreEqual(0.2f, NewRepo().Load().Snapshot.Master);
            Assert.AreEqual(0.8f, JObject.Parse(files.Read(paths.V2BackupPath))["audio"].Value<float>("master"));
        }
        [Test] public void FractionalVolumesRemainCleanAfterReload()
        {
            Assert.IsTrue(repo.Save(new SettingsSnapshot(0.42f, 0.37f, 0.63f)).Success);
            var loaded = NewRepo().Load();
            Assert.IsTrue(loaded.Persisted); Assert.IsNull(loaded.Notice); Assert.AreEqual(0.42f, loaded.Snapshot.Master);
        }
        [Test] public void CorruptMainUsesValidBackupAndNeverRotatesCorruptionOverIt()
        {
            repo.Save(new SettingsSnapshot(0.2f)); files.Write(paths.V2Path, "broken json");
            var recovered = NewRepo().Load(); Assert.AreEqual(0.8f, recovered.Snapshot.Master);
            Assert.AreEqual("SETTINGS_BACKUP_RESTORED", recovered.Notice);
            Assert.IsTrue(NewRepo().Save(recovered.Snapshot).Success);
            Assert.AreEqual(0.8f, JObject.Parse(files.Read(paths.V2BackupPath))["audio"].Value<float>("master"));
        }
        [Test] public void MandatoryObjectMissingFallsBackButOptionalScalarRepairs()
        {
            var root = JObject.Parse(files.Read(paths.V2Path)); ((JObject)root["audio"]).Remove("music");
            files.Write(paths.V2Path, root.ToString()); var repaired = NewRepo().Load();
            Assert.AreEqual(1f, repaired.Snapshot.Music); Assert.IsFalse(repaired.Persisted);
            root.Remove("audio"); files.Write(paths.V2Path, root.ToString());
            Assert.AreEqual(1f, NewRepo().Load().Snapshot.Master);
        }
        [Test] public void UnknownEnumAndNonfiniteValuesInvalidateCandidate()
        {
            string original = files.Read(paths.V2Path);
            foreach (var value in new JToken[] { new JValue("FutureMode"), new JValue(0) })
            {
                var root = JObject.Parse(original); root["displayIntent"]["windowMode"] = value;
                files.Write(paths.V2Path, root.ToString()); Assert.AreEqual(1f, NewRepo().Load().Snapshot.Master);
            }
            var invalid = JObject.Parse(original); invalid["audio"]["master"] = "NaN";
            files.Write(paths.V2Path, invalid.ToString()); Assert.AreEqual(1f, NewRepo().Load().Snapshot.Master);
        }
        [Test] public void FutureVersionIsProtectedEvenWithUnknownEnums()
        {
            var root = JObject.Parse(files.Read(paths.V2Path)); root["schemaVersion"] = 99;
            root["displayIntent"]["windowMode"] = "FutureMode"; string future = root.ToString(); files.Write(paths.V2Path, future);
            var reader = NewRepo(); Assert.IsTrue(reader.Load().ReadOnly);
            Assert.AreEqual(SettingsPersistenceOutcome.ReadOnly, reader.Save(new SettingsSnapshot()).Outcome);
            Assert.AreEqual(future, files.Read(paths.V2Path));
        }
        [Test] public void RootNullAndMissingGenerationDoNotEscapeAsNullState()
        {
            files.Write(paths.V2Path, "null"); Assert.IsNotNull(NewRepo().Load().Snapshot);
            files.Data.Clear(); repo.Save(new SettingsSnapshot(0.2f));
            var root = JObject.Parse(files.Read(paths.V2Path)); root.Remove("writeGeneration"); files.Write(paths.V2Path, root.ToString());
            Assert.AreEqual(1f, NewRepo().Load().Snapshot.Master);
        }
        [Test] public void OrdinaryHighGenerationTempNeverPromotesWithoutJournal()
        {
            var root = JObject.Parse(files.Read(paths.V2Path)); root["writeGeneration"] = 200; root["audio"]["master"] = 0.1;
            files.Write(paths.V2TemporaryPath, root.ToString()); Assert.AreEqual(0.8f, NewRepo().Load().Snapshot.Master);
        }
        [Test] public void ValidJournalRecoversInterruptedValidatedTemp()
        {
            var crash = CaptureBeforeReplace(); files.Data = crash;
            var load = NewRepo().Load(); Assert.AreEqual(0.2f, load.Snapshot.Master);
            Assert.AreEqual("SETTINGS_INTERRUPTED_COMMIT_RECOVERED", load.Notice);
        }
        [Test] public void RecoveryRequiresMatchingOperationHashAndGeneration()
        {
            var crash = CaptureBeforeReplace();
            foreach (var field in new[] { "OperationId", "ContentHash", "TargetGeneration" })
            {
                files.Data = new Dictionary<string, string>(crash);
                var journal = JObject.Parse(files.Read(paths.V2JournalPath));
                journal[field] = field == "TargetGeneration" ? new JValue(500) : new JValue("wrong");
                files.Write(paths.V2JournalPath, journal.ToString());
                Assert.AreEqual(0.8f, NewRepo().Load().Snapshot.Master);
            }
        }
        [Test] public void SameGenerationConflictPrefersValidMain()
        {
            files.Data = CaptureBeforeReplace();
            var main = JObject.Parse(files.Read(paths.V2Path)); main["writeGeneration"] = 2; files.Write(paths.V2Path, main.ToString());
            Assert.AreEqual(0.8f, NewRepo().Load().Snapshot.Master);
        }
        [Test] public void ReportedReplaceFailureCannotRecoverTempOnNextStartup()
        {
            files.FailMainReplace = true;
            Assert.AreEqual(SettingsPersistenceOutcome.NotCommitted, repo.Save(new SettingsSnapshot(0.2f)).Outcome);
            files.FailMainReplace = false;
            Assert.AreEqual(0.8f, NewRepo().Load().Snapshot.Master);
        }
        [Test] public void JournalCleanupFailureAfterCommitIsStillSuccess()
        {
            files.FailDelete = true;
            Assert.IsTrue(repo.Save(new SettingsSnapshot(0.2f)).Success);
            Assert.AreEqual(0.2f, NewRepo().Load().Snapshot.Master);
        }
        [Test] public void WriteDenialDoesNotDestroyOldFormalFile()
        {
            files.FailWrites = true;
            Assert.IsFalse(repo.Save(new SettingsSnapshot(0.2f)).Success);
            files.FailWrites = false; Assert.AreEqual(0.8f, NewRepo().Load().Snapshot.Master);
        }
        [Test] public void ImportSnapshotIsCreatedOnceAndLegacySourceNeverMutated()
        {
            files.Data.Clear(); int imports = 0;
            var reader = NewRepo(() => { imports++; return new SettingsSnapshot(0.3f); });
            var first = reader.Load(); string original = files.Read(paths.LegacyOriginalPath);
            reader.Save(first.Snapshot); reader.Save(new SettingsSnapshot(0.2f)); reader.Load();
            Assert.AreEqual(1, imports); Assert.AreEqual(original, files.Read(paths.LegacyOriginalPath));
        }
        [Test] public void LoadUnavailableDisablesWritesRatherThanOverwritingUnknownData()
        {
            files.FailReads = true; var reader = NewRepo(); Assert.IsTrue(reader.Load().ReadOnly);
            files.FailReads = false; Assert.AreEqual(SettingsPersistenceOutcome.ReadOnly, reader.Save(new SettingsSnapshot()).Outcome);
        }
        private Dictionary<string, string> CaptureBeforeReplace()
        {
            Dictionary<string, string> crash = null;
            files.BeforeMainReplace = () => crash = new Dictionary<string, string>(files.Data);
            Assert.IsTrue(repo.Save(new SettingsSnapshot(0.2f)).Success); files.BeforeMainReplace = null;
            Assert.IsNotNull(crash); return crash;
        }
        private sealed class MemoryFiles : ISettingsFileStore
        {
            internal Dictionary<string, string> Data = new Dictionary<string, string>();
            internal bool FailMainReplace, FailDelete, FailWrites, FailReads;
            internal Action BeforeMainReplace;
            public bool Exists(string path) => Data.ContainsKey(path);
            public string Read(string path) { if (FailReads) throw new IOException("injected read failure"); return Data[path]; }
            public void Write(string path, string text) { if (FailWrites) throw new IOException("injected write failure"); Data[path] = text; }
            public void Promote(string source, string target, string backup)
            {
                if (target.EndsWith(".v2.json", StringComparison.Ordinal))
                {
                    BeforeMainReplace?.Invoke(); if (FailMainReplace) throw new IOException("injected replace failure");
                }
                if (backup != null && Data.ContainsKey(target)) Data[backup] = Data[target];
                Data[target] = Data[source]; Data.Remove(source);
            }
            public void Delete(string path) { if (FailDelete) throw new IOException("injected cleanup failure"); Data.Remove(path); }
        }
    }
}
