using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Hollowknight.Feel;

namespace Hollowknight.Player
{
    public enum AttackDir { Forward, Up, Down }

    /// <summary>
    /// Nail attack: forward / up / down slashes, two-swing combo, cooldown, input buffer, sideways recoil,
    /// down-slash pogo bounce, and the wall-slide slash rules. Timers run in seconds.
    /// Sits next to PlayerController2D and uses its public hooks.
    /// </summary>
    [RequireComponent(typeof(PlayerController2D))]
    public class PlayerCombat2D : MonoBehaviour
    {
        [Header("Debug")]
        [Tooltip("Draws the active slash box in the Scene/Game view with a translucent sprite.")]
        [SerializeField] bool showSlashBox = true;

        public bool IsAttacking { get; private set; }
        public AttackDir CurrentDir { get; private set; }
        /// <summary>0 or 1: which of the two alternating swings the current / last attack used.</summary>
        public int ComboIndex { get; private set; }
        /// <summary>
        /// True while the damaging hitbox is live: from the swing animation's strike keyframe (see StrikeTime) for slashActiveTime.
        /// Not from the button press - the sword is still being drawn back then. It never starts before the keyframe is on screen
        /// (it is checked on the next physics step after, so at most one step late).
        /// </summary>
        public bool SlashActive => IsAttacking && SwingClock >= StrikeTime && SwingClock <= StrikeTime + Tuning.slashActiveTime;
        /// <summary>Seconds into the current swing at which its hit counts (the animation keyframe where the blade arrives).</summary>
        public float StrikeTime { get; private set; }
        /// <summary>Seconds since the current swing began (the attack timer).</summary>
        public float SwingTime => attackTimer;
        /// <summary>
        /// How far into the swing ANIMATION we are: the Animator's own clip time when it is playing the swing, otherwise the attack timer
        /// (the first physics steps, before the Animator has started the clip). The hit window runs on this clock.
        /// </summary>
        public float SwingClock => visuals != null && visuals.TryGetSwingTime(out float clip) ? clip : attackTimer;
        public int SwingsStarted { get; private set; }
        /// <summary>How many swings have given their first-hit freeze / shake (one per swing that connected).</summary>
        public int FirstHitFeedbackCount { get; private set; }

        /// <summary>direction, combo index (0/1), whether the player was on the ground</summary>
        public event Action<AttackDir, int, bool> AttackStarted;
        public event Action AttackEnded;
        public event Action<HitTarget, AttackDir> Hit;

        PlayerController2D controller;
        PlayerVisuals2D visuals;
        PlayerTuning Tuning => controller.Tuning;
        InputAction attackAction;
        bool attackPressedFlag;
        int attackQueue;
        float attackTimer;
        float cooldown;
        float altAttackTime = -999f;
        bool alt;                       // next swing uses the alternate animation
        AttackDir prevDir = AttackDir.Forward;
        bool recoiledThisSwing, bouncedThisSwing;
        readonly HashSet<HitTarget> hitThisSwing = new HashSet<HitTarget>();
        readonly Collider2D[] results = new Collider2D[24];
        ContactFilter2D filter;
        Transform boxVisual;
        SpriteRenderer boxRenderer;
        Bounds lastBox;

        void Awake()
        {
            controller = GetComponent<PlayerController2D>();
            visuals = GetComponentInChildren<PlayerVisuals2D>(true);
            filter = new ContactFilter2D { useTriggers = true, useLayerMask = false };

            attackAction = new InputAction("Attack", InputActionType.Button);
            attackAction.AddBinding("<Keyboard>/j");
            attackAction.AddBinding("<Keyboard>/c");
            attackAction.AddBinding("<Mouse>/leftButton");
            attackAction.AddBinding("<Gamepad>/buttonWest");
        }

        void OnEnable() { attackAction.Enable(); controller.HardLanded += OnHardLanded; }
        void OnDisable() { attackAction.Disable(); controller.HardLanded -= OnHardLanded; }

        void OnHardLanded() => CancelAttack();   // a hard landing cancels a swing in progress

        /// <summary>Drops the swing in progress and any buffered attack press (hard landing, being hurt, dying).</summary>
        public void CancelAttack() { attackQueue = 0; attackPressedFlag = false; EndAttack(); }
        void OnDestroy() => attackAction?.Dispose();

        void Update()
        {
            if (attackAction.WasPressedThisFrame()) attackPressedFlag = true;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            controller.AttackLocked = IsAttacking;
            if (cooldown > 0f) cooldown -= dt;
            if (controller.InputLocked) { attackPressedFlag = false; attackQueue = 0; }   // a hard landing ignores all input

            if (attackPressedFlag) { attackQueue = Tuning.attackBufferSteps; attackPressedFlag = false; }
            else if (attackQueue > 0 && !controller.IsDashing) attackQueue--;     // a press made during a dash waits for it to end

            if (IsAttacking)
            {
                attackTimer += dt;
                if (SlashActive) CheckHits();
                if (attackTimer >= Tuning.attackDuration) EndAttack();
            }

            if (attackQueue > 0 && CanAttack())
            {
                attackQueue = 0;
                StartAttack();
            }

            UpdateBoxVisual();
        }

        // ------------------------------------------------------------------ rules

        bool CanAttack()
        {
            if (IsAttacking || cooldown > 0f || controller.InputLocked) return false;
            if (controller.IsDashing) return false;                 // a dash has to finish first; the press stays buffered
            return true;
        }

        AttackDir ChooseDirection()
        {
            float y = controller.MoveInput.y;
            if (y > 0f) return AttackDir.Up;
            if (y < 0f && !controller.IsGrounded) return AttackDir.Down;   // down on the ground is a normal forward swing
            return AttackDir.Forward;
        }

        void StartAttack()
        {
            var dir = ChooseDirection();

            // turn toward a held horizontal direction before swinging
            float x = controller.MoveInput.x;
            if (x != 0f) controller.FaceToward((int)x);

            controller.CancelFloat();

            // two-swing combo: alternates, restarts after a pause or when the direction changes
            if (Time.time - altAttackTime > Tuning.attackDuration + Tuning.comboResetTime || dir != prevDir) alt = false;
            ComboIndex = alt ? 1 : 0;
            alt = !alt;

            // wall slide rules: a forward slash slows the slide; up / down slashes let go of the wall
            if (controller.IsWallSliding)
            {
                if (dir == AttackDir.Forward) controller.SlowFallForWallSlash(Tuning.wallSlashMaxFall, Tuning.attackDuration);
                else if (dir == AttackDir.Up) controller.PushOffWall(Tuning.upSlashWallPush);
                else controller.CancelWallSlide();
            }

            // which animation the swing plays (PlayerVisuals2D picks the same one) decides the keyframe its hit counts on
            bool grounded = controller.IsGrounded;
            StrikeTime = dir == AttackDir.Up ? Tuning.upSlashStartTime
                       : dir == AttackDir.Down ? Tuning.downSlashStartTime
                       : grounded ? (ComboIndex == 0 ? Tuning.forwardSlashStartTime : Tuning.comboSlashStartTime)
                       : Tuning.airSlashStartTime;

            CurrentDir = dir;
            prevDir = dir;
            altAttackTime = Time.time;
            IsAttacking = true;
            attackTimer = 0f;
            cooldown = Mathf.Max(Tuning.attackCooldown, Tuning.attackDuration);   // never shorter than the swing itself
            hitThisSwing.Clear();
            recoiledThisSwing = bouncedThisSwing = false;
            SwingsStarted++;
            AttackStarted?.Invoke(dir, ComboIndex, controller.IsGrounded);
        }

        void EndAttack()
        {
            if (!IsAttacking) return;
            IsAttacking = false;
            AttackEnded?.Invoke();
        }

        // ------------------------------------------------------------------ hitting things

        Bounds SlashBounds()
        {
            Vector2 size, offset;
            int f = controller.FacingDirection;
            switch (CurrentDir)
            {
                case AttackDir.Up: size = Tuning.upSlashSize; offset = Tuning.upSlashOffset; break;
                case AttackDir.Down: size = Tuning.downSlashSize; offset = Tuning.downSlashOffset; break;
                default: size = Tuning.forwardSlashSize; offset = new Vector2(Tuning.forwardSlashOffset.x * f, Tuning.forwardSlashOffset.y); break;
            }
            return new Bounds((Vector2)transform.position + offset, size);
        }

        void CheckHits()
        {
            lastBox = SlashBounds();
            int n = Physics2D.OverlapBox(lastBox.center, lastBox.size, 0f, filter, results);
            for (int i = 0; i < n; i++)
            {
                var c = results[i];
                if (c == null || c.transform.IsChildOf(transform)) continue;
                var target = c.GetComponentInParent<HitTarget>();
                if (target == null || !hitThisSwing.Add(target)) continue;

                target.TakeHit(CurrentDir);
                Hit?.Invoke(target, CurrentDir);

                // hit-stop + shake on the FIRST thing a swing connects with (the original's per-attack "first hit freeze");
                // a swing that hits three things still only stops the game once
                if (hitThisSwing.Count == 1)
                {
                    FreezeMoment.Start(Tuning.nailHitFreeze);
                    CameraShake2D.Do(Tuning.nailHitShake, Tuning.shakeFreezeFrames);
                    FirstHitFeedbackCount++;
                }

                switch (CurrentDir)
                {
                    case AttackDir.Forward:
                        if (target.recoilsPlayer && !recoiledThisSwing)
                        {
                            recoiledThisSwing = true;
                            controller.ApplyRecoil(-controller.FacingDirection, Tuning.recoilSteps, Tuning.recoilSpeed);
                        }
                        break;
                    case AttackDir.Down:
                        if (target.pogo && !bouncedThisSwing)
                        {
                            bouncedThisSwing = true;
                            controller.StartBounce(Tuning.bounceStartSteps);
                            EndAttack();            // the bounce frees you to swing again as soon as the cooldown allows
                        }
                        break;
                }
            }
        }

        // ------------------------------------------------------------------ debug visual

        void UpdateBoxVisual()
        {
#if UNITY_EDITOR
            if (!showSlashBox) { if (boxVisual != null) boxVisual.gameObject.SetActive(false); return; }
            if (boxVisual == null)
            {
                var go = new GameObject("SlashBox (debug)");
                boxRenderer = go.AddComponent<SpriteRenderer>();
                boxRenderer.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                boxRenderer.color = new Color(1f, 1f, 1f, 0.28f);
                boxRenderer.sortingOrder = 10;
                boxRenderer.drawMode = SpriteDrawMode.Sliced;
                boxVisual = go.transform;
            }
            bool show = SlashActive;
            boxVisual.gameObject.SetActive(show);
            if (show)
            {
                var b = SlashBounds();
                boxVisual.position = b.center;
                boxRenderer.size = b.size;
            }
#endif
        }

        void OnDrawGizmosSelected()
        {
            if (controller == null) controller = GetComponent<PlayerController2D>();
            if (controller == null || controller.Tuning == null) return;
            Gizmos.color = Color.cyan;
            foreach (var d in new[] { AttackDir.Forward, AttackDir.Up, AttackDir.Down })
            {
                var saved = CurrentDir; CurrentDir = d;
                var b = SlashBounds(); CurrentDir = saved;
                Gizmos.DrawWireCube(b.center, b.size);
            }
        }
    }
}
