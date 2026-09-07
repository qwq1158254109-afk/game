using System;

namespace StudentGrowth
{
    public interface IBattleRandom
    {
        uint Seed { get; }
        int Draws { get; }
        int Next(int exclusiveMax);
        IBattleRandom Copy();
    }
    // Explicit versioned algorithm; previews use an independent copy.
    public sealed class BattleRandom : IBattleRandom
    {
        public const string Algorithm = "xorshift32-v1";
        private uint state;
        public uint Seed { get; }
        public int Draws { get; private set; }
        public BattleRandom(uint seed) { Seed=seed; state=seed==0?0x9E3779B9u:seed; }
        public int Next(int exclusiveMax)
        {
            if (exclusiveMax < 1) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            state ^= state << 13; state ^= state >> 17; state ^= state << 5; Draws++;
            return (int)(state % (uint)exclusiveMax);
        }
        public IBattleRandom Copy() => new BattleRandom(Seed) { state=state, Draws=Draws };
    }
}
