using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowknight.Player
{
    /// <summary>
    /// Silk (the resource nail hits fill) and bind (hold a button to turn silk into masks).
    /// Rules follow what the original's code shows: one silk per nail hit, bind needs the full 9, it cannot start while
    /// dashing, hurt, dead, hard-landing or in the middle of a swing, taking a hit or dying breaks it, and dying empties
    /// the silk. The bind itself is a state machine in the original that cannot be read as code, so how long it takes,
    /// how much it heals and when the silk is spent are our own choices (tuning "Silk / bind").
    /// </summary>
    [RequireComponent(typeof(PlayerController2D), typeof(PlayerHealth2D))]
    public class PlayerSilk2D : MonoBehaviour
    {
        public int Silk { get; private set; }
        public int MaxSilk => Tuning.maxSilk;
        public bool IsBinding { get; private set; }
        /// <summary>0..1 through the current bind.</summary>
        public float BindProgress { get; private set; }
        public bool BindReady => Silk >= Tuning.bindCost;
        public int BindsCompleted { get; private set; }
        public int BindsCancelled { get; private set; }

        /// <summary>new silk total</summary>
        public event Action<int> SilkChanged;
        /// <summary>amount gained, world position it came from</summary>
        public event Action<int, Vector2> SilkGained;
        public event Action BindStarted, BindCompleted;
        /// <summary>progress reached when the bind was broken</summary>
        public event Action<float> BindCancelled;

        PlayerController2D controller;
        PlayerHealth2D health;
        PlayerCombat2D combat;
        PlayerTuning Tuning => controller.Tuning;
        /// <summary>The tuning asset in use (for UI).</summary>
        public PlayerTuning TuningAsset => controller.Tuning;
        InputAction bindAction;
        bool bindPressedFlag, bindHeld;
        float bindElapsed;

        void Awake()
        {
            controller = GetComponent<PlayerController2D>();
            health = GetComponent<PlayerHealth2D>();
            combat = GetComponent<PlayerCombat2D>();

            bindAction = new InputAction("Bind", InputActionType.Button);
            bindAction.AddBinding("<Keyboard>/f");
            bindAction.AddBinding("<Gamepad>/buttonNorth");
        }

        void OnEnable()
        {
            bindAction.Enable();
            if (combat != null) combat.Hit += OnNailHit;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            bindAction.Disable();
            if (combat != null) combat.Hit -= OnNailHit;
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            if (IsBinding) EndBind(false);
        }

        void OnDestroy() => bindAction?.Dispose();

        void Update()
        {
            bindHeld = bindAction.IsPressed();
            if (bindAction.WasPressedThisFrame()) bindPressedFlag = true;
        }

        void FixedUpdate()
        {
            bool pressed = bindPressedFlag;
            bindPressedFlag = false;

            if (IsBinding)
            {
                if (!bindHeld || !CanKeepBinding()) { EndBind(false); return; }
                bindElapsed += Time.fixedDeltaTime;
                BindProgress = Mathf.Clamp01(bindElapsed / Mathf.Max(0.05f, Tuning.bindTime));
                if (bindElapsed >= Tuning.bindTime) Finish();
            }
            else if (pressed && bindHeld && CanBind())
            {
                StartBind();
            }
        }

        // ------------------------------------------------------------------ rules

        /// <summary>Mirrors the original CanBind: not dashing, hurt, dead, hard-landing, respawning or mid-swing; plus silk and missing health.</summary>
        public bool CanBind()
        {
            if (IsBinding || !BindReady) return false;
            if (health.Health >= health.MaxHealth) return false;           // nothing to heal
            if (health.IsDead || health.IsRespawning) return false;
            if (controller.IsDamaged || controller.IsDashing || controller.IsHardLanding || controller.IsHeldForRespawn || controller.IsMantling) return false;
            if (combat != null && combat.IsAttacking) return false;
            return true;
        }

        bool CanKeepBinding() => !health.IsDead && !health.IsRespawning && !controller.IsDamaged;

        // ------------------------------------------------------------------ silk

        public void AddSilk(int amount, Vector2 from)
        {
            if (amount <= 0 || health.IsDead) return;
            int before = Silk;
            Silk = Mathf.Min(MaxSilk, Silk + amount);
            if (Silk == before) return;
            SilkChanged?.Invoke(Silk);
            SilkGained?.Invoke(Silk - before, from);
        }

        public void AddSilk(int amount) => AddSilk(amount, (Vector2)transform.position);

        /// <summary>Debug / test helper: sets the silk directly (no effects).</summary>
        public void DebugSetSilk(int value)
        {
            Silk = Mathf.Clamp(value, 0, MaxSilk);
            SilkChanged?.Invoke(Silk);
        }

        void OnNailHit(HitTarget target, AttackDir dir)
        {
            if (target != null && target.givesSilk) AddSilk(Tuning.silkPerHit, target.transform.position);
        }

        // ------------------------------------------------------------------ bind

        void StartBind()
        {
            IsBinding = true;
            bindElapsed = 0f;
            BindProgress = 0f;
            controller.SetBinding(true);
            BindStarted?.Invoke();
        }

        void Finish()
        {
            Silk = Mathf.Max(0, Silk - Tuning.bindCost);
            health.Heal(Tuning.bindHeal);
            BindsCompleted++;
            EndBind(true);
            SilkChanged?.Invoke(Silk);
        }

        void EndBind(bool completed)
        {
            float reached = BindProgress;
            IsBinding = false;
            BindProgress = 0f;
            controller.SetBinding(false);
            if (completed) BindCompleted?.Invoke();
            else { BindsCancelled++; BindCancelled?.Invoke(reached); }
        }

        void OnDamaged(DamageInfo info) { if (IsBinding) EndBind(false); }

        void OnDied()
        {
            if (IsBinding) EndBind(false);
            Silk = 0;                                  // the original empties the silk on death
            SilkChanged?.Invoke(Silk);
        }
    }
}
