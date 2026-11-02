using System.Collections.Generic;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // A place the hero comes back to after dying. The name is what PlayerData stores. Like the original's
    // marker it also says which way the hero faces on arrival. Markers register themselves while enabled so a
    // saved name can be turned back into a marker.
    public class RespawnMarker : MonoBehaviour
    {
        static readonly List<RespawnMarker> Active = new List<RespawnMarker>();

        [SerializeField] string markerName;
        [SerializeField] bool facingRight = true;

        // The serialized name, or the object's name when none was set.
        public string MarkerName => string.IsNullOrEmpty(markerName) ? gameObject.name : markerName;
        public bool FacingRight => facingRight;

        // For markers built from code (tests, generated rooms).
        public void Configure(string name, bool faceRight)
        {
            markerName = name;
            facingRight = faceRight;
        }

        // True for a bench-like spot: when the world starts in its room it becomes the death respawn (PlayerData
        // respawnScene + respawnMarker). Only the Tut_01 Start marker sets it in the grey-box rooms.
        [SerializeField] bool setsBenchRespawn;
        public bool SetsBenchRespawn => setsBenchRespawn;
        public void SetBench(bool value) => setsBenchRespawn = value;

        // The enabled marker with this name in this scene, or null (both rooms have a "Start").
        public static RespawnMarker Find(string name, UnityEngine.SceneManagement.Scene scene)
        {
            if (string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < Active.Count; i++)
                if (Active[i] != null && Active[i].MarkerName == name && Active[i].gameObject.scene == scene) return Active[i];
            return null;
        }

        // The enabled marker with this name, or null.
        public static RespawnMarker Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < Active.Count; i++)
                if (Active[i] != null && Active[i].MarkerName == name) return Active[i];
            return null;
        }

        void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        void OnDisable()
        {
            Active.Remove(this);
        }
    }
}
