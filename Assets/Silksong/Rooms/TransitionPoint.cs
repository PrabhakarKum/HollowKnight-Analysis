using UnityEngine;

namespace Hollowknight.Silksong
{
    // A gate at a room edge: a trigger box that sends the hero to another room, and also the spot where the
    // hero appears when arriving from the other side. Which edge it is comes from the object's NAME
    // ("left1", "right2", "top1", "bot2", "door1"), like in the original.
    //
    // Not built yet (original features): doors, audio snapshots, custom fade/entry/cutscene hooks,
    // alwaysEnterLeft/Right, hard landing, nonHazardGate/respawnMarker (comes with the hazard task).
    [RequireComponent(typeof(Collider2D))]
    public class TransitionPoint : MonoBehaviour
    {
        // Set by RoomTransitionManager (Task 3) when it exists. Static because gates live in room scenes that
        // are loaded and unloaded around the manager, so they cannot hold a scene reference to it.
        public static ITransitionRequestHandler Handler { get; set; }

        [SerializeField] string targetScene;
        // The original names this field entryPoint. Renamed to entryGate because it is the name of a gate
        // in the target room.
        [SerializeField] string entryGate;
        // Shift added to the arrival spot (Tut_01b right1 uses (-1, 0) to land inside the next room).
        [SerializeField] Vector2 entryOffset;
        // Extra seconds to wait before walking in, when this gate is the arrival gate.
        [SerializeField] float entryDelay;

        // Set once this gate fired so OnTriggerStay does not send the request every physics step.
        bool activated;

        public string TargetScene => targetScene;
        public string EntryGate => entryGate;
        public Vector2 EntryOffset => entryOffset;
        public float EntryDelay => entryDelay;
        public bool HasTarget => !string.IsNullOrEmpty(targetScene) && !string.IsNullOrEmpty(entryGate);

        // Read on demand, not cached, so renaming the object in the editor or by code takes effect.
        public GatePosition Position => GatePositions.FromName(name);

        // For gates built from code (tests, generated rooms).
        public void Configure(string target, string arrivalGate, Vector2 offset, float delay = 0f)
        {
            targetScene = target;
            entryGate = arrivalGate;
            entryOffset = offset;
            entryDelay = delay;
        }

        public TransitionRequest BuildRequest() => new TransitionRequest
        {
            TargetScene = targetScene,
            EntryGate = entryGate,
            LeaveDirection = Position,
            EntryDelay = entryDelay
        };

        public void ResetActivation() => activated = false;

        // Applies the rules and, on Leave, sends the request. Returns what was decided; the caller does the
        // physical push-back. Separate from the collider callbacks so it can be tested without physics.
        public TransitionDecision Touch(ITransitionRequestHandler handler, HeroTransitionState hero)
        {
            if (handler == null || activated) return TransitionDecision.Ignore;

            if (Position == GatePosition.Door) Stubs.NotImplemented("TransitionPoint.Door");

            var decision = TransitionRules.Evaluate(Position, handler.State, handler.IsTransitionRunning, HasTarget, hero);
            if (decision == TransitionDecision.Leave)
            {
                activated = true;
                handler.RequestTransition(BuildRequest());
            }
            return decision;
        }

        void OnEnable()
        {
            GateRegistry.Register(this);
        }

        void OnDisable()
        {
            GateRegistry.Unregister(this);
        }

        void OnTriggerEnter2D(Collider2D other) => OnHeroTouch(other);

        void OnTriggerStay2D(Collider2D other) => OnHeroTouch(other);

        void OnHeroTouch(Collider2D other)
        {
            // The hero is recognised by its ITransitionHero component rather than a physics layer, so no
            // project layer setting is needed.
            var hero = other.GetComponentInParent<ITransitionHero>();
            if (hero == null) return;

            var decision = Touch(Handler, hero.GetTransitionState());
            if (decision == TransitionDecision.PushBack) PushBack(hero, other);
        }

        // Stops the hero and moves it just outside the gate box so it cannot keep standing inside.
        void PushBack(ITransitionHero hero, Collider2D heroCollider)
        {
            var body = hero.Body;
            if (body != null)
            {
                var v = body.linearVelocity;
                if (Position == GatePosition.Left || Position == GatePosition.Right) v.x = 0f;
                else v.y = 0f;
                body.linearVelocity = v;
            }

            var gateCollider = GetComponent<Collider2D>();
            Vector2 offset = TransitionRules.PushBackOffset(Position, gateCollider.bounds, heroCollider.bounds);
            heroCollider.transform.Translate(offset, Space.World);
        }
    }
}
