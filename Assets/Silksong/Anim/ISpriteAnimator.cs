using System;

namespace Hollowknight.Silksong
{
    // Hero and enemy code talk to this instead of Unity's Animator, so the animation backend
    // (the original used tk2d) can change without touching gameplay code.
    public interface ISpriteAnimator
    {
        void Play(string clip);
        void PlayFromFrame(string clip, int frame);
        bool IsPlaying(string clip);

        // Raised once when a non-looping clip reaches its end. Never raised for looping clips.
        event Action<string> Completed;

        // Raised with (clip name, frame index) when playback passes a frame-event marker.
        event Action<string, int> FrameEvent;
    }
}
