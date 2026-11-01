using System.Collections.Generic;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // A damage zone (spikes) for the test room. Needs a trigger collider. While the hero overlaps it, the zone hits
    // the hero through the normal HeroHealth.TakeHit path, so invulnerability, stun and knockback all come from
    // HeroHealth and HeroConfig: a hit that lands starts the invulnerability window, and further hits are refused
    // until it ends (so a hero standing on spikes is hurt once per window, not every step).
    //
    // Damage is the fixed default 1: HeroConfig has no field for hazard damage (the original sets it by hazard type,
    // spikes 1; see docs/study/06-damage-and-respawn.md, row 20). The hit's source is this object, so the knockback
    // pushes the hero away from the zone sideways.
    //
    // Not built (see the study page): the original's hazard respawn (hero put back on safe ground) is not here; the
    // hero is hurt and knocked back like any other hit.
    [RequireComponent(typeof(Collider2D))]
    public class HazardZone : MonoBehaviour
    {
        // Why a constant: no HeroConfig field exists for it and the config is frozen. Spikes deal 1 in the original.
        public const int Damage = 1;

        readonly List<HeroHealth> inside = new List<HeroHealth>();

        void Awake()
        {
            Stubs.NotImplemented("Hero hazard respawn");
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var health = other.GetComponentInParent<HeroHealth>();
            if (health == null) return;
            if (!inside.Contains(health)) inside.Add(health);
            Hit(health);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            var health = other.GetComponentInParent<HeroHealth>();
            if (health == null) return;
            if (!inside.Contains(health)) inside.Add(health);
            Hit(health);
        }

        void OnTriggerExit2D(Collider2D other)
        {
            var health = other.GetComponentInParent<HeroHealth>();
            if (health != null) inside.Remove(health);
        }

        void OnDisable()
        {
            inside.Clear();
        }

        // Stay events are not sent for a body that has gone to sleep (a hero standing still on spikes), so the zone
        // also re-checks the heroes it knows are inside every physics step. Tests with manual stepping call this
        // themselves after each Physics2D.Simulate.
        void FixedUpdate() => Tick();

        public void Tick()
        {
            for (int i = inside.Count - 1; i >= 0; i--)
            {
                if (inside[i] == null) { inside.RemoveAt(i); continue; }
                Hit(inside[i]);
            }
        }

        void Hit(HeroHealth health)
        {
            // TakeHit refuses while invulnerable, stunned or dead.
            health.TakeHit(new HitInstance { Damage = Damage, Direction = AttackDirection.Normal, Source = gameObject });
        }
    }
}
