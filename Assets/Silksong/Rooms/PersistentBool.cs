using UnityEngine;

namespace Hollowknight.Silksong
{
    // Put on an object whose on/off state must outlive its room (a breakable that stays broken). The room's
    // transition code calls Save before leaving and Restore after loading, like the original's save-level-state
    // step. Wiring that call is the transition manager's job; here it is explicit so it can be tested.
    public class PersistentBool : MonoBehaviour
    {
        [SerializeField] string itemId;

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
            if (data != null) data.SetBool(sceneName, ItemId, Value);
        }

        // Leaves Value alone when nothing was ever saved, so the object keeps its authored state.
        public void Restore()
        {
            if (data != null && data.HasBool(sceneName, ItemId)) Value = data.GetBool(sceneName, ItemId);
        }
    }
}
