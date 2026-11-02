using System.Collections.Generic;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // Put on an object whose on/off state must outlive its room (a breakable that stays broken). The room's
    // transition code calls Save before leaving and Restore after loading, like the original's save-level-state
    // step. Wiring that call is the transition manager's job; here it is explicit so it can be tested.
    public class PersistentBool : MonoBehaviour
    {
        [SerializeField] string itemId;

        // Semi-persistent items (reset on bench rest) are not built; this flag only records the request.
        [SerializeField] bool semiPersistent;

        // Which live PersistentBool first claimed each scene+id, to warn when two share an id (they would overwrite each other).
        static readonly Dictionary<string, PersistentBool> Owners = new Dictionary<string, PersistentBool>();

        SceneData data;
        string sceneName;

        public bool Value { get; set; }

        // The serialized id, or the object's name when none was set.
        public string ItemId => string.IsNullOrEmpty(itemId) ? gameObject.name : itemId;

        public void Configure(SceneData sceneData, string scene, string id)
        {
            data = sceneData;
            sceneName = scene;
            itemId = id;
        }

        public void Save()
        {
            if (data == null) return;
            if (semiPersistent) Stubs.NotImplemented("PersistentBool.SemiPersistent");
            WarnIfIdShared();
            data.SetBool(sceneName, ItemId, Value);
        }

        void WarnIfIdShared()
        {
            string key = sceneName + "\n" + ItemId;
            PersistentBool owner;
            if (Owners.TryGetValue(key, out owner) && owner != null && owner != this)
                Debug.LogWarning("[PersistentBool] '" + ItemId + "' in scene '" + sceneName + "' is used by two objects; their saved state will overwrite each other.", this);
            else
                Owners[key] = this;
        }

        // Leaves Value alone when nothing was ever saved, so the object keeps its authored state.
        public void Restore()
        {
            if (data != null && data.HasBool(sceneName, ItemId)) Value = data.GetBool(sceneName, ItemId);
        }
    }
}
