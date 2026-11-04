using System;

namespace Hollowknight.Silksong
{
    // Hero and enemy code talk to this instead of Unity's Animator, so the animation backend
    // (the original used tk2d) can change without touching gameplay code.
    public interface ISpriteAnimator
    {
        // Start a clip from its first frame. Safe to call every frame from a state machine:
        //  - the same LOOPING clip already playing: no-op (it keeps its current frame);
        //  - a non-looping clip: always restarts, including after it has finished.
        // Frame events on the first frame fire (see PlayFromFrame).
        void Play(string clip);

        // Same as Play(clip) but the clip runs at a multiple of its own speed (1 = natural). Speed stays
        // for the whole clip, including a loop continuation; any later plain Play resets it to 1.
        // Default body ignores the speed, so animators that cannot change speed need no change.
        void Play(string clip, float speed) => Play(clip);

        // Always restarts the clip at the given zero-based frame. Frame events placed on that
        // start frame DO fire; events on earlier frames do not.
        void PlayFromFrame(string clip, int frame);

        bool IsPlaying(string clip);

        // Raised once when a non-looping clip reaches its end. Never raised for looping clips,
        // and not for a clip that was replaced before it ended.
        event Action<string> Completed;

        // Raised with (clip name, frame index) when playback reaches a frame-event marker.
        event Action<string, int> FrameEvent;
    }
}
