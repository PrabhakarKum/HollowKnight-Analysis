using UnityEngine;

namespace Hollowknight.Silksong
{
    // Ground and air movement: run, variable-height jump, coyote time, jump buffering, gravity and
    // fall cap. Runs once per fixed step and reads only HeroConfig. Every duration is in seconds and
    // advanced by the step length, so the result matches at any physics rate.
    public class HeroMovement
    {
        // Comparisons on accumulated float seconds need a little slack to not depend on rounding.
        const float TimeEpsilon = 1e-4f;
        // Floor normals steeper than this (as a minimum y) are walls, not floors.
        const float MinFloorNormalY = 0.7f;
        // Tolerance for "not moving upward" so resting contact jitter still counts as grounded.
        const float RestingVelocityY = 0.01f;

        readonly HeroController owner;
        readonly RaycastHit2D[] hits = new RaycastHit2D[8];

        bool wasGrounded;
        // Landing is only reported after having been in the air, so spawning on the floor is silent.
        bool everAirborne;
        bool jumping;
        float jumpElapsed;

        // Landing animation: Landed was raised and "Land" has not started yet / "Land" was started by us and may
        // still be running. Both are dropped the moment the hero runs, leaves the floor, swings or is hurt.
        bool landPending;
        bool landing;

        // Seconds since the hero left the floor (0 on the step it left); a ledge jump is allowed while
        // this is within CoyoteSeconds, inclusive.
        float airTime;
        bool coyoteArmed;

        // Seconds since an unspent jump press was seen (0 on the step of the press), usable while
        // within JumpBufferSeconds, inclusive.
        bool bufferArmed;
        float bufferAge;
        // A press and release inside one fixed step is a tap: it must still jump even though the
        // button is already up when the jump becomes possible.
        bool bufferSurvivesRelease;

        public HeroMovement(HeroController owner)
        {
            this.owner = owner;
        }

        public void FixedStep(float dt)
        {
            var cfg = owner.Config;
            var input = owner.Input;
            var body = owner.Body;
            Vector2 vel = body.linearVelocity;

            ReadIntent(input.Move, cfg);

            bool grounded = ProbeGround() && vel.y <= RestingVelocityY && !jumping;

            if (input.Jump.WasPressed)
            {
                bufferArmed = true;
                bufferAge = 0f;
                bufferSurvivesRelease = input.Jump.WasReleased;
            }
            else if (bufferArmed)
            {
                bufferAge += dt;
                // Like the original, a queued jump is dropped if the button is let go in a later step.
                if (!input.Jump.IsPressed && !bufferSurvivesRelease) bufferArmed = false;
                if (bufferAge > cfg.JumpBufferSeconds + TimeEpsilon) bufferArmed = false;
            }

            if (grounded)
            {
                airTime = 0f;
                coyoteArmed = true;
                if (!wasGrounded && everAirborne) { landPending = true; owner.RaiseLanded(); }
            }
            else
            {
                everAirborne = true;
                if (wasGrounded) airTime = 0f; else airTime += dt;
            }

            bool coyoteOk = coyoteArmed && airTime <= cfg.CoyoteSeconds + TimeEpsilon;
            if (bufferArmed && (grounded || coyoteOk))
            {
                jumping = true;
                jumpElapsed = 0f;
                bufferArmed = false;
                bufferSurvivesRelease = false;
                coyoteArmed = false;
                grounded = false;
            }

            if (jumping) vel = SustainJump(vel, cfg, input.Jump.IsPressed, dt);

            // Standing still must stay still on any floor the probe accepts (a frictionless body would
            // otherwise creep down slopes). Requires being grounded on the previous step too, so a hero
            // that is still arriving keeps falling until it really touches.
            int dir = (owner.WantsRight ? 1 : 0) - (owner.WantsLeft ? 1 : 0);
            if (!jumping && grounded && wasGrounded && dir == 0) vel = Vector2.zero;
            else if (!jumping) vel.y = Mathf.Max(vel.y - cfg.Gravity * dt, -cfg.FallSpeedMax);

            // Run is digital like the original: a tilted stick past the threshold is full speed.
            vel.x = dir * cfg.RunSpeed;
            if (dir != 0) owner.FacingRight = dir > 0;

            body.linearVelocity = vel;

            wasGrounded = grounded;
            owner.IsGrounded = grounded;
            // Hurt and Dead belong to the health system: movement must not overwrite them (it ran over them
            // before the damage task existed). Health hands the state back itself when the stun ends.
            if (owner.State == HeroState.Hurt || owner.State == HeroState.Dead) { landPending = landing = false; return; }
            owner.State = grounded ? HeroState.Grounded : (vel.y > 0f ? HeroState.Rising : HeroState.Falling);
            PlayAnimation(dir != 0);
        }

        // Hurt, death and respawn drop a jump in progress, the buffered press and the coyote window, and forget
        // the ground contact only when asked (respawn), so a hit that leaves the hero on the floor never raises Landed.
        public void ResetForDamage(bool forgetGround)
        {
            jumping = false;
            jumpElapsed = 0f;
            landPending = landing = false;
            bufferArmed = false;
            bufferSurvivesRelease = false;
            coyoteArmed = false;
            if (forgetGround) wasGrounded = false;
            airTime = 0f;
        }

        // While the button is held the rise speed is simply re-set to JumpSpeed each step (gravity is
        // skipped), up to the maximum hold. An early release cuts the speed once the minimum hold has
        // passed, so a quick tap still gets a small hop instead of nothing.
        Vector2 SustainJump(Vector2 vel, HeroConfig cfg, bool held, float dt)
        {
            bool minDone = jumpElapsed + TimeEpsilon >= cfg.JumpHoldMinSeconds;

            // Rising stalled (ceiling): the jump is over, let gravity take it from here.
            if (jumpElapsed > 0f && vel.y <= RestingVelocityY)
            {
                jumping = false;
                return vel;
            }

            if (!held && minDone)
            {
                if (vel.y > 0f) vel.y *= cfg.JumpReleaseMultiplier;
                jumping = false;
            }
            else if (jumpElapsed + TimeEpsilon < cfg.JumpHoldMaxSeconds)
            {
                vel.y = cfg.JumpSpeed;
                jumpElapsed += dt;
            }
            else
            {
                jumping = false;
            }
            return vel;
        }

        void ReadIntent(Vector2 move, HeroConfig cfg)
        {
            owner.WantsLeft = move.x < -cfg.HorizontalPressThreshold;
            owner.WantsRight = move.x > cfg.HorizontalPressThreshold;
            owner.WantsUp = move.y > cfg.VerticalPressThreshold;
            owner.WantsDown = move.y < -cfg.VerticalPressThreshold;
        }

        // The probe reaches no further than twice the collider skin, so "grounded" means touching, not
        // merely close.
        bool ProbeGround()
        {
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = owner.GroundLayers,
                useTriggers = false
            };
            int count = owner.BodyCollider.Cast(Vector2.down, filter, hits, Physics2D.defaultContactOffset * 2f);
            for (int i = 0; i < count; i++)
                if (hits[i].normal.y >= MinFloorNormalY) return true;
            return false;
        }

        // The hero's own clip names; the importer saves the original's clips under them (docs/study/07).
        //
        // Land: on the step Landed is raised and the hero stands still, "Land" plays once. While it runs the per-step
        // Idle request is held back; when it finishes (IsPlaying turns false) Idle follows. Anything else cuts it at
        // once: running (Run is never blocked), leaving the floor (jump, fall, dash, wall), being hurt, or a swing.
        // Landing while already moving goes straight to Run.
        void PlayAnimation(bool moving)
        {
            if (owner.State != HeroState.Grounded || moving) landPending = landing = false;
            var anim = owner.Animator;
            if (anim == null) return;
            // A swing is an overlay on movement (HeroAttack sets no HeroState): do not replace its clip with
            // Idle/Run/Fall on the step it started, or it would never show.
            if (owner.Attack != null && owner.Attack.IsAttacking
                && (anim.IsPlaying("Slash") || anim.IsPlaying("SlashUp") || anim.IsPlaying("SlashDown")))
            {
                landPending = landing = false;
                return;
            }
            switch (owner.State)
            {
                case HeroState.Grounded:
                    if (moving) anim.Play("Run");
                    else if (landPending)
                    {
                        landPending = false;
                        landing = true;
                        anim.Play("Land");
                    }
                    else if (landing && anim.IsPlaying("Land")) { }
                    else
                    {
                        landing = false;
                        anim.Play("Idle");
                    }
                    break;
                case HeroState.Rising: anim.Play("Jump"); break;
                default: anim.Play("Fall"); break;
            }
        }
    }
}
