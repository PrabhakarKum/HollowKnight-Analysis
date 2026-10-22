using System;
using System.Collections;
using UnityEngine;
using Hollowknight.Feel;

namespace Hollowknight.Player
{
    public enum DamageKind
    {
        /// <summary>An enemy or projectile: knocks the hero back.</summary>
        Enemy,
        /// <summary>Spikes and the like: the hero is moved back to the last safe ground instead of being knocked back.</summary>
        Hazard
    }

    public readonly struct DamageInfo
    {
        public readonly int Amount;
        /// <summary>World x of whatever hurt the hero; the knock-back pushes away from it.</summary>
        public readonly float SourceX;
        public readonly DamageKind Kind;
        public readonly GameObject Source;

        public DamageInfo(int amount, float sourceX, DamageKind kind, GameObject source = null)
        {
            Amount = amount; SourceX = sourceX; Kind = kind; Source = source;
        }
    }

    /// <summary>
    /// Health ("masks"), taking damage, knock-back, hit freeze, invulnerability, hazard respawn and death.
    /// Follows the structure of the original TakeDamage / StartRecoil / Invulnerable routines (values are our own):
    /// a hit is ignored while invulnerable, knocked back or dead; otherwise health drops, swings and dashes are cancelled,
    /// and either the hero dies, or is knocked away from the source with a brief hit freeze, then stays invulnerable.
    /// </summary>
    [RequireComponent(typeof(PlayerController2D))]
    public class PlayerHealth2D : MonoBehaviour
    {
        [Tooltip("Debug: nothing can hurt the hero.")]
        [SerializeField] bool godMode;
        [Tooltip("A ground position only counts as 'safe' for hazard respawns once the hero has been away from it this long, so a respawn never lands on the spikes just touched.")]
        [SerializeField] float safeLag = 0.25f;

        public int Health { get; private set; }
        public int MaxHealth => controller.Tuning.maxHealth;
        public bool IsDead { get; private set; }
        public bool IsInvulnerable { get; private set; }
        /// <summary>True from a hazard hit / death until control has returned.</summary>
        public bool IsRespawning { get; private set; }
        public Vector2 CheckpointPosition { get; private set; }
        public Vector2 LastSafePosition { get; private set; }
        public bool GodMode { get => godMode; set => godMode = value; }
        /// <summary>True while any hit-stop is running (see FreezeMoment).</summary>
        public bool IsFrozen => FreezeMoment.IsFrozen;
        public int HitsTaken { get; private set; }

        public event Action<DamageInfo> Damaged;
        public event Action<int> HealthChanged;
        public event Action Died, Respawned;

        PlayerController2D controller;
        PlayerCombat2D combat;
        Rigidbody2D rb;
        SpriteRenderer flickerRenderer;
        PlayerTuning Tuning => controller.Tuning;

        float invulnerableLeft;
        Coroutine invulnerableRoutine;
        readonly System.Collections.Generic.Queue<(float time, Vector2 pos)> groundTrail = new System.Collections.Generic.Queue<(float, Vector2)>();

        void Awake()
        {
            controller = GetComponent<PlayerController2D>();
            combat = GetComponent<PlayerCombat2D>();
            rb = GetComponent<Rigidbody2D>();
            var animator = GetComponentInChildren<Animator>();
            flickerRenderer = animator != null ? animator.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
        }

        void Start()
        {
            Health = MaxHealth;
            CheckpointPosition = LastSafePosition = rb.position;
            HealthChanged?.Invoke(Health);
        }

        void OnDisable()
        {
            SetBodyVisible(true);
        }

        void FixedUpdate()
        {
            // Remember where the hero last stood on the ground, lagging behind by safeLag seconds: a hazard respawn
            // then puts it back on ground it was already well clear of, not on the edge of the spikes it just hit.
            if (controller.IsGrounded && !IsDead && !IsRespawning && !controller.IsDamaged)
                groundTrail.Enqueue((Time.time, rb.position));
            // (frozen while being respawned / dead, so the spot we are about to be put back on cannot creep toward the hazard)
            while (!IsRespawning && !IsDead && groundTrail.Count > 0 && Time.time - groundTrail.Peek().time >= safeLag)
                LastSafePosition = groundTrail.Dequeue().pos;
        }

        // ------------------------------------------------------------------ rules

        /// <summary>Mirrors the original CanTakeDamage: not invulnerable, not mid knock-back, not dead.</summary>
        public bool CanTakeDamage => !godMode && !IsDead && !IsInvulnerable && !IsRespawning && !controller.IsDamaged;

        public void SetCheckpoint(Vector2 position) => CheckpointPosition = position;

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
            HealthChanged?.Invoke(Health);
        }

        /// <summary>Returns true if the hit landed.</summary>
        public bool TakeDamage(DamageInfo info)
        {
            if (info.Amount <= 0 || !CanTakeDamage) return false;

            Health = Mathf.Max(0, Health - info.Amount);
            HitsTaken++;
            HealthChanged?.Invoke(Health);
            Damaged?.Invoke(info);
            if (combat != null) combat.CancelAttack();

            if (Health <= 0)
            {
                StartCoroutine(DieRoutine());
                return true;
            }

            StartInvulnerable(Tuning.invulnerableTime);
            HurtFeedback(Tuning.heroDamageShake);
            if (info.Kind == DamageKind.Hazard)
            {
                IsRespawning = true;
                groundTrail.Clear();
                controller.HoldForRespawn(true);
                StartCoroutine(HazardRespawnRoutine());
            }
            else
            {
                float away = rb.position.x >= info.SourceX ? 1f : -1f;
                var v = Tuning.damageRecoilVelocity;
                int steps = Mathf.Max(1, Mathf.RoundToInt(Tuning.damageRecoilTime / Time.fixedDeltaTime));
                controller.BeginDamageRecoil(new Vector2(away * v.x, v.y), steps);
            }
            return true;
        }

        /// <summary>Debug / test helper: sets health directly (no knock-back, no invulnerability, no death).</summary>
        public void DebugSetHealth(int value)
        {
            Health = Mathf.Clamp(value, 1, MaxHealth);
            HealthChanged?.Invoke(Health);
        }

        /// <summary>Debug / test helper: kills the hero regardless of invulnerability.</summary>
        public void Kill()
        {
            if (IsDead) return;
            Health = 0;
            HealthChanged?.Invoke(Health);
            if (combat != null) combat.CancelAttack();
            StartCoroutine(DieRoutine());
        }

        // ------------------------------------------------------------------ invulnerability + hit freeze

        void StartInvulnerable(float seconds)
        {
            invulnerableLeft = Mathf.Max(invulnerableLeft, seconds);
            IsInvulnerable = true;
            if (invulnerableRoutine == null) invulnerableRoutine = StartCoroutine(InvulnerableRoutine());
        }

        IEnumerator InvulnerableRoutine()
        {
            // counts down in game time, so the hit freeze does not eat into it (like the original, which counts deltaTime)
            Color baseColor = flickerRenderer != null ? flickerRenderer.color : Color.white;
            while (invulnerableLeft > 0f && !IsDead)
            {
                invulnerableLeft -= Time.deltaTime;
                if (flickerRenderer != null)
                {
                    bool dim = Mathf.FloorToInt(Time.time / Mathf.Max(0.01f, Tuning.flickerInterval)) % 2 == 0;
                    var c = baseColor; c.a = dim ? Tuning.flickerDimAlpha : baseColor.a;
                    flickerRenderer.color = c;
                }
                yield return null;
            }
            if (flickerRenderer != null) flickerRenderer.color = baseColor;
            IsInvulnerable = false;
            invulnerableLeft = 0f;
            invulnerableRoutine = null;
        }

        /// <summary>Hit-stop + camera shake when the hero is hurt (the freeze table and stacking live in FreezeMoment / GameTime).</summary>
        void HurtFeedback(ShakePreset shake)
        {
            FreezeMoment.Start(Tuning.heroDamageFreeze);
            CameraShake2D.Do(shake, Tuning.shakeFreezeFrames);
        }

        // ------------------------------------------------------------------ hazards and death

        void SetBodyVisible(bool visible)
        {
            if (flickerRenderer != null) flickerRenderer.enabled = visible;
        }

        IEnumerator HazardRespawnRoutine()
        {
            // like the original: the hero vanishes at once, is put back on safe ground, then gets a longer
            // invulnerability (twice the hit invulnerability plus 0.3 s) and faces back toward the hazard
            SetBodyVisible(false);
            yield return new WaitForSeconds(Tuning.hazardRespawnDelay);
            if (IsDead) { SetBodyVisible(true); yield break; }
            float xBefore = rb.position.x;
            controller.TeleportTo(LastSafePosition);
            controller.FaceToward(xBefore >= LastSafePosition.x ? 1 : -1);
            SetBodyVisible(true);
            StartInvulnerable(Tuning.invulnerableTime * 2f + 0.3f);
            groundTrail.Clear();
            yield return new WaitForSeconds(Tuning.respawnHoldTime);
            if (IsDead) yield break;
            controller.HoldForRespawn(false);
            IsRespawning = false;
        }

        IEnumerator DieRoutine()
        {
            IsDead = true;
            IsRespawning = true;
            invulnerableLeft = 0f;
            controller.SetDead(true);
            HurtFeedback(Tuning.heroDeathShake);
            Died?.Invoke();

            yield return new WaitForSeconds(Tuning.deathRespawnDelay);

            // back to the checkpoint with full health
            controller.HoldForRespawn(true);
            controller.SetDead(false);
            controller.TeleportTo(CheckpointPosition);
            LastSafePosition = CheckpointPosition;
            groundTrail.Clear();
            IsDead = false;
            Health = MaxHealth;
            HealthChanged?.Invoke(Health);
            StartInvulnerable(Tuning.invulnerableTime);   // our own addition: a moment of safety after coming back
            Respawned?.Invoke();

            yield return new WaitForSeconds(Tuning.respawnHoldTime);
            controller.HoldForRespawn(false);
            IsRespawning = false;
        }
    }
}
