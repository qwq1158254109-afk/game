using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    public static class GameplayInputGate
    {
        private static readonly HashSet<object> Blockers = new HashSet<object>();
        private static int consumeThroughFrame = -1;

        public static bool IsBlocked => Blockers.Count > 0 || Time.frameCount <= consumeThroughFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            Blockers.Clear();
            consumeThroughFrame = -1;
        }

        public static void Acquire(object owner)
        {
            if (owner != null)
            {
                Blockers.Add(owner);
            }
        }

        public static void Release(object owner, bool consumeCurrentFrame = false)
        {
            if (owner != null)
            {
                Blockers.Remove(owner);
            }

            if (consumeCurrentFrame)
            {
                consumeThroughFrame = Mathf.Max(consumeThroughFrame, Time.frameCount);
            }
        }
    }
}
