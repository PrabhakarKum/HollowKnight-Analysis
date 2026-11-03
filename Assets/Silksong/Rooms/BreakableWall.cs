using UnityEngine;

namespace Hollowknight.Silksong
{
    // A solid wall that breaks once when the hero's nail hits it and stays broken: the broken flag is stored in the
    // current PlayerData.sceneData through the PersistentBool beside it, so leaving the room and coming back (or
    // loading a save) shows it broken. The nail finds it through IHittable, like any target.
    //
    // Needs a solid (non-trigger) Collider2D and optionally renderers. Breaking disables them rather than destroying
    // the object, so the object can be told to re-read its state (Reapply).
    //
    // Not built: break particles, sound, camera shake, the damage threshold (any hit breaks it).
    [RequireComponent(typeof(PersistentBool))]
    public class BreakableWall : MonoBehaviour, IHittable
    {
        PersistentBool persistent;

        public bool IsBroken { get; private set; }

        PersistentBool Persistent => persistent != null ? persistent : (persistent = GetComponent<PersistentBool>());

        // For walls built from code: the explicit save id (unique within the room).
        public void Configure(string id) => Persistent.SetId(id);

        void Start() => Reapply();

        // Reads the stored flag from the current PlayerData and shows the wall accordingly. Called on start; call again
        // if the player data is replaced while the room is live.
        public void Reapply() => Apply(Persistent.IsSet);

        public bool TakeHit(HitInstance hit)
        {
            if (IsBroken) return false;
            Break();
            return true;
        }

        public void Break()
        {
            if (IsBroken) return;
            Persistent.Set(true);
            Apply(true);
            Stubs.NotImplemented("Breakable wall: break effect and sound");
        }

        void Apply(bool broken)
        {
            IsBroken = broken;
            foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = !broken;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = !broken;
        }
    }
}
