using UnityEngine;

namespace Hollowknight.Silksong
{
    // Every hero tuning number lives here so no gameplay code carries a magic number. Defaults are the
    // original's values (see docs/study/02-hero-movement.md for the source of each). The original
    // counts time in fixed steps at 50 Hz; here they are stored as seconds so behaviour does not
    // change if the physics rate does. Fields marked PROVISIONAL were not found in the export.
    [CreateAssetMenu(fileName = "HeroConfig", menuName = "Silksong/Hero Config")]
    public class HeroConfig : ScriptableObject
    {
        [Header("Run and jump (units per second / seconds)")]
        public float RunSpeed = 8.25f;
        public float JumpSpeed = 18.6f;
        // Rising speed is held at JumpSpeed this long while the button stays down (9 steps of 0.02 s).
        public float JumpHoldMaxSeconds = 0.18f;
        // Releasing earlier than this has no effect until it has elapsed (2 steps of 0.02 s).
        public float JumpHoldMinSeconds = 0.04f;
        // Rising velocity is multiplied by this on an early release.
        public float JumpReleaseMultiplier = 0.5f;
        // Downward acceleration in units per second squared: the original's Physics2D gravity (60)
        // times its hero gravity scale (1).
        public float Gravity = 60f;
        public float FallSpeedMax = 30f;
        // Grace after leaving a ledge (4 steps of 0.02 s).
        public float CoyoteSeconds = 0.08f;
        // How long an early jump press is remembered (2 steps of 0.02 s).
        public float JumpBufferSeconds = 0.04f;

        [Header("Input thresholds (from docs/study/04-input.md)")]
        public float HorizontalPressThreshold = 0.3f;
        public float VerticalPressThreshold = 0.5f;

        [Header("Dash (unused until the dash task)")]
        public float DashSpeed = 28f;
        public float DashSeconds = 0.1f;
        public float DashCooldownSeconds = 0.425f;

        [Header("Wall (unused until the wall task)")]
        // PROVISIONAL: the original accelerates down the wall at 24 u/s^2 rather than using a fixed speed.
        public float WallSlideSpeed = 6f;
        public float WallJumpSpeedX = 25f;
        // PROVISIONAL: assumed equal to JumpSpeed; the wall jump was not traced in the original.
        public float WallJumpSpeedY = 18.6f;
        public float WallJumpLockSeconds = 0.3f;

        [Header("Attack (unused until the attack task)")]
        public float AttackActiveSeconds = 0.35f;
        public float AttackCooldownSeconds = 0.41f;
        // PROVISIONAL: best-guess match (BOUNCE_VELOCITY); not confirmed as the pogo bounce.
        public float PogoBounceSpeed = 12f;

        [Header("Hurt (unused until the damage task)")]
        public float HurtInvulnerableSeconds = 1f;
        // PROVISIONAL: RECOIL_VELOCITY is a guess for the damage recoil speed; not confirmed.
        public float HurtKnockbackX = 15f;
        // PROVISIONAL: no vertical knockback value was found.
        public float HurtKnockbackY = 0f;
        public float HurtStunSeconds = 0.2f;
    }
}
