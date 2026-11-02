namespace Hollowknight.Silksong
{
    // Everything a gate asks for when the hero walks into it. A plain value so the manager can queue,
    // log or compare it. Mirrors the useful part of the original SceneLoadInfo.
    public struct TransitionRequest
    {
        public string TargetScene;
        // Name of the gate in the target room where the hero appears. The original calls this entryPoint.
        public string EntryGate;
        // The sending gate's side; the hero walks out that way.
        public GatePosition LeaveDirection;
        // Extra seconds to wait before walking in (sender-side delay in the original).
        public float EntryDelay;
    }

    // The receiving end of a gate touch. Implemented by RoomTransitionManager (Task 3). Gates find it through
    // TransitionPoint.Handler, so gates never reference the manager class and tests can plug in a fake.
    public interface ITransitionRequestHandler
    {
        // Current game state; gates only fire while Playing (see TransitionRules).
        GameState State { get; }

        // True from accepting a request until the hero has finished entering the next room.
        bool IsTransitionRunning { get; }

        // Starts the room change. The handler must ignore a request while one is already running.
        void RequestTransition(TransitionRequest request);
    }
}
