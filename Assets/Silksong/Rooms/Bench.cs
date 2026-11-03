using System;
using System.Collections;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // A bench: a trigger on a RespawnMarker object. When the hero stands still inside it and presses up, the hero rests.
    // Rest, in order: the bench becomes the death respawn, health is filled, the game is saved (into the selected
    // slot; nothing happens when no slot is selected), the screen fades out and back in. Control stays locked for the
    // whole rest, through the same lock the room transition uses (HeroController.ControlLocked).
    // The order of these steps is inferred, see docs/study/13d-bench-rest.md.
    //
    // Silk is not touched: what a bench does to silk in the original is not known.
    // Stubbed: rest pose and animation, sound.
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(RespawnMarker))]
    public class Bench : MonoBehaviour
    {
        HeroController hero;
        Collider2D benchCollider;
        RespawnMarker marker;
        bool wasUp;
        bool resting;
        float cooldown;
        bool heroInside;
        bool lockedByBench;

        public bool IsResting => resting;
        public int RestCount { get; private set; }
        // Times a rest wrote a save file (stays 0 with no selected slot).
        public int SavesWritten { get; private set; }

        // Raised once the fade-in has finished and control is back.
        public event Action Rested;

        void Awake()
        {
            benchCollider = GetComponent<Collider2D>();
            marker = GetComponent<RespawnMarker>();
        }

        void OnTriggerEnter2D(Collider2D other) => Touch(other);
        void OnTriggerStay2D(Collider2D other) => Touch(other);

        void Touch(Collider2D other)
        {
            var h = other.GetComponentInParent<HeroController>();
            if (h != null) { hero = h; heroInside = true; }
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<HeroController>() == hero) heroInside = false;
        }

        void OnDisable()
        {
            heroInside = false;
            if (resting) AbortRest();
        }

        // A sleeping body gets no stay events, so the overlap is also checked directly each step.
        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        // One decision step; public so tests with manual stepping can call it after each physics step.
        public void Tick(float dt)
        {
            if (cooldown > 0f) cooldown -= dt;
            if (hero == null || hero.Config == null) return;
            var col = hero.GetComponent<Collider2D>();
            if (col != null && benchCollider != null)
                heroInside = heroInside && benchCollider.Distance(col).isOverlapped;

            // While the bench or a transition has the controls the input reads as released; keep the last real
            // reading so a held up does not count as a new press when control comes back.
            if (hero.ControlLocked) return;

            float threshold = hero.Config.VerticalPressThreshold;
            float y = hero.Input != null ? hero.Input.Move.y : 0f;
            bool edge = BenchRules.IsUpEdge(wasUp, y, threshold);
            wasUp = BenchRules.IsUp(y, threshold);
            if (!edge) return;

            var health = hero.Health;
            var world = RoomWorldBootstrap.Instance;
            bool transitioning = world != null && world.Manager != null && world.Manager.IsTransitionRunning;
            if (!BenchRules.CanRest(heroInside, hero.State, hero.IsGrounded, hero.Velocity.x,
                    health != null && health.IsDead, hero.ControlLocked, transitioning, resting, cooldown))
                return;

            StartCoroutine(Rest(world));
        }

        IEnumerator Rest(RoomWorldBootstrap world)
        {
            resting = true;
            cooldown = BenchRules.CooldownSeconds;
            wasUp = true;   // still held: not a new press afterwards
            var health = hero.Health;
            hero.ResetMovement(false);
            hero.ControlLocked = true;
            lockedByBench = true;
            if (health != null) health.TransitionInvulnerable = true;
            if (hero.Body != null) hero.Body.linearVelocity = Vector2.zero;
            Stubs.NotImplemented("Bench rest pose and animation");
            Stubs.NotImplemented("Bench rest sound");

            // 1. the bench becomes the respawn  2. health is full  (silk is left alone)
            PlayerData data = health != null ? health.Data : (world != null ? world.PlayerData : null);
            if (world != null) world.SetBenchRespawn(marker);
            else if (data != null)
            {
                data.respawnScene = gameObject.scene.name;
                data.respawnMarker = marker.MarkerName;
            }
            if (data != null) data.health = data.maxHealth;

            // 3. save AFTER the changes. No selected slot: SaveSlots.Autosave does nothing.
            if (SaveSlots.Autosave(data)) SavesWritten++;

            // 4. fade out and in with the room-change fader
            var fader = world != null ? world.Fader : null;
            if (fader != null) yield return fader.FadeOut();
            if (fader != null) yield return fader.FadeIn();

            EndLock();
            resting = false;
            RestCount++;
            Rested?.Invoke();
        }

        void EndLock()
        {
            if (!lockedByBench) return;
            lockedByBench = false;
            if (hero == null) return;
            hero.ControlLocked = false;
            if (hero.Health != null) hero.Health.TransitionInvulnerable = false;
        }

        void AbortRest()
        {
            StopAllCoroutines();
            EndLock();
            resting = false;
        }
    }
}
