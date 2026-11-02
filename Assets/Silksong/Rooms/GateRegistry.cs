using System.Collections.Generic;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // All enabled gates, so the arrival gate can be found by name after a room loads. Replaces the original's
    // static TransitionPoint list that GameManager searches (GameManager.FindTransitionPoint, line 3170).
    public static class GateRegistry
    {
        static readonly List<TransitionPoint> Gates = new List<TransitionPoint>();

        public static int Count => Gates.Count;

        public static void Register(TransitionPoint gate)
        {
            if (gate != null && !Gates.Contains(gate)) Gates.Add(gate);
        }

        public static void Unregister(TransitionPoint gate)
        {
            Gates.Remove(gate);
        }

        // The first gate with exactly this name. A missing name falls back to the first gate with a warning
        // rather than failing: a typo in room data should still put the hero somewhere in the room (the
        // original does the same). With no gates at all there is nothing to fall back on, so it logs an
        // error and returns null.
        public static TransitionPoint Find(string gateName)
        {
            TransitionPoint first = null;
            for (int i = 0; i < Gates.Count; i++)
            {
                var gate = Gates[i];
                if (gate == null) continue;   // destroyed with its scene without OnDisable running
                if (first == null) first = gate;
                if (!string.IsNullOrEmpty(gateName) && gate.name == gateName) return gate;
            }

            if (first == null)
            {
                Debug.LogError($"[Gates] Cannot find gate '{gateName}': there are no gates in the loaded rooms.");
                return null;
            }

            Debug.LogWarning($"[Gates] Gate '{gateName}' not found, using the first gate '{first.name}' instead.");
            return first;
        }

        public static void ClearForTests()
        {
            Gates.Clear();
        }
    }
}
