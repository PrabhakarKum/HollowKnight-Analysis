using System;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // What the camera needs to know about the thing it follows. HeroController is wrapped by HeroCameraSubject;
    // tests use a fake so the camera can be stepped without physics.
    public interface ICameraSubject
    {
        bool FacingRight { get; }
        HeroState State { get; }
        bool IsGrounded { get; }
        Vector2 Velocity { get; }
        bool WantsUp { get; }
        bool WantsDown { get; }
    }

    public sealed class HeroCameraSubject : ICameraSubject
    {
        readonly HeroController hero;
        public HeroCameraSubject(HeroController hero) { this.hero = hero; }
        public bool FacingRight => hero.FacingRight;
        public HeroState State => hero.State;
        public bool IsGrounded => hero.IsGrounded;
        public Vector2 Velocity => hero.Velocity;
        public bool WantsUp => hero.WantsUp;
        public bool WantsDown => hero.WantsDown;
    }

    // Camera values. They are camera-only, so they live here and NOT in HeroConfig (which holds hero values only).
    // Defaults are the original's, read from _GameCameras.prefab and CameraTarget / CameraController (see
    // docs/study/08-camera.md for the table with the prototype's numbers beside them). Times are seconds and
    // speeds are units per second, so nothing depends on the frame rate.
    [Serializable]
    public class GameCameraSettings
    {
        [Header("Look-ahead (CameraTarget)")]
        public float XLookAhead = 1f;              // prefab xLookAhead
        public float LookAheadRampSpeed = 6f;      // literal 6 in CameraTarget.Update
        public float OffsetHardLimit = 1f;         // the offset is clamped to +-1 in code
        public float DashLookAhead = 1f;           // prefab dashLookAhead
        public float DashMinSpeed = 5f;            // |vx| above this counts as dashing for the look-ahead

        [Header("Smoothing")]
        public float TargetDampTime = 0.2f;        // prefab dampTimeNormal (the follow target's damp)
        public float CameraDampTime = 0.075f;      // prefab dampTime / dampTimeX / dampTimeY (the camera's damp)

        [Header("Falling look-down")]
        public float FallStartSpeed = 22f;         // falling faster than this starts the look-down
        public float FallOffsetRampPerSecond = 4f; // literal 4
        public float FallOffsetMax = 1f;           // offset floors at -1 unit
        public float FallDampShrinkPerSecond = 0.1f; // dampTimeFalling -= dt / 10
        public float FallDampMin = 0.03f;

        [Header("Look up / down (CameraController)")]
        public float LookDelaySeconds = 0.85f;     // HeroController.LOOK_DELAY
        public float LookOffset = 6f;              // literal 6
        public float LookSlowDampTime = 0.15f;     // literal 0.15 for vertical smoothing while looking
        public float LookSlowSeconds = 0.35f;      // prefab lookSlowTime

        [Header("Room edge (CameraController.KeepWithinSceneBounds)")]
        public float EdgeInsetX = 14.6f;
        public float EdgeInsetY = 8.3f;
    }

    // The follow camera. Two stages like the original: a follow target that leads the hero (look-ahead, dash lead,
    // falling look-down) and eases toward that point, then the camera that eases toward the target and is kept
    // inside the room. Runs in LateUpdate with Time.deltaTime, or call Step(dt) yourself with ManualStepping on so
    // tests advance it deterministically at any rate. URP only: no built-in image effects are used or required.
    [RequireComponent(typeof(Camera))]
    public class GameCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] RoomBounds bounds;
        public GameCameraSettings Settings = new GameCameraSettings();

        public bool ManualStepping { get; set; }
        public RoomBounds Bounds { get => bounds; set => bounds = value; }
        public ICameraSubject Subject { get; set; }

        // Setting the target re-reads its hero (if any) and snaps to it on the next step.
        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                var hero = value != null ? value.GetComponent<HeroController>() : null;
                Subject = hero != null ? new HeroCameraSubject(hero) : null;
                initialised = false;
            }
        }

        // Read-outs for tests and the debug HUD.
        public Vector2 FollowTargetPosition => followPos;
        public float LookAheadOffset => xOffset;
        public float DashOffset => dashOffset;
        public float FallOffset => fallOffset;
        public float LookOffsetY => lookOffset;

        bool initialised;
        Vector2 followPos, followVel, camVel, heroPrev;
        float xOffset, dashOffset, fallOffset, lookOffset;
        float fallDamp, lookTimer, lookSlowTimer;
        bool isFalling;
        Camera cam;

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (target != null && Subject == null) Target = target;
            ReportUnbuiltRules();
        }

        // Rules the original camera has that are not built yet. One-shot, so calling this is cheap.
        static void ReportUnbuiltRules()
        {
            Stubs.NotImplemented("GameCamera: sprint, wall-sprint, slide, harpoon, updraft, super-jump and rising look-ahead/offsets");
            Stubs.NotImplemented("GameCamera: umbrella-float look-down");
            Stubs.NotImplemented("GameCamera: lock areas (CameraLockArea) and camera offset areas");
            Stubs.NotImplemented("GameCamera: detach / short-damp after hits and respawns, scene-transition positioning");
            Stubs.NotImplemented("GameCamera: screen shake and post effects (bloom, colour correction, noise) as URP volume");
        }

        void LateUpdate()
        {
            if (ManualStepping || Time.timeScale <= Mathf.Epsilon) return;
            Step(Time.deltaTime);
        }

        // Puts the camera on the target at once, with the look-ahead already facing the way the hero faces
        // (CameraTarget.PositionToStart). Also what the first step does.
        public void SnapToTarget()
        {
            if (target == null) return;
            if (cam == null) cam = GetComponent<Camera>();
            var s = Settings;
            Vector2 hero = target.position;
            xOffset = Mathf.Clamp(FacingRight ? 1f : -1f, -s.XLookAhead, s.XLookAhead);
            dashOffset = fallOffset = lookOffset = 0f;
            fallDamp = s.CameraDampTime;
            lookTimer = lookSlowTimer = 0f;
            isFalling = false;
            followVel = camVel = Vector2.zero;
            followPos = KeepInside(new Vector2(hero.x + xOffset, hero.y));
            heroPrev = hero;
            Place(followPos);
            initialised = true;
        }

        bool FacingRight => Subject == null || Subject.FacingRight;

        public void Step(float dt)
        {
            if (target == null || dt <= 0f) return;
            if (!initialised) { SnapToTarget(); return; }
            var s = Settings;
            Vector2 hero = target.position;
            bool facingRight = FacingRight;
            HeroState state = Subject != null ? Subject.State : HeroState.Grounded;
            Vector2 vel = Subject != null ? Subject.Velocity : Vector2.zero;

            // ---- stage 1: where the follow target wants to be ----
            if (facingRight) { if (xOffset < s.XLookAhead) xOffset += dt * s.LookAheadRampSpeed; }
            else if (xOffset > -s.XLookAhead) xOffset -= dt * s.LookAheadRampSpeed;
            xOffset = Mathf.Clamp(xOffset, -s.OffsetHardLimit, s.OffsetHardLimit);

            dashOffset = state == HeroState.Dashing && Mathf.Abs(vel.x) > s.DashMinSpeed
                ? (facingRight ? s.DashLookAhead : -s.DashLookAhead)
                : 0f;

            if (state == HeroState.Falling && vel.y < -s.FallStartSpeed)
            {
                isFalling = true;
                fallOffset = Mathf.Max(-s.FallOffsetMax, fallOffset - s.FallOffsetRampPerSecond * dt);
            }
            else
            {
                isFalling = false;
                fallOffset = 0f;
            }

            Vector2 dest = new Vector2(hero.x + xOffset + dashOffset, hero.y + fallOffset);

            // Catch-up: while the hero moves toward the destination faster than the target eases, the target is
            // carried along by the hero's own movement, never past the destination.
            Vector2 d = hero - heroPrev;
            if (d.x < -0.001f && followPos.x > dest.x) followPos.x = Mathf.Max(dest.x, followPos.x + d.x);
            if (d.x > 0.001f && followPos.x < dest.x) followPos.x = Mathf.Min(dest.x, followPos.x + d.x);
            if (d.y < -0.001f && followPos.y > dest.y) followPos.y = Mathf.Max(dest.y, followPos.y + d.y);
            if (d.y > 0.001f && followPos.y < dest.y) followPos.y = Mathf.Min(dest.y, followPos.y + d.y);
            heroPrev = hero;

            followPos.x = Smooth(followPos.x, dest.x, ref followVel.x, s.TargetDampTime, dt);
            followPos.y = Smooth(followPos.y, dest.y, ref followVel.y, s.TargetDampTime, dt);
            followPos = KeepInside(followPos);
            // Keep the look-ahead from pushing the target past the room edge and hiding there.
            if (bounds != null)
            {
                Vector2 inset = Inset();
                Rect area = bounds.Area;
                if (area.width > 2f * inset.x)
                {
                    if (followPos.x + xOffset < area.xMin + inset.x) xOffset = area.xMin + inset.x - followPos.x - 0.001f;
                    if (followPos.x + xOffset > area.xMax - inset.x) xOffset = area.xMax - inset.x - followPos.x + 0.001f;
                }
            }

            // ---- look up / down: standing still on the ground, holding up or down ----
            bool standing = state == HeroState.Grounded && (Subject == null || Subject.IsGrounded) && Mathf.Abs(vel.x) < 0.01f;
            bool wantsUp = Subject != null && Subject.WantsUp && !Subject.WantsDown;
            bool wantsDown = Subject != null && Subject.WantsDown && !Subject.WantsUp;
            if (standing && (wantsUp || wantsDown)) lookTimer += dt; else lookTimer = 0f;
            if (lookTimer >= s.LookDelaySeconds)
            {
                lookSlowTimer = s.LookSlowSeconds;
                lookOffset = hero.y - followPos.y + (wantsUp ? s.LookOffset : -s.LookOffset);
            }
            else lookOffset = 0f;

            // ---- stage 2: the camera eases toward the target, inside the room ----
            Vector2 camDest = KeepInside(new Vector2(followPos.x, followPos.y + lookOffset));
            Vector2 pos = transform.position;
            pos.x = Smooth(pos.x, camDest.x, ref camVel.x, s.CameraDampTime, dt);

            float yTime;
            if (lookSlowTimer > 0f)
            {
                yTime = s.LookSlowDampTime;
                lookSlowTimer -= dt;
            }
            else if (isFalling)
            {
                fallDamp = Mathf.Max(s.FallDampMin, fallDamp - s.FallDampShrinkPerSecond * dt);
                yTime = fallDamp;
            }
            else
            {
                yTime = s.CameraDampTime;
                fallDamp = s.CameraDampTime;
            }
            pos.y = Smooth(pos.y, camDest.y, ref camVel.y, yTime, dt);
            Place(KeepInside(pos));
        }

        Vector2 Inset()
        {
            var s = Settings;
            float halfH = cam != null && cam.orthographic ? cam.orthographicSize : float.MaxValue;
            float halfW = cam != null && cam.orthographic ? halfH * cam.aspect : float.MaxValue;
            return new Vector2(Mathf.Min(s.EdgeInsetX, halfW), Mathf.Min(s.EdgeInsetY, halfH));
        }

        Vector2 KeepInside(Vector2 p) => bounds == null ? p : bounds.ClampCameraCenter(p, Inset());

        void Place(Vector2 p) => transform.position = new Vector3(p.x, p.y, transform.position.z);

        // Critically damped ease toward a target, in closed form. Same curve as Unity's SmoothDamp but exact, so
        // stepping at 50 or 100 Hz lands on the same position for a target that stays put.
        public static float Smooth(float current, float goal, ref float velocity, float smoothTime, float dt)
        {
            if (smoothTime < 0.0001f) { velocity = 0f; return goal; }
            float omega = 2f / smoothTime;
            float change = current - goal;
            float temp = velocity + omega * change;
            float decay = Mathf.Exp(-omega * dt);
            velocity = (velocity - omega * temp * dt) * decay;
            return goal + (change + temp * dt) * decay;
        }
    }
}
