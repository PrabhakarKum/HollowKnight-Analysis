using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowknight.Silksong
{
    // All enabled gates, so the arrival gate can be found by name after a room loads. Replaces the original's
    // static TransitionPoint list that GameManager searches (GameManager.FindTransitionPoint, line 3170).
    public static class GateRegistry
    {
        static readonly List<TransitionPoint> Gates = new List<TransitionPoint>();

        public static int Count => Gates.Count;

        // With domain reload disabled the list would keep gates from the last play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Gates.Clear();

        public static void Register(TransitionPoint gate)
        {
            if (gate != null && !Gates.Contains(gate)) Gates.Add(gate);
        }

        public static void Unregister(TransitionPoint gate)
        {
            Gates.Remove(gate);
        }

        // Search across ALL loaded rooms. Room names repeat (Tut_01 and Tut_01b both have left1, right1...),
        // so Tasks 3 and 6 must use Find(name, scene) with the NEW room's scene; this overload is for tests.
        public static TransitionPoint Find(string gateName) => FindIn(gateName, null);

        // The gate with this name in this scene. A missing name falls back to the first gate of THAT scene
        // with a warning (the original does the same); a scene without gates logs an error and returns null.
        public static TransitionPoint Find(string gateName, Scene scene) => FindIn(gateName, scene);

        // First registered gate of the scene, or null.
        public static TransitionPoint FirstGate(Scene scene)
        {
            for (int i = 0; i < Gates.Count; i++)
                if (Gates[i] != null && Gates[i].gameObject.scene == scene) return Gates[i];
            return null;
        }

        static TransitionPoint FindIn(string gateName, Scene? scene)
        {
            TransitionPoint first = null;
            for (int i = 0; i < Gates.Count; i++)
            {
                var gate = Gates[i];
                if (gate == null) continue;   // destroyed with its scene without OnDisable running
                if (scene.HasValue && gate.gameObject.scene != scene.Value) continue;
                if (first == null) first = gate;
                if (!string.IsNullOrEmpty(gateName) && gate.name == gateName) return gate;
            }

            if (first == null)
            {
                Debug.LogError($"[Gates] Cannot find gate '{gateName}': there are no gates in the searched room.");
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
