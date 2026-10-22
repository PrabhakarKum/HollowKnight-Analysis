using UnityEngine;
using Hollowknight.Feel;
using Hollowknight.Player;

namespace Hollowknight.CameraSystem
{
    /// <summary>
    /// Platformer camera: facing look-ahead, separate X/Y damping, grounded-only vertical follow with a
    /// dead zone, fall/rise offsets, and lock zones that clamp the view.
    /// Put this on the camera. Runs in LateUpdate.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow2D : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] PlayerController2D target;
        [SerializeField] Vector2 baseOffset = new Vector2(0f, 1.2f);

        [Header("Look-ahead (horizontal)")]
        [Tooltip("How far ahead of the facing direction the camera leads (world units).")]
        [SerializeField] float lookAheadDistance = 3f;
        [Tooltip("The look-ahead offset moves toward the facing side at this constant speed (the original uses 6 units/s) - a straight-line ramp, not an ease.")]
        [SerializeField] float lookAheadRampSpeed = 6f;
        [Tooltip("The original follows the facing direction even when standing still. Turn on to freeze the look-ahead while the hero is not moving.")]
        [SerializeField] bool lookAheadOnlyWhenMoving = false;
        [Tooltip("Extra offset added in the dash direction while dashing fast (|vx| > 5). It switches on/off at once; the damping below smooths it.")]
        [SerializeField] float dashLookAheadBonus = 1.5f;
        [Tooltip("Extra offset in the facing direction while sprinting (the original has a separate sprint look-ahead; its value is asset data, so this one is our guess).")]
        [SerializeField] float sprintLookAheadBonus = 2.2f;

        [Header("Damping")]
        [SerializeField] float dampTimeX = 0.07f;
        [SerializeField] float dampTimeY = 0.18f;
        [SerializeField] float dampTimeYRising = 0.25f;
        [SerializeField] float dampTimeYFalling = 0.05f;
        [SerializeField] float maxSpeedY = 60f;

        [Header("Vertical follow")]
        [Tooltip("While airborne, the camera ignores hero height changes inside this band (jumps don't bob the view).")]
        [SerializeField] float airVerticalDeadZone = 2.5f;
        [Tooltip("Dead zone below the anchor while airborne. Keep small so falling drags the view down and the fall offset can show what is below.")]
        [SerializeField] float airVerticalDeadZoneDown = 0.5f;

        [Header("Look down when falling fast / floating (the original ramps a normalized offset)")]
        [Tooltip("World units that a normalized offset of -1 is worth.")]
        [SerializeField] float fallOffsetUnit = 2f;
        [Tooltip("Only at near-terminal fall speed does the camera start to look down.")]
        [SerializeField] float fallOffsetStartSpeed = 17f;
        [Tooltip("The normalized offset moves down at this rate per second while falling fast (the original uses 4), down to -1.")]
        [SerializeField] float fallOffsetRampPerSecond = 4f;
        [Tooltip("After floating for this long, the camera starts looking further down (the original: 1.25 s).")]
        [SerializeField] float floatLookDownDelay = 1.25f;
        [SerializeField] float floatLookDownRampPerSecond = 1f;
        [SerializeField] float floatLookDownMax = 1.5f;

        [Header("Start")]
        [SerializeField] bool snapToTargetOnStart = true;

        Camera cam;
        float lookAheadX, dashOffsetX;
        float fallNorm, floatTimer;   // normalized look-down offset (0 .. -1, or -1.5 while floating long) and time spent floating
        float focusY;                 // vertical anchor that the camera follows
        float velX, velY;
        CameraLockZone currentZone;
        CameraShake2D shake;
        Vector3 basePos;              // where the follow logic wants the camera; the shake is added on top and never fed back
        Vector3 lastWritten;          // the position (with shake) this script wrote last, to notice outside moves

        public Transform Target => target != null ? target.transform : null;
        public float LookAheadX => lookAheadX;
        public float DashOffsetX => dashOffsetX;
        public float FallOffsetNorm => fallNorm;
        /// <summary>The camera position without the screen shake.</summary>
        public Vector3 BasePosition => basePos;
        /// <summary>What the screen shake added to the base position on the last frame.</summary>
        public Vector2 ShakeOffset { get; private set; }

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (target == null) target = FindFirstObjectByType<PlayerController2D>();
            shake = GetComponent<CameraShake2D>();
            if (shake == null) shake = gameObject.AddComponent<CameraShake2D>();
            basePos = lastWritten = transform.position;
        }

        void Start()
        {
            if (target != null && snapToTargetOnStart) SnapToTarget();
        }

        public void SetTarget(PlayerController2D newTarget, bool snap = true)
        {
            target = newTarget;
            if (snap && target != null) SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            focusY = target.transform.position.y;
            lookAheadX = 0f; dashOffsetX = 0f; fallNorm = 0f; floatTimer = 0f; velX = velY = 0f;
            var p = Clamp(new Vector2(target.transform.position.x + baseOffset.x, focusY + baseOffset.y));
            basePos = lastWritten = new Vector3(p.x, p.y, transform.position.z);
            transform.position = basePos;
            ShakeOffset = Vector2.zero;
        }

        public void EnterZone(CameraLockZone zone) { currentZone = zone; }
        public void ExitZone(CameraLockZone zone) { if (currentZone == zone) currentZone = null; }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            Vector2 heroPos = target.transform.position;
            Vector2 heroVel = target.Velocity;

            // --- look-ahead -----------------------------------------------------------
            // a world-unit offset that slides toward the facing side at a constant speed (not tied to movement);
            // a dash adds its own offset on top
            float desiredLook = target.FacingDirection * lookAheadDistance;
            if (lookAheadOnlyWhenMoving && Mathf.Abs(heroVel.x) < 0.5f) desiredLook = lookAheadX;
            lookAheadX = Mathf.MoveTowards(lookAheadX, desiredLook, lookAheadRampSpeed * dt);
            if (target.State == MotionState.Dashing && Mathf.Abs(heroVel.x) > 5f) dashOffsetX = target.FacingDirection * dashLookAheadBonus;
            else if (target.IsSprinting) dashOffsetX = target.FacingDirection * sprintLookAheadBonus;   // a sprint has its own look-ahead
            else dashOffsetX = 0f;

            // --- vertical anchor: follow ground height, ignore small jumps ----------------
            if (target.IsGrounded)
            {
                focusY = heroPos.y;
            }
            else
            {
                float diff = heroPos.y - focusY;
                if (diff > airVerticalDeadZone) focusY = heroPos.y - airVerticalDeadZone;
                else if (diff < -airVerticalDeadZoneDown) focusY = heroPos.y + airVerticalDeadZoneDown;
            }

            // --- look down: ramps while falling at near-terminal speed, and slowly after a long float ----
            // (outside those states the offset snaps back to 0; the damping below smooths what you see)
            if (target.State == MotionState.Falling && heroVel.y < -fallOffsetStartSpeed)
            {
                fallNorm = Mathf.Max(-1f, fallNorm - fallOffsetRampPerSecond * dt);
                floatTimer = 0f;
            }
            else if (target.State == MotionState.Floating)
            {
                floatTimer += dt;
                fallNorm = floatTimer > floatLookDownDelay ? Mathf.Max(-floatLookDownMax, fallNorm - floatLookDownRampPerSecond * dt) : 0f;
            }
            else
            {
                fallNorm = 0f;
                floatTimer = 0f;
            }

            // --- destination + damped move -------------------------------------------------
            var dest = new Vector2(heroPos.x + baseOffset.x + lookAheadX + dashOffsetX, focusY + baseOffset.y + fallNorm * fallOffsetUnit);
            dest = Clamp(dest);

            float yTime = target.State == MotionState.Rising ? dampTimeYRising
                        : target.State == MotionState.Falling ? dampTimeYFalling : dampTimeY;

            // someone moved the camera from outside (a teleport, an editor tool): take that as the new base
            if ((transform.position - lastWritten).sqrMagnitude > 1e-8f) basePos = transform.position;

            Vector3 pos = basePos;
            pos.x = Mathf.SmoothDamp(pos.x, dest.x, ref velX, dampTimeX);
            pos.y = Mathf.SmoothDamp(pos.y, dest.y, ref velY, yTime, maxSpeedY, dt);
            var clamped = Clamp(new Vector2(pos.x, pos.y));
            pos.x = clamped.x; pos.y = clamped.y;
            basePos = pos;

            // screen shake rides on top of the follow position (the original adds it at render time and takes it off again)
            ShakeOffset = shake != null ? shake.Evaluate(dt) : Vector2.zero;
            lastWritten = new Vector3(pos.x + ShakeOffset.x, pos.y + ShakeOffset.y, pos.z);
            transform.position = lastWritten;
        }

        /// <summary>Clamp a camera center so the view stays inside the active lock zone.</summary>
        Vector2 Clamp(Vector2 p)
        {
            if (currentZone == null || cam == null) return p;
            Bounds b = currentZone.Bounds;
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;

            p.x = b.size.x <= halfW * 2f ? b.center.x : Mathf.Clamp(p.x, b.min.x + halfW, b.max.x - halfW);
            p.y = b.size.y <= halfH * 2f ? b.center.y : Mathf.Clamp(p.y, b.min.y + halfH, b.max.y - halfH);
            return p;
        }
    }
}
