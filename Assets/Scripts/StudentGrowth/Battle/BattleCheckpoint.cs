using System;
using System.Collections;
using System.Reflection;

namespace StudentGrowth
{
    // Only public session data is copied. Unity asset references stay references; runtime lists
    // and dictionaries are detached. No MonoBehaviour, consumer, event or flow token is copied.
    public sealed class BattleCheckpoint
    {
        private readonly GameSessionState data;
        public BattleCheckpoint(GameSessionState source) { data = Copy(source); }
        public GameSessionState CreateWorkingCopy() => Copy(data);
        public bool Matches(GameSessionState live) => Equal(data, live);
        public void Restore(GameSessionState live)
        {
            var copy = Copy(data);
            foreach (var field in Fields(typeof(GameSessionState)))
            {
                var value = field.GetValue(copy);
                // Preserve roots held by HUDs/controllers, especially PlayerState identity.
                if (field.Name == nameof(GameSessionState.playerState) || field.Name == nameof(GameSessionState.stageState)
                    || field.Name == nameof(GameSessionState.timeActionState) || field.Name == nameof(GameSessionState.deckState)
                    || field.Name == nameof(GameSessionState.itemInventory) || field.Name == nameof(GameSessionState.locationState))
                {
                    var target = field.GetValue(live);
                    if (target != null && value != null)
                    { foreach (var member in Fields(value.GetType())) member.SetValue(target, member.GetValue(value)); continue; }
                }
                field.SetValue(live, value);
            }
        }
        private static FieldInfo[] Fields(Type type) => type.GetFields(BindingFlags.Instance | BindingFlags.Public);
        public static T Copy<T>(T value) => (T)Clone(value);
        private static object Clone(object value)
        {
            if (value == null) return null;
            var type = value.GetType();
            if (type.IsValueType || value is string || value is UnityEngine.Object) return value;
            var copy = Activator.CreateInstance(type);
            if (value is IDictionary dictionary)
            { foreach (DictionaryEntry entry in dictionary) ((IDictionary)copy).Add(Clone(entry.Key), Clone(entry.Value)); }
            else if (value is IList list) { foreach (var entry in list) ((IList)copy).Add(Clone(entry)); }
            else foreach (var field in Fields(type)) field.SetValue(copy, Clone(field.GetValue(value)));
            return copy;
        }
        private static bool Equal(object a, object b)
        {
            if (ReferenceEquals(a,b)) return true;
            if (a == null || b == null || a.GetType()!=b.GetType()) return false;
            if (a.GetType().IsValueType || a is string || a is UnityEngine.Object) return a.Equals(b);
            if (a is IDictionary da && b is IDictionary db)
            { if(da.Count!=db.Count)return false; foreach(DictionaryEntry e in da)if(!db.Contains(e.Key)||!Equal(e.Value,db[e.Key]))return false;return true; }
            if (a is IList la && b is IList lb)
            { if(la.Count!=lb.Count)return false;for(int i=0;i<la.Count;i++)if(!Equal(la[i],lb[i]))return false;return true; }
            foreach(var f in Fields(a.GetType()))if(!Equal(f.GetValue(a),f.GetValue(b)))return false;
            return true;
        }
    }
}
