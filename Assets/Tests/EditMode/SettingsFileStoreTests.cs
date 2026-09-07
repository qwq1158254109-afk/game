using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public sealed class SettingsFileStoreTests
    {
        private SettingsPathProvider paths;
        [SetUp] public void SetUp()
        {
            paths = new SettingsPathProvider(Path.Combine(Application.dataPath, "../Temp/SettingsIOTests", Guid.NewGuid().ToString("N")));
        }
        [Test] public void UnityRuntimeCanCommitReloadAndRecoverOnTheWorkspaceFilesystem()
        {
            var repo = new SettingsRepository(paths, null);
            Assert.IsTrue(repo.Save(new SettingsSnapshot(0.8f)).Success);
            Assert.IsTrue(repo.Save(new SettingsSnapshot(0.2f)).Success);
            Assert.AreEqual(0.2f, new SettingsRepository(paths, null).Load().Snapshot.Master);
            File.WriteAllText(paths.V2Path, "truncated");
            Assert.AreEqual(0.8f, new SettingsRepository(paths, null).Load().Snapshot.Master);
        }
        [Test] public void LockedFormalFileIsPreservedWhenSaveFails()
        {
            var repo = new SettingsRepository(paths, null);
            Assert.IsTrue(repo.Save(new SettingsSnapshot(0.8f)).Success);
            using (var held = new FileStream(paths.V2Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.IsFalse(repo.Save(new SettingsSnapshot(0.2f)).Success);
            Assert.AreEqual(0.8f, new SettingsRepository(paths, null).Load().Snapshot.Master);
        }
    }
}
