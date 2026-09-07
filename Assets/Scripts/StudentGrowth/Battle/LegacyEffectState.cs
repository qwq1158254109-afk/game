using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace StudentGrowth
{
    // The original AddStat/ClampAll semantics run on a private copy, never on the session player.
    public sealed class LegacyEffectState
    {
        private readonly PlayerState player;
        private static readonly FieldInfo[] Fields = typeof(PlayerState).GetFields(BindingFlags.Instance | BindingFlags.Public);
        public LegacyEffectState(PlayerState source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            player = new PlayerState();
            foreach (var field in Fields) field.SetValue(player, field.GetValue(source));
        }
        public int Get(PlayerStatKey stat) => player.GetStat(stat);
        internal void Add(PlayerStatKey stat, int amount) => player.AddStat(stat, amount);
        internal LegacyEffectState Copy() => new LegacyEffectState(player);
        public IReadOnlyDictionary<PlayerStatKey, int> Snapshot()
        {
            var values = new Dictionary<PlayerStatKey, int>();
            foreach (PlayerStatKey key in Enum.GetValues(typeof(PlayerStatKey))) values.Add(key, player.GetStat(key));
            return new ReadOnlyDictionary<PlayerStatKey, int>(values);
        }
    }
}
