using UnityEngine;

namespace Hollowknight.Silksong
{
    // The pure rules of the enemy core, kept free of scene objects so EditMode tests can check them.
    public static class EnemyRules
    {
        // hp -= damage, never below zero. Negative damage heals nothing.
        public static int ApplyDamage(int health, int damage) => Mathf.Max(0, health - Mathf.Max(0, damage));

        // A hit lands only on a living enemy and only when it deals damage (the original ignores zero-damage hits).
        public static bool CanTakeHit(bool isDead, int damage) => !isDead && damage > 0;

        public static bool IsLethal(int healthAfter) => healthAfter <= 0;

        // Sideways shove only for left/right swings (up and down shoves are skipped, study row 18).
        public static bool ShovesSideways(AttackDirection direction) => direction == AttackDirection.Normal;

        // Away from the source along x; a source at the same x pushes right; no source pushes right.
        public static float ShoveDirection(float enemyX, float sourceX, bool hasSource)
        {
            if (!hasSource) return 1f;
            return enemyX >= sourceX ? 1f : -1f;
        }

        public static float ShoveVelocityX(float direction, float speed) => (direction >= 0f ? 1f : -1f) * speed;

        // Distance the shove covers: speed x seconds (15 x 0.15 = 2.25).
        public static float ShoveDistance(float speed, float seconds) => speed * seconds;

        // Flash strength t seconds after the hit: full strength during the hold, then a straight fall to zero.
        // (The original also has a rise time, which is 0 for enemy hits, so it is not modelled.)
        public static float FlashStrength(float t, float hold, float fall, float strength)
        {
            if (t < 0f) return 0f;
            if (t <= hold) return strength;
            if (fall <= 0f) return 0f;
            float k = (t - hold) / fall;
            return k >= 1f ? 0f : strength * (1f - k);
        }

        public static float FlashDuration(float hold, float fall) => Mathf.Max(0f, hold) + Mathf.Max(0f, fall);

        // Contact damage actually dealt: zero once dead (the original zeroes damageDealt on death).
        public static int ContactDamage(int configured, bool isDead) => isDead ? 0 : Mathf.Max(0, configured);
    }
}
