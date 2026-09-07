using System;
using System.Collections;
using System.Collections.Generic;

namespace StudentGrowth
{
    public readonly struct SaveSlotId
    {
        public const int Count = 10;
        public int Value { get; }
        public bool IsAutomatic => Value == 0;
        public SaveSlotId(int value) { if (value < 0 || value >= Count) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
    }
    public interface IReadOnlySaveSlotSummary
    {
        SaveSlotId SlotId { get; }
        bool IsEmpty { get; }
        bool IsValid { get; }
        int Day { get; }
        double PlayedSeconds { get; }
        string ErrorCode { get; }
    }
    public sealed class SaveSlotSummary : IReadOnlySaveSlotSummary
    {
        public SaveSlotId SlotId { get; }
        public bool IsEmpty { get; }
        public bool IsValid { get; }
        public int Day { get; }
        public double PlayedSeconds { get; }
        public string ErrorCode { get; }
        public string Revision { get; }
        public string RunId { get; set; }
        public long Sequence { get; set; }
        public string SavedAtUtc { get; set; }
        public string LocationId { get; set; }
        public string ScenePath { get; set; }
        public TimePeriod Period { get; set; }
        public SaveSlotSummary(SaveSlotId slot, bool empty, bool valid, int day = 0, double seconds = 0, string error = null, string revision = null)
        { SlotId = slot; IsEmpty = empty; IsValid = valid; Day = day; PlayedSeconds = seconds; ErrorCode = error; Revision = revision; }
    }
    // Plain, versioned game data. Never serialize a MonoBehaviour or a Unity object reference.
    [Serializable] public sealed class SaveGameRecord
    {
        public int version = 1;
        public string runId;
        public long saveSequence;
        public string savedAtUtc;
        public bool tutorialSeen;
        public string scenePath;
        public bool hasPlayerPosition;
        public float playerX, playerY, facingX, facingY = -1;
        public double playedSeconds;
        public PlayerState player;
        public TimeActionState time;
        public LocationState location;
        public string currentStageId, endingId;
        public int remainingTime, clearedStageCount, defeatedBossCount;
        public bool bossUnlocked, bossDefeated, endingReached;
        public List<string> cardIds = new List<string>();
        public List<string> itemIds = new List<string>();
        public List<string> activityIds = new List<string>();
        public List<string> stageIds = new List<string>();
        public List<string> clearedStageIds = new List<string>();
        public List<string> defeatedBossIds = new List<string>();
        public List<string> unlockedActivityIds = new List<string>();
        public List<string> unlockedLocationIds = new List<string>();
    }
    public sealed class LoadedGameState
    {
        public SaveGameRecord Record { get; }
        public GameSessionState Session { get; }
        internal LoadedGameState(SaveGameRecord record, GameSessionState session) { Record = record; Session = session; }
    }
    public class CommandResult
    {
        public bool Success { get; }
        public string ErrorCode { get; }
        public CommandResult(bool success, string error = null) { Success = success; ErrorCode = error; }
    }
    public sealed class SaveOperationResult : CommandResult
    { public SaveOperationResult(bool success, string error = null) : base(success,error) { } }
    public sealed class LoadOperationResult : CommandResult
    {
        public LoadedGameState State { get; }
        public LoadOperationResult(LoadedGameState state, string error = null) : base(state != null,error) { State = state; }
    }
    public sealed class GameFlowRestoreResult : CommandResult
    {
        public IReadOnlyList<PostLoadHandlerResult> PostLoadResults { get; }
        public GameFlowRestoreResult(bool success, string error = null, IReadOnlyList<PostLoadHandlerResult> post = null) : base(success,error) { PostLoadResults = post; }
    }
    public interface ISaveLoadService
    {
        IReadOnlyList<IReadOnlySaveSlotSummary> GetSlotSummaries();
        IEnumerator SaveAsync(SaveSlotId slot, Action<SaveOperationResult> completed);
        IEnumerator LoadAsync(SaveSlotId slot, Action<LoadOperationResult> completed);
    }
    public interface IGameFlowService
    { IEnumerator RestoreLoadedGameAsync(LoadedGameState state, Action<GameFlowRestoreResult> completed); }
}
