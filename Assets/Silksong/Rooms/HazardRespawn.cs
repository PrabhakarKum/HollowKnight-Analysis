using System;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // Where the hero is put back after a hazard hit: a name (for debugging and matching to a marker), a world
    // position and which way to face. Data only; the marker and trigger components that fill it come later.
    // A class with public fields because JsonUtility cannot store nullable structs, and "not set yet" must survive a save.
    [Serializable]
    public class HazardRespawn
    {
        public string markerName;
        public Vector2 position;
        public bool facingRight = true;
        // The room (scene name) the spot was saved in. The position is a plain world position and means nothing in
        // any other room, so a spot from another room is ignored (see RespawnRules). Added later: old saves read it
        // as empty, which counts as "room unknown" and is never used.
        public string scene;

        public HazardRespawn() { }

        public HazardRespawn(string markerName, Vector2 position, bool facingRight, string scene)
            : this(markerName, position, facingRight)
        {
            this.scene = scene;
        }

        public HazardRespawn(string markerName, Vector2 position, bool facingRight)
        {
            this.markerName = markerName;
            this.position = position;
            this.facingRight = facingRight;
        }

        // Based on markerName alone: an empty or null name means not set, whatever position holds.
        public bool IsSet => !string.IsNullOrEmpty(markerName);
    }
}
