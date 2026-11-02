using System.Collections;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // What the transition code needs from the hero. Implemented by the hero side in Task 4
    // (Hero/HeroTransitionDriver); tests use a fake. Walk speeds, timings and invulnerability are the hero's
    // own business and live behind these methods, never in the room code.
    public interface ITransitionHero
    {
        // Facts the gate rules read when the hero touches a gate. A snapshot, so the rules stay pure.
        HeroTransitionState GetTransitionState();

        // The hero's body, so a gate can stop it and move it out of the gate box on push-back.
        Rigidbody2D Body { get; }

        // True while the hero ignores player input (set from the first touch until entering has finished).
        bool ControlLocked { get; }

        // Takes input away and starts the walk-out through a gate on this side. Called when a request starts.
        void LeaveRoom(GatePosition gate);

        // Places the hero at the arrival gate (plus the gate's entry offset), then walks it in and hands the
        // controls back. Runs as a coroutine so the manager can wait for it. delay is the extra wait before
        // walking in.
        IEnumerator EnterRoom(TransitionPoint gate, float delay);
    }
}
