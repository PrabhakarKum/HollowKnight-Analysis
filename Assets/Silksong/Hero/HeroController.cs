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

        Rigidbody2D body;
        Collider2D bodyCollider;
        HeroMovement movement;
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
        }

        void FixedUpdate()
        {
            if (!ManualStepping) StepFixed(Time.fixedDeltaTime);
        }

        // Exactly what one FixedUpdate does. Public so tests can run a known number of steps.
        public void StepFixed(float dt)
        {
            if (Input == null || Config == null) return;
            movement.FixedStep(dt);

            // Dash and attack arrive in later tasks; flag the press so the gap is visible.
            if (Input.Dash.WasPressed) Stubs.NotImplemented("Hero dash");
            if (Input.Attack.WasPressed) Stubs.NotImplemented("Hero attack");
        }

        void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }
    }
}
