using UnityEngine;

namespace Hollowknight.Silksong
{
    // The pure decisions behind the hero's hit feedback (docs/study/14e-hit-feel.md). No engine state, so they are
    // tested in EditMode; HeroAttack applies the results.
    public static class HeroHitFeelRules
    {
        // Silk after a hit: add the amount, clamp to [0, max]. A negative amount or max is treated as zero.
        public static int AddSilk(int silk, int amount, int maxSilk)
        {
            int max = Mathf.Max(0, maxSilk);
            return Mathf.Clamp(silk + Mathf.Max(0, amount), 0, max);
        }

        // Does a landed hit give silk? Needs damage and a target that gives it.
        public static bool GivesSilk(int damage, bool targetGivesSilk) => damage > 0 && targetGivesSilk;

        // A sideways hit recoils the hero only when the hero is in control: not already recoiling, dashing, clinging
        // to a wall, wall-jump locked, hurt/dead or locked by a transition.
        public static bool CanRecoilSideways(bool recoiling, bool dashing, bool clinging, bool wallJumpLocked, bool hurtOrDead, bool controlLocked)
            => !recoiling && !dashing && !clinging && !wallJumpLocked && !hurtOrDead && !controlLocked;

        // Push direction (x) away from a swing that went to the right (+1 is right): -1 for a right swing, +1 for left.
        public static float RecoilDirection(bool swingRight) => swingRight ? -1f : 1f;

        // Upward hit: a rise faster than the limit is cut to the limit; falling or slower is left alone.
        public static float CapRiseSpeed(float velocityY, float maxRiseSpeed) => velocityY > maxRiseSpeed ? maxRiseSpeed : velocityY;

        // Whether a downward hit bounces: the target says yes (or has no opinion: older hittables keep the pogo) and
        // no active NonBouncer blocks it.
        public static bool BounceAllowed(bool hasFeedback, bool feedbackBounces, bool nonBouncerActive)
            => (!hasFeedback || feedbackBounces) && !nonBouncerActive;
    }
}
