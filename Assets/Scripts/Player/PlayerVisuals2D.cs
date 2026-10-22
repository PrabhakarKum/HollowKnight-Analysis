using UnityEngine;

namespace Hollowknight.Player
{
    /// <summary>
    /// Picks an animation state on the child Animator from what PlayerController2D is doing.
    /// State names match the DeadRevolver PlayerAnimator controller; change the serialized names to use another set.
    /// </summary>
    public class PlayerVisuals2D : MonoBehaviour
    {
        [SerializeField] PlayerController2D controller;
        [SerializeField] Animator animator;

        [Header("State names in the Animator")]
        [SerializeField] string idle = "Idle";
        [SerializeField] string run = "Run";
        [SerializeField] string rise = "JumpRise";
        [SerializeField] string mid = "JumpMid";
        [SerializeField] string fall = "JumpFall";
        [SerializeField] string land = "Land";
        [SerializeField] string hardLand = "Crouch";
        [SerializeField] string bind = "Crouch";
        [SerializeField] string dash = "DashLoop";
        [SerializeField] string sprint = "DashLoop";
        [SerializeField] string sprintSkid = "DashEnd";
        [SerializeField] string wallSlide = "WallSlide";
        [SerializeField] string wallJump = "WallJump";
        [SerializeField] string airJump = "FrontFlip";
        [SerializeField] string groundSlashA = "SwordStandingSlash";
        [SerializeField] string groundSlashB = "ComboAttackB";
        [SerializeField] string airSlash = "AirSlash";
        [SerializeField] string upSlash = "AirSlashUp";
        [SerializeField] string downSlash = "AirSlashDown";
        [SerializeField] string hurt = "HitDamage";
        [SerializeField] string die = "Die";

        [Header("Wall slide fit")]
        [Tooltip("The wall-slide art is narrower than the collider and its width changes per frame. While sliding, the visual is moved each frame so the art's edge nearest the wall touches the collider edge (plus this small overlap).")]
        [SerializeField] float wallTouchOverlap = 0.015f;   // about the physics contact skin, so the art meets the wall surface

        [Header("Baked opaque-pixel extents (Hollowknight > Bake Sprite Edges)")]
        [Tooltip("The sprite mesh is looser than the visible pixels, so the exact left/right edge of the opaque art is baked per sprite (local units, unflipped, relative to the pivot).")]
        [SerializeField] string[] edgeSpriteNames = new string[0];
        [SerializeField] float[] edgeMinX = new float[0];
        [SerializeField] float[] edgeMaxX = new float[0];

        System.Collections.Generic.Dictionary<string, Vector2> edges;

        [Header("Timing")]
        [SerializeField] float crossFade = 0.04f;
        [SerializeField] float landTime = 0.12f;
        [SerializeField] float oneShotTime = 0.25f;
        [SerializeField] float runThreshold = 0.5f;
        [SerializeField] float apexSpeed = 2f;

        string current;
        float lockUntil;        // a one-shot animation (land, flip, wall jump, slash) is playing until this time
        bool lockIsAttack;
        float lockStartTime;
        PlayerCombat2D combat;

        void Awake()
        {
            if (controller == null) controller = GetComponentInParent<PlayerController2D>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            combat = GetComponentInParent<PlayerCombat2D>();

            edges =new System.Collections.Generic.Dictionary<string, Vector2>();
            int n = Mathf.Min(edgeSpriteNames.Length, Mathf.Min(edgeMinX.Length, edgeMaxX.Length));
            for (int i = 0; i < n; i++) edges[edgeSpriteNames[i]] = new Vector2(edgeMinX[i], edgeMaxX[i]);
        }

        /// <summary>Left/right extent of the sprite's opaque art in local units (unflipped). Falls back to the mesh.</summary>
        public Vector2 ArtExtentX(Sprite s)
        {
            if (edges != null && edges.TryGetValue(s.name, out var e)) return e;
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (var v in s.vertices) { min = Mathf.Min(min, v.x); max = Mathf.Max(max, v.x); }
            return new Vector2(min, max);
        }

        void OnEnable()
        {
            if (controller == null) return;
            controller.Landed += OnLanded;
            controller.SprintEnded += OnSprintEnded;
            controller.AirJumped += OnAirJump;
            controller.WallJumped += OnWallJump;
            if (combat != null) combat.AttackStarted += OnAttackStarted;
        }

        void OnDisable()
        {
            if (controller == null) return;
            controller.Landed -= OnLanded;
            controller.SprintEnded -= OnSprintEnded;
            controller.AirJumped -= OnAirJump;
            controller.WallJumped -= OnWallJump;
            if (combat != null) combat.AttackStarted -= OnAttackStarted;
        }

        void OnLanded() => OneShot(land, landTime);
        void OnSprintEnded(bool skid) { if (skid) OneShot(sprintSkid, 0.18f); }
        void OnAirJump() => OneShot(airJump, oneShotTime);
        void OnWallJump() => OneShot(wallJump, oneShotTime);

        void OnAttackStarted(AttackDir dir, int combo, bool grounded)
        {
            string state = dir == AttackDir.Up ? upSlash
                         : dir == AttackDir.Down ? downSlash
                         : grounded ? (combo == 0 ? groundSlashA : groundSlashB)
                         : airSlash;
            OneShot(state, controller.Tuning.attackDuration, true);
        }

        void OneShot(string state, float duration, bool isAttack = false)
        {
            Play(state, true);
            lockStartTime = Time.time;
            lockUntil = Time.time + duration;
            lockIsAttack = isAttack;
        }

        void Update()
        {
            if (controller == null || animator == null) return;


            // dashes, wall slides and leaving the ground interrupt a one-shot
            // A wall jump (or any one-shot) starts inside a physics step that may still report "wall sliding" for one more
            // frame; that stale flag must not cancel the one-shot. Only a slide that begins later (a chain wall jump) does.
            bool slideInterrupts = controller.IsWallSliding && !lockIsAttack && Time.time - lockStartTime > 0.1f;
            bool interrupt = controller.IsDashing || controller.IsHardLanding || slideInterrupts || controller.IsDamaged || controller.IsDead || controller.IsBinding;
            if (Time.time < lockUntil && !interrupt)
            {
                // landing is cut short by running off; flips and wall jumps play out
                if (current == land && Mathf.Abs(controller.Velocity.x) > runThreshold) lockUntil = 0f;
                else return;
            }

            string target;
            var v = controller.Velocity;
            switch (controller.State)
            {
                case MotionState.Dead: target = die; break;
                case MotionState.Damaged: target = hurt; break;
                case MotionState.Binding: target = bind; break;
                case MotionState.HardLanding: target = hardLand; break;
                case MotionState.Dashing: target = dash; break;
                case MotionState.Sprinting: target = sprint; break;
                case MotionState.WallSliding: target = wallSlide; break;
                case MotionState.Grounded: target = Mathf.Abs(v.x) > runThreshold ? run : idle; break;
                case MotionState.Rising: target = Mathf.Abs(v.y) < apexSpeed ? mid : rise; break;
                case MotionState.Floating: target = mid; break;
                default: target = Mathf.Abs(v.y) < apexSpeed ? mid : fall; break;
            }
            Play(target, false);
        }

        // Runs after the Animator has chosen this frame's sprite.
        void LateUpdate()
        {
            if (controller == null || animator == null) return;
            var vt = animator.transform;
            var lp = vt.localPosition;
            float targetX = 0f;

            bool slashing = combat != null && combat.IsAttacking;
            if (controller.State == MotionState.WallSliding && controller.WallDirection != 0 && !slashing)
            {
                var sr = animator.GetComponent<SpriteRenderer>();
                var box = controller.GetComponent<Collider2D>();
                if (sr != null && sr.sprite != null && box != null)
                {
                    int d = controller.WallDirection;
                    // how far the opaque art reaches toward the wall (local x, mirrored when flipped), from the pivot
                    var ext = ArtExtentX(sr.sprite);
                    float lo = sr.flipX ? -ext.y : ext.x;     // leftmost / rightmost art edge after flipping
                    float hi = sr.flipX ? -ext.x : ext.y;
                    float reach = d > 0 ? hi : -lo;
                    float halfWidth = box.bounds.extents.x;                 // collider half width, world units
                    targetX = d * (halfWidth + wallTouchOverlap - reach);   // put the art's wall-side edge on the collider edge
                }
            }

            // set directly every frame: the pack's Animator controller resets the visual's position each frame
            // (write defaults), so any speed-limited smoothing here would make the offset depend on frame time
            lp.x = targetX;
            vt.localPosition = lp;
        }

        void Play(string state, bool restart)
        {
            if (string.IsNullOrEmpty(state)) return;
            if (!restart && state == current) return;
            current = state;
            animator.CrossFadeInFixedTime(state, crossFade, 0, 0f);
        }
    }
}
