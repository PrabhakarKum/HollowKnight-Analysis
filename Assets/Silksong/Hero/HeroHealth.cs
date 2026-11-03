using System;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // Taking damage, the hurt knockback and stun, invulnerability, death and respawn. Health lives in PlayerData
    // (the original keeps it there too) so the save system sees it.
    //
    // How it fits into HeroController.StepFixed:
    //  - FixedStep runs first. It ticks the invulnerability and death timers and, while the hero is stunned by a hit
    //    or dead, OWNS the velocity: it forces the knockback speed every step (gravity is off, like the original's
    //    recoil) and returns true, and the controller then skips HeroMovement and the wall's second half. This is
    //    the same bypass the dash uses, so movement's vertical-velocity ownership cannot undo the knockback.
    //  - HeroState.Hurt / Dead are set here and held: HeroMovement no longer rewrites them. When the stun ends the
    //    knockback stops dead (the original's ResetMotion), the state goes back to Falling and movement runs in that
    //    same step.
    //  - A hit cancels dash, wall cling/wall jump, swing and pogo bounce, and a jump in progress, at once.
    // All timers are seconds reduced by the step length, so 50 and 100 Hz agree.
    // Rules and the comparison with the prototype and the original are in docs/study/06-damage-and-respawn.md.
    public class HeroHealth : MonoBehaviour, IHittable
    {
        const float TimeEpsilon = 1e-4f;

        // PROVISIONAL: the prototype's delay (the original reloads a scene behind a death screen, no number).
        // A negative value turns the automatic respawn off. Only used when a marker is known.
        [SerializeField] float deathRespawnDelaySeconds = 1.6f;

        HeroController owner;
        PlayerData data;

        bool dead;
        float invulnerableRemaining;
        float stunRemaining;
        // Explicit flag: the float countdown can undershoot zero, and the end-of-stun hand-back must still run.
        bool stunActive;
        float knockDirection = 1f;
        float deathElapsed;

        public float DeathRespawnDelaySeconds
        {
            get => deathRespawnDelaySeconds;
            set => deathRespawnDelaySeconds = value;
        }

        // The save slot used when a respawn is stored. Negative (the default) means do not write a file: the
        // scene that wires up the real game sets it (GameSession.StartGame uses the selected slot). Nothing saves from
        // the death itself: the respawn saves after the refill, so a slot never holds health 0.
        public int SaveSlot { get; set; } = -1;

        // The hero's player data. Created from the new-game defaults when nobody supplies one.
        public PlayerData Data
        {
            get => data;
            set => data = value ?? PlayerData.CreateNew();
        }

        public int Health => data.health;
        public int MaxHealth => data.maxHealth;
        public bool IsDead => dead;
        public bool IsInvulnerable => invulnerableRemaining > 0f;
        // True while the knockback is forcing the hero's velocity (the hurt state).
        public bool IsStunned => stunActive;
        public float InvulnerableRemaining => Mathf.Max(0f, invulnerableRemaining);
        public float StunRemaining => Mathf.Max(0f, stunRemaining);
        // The last marker the hero respawned at; used by the automatic respawn after a death.
        public RespawnMarker CurrentMarker { get; private set; }

        // Additive (Milestone 2 task 8). When set, it is called ONCE when the death delay has passed, INSTEAD of the
        // built-in "respawn at the marker in the live scenes" lookup. The world sets it so a death can send the hero to
        // a bench in another room. Null (the default) keeps the old behaviour for every other scene.
        public Action DeathRespawnHandler { get; set; }

        // Additive (task 8). A hazard hit that does not kill: the hero is put back at a spot of the current room.
        // Health stays as the hit left it; the hit's invulnerability window is kept; the knockback stun ends and the
        // hero is stopped and made to fall at the spot. Not a respawn: markers and player respawn data are untouched.
        public void PlaceAtHazard(Vector2 position, bool facingRight)
        {
            if (owner == null || dead) return;
            stunRemaining = 0f;
            stunActive = false;
            CancelActions(true);
            owner.Body.position = position;
            owner.transform.position = new Vector3(position.x, position.y, owner.transform.position.z);
            owner.Body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            owner.IsGrounded = false;
            owner.FacingRight = facingRight;
            owner.State = HeroState.Falling;
            Stubs.NotImplemented("Hero hazard respawn fade and recovery animation");
        }

        // Set by HeroTransitionDriver from the start of leaving a room until the hero has finished entering the next
        // one (the original's NO_DAMAGE mode). Not a timer, so it is kept apart from the hurt invulnerability.
        internal bool TransitionInvulnerable { get; set; }

        // Additive (Milestone 4 task 5). The downspike grace: for a few steps after a downward hit that bounced, enemy
        // contact must not hurt the hero (the original's DOWNSPIKE_INVULNERABILITY_STEPS). It is NOT part of
        // CanTakeDamage: only enemy contact (EnemyContactDamage) consults it, so spikes and other hazards still hurt.
        // The counter ticks down at the start of each controller step.
        int contactGraceSteps;
        public bool IsInContactGrace => contactGraceSteps > 0;
        public int ContactGraceStepsRemaining => contactGraceSteps;
        public void BeginContactGrace(int steps) => contactGraceSteps = Mathf.Max(contactGraceSteps, steps);

        public bool CanTakeDamage =>!dead && invulnerableRemaining <= 0f && !stunActive && !TransitionInvulnerable;

        public event Action Damaged;
        public event Action Died;
        public event Action Respawned;

        internal void Bind(HeroController controller)
        {
            owner = controller;
            if (data == null) data = PlayerData.CreateNew();
        }

        // A hit on the hero. Returns whether it landed (false while invulnerable, stunned or dead, for a hit that
        // deals no damage, and for the hero's own attack: the hit instance the hero hands out names the hero as
        // its source, so it is refused here as well as never being offered the hero by HeroAttack).
        public bool TakeHit(HitInstance hit)
        {
            if (owner == null || owner.Config == null) return false;
            if (hit.Damage <= 0) return false;
            if (IsOwnHit(hit)) return false;
            if (!CanTakeDamage) return false;

            var cfg = owner.Config;
            var body = owner.Body;
            data.health = Mathf.Max(0, data.health - hit.Damage);

            // Away from the source along x; with no source, away from the way the hero faces.
            if (hit.Source != null) knockDirection = body.position.x >= hit.Source.transform.position.x ? 1f : -1f;
            else knockDirection = owner.FacingRight ? -1f : 1f;
            // The hero turns to face what hit it, like the original.
            owner.FacingRight = knockDirection < 0f;

            CancelActions();
            owner.IsGrounded = false;

            // Exists in the original and is not built (the hero's hit-stop stays off by owner decision), plus the
            // sound, effects and the flicker.
            Stubs.NotImplemented("Hero damage hit-stop");
            Stubs.NotImplemented("Hero damage effects and sound");

            if (data.health <= 0)
            {
                EnterDeath();
                Damaged?.Invoke();
                Died?.Invoke();
                return true;
            }

            // Invulnerability starts at the hit and includes the stun (the original starts it with the recoil).
            invulnerableRemaining = cfg.HurtInvulnerableSeconds;
            Stubs.NotImplemented("Hero invulnerability flicker");
            stunRemaining = cfg.HurtStunSeconds;
            stunActive = stunRemaining > TimeEpsilon;
            if (stunActive)
            {
                owner.State = HeroState.Hurt;
                body.linearVelocity = KnockbackVelocity(cfg);
                owner.Animator?.Play("Hurt");
            }
            else
            {
                stunRemaining = 0f;
            }
            Damaged?.Invoke();
            return true;
        }

        // Puts the hero back at the marker with full health and nothing left over from before: no dash, cling,
        // swing, bounce, jump, stun, invulnerability or death. Also remembers the marker in the player data.
        public void RespawnAt(RespawnMarker marker)
        {
            if (marker == null || owner == null)
            {
                Debug.LogWarning("[Hero] RespawnAt needs a marker.");
                return;
            }

            CancelActions(true);
            owner.Dash.ResetForRespawn();
            owner.Attack.ResetForRespawn();
            // A respawn mid-transition (hazard in the old room) ends the lock and the transition invulnerability.
            owner.Transition?.RestoreControl();

            dead = false;
            invulnerableRemaining = 0f;
            stunRemaining = 0f;
            stunActive = false;
            deathElapsed = 0f;
            data.health = data.maxHealth;

            Vector2 pos = marker.transform.position;
            owner.Body.position = pos;
            owner.transform.position = new Vector3(pos.x, pos.y, owner.transform.position.z);
            owner.Body.linearVelocity = Vector2.zero;
            owner.IsGrounded = false;
            owner.FacingRight = marker.FacingRight;
            owner.State = HeroState.Falling;

            CurrentMarker = marker;
            data.respawnMarker = marker.MarkerName;
            string scene = marker.gameObject.scene.name;
            if (!string.IsNullOrEmpty(scene)) data.respawnScene = scene;
            if (SaveSlot >= 0) SaveSystem.TrySave(data, SaveSlot);

            Stubs.NotImplemented("Hero respawn fade and bench pose");
            Respawned?.Invoke();
        }

        // First thing in every controller step. Returns true when health owns the hero's velocity this step
        // (stunned or dead), so the controller must not run HeroMovement.
        internal bool FixedStep(float dt)
        {
            if (contactGraceSteps > 0) contactGraceSteps--;
            if (invulnerableRemaining > 0f)
            {
                invulnerableRemaining -= dt;
                if (invulnerableRemaining <= TimeEpsilon) invulnerableRemaining = 0f;
            }

            if (dead)
            {
                owner.Body.linearVelocity = Vector2.zero;
                if (deathRespawnDelaySeconds >= 0f)
                {
                    deathElapsed += dt;
                    if (deathElapsed + TimeEpsilon >= deathRespawnDelaySeconds)
                    {
                        if (DeathRespawnHandler != null)
                        {
                            // Called once; the handler (or its later transition) owns the respawn from here.
                            deathElapsed = float.NegativeInfinity;
                            DeathRespawnHandler();
                            return dead;
                        }
                        var marker = CurrentMarker != null ? CurrentMarker : RespawnMarker.Find(data.respawnMarker);
                        if (marker != null)
                        {
                            RespawnAt(marker);
                            return false;
                        }
                        // Nowhere to go: stay dead (the delay has passed, so do not search every step).
                        deathElapsed = float.NegativeInfinity;
                    }
                }
                return true;
            }

            if (!stunActive) return false;

            // The stun ends on the step after its last forced step, and movement runs in that same step.
            if (stunRemaining <= TimeEpsilon)
            {
                stunRemaining = 0f;
                stunActive = false;
                // The original stops the hero dead at the end of the recoil, then gravity takes over again.
                owner.Body.linearVelocity = Vector2.zero;
                owner.State = HeroState.Falling;
                return false;
            }

            owner.Body.linearVelocity = KnockbackVelocity(owner.Config);
            owner.State = HeroState.Hurt;
            stunRemaining -= dt;
            return true;
        }

        Vector2 KnockbackVelocity(HeroConfig cfg) => new Vector2(knockDirection * cfg.HurtKnockbackX, cfg.HurtKnockbackY);

        bool IsOwnHit(HitInstance hit)
        {
            return hit.Source != null && (hit.Source == gameObject || hit.Source.transform.IsChildOf(transform));
        }

        // Ends everything the hero was doing. Cooldowns keep running (a hit); respawn clears them separately.
        void CancelActions(bool forgetGround = false)
        {
            owner.Dash.CancelForDamage();
            owner.Wall.ResetForDamage();
            owner.Attack.CancelForDamage();
            owner.ResetMovement(forgetGround);
        }

        void EnterDeath()
        {
            dead = true;
            stunRemaining = 0f;
            stunActive = false;
            invulnerableRemaining = 0f;
            deathElapsed = 0f;
            owner.State = HeroState.Dead;
            owner.Transition?.RestoreControl();
            owner.Body.linearVelocity = Vector2.zero;
            owner.Animator?.Play("Death");
            // Exist in the original and are not built.
            Stubs.NotImplemented("Hero death animation and corpse");
            Stubs.NotImplemented("Hero death screen");
            Stubs.NotImplemented("Hero silk loss and cocoon on death");
            Stubs.NotImplemented("Save at the moment of death (the original writes before the refill; this port saves in RespawnAt, after it)");
        }

        // A disabled hero comes back alive, unhurt and not invulnerable; a Hurt or Dead state must not outlive
        // the component that owns it (movement no longer overwrites those states).
        void OnDisable()
        {
            dead = false;
            contactGraceSteps = 0;
            invulnerableRemaining = 0f;
            stunRemaining = 0f;
            stunActive = false;
            deathElapsed = 0f;
            if (owner != null && owner.Transition != null) owner.Transition.RestoreControl();
            if (owner != null && (owner.State == HeroState.Hurt || owner.State == HeroState.Dead))
                owner.State = HeroState.Falling;
        }
    }
}
