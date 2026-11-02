using System;

namespace Hollowknight.Silksong
{
    // Timings of a room change. Kept here and not in HeroConfig (a frozen contract that belongs to the hero).
    // Instance fields (not const) so tests can shrink the waits to zero and the owner can tune in the Inspector.
    // One object is shared by RoomTransitionManager and ScreenFader so there is a single source of truth.
    [Serializable]
    public class RoomTransitionSettings
    {
        // Seconds the new room must stay hidden after the fade-out starts, so the screen is dark before the
        // old room goes away. Code-side value from GameManager.BeginSceneTransitionRoutine (study 10, section 2).
        public const float DefaultFadeWaitSeconds = 0.34f;

        // Pause between the new room becoming live and the hero being placed / fade-in starting. Value from
        // HeroController.EnterHeroSubFadeUp (study 10, section 3). The hero side must not wait it again.
        public const float DefaultHeroWaitSeconds = 0.165f;

        public float fadeWaitSeconds = DefaultFadeWaitSeconds;
        public float heroWaitSeconds = DefaultHeroWaitSeconds;

        // Fade lengths are tuned by eye: the original keeps them inside a PlayMaker "Screen Fader" FSM that
        // cannot be read from code. Fade-out is shorter than the fade wait so the screen is already black
        // when the wait ends.
        public float fadeOutSeconds = 0.25f;
        public float fadeInSeconds = 0.3f;

        // Null when the values make sense; otherwise a warning text. A fade-out as long as the wait means the
        // room could be swapped while the screen is still partly visible.
        public string ValidationWarning()
        {
            if (fadeOutSeconds >= fadeWaitSeconds)
                return $"fadeOutSeconds ({fadeOutSeconds}) should be shorter than fadeWaitSeconds ({fadeWaitSeconds}), or the old room may be seen while it unloads.";
            return null;
        }
    }
}
