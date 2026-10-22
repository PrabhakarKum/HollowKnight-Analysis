using UnityEngine;

namespace Hollowknight.Feel
{
    /// <summary>The named shakes the original's camera-shake FSM understands (plus None).</summary>
    public enum ShakePreset { None, Tiny, Small, Average, AverageQuick, Big, BigQuick, EnemyKill, RumblingFall }

    /// <summary>
    /// The shake library. The preset NAMES and the three behaviours come from the original; the magnitudes (world units),
    /// durations and freeze-frame counts are our own guesses - tune them here until they feel right.
    /// </summary>
    public static class ShakePresets
    {
        static AnimationCurve Fade() => AnimationCurve.Linear(0f, 1f, 1f, 0f);

        static ShakeProfile Shake(string name, float magnitude, float duration, int freezeFrames) => new ShakeProfile
        {
            name = name, type = ShakeType.Shake, magnitude = magnitude, duration = duration,
            freezeFrames = freezeFrames, decay = Fade()
        };

        public static readonly ShakeProfile Tiny = Shake("Tiny", 0.04f, 0.12f, 0);
        public static readonly ShakeProfile Small = Shake("Small", 0.08f, 0.20f, 0);
        public static readonly ShakeProfile Average = Shake("Average", 0.16f, 0.30f, 1);
        public static readonly ShakeProfile AverageQuick = Shake("AverageQuick", 0.16f, 0.15f, 0);
        public static readonly ShakeProfile Big = Shake("Big", 0.30f, 0.45f, 2);
        public static readonly ShakeProfile BigQuick = Shake("BigQuick", 0.30f, 0.20f, 1);
        public static readonly ShakeProfile EnemyKill = Shake("EnemyKill", 0.12f, 0.20f, 0);

        /// <summary>Constant low jitter while the hero has been falling at full speed for a long time.</summary>
        public static readonly ShakeProfile RumblingFall = new ShakeProfile
        {
            name = "RumblingFall", type = ShakeType.Rumble, magnitude = 0.05f, duration = 1f, freezeFrames = 0
        };

        public static ShakeProfile Get(ShakePreset preset)
        {
            switch (preset)
            {
                case ShakePreset.Tiny: return Tiny;
                case ShakePreset.Small: return Small;
                case ShakePreset.Average: return Average;
                case ShakePreset.AverageQuick: return AverageQuick;
                case ShakePreset.Big: return Big;
                case ShakePreset.BigQuick: return BigQuick;
                case ShakePreset.EnemyKill: return EnemyKill;
                case ShakePreset.RumblingFall: return RumblingFall;
                default: return null;
            }
        }
    }
}
