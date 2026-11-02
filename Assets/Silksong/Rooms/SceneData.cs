using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // Per-room memory of one-off things (breakable broken, lever pulled, pickup taken), keyed by scene
    // name + item id. Stored as flat lists because JsonUtility cannot serialise dictionaries; lookups go through
    // an index built lazily after loading. Plain C#, so it is testable without a scene.
    [Serializable]
    public class SceneData : ISerializationCallbackReceiver
    {
        [Serializable]
        public class BoolEntry { public string scene; public string id; public bool value; }

        [Serializable]
        public class IntEntry { public string scene; public string id; public int value; }

        // Serialisation only: mutate through Set*, never by editing these lists, or the lookup index goes stale
        // (call Invalidate() if you must).
        public List<BoolEntry> bools = new List<BoolEntry>();
        public List<IntEntry> ints = new List<IntEntry>();

        [NonSerialized] Dictionary<string, BoolEntry> boolIndex;
        [NonSerialized] Dictionary<string, IntEntry> intIndex;

        // Length-prefixing the scene keeps the key unambiguous for any characters, including a separator
        // sitting next to the join (("a\n","b") vs ("a","\nb")).
        static string Key(string scene, string id) => (scene == null ? 0 : scene.Length) + ":" + scene + id;

        // Drops the lookup index so it is rebuilt from the lists on next use.
        public void Invalidate()
        {
            boolIndex = null;
            intIndex = null;
        }

        public void OnBeforeSerialize() { }

        // FromJsonOverwrite replaces the lists under an already-built index; rebuild it.
        public void OnAfterDeserialize() => Invalidate();

        public bool HasBool(string scene, string id) => BoolIndex().ContainsKey(Key(scene, id));

        public bool GetBool(string scene, string id)
        {
            BoolEntry e;
            return BoolIndex().TryGetValue(Key(scene, id), out e) && e.value;
        }

        public void SetBool(string scene, string id, bool value)
        {
            var index = BoolIndex();
            BoolEntry e;
            if (index.TryGetValue(Key(scene, id), out e)) { e.value = value; return; }
            e = new BoolEntry { scene = scene, id = id, value = value };
            bools.Add(e);
            index[Key(scene, id)] = e;
        }

        public int GetInt(string scene, string id)
        {
            IntEntry e;
            return IntIndex().TryGetValue(Key(scene, id), out e) ? e.value : 0;
        }

        public void SetInt(string scene, string id, int value)
        {
            var index = IntIndex();
            IntEntry e;
            if (index.TryGetValue(Key(scene, id), out e)) { e.value = value; return; }
            e = new IntEntry { scene = scene, id = id, value = value };
            ints.Add(e);
            index[Key(scene, id)] = e;
        }

        Dictionary<string, BoolEntry> BoolIndex()
        {
            if (bools == null) bools = new List<BoolEntry>();
            if (boolIndex == null)
            {
                boolIndex = new Dictionary<string, BoolEntry>();
                // A hand-edited file could repeat a key; the last entry wins, as if set in order.
                foreach (var e in bools) if (e != null) boolIndex[Key(e.scene, e.id)] = e;
            }
            return boolIndex;
        }

        Dictionary<string, IntEntry> IntIndex()
        {
            if (ints == null) ints = new List<IntEntry>();
            if (intIndex == null)
            {
                intIndex = new Dictionary<string, IntEntry>();
                foreach (var e in ints) if (e != null) intIndex[Key(e.scene, e.id)] = e;
            }
            return intIndex;
        }
    }
}
