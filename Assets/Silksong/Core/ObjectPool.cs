using System.Collections.Generic;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // Minimal prefab pool for effects and projectiles, so attacks do not allocate every swing.
    public static class ObjectPool
    {
        static readonly Dictionary<int, Stack<GameObject>> Idle = new Dictionary<int, Stack<GameObject>>();
        // Remembers which prefab each live instance came from, so Despawn needs only the instance.
        static readonly Dictionary<GameObject, int> Origin = new Dictionary<GameObject, int>();

        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            int key = prefab.gameObject.GetInstanceID();
            GameObject go = null;

            if (Idle.TryGetValue(key, out var stack))
            {
                // Scene unloads destroy pooled objects behind our back; skip those dead entries.
                while (stack.Count > 0 && go == null) go = stack.Pop();
            }

            if (go == null)
            {
                go = Object.Instantiate(prefab.gameObject, position, rotation);
                Origin[go] = key;
            }
            else
            {
                go.transform.SetPositionAndRotation(position, rotation);
            }

            go.SetActive(true);
            foreach (var p in go.GetComponents<IPoolable>()) p.OnSpawn();
            return go.GetComponent<T>();
        }

        public static void Despawn(GameObject instance)
        {
            if (instance == null) return;

            if (!Origin.TryGetValue(instance, out int key))
            {
                // Not ours: destroying is safer than parking an object we cannot hand back correctly.
                Object.Destroy(instance);
                return;
            }

            foreach (var p in instance.GetComponents<IPoolable>()) p.OnDespawn();
            instance.SetActive(false);

            if (!Idle.TryGetValue(key, out var stack))
            {
                stack = new Stack<GameObject>();
                Idle[key] = stack;
            }
            stack.Push(instance);
        }
    }
}
