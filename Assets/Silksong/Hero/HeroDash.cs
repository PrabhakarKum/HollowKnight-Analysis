using System;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // The dash. While it runs it owns the hero's velocity outright (HeroMovement is skipped for those
    // steps) because HeroMovement would otherwise zero or overwrite it: it zeroes an idle grounded hero
    // and sets run speed every step. The dash sets a constant horizontal speed with zero vertical speed,
    // which is "no gravity" here because gravity is applied by HeroMovement, not by the body. When the
    // time is up the same step hands over to HeroMovement, so control returns without a stalled step.
    //
    // Rules (see the Dash section of docs/study/02-hero-movement.md):
    //  - starts on a latched Dash press edge, if the cooldown is over and, in the air, the one air dash
    //    of this airtime is unspent;
    //  - direction is the held left/right, otherwise the way the hero already faces;
    //  - the cooldown runs from the START of the dash;
    //  - touching the floor, clinging to a wall or wall jumping refreshes the air dash (HeroWall calls RefreshAirDash).
    // All timers are seconds reduced by the step length, so the result does not depend on the physics rate.
    public class HeroDash : MonoBehaviour
    {
        // Slack for comparing accumulated float seconds, same idea as HeroMovement.
        const float TimeEpsilon = 1e-4f;

        HeroController owner;
        bool dashing;
        float dashRemaining;
        float cooldownRemaining;
        int dashDirection = 1;
        // True from an airborne dash until the hero touches the floor again.
        bool airDashUsed;

        public bool IsDashing => dashing;
        // Whether the air-dash allowance is available (cooldown not included); standing on the floor counts as refreshed.
        public bool CanAirDash => !airDashUsed || (owner != null && owner.IsGrounded);

        public event Action Dashed;

        // Clinging to a wall or jumping off one gives the air dash back and clears the cooldown (the original
        // does both). Called by HeroWall.
        internal void RefreshAirDash()
        {
            ResetAirDashAllowance();
            cooldownRemaining = 0f;
        }

        // Gives the air dash back but leaves the cooldown alone (the pogo bounce does exactly this).
        internal void ResetAirDashAllowance()
        {
            airDashUsed = false;
        }

        internal void Bind(HeroController controller)
        {
            owner = controller;
        }

        // Runs first in every controller step. Returns true when the dash set the hero's velocity this step,
        // in which case the controller must not run HeroMovement.
        internal bool FixedStep(float dt)
        {
            var cfg = owner.Config;
            var input = owner.Input;

            if (cooldownRemaining > 0f) cooldownRemaining -= dt;

            // Another system (damage, death) set the state since the last step: it wins, so the dash must
            // not overwrite that state. The cooldown still runs on.
            if (dashing && (owner.State == HeroState.Hurt || owner.State == HeroState.Dead))
            {
                dashing = false;
                return false;
            }

            // IsGrounded is the result of the last HeroMovement step; during a dash it stays as it was when
            // the dash began, which is fine because the allowance only needs to refresh once movement resumes.
            if (!dashing && owner.IsGrounded) airDashUsed = false;

            // Jumping while the cooldown runs would nearly clear it in the original.
            if (!dashing && cooldownRemaining > TimeEpsilon && input.Jump.WasPressed)
                Stubs.NotImplemented("Hero jump-shortened dash cooldown");

            if (!dashing)
            {
                if (!input.Dash.WasPressed) return false;
                if (!CanStart())
                {
                    // The original remembers a refused press for a short while and fires it when possible.
                    if (owner.State != HeroState.Hurt && owner.State != HeroState.Dead)
                        Stubs.NotImplemented("Hero dash buffer");
                    return false;
                }
                Begin(cfg, input);
            }

            // The dash ends on the step after its last moving step, and movement runs in that same step.
            if (dashRemaining <= TimeEpsilon)
            {
                dashing = false;
                // Dash still held at the end rolls into a sprint in the original.
                if (input.Dash.IsPressed) Stubs.NotImplemented("Hero sprint after dash");
                return false;
            }

            owner.Body.linearVelocity = new Vector2(dashDirection * cfg.DashSpeed, 0f);
            dashRemaining -= dt;
            owner.State = HeroState.Dashing;
            owner.Animator?.Play("Dash");
            return true;
        }

        // A disabled hero must not come back mid-dash or with a stale cooldown or air-dash flag.
        void OnDisable()
        {
            dashing = false;
            dashRemaining = 0f;
            cooldownRemaining = 0f;
            airDashUsed = false;
        }

        bool CanStart()
        {
            if (owner.State == HeroState.Hurt || owner.State == HeroState.Dead) return false;
            if (cooldownRemaining > TimeEpsilon) return false;
            if (!owner.IsGrounded && airDashUsed) return false;
            return true;
        }

        void Begin(HeroConfig cfg, IHeroInput input)
        {
            float x = input.Move.x;
            bool left = x < -cfg.HorizontalPressThreshold;
            bool right = x > cfg.HorizontalPressThreshold;
            // Held direction wins; with nothing (or both) held the hero keeps facing the way it faces.
            if (right && !left) owner.FacingRight = true;
            else if (left && !right) owner.FacingRight = false;
            dashDirection = owner.FacingRight ? 1 : -1;

            // Silksong turns a down press with no sideways input in the air into a downward dash.
            if (!owner.IsGrounded && input.Move.y < -cfg.VerticalPressThreshold && !left && !right)
                Stubs.NotImplemented("Hero down dash");

            if (!owner.IsGrounded) airDashUsed = true;
            dashing = true;
            dashRemaining = cfg.DashSeconds;
            cooldownRemaining = cfg.DashCooldownSeconds;
            Dashed?.Invoke();
        }
    }
}
