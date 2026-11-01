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
                if (!wasGrounded && everAirborne) owner.RaiseLanded();
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

            // Gravity also acts while standing: the contact stops the body, and only a real contact may
            // cancel the fall, so a falling hero cannot hover above the floor.
            if (!jumping) vel.y = Mathf.Max(vel.y - cfg.Gravity * dt, -cfg.FallSpeedMax);

            // Run is digital like the original: a tilted stick past the threshold is full speed.
            int dir = (owner.WantsRight ? 1 : 0) - (owner.WantsLeft ? 1 : 0);
            vel.x = dir * cfg.RunSpeed;
            if (dir != 0) owner.FacingRight = dir > 0;

            body.linearVelocity = vel;

            wasGrounded = grounded;
            owner.IsGrounded = grounded;
            owner.State = grounded ? HeroState.Grounded : (vel.y > 0f ? HeroState.Rising : HeroState.Falling);
            PlayAnimation(dir != 0);
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

        // Clip names are placeholders; the animation task maps them to the real clips.
        void PlayAnimation(bool moving)
        {
            var anim = owner.Animator;
            if (anim == null) return;
            switch (owner.State)
            {
                case HeroState.Grounded: anim.Play(moving ? "Run" : "Idle"); break;
                case HeroState.Rising: anim.Play("Jump"); break;
                default: anim.Play("Fall"); break;
            }
        }
    }
}
