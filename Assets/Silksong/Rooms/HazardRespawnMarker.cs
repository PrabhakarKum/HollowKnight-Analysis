using UnityEngine;

namespace Hollowknight.Silksong
{
    // An empty marker that says "put the hero back here after a hazard hit", with a facing. Like the original, it
    // looks for ground straight below itself when it wakes. With no ground below it simply uses its own position.
    //
    // The transform is where the hero's FEET go; SpawnPosition is where the hero's root goes (feet plus the root
    // height of the hero collider). The ground search is repeated on demand until it succeeds once, because a marker
    // in a room that is switched on may wake before the floor's collider is registered.
    public class HazardRespawnMarker : MonoBehaviour
    {
        const float SearchDistance = 50f;
        const float SearchStartUp = 0.05f;

        [SerializeField] bool facingRight = true;

        bool snapped;
        Vector2 feet;

        public bool FacingRight => facingRight;
        public bool HasGround { get; private set; }

        public void Configure(bool faceRight) => facingRight = faceRight;

        // Where the hero's root is put: the feet spot raised by the hero's root height, plus a hair so it settles.
        public Vector2 SpawnPosition
        {
            get
            {
                if (!snapped) Snap();
                float above = SilksongUnits.HeroColliderSize.y * 0.5f - SilksongUnits.HeroColliderOffset.y + 0.01f;
                return new Vector2(feet.x, feet.y + above);
            }
        }

        void Awake() => Snap();

        // Raycasts down and takes the first solid (non-trigger) collider that is not the hero.
        public void Snap()
        {
            feet = transform.position;
            Physics2D.SyncTransforms();
            Vector2 origin = (Vector2)transform.position + Vector2.up * SearchStartUp;
            var hits = Physics2D.RaycastAll(origin, Vector2.down, SearchDistance);
            float best = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.collider == null || h.collider.isTrigger) continue;
                if (h.collider.GetComponentInParent<HeroController>() != null) continue;
                if (h.distance < best) { best = h.distance; feet = h.point; found = true; }
            }
            HasGround = found;
            snapped = found;
        }
    }
}
