using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace StudentGrowth
{
    public enum SettingsPersistenceOutcome { Committed, NotCommitted, Indeterminate, ReadOnly }
    public sealed class SettingsSaveResult
    {
        public SettingsPersistenceOutcome Outcome { get; }
        public string ErrorCode { get; }
        public bool Success => Outcome == SettingsPersistenceOutcome.Committed;
        public SettingsSaveResult(SettingsPersistenceOutcome outcome, string errorCode = null) { Outcome = outcome; ErrorCode = errorCode; }
    }
    public sealed class SettingsLoadResult
    {
        public SettingsSnapshot Snapshot { get; }
        public bool Persisted { get; }
        public bool ReadOnly { get; }
        public string Notice { get; }
        public SettingsLoadResult(SettingsSnapshot snapshot, bool persisted, bool readOnly = false, string notice = null)
        { Snapshot = snapshot; Persisted = persisted; ReadOnly = readOnly; Notice = notice; }
    }
    public interface ISettingsRepository
    {
        SettingsLoadResult Load();
        SettingsSaveResult Save(SettingsSnapshot snapshot);
    }
    public sealed class SettingsPathProvider
    {
        public string DirectoryPath { get; }
        public string V2Path => Path.Combine(DirectoryPath, "StudentGrowth.Settings.v2.json");
        public string V2TemporaryPath => Path.Combine(DirectoryPath, "StudentGrowth.Settings.v2.tmp");
        public string V2BackupPath => Path.Combine(DirectoryPath, "StudentGrowth.Settings.v2.bak");
        public string V2JournalPath => Path.Combine(DirectoryPath, "StudentGrowth.Settings.v2.journal");
        public string LegacyOriginalPath => Path.Combine(DirectoryPath, "PlayerPrefs.original.json");
        public string CorruptedDirectory => Path.Combine(DirectoryPath, "Corrupted");
        public string DiagnosticsDirectory => Path.Combine(DirectoryPath, "Diagnostics");
        public SettingsPathProvider(string directoryPath) { DirectoryPath = Path.GetFullPath(directoryPath); }
    }

    // Injection boundary for deterministic IO-failure tests. All paths originate from the provider.
    public interface ISettingsFileStore
    {
        bool Exists(string path);
        string Read(string path);
        void Write(string path, string content);
        void Promote(string source, string target, string backup);
        void Delete(string path);
    }
    public sealed class SettingsFileStore : ISettingsFileStore
    {
        public bool Exists(string path) => File.Exists(path);
        public string Read(string path) => File.ReadAllText(path, Encoding.UTF8);
        public void Write(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = new UTF8Encoding(false).GetBytes(content);
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
        }
        public void Promote(string source, string target, string backup)
        {
            if (File.Exists(target)) File.Replace(source, target, backup);
            else File.Move(source, target);
        }
        public void Delete(string path) { if (File.Exists(path)) File.Delete(path); }
    }
    internal enum CommitStage { WritingTemporary, TemporaryValidated, ReplaceStarted, ReplaceSucceeded, ReplaceFailedReported }
    internal sealed class SettingsCommitJournal
    {
        public string OperationId { get; set; }
        public long TargetGeneration { get; set; }
        public string ContentHash { get; set; }
        public CommitStage Stage { get; set; }
    }

    public sealed class SettingsRepository : ISettingsRepository
    {
        private readonly SettingsPathProvider paths;
        private readonly ISettingsFileStore files;
        private readonly Func<SettingsSnapshot> importLegacy;
        private readonly SettingsSnapshot defaults;
        private readonly object gate = new object();
        private long generation;
        private bool readOnly;
        private sealed class Candidate
        {
            internal string Json; internal SettingsDataV2 Data; internal bool Future; internal bool Repaired;
            internal bool Valid => Data != null;
        }
        public SettingsRepository(SettingsPathProvider paths, Func<SettingsSnapshot> importLegacy,
            SettingsSnapshot defaults = null, ISettingsFileStore files = null)
        { this.paths = paths; this.importLegacy = importLegacy; this.defaults = defaults ?? new SettingsSnapshot(); this.files = files ?? new SettingsFileStore(); }

        public SettingsLoadResult Load()
        {
            lock (gate)
            {
                try
                {
                    var main = ReadCandidate(paths.V2Path);
                    var backup = ReadCandidate(paths.V2BackupPath);
                    var temp = ReadCandidate(paths.V2TemporaryPath);
                    if (main.Future || backup.Future || temp.Future)
                    {
                        readOnly = true;
                        return new SettingsLoadResult(defaults.Copy(), false, true, "SETTINGS_NEWER_VERSION");
                    }
                    generation = Math.Max(main.Valid ? main.Data.Generation : 0, backup.Valid ? backup.Data.Generation : 0);
                    var journal = ReadJournal();
                    bool recover = temp.Valid && journal != null &&
                        (journal.Stage == CommitStage.TemporaryValidated || journal.Stage == CommitStage.ReplaceStarted) &&
                        !string.IsNullOrEmpty(journal.OperationId) && journal.OperationId == temp.Data.OperationId &&
                        journal.TargetGeneration == temp.Data.Generation && journal.ContentHash == Hash(temp.Json) &&
                        temp.Data.Generation > generation;
                    if (recover)
                    {
                        ProtectInvalidMain(main);
                        files.Promote(paths.V2TemporaryPath, paths.V2Path, main.Valid ? paths.V2BackupPath : null);
                        var observed = ReadCandidate(paths.V2Path);
                        if (!observed.Valid || Hash(observed.Json) != journal.ContentHash) throw new IOException("Recovery verification failed");
                        generation = observed.Data.Generation;
                        CleanupCommit();
                        return new SettingsLoadResult(SettingsSerializer.Decode(observed.Data), !observed.Repaired, false, "SETTINGS_INTERRUPTED_COMMIT_RECOVERED");
                    }
                    if (main.Valid)
                    {
                        // Only maintenance failures are ignored; the valid main remains authoritative.
                        TryQuarantineTemp();
                        return new SettingsLoadResult(SettingsSerializer.Decode(main.Data), !main.Repaired, false,
                            main.Repaired ? "SETTINGS_FIELDS_REPAIRED" : null);
                    }
                    if (backup.Valid)
                        return new SettingsLoadResult(SettingsSerializer.Decode(backup.Data), false, false, "SETTINGS_BACKUP_RESTORED");
                    var legacy = importLegacy?.Invoke();
                    if (legacy != null)
                    {
                        if (!files.Exists(paths.LegacyOriginalPath))
                        {
                            // Import is read-only. The first snapshot of legacy keys never rotates.
                            string original = JsonConvert.SerializeObject(SettingsSerializer.Encode(legacy, 0, null), SettingsSerializer.Options);
                            string staging = paths.LegacyOriginalPath + ".tmp";
                            files.Write(staging, original);
                            if (files.Read(staging) != original) throw new IOException("Legacy snapshot verification failed");
                            files.Promote(staging, paths.LegacyOriginalPath, null);
                        }
                        return new SettingsLoadResult(legacy.Copy(), false, false, "SETTINGS_LEGACY_IMPORTED");
                    }
                    return new SettingsLoadResult(defaults.Copy(), false, false, main.Json != null ? "SETTINGS_DEFAULTS_RESTORED" : null);
                }
                catch (Exception)
                {
                    // IO unavailability must not be treated as permission to overwrite an unreadable file.
                    readOnly = true;
                    return new SettingsLoadResult(defaults.Copy(), false, true, "SETTINGS_LOAD_UNAVAILABLE");
                }
            }
        }

        public SettingsSaveResult Save(SettingsSnapshot snapshot)
        {
            lock (gate)
            {
                if (readOnly) return new SettingsSaveResult(SettingsPersistenceOutcome.ReadOnly, "SETTINGS_READ_ONLY");
                SettingsCommitJournal journal = null;
                bool replaceRequested = false;
                string oldHash = null;
                try
                {
                    var main = ReadCandidate(paths.V2Path);
                    var backup = ReadCandidate(paths.V2BackupPath);
                    if (main.Future || backup.Future) { readOnly = true; return new SettingsSaveResult(SettingsPersistenceOutcome.ReadOnly, "SETTINGS_NEWER_VERSION"); }
                    generation = Math.Max(generation, Math.Max(main.Valid ? main.Data.Generation : 0, backup.Valid ? backup.Data.Generation : 0));
                    var next = checked(generation + 1);
                    var operation = Guid.NewGuid().ToString("N");
                    var json = JsonConvert.SerializeObject(SettingsSerializer.Encode(snapshot, next, operation), SettingsSerializer.Options);
                    oldHash = main.Json == null ? null : Hash(main.Json);
                    journal = new SettingsCommitJournal { OperationId = operation, TargetGeneration = next, ContentHash = Hash(json), Stage = CommitStage.WritingTemporary };
                    WriteJournal(journal);
                    files.Write(paths.V2TemporaryPath, json);
                    var temp = ReadCandidate(paths.V2TemporaryPath);
                    if (!temp.Valid || Hash(temp.Json) != journal.ContentHash) throw new IOException("Temporary verification failed");
                    journal.Stage = CommitStage.TemporaryValidated; WriteJournal(journal);
                    ProtectInvalidMain(main);
                    journal.Stage = CommitStage.ReplaceStarted; WriteJournal(journal);
                    replaceRequested = true;
                    files.Promote(paths.V2TemporaryPath, paths.V2Path, main.Valid ? paths.V2BackupPath : null);
                    var observed = ReadCandidate(paths.V2Path);
                    if (!observed.Valid || Hash(observed.Json) != journal.ContentHash) throw new IOException("Main verification failed");
                    generation = next;
                    // The verified main is the commit point. Journal cleanup failure cannot undo it.
                    try { journal.Stage = CommitStage.ReplaceSucceeded; WriteJournal(journal); CleanupCommit(); } catch (Exception) { }
                    return new SettingsSaveResult(SettingsPersistenceOutcome.Committed);
                }
                catch (Exception)
                {
                    if (replaceRequested)
                    {
                        try
                        {
                            var now = ReadCandidate(paths.V2Path);
                            if (now.Valid && Hash(now.Json) == journal.ContentHash)
                            { generation = now.Data.Generation; return new SettingsSaveResult(SettingsPersistenceOutcome.Committed); }
                            if (now.Json != null && Hash(now.Json) != oldHash)
                                return new SettingsSaveResult(SettingsPersistenceOutcome.Indeterminate, "SETTINGS_SAVE_UNCERTAIN");
                        }
                        catch (Exception) { return new SettingsSaveResult(SettingsPersistenceOutcome.Indeterminate, "SETTINGS_SAVE_UNCERTAIN"); }
                    }
                    // Do not claim definite failure unless the recoverable journal is durably invalidated.
                    try
                    {
                        if (journal != null) { journal.Stage = CommitStage.ReplaceFailedReported; WriteJournal(journal); }
                    }
                    catch (Exception)
                    {
                        try { files.Delete(paths.V2TemporaryPath); }
                        catch (Exception) { return new SettingsSaveResult(SettingsPersistenceOutcome.Indeterminate, "SETTINGS_SAVE_UNCERTAIN"); }
                    }
                    TryQuarantineTemp();
                    return new SettingsSaveResult(SettingsPersistenceOutcome.NotCommitted, "SETTINGS_SAVE_FAILED");
                }
            }
        }

        private Candidate ReadCandidate(string path)
        {
            if (!files.Exists(path)) return new Candidate();
            var c = new Candidate { Json = files.Read(path) };
            try { c.Data = SettingsSerializer.Parse(c.Json, out c.Future, out c.Repaired); }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException || ex is FormatException || ex is OverflowException) { }
            return c;
        }
        private SettingsCommitJournal ReadJournal()
        {
            try { return files.Exists(paths.V2JournalPath) ? JsonConvert.DeserializeObject<SettingsCommitJournal>(files.Read(paths.V2JournalPath), SettingsSerializer.Options) : null; }
            catch (JsonException) { return null; }
        }
        private void WriteJournal(SettingsCommitJournal journal)
        {
            string next = paths.V2JournalPath + ".next";
            files.Write(next, JsonConvert.SerializeObject(journal, SettingsSerializer.Options));
            files.Promote(next, paths.V2JournalPath, null);
        }
        private void ProtectInvalidMain(Candidate main)
        {
            if (main.Json == null || main.Valid) return;
            string copy = Path.Combine(paths.CorruptedDirectory, "settings-" + Guid.NewGuid().ToString("N") + ".json");
            files.Write(copy, main.Json);
            // Replace may overwrite the corrupt main, but must never rotate it into a valid backup.
        }
        private void TryQuarantineTemp()
        {
            try
            {
                if (!files.Exists(paths.V2TemporaryPath)) return;
                string copy = Path.Combine(paths.DiagnosticsDirectory, "uncommitted-" + Guid.NewGuid().ToString("N") + ".json");
                files.Write(copy, files.Read(paths.V2TemporaryPath)); files.Delete(paths.V2TemporaryPath);
            }
            catch (Exception) { }
        }
        private void CleanupCommit() { files.Delete(paths.V2TemporaryPath); files.Delete(paths.V2JournalPath); }
        private static string Hash(string content)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(content))).Replace("-", "");
        }
    }
}
