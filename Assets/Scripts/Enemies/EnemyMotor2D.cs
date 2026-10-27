using UnityEngine;
using Hollowknight.Player;

namespace Hollowknight.Enemies
{
    /// <summary>
    /// Moves an enemy: ground check, run toward a target speed, jump, knock-back, and the sensors the brain needs
    /// (wall ahead, obstacle height, ledge ahead). Uses plain Rigidbody2D physics (the enemy has its own gravity scale);
    /// the hero and other enemies do not collide with it (it is hit through triggers, not blocked).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class EnemyMotor2D : MonoBehaviour
    {
        public bool IsGrounded { get; private set; }
        public int Facing { get; private set; } = 1;
        public Rigidbody2D Body => rb;
        public Collider2D BodyCollider => col;
        public Vector2 Velocity => rb.linearVelocity;
        public float FeetY => col.bounds.min.y;
        public bool IsKnocked => Time.time < knockUntil;
        public event System.Action Landed;

        [SerializeField] EnemyConfig config;
        EnemyConfig cfg;
        Rigidbody2D rb;
        Collider2D col;
        ContactFilter2D filter;
        readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        float targetVx;
        float knockUntil;
        float airTargetVx;
        bool inJump;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var mat = new PhysicsMaterial2D("EnemyNoFriction") { friction = 0f, bounciness = 0f };
            col.sharedMaterial = mat;
            filter = new ContactFilter2D { useTriggers = false, useLayerMask = false };
            if (config != null) Setup(config);
        }

        /// <summary>Stores the config (so it is saved with the scene) and applies it.</summary>
        public void Configure(EnemyConfig c) { config = c; Setup(c); }

        void Start()
        {
            // the hero and the other enemies walk through this body; it still stands on the ground and hits walls
            var hero = FindFirstObjectByType<PlayerController2D>();
            if (hero != null) foreach (var c in hero.GetComponentsInChildren<Collider2D>()) Physics2D.IgnoreCollision(col, c, true);
            foreach (var other in FindObjectsByType<EnemyMotor2D>(FindObjectsSortMode.None))
                if (other != this) Physics2D.IgnoreCollision(col, other.col, true);
        }

        public void Setup(EnemyConfig config)
        {
            cfg = config;
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = cfg.gravityScale;
        }

        public void MoveX(float vx) { targetVx = vx; }
        public void Stop() { targetVx = 0f; }
        public void Face(int dir)
        {
            if (dir != 0) Facing = dir > 0 ? 1 : -1;
        }

        /// <summary>Jumps with a given upward speed, keeping a forward speed in the air.</summary>
        public void Jump(float vy, float vx)
        {
            rb.linearVelocity = new Vector2(vx, vy);
            airTargetVx = vx;
            inJump = true;
            if (vx != 0f) Face(vx > 0f ? 1 : -1);
        }

        /// <summary>Upward speed that just clears a height under this enemy's own gravity.</summary>
        public float JumpSpeedFor(float height)
        {
            float g = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale;
            return Mathf.Sqrt(2f * g * Mathf.Max(0.1f, height));
        }

        public void Knock(Vector2 velocity, float seconds)
        {
            rb.linearVelocity = velocity;
            knockUntil = Time.time + seconds;
            inJump = false;
        }

        void FixedUpdate()
        {
            bool was = IsGrounded;
            IsGrounded = false;
            int n = col.Cast(Vector2.down, filter, hits, 0.06f);
            for (int i = 0; i < n; i++)
            {
                if (IsSolid(hits[i].collider) && hits[i].normal.y > 0.5f) { IsGrounded = true; break; }
            }
            if (IsGrounded && !was && rb.linearVelocity.y <= 0.05f) { inJump = false; Landed?.Invoke(); }

            if (IsKnocked) return;
            var v = rb.linearVelocity;
            if (inJump && !IsGrounded)
            {
                v.x = Mathf.MoveTowards(v.x, airTargetVx, cfg.acceleration * 0.3f * Time.fixedDeltaTime);   // committed to the jump
            }
            else
            {
                v.x = Mathf.MoveTowards(v.x, targetVx, cfg.acceleration * (IsGrounded ? 1f : 0.4f) * Time.fixedDeltaTime);
            }
            rb.linearVelocity = v;
        }

        // ------------------------------------------------------------------ sensing

        /// <summary>Solid world geometry: not a trigger, and not the hero or any enemy.</summary>
        public bool IsSolid(Collider2D c)
        {
            if (c == null || c == col || c.isTrigger) return false;
            if (c.GetComponentInParent<PlayerController2D>() != null) return false;
            if (c.GetComponentInParent<EnemyMotor2D>() != null) return false;
            return true;
        }

        public bool RayWorld(Vector2 origin, Vector2 dir, float length, out RaycastHit2D hit)
        {
            hit = default;
            int n = Physics2D.Raycast(origin, dir.normalized, filter, hits, length);
            float best = float.MaxValue; bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (!IsSolid(hits[i].collider)) continue;
                if (hits[i].distance < best) { best = hits[i].distance; hit = hits[i]; found = true; }
            }
            return found;
        }

        public bool LineBlocked(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            return RayWorld(from, d, d.magnitude, out _);
        }

        /// <summary>A wall right in front of the body (at any height of the body).</summary>
        public bool WallAhead(int dir)
        {
            int n = col.Cast(new Vector2(dir, 0f), filter, hits, 0.12f);
            for (int i = 0; i < n; i++)
                if (IsSolid(hits[i].collider) && Mathf.Abs(hits[i].normal.x) > 0.7f) return true;
            return false;
        }

        /// <summary>Height of the obstacle ahead above the feet, or 0 if there is none within maxHeight (or it is taller).</summary>
        public float ObstacleHeight(int dir, float maxHeight)
        {
            var b = col.bounds;
            float x = b.center.x + dir * (b.extents.x + 0.2f);
            Vector2 origin = new Vector2(x, b.min.y + maxHeight + 0.3f);
            if (!RayWorld(origin, Vector2.down, maxHeight + 0.3f, out var top)) return 0f;
            float h = top.point.y - b.min.y;
            if (h > maxHeight || h < 0.1f) return 0f;
            // room to stand on top of it?
            if (RayWorld(top.point + new Vector2(0f, 0.05f), Vector2.up, b.size.y, out _)) return 0f;
            return h;
        }

        /// <summary>No ground a short way down in front of the feet.</summary>
        public bool LedgeAhead(int dir)
        {
            var b = col.bounds;
            Vector2 origin = new Vector2(b.center.x + dir * (b.extents.x + 0.25f), b.min.y + 0.1f);
            return !RayWorld(origin, Vector2.down, 1.0f, out _);
        }
    }
}
