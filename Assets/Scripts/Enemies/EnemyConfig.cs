using UnityEngine;

namespace Hollowknight.Enemies
{
    public enum EnemyKind { Gunner, Archer, Brawler, BladeThrower }

    /// <summary>
    /// One melee blow (the brawler has a punch and a kick). The animation is played by hand so the hit lines up with the art:
    /// the wind-up plays through the telegraph (stretched to fill it, with a lean back and a snap forward) and arrives at 'strikeTime',
    /// where the hitbox goes live on the first frame with the limb fully out. The hitbox reaches only as far as the limb does.
    /// </summary>
    [System.Serializable]
    public class MeleeMove
    {
        [Tooltip("Animation clip for the wind-up and the swing.")]
        public string clip = "PunchA";
        [Tooltip("Length of the clip in seconds.")]
        public float clipLength = 0.375f;
        [Tooltip("Seconds into the clip where the blow lands: the first frame with the limb fully out.")]
        public float strikeTime = 0.125f;
        [Tooltip("The enemy starts this move when the hero is this close (centre to centre). Set so the limb ends up touching the hero's art.")]
        public float startRange = 1.15f;
        [Tooltip("Seconds after the strike frame appears before the hitbox goes live: the limb is seen reaching out, THEN it connects (0 = the hit and the frame are the same instant, which reads as the damage landing before the animation).")]
        public float hitDelay = 0.04f;
        [Tooltip("The hitbox is live this long (real seconds) once it has started.")]
        public float hitWindow = 0.08f;
        [Tooltip("Animation speed while the hitbox is live (a little below 1 keeps the limb out long enough to read without looking like a stop).")]
        public float hitSpeed = 0.75f;
        [Tooltip("Animation speed for the rest of the swing.")]
        public float swingSpeed = 1f;
        [Tooltip("During the wind-up the body leans back this far (world units, pixel-stepped) and then snaps forward into the strike, so the telegraph is a movement, not a held pose.")]
        public float pullBack = 0.16f;
        /// <summary>Seconds the slowed-down part of the swing lasts (from the strike frame appearing to the end of the hit window).</summary>
        public float SlowSpan => hitDelay + hitWindow;
        /// <summary>Seconds from the strike to the end of the swing, so the clip's last frame arrives exactly as the attack state ends (no hold at the end).</summary>
        public float FollowThrough => SlowSpan + Mathf.Max(0f, clipLength - strikeTime - SlowSpan * hitSpeed) / Mathf.Max(0.1f, swingSpeed);
        [Tooltip("How far the limb's tip reaches from the enemy's centre in the strike frame (measured from the art). The tests check the blow only lands once the tip touches the hero's art.")]
        public float limbReach = 1.06f;
        [Tooltip("Hitbox size and its centre relative to the enemy's centre (x mirrored with facing). Its far edge sits just inside the limb's tip.")]
        public Vector2 hitSize = new Vector2(0.65f, 0.7f);
        public Vector2 hitOffset = new Vector2(0.575f, -0.05f);
    }

    /// <summary>
    /// One ranged shot (gunner bullet, archer arrow, thrown blade). Like a MeleeMove, the animation is played by hand so the projectile leaves
    /// on the animation's release frame: the wind-up clip is stretched over the telegraph (never held), then the firing clip plays from a few
    /// frames before the release, and the projectile appears exactly when the clip reaches 'releaseTime'.
    /// </summary>
    [System.Serializable]
    public class ShotMove
    {
        [Tooltip("Telegraph: this clip is stretched over the whole telegraph (so the animation keeps moving) up to 'windupTo'. Empty = use the telegraph clip as it is.")]
        public string windupClip = "";
        public float windupClipLength = 0.4f;
        [Tooltip("Clip time the wind-up has reached when the telegraph ends.")]
        public float windupTo = 0.4f;
        [Tooltip("During the wind-up the body leans back this far and snaps forward into the shot (0 = none).")]
        public float windupLean = 0f;

        [Tooltip("The firing clip. Empty = old behaviour (the projectile leaves when the attack starts).")]
        public string clip = "";
        public float clipLength = 0.3f;
        [Tooltip("Clip time at which the projectile leaves: the release / recoil frame.")]
        public float releaseTime = 0.1f;
        [Tooltip("Seconds of the clip shown before the release (the frames leading up to it).")]
        public float leadIn = 0.05f;
        [Tooltip("Playback speed of the firing clip.")]
        public float speed = 1f;

        public bool Timed => !string.IsNullOrEmpty(clip);
        /// <summary>Clip time the firing animation starts from.</summary>
        public float StartTime => Mathf.Max(0f, releaseTime - leadIn);
        /// <summary>Seconds from the start of a shot's animation to the release (real time).</summary>
        public float ReleaseDelay => (releaseTime - StartTime) / Mathf.Max(0.1f, speed);
        /// <summary>Seconds one shot's animation takes from its start to the clip's end.</summary>
        public float Span => (clipLength - StartTime) / Mathf.Max(0.1f, speed);
    }

    /// <summary>
    /// All the numbers and animation names for one kind of enemy (one asset per kind, built by Hollowknight > Build Enemy Test Arena).
    /// Every value here is our own design: the original keeps enemy numbers in per-enemy asset data.
    /// </summary>
    [CreateAssetMenu(menuName = "Hollowknight/Enemy Config", fileName = "EnemyConfig")]
    public class EnemyConfig : ScriptableObject
    {
        public EnemyKind kind;

        [Header("Health")]
        public int maxHealth = 4;
        [Tooltip("Knock-back when hit by the nail: x is away from the hero, y is a small hop.")]
        public Vector2 hitKnockback = new Vector2(5f, 3f);
        [Tooltip("Seconds an enemy is staggered after a hit that does not kill it (an attack in progress is cancelled).")]
        public float hurtTime = 0.3f;

        [Header("Movement")]
        public float walkSpeed = 2.2f;
        public float chaseSpeed = 4.5f;
        public float acceleration = 40f;
        public float gravityScale = 4f;

        [Header("Senses")]
        public float sightRange = 8f;
        [Tooltip("How far above / below its centre the hero can be and still be noticed.")]
        public float sightHeight = 3.5f;
        [Tooltip("Seconds it keeps hunting after losing sight of the hero.")]
        public float loseSightTime = 2.5f;
        public float patrolRange = 3f;
        public float patrolPause = 1f;
        [Tooltip("Off: patrolling stops at ledges. (When chasing, it still steps down after a hero that is below it.)")]
        public bool canDropLedges = false;

        [Header("Spacing")]
        [Tooltip("Ranged: backs away when the hero is closer than this, approaches when farther than the max, holds and shoots in between.")]
        public float rangedMin = 4f;
        public float rangedMax = 8f;

        [Header("Attack")]
        public int damage = 1;
        [Tooltip("Wind-up before the attack; the enemy stands still and shows a '!' so the hero can react.")]
        public float telegraphTime = 0.5f;
        [Tooltip("Ranged: how long the attack itself lasts, including the whole burst. (Melee uses each move's own timing.)")]
        public float activeTime = 0.2f;
        public float recoverTime = 0.8f;
        [Tooltip("Extra pause after recovering before it may attack again.")]
        public float cooldown = 0.4f;
        [Tooltip("Melee: the blows it throws, in turn (the brawler: a punch, then a kick).")]
        public MeleeMove[] moves = new MeleeMove[0];

        [Header("Ranged")]
        [Tooltip("Animation timing of a shot: which frame the projectile leaves on.")]
        public ShotMove shot = new ShotMove();
        public float projectileSpeed = 14f;
        public float projectileGravity = 0f;
        public int burstCount = 1;
        public float burstGap = 0.14f;
        [Tooltip("Where shots leave, relative to the enemy's centre (x mirrored with facing).")]
        public Vector2 muzzleOffset = new Vector2(0.9f, 0.15f);
        [Tooltip("Blades fly out this far and come back (0 = a plain shot that is gone when it lands).")]
        public float boomerangDistance = 0f;

        [Header("Jump (chasing)")]
        public bool canJump = true;
        public float jumpCooldown = 0.9f;
        [Tooltip("Highest obstacle / platform edge it will try to jump onto.")]
        public float maxJumpHeight = 3.2f;
        public float jumpForwardSpeed = 5.5f;

        [Header("Contact")]
        [Tooltip("Touching the enemy's body hurts the hero.")]
        public bool contactDamage = false;

        [Header("Colours (for hit sparks, death bursts and the '!')")]
        public Color bodyMid = new Color(0.5f, 0.5f, 0.5f);
        public Color bodyLight = new Color(0.9f, 0.9f, 0.9f);
        public Color accent = new Color(1f, 0.6f, 0.2f);

        [Header("Animation clip names (the generated animator has one state per clip)")]
        public string idleClip = "Idle";
        public string walkClip = "Walk";
        public string runClip = "Run";
        [Tooltip("Played while aware of the hero but standing still.")]
        public string aggroIdleClip = "Idle";
        public string jumpRiseClip = "JumpRise", jumpMidClip = "JumpMid", jumpFallClip = "JumpFall";
        public string hurtClip = "HitDamage";
        public string dieClip = "Die";
        public string telegraphClip = "Idle";
        public string attackClip = "Idle";
        public string recoverClip = "Idle";
    }
}
