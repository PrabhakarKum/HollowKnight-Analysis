using System;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // Hit points of an enemy, taken from the hero's needle through IHittable. Rules (study 14, section 2):
    // hp -= damage per landed hit; no invulnerability window and no stun; a dead enemy refuses hits.
    // Events fire in this order for one hit: Hit, Shoved (sideways swings only), Died (killing blow only).
    // Death: contact damage goes to zero first, then renderers and colliders are switched off, then Died is
    // raised once, then the object is disabled (or destroyed) after RemoveDelaySeconds (default 0).
    // Not built: hit and death sound/effects, corpse, drops (silk gain is HeroAttack's job).
    public class EnemyHealth : MonoBehaviour, IHittable, IHitFeedbackTarget
    {
        // Hero feedback on a landed hit (study 14e): an ordinary enemy gives silk and bounces a downward hit.
        public bool GivesSilk => true;
        public bool Bounces => true;

        [SerializeField] SilksongEnemyConfig config;
        [SerializeField] float removeDelaySeconds;
        [SerializeField] bool destroyOnDeath;

        int health;
        bool initialised;

        public SilksongEnemyConfig Config => config;
        public int MaxHealth => config != null ? config.maxHealth : SilksongEnemyConfigDefaults.MaxHealth;
        public int Health { get { EnsureInit(); return health; } }
        public bool IsDead { get; private set; }
        public float ShoveSpeed => config != null ? config.shoveSpeed : SilksongEnemyConfigDefaults.ShoveSpeed;
        public float ShoveSeconds => config != null ? config.shoveSeconds : SilksongEnemyConfigDefaults.ShoveSeconds;

        // Seconds between death and the object being disabled/destroyed. 0 = at once.
        public float RemoveDelaySeconds { get => removeDelaySeconds; set => removeDelaySeconds = Mathf.Max(0f, value); }
        public bool DestroyOnDeath { get => destroyOnDeath; set => destroyOnDeath = value; }

        public event Action<HitInstance> Hit;
        // Velocity along x (away from the hit source) and how long it lasts. The walker applies it.
        public event Action<float, float> Shoved;
        public event Action Died;

        void Awake() => EnsureInit();

        void EnsureInit()
        {
            if (initialised) return;
            initialised = true;
            health = MaxHealth;
        }

        // Code-built enemies (tests, the room builder) hand over the config; health is refilled.
        public void Configure(SilksongEnemyConfig value)
        {
            config = value;
            initialised = true;
            health = MaxHealth;
            IsDead = false;
            var contact = GetComponent<EnemyContactDamage>();
            if (contact != null) contact.Configure(value);
        }

        public bool TakeHit(HitInstance hit)
        {
            EnsureInit();
            if (!EnemyRules.CanTakeHit(IsDead, hit.Damage)) return false;

            health = EnemyRules.ApplyDamage(health, hit.Damage);
            Stubs.NotImplemented("Enemy hit sound and blood effects");
            Hit?.Invoke(hit);

            if (EnemyRules.ShovesSideways(hit.Direction))
            {
                bool hasSource = hit.Source != null;
                float dir = EnemyRules.ShoveDirection(transform.position.x, hasSource ? hit.Source.transform.position.x : 0f, hasSource);
                Shoved?.Invoke(EnemyRules.ShoveVelocityX(dir, ShoveSpeed), ShoveSeconds);
            }

            if (EnemyRules.IsLethal(health)) Die();
            return true;
        }

        void Die()
        {
            if (IsDead) return;
            IsDead = true;
            health = 0;

            // A dying enemy can no longer hurt the hero.
            var contact = GetComponent<EnemyContactDamage>();
            if (contact != null) contact.ZeroDamage();

            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
            Stubs.NotImplemented("Enemy corpse, death sound and drops");

            Died?.Invoke();

            if (removeDelaySeconds <= 0f) Remove();
            else Invoke(nameof(Remove), removeDelaySeconds);
        }

        void Remove()
        {
            if (destroyOnDeath) Destroy(gameObject);
            else gameObject.SetActive(false);
        }
    }
}
