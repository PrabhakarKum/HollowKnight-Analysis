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
        // Grace after leaving a ledge: the original counts 4 steps (0.08 s at 50 Hz). Here the window is
        // inclusive seconds, so at 50 Hz it covers one more step than the original count.
        public float CoyoteSeconds = 0.08f;
        // How long an early jump press is remembered: the original queue is 2 steps (0.04 s at 50 Hz). Here
        // the window is inclusive seconds, so at 50 Hz it covers one more step than the original count.
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

        // Added in the attack task: the config had no hitbox sizes or damage. Boxes are centred on the hero's
        // position; the x offset of the sideways box is mirrored with the facing side. PROVISIONAL: the
        // original uses polygon colliders on the slash prefabs, so these are the owner's hand-tuned prototype
        // boxes (PlayerTuning.asset) and must not be changed without the owner.
        public Vector2 AttackSideSize = new Vector2(1.1f, 1.2f);
        public Vector2 AttackSideOffset = new Vector2(0.8f, 0.15f);
        public Vector2 AttackUpSize = new Vector2(1.4f, 0.8f);
        public Vector2 AttackUpOffset = new Vector2(0f, 1.15f);
        public Vector2 AttackDownSize = new Vector2(1.4f, 0.5f);
        public Vector2 AttackDownOffset = new Vector2(0f, 0f);
        // PROVISIONAL: the original's damage grows with needle upgrades; 1 is the prototype's nailDamage.
        public int AttackDamage = 1;

        // Hit feel on the hero (Milestone 4 task 5, docs/study/14e-hit-feel.md). Additive: values are the original's.
        // Silk gained per damaging nail hit on a target that gives silk (HeroController.SilkGain).
        public int SilkPerNailHit = 1;
        // Sideways hit: push away from the target at this speed (RECOIL_HOR_VELOCITY) ...
        public float NailRecoilSpeed = 3.75f;
        // ... for this many physics steps (RECOIL_HOR_STEPS; 8 steps = 0.16 s at 50 Hz, counted in steps like the original).
        public int NailRecoilSteps = 8;
        // Upward hit: a rise faster than this is cut to it (RECOIL_DOWN_VELOCITY).
        public float UpRecoilMaxRiseSpeed = 0f;
        // After a downward hit that bounces, enemy contact is ignored for this many steps (DOWNSPIKE_INVULNERABILITY_STEPS;
        // the original's long downspike uses 10 and is not built).
        public int DownspikeGraceSteps = 2;

        [Header("Hurt (unused until the damage task)")]
        public float HurtInvulnerableSeconds = 1f;
        // PROVISIONAL: RECOIL_VELOCITY is a guess for the damage recoil speed; not confirmed.
        public float HurtKnockbackX = 15f;
        // PROVISIONAL: no vertical knockback value was found.
        public float HurtKnockbackY = 0f;
        public float HurtStunSeconds = 0.2f;

        [Header("Room transitions (docs/study/10-room-transitions.md section 3; read from the original and its hero prefab)")]
        // How long the hero walks inward after arriving through a left or right gate before control returns.
        public float TransitionWalkInSeconds = 0.33f;
        // Arriving through a top gate: the downward speed the hero is dropped with, and how long until control returns.
        public float TransitionDropSpeed = -12f;
        public float TransitionDropSeconds = 0.33f;
        // Arriving through a bottom gate: the hero starts this far above the gate and is thrown sideways (the way it
        // faces) and up for TransitionBottomSeconds, then falls.
        public float TransitionBottomLift = 3f;
        public float TransitionBottomSeconds = 0.1f;
        public float TransitionBottomSpeedX = 8f;
        public float TransitionBottomSpeedY = 9.4f;
        // Leaving through a top gate: the hero is pushed upward at this speed (the original's MIN_JUMP_SPEED).
        public float TransitionLeaveUpSpeed = 3f;
    }
}
