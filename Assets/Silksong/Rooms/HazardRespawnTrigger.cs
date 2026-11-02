using UnityEngine;

namespace Hollowknight.Silksong
{
    // A trigger box. When the hero touches it, the linked HazardRespawnMarker becomes the hero's hazard respawn spot
    // for THIS room (position, facing and the room's scene name go into PlayerData.hazardRespawn).
    [RequireComponent(typeof(Collider2D))]
    public class HazardRespawnTrigger : MonoBehaviour
    {
        [SerializeField] HazardRespawnMarker marker;

        public HazardRespawnMarker Marker => marker;

        public void Configure(HazardRespawnMarker target) => marker = target;

        void Reset()
        {
            var c = GetComponent<Collider2D>();
            if (c != null) c.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other) => Touch(other);

        void Touch(Collider2D other)
        {
            var health = other.GetComponentInParent<HeroHealth>();
            if (health != null) Apply(health.Data);
        }

        // The storing step, separate from the physics callback so it can be tested directly.
        public bool Apply(PlayerData data)
        {
            if (marker == null || data == null) return false;
            data.SetHazardRespawn(marker.gameObject.scene.name, marker.name, marker.SpawnPosition, marker.FacingRight);
            return true;
        }
    }
}
