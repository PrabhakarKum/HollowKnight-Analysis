using System;
using System.Collections;
using UnityEngine;
using Hollowknight.Feel;
using Hollowknight.Player;

namespace Hollowknight.Enemies
{
    /// <summary>
    /// Enemy hit points. A nail hit (reported by the HitTarget the slash finds) takes PlayerTuning.nailDamage; the enemy flashes, is knocked
    /// back and staggered, and at zero it dies once: death freeze + shake, a pixel burst, its attacks stop, and the body fades away.
    /// Follows the structure of the original HealthManager (Hit -> TakeDamage -> NonFatalHit / Die; a dead enemy ignores further hits).
    /// </summary>
    public class EnemyHealth2D : MonoBehaviour
    {
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public bool IsDead { get; private set; }
        public int DamageTaken { get; private set; }

        /// <summary>damage dealt, world position of the hero that hit</summary>
        public event Action<int, Vector2> Damaged;
        public event Action Died;

        [SerializeField] EnemyConfig config;
        EnemyConfig cfg;
        HitTarget target;
        EnemyBrain2D brain;
        EnemyMotor2D motor;
        PlayerController2D hero;
        SpriteRenderer[] renderers;

        void Awake()
        {
            target = GetComponent<HitTarget>();
            renderers = GetComponentsInChildren<SpriteRenderer>();
            if (config != null) Setup(config);
        }

        // looked up when needed: when an enemy is built in play mode the health component is added before the brain, so Awake would miss it
        EnemyBrain2D Brain { get { if (brain == null) brain = GetComponent<EnemyBrain2D>(); return brain; } }

        public void Configure(EnemyConfig c) { config = c; Setup(c); }

        void OnEnable() { if (target != null) target.Hit += OnHit; }
        void OnDisable() { if (target != null) target.Hit -= OnHit; }

        public void Setup(EnemyConfig config)
        {
            cfg = config;
            MaxHealth = Health = cfg.maxHealth;
        }

        void OnHit(AttackDir dir)
        {
            if (IsDead) return;
            if (hero == null) hero = FindFirstObjectByType<PlayerController2D>();
            int damage = hero != null ? hero.Tuning.nailDamage : 1;
            Vector2 from = hero != null ? (Vector2)hero.transform.position : (Vector2)transform.position + Vector2.left;
            ApplyDamage(damage, from, dir);
        }

        public void ApplyDamage(int damage, Vector2 fromPosition, AttackDir dir = AttackDir.Forward)
        {
            if (IsDead || damage <= 0) return;
            Health = Mathf.Max(0, Health - damage);
            DamageTaken += damage;
            int away = transform.position.x >= fromPosition.x ? 1 : -1;
            Damaged?.Invoke(damage, fromPosition);
            EnemyFx.HitSpark(transform.position, away, cfg);
            if (Health <= 0) Die(away);
            else if (Brain != null) Brain.OnHurt(away, dir);
        }

        void Die(int away)
        {
            IsDead = true;
            Died?.Invoke();
            FreezeMoment.Start(FreezeType.EnemyDeath);
            CameraShake2D.Do(ShakePreset.EnemyKill, true);
            EnemyFx.DeathBurst(transform.position, cfg);
            // a corpse can no longer be slashed (no more recoil, pogo or silk from it)
            if (target != null) { target.Hit -= OnHit; Destroy(target); }
            if (Brain != null) Brain.OnDeath(away);
            StartCoroutine(FadeAndRemove());
        }

        IEnumerator FadeAndRemove()
        {
            yield return new WaitForSeconds(1.0f);                         // the death animation plays out
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                // fade in coarse steps (blink out) to stay pixel-like
                float a = (Mathf.FloorToInt(t * 16f) % 2 == 0) ? 1f : 0.3f;
                foreach (var r in renderers) if (r != null) { var c = r.color; c.a = a; r.color = c; }
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
