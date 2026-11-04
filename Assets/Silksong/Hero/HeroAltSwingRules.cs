namespace Hollowknight.Silksong
{
    // The alternate sideways swing (study 15 section 3). Sideways swings go first, alternate, first, alternate...
    // The sequence starts again when more than ResetSeconds passed since the last swing STARTED, or when the swing
    // direction differs from the previous one. A wall slash never changes the flag. Up and down never set it (the
    // default crest has no alternate up or down swing). The 0.65 s is the original's 0.15 s recovery plus 0.5 s;
    // it is a constant here, not a HeroConfig field, so the hand-tuned HeroConfig asset stays untouched.
    public static class HeroAltSwingRules
    {
        public const float ResetSeconds = 0.65f;

        public const string FirstBodyClip = "Slash";
        public const string AltBodyClip = "SlashAlt";
        public const string WallBodyClip = "WallSlash";

        // True when the flag must be cleared before this swing.
        public static bool ShouldReset(float secondsSinceLastSwingStart, bool directionChanged)
            => secondsSinceLastSwingStart > ResetSeconds || directionChanged;

        // The flag to use for this swing (after the reset check).
        public static bool FlagAfterReset(bool flag, float secondsSinceLastSwingStart, bool directionChanged)
            => flag && !ShouldReset(secondsSinceLastSwingStart, directionChanged);

        // The flag after a sideways swing: a first swing arms the alternate, the alternate clears it.
        public static bool FlagAfterSideSwing(bool usedAlternate) => !usedAlternate;

        public static string BodyClip(bool alternate) => alternate ? AltBodyClip : FirstBodyClip;
    }
}
