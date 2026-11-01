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
    //  - touching the floor refreshes the air dash.
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
        // Whether a dash begun in the air right now would be allowed by the allowance (cooldown not included).
        public bool CanAirDash => !airDashUsed;

        public event Action Dashed;

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

            // IsGrounded is the result of the last HeroMovement step; during a dash it stays as it was when
            // the dash began, which is fine because the allowance only needs to refresh once movement resumes.
            if (!dashing && owner.IsGrounded) airDashUsed = false;

            if (!dashing)
            {
                if (!input.Dash.WasPressed) return false;
                if (!CanStart()) return false;
                Begin(cfg, input);
            }

            // The dash ends on the step after its last moving step, and movement runs in that same step.
            if (dashRemaining <= TimeEpsilon)
            {
                dashing = false;
                return false;
            }

            owner.Body.linearVelocity = new Vector2(dashDirection * cfg.DashSpeed, 0f);
            dashRemaining -= dt;
            owner.State = HeroState.Dashing;
            owner.Animator?.Play("Dash");
            return true;
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
