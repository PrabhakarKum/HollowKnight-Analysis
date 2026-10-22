using UnityEngine;

namespace Hollowknight.Player
{
    /// <summary>
    /// Hurts the hero while the hero overlaps this object's trigger collider: an enemy body, a projectile, spikes.
    /// The hero's own invulnerability decides whether a given overlap actually lands.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DamageSource2D : MonoBehaviour
    {
        [Tooltip("Health lost per hit.")]
        public int damage = 1;
        [Tooltip("Enemy: knocks the hero back. Hazard: moves the hero to the last safe ground (spikes).")]
        public DamageKind kind = DamageKind.Enemy;
        [Tooltip("Turn off to make this harmless (for example a dead enemy).")]
        public bool active = true;

        Collider2D col;
        ContactFilter2D filter;
        readonly Collider2D[] overlaps = new Collider2D[8];

        void Awake()
        {
            col = GetComponent<Collider2D>();
            filter = new ContactFilter2D { useTriggers = false, useLayerMask = false };
        }

        void Reset()
        {
            var c = GetComponent<Collider2D>();
            if (c != null) c.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other) => TryHurt(other);

        // Polled instead of relying on OnTriggerStay2D: a resting Rigidbody2D falls asleep and stops getting Stay
        // callbacks, so a hero standing still inside an enemy would never be hurt again once invulnerability ran out.
        void FixedUpdate()
        {
            if (!active) return;
            int n = col.Overlap(filter, overlaps);
            for (int i = 0; i < n; i++) TryHurt(overlaps[i]);
        }

        void TryHurt(Collider2D other)
        {
            if (!active) return;
            var health = other.GetComponentInParent<PlayerHealth2D>();
            if (health == null) return;
            health.TakeDamage(new DamageInfo(damage, col.bounds.center.x, kind, gameObject));
        }
    }
}
