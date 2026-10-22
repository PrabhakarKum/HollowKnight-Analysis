using UnityEngine;
using Hollowknight.Feel;

namespace Hollowknight.Player
{
    /// <summary>
    /// All movement/jump tunables. Time-based values that HK-style games count in fixed steps are
    /// exposed as steps (at the default 50 Hz fixed timestep, 1 step = 0.02 s) so they feel the same
    /// regardless of frame rate.
    /// </summary>
    [CreateAssetMenu(menuName = "Hollowknight/Player Tuning", fileName = "PlayerTuning")]
    public class PlayerTuning : ScriptableObject
    {
        [Header("Run")]
        public float runSpeed = 8.3f;
        public float groundAcceleration = 140f;
        public float groundDeceleration = 180f;
        public float airAcceleration = 90f;
        public float airDeceleration = 90f;
        [Range(0f, 0.5f)] public float inputDeadzone = 0.2f;

        [Header("Jump")]
        [Tooltip("Upward speed held while the jump button is held.")]
        public float jumpSpeed = 15f;
        [Tooltip("Max fixed steps the jump keeps rising while held.")]
        public int jumpSteps = 7;
        [Tooltip("Min fixed steps before releasing the button can end the jump.")]
        public int jumpStepsMin = 4;
        [Tooltip("Fraction of upward speed kept when the button is released early (the original game halves it).")]
        [Range(0f, 1f)] public float jumpEndVelocityKeep = 0.5f;
        [Tooltip("Fraction of upward speed kept when the rise reaches its full step count (1 = coast over the top).")]
        [Range(0f, 1f)] public float jumpNaturalEndKeep = 1f;

        [Header("Double jump")]
        [Tooltip("Extra jumps allowed in the air before touching ground / a wall. 0 disables.")]
        public int airJumps = 1;
        public float doubleJumpSpeed = 12f;
        [Tooltip("Max fixed steps the double jump keeps rising while held.")]
        public int doubleJumpSteps = 6;
        [Tooltip("The original double jump is a fixed-length rise, not button-dependent. Keep equal to doubleJumpSteps for that.")]
        public int doubleJumpStepsMin = 6;

        [Tooltip("Pressing jump while falling and this close to the ground (or closer than fall speed x jump buffer time) is kept as a buffered ground jump instead of spending the air jump.")]
        public float airJumpMinGroundGap = 0.3f;

        [Header("Float (a fresh jump press in the air once the double jump is spent; hold to keep floating)")]
        [Tooltip("OFF by default: the hero never floats, however many keys are pressed. The code is kept if you ever want the ability back.")]
        public bool floatEnabled = false;
        public float floatFallSpeed = 3.5f;
        [Tooltip("Off (default): a float only starts from a press made while already falling, so mashing jump right after a double jump does not make the hero hover at the top. On: a press made during the rise is remembered and floats at the top (closer to the original's queued presses).")]
        public bool floatFromPressDuringRise = false;
        [Tooltip("Floating cannot start when ground is closer than this below the player (the original refuses to float when about to land).")]
        public float floatMinGroundGap = 1.0f;
        [Tooltip("Max steps a single float can last. 0 = unlimited.")]
        public int floatMaxSteps = 100;

        [Header("Gravity")]
        public float gravity = 70f;
        public float maxFallSpeed = 20f;
        [Tooltip("Air hang: gravity multiplier right after a jump's rise ends (the game uses low gravity here).")]
        [Range(0.05f, 1f)] public float hangGravityMultiplier = 0.45f;
        [Tooltip("Air hang: how fast the gravity multiplier climbs back to 1 per second while the jump button is held. Releasing snaps it to 1.")]
        public float hangGravityRampPerSecond = 3f;

        [Header("Forgiveness (fixed steps)")]
        [Tooltip("Coyote time: steps after leaving a ledge during which jump still works.")]
        public int coyoteSteps = 8;
        [Tooltip("Jump buffer: steps a jump press is remembered before landing.")]
        public int jumpBufferSteps = 6;
        [Tooltip("Steps a jump release is remembered so a very short tap still gives the min jump.")]
        public int jumpReleaseBufferSteps = 4;

        [Header("Dash")]
        public float dashSpeed = 22f;
        [Tooltip("Fixed steps a dash lasts on the ground.")]
        public int dashSteps = 9;
        [Tooltip("Fixed steps a dash lasts in the air (the original has separate ground / air dash times).")]
        public int airDashSteps = 8;
        [Tooltip("Steps from the START of a dash until another can start (the original starts the cooldown at dash start, not at its end).")]
        public int dashCooldownSteps = 23;
        [Tooltip("Jumping shortens a running dash cooldown to at most this many steps (the original sets it to 0.05 s).")]
        public int jumpDashCooldownSteps = 3;
        [Tooltip("Steps a dash press is buffered.")]
        public int dashBufferSteps = 6;
        [Tooltip("Dashes allowed in the air before touching ground / a wall.")]
        public int airDashes = 1;
        [Tooltip("Fraction of run speed kept horizontally when a dash ends.")]
        [Range(0f, 1.5f)] public float dashExitSpeedFactor = 1f;

        [Header("Wall slide / wall jump")]
        public float wallCheckDistance = 0.08f;
        [Tooltip("Max downward speed while sliding on a wall (hold toward the wall).")]
        public float wallSlideSpeed = 3.5f;
        [Tooltip("Steps after leaving a wall during which a wall jump still works.")]
        public int wallCoyoteSteps = 5;
        [Tooltip("Sticky wall: once sliding, you stay on the wall until you hold AWAY from it for this many steps (or stop touching it).")]
        public int wallStickySteps = 8;
        [Tooltip("Horizontal kick-off speed away from the wall; it decays in a straight line to run speed over the lock steps.")]
        public float wallJumpSpeedX = 12f;
        [Tooltip("Steps horizontal input is ignored after a wall jump while the kick-off speed decays.")]
        public int wallJumpLockSteps = 10;
        [Tooltip("Reset the air dash when sliding on / jumping from a wall.")]
        public bool wallResetsAirDash = true;

        [Header("Forgiveness (position)")]
        [Tooltip("Rising into the underside of a platform corner: slide sideways up to this far to slip past it.")]
        public float cornerCorrection = 0.3f;
        [Tooltip("In the air, pressing into a ledge whose top is at most this far above the feet: pop up onto it.")]
        public float ledgeStepUp = 0.25f;

        [Header("Hard landing")]
        public bool hardLandingEnabled = true;
        [Tooltip("Seconds spent falling at terminal speed before the next landing is a hard landing.")]
        public float bigFallTime = 1.1f;
        [Tooltip("Seconds the hero is stuck in the hard-landing state, ignoring all input.")]
        public float hardLandingTime = 0.5f;

        [Header("Attack (timers in seconds, like the original)")]
        [Tooltip("How long a swing lasts. You cannot start another swing during it.")]
        public float attackDuration = 0.30f;
        [Tooltip("Time from the start of a swing until the next may start. Never shorter than the duration (the original clamps it).")]
        public float attackCooldown = 0.36f;
        [Tooltip("How long the damaging hitbox is active at the start of a swing.")]
        public float slashActiveTime = 0.14f;
        [Tooltip("An attack pressed this many fixed steps early is remembered.")]
        public int attackBufferSteps = 6;
        [Tooltip("The two-swing combo restarts if the gap since the last swing exceeds duration + this, or the direction changes.")]
        public float comboResetTime = 0.4f;
        [Tooltip("Forward slash: box size and the offset of its centre from the player (x is mirrored with facing).")]
        public Vector2 forwardSlashSize = new Vector2(1.6f, 1.2f);
        public Vector2 forwardSlashOffset = new Vector2(1.2f, 0.15f);
        public Vector2 upSlashSize = new Vector2(1.4f, 1.5f);
        public Vector2 upSlashOffset = new Vector2(0f, 1.15f);
        public Vector2 downSlashSize = new Vector2(1.4f, 1.4f);
        public Vector2 downSlashOffset = new Vector2(0f, -1.1f);
        [Tooltip("Hitting something with a sideways slash pushes you away for this many fixed steps at the speed below.")]
        public int recoilSteps = 5;
        public float recoilSpeed = 8f;
        [Tooltip("Down-slash pogo: the bounce is a short jump that starts this many steps into the normal rise (so jumpSteps minus this remain). It cannot be cut short.")]
        public int bounceStartSteps = 3;
        [Tooltip("A forward slash while wall sliding halves the fall speed, but never faster than this downward speed.")]
        public float wallSlashMaxFall = 5f;
        [Tooltip("An up-slash while wall sliding steps the hero this far away from the wall (the original uses 0.8).")]
        public float upSlashWallPush = 0.8f;

        [Header("Damage / health (all values are our own guesses; the original keeps them in asset files)")]
        [Tooltip("Health 'masks' the hero starts with.")]
        public int maxHealth = 5;
        [Tooltip("Seconds of invulnerability after a hit (the original's INVUL_TIME). Runs in game time, so the hit freeze does not eat it.")]
        public float invulnerableTime = 1.3f;
        [Tooltip("Knock-back velocity after a hit. x points away from the thing that hurt, y is a small hop.")]
        public Vector2 damageRecoilVelocity = new Vector2(9f, 5f);
        [Tooltip("Seconds the knock-back lasts (the original's RECOIL_DURATION). Input is ignored and gravity is off meanwhile.")]
        public float damageRecoilTime = 0.2f;
        [Tooltip("Seconds between dying and being put back at the checkpoint.")]
        public float deathRespawnDelay = 1.6f;
        [Tooltip("Spikes / hazards: seconds between the hit and being moved to the last safe ground.")]
        public float hazardRespawnDelay = 0.5f;
        [Tooltip("Seconds the hero is held still after being moved back (input ignored) before control returns.")]
        public float respawnHoldTime = 0.25f;
        [Tooltip("Invulnerability flicker: seconds per on/off pulse, and the opacity of the dim phase.")]
        public float flickerInterval = 0.08f;
        [Range(0f, 1f)] public float flickerDimAlpha = 0.3f;

        [Header("Hit-stop & screen shake (freeze timings are read from the original; which freeze / shake goes with which event, and the shake sizes in ShakePresets.cs, are our own choices)")]
        [Tooltip("Freeze when the hero is hurt or dies. None = no freeze. HeroDamage stops the game ~0.28 s, HeroDamageShort ~0.15 s.")]
        public FreezeType heroDamageFreeze = FreezeType.HeroDamage;
        [Tooltip("Camera shake when the hero is hurt.")]
        public ShakePreset heroDamageShake = ShakePreset.Average;
        [Tooltip("Camera shake when the hero dies.")]
        public ShakePreset heroDeathShake = ShakePreset.Big;
        [Tooltip("Freeze on the FIRST thing each swing hits (the original's per-attack 'first hit freeze'; its default is none, so this length is our choice). QuickFreeze ~0.02 s.")]
        public FreezeType nailHitFreeze = FreezeType.QuickFreeze;
        [Tooltip("Camera shake on the first thing each swing hits.")]
        public ShakePreset nailHitShake = ShakePreset.Tiny;
        [Tooltip("Camera shake when a hard landing happens.")]
        public ShakePreset hardLandShake = ShakePreset.Average;
        [Tooltip("The camera rumbles from the moment a fall has lasted bigFallTime until the hero lands, is hurt, or dies (the original's 'RumblingFall').")]
        public bool fallRumbleEnabled = true;
        [Tooltip("Let big shakes also stop the game for a frame or two (the camera's 'freeze frames').")]
        public bool shakeFreezeFrames = true;

        [Header("Silk / bind (rules read from the original; every number is our own guess - the bind itself is a PlayMaker state machine in the original, not readable as code)")]
        [Tooltip("Silk the hero can hold. The original's 'can bind' threshold is 9.")]
        public int maxSilk = 9;
        [Tooltip("Silk gained per nail hit on a target (the original's HitSilkGeneration.Full is 1 per hit).")]
        public int silkPerHit = 1;
        [Tooltip("Silk a bind uses up.")]
        public int bindCost = 9;
        [Tooltip("Seconds the button must be held for a bind to finish.")]
        public float bindTime = 1.0f;
        [Tooltip("Masks restored by a finished bind.")]
        public int bindHeal = 3;

        [Header("Sprint (read from the original's hooks; the speeds live in a PlayMaker state machine we cannot read, so every number is our own guess)")]
        [Tooltip("Holding the dash button after a dash keeps the hero sprinting. Off = the dash behaves exactly as before.")]
        public bool sprintEnabled = true;
        [Tooltip("Sprint speed (run is 8.3, a dash is 22).")]
        public float sprintSpeed = 14f;
        [Tooltip("How fast the speed eases from the dash's speed down to the sprint speed.")]
        public float sprintAcceleration = 70f;
        [Tooltip("Sprint jump (the original's 'shuttlecock'): forced forward speed while rising.")]
        public float shuttlecockSpeed = 14f;
        [Tooltip("The sprint jump starts this many steps 'early' (the original sets jumped_steps to -5), so the rise lasts this much longer.")]
        public int shuttlecockExtraSteps = 5;
        [Tooltip("When a sprint jump is cut off in the air (the fall starts, a swing, a double jump), its speed fades out at this rate per second (turning around cancels it).")]
        public float shuttleCarryDecay = 7f;

        [Header("Mantle / clamber (the ledge test and the numbers marked 'read' come from the original's CheckClamberLedge; the climb motion is a PlayMaker state machine we cannot read, so its timing is our guess)")]
        [Tooltip("Off = the hero never pulls itself up onto ledges (the old behaviour).")]
        public bool mantleEnabled = true;
        [Tooltip("Read: a mantle can only start while the vertical speed is below this (not while still shooting upward).")]
        public float mantleMaxRiseSpeed = 5f;
        [Tooltip("Read: probe height above the hero's centre for the ledge test (the original uses 0.67).")]
        public float mantleProbeHeight = 0.67f;
        [Tooltip("Derived from the original (0.37 / 0.77 ahead of ITS centre, 0.75 head ray), but measured here from our hero's FRONT EDGE because our collider is wider than the original's (assumed ~0.3 half-width there): how far past the hero's front edge the two downward ledge probes are, and the length of the free-space ray at head height beyond the edge.")]
        public float mantleProbeNear = 0.07f, mantleProbeFar = 0.47f, mantleHeadRay = 0.45f;
        [Tooltip("Read: length of the downward ledge probes (2.26) and of the upward headroom check above the ledge (2.16).")]
        public float mantleDownRay = 2.26f, mantleHeadroom = 2.16f;
        [Tooltip("Read: the two ledge probes must land within this height of each other (a flat ledge top).")]
        public float mantleFlatTolerance = 0.1f;
        [Tooltip("Read: the ledge top must be at least this far above the ground under the hero (1.5), so low steps are not mantled; the ground probe is 2.0 long.")]
        public float mantleMinLedgeAboveGround = 1.5f, mantleGroundProbe = 2f;
        [Tooltip("Seconds the pull-up takes (guess). The ledge-climb animation is sped up to fit.")]
        public float mantleTime = 0.40f;
        [Tooltip("Share of the pull-up spent going straight up before stepping onto the ledge (guess).")]
        [Range(0.2f, 0.9f)] public float mantleUpShare = 0.6f;
        [Tooltip("How far the hero ends up past its starting x, onto the ledge (guess).")]
        public float mantleStepForward = 0.7f;
        [Tooltip("A jump press this many steps into the mantle cancels it into a jump (the original lets a jump cancel the mantle).")]
        public int mantleJumpCancelStep = 2;
        [Tooltip("Steps after a cancelled mantle before another may start (guess).")]
        public int mantleCooldownSteps = 10;

        [Header("Ground check")]
        public LayerMask groundMask = ~0;
        public float groundCheckDistance = 0.06f;
        [Range(0f, 90f)] public float maxGroundAngle = 50f;
    }
}
