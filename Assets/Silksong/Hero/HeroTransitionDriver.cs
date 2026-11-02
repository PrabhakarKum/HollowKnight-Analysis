using System.Collections;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // Optional extra for a transition hero: lets the room manager give the controls back and clear invulnerability if
    // a transition is abandoned half way (a failed load, a cancel). Declared by the transition manager task as well
    // under this exact name; the duplicate is removed when the branches are merged.
    public interface ITransitionHeroRecovery
    {
        void RestoreControl();
    }

    // The hero's side of changing rooms: walking out of a room (LeaveRoom) and being placed and walked into the next
    // one (EnterRoom). Rules and numbers come from docs/study/10-room-transitions.md section 3; the tuning numbers the
    // hero owns are HeroConfig.Transition* fields.
    //
    // How it works with the rest of the hero:
    //  - The controls are locked through HeroController.ControlLocked. While locked, HeroController.Input returns this
    //    driver's scripted input (all buttons up, Move set by the driver), so dash, wall, attack and movement all
    //    ignore the player without any lock check of their own and keep their normal step order. Walking out or in is
    //    therefore just "scripted Move = left or right": HeroMovement then runs at run speed, applies gravity and plays
    //    Run, exactly as for a player holding the stick.
    //  - Only the vertical cases (leaving upward, hovering before a top drop-in, the bottom-gate throw) need the
    //    driver to set the velocity itself; FixedStep then returns true and the controller skips movement and wall,
    //    the same bypass the dash uses.
    //  - Time is counted in fixed steps from HeroController.StepFixed (seconds reduced by dt), never with wall-clock
    //    waits, so 50 Hz and 100 Hz agree and tests can step it deterministically.
    //  - The hero cannot be hurt from LeaveRoom until the entering has finished (HeroHealth.TransitionInvulnerable).
    // The 0.165 s wait before fading in belongs to the room manager; EnterRoom's delay is only the extra wait.
    public class HeroTransitionDriver : MonoBehaviour, ITransitionHero, ITransitionHeroRecovery
    {
        const float TimeEpsilon = 1e-4f;
        // How far below the arrival spot the ground is searched for. Rooms are far smaller than this.
        const float GroundSearchDistance = 100f;
        // Same floor rule as HeroMovement: steeper surfaces are walls.
        const float MinFloorNormalY = 0.7f;
        // Safety cap for the walk-out: the room manager normally replaces it with EnterRoom within a second or two
        // (fade plus load). If nothing ever arrives (failed load, manager gone) the hero must not stay locked and
        // unhurtable forever, so after this long the lock is dropped with an error. Not a HeroConfig value because
        // it is a guard, not tuning.
        const float LeaveTimeoutSeconds = 5f;
        // An arrival spot that lies inside the floor is lifted by this much per try, at most this many times (2 units
        // in all), before giving up with a warning.
        const float EmbeddedLiftStep = 0.25f;
        const int MaxEmbeddedLiftTries = 8;

        enum Phase { None, Leaving, Waiting, WalkingIn, Dropping, Thrown }

        // Buttons stay up; only Move is scripted.
        sealed class ScriptedHeroInput : IHeroInput
        {
            public Vector2 Move { get; set; }
            public InputButton Jump => default;
            public InputButton Dash => default;
            public InputButton Attack => default;
        }

        readonly ScriptedHeroInput scripted = new ScriptedHeroInput();
        readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        readonly Collider2D[] overlaps = new Collider2D[4];
        float leaveElapsed;

        HeroController owner;
        Phase phase;
        float remaining;
        GatePosition gateSide = GatePosition.Unknown;
        // Which way a left/right walk or a bottom throw goes: +1 or -1 in world x.
        float walkDirection = 1f;
        bool inPosition = true;

        internal IHeroInput ScriptedInput => scripted;

        public Rigidbody2D Body => owner != null ? owner.Body : null;
        public bool ControlLocked => owner != null && owner.ControlLocked;
        // True from LeaveRoom until the entering has finished (or RestoreControl).
        public bool IsInvulnerable => owner != null && owner.Health != null && owner.Health.TransitionInvulnerable;
        public bool IsTransitioning => phase != Phase.None;

        internal void Bind(HeroController controller)
        {
            owner = controller;
        }

        public HeroTransitionState GetTransitionState()
        {
            var health = owner.Health;
            return new HeroTransitionState
            {
                // HeroController.FacingRight already means world +x. (The source reads the sprite scale, where a
                // negative x scale means right for Hornet's art; this hero does not flip that way, so no mapping.)
                FacingRight = owner.FacingRight,
                // Back-sprinting does not exist in this port.
                BackSprinting = false,
                // Stunned by a hit or dead: the original's recoil/cutscene "busy" cases. A locked hero is NOT busy,
                // or the bottom-gate exception on arrival could never fire.
                Busy = health != null && (health.IsStunned || health.IsDead),
                VelocityY = owner.Body.linearVelocity.y,
                InPosition = inPosition
            };
        }

        // Takes the controls and hero's vulnerability away and starts walking out through this side.
        public void LeaveRoom(GatePosition gate)
        {
            if (!CanLock()) return;
            Lock();
            leaveElapsed = 0f;
            if (gate == GatePosition.Top)
            {
                // Played once here, not every step.
                owner.IsGrounded = false;
                owner.State = HeroState.Rising;
                owner.Animator?.Play("Jump");
            }
            inPosition = false;
            phase = Phase.Leaving;
            gateSide = gate;
            switch (gate)
            {
                case GatePosition.Right: walkDirection = 1f; break;
                case GatePosition.Left: walkDirection = -1f; break;
                case GatePosition.Door:
                case GatePosition.Unknown:
                    Stubs.NotImplemented("Hero leaving through a door or unknown gate");
                    break;
            }
            // The original remembers a dash or sprint in progress and re-enters in that state; the study says skip it
            // here: Lock() just ends the dash.
        }

        public IEnumerator EnterRoom(TransitionPoint gate, float delay)
        {
            if (gate == null)
            {
                Debug.LogWarning("[Hero] EnterRoom needs an arrival gate.");
                RestoreControl();
                yield break;
            }

            if (!CanLock()) yield break;
            Lock();
            gateSide = gate.Position;
            Vector2 spot = (Vector2)gate.transform.position + gate.EntryOffset;
            Place(gateSide, spot);
            inPosition = true;

            remaining = Mathf.Max(0f, delay);
            phase = Phase.Waiting;
            // Control comes back inside HeroController.StepFixed; this just lets the manager wait for it.
            while (phase != Phase.None) yield return null;
        }

        // Gives the controls back and clears the invulnerability. Safe to call at any time.
        public void RestoreControl()
        {
            phase = Phase.None;
            scripted.Move = Vector2.zero;
            inPosition = true;
            if (owner == null) return;
            owner.ControlLocked = false;
            if (owner.Health != null) owner.Health.TransitionInvulnerable = false;
        }

        // Called first in every HeroController step. Returns true when the driver set the velocity itself and
        // movement/wall must not run this step.
        internal bool FixedStep(float dt)
        {
            bool owned = Tick(dt);
            // When the driver sets the velocity, HeroMovement does not run and would leave the last Wants* flags stale.
            if (owned) ClearIntent();
            return owned;
        }

        void ClearIntent()
        {
            owner.WantsLeft = owner.WantsRight = owner.WantsUp = owner.WantsDown = false;
        }

        bool Tick(float dt)
        {
            if (phase == Phase.None) return false;

            switch (phase)
            {
                case Phase.Leaving:
                    leaveElapsed += dt;
                    if (leaveElapsed > LeaveTimeoutSeconds)
                    {
                        Debug.LogError("[Hero] LeaveRoom was never followed by EnterRoom after " + LeaveTimeoutSeconds
                            + " s; giving the controls back.");
                        RestoreControl();
                        return false;
                    }
                    return LeaveStep();

                case Phase.Waiting:
                    if (remaining > TimeEpsilon)
                    {
                        remaining -= dt;
                        scripted.Move = Vector2.zero;
                        // Standing on a side gate needs nothing; above a top or bottom gate the hero hovers until the
                        // wait ends (gravity off, like the original).
                        if (gateSide == GatePosition.Top || gateSide == GatePosition.Bottom)
                        {
                            owner.Body.linearVelocity = Vector2.zero;
                            return true;
                        }
                        return false;
                    }
                    BeginMovePhase();
                    return Tick(dt);

                case Phase.WalkingIn:
                    if (remaining <= TimeEpsilon) { Finish(); return false; }
                    remaining -= dt;
                    scripted.Move = new Vector2(walkDirection, 0f);
                    return false;

                case Phase.Dropping:
                    if (remaining <= TimeEpsilon) { Finish(); return false; }
                    remaining -= dt;
                    scripted.Move = Vector2.zero;
                    return false;   // gravity and landing are HeroMovement's

                case Phase.Thrown:
                    if (remaining <= TimeEpsilon) { Finish(); return false; }
                    remaining -= dt;
                    var cfg = owner.Config;
                    owner.Body.linearVelocity = new Vector2(walkDirection * cfg.TransitionBottomSpeedX, cfg.TransitionBottomSpeedY);
                    owner.State = HeroState.Rising;
                    owner.IsGrounded = false;
                    return true;
            }
            return false;
        }

        bool LeaveStep()
        {
            switch (gateSide)
            {
                case GatePosition.Right:
                case GatePosition.Left:
                    // The movement code does the rest: run speed, gravity, facing and the Run clip. (The original keeps
                    // a faster speed if the hero already had one; the dash is cancelled here, so that case is dropped.)
                    scripted.Move = new Vector2(walkDirection, 0f);
                    return false;
                case GatePosition.Top:
                    scripted.Move = Vector2.zero;
                    owner.Body.linearVelocity = new Vector2(0f, owner.Config.TransitionLeaveUpSpeed);
                    owner.State = HeroState.Rising;
                    owner.IsGrounded = false;
                    ClearIntent();
                    return true;
                default:
                    // Bottom: the original sets zero velocity; here the hero just keeps falling under gravity, which
                    // is what carried it into the gate anyway. Doors and unknown gates also do nothing special.
                    scripted.Move = Vector2.zero;
                    return false;
            }
        }

        // The wait is over: start the move that fits the arrival gate.
        void BeginMovePhase()
        {
            var cfg = owner.Config;
            switch (gateSide)
            {
                case GatePosition.Left:
                case GatePosition.Right:
                    remaining = cfg.TransitionWalkInSeconds;
                    phase = Phase.WalkingIn;
                    break;
                case GatePosition.Top:
                    owner.Body.linearVelocity = new Vector2(0f, cfg.TransitionDropSpeed);
                    remaining = cfg.TransitionDropSeconds;
                    phase = Phase.Dropping;
                    break;
                case GatePosition.Bottom:
                    // Thrown the way the hero already faces.
                    walkDirection = owner.FacingRight ? 1f : -1f;
                    remaining = cfg.TransitionBottomSeconds;
                    phase = Phase.Thrown;
                    break;
                default:
                    // Doors and unknown gates: no walk, control returns straight away.
                    remaining = 0f;
                    phase = Phase.WalkingIn;
                    break;
            }
        }

        // Puts the hero at the arrival spot and wipes what it carried over from the old room.
        void Place(GatePosition side, Vector2 spot)
        {
            var body = owner.Body;
            owner.ResetMovement(true);
            body.linearVelocity = Vector2.zero;
            scripted.Move = Vector2.zero;

            switch (side)
            {
                case GatePosition.Left:
                case GatePosition.Right:
                    // Arriving at a left gate you face right (inward), and the other way round.
                    walkDirection = side == GatePosition.Left ? 1f : -1f;
                    owner.FacingRight = walkDirection > 0f;
                    SetPosition(SnapToGround(spot));
                    owner.IsGrounded = true;
                    owner.State = HeroState.Grounded;
                    owner.Animator?.Play("Idle");
                    break;
                case GatePosition.Top:
                    SetPosition(spot);
                    // The gate's hardLandOnExit flag (a hard landing after the drop) is not built; the landing after
                    // the drop is the normal one.
                    Stubs.NotImplemented("Gate hardLandOnExit (hard landing after a top-gate arrival)");
                    owner.IsGrounded = false;
                    owner.State = HeroState.Falling;
                    owner.Animator?.Play("Fall");
                    break;
                case GatePosition.Bottom:
                    // The gate's alwaysEnterLeft / alwaysEnterRight (forced facing at a bottom gate) are not built; the
                    // hero is thrown the way it already faces.
                    Stubs.NotImplemented("Gate alwaysEnterLeft/alwaysEnterRight facing at bottom gates");
                    SetPosition(spot + new Vector2(0f, owner.Config.TransitionBottomLift));
                    owner.IsGrounded = false;
                    owner.State = HeroState.Rising;
                    owner.Animator?.Play("Jump");
                    break;
                default:
                    Stubs.NotImplemented("Hero entering through a door or unknown gate");
                    SetPosition(SnapToGround(spot));
                    owner.IsGrounded = true;
                    owner.State = HeroState.Grounded;
                    break;
            }
            // Hiding the hero sprite until the fade-up starts, and the arrival sound/dust, exist in the original.
        }

        void SetPosition(Vector2 p)
        {
            var t = owner.transform;
            t.position = new Vector3(p.x, p.y, t.position.z);
            owner.Body.position = p;
            // Collider casts read the physics copy of the transform, so sync before asking the world anything.
            Physics2D.SyncTransforms();
        }

        // Moves the arrival spot straight down onto the first floor below it (the original looks for ground near
        // the gate too). With no floor found the spot is used as it is and the hero simply falls.
        Vector2 SnapToGround(Vector2 spot)
        {
            SetPosition(spot);
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = owner.GroundLayers, useTriggers = false };
            // A spot inside the floor would make the cast report distance 0 and leave the hero embedded, so lift it
            // out first (bounded). Only a real overlap counts: a hero resting exactly on the floor does not overlap.
            int tries = 0;
            while (owner.BodyCollider.Overlap(filter, overlaps) > 0 && tries < MaxEmbeddedLiftTries)
            {
                spot += Vector2.up * EmbeddedLiftStep;
                SetPosition(spot);
                tries++;
            }
            if (tries == MaxEmbeddedLiftTries && owner.BodyCollider.Overlap(filter, overlaps) > 0)
            {
                Debug.LogWarning("[Hero] The arrival spot is inside solid ground and could not be lifted clear.");
                return spot;
            }
            int count = owner.BodyCollider.Cast(Vector2.down, filter, hits, GroundSearchDistance);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
                if (hits[i].normal.y >= MinFloorNormalY && hits[i].distance < best) best = hits[i].distance;
            return best < float.MaxValue ? spot + Vector2.down * best : spot;
        }

        // A transition needs the hero's input and config to run (HeroController.StepFixed does nothing without a
        // config, and without input the hero would never tick). Refuse up front rather than lock a hero that can never
        // be unlocked.
        bool CanLock()
        {
            if (owner != null && owner.Config != null && owner.HasInput) return true;
            Debug.LogError("[Hero] Cannot start a room transition: the hero has no input or config.");
            return false;
        }

        // Wipes dash, cling, swing and jump, takes the controls, makes the hero unhurtable.
        void Lock()
        {
            if (owner.Dash.IsDashing) Stubs.NotImplemented("Hero dash and sprint re-entry after a room change");
            owner.Dash.CancelForDamage();
            owner.Wall.ResetForDamage();
            owner.Attack.CancelForDamage();
            owner.ResetMovement(false);
            scripted.Move = Vector2.zero;
            ClearIntent();
            owner.ControlLocked = true;
            owner.Health.TransitionInvulnerable = true;
        }

        void Finish()
        {
            RestoreControl();
        }

        // A disabled hero must not stay locked or unhurtable.
        void OnDisable()
        {
            RestoreControl();
        }
    }
}
