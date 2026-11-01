using System;
using UnityEngine;

namespace Hollowknight.Silksong
{
    public enum HeroState
    {
        Grounded,
        Rising,
        Falling,
        Dashing,
        WallClinging,
        Attacking,
        Hurt,
        Dead
    }

    // Owns the hero's body, config and injected input, and exposes read-only state. Movement rules
    // live in HeroMovement; later tasks (dash, wall, attack, health) add their own partners here.
    // Input and Config are injected so tests need neither a keyboard nor a saved asset.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class HeroController : MonoBehaviour
    {
        public IHeroInput Input { get; set; }
        public HeroConfig Config { get; set; }
        // Optional: the hero plays nothing when no animator is assigned.
        public ISpriteAnimator Animator { get; set; }

        // Layers the hero treats as floor. Default is everything; the room task narrows it.
        public LayerMask GroundLayers = ~0;

        public HeroState State { get; internal set; } = HeroState.Falling;
        public bool IsGrounded { get; internal set; }
        public bool FacingRight { get; internal set; } = true;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;

        // Input turned into the original's four directional "buttons" (thresholds from HeroConfig).
        public bool WantsLeft { get; internal set; }
        public bool WantsRight { get; internal set; }
        public bool WantsUp { get; internal set; }
        public bool WantsDown { get; internal set; }

        // Tests set this and drive StepFixed themselves so they control exactly how many steps run.
        public bool ManualStepping { get; set; }

        public event Action Landed;
        public HeroDash Dash => dash;
        public HeroWall Wall => wall;
        public HeroAttack Attack => attack;

        Rigidbody2D body;
        Collider2D bodyCollider;
        HeroMovement movement;
        HeroDash dash;
        HeroWall wall;
        HeroAttack attack;
        PhysicsMaterial2D runtimeMaterial;

        internal Rigidbody2D Body => body;
        internal Collider2D BodyCollider => bodyCollider;

        internal void RaiseLanded() => Landed?.Invoke();

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();

            body.bodyType = RigidbodyType2D.Dynamic;
            // Gravity is applied by HeroMovement so the config's number is the whole truth and does
            // not depend on the project's Physics2D settings.
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.None;

            // A frictionless hero neither sticks to walls nor gets slowed by the floor while the
            // movement code sets velocity directly.
            if (bodyCollider.sharedMaterial == null)
            {
                runtimeMaterial = new PhysicsMaterial2D("HeroNoFriction") { friction = 0f, bounciness = 0f };
                bodyCollider.sharedMaterial = runtimeMaterial;
            }

            movement = new HeroMovement(this);
            // Added here so a hero built from code or an older scene gets a dash without extra setup.
            dash = GetComponent<HeroDash>();
            if (dash == null) dash = gameObject.AddComponent<HeroDash>();
            dash.Bind(this);
            wall = GetComponent<HeroWall>();
            if (wall == null) wall = gameObject.AddComponent<HeroWall>();
            wall.Bind(this);
            attack = GetComponent<HeroAttack>();
            if (attack == null) attack = gameObject.AddComponent<HeroAttack>();
            attack.Bind(this);
        }

        void FixedUpdate()
        {
            if (!ManualStepping) StepFixed(Time.fixedDeltaTime);
        }

        // Exactly what one FixedUpdate does. Public so tests can run a known number of steps.
        public void StepFixed(float dt)
        {
            if (Input == null || Config == null) return;
            // While dashing, the dash owns velocity and movement must not run (it would zero or overwrite it).
            bool dashed = dash.FixedStep(dt);
            // The swing starts before movement and wall so a swing on a wall changes the cling in this same
            // step; its pogo is applied after them because movement rewrites vertical velocity (see HeroAttack).
            attack.BeginStep(dt, dashed);
            if (dashed) wall.OnDashStep();
            else if (!wall.BeginStep(dt))
            {
                // A wall jump handles its own step; otherwise movement runs and the wall adjusts its result.
                movement.FixedStep(dt);
                wall.EndStep(dt);
            }
            attack.EndStep(dt);
        }

        void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }
    }
}
