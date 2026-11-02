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

        // True from accepting a request until the new room is loaded and activated, i.e. until State
        // becomes EnteringLevel. It must NOT stay true through the entering phase: that phase is carried by
        // State == EnteringLevel, and gates need this false then so a bottom gate can be left straight away
        // (TransitionRules checks this flag before the EnteringLevel exception).
        bool IsTransitionRunning { get; }

        // Starts the room change. The handler must ignore a request while IsTransitionRunning is true.
        // A gate marks itself activated when it sends a request; if the handler rejects it, call
        // TransitionPoint.ResetActivation() on that gate or it will never fire again.
        void RequestTransition(TransitionRequest request);
    }
}
