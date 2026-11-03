using System.Collections.Generic;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // Pure decisions behind the persistent objects, kept out of the components so they can be tested without a scene.
    public static class PersistentRules
    {
        // An object starts in its "done" state (broken, collected) only when its flag was stored AND is true.
        // No entry means the room was never touched, so the object keeps its authored (intact) state.
        public static bool StartsDone(bool hasEntry, bool value) => hasEntry && value;

        // Why an id cannot be used, or null when it is fine. The id is part of the save key (scene name + id), so it
        // must be written by hand and stay stable; the object's name is not a good key because renaming would lose
        // the saved state.
        public static string IdProblem(string id) => string.IsNullOrEmpty(id) ? "has no Id" : null;
    }

    // Put on an object whose on/off state must outlive its room (a breakable that stays broken). The state lives in
    // the CURRENT PlayerData.sceneData, keyed by scene name + Id.
    //
    // Which storage: the running world's PlayerData (RoomWorldBootstrap.Instance.PlayerData) is read at the moment of
    // use, never cached, because loading a save replaces that object. With no world (isolated tests, a room opened
    // alone) a static fallback PlayerData is used; tests may set PersistentBool.Fallback and must call ResetStatics
    // in teardown. Configure still pins a specific SceneData (the older explicit Save/Restore use).
    //
    // Two entry points: Save/Restore with Value (explicit, as before), and IsSet/Set (read and write the stored flag
    // right now). BreakableWall and Pickup use IsSet/Set.
    public class PersistentBool : MonoBehaviour
    {
        [SerializeField] string itemId;

        // Semi-persistent items (reset on bench rest) are not built; this flag only records the request.
        [SerializeField] bool semiPersistent;

        // Which live PersistentBool first claimed each scene+id, to warn when two share an id (they would overwrite each other).
        static readonly Dictionary<string, PersistentBool> Owners = new Dictionary<string, PersistentBool>();

        static PlayerData fallback;

        // Storage used when no world is running. Created on first use; tests can assign their own.
        public static PlayerData Fallback
        {
            get => fallback ?? (fallback = PlayerData.CreateNew());
            set => fallback = value;
        }

        // Forgets the fallback data and the id claims (test teardown, play-mode start with domain reload off).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics()
        {
            fallback = null;
            Owners.Clear();
        }

        // The SceneData in force right now: the running world's, else the fallback's.
        public static SceneData CurrentSceneData()
        {
            var world = RoomWorldBootstrap.Instance;
            var pd = world != null && world.PlayerData != null ? world.PlayerData : Fallback;
            if (pd.sceneData == null) pd.sceneData = new SceneData();
            return pd.sceneData;
        }

        SceneData data;
        string sceneName;

        public bool Value { get; set; }

        // The serialized id, or the object's name when none was set.
        public string ItemId => string.IsNullOrEmpty(itemId) ? gameObject.name : itemId;

        // True when an id was written on purpose (the rule: explicit ids only).
        public bool HasExplicitId => PersistentRules.IdProblem(itemId) == null;

        // The scene half of the key: the scene this object lives in, unless Configure gave one.
        public string SceneName => string.IsNullOrEmpty(sceneName) ? gameObject.scene.name : sceneName;

        // The storage, looked up on every use so a replaced PlayerData is honoured.
        SceneData Data => data ?? CurrentSceneData();

        public void Configure(SceneData sceneData, string scene, string id)
        {
            data = sceneData;
            sceneName = scene;
            itemId = id;
        }

        // For objects built from code (generated rooms, tests). The data and scene stay automatic.
        public void SetId(string id) => itemId = id;

        // Whether the stored flag is true now. No entry counts as false.
        public bool IsSet
        {
            get
            {
                var d = Data;
                return PersistentRules.StartsDone(d.HasBool(SceneName, ItemId), d.GetBool(SceneName, ItemId));
            }
        }

        // Writes the flag into the current storage immediately.
        public void Set(bool value)
        {
            WarnIfIdShared();
            Value = value;
            Data.SetBool(SceneName, ItemId, value);
        }

        void Start()
        {
            // Checked once when the object comes alive, not every frame.
            if (!HasExplicitId)
                Debug.LogWarning("[PersistentBool] '" + gameObject.name + "' " + PersistentRules.IdProblem(itemId) + "; set an explicit Id or its state will follow the object's name.", this);
            WarnIfIdShared();
        }

        void OnDestroy()
        {
            string key = SceneName + "\n" + ItemId;
            PersistentBool owner;
            if (Owners.TryGetValue(key, out owner) && owner == this) Owners.Remove(key);
        }

        public void Save()
        {
            if (semiPersistent) Stubs.NotImplemented("PersistentBool.SemiPersistent");
            WarnIfIdShared();
            Data.SetBool(SceneName, ItemId, Value);
        }

        void WarnIfIdShared()
        {
            string key = SceneName + "\n" + ItemId;
            PersistentBool owner;
            if (Owners.TryGetValue(key, out owner) && owner != null && owner != this)
                Debug.LogWarning("[PersistentBool] '" + ItemId + "' in scene '" + SceneName + "' is used by two objects; their saved state will overwrite each other.", this);
            else
                Owners[key] = this;
        }

        // Leaves Value alone when nothing was ever saved, so the object keeps its authored state.
        public void Restore()
        {
            var d = Data;
            if (d.HasBool(SceneName, ItemId)) Value = d.GetBool(SceneName, ItemId);
        }
    }
}
