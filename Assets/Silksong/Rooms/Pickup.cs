using UnityEngine;

namespace Hollowknight.Silksong
{
    // A trigger the hero collects once by touching it. The collected flag is stored in the current
    // PlayerData.sceneData through the PersistentBool beside it, so a collected pickup stays gone when the room is
    // re-entered or a save is loaded. Needs a trigger Collider2D.
    //
    // Not built: the item itself. Collecting only records the flag; nothing is added to the hero.
    [RequireComponent(typeof(PersistentBool))]
    [RequireComponent(typeof(Collider2D))]
    public class Pickup : MonoBehaviour
    {
        PersistentBool persistent;

        public bool IsCollected { get; private set; }

        PersistentBool Persistent => persistent != null ? persistent : (persistent = GetComponent<PersistentBool>());

        // For pickups built from code: the explicit save id (unique within the room).
        public void Configure(string id) => Persistent.SetId(id);

        void Start() => Reapply();

        // Reads the stored flag from the current PlayerData and shows the pickup accordingly.
        public void Reapply() => Apply(Persistent.IsSet);

        void OnTriggerEnter2D(Collider2D other) => Touch(other);

        void OnTriggerStay2D(Collider2D other) => Touch(other);

        void Touch(Collider2D other)
        {
            if (IsCollected || other == null) return;
            if (other.GetComponentInParent<HeroController>() == null) return;
            Collect();
        }

        public void Collect()
        {
            if (IsCollected) return;
            Persistent.Set(true);
            Apply(true);
            Stubs.NotImplemented("Pickup effect: item is not granted yet");
        }

        void Apply(bool collected)
        {
            IsCollected = collected;
            foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = !collected;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = !collected;
        }
    }
}
