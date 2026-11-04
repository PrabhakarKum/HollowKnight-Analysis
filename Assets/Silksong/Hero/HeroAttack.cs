using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // The needle attack and the down-slash pogo. Attacking is an overlay on whatever the hero is doing (the
    // original keeps it as a flag, not a state), so HeroState is never set to Attacking and movement keeps
    // running during a swing.
    //
    // Hook order inside HeroController.StepFixed, and why:
    //  1. dash.FixedStep            - the dash decides first whether the hero is dashing; a dashing hero cannot
    //                                  start a swing.
    //  2. attack.BeginStep          - BEFORE movement and wall: ends the swing on hurt/dead, ticks the timers,
    //                                  starts a swing. Starting must precede the wall because a swing on the
    //                                  wall changes the cling (slow the slide, step off the wall, let go) and
    //                                  the wall must see that result in this same step.
    //  3. wall / movement           - unchanged.
    //  4. attack.EndStep            - AFTER movement and wall: hit detection, then the pogo. Movement writes
    //                                  the vertical velocity every step (gravity), so the bounce, like the wall
    //                                  cling, has to overwrite it afterwards. Same idea as HeroDash owning
    //                                  velocity, but here movement still runs and is overridden.
    // The hitbox is a trigger collider on a child object, enabled for AttackActiveSeconds. All timers are
    // seconds reduced by the step length, so 50 and 100 Hz agree to within one step.
    // Rules and the comparison with the prototype are in docs/study/05-combat.md.
    public class HeroAttack : MonoBehaviour
    {
        const float TimeEpsilon = 1e-4f;
        // The original's BOUNCE_TIME: how long the bounce keeps forcing the rising speed.
        const float BounceSeconds = 0.25f;
        // A sideways swing on a wall halves the slide speed but never faster than this (original and prototype).
        const float WallSlashMaxFall = 5f;
        // An up swing on a wall steps the hero this far off it (original and prototype).
        const float UpSlashWallPush = 0.8f;
        const float RestingVelocityY = 0.01f;

        readonly HashSet<IHittable> hitThisSwing = new HashSet<IHittable>();
        readonly Collider2D[] overlaps = new Collider2D[24];
        HeroController owner;
        BoxCollider2D hitbox;
        HeroSlashEffect slashEffect;

        bool attacking;
        float activeRemaining;
        float cooldownRemaining;
        AttackDirection currentDirection;
        bool clingCancelled;
        bool endedByDamage;

        bool bouncing;
        float bounceElapsed;
        float velocityYAtStepStart;

        // Sideways hit recoil (study 14e): forced horizontal speed for a number of steps after a landed slash.
        int recoilStepsRemaining;
        float recoilDirection;
        bool swingRight;

        public bool IsAttacking => attacking;
        public bool IsRecoiling => recoilStepsRemaining > 0;
        public AttackDirection CurrentDirection => currentDirection;
        public bool IsBouncing => bouncing;
        // The child trigger collider; enabled only while a swing is live.
        public Collider2D Hitbox => hitbox;
        public bool HitboxActive => hitbox != null && hitbox.enabled;
        // Seconds until another swing may start (0 when ready).
        public float CooldownRemaining => Mathf.Max(0f, cooldownRemaining);
        // True while a swing that let go of the wall is running: the wall must not latch the hero straight back.
        internal bool BlocksWallCling => clingCancelled;

        // A swing started (left and right are both Normal; look at HeroController.FacingRight).
        public event Action<AttackDirection> Attacked;
        // A target took the hit.
        public event Action<IHittable, AttackDirection> HitLanded;

        internal void Bind(HeroController controller)
        {
            owner = controller;
            if (hitbox != null) return;
            var go = new GameObject("NailHitbox");
            go.transform.SetParent(transform, false);
            hitbox = go.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            hitbox.enabled = false;
        }

        internal void BeginStep(float dt, bool dashActive)
        {
            var input = owner.Input;
            velocityYAtStepStart = owner.Body.linearVelocity.y;

            if (cooldownRemaining > 0f) cooldownRemaining -= dt;

            // Another system (damage, death) owns the hero now: drop the swing and the bounce and do not start
            // anything. The cooldown still runs on.
            if (owner.State == HeroState.Hurt || owner.State == HeroState.Dead)
            {
                EndSwing();
                bouncing = false;
                endedByDamage = true;
                return;
            }
            endedByDamage = false;

            if (attacking)
            {
                activeRemaining -= dt;
                if (activeRemaining <= TimeEpsilon) EndSwing();
            }

            if (!input.Attack.WasPressed) return;

            if (dashActive || attacking || cooldownRemaining > TimeEpsilon)
            {
                // The original remembers a refused press for a few steps and fires it when possible.
                Stubs.NotImplemented("Hero attack buffer");
                return;
            }
            StartSwing();
        }

        internal void EndStep(float dt)
        {
            if (endedByDamage) return;
            if (attacking) DetectHits();
            UpdateBounce(dt);
            UpdateRecoil();
        }

        // While recoiling, the push speed is forced every step (movement just set the run speed). Anything that takes
        // over the hero's motion ends it without touching the velocity.
        void UpdateRecoil()
        {
            if (recoilStepsRemaining <= 0) return;
            if (owner.Dash.IsDashing || owner.Wall.IsClinging || owner.Wall.IsWallJumpLocked
                || owner.State == HeroState.Hurt || owner.State == HeroState.Dead || owner.ControlLocked)
            {
                recoilStepsRemaining = 0;
                return;
            }
            Vector2 vel = owner.Body.linearVelocity;
            vel.x = recoilDirection * owner.Config.NailRecoilSpeed;
            owner.Body.linearVelocity = vel;
            recoilStepsRemaining--;
        }

        // Feedback of one landed hit (study 14e): silk, then the recoil or bounce for the swing's direction.
        void ApplyHitFeedback(IHittable target, Collider2D c)
        {
            var cfg = owner.Config;
            var feedback = target as IHitFeedbackTarget;
            var nonBouncer = c.GetComponentInParent<NonBouncer>();
            bool nonBouncerActive = nonBouncer != null && nonBouncer.Active;

            if (feedback != null && HeroHitFeelRules.GivesSilk(cfg.AttackDamage, feedback.GivesSilk))
            {
                var data = owner.Health.Data;
                data.silk = HeroHitFeelRules.AddSilk(data.silk, cfg.SilkPerNailHit, data.maxSilk);
            }

            if (currentDirection == AttackDirection.Down)
            {
                // A target without the feedback hook keeps the old rule (any hittable without an active NonBouncer).
                if (HeroHitFeelRules.BounceAllowed(feedback != null, feedback != null && feedback.Bounces, nonBouncerActive))
                {
                    StartBounce();
                    // The hero ignores enemy contact for a couple of steps (hazards still hurt).
                    owner.Health.BeginContactGrace(cfg.DownspikeGraceSteps);
                }
            }
            else if (feedback == null) return;   // walls, dummies: no recoil
            else if (currentDirection == AttackDirection.Up)
            {
                if (owner.Dash.IsDashing || owner.Wall.IsClinging) return;
                Vector2 vel = owner.Body.linearVelocity;
                vel.y = HeroHitFeelRules.CapRiseSpeed(vel.y, cfg.UpRecoilMaxRiseSpeed);
                owner.Body.linearVelocity = vel;
            }
            else
            {
                if (!HeroHitFeelRules.CanRecoilSideways(recoilStepsRemaining > 0, owner.Dash.IsDashing, owner.Wall.IsClinging,
                        owner.Wall.IsWallJumpLocked, owner.State == HeroState.Hurt || owner.State == HeroState.Dead, owner.ControlLocked)
                    || cfg.NailRecoilSteps <= 0) return;
                recoilDirection = HeroHitFeelRules.RecoilDirection(swingRight);
                // Applied by UpdateRecoil later in this same EndStep, so the hit step is step 1 of the push.
                recoilStepsRemaining = cfg.NailRecoilSteps;
            }
        }

        void StartSwing()
        {
            var cfg = owner.Config;
            var input = owner.Input;
            Vector2 move = input.Move;
            bool left = move.x < -cfg.HorizontalPressThreshold;
            bool right = move.x > cfg.HorizontalPressThreshold;
            bool up = move.y > cfg.VerticalPressThreshold;
            bool down = move.y < -cfg.VerticalPressThreshold;
            var wall = owner.Wall;
            bool clinging = wall.IsClinging;
            int clingSide = wall.WallSide;

            // Up wins over down; down on the floor is an ordinary sideways swing (no pogo from standing).
            AttackDirection dir = up ? AttackDirection.Up
                : (down && !owner.IsGrounded) ? AttackDirection.Down
                : AttackDirection.Normal;

            // The swing turns the hero toward a held direction first (not on a wall: the cling decides facing).
            if (!clinging)
            {
                if (right && !left) owner.FacingRight = true;
                else if (left && !right) owner.FacingRight = false;
            }

            bool sideRight = owner.FacingRight;
            clingCancelled = false;
            if (clinging)
            {
                if (dir == AttackDirection.Normal)
                {
                    // Slashes away from the wall (the hero faces the wall) and slows the slide once.
                    sideRight = clingSide < 0;
                    Vector2 v = owner.Body.linearVelocity;
                    v.y = Mathf.Max(v.y * 0.5f, -WallSlashMaxFall);
                    owner.Body.linearVelocity = v;
                }
                else
                {
                    // Up and down swings let go of the wall; up also steps the hero off it.
                    if (dir == AttackDirection.Up)
                        owner.Body.position += new Vector2(-clingSide * UpSlashWallPush, 0f);
                    wall.CancelClingForAttack();
                    clingCancelled = true;
                    sideRight = owner.FacingRight;
                }
            }

            currentDirection = dir;
            swingRight = sideRight;
            PlaceHitbox(cfg, dir, sideRight);
            hitThisSwing.Clear();
            attacking = true;
            activeRemaining = cfg.AttackActiveSeconds;
            // The cooldown is never shorter than the swing itself (the original clamps it the same way).
            cooldownRemaining = Mathf.Max(cfg.AttackCooldownSeconds, cfg.AttackActiveSeconds);
            // Both exist in the original and are not built: the second (alternate) swing is art plus a reset timer,
            // and the default crest answers a down press with a thrusting "downspike" instead of a slash.
            Stubs.NotImplemented("Hero alternate slash");
            if (dir == AttackDirection.Down) Stubs.NotImplemented("Hero downspike thrust");
            owner.Animator?.Play(dir == AttackDirection.Up ? "SlashUp" : dir == AttackDirection.Down ? "SlashDown" : "Slash");
            ShowSlashEffect(dir, sideRight);
            Attacked?.Invoke(dir);
        }

        // The nail swipe is drawn by its own child (visual only). Built on the first swing, once the hero's art exists.
        // Side swings use the first swipe for now; the alternate and the wall offset come with the alternate-swing task.
        void ShowSlashEffect(AttackDirection dir, bool goesRight)
        {
            if (slashEffect == null) slashEffect = HeroSlashEffect.TryCreate(transform, owner.Animator);
            if (slashEffect != null) slashEffect.Show(dir, goesRight, false);
        }

        void PlaceHitbox(HeroConfig cfg, AttackDirection dir, bool sideRight)
        {
            Vector2 size, offset;
            switch (dir)
            {
                case AttackDirection.Up: size = cfg.AttackUpSize; offset = cfg.AttackUpOffset; break;
                case AttackDirection.Down: size = cfg.AttackDownSize; offset = cfg.AttackDownOffset; break;
                default:
                    size = cfg.AttackSideSize;
                    offset = new Vector2(cfg.AttackSideOffset.x * (sideRight ? 1f : -1f), cfg.AttackSideOffset.y);
                    break;
            }
            hitbox.size = size;
            hitbox.offset = offset;
            hitbox.enabled = true;
        }

        void EndSwing()
        {
            attacking = false;
            activeRemaining = 0f;
            clingCancelled = false;
            if (hitbox != null) hitbox.enabled = false;
            hitThisSwing.Clear();
            if (slashEffect != null) slashEffect.Hide();
        }

        void DetectHits()
        {
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = false };
            int count = hitbox.Overlap(filter, overlaps);
            for (int i = 0; i < count; i++)
            {
                var c = overlaps[i];
                if (c == null || c.attachedRigidbody == owner.Body || c.transform.IsChildOf(transform)) continue;
                var target = c.GetComponentInParent<IHittable>();
                // Plain ground and walls are not hittable, so they never pogo.
                if (target == null || !hitThisSwing.Add(target)) continue;

                bool landed = target.TakeHit(new HitInstance
                {
                    Damage = owner.Config.AttackDamage,
                    Direction = currentDirection,
                    Source = gameObject
                });
                if (!landed) continue;
                HitLanded?.Invoke(target, currentDirection);
                ApplyHitFeedback(target, c);
            }
        }

        void StartBounce()
        {
            if (bouncing) return;
            bouncing = true;
            bounceElapsed = 0f;
            if (slashEffect != null) slashEffect.Hide();   // the down swipe ends with the pogo
            // A bounce gives the air dash back (the cooldown is left alone, like the original).
            owner.Dash.ResetAirDashAllowance();
            Stubs.NotImplemented("Hero bounce refreshes double jump");
        }

        // While bouncing, the rising speed is forced every step (movement just applied gravity on top).
        void UpdateBounce(float dt)
        {
            if (!bouncing) return;
            bool started = bounceElapsed > 0f;
            // Anything that takes over the hero's motion ends the bounce without touching its velocity.
            if (owner.Dash.IsDashing || owner.Wall.IsClinging || owner.Wall.IsWallJumpLocked
                || (started && owner.IsGrounded)
                || (started && velocityYAtStepStart <= RestingVelocityY))   // head bump: the rise stalled
            {
                bouncing = false;
                return;
            }

            var cfg = owner.Config;
            Vector2 vel = owner.Body.linearVelocity;
            if (bounceElapsed + TimeEpsilon >= BounceSeconds)
            {
                // The original cuts the vertical speed to zero when the bounce time is over.
                bouncing = false;
                vel.y = 0f;
                owner.Body.linearVelocity = vel;
                return;
            }
            vel.y = cfg.PogoBounceSpeed;
            owner.Body.linearVelocity = vel;
            bounceElapsed += dt;
            owner.IsGrounded = false;
            owner.State = HeroState.Rising;
            owner.Animator?.Play("Jump");
        }

        // A hit ends the swing, the hitbox and the bounce at once; the cooldown keeps running.
        internal void CancelForDamage()
        {
            EndSwing();
            bouncing = false;
            bounceElapsed = 0f;
            recoilStepsRemaining = 0;
        }

        // Respawn: no swing, bounce or cooldown left, so the next press swings immediately.
        internal void ResetForRespawn()
        {
            OnDisable();
        }

        // A disabled hero must not come back mid-swing, mid-bounce or with a stale cooldown.
        void OnDisable()
        {
            EndSwing();
            cooldownRemaining = 0f;
            bouncing = false;
            bounceElapsed = 0f;
            recoilStepsRemaining = 0;
            endedByDamage = false;
        }
    }
}
