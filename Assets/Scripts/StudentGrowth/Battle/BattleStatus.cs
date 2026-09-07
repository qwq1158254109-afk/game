using System;

namespace StudentGrowth
{
    public enum BattleStatusKind { Focus, Distraction, WeakDefense }

    // Immutable values can safely be shared by snapshots; updates replace the entire value.
    public sealed class BattleStatus
    {
        public BattleStatusKind Kind { get; }
        public int Intensity { get; }
        public int ActiveFromPlayerTurn { get; }
        public int ExpiresAfterPlayerTurn { get; }
        public bool IsPersistent => Kind == BattleStatusKind.Focus;
        public bool IsActive(int turn) => turn >= ActiveFromPlayerTurn && turn <= ExpiresAfterPlayerTurn;
        public int RemainingTurns(int turn) => IsPersistent ? 0 : Math.Max(0, ExpiresAfterPlayerTurn - Math.Max(turn, ActiveFromPlayerTurn) + 1);
        internal BattleStatus(BattleStatusKind kind, int intensity, int from, int expires)
        { Kind=kind; Intensity=intensity; ActiveFromPlayerTurn=from; ExpiresAfterPlayerTurn=expires; }
    }
}
