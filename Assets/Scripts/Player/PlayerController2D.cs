using UnityEngine;
using UnityEngine.InputSystem;
using Hollowknight.Feel;

namespace Hollowknight.Player
{
    public enum MotionState { Grounded, Rising, Falling, Dashing, WallSliding, Floating, HardLanding, Damaged, Dead, Binding, Sprinting }

    /// <summary>
    /// Run + variable-height jump + coyote/buffering + custom gravity + dash (ground/air)
    /// + wall slide / wall jump. Fixed-step based.
    /// Requires a Rigidbody2D (gravityScale is forced to 0) and a Collider2D.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class PlayerController2D : MonoBehaviour
    {
        [SerializeField] PlayerTuning tuning;
        [SerializeField] SpriteRenderer spriteToFlip;

        public MotionState State { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsDashing => dashing;
        public bool IsWallSliding { get; private set; }
        public bool IsFloating { get; private set; }
        /// <summary>-1 wall on the left, +1 on the right, 0 none.</summary>
        public int WallDirection { get; private set; }
        public int FacingDirection { get; private set; } = 1;
        public Vector2 Velocity => rb.linearVelocity;

        public PlayerTuning Tuning => tuning;
        /// <summary>Held direction as -1/0/+1 on each axis (after the dead zone).</summary>
        public Vector2 MoveInput => new Vector2(moveX, moveY);
        public bool IsBouncing => bouncing;
        /// <summary>True while stuck in a hard landing; all input is ignored.</summary>
        public bool IsHardLanding => hardLanding;
        public bool InputLocked => hardLanding || damageRecoilSteps > 0 || dead || respawnHold || binding;
        /// <summary>True while the hero is binding (rooted sideways, input ignored; set by PlayerSilk2D).</summary>
        public bool IsBinding => binding;
        /// <summary>True during the knock-back after a hit (input ignored, gravity off).</summary>
        public bool IsDamaged => damageRecoilSteps > 0;
        public bool IsDead => dead;
        /// <summary>True while held still after being put back at a checkpoint / safe ground.</summary>
        public bool IsHeldForRespawn => respawnHold;
        public float FallTimer => fallTimer;
        public int AirJumpsLeft => airJumpsLeft;
        public int AirDashesLeft => airDashesLeft;
        public bool IsRecoiling => recoilStepsLeft > 0;
        /// <summary>Holding the dash button after a dash keeps the hero sprinting (a faster run).</summary>
        public bool IsSprinting => sprinting;
        public int SprintDirection => sprintDir;
        /// <summary>True during a sprint jump (the "shuttlecock"): forced forward speed until the fall begins.</summary>
        public bool IsShuttlecock => shuttle;
        public bool IsDashHeld => dashHeld;

        // one-off moments, for animation / audio / effects
        public event System.Action GroundJumped, AirJumped, WallJumped, Landed, DashStarted, HardLanded, SprintStarted, ShuttleJumped;
        /// <summary>true if the sprint ended on the ground at speed (a skid)</summary>
        public event System.Action<bool> SprintEnded;

        Rigidbody2D rb;
        Collider2D col;
        InputAction moveAction, jumpAction, dashAction;
        readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        ContactFilter2D groundFilter;

        // input state (written in Update, consumed in FixedUpdate)
        float moveX, moveY;
        bool jumpHeld;
        bool jumpPressedFlag, jumpReleasedFlag, dashPressedFlag;

        // jump timers
        int jumpQueueSteps;
        int jumpReleaseQueueSteps;
        int ledgeSteps;
        int jumpStepsDone;
        bool jumping;
        bool jumpedSinceGrounded;
        float riseSpeed;          // upward speed of the current rise (jump or double jump)
        int riseMaxSteps, riseMinSteps;
        int airJumpsLeft;
        int floatSteps;
        float gravityMul = 1f;    // air hang multiplier on gravity (1 = normal)

        // wall timers
        int wallSteps;            // wall coyote counter
        int lastWallDir;          // wall side remembered for wall coyote
        int wallLockSteps;        // horizontal input ignored after a wall jump
        int wallLockDir;          // direction of the wall-jump kick
        float wallKickSpeed, wallKickDecel;   // kick-off speed now, and its per-step decay toward run speed
        int recoilStepsLeft, recoilDir;       // knock-back after hitting something sideways
        float recoilSpeed;
        bool bouncing;                        // a down-slash pogo hop is in progress (cannot be cut short)
        bool floatPressValid;                 // the latest jump press may start a float (made while falling)
        float fallTimer;                      // seconds spent at terminal fall speed
        bool willHardLand;                    // fell long enough: the next landing is a hard one
        bool fallRumbling;                    // the long-fall camera rumble is running
        bool hardLanding;                     // currently stuck in a hard landing
        int hardLandStepsLeft;
        float wallSlashSlowUntil;             // wall slide speed is halved until this time (a forward slash on the wall)
        int wallSlideSide;        // -1 / +1 while stuck to a wall, 0 otherwise
        int wallUnstickSteps;     // steps held away from the wall while sliding
        int damageRecoilSteps;                // knock-back after being hurt: steps left, and the forced velocity
        Vector2 damageRecoilVel;
        bool dead, respawnHold, binding;

        // dash timers
        bool dashing;
        int dashStepsLeft;
        int dashDir;
        int dashCooldown;
        int dashQueueSteps;
        int airDashesLeft;

        // sprint (dash button held after a dash) and the sprint jump ("shuttlecock")
        bool dashHeld;
        bool sprinting;
        int sprintDir;
        int sprintBufferSteps;    // after leaving the ground while sprinting / dashing, a jump still counts as a sprint jump for this many steps
        bool shuttle;
        int shuttleDir;
        float carryX;             // speed left over after a sprint jump is cut off in the air; fades out, and turning around cancels it

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (tuning == null) tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            if (spriteToFlip == null) spriteToFlip = GetComponentInChildren<SpriteRenderer>();

            groundFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = tuning.groundMask };

            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");
            moveAction.AddBinding("<Gamepad>/dpad");

            jumpAction = new InputAction("Jump", InputActionType.Button);
            jumpAction.AddBinding("<Keyboard>/space");
            jumpAction.AddBinding("<Keyboard>/z");
            jumpAction.AddBinding("<Gamepad>/buttonSouth");

            dashAction = new InputAction("Dash", InputActionType.Button);
            dashAction.AddBinding("<Keyboard>/leftShift");
            dashAction.AddBinding("<Keyboard>/x");
            dashAction.AddBinding("<Gamepad>/buttonEast");      // West is the attack button
            dashAction.AddBinding("<Gamepad>/rightTrigger");
        }

        void OnEnable() { moveAction.Enable(); jumpAction.Enable(); dashAction.Enable(); }
        void OnDisable() { moveAction.Disable(); jumpAction.Disable(); dashAction.Disable(); }
        void OnDestroy() { moveAction?.Dispose(); jumpAction?.Dispose(); dashAction?.Dispose(); }

        void Update()
        {
            var move = moveAction.ReadValue<Vector2>();
            moveX = Mathf.Abs(move.x) < tuning.inputDeadzone ? 0f : Mathf.Sign(move.x);
            moveY = Mathf.Abs(move.y) < tuning.inputDeadzone ? 0f : Mathf.Sign(move.y);
            jumpHeld = jumpAction.IsPressed();
            if (jumpAction.WasPressedThisFrame()) jumpPressedFlag = true;
            if (jumpAction.WasReleasedThisFrame()) jumpReleasedFlag = true;
            if (dashAction.WasPressedThisFrame()) dashPressedFlag = true;
            dashHeld = dashAction.IsPressed();
        }

        void FixedUpdate()
        {
            ConsumeInputFlags();
            CheckGround();
            CheckWall();
            UpdateFallTimer();

            if (dead || respawnHold) { HandleLockedOut(); return; }
            if (damageRecoilSteps > 0) { HandleDamageRecoil(); return; }
            if (binding) { HandleBinding(); return; }

            if (hardLanding)
            {
                HandleHardLanding();
                return;
            }

            HandleDash();
            if (dashing)
            {
                UpdateState();
                return;
            }

            HandleSprint();
            HandleShuttle();
            HandleJump();
            UpdateFloat();
            LedgeStepUp();
            ApplyHorizontal();
            ApplyGravity();
            UpdateState();
            UpdateFacing();
        }

        // ------------------------------------------------------------------ input

        void ConsumeInputFlags()
        {
            if (jumpPressedFlag)
            {
                jumpQueueSteps = tuning.jumpBufferSteps;
                // was this press made while falling? (only such presses may start a float, unless the setting says otherwise)
                floatPressValid = tuning.floatFromPressDuringRise || (!jumping && rb.linearVelocity.y <= 0.01f);
                jumpPressedFlag = false;
            }
            else if (jumpQueueSteps > 0 && !dashing) jumpQueueSteps--; // a press made during a dash waits for it to end

            if (jumpReleasedFlag) { jumpReleaseQueueSteps = tuning.jumpReleaseBufferSteps; jumpReleasedFlag = false; }
            else if (jumpReleaseQueueSteps > 0) jumpReleaseQueueSteps--;

            if (dashPressedFlag) { dashQueueSteps = tuning.dashBufferSteps; dashPressedFlag = false; }
            else if (dashQueueSteps > 0) dashQueueSteps--;

            if (dashCooldown > 0) dashCooldown--;
            if (wallLockSteps > 0) wallLockSteps--;
            if (sprintBufferSteps > 0) sprintBufferSteps--;
        }

        // ------------------------------------------------------------------ sensing

        void CheckGround()
        {
            bool was = IsGrounded;
            IsGrounded = false;
            int n = col.Cast(Vector2.down, groundFilter, hits, tuning.groundCheckDistance);
            for (int i = 0; i < n; i++)
            {
                if (Vector2.Angle(hits[i].normal, Vector2.up) <= tuning.maxGroundAngle) { IsGrounded = true; break; }
            }

            // not grounded while moving up through a jump (or being knocked up by a hit)
            if (IsGrounded && (jumping || damageRecoilSteps > 0) && rb.linearVelocity.y > 0.01f) IsGrounded = false;

            if (IsGrounded)
            {
                ledgeSteps = tuning.coyoteSteps;
                airDashesLeft = tuning.airDashes;
                airJumpsLeft = tuning.airJumps;
                if (!was)
                {
                    if (willHardLand && tuning.hardLandingEnabled) StartHardLanding();
                    willHardLand = false;
                    fallTimer = 0f;
                }
                if (!was && !hardLanding && rb.linearVelocity.y <= 0.01f) Landed?.Invoke();
                // standing on the ground and not mid-rise: a ground jump is available again.
                // (Not tied to "was airborne": a jump that never left the ground, e.g. a head bump under a
                // low platform, must not lock out jumping.)
                if (!jumping) jumpedSinceGrounded = false;
            }
            else
            {
                if (ledgeSteps > 0) ledgeSteps--;
                // just ran off an edge while sprinting / dashing: a jump in the next few steps is still a sprint jump
                if (was && (sprinting || dashing)) sprintBufferSteps = tuning.coyoteSteps;
            }
        }

        // ------------------------------------------------------------------ hard landing

        /// <summary>Counts seconds spent at terminal fall speed; past bigFallTime the next landing is a hard one.</summary>
        void UpdateFallTimer()
        {
            if (IsGrounded || dead || respawnHold) { fallTimer = 0f; StopFallRumble(); return; }
            bool atTerminal = rb.linearVelocity.y <= -(tuning.maxFallSpeed - 0.05f) && !IsWallSliding && !IsFloating;
            fallTimer = atTerminal ? fallTimer + Time.fixedDeltaTime : 0f;
            if (fallTimer > tuning.bigFallTime) { willHardLand = true; StartFallRumble(); }
            else if (IsWallSliding || rb.linearVelocity.y > -1f) StopFallRumble();   // no longer falling: the rumble stops
        }

        /// <summary>The camera rumbles while a long fall goes on (the original starts "RumblingFall" the moment willHardLand is set).</summary>
        void StartFallRumble()
        {
            if (fallRumbling || !tuning.fallRumbleEnabled) return;
            fallRumbling = true;
            CameraShake2D.Do(ShakePreset.RumblingFall);
        }

        void StopFallRumble()
        {
            if (!fallRumbling) return;
            fallRumbling = false;
            CameraShake2D.Stop(ShakePreset.RumblingFall);
        }

        void StartHardLanding()
        {
            CameraShake2D.Do(tuning.hardLandShake, tuning.shakeFreezeFrames);
            hardLanding = true;
            hardLandStepsLeft = Mathf.Max(1, Mathf.RoundToInt(tuning.hardLandingTime / Time.fixedDeltaTime));
            jumping = false; bouncing = false; IsFloating = false; recoilStepsLeft = 0; dashing = false;
            if (sprinting) { sprinting = false; SprintEnded?.Invoke(false); }     // the original cancels the sprint on a hard landing
            shuttle = false; carryX = 0f;
            ClearInputBuffers();
            rb.linearVelocity = Vector2.zero;
            HardLanded?.Invoke();
        }

        void ClearInputBuffers()
        {
            jumpPressedFlag = jumpReleasedFlag = dashPressedFlag = false;
            jumpQueueSteps = jumpReleaseQueueSteps = dashQueueSteps = 0;
        }

        /// <summary>Stuck in place for hardLandingTime; presses made meanwhile are thrown away.</summary>
        void HandleHardLanding()
        {
            ClearInputBuffers();
            rb.linearVelocity = new Vector2(0f, Mathf.Min(rb.linearVelocity.y, 0f));
            if (--hardLandStepsLeft <= 0) hardLanding = false;
            UpdateState();
        }

        void CheckWall()
        {
            WallDirection = 0;
            if (!IsGrounded)
            {
                if (WallOnSide(Vector2.left)) WallDirection = -1;
                else if (WallOnSide(Vector2.right)) WallDirection = 1;
            }

            if (WallDirection != 0)
            {
                wallSteps = tuning.wallCoyoteSteps;
                lastWallDir = WallDirection;
            }
            else if (wallSteps > 0) wallSteps--;

            // Sticky wall slide: pressing into a wall while falling starts it; it then lasts until you hold AWAY
            // for wallStickySteps, stop touching the wall, land, or jump.
            if (wallSlideSide != 0)
            {
                int side = wallSlideSide;
                if (jumping)
                {
                    wallSlideSide = 0;                                  // a (wall) jump sets its own facing
                }
                else if (IsGrounded || WallDirection != side)
                {
                    // landed, or lost the wall: like the original, turn to face away from the wall
                    FaceDirection(-side);
                    wallSlideSide = 0;
                }
                else
                {
                    bool holdingAway = moveX != 0f && (int)moveX == -side;
                    wallUnstickSteps = holdingAway ? wallUnstickSteps + 1 : 0;
                    if (wallUnstickSteps >= tuning.wallStickySteps)
                    {
                        FaceDirection(-side);                           // let go of the wall: face away from it
                        wallSlideSide = 0;
                    }
                }
            }
            else if (WallDirection != 0 && Mathf.Approximately(moveX, WallDirection)
                     && !jumping && rb.linearVelocity.y <= 0.01f && !InputLocked)
            {
                wallSlideSide = WallDirection;
                wallUnstickSteps = 0;
            }
            IsWallSliding = wallSlideSide != 0;

            if (IsWallSliding)
            {
                airJumpsLeft = tuning.airJumps;
                if (tuning.wallResetsAirDash) airDashesLeft = tuning.airDashes;
            }
        }

        bool WallOnSide(Vector2 dir)
        {
            int n = col.Cast(dir, groundFilter, hits, tuning.wallCheckDistance);
            for (int i = 0; i < n; i++)
            {
                // vertical-ish surface only (not floor or ceiling)
                if (Mathf.Abs(hits[i].normal.x) > 0.9f) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ dash

        void HandleDash()
        {
            if (dashing)
            {
                dashStepsLeft--;
                bool hitWall = !IsGrounded && WallDirection != 0 && WallDirection == dashDir;
                if (hitWall)
                {
                    // running into a wall mid-dash ends the dash and puts you on the wall
                    EndDash();
                    wallSlideSide = WallDirection;
                    wallUnstickSteps = 0;
                    IsWallSliding = true;
                    airJumpsLeft = tuning.airJumps;
                    if (tuning.wallResetsAirDash) airDashesLeft = tuning.airDashes;
                    var vw = rb.linearVelocity; rb.linearVelocity = new Vector2(0f, Mathf.Min(vw.y, 0f));
                }
                else if (dashStepsLeft <= 0) EndDash(allowSprint: true);
                else
                {
                    rb.linearVelocity = new Vector2(dashDir * tuning.dashSpeed, 0f);
                    return;
                }
            }

            bool canDash = dashCooldown <= 0 && (IsGrounded || airDashesLeft > 0 || IsWallSliding);
            if (dashQueueSteps > 0 && canDash) StartDash();
        }

        void StartDash()
        {
            // away from a wall when sliding; otherwise input direction; otherwise facing
            if (IsWallSliding) dashDir = -WallDirection;
            else dashDir = moveX != 0f ? (int)moveX : FacingDirection;

            if (!IsGrounded) airDashesLeft--;

            sprinting = false; shuttle = false; carryX = 0f;
            dashing = true;
            IsFloating = false;
            wallSlideSide = 0;
            recoilStepsLeft = 0;
            bouncing = false;
            dashStepsLeft = IsGrounded ? tuning.dashSteps : tuning.airDashSteps;
            dashCooldown = tuning.dashCooldownSteps;   // the cooldown runs from the start of the dash
            dashQueueSteps = 0;
            jumping = false;
            wallLockSteps = 0;
            FaceDirection(dashDir);
            rb.linearVelocity = new Vector2(dashDir * tuning.dashSpeed, 0f);
            DashStarted?.Invoke();
        }

        void EndDash(bool allowSprint = false)
        {
            dashing = false;
            // carry momentum only in the held direction, otherwise settle to a stop
            float exit = moveX != 0f ? dashDir * tuning.runSpeed * tuning.dashExitSpeedFactor : 0f;
            rb.linearVelocity = new Vector2(exit, 0f);

            // like the original (FinishedDashing -> "TRY SPRINT"): a dash that runs its course while the dash button is
            // still held rolls into a sprint, on the ground or in the air
            if (allowSprint && dashHeld && (moveX == 0f || (int)moveX == dashDir) && CanSprint())
            {
                StartSprint(dashDir);
                rb.linearVelocity = new Vector2(dashDir * tuning.dashSpeed, 0f);   // eases down to sprint speed instead of snapping
            }
        }

        // ------------------------------------------------------------------ sprint + sprint jump

        /// <summary>Mirrors the original CanSprint: not mid-swing, not dashing, not locked out of control.</summary>
        bool CanSprint() => tuning.sprintEnabled && !dead && !respawnHold && !binding && damageRecoilSteps <= 0
                            && !hardLanding && !dashing && !AttackLocked;

        void StartSprint(int dir)
        {
            sprinting = true;
            sprintDir = dir;
            carryX = 0f;
            FaceDirection(dir);
            SprintStarted?.Invoke();
        }

        void EndSprint(bool skid)
        {
            if (!sprinting) return;
            sprinting = false;
            bool skidded = skid && IsGrounded && Mathf.Abs(rb.linearVelocity.x) > tuning.runSpeed + 1f;
            SprintEnded?.Invoke(skidded);
        }

        void HandleSprint()
        {
            if (!sprinting) return;
            if (!dashHeld || !tuning.sprintEnabled) { EndSprint(true); return; }                       // let go of dash: the sprint is over
            if (moveX != 0f && (int)moveX != sprintDir) { EndSprint(true); return; }                   // turning around ends it (a skid)
            if (IsWallSliding || AttackLocked) { EndSprint(false); return; }
            if (WallOnSide(sprintDir > 0 ? Vector2.right : Vector2.left)) { EndSprint(false); return; } // ran into a wall
            FaceDirection(sprintDir);
        }

        /// <summary>The sprint jump ends when the fall starts, a swing starts, a wall is touched, or the hero lands.</summary>
        void HandleShuttle()
        {
            if (!shuttle) return;
            if (IsGrounded && !jumping && rb.linearVelocity.y <= 0.01f)
            {
                // landed: keep sprinting if the dash button is still held (the original sends TRY SPRINT here)
                int dir = shuttleDir;
                shuttle = false;
                if (dashHeld && CanSprint()) StartSprint(dir);
                return;
            }
            bool wallAhead = WallDirection != 0 && WallDirection == shuttleDir;
            bool fallStarted = !jumping && rb.linearVelocity.y <= 0f;
            if (fallStarted || AttackLocked || IsWallSliding || wallAhead)
            {
                // when the fall begins with the dash button still held, the original sends TRY SPRINT: the sprint carries on in the air
                bool resume = fallStarted && !AttackLocked && !IsWallSliding && !wallAhead && dashHeld && CanSprint();
                int dir = shuttleDir;
                if (resume) { shuttle = false; StartSprint(dir); }
                else EndShuttle(true);
            }
        }

        /// <summary>Ends the sprint jump's forced speed; in the air the speed is kept as a fading carry (turning cancels it).</summary>
        void EndShuttle(bool keepCarry)
        {
            if (!shuttle) return;
            shuttle = false;
            if (keepCarry && !IsGrounded) carryX = shuttleDir * tuning.shuttlecockSpeed;
        }

        void StartShuttle()
        {
            shuttle = true;
            shuttleDir = sprinting ? sprintDir : FacingDirection;
            sprinting = false;
            sprintBufferSteps = 0;
            carryX = 0f;
            jumpStepsDone = -tuning.shuttlecockExtraSteps;       // starts "before" step 0, so the rise lasts that many steps longer
            FaceDirection(shuttleDir);
            ShuttleJumped?.Invoke();
        }

        // ------------------------------------------------------------------ jump

        void HandleJump()
        {
            bool canGroundJump = !jumpedSinceGrounded && (IsGrounded || ledgeSteps > 0);
            bool canWallJump = !canGroundJump && !IsGrounded && wallSteps > 0 && lastWallDir != 0;
            bool canAirJump = !canGroundJump && !canWallJump && !IsGrounded && airJumpsLeft > 0;
            if (canAirJump && jumpQueueSteps > 0 && !jumping && GroundCloseBelow())
                canAirJump = false; // keep the press buffered so it fires as a ground jump on landing

            if (jumpQueueSteps > 0 && !(canGroundJump || canWallJump || canAirJump) && CanStartFloat())
            {
                // like the original, floating starts from a fresh jump press (after the double jump is spent),
                // not from simply still holding the button
                jumpQueueSteps = 0;
                IsFloating = true;
                floatSteps = 0;
            }

            if (jumpQueueSteps > 0 && (canGroundJump || canWallJump || canAirJump))
            {
                jumping = true;
                IsFloating = false;
                bouncing = false;
                recoilStepsLeft = 0;
                jumpedSinceGrounded = true;
                jumpStepsDone = 0;
                jumpQueueSteps = 0;
                ledgeSteps = 0;
                jumpReleaseQueueSteps = 0;
                floatSteps = 0;
                dashCooldown = Mathf.Min(dashCooldown, tuning.jumpDashCooldownSteps);   // jumping almost clears the dash cooldown

                riseSpeed = tuning.jumpSpeed;
                riseMaxSteps = tuning.jumpSteps;
                riseMinSteps = tuning.jumpStepsMin;

                if (!canWallJump && !canAirJump && tuning.sprintEnabled && (sprinting || sprintBufferSteps > 0))
                    StartShuttle();                                   // a ground jump while sprinting (or just off a ledge) is a sprint jump

                if (canWallJump)
                {
                    shuttle = false; carryX = 0f; sprinting = false;
                    int away = -lastWallDir;
                    wallKickSpeed = tuning.wallJumpSpeedX;
                    wallKickDecel = (tuning.wallJumpSpeedX - tuning.runSpeed) / Mathf.Max(1, tuning.wallJumpLockSteps);
                    wallLockDir = away;
                    rb.linearVelocity = new Vector2(away * wallKickSpeed, rb.linearVelocity.y);
                    wallLockSteps = tuning.wallJumpLockSteps;
                    riseMinSteps = 1;               // a wall jump can be cut short almost at once
                    wallSlideSide = 0;
                    wallSteps = 0;
                    FaceDirection(away);
                    airJumpsLeft = tuning.airJumps;
                    if (tuning.wallResetsAirDash) airDashesLeft = tuning.airDashes;
                    WallJumped?.Invoke();
                }
                else if (canAirJump)
                {
                    EndShuttle(true); sprinting = false;               // a double jump cuts a sprint jump off (the speed fades out)
                    airJumpsLeft--;
                    riseSpeed = tuning.doubleJumpSpeed;
                    riseMaxSteps = tuning.doubleJumpSteps;
                    riseMinSteps = tuning.doubleJumpStepsMin;
                    AirJumped?.Invoke();
                }
                else GroundJumped?.Invoke();
            }

            if (!jumping) return;

            bool released = !jumpHeld || jumpReleaseQueueSteps > 0;
            if (jumpStepsDone >= riseMaxSteps)
            {
                EndJump(true);                      // reached the full rise: coast over the top
                return;
            }
            if (released && !bouncing && jumpStepsDone >= riseMinSteps)
            {
                EndJump(false);                     // let go early: cut the upward speed (a pogo bounce cannot be cut)
                return;
            }

            // bumped a ceiling: upward speed was killed by the physics step
            if (jumpStepsDone > 1 && rb.linearVelocity.y < 0.1f)
            {
                EndJump(false);
                return;
            }

            CornerCorrect();

            var v = rb.linearVelocity;
            v.y = riseSpeed;
            rb.linearVelocity = v;
            jumpStepsDone++;
        }

        // ------------------------------------------------------------------ damage hooks (used by PlayerHealth2D)

        /// <summary>Drops everything the hero was doing: dash, jump, float, wall slide, pogo, hard landing, queued presses.</summary>
        void CancelAllActions()
        {
            dashing = false; jumping = false; bouncing = false; IsFloating = false;
            if (sprinting) { sprinting = false; SprintEnded?.Invoke(false); }
            shuttle = false; carryX = 0f; sprintBufferSteps = 0;
            recoilStepsLeft = 0; wallLockSteps = 0; hardLanding = false;
            willHardLand = false; fallTimer = 0f; gravityMul = 1f;
            StopFallRumble();
            CancelWallSlide();
            ClearInputBuffers();
        }

        /// <summary>
        /// Knock-back after being hurt: all actions are cancelled, then for the given steps the velocity is forced
        /// (gravity off, input ignored), then the hero stops dead and gravity returns - like the original's StartRecoil
        /// + CancelDamageRecoil. The hero turns to face what hit it.
        /// </summary>
        public void BeginDamageRecoil(Vector2 velocity, int steps)
        {
            CancelAllActions();
            damageRecoilVel = velocity;
            damageRecoilSteps = Mathf.Max(1, steps);
            if (velocity.x != 0f) FaceDirection(velocity.x > 0f ? -1 : 1);
            rb.linearVelocity = velocity;
        }

        /// <summary>Dying: actions are dropped, input is ignored, the body just falls to the ground.</summary>
        public void SetDead(bool value)
        {
            dead = value;
            if (value) { CancelAllActions(); damageRecoilSteps = 0; }
        }

        /// <summary>Held perfectly still (no gravity, no input), e.g. right after being put back at a checkpoint.</summary>
        public void HoldForRespawn(bool value)
        {
            respawnHold = value;
            if (value) { CancelAllActions(); damageRecoilSteps = 0; rb.linearVelocity = Vector2.zero; }
        }

        public void TeleportTo(Vector2 position)
        {
            rb.position = position;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            rb.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        /// <summary>Binding (healing): all actions are dropped and the hero is rooted sideways; gravity still applies.</summary>
        public void SetBinding(bool value)
        {
            if (binding == value) return;
            binding = value;
            if (value)
            {
                CancelAllActions();
                var v = rb.linearVelocity; v.x = 0f; rb.linearVelocity = v;
            }
        }

        void HandleBinding()
        {
            ClearInputBuffers();
            var v = rb.linearVelocity; v.x = 0f; rb.linearVelocity = v;
            ApplyGravity();
            UpdateState();
        }

        void HandleDamageRecoil()
        {
            ClearInputBuffers();
            rb.linearVelocity = damageRecoilVel;
            if (--damageRecoilSteps <= 0) rb.linearVelocity = Vector2.zero;   // stop dead, then gravity takes over next step
            UpdateState();
        }

        void HandleLockedOut()
        {
            ClearInputBuffers();
            if (respawnHold) { rb.linearVelocity = Vector2.zero; UpdateState(); return; }
            // dead: no sideways drift, fall to the ground
            var v = rb.linearVelocity;
            v.x = 0f;
            if (IsGrounded && v.y <= 0f) v.y = 0f;
            else v.y = Mathf.Max(v.y - tuning.gravity * Time.fixedDeltaTime, -tuning.maxFallSpeed);
            rb.linearVelocity = v;
            UpdateState();
        }

        // ------------------------------------------------------------------ hooks for combat

        public void FaceToward(int dir) => FaceDirection(dir);

        /// <summary>An attack ends a float; floating can only start again after jump has been released and pressed again.</summary>
        public void CancelFloat() { IsFloating = false; }

        /// <summary>Set by the combat component while a swing is in progress; floating cannot start during it.</summary>
        public bool AttackLocked { get; set; }

        public void CancelWallSlide() { wallSlideSide = 0; wallUnstickSteps = 0; IsWallSliding = false; }

        /// <summary>
        /// A forward slash on a wall halves the slide speed (never faster than maxFall) for the length of the swing.
        /// </summary>
        public void SlowFallForWallSlash(float maxFall, float duration)
        {
            var v = rb.linearVelocity;
            v.y = Mathf.Max(v.y * 0.5f, -maxFall);
            rb.linearVelocity = v;
            wallSlashSlowUntil = Time.time + duration;
        }

        /// <summary>An up-slash on a wall steps the hero away from it (the original moves it 0.8 units) and drops the slide.</summary>
        public void PushOffWall(float distance)
        {
            int side = wallSlideSide != 0 ? wallSlideSide : WallDirection;
            CancelWallSlide();
            if (side != 0) rb.position += new Vector2(-side * distance, 0f);
        }

        /// <summary>Knock-back after hitting something sideways: forced horizontal speed for a few steps.</summary>
        public void ApplyRecoil(int dirX, int steps, float speed)
        {
            if (dashing) EndDash();
            recoilDir = dirX; recoilStepsLeft = steps; recoilSpeed = speed;
        }

        /// <summary>
        /// Down-slash pogo: a short jump that starts part-way through the normal rise, cannot be cut short,
        /// and refreshes the air jump and air dash.
        /// </summary>
        public void StartBounce(int startSteps)
        {
            if (dashing) EndDash();
            jumping = true; bouncing = true; jumpedSinceGrounded = true;
            jumpStepsDone = Mathf.Clamp(startSteps, 0, Mathf.Max(0, tuning.jumpSteps - 1));
            riseSpeed = tuning.jumpSpeed; riseMaxSteps = tuning.jumpSteps; riseMinSteps = 0;
            jumpQueueSteps = 0; ledgeSteps = 0; floatSteps = 0; IsFloating = false;
            airJumpsLeft = tuning.airJumps; airDashesLeft = tuning.airDashes;
            wallSlideSide = 0; IsWallSliding = false;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, riseSpeed);
        }

        // ------------------------------------------------------------------ positional forgiveness

        readonly Collider2D[] overlap = new Collider2D[8];

        bool BoxFree(Vector2 center, Vector2 size)
        {
            int n = Physics2D.OverlapBox(center, size, 0f, groundFilter, overlap);
            for (int i = 0; i < n; i++)
                if (overlap[i] != col) return false;
            return true;
        }

        /// <summary>Rising into a platform corner by a hair: slip sideways past it instead of bonking.</summary>
        void CornerCorrect()
        {
            if (tuning.cornerCorrection <= 0f) return;
            var b = col.bounds;
            Vector2 size = new Vector2(b.size.x * 0.98f, b.size.y * 0.98f);
            float lift = riseSpeed * Time.fixedDeltaTime + 0.02f;
            if (BoxFree((Vector2)b.center + Vector2.up * lift, size)) return;   // not blocked overhead

            for (float dx = 0.05f; dx <= tuning.cornerCorrection + 0.001f; dx += 0.05f)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    var shifted = (Vector2)b.center + new Vector2(s * dx, 0f);
                    if (BoxFree(shifted, size) && BoxFree(shifted + Vector2.up * lift, size))
                    {
                        rb.position += new Vector2(s * dx, 0f);
                        return;
                    }
                }
            }
        }

        /// <summary>Airborne and pressing into a ledge whose top is just above the feet: pop up onto it.</summary>
        void LedgeStepUp()
        {
            if (tuning.ledgeStepUp <= 0f || IsGrounded || moveX == 0f || dashing) return;
            var b = col.bounds;
            Vector2 size = new Vector2(b.size.x * 0.98f, b.size.y * 0.98f);
            Vector2 ahead = new Vector2(Mathf.Sign(moveX) * 0.08f, 0f);
            if (BoxFree((Vector2)b.center + ahead, size)) return;               // nothing in the way

            for (float dy = 0.05f; dy <= tuning.ledgeStepUp + 0.001f; dy += 0.05f)
            {
                if (BoxFree((Vector2)b.center + ahead + Vector2.up * dy, size) && BoxFree((Vector2)b.center + Vector2.up * dy, size))
                {
                    rb.position += new Vector2(0f, dy);
                    if (rb.linearVelocity.y < 0f) rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                    return;
                }
            }
        }

        bool GroundCloseBelow()
        {
            float vy = rb.linearVelocity.y;
            if (vy > 0f) return false;
            return GroundWithin(Mathf.Max(tuning.airJumpMinGroundGap, -vy * tuning.jumpBufferSteps * Time.fixedDeltaTime));
        }

        /// <summary>True if walkable ground is within this distance straight below the collider.</summary>
        bool GroundWithin(float dist)
        {
            int n = col.Cast(Vector2.down, groundFilter, hits, dist);
            for (int i = 0; i < n; i++)
                if (Vector2.Angle(hits[i].normal, Vector2.up) <= tuning.maxGroundAngle) return true;
            return false;
        }

        void EndJump(bool naturalEnd)
        {
            jumping = false;
            bouncing = false;
            var v = rb.linearVelocity;
            if (v.y > 0f) v.y *= naturalEnd ? tuning.jumpNaturalEndKeep : tuning.jumpEndVelocityKeep;
            rb.linearVelocity = v;
            gravityMul = tuning.hangGravityMultiplier;   // start the air hang
        }

        // ------------------------------------------------------------------ run + gravity

        void ApplyHorizontal()
        {
            var v = rb.linearVelocity;
            if (recoilStepsLeft > 0 && !IsWallSliding)
            {
                // knocked back by hitting something sideways: forced speed away from it for a few steps
                v.x = recoilDir * recoilSpeed;
                rb.linearVelocity = v;
                recoilStepsLeft--;
                return;
            }
            if (IsWallSliding)
            {
                // stuck to the wall: no sideways drift (even if holding away) until the sticky steps run out
                v.x = 0f;
                rb.linearVelocity = v;
                return;
            }
            if (wallLockSteps > 0)
            {
                // wall-jump kick-off: forced speed away from the wall, decaying in a line down to run speed
                v.x = wallLockDir * wallKickSpeed;
                rb.linearVelocity = v;
                wallKickSpeed = Mathf.Max(tuning.runSpeed, wallKickSpeed - wallKickDecel);
                return;
            }

            if (shuttle)
            {
                // sprint jump: forced forward speed all the way up
                v.x = shuttleDir * tuning.shuttlecockSpeed;
                rb.linearVelocity = v;
                return;
            }
            if (sprinting)
            {
                v.x = Mathf.MoveTowards(v.x, sprintDir * tuning.sprintSpeed, tuning.sprintAcceleration * Time.fixedDeltaTime);
                rb.linearVelocity = v;
                return;
            }
            if (carryX != 0f)
            {
                bool turnedAround = moveX != 0f && Mathf.Sign(moveX) != Mathf.Sign(carryX);
                if (IsGrounded || turnedAround) carryX = 0f;
                else
                {
                    v.x = carryX;
                    carryX = Mathf.MoveTowards(carryX, 0f, tuning.shuttleCarryDecay * Time.fixedDeltaTime);
                    if (moveX != 0f && Mathf.Abs(carryX) <= tuning.runSpeed) carryX = 0f;   // slowed to running pace while steering that way: normal control again
                    rb.linearVelocity = v;
                    return;
                }
            }

            float target = moveX * tuning.runSpeed;
            float rate;
            if (Mathf.Abs(target) > 0.01f) rate = IsGrounded ? tuning.groundAcceleration : tuning.airAcceleration;
            else rate = IsGrounded ? tuning.groundDeceleration : tuning.airDeceleration;
            v.x = Mathf.MoveTowards(v.x, target, rate * Time.fixedDeltaTime);
            rb.linearVelocity = v;
        }

        /// <summary>A jump press floats only when nothing else would use it and the player is clear of the ground.</summary>
        bool CanStartFloat()
        {
            if (!tuning.floatEnabled || IsFloating || jumping || IsGrounded || ledgeSteps > 0) return false;
            if (!floatPressValid) return false;                              // the press was made during a rise
            if (IsWallSliding || dashing || AttackLocked) return false;
            if (rb.linearVelocity.y > 0.01f) return false;                  // only while falling; an early press stays buffered
            return !GroundWithin(tuning.floatMinGroundGap);                 // not when about to land
        }

        void UpdateFloat()
        {
            if (!IsFloating) return;
            bool stop = !tuning.floatEnabled || !jumpHeld || IsGrounded || IsWallSliding || dashing || jumping
                        || (tuning.floatMaxSteps > 0 && floatSteps >= tuning.floatMaxSteps);
            if (stop) { IsFloating = false; return; }
            floatSteps++;
        }

        void ApplyGravity()
        {
            if (jumping) return;
            var v = rb.linearVelocity;
            if (IsGrounded && v.y <= 0f) { v.y = 0f; rb.linearVelocity = v; gravityMul = 1f; return; }

            // air hang: low gravity right after a jump, ramping back to normal while the button is held;
            // letting go (or touching ground/walls) restores it at once
            if (!jumpHeld || IsWallSliding) gravityMul = 1f;
            else if (gravityMul < 1f) gravityMul = Mathf.Min(1f, gravityMul + tuning.hangGravityRampPerSecond * Time.fixedDeltaTime);

            if (IsFloating)
            {
                v.y = -tuning.floatFallSpeed;       // floating holds a constant fall speed
                rb.linearVelocity = v;
                return;
            }

            float slideSpeed = Time.time < wallSlashSlowUntil ? tuning.wallSlideSpeed * 0.5f : tuning.wallSlideSpeed;
            float maxFall = IsWallSliding ? slideSpeed : tuning.maxFallSpeed;
            v.y = Mathf.Max(v.y - tuning.gravity * gravityMul * Time.fixedDeltaTime, -maxFall);
            rb.linearVelocity = v;
        }

        // ------------------------------------------------------------------ state

        void UpdateState()
        {
            if (dead) { State = MotionState.Dead; return; }
            if (damageRecoilSteps > 0) { State = MotionState.Damaged; return; }
            if (binding) { State = MotionState.Binding; return; }
            if (hardLanding) { State = MotionState.HardLanding; return; }
            if (dashing) { State = MotionState.Dashing; return; }
            if (sprinting && IsGrounded) { State = MotionState.Sprinting; return; }
            if (IsGrounded) { State = MotionState.Grounded; return; }
            if (IsWallSliding) { State = MotionState.WallSliding; return; }
            if (IsFloating) { State = MotionState.Floating; return; }
            State = rb.linearVelocity.y > 0.01f ? MotionState.Rising : MotionState.Falling;
        }

        void UpdateFacing()
        {
            if (wallLockSteps > 0) return;
            if (IsWallSliding) { FaceDirection(WallDirection); return; }
            if (moveX != 0f) FaceDirection((int)moveX);
        }

        void FaceDirection(int dir)
        {
            if (dir == 0 || dir == FacingDirection) return;
            FacingDirection = dir;
            if (spriteToFlip != null) spriteToFlip.flipX = dir < 0;
        }
    }
}
