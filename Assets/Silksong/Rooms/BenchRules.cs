using UnityEngine;

namespace Hollowknight.Silksong
{
    // The pure decisions of a bench, kept apart from Unity objects so EditMode tests can cover them.
    public static class BenchRules
    {
        // Speed under which the hero counts as standing still. Not from the original (the bench rule is inferred);
        // a grounded hero that is not running is well under this.
        public const float MaxRestSpeed = 0.1f;

        // Seconds after a rest before another rest may start. A guard on top of the up-press edge.
        public const float CooldownSeconds = 0.5f;

        // True on the step the stick goes from not-up to up. Holding up gives false after the first step.
        public static bool IsUpEdge(bool wasUp, float moveY, float threshold) => !wasUp && moveY > threshold;

        public static bool IsUp(float moveY, float threshold) => moveY > threshold;

        // Whether a rest may begin now. The caller checks the up-press edge separately.
        public static bool CanRest(bool inside, HeroState state, bool grounded, float speed, bool dead, bool controlLocked,
                                   bool transitionRunning, bool resting, float cooldownRemaining)
        {
            if (!inside || !grounded || dead || controlLocked || transitionRunning || resting) return false;
            if (cooldownRemaining > 0f) return false;
            if (state != HeroState.Grounded) return false;   // dashing, attacking, hurt, dead, clinging, airborne
            return Mathf.Abs(speed) <= MaxRestSpeed;
        }
    }
}
