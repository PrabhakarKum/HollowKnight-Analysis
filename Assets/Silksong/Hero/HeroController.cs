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
        // The injected input. While the control is locked (room transitions) every hero system sees the driver's
        // scripted input instead, so none of them needs its own lock check and the dash/wall/attack/health order is
        // untouched; unlocking simply hands the real input back.
        IHeroInput rawInput;
        public IHeroInput Input
        {
            get => ControlLocked && transition != null ? transition.ScriptedInput : rawInput;
            set => rawInput = value;
        }

        // True while the hero ignores the player (set by HeroTransitionDriver).
        public bool ControlLocked { get; internal set; }
        public HeroTransitionDriver Transition => transition;
        internal bool HasInput => rawInput != null;
        public HeroConfig Config { get; set; }
        // Optional: the hero plays nothing when no animator is assigned.
        public ISpriteAnimator Animator { get; set; }

        // Layers the hero treats as floor. Default is everything; the room task narrows it.
        public LayerMask GroundLayers = ~0;

        // Hurt and Dead are owned by HeroHealth (HeroMovement never overwrites them, so a stray Hurt/Dead set by anything
        // else would stay stuck). Only HeroHealth, and tests that simulate it, may set them.
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
        public HeroHealth Health => health;

        Rigidbody2D body;
        Collider2D bodyCollider;
        HeroMovement movement;
        HeroDash dash;
        HeroWall wall;
        HeroAttack attack;
        HeroHealth health;
        HeroTransitionDriver transition;
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
            health = GetComponent<HeroHealth>();
            if (health == null) health = gameObject.AddComponent<HeroHealth>();
            health.Bind(this);
            transition = GetComponent<HeroTransitionDriver>();
            if (transition == null) transition = gameObject.AddComponent<HeroTransitionDriver>();
            transition.Bind(this);
        }

        // Hurt and death cancel a jump in progress and its buffers (the original resets all motion).
        internal void ResetMovement(bool forgetGround) => movement.ResetForDamage(forgetGround);

        void FixedUpdate()
        {
            if (!ManualStepping) StepFixed(Time.fixedDeltaTime);
        }

        // Exactly what one FixedUpdate does. Public so tests can run a known number of steps.
        public void StepFixed(float dt)
        {
            if (Config == null) return;
            // The transition driver ticks first: it times the walk-in and unlocks the controls in the step the walk
            // ends. When it owns the velocity outright (leaving upward, waiting to drop in, the bottom-gate throw)
            // movement and the wall must not run, like the dash; dash and attack see neutral input meanwhile.
            bool scripted = transition.FixedStep(dt);
            // Ticked before the input check so a lock can never outlive the input; the driver also refuses to lock
            // a hero that has no input (see HeroTransitionDriver.CanLock).
            if (rawInput == null) return;
            // Health first: it ticks invulnerability, and while the hero is stunned by a hit (or dead) it owns the
            // velocity outright, like the dash does, so movement and the wall must not run (see HeroHealth).
            bool owned = health.FixedStep(dt);
            // While dashing, the dash owns velocity and movement must not run (it would zero or overwrite it).
            bool dashed = dash.FixedStep(dt);
            // The swing starts before movement and wall so a swing on a wall changes the cling in this same
            // step; its pogo is applied after them because movement rewrites vertical velocity (see HeroAttack).
            attack.BeginStep(dt, dashed);
            bool movementRan = false;
            if (owned) wall.BeginStep(dt);   // sees Hurt/Dead and lets go of everything; never starts a wall jump
            else if (dashed) wall.OnDashStep();
            else if (scripted)
            {
                // The driver set the velocity itself (leaving upward, hovering, the bottom-gate throw): movement and
                // the wall must not overwrite it.
            }
            else if (ControlLocked)
            {
                // A scripted walk or fall (room transition): movement only. The wall stays out so a scripted walk
                // into a wall cannot start a cling, and the player's wall jump cannot fire.
                movement.FixedStep(dt);
                movementRan = true;
            }
            else if (!wall.BeginStep(dt))
            {
                // A wall jump handles its own step; otherwise movement runs and the wall adjusts its result.
                movement.FixedStep(dt);
                movementRan = true;
                wall.EndStep(dt);
            }
            // Facing changed by a dash, wall jump, hit or scripted move must not look like a ground turn later.
            if (!movementRan) movement.SyncFacing();
            attack.EndStep(dt);
        }

        void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }
    }
}
