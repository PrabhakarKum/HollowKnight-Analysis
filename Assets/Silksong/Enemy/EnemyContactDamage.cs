using System.Collections.Generic;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // Hurts the hero on touch, through the same HeroHealth.TakeHit path HazardZone uses, so invulnerability,
    // stun and knockback (away from this object) all come from the hero. Detection copies HazardZone: the
    // Collider2D on this object reports the hero through trigger (or, for a solid box, collision) events, and
    // Tick re-checks the known heroes every physics step because stay events stop for a sleeping body.
    // Damage is zeroed on death (EnemyHealth calls ZeroDamage).
    // Not built: the hero's 2-step downspike grace (study section 3, HeroHealth territory).
    [RequireComponent(typeof(Collider2D))]
    public class EnemyContactDamage : MonoBehaviour
    {
        readonly List<HeroHealth> inside = new List<HeroHealth>();
        Collider2D ownCollider;
        EnemyHealth health;
        int damage = SilksongEnemyConfigDefaults.ContactDamage;
        bool zeroed;

        public int InsideCount => inside.Count;
        public EnemyDamageType DamageType { get; private set; } = SilksongEnemyConfigDefaults.DamageType;

        // Damage a touch would deal right now.
        public int DamageDealt
        {
            get
            {
                if (health == null) health = GetComponent<EnemyHealth>();
                bool dead = zeroed || (health != null && health.IsDead);
                return EnemyRules.ContactDamage(damage, dead);
            }
        }

        public void Configure(SilksongEnemyConfig config)
        {
            if (config == null) return;
            damage = config.contactDamage;
            DamageType = config.damageType;
            zeroed = false;
        }

        public void ZeroDamage()
        {
            zeroed = true;
            inside.Clear();
        }

        void Awake()
        {
            ownCollider = GetComponent<Collider2D>();
            health = GetComponent<EnemyHealth>();
            if (health != null && health.Config != null) Configure(health.Config);
        }

        void OnTriggerEnter2D(Collider2D other) => Touch(other);
        void OnTriggerStay2D(Collider2D other) => Touch(other);
        void OnCollisionEnter2D(Collision2D c) => Touch(c.collider);
        void OnCollisionStay2D(Collision2D c) => Touch(c.collider);

        void Touch(Collider2D other)
        {
            var hero = other.GetComponentInParent<HeroHealth>();
            // Only the hero's body counts. The needle's hitbox is a trigger on a child object; touching an enemy
            // with the swing must never hurt the hero.
            if (hero == null || other.gameObject != hero.gameObject) return;
            if (!inside.Contains(hero)) inside.Add(hero);
            Hit(hero);
        }

        void OnTriggerExit2D(Collider2D other) => Leave(other);
        void OnCollisionExit2D(Collision2D c) => Leave(c.collider);

        void Leave(Collider2D other)
        {
            var hero = other.GetComponentInParent<HeroHealth>();
            if (hero != null) inside.Remove(hero);
        }

        void OnDisable() => inside.Clear();

        void FixedUpdate() => Tick();

        public void Tick()
        {
            if (ownCollider == null) ownCollider = GetComponent<Collider2D>();
            for (int i = inside.Count - 1; i >= 0; i--)
            {
                var hero = inside[i];
                var col = hero != null ? hero.GetComponent<Collider2D>() : null;
                if (hero == null || col == null || hero.IsDead || !ownCollider.enabled || !ownCollider.Distance(col).isOverlapped)
                {
                    inside.RemoveAt(i);
                    continue;
                }
                Hit(hero);
            }
        }

        void Hit(HeroHealth hero)
        {
            int dmg = DamageDealt;
            if (dmg <= 0 || hero.IsDead) return;
            hero.TakeHit(new HitInstance { Damage = dmg, Direction = AttackDirection.Normal, Source = gameObject });
        }
    }
}
