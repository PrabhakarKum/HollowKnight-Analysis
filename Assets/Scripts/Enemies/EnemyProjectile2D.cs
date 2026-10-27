using UnityEngine;
using Hollowknight.Player;

namespace Hollowknight.Enemies
{
    /// <summary>
    /// A bullet, an arrow or a spinning blade fired by an enemy. It hurts the hero through a DamageSource2D trigger, is stopped by the world,
    /// and plays an impact effect. Blades fly out, then come back to the thrower (a boomerang) and keep hurting on the way.
    /// </summary>
    public class EnemyProjectile2D : MonoBehaviour
    {
        public enum Mode { Straight, Ballistic, Boomerang }

        public static int SpawnedCount { get; private set; }
        public static int AliveCount { get; private set; }

        public Mode Kind { get; private set; }
        public Vector2 Velocity => velocity;
        public bool Returning => returning;
        public int HitsOnHero => damageSource != null ? damageSource.HurtCount : 0;
        public bool HitWorld { get; private set; }

        Vector2 velocity;
        float gravity;
        float life;
        float radius;
        Transform owner;
        float outDistance, speed;
        Vector2 startPos;
        bool returning;
        bool rotateToVelocity;
        float spin;
        Sprite[] frames;
        float fps;
        float animT;
        Sprite[] impact;
        float effectFps;
        SpriteRenderer sr;
        DamageSource2D damageSource;
        ContactFilter2D filter;
        readonly Collider2D[] results = new Collider2D[8];

        /// <summary>Builds and launches a projectile. 'owner' is the thrower (blades return to it).</summary>
        public static EnemyProjectile2D Spawn(Mode mode, Vector2 position, Vector2 velocity, EnemyConfig cfg, EnemyArtSet art, Transform owner, bool facingRight, float gravity = 0f)
        {
            var go = new GameObject("EnemyProjectile (" + mode + ")");
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 7;
            sr.flipX = false;
            var circle = go.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = mode == Mode.Boomerang ? 0.4f : 0.18f;
            var dmg = go.AddComponent<DamageSource2D>();
            dmg.damage = cfg.damage;
            dmg.kind = DamageKind.Enemy;
            var p = go.AddComponent<EnemyProjectile2D>();
            p.Kind = mode;
            p.velocity = velocity;
            p.speed = velocity.magnitude;
            p.gravity = gravity;
            p.life = 4f;
            p.radius = circle.radius;
            p.owner = owner;
            p.startPos = position;
            p.outDistance = cfg.boomerangDistance;
            p.rotateToVelocity = mode == Mode.Ballistic || mode == Mode.Straight;
            p.spin = mode == Mode.Boomerang ? 0f : 0f;
            p.frames = art != null ? art.projectile : null;
            p.fps = art != null ? art.projectileFps : 16f;
            p.impact = art != null ? art.impact : null;
            p.effectFps = art != null ? art.effectFps : 30f;
            p.sr = sr;
            p.damageSource = dmg;
            if (p.frames != null && p.frames.Length > 0) sr.sprite = p.frames[0];
            if (mode != Mode.Boomerang && velocity.sqrMagnitude > 0.01f)
                go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            dmg.Hurt += p.OnHurtHero;
            SpawnedCount++;
            AliveCount++;
            return p;
        }

        void Awake()
        {
            filter = new ContactFilter2D { useTriggers = false, useLayerMask = false };
        }

        void OnDestroy() { AliveCount = Mathf.Max(0, AliveCount - 1); }

        void Update()
        {
            if (frames != null && frames.Length > 1)
            {
                animT += Time.deltaTime;
                sr.sprite = frames[Mathf.FloorToInt(animT * fps) % frames.Length];
            }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            life -= dt;
            if (life <= 0f) { Destroy(gameObject); return; }

            if (Kind == Mode.Boomerang) StepBoomerang(dt);
            else
            {
                velocity.y -= gravity * dt;
                transform.position += (Vector3)(velocity * dt);
                if (rotateToVelocity && velocity.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            }

            // stopped by the world (not by the hero or other enemies)
            int n = Physics2D.OverlapCircle((Vector2)transform.position, radius, filter, results);
            for (int i = 0; i < n; i++)
            {
                var c = results[i];
                if (c == null || c.isTrigger) continue;
                if (c.GetComponentInParent<PlayerController2D>() != null || c.GetComponentInParent<EnemyMotor2D>() != null) continue;
                HitWorld = true;
                if (Kind == Mode.Boomerang) { if (!returning) returning = true; continue; }   // a blade that hits a wall turns back
                Impact();
                return;
            }
        }

        void StepBoomerang(float dt)
        {
            Vector2 pos = transform.position;
            if (!returning)
            {
                transform.position = pos + velocity * dt;
                if (Vector2.Distance(startPos, transform.position) >= outDistance) returning = true;
            }
            else
            {
                if (owner == null) { Destroy(gameObject); return; }
                Vector2 to = (Vector2)owner.position + new Vector2(0f, 0.2f) - pos;
                if (to.magnitude < 0.5f) { Destroy(gameObject); return; }
                velocity = to.normalized * speed;
                transform.position = pos + velocity * dt;
            }
        }

        void OnHurtHero(PlayerHealth2D hero)
        {
            if (Kind != Mode.Boomerang) Impact();       // bullets and arrows are used up; a blade keeps flying
        }

        void Impact()
        {
            SpriteFlipbook2D.PlayOnce(transform.position, impact, effectFps, velocity.x < 0f, 1f);
            Destroy(gameObject);
        }
    }
}
