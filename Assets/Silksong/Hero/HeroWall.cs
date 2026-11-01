using UnityEngine;

namespace Hollowknight.Silksong
{
    // Wall cling (the original's "wall slide") and wall jump. HeroMovement still runs every step; this
    // component works on either side of it, same idea as HeroDash but without taking velocity away:
    //  - BeginStep (before movement): ends everything if hurt/dead, and turns a jump press while clinging
    //    into a wall jump. That step is fully handled here (returns true) so movement never sees the press
    //    and cannot start a ground or coyote jump from it.
    //  - EndStep (after movement): probes the walls, then overrides what movement just wrote: the forced
    //    wall-jump push and rise, the sticky cling (no sideways drift, fall speed capped).
    // Rules and the prototype comparison are in the Wall section of docs/study/02-hero-movement.md.
    // All timers are seconds reduced by the step length, so 50 and 100 Hz agree.
    public class HeroWall : MonoBehaviour
    {
        const float TimeEpsilon = 1e-4f;
        // A wall is a surface this upright (minimum |normal.x|).
        const float MinWallNormalX = 0.9f;
        const float RestingVelocityY = 0.01f;
        // Holding away from the wall this long lets go (original WALL_STICKY_STEPS = 3 steps). HeroConfig is a
        // frozen contract and has no field for it, so it is a named constant here.
        const float UnstickSeconds = 0.06f;

        readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        HeroController owner;

        int touchingSide;      // -1 wall on the left, +1 on the right, 0 none (from the last probe)
        bool clinging;
        int clingSide;
        float unstickTime;
        bool prevWantsDown;

        bool lockActive;       // forced horizontal push after a wall jump
        float lockElapsed;
        int lockDir;
        bool riseActive;       // forced rise speed after a wall jump
        float riseElapsed;

        bool endedByDamage;    // hurt/dead seen at the start of this step: do not cling again this step

        public bool IsTouchingWall => touchingSide != 0;
        public bool IsClinging => clinging;
        public bool IsWallJumpLocked => lockActive;
        // -1 left, +1 right, 0 none.
        public int WallSide => touchingSide;

        internal void Bind(HeroController controller)
        {
            owner = controller;
        }

        // Returns true when this component fully handled the step (a wall jump), so movement must not run.
        internal bool BeginStep(float dt)
        {
            var input = owner.Input;

            if (owner.State == HeroState.Hurt || owner.State == HeroState.Dead)
            {
                // Another system owns the hero now; do not overwrite its state or velocity.
                ResetAll();
                endedByDamage = true;
                return false;
            }
            endedByDamage = false;

            // The rise is over when the body stopped going up (ceiling) since the last step.
            if (riseActive && riseElapsed > 0f && owner.Body.linearVelocity.y <= RestingVelocityY)
                riseActive = false;

            if (input.Jump.WasPressed)
            {
                if (clinging)
                {
                    StartWallJump(dt);
                    return true;
                }
                // Touching a wall in the air without clinging can still wall jump in the original (chaining).
                if (touchingSide != 0 && !owner.IsGrounded) Stubs.NotImplemented("Hero wall jump chain");
            }
            return false;
        }

        // Runs after HeroMovement in the same step.
        internal void EndStep(float dt)
        {
            if (endedByDamage) return;

            var cfg = owner.Config;
            var input = owner.Input;
            Vector2 vel = owner.Body.linearVelocity;

            ProbeWall();

            bool wantsToward = touchingSide > 0 ? owner.WantsRight : (touchingSide < 0 && owner.WantsLeft);

            if (!clinging && touchingSide != 0 && wantsToward && !owner.IsGrounded
                && vel.y <= RestingVelocityY && !riseActive)
            {
                BeginCling();
            }
            else if (clinging)
            {
                if (!ContinueCling(dt)) EndCling();
            }

            if (clinging)
            {
                lockActive = false;
                riseActive = false;
                vel.x = 0f;
                if (vel.y > 0f) vel.y = 0f;
                vel.y = Mathf.Max(vel.y, -cfg.WallSlideSpeed);
                owner.Body.linearVelocity = vel;
                owner.State = HeroState.WallClinging;
                owner.FacingRight = clingSide > 0;
                owner.Animator?.Play("WallCling");
            }
            else
            {
                bool changed = false;
                if (lockActive)
                {
                    float t = Mathf.Clamp01(lockElapsed / cfg.WallJumpLockSeconds);
                    vel.x = lockDir * Mathf.Lerp(cfg.WallJumpSpeedX, cfg.RunSpeed, t);
                    changed = true;
                    // Pushing back toward the wall would end the lock early in the original.
                    if ((lockDir > 0 && owner.WantsLeft) || (lockDir < 0 && owner.WantsRight))
                        Stubs.NotImplemented("Hero wall jump short lock");
                    lockElapsed += dt;
                    if (lockElapsed + TimeEpsilon >= cfg.WallJumpLockSeconds) lockActive = false;
                }
                if (riseActive)
                {
                    if (!input.Jump.IsPressed)
                    {
                        vel.y = cfg.WallJumpSpeedY * cfg.JumpReleaseMultiplier;
                        riseActive = false;
                    }
                    else if (riseElapsed + TimeEpsilon < cfg.JumpHoldMaxSeconds)
                    {
                        vel.y = cfg.WallJumpSpeedY;
                        riseElapsed += dt;
                    }
                    else riseActive = false;
                    changed = true;
                }
                if (changed)
                {
                    owner.Body.linearVelocity = vel;
                    owner.State = vel.y > 0f ? HeroState.Rising : HeroState.Falling;
                }
            }

            prevWantsDown = owner.WantsDown;
        }

        // The dash owns the step: it cancels any cling or forced wall-jump motion. Dashing at a wall should
        // end the dash and start a cling in the original; that is not built.
        internal void OnDashStep()
        {
            ResetAll();
            ProbeWall();
            int dir = owner.FacingRight ? 1 : -1;
            if (touchingSide == dir) Stubs.NotImplemented("Hero dash ends at wall");
        }

        void BeginCling()
        {
            clinging = true;
            clingSide = touchingSide;
            unstickTime = 0f;
            // Clinging gives the air dash back and clears the dash cooldown, like the original.
            owner.Dash.RefreshAirDash();
            // The original pauses the fall for 0.15 s and then accelerates down at 24 u/s^2; here the fall is
            // simply capped at WallSlideSpeed. Both of those are unbuilt.
            Stubs.NotImplemented("Hero wall stick delay");
            Stubs.NotImplemented("Hero wall slide acceleration");
        }

        // False when the cling ends this step.
        bool ContinueCling(float dt)
        {
            if (owner.IsGrounded) return false;
            if (touchingSide != clingSide) return false;

            bool away = clingSide > 0 ? (owner.WantsLeft && !owner.WantsRight) : (owner.WantsRight && !owner.WantsLeft);
            bool toward = clingSide > 0 ? owner.WantsRight : owner.WantsLeft;
            unstickTime = away ? unstickTime + dt : 0f;
            if (unstickTime + TimeEpsilon >= UnstickSeconds) return false;

            // A fresh down press lets go too, unless the hero is pressing into the wall.
            if (owner.WantsDown && !prevWantsDown && !toward) return false;
            return true;
        }

        // Letting go turns the hero away from the wall, as the original does.
        void EndCling()
        {
            if (!clinging) return;
            clinging = false;
            owner.FacingRight = clingSide < 0;
        }

        void StartWallJump(float dt)
        {
            var cfg = owner.Config;
            int away = -clingSide;
            clinging = false;
            lockActive = true;
            lockDir = away;
            lockElapsed = dt;
            riseActive = true;
            riseElapsed = dt;
            owner.Dash.RefreshAirDash();
            owner.Body.linearVelocity = new Vector2(away * cfg.WallJumpSpeedX, cfg.WallJumpSpeedY);
            owner.FacingRight = away > 0;
            owner.IsGrounded = false;
            owner.State = HeroState.Rising;
            owner.Animator?.Play("Jump");
            touchingSide = 0;
        }

        // Walls are only felt within twice the collider skin, so "touching" means touching.
        void ProbeWall()
        {
            touchingSide = 0;
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = owner.GroundLayers,
                useTriggers = false
            };
            float reach = Physics2D.defaultContactOffset * 2f;
            if (HitsWall(Vector2.right, filter, reach)) touchingSide = 1;
            else if (HitsWall(Vector2.left, filter, reach)) touchingSide = -1;
        }

        bool HitsWall(Vector2 dir, ContactFilter2D filter, float reach)
        {
            int count = owner.BodyCollider.Cast(dir, filter, hits, reach);
            for (int i = 0; i < count; i++)
                if (Mathf.Abs(hits[i].normal.x) >= MinWallNormalX) return true;
            return false;
        }

        void ResetAll()
        {
            clinging = false;
            lockActive = false;
            riseActive = false;
            unstickTime = 0f;
        }

        // A disabled hero must not come back clinging or mid-push.
        void OnDisable()
        {
            ResetAll();
            touchingSide = 0;
            lockElapsed = 0f;
            riseElapsed = 0f;
            prevWantsDown = false;
            endedByDamage = false;
        }
    }
}
