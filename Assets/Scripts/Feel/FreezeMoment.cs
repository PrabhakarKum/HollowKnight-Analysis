using System.Collections;
using UnityEngine;

namespace Hollowknight.Feel
{
    /// <summary>The kinds of freeze the original has (GameManager.FreezeMoment). None = no freeze.</summary>
    public enum FreezeType
    {
        None = -1,
        HeroDamage = 0,
        EnemyDeath = 1,
        BossDeathStrike = 2,
        NailClashEffect = 3,
        BossStun = 4,
        EnemyDeathShort = 5,
        QuickFreeze = 6,
        ZapFreeze = 7,
        WitchBindHit = 8,
        HeroDamageShort = 9,
        BossDeathSlow = 10,
        RaceWinSlow = 11,
        EnemyBattleEndSlow = 12,
        BigEnemyDeathSlow = 13,
        BindBreak = 14
    }

    /// <summary>
    /// Hit-stop. A "freeze moment" ramps the game's speed down to a target (usually 0), holds it for a real-time wait,
    /// then ramps back up to 1. The 15 timings below are read from the original's GameManager.FreezeMoment switch;
    /// each freeze owns its own time control, so overlapping freezes multiply instead of overwriting each other.
    /// </summary>
    public static class FreezeMoment
    {
        public readonly struct Timing
        {
            public readonly float RampDown, Wait, RampUp, TargetSpeed;
            public Timing(float rampDown, float wait, float rampUp, float targetSpeed)
            { RampDown = rampDown; Wait = wait; RampUp = rampUp; TargetSpeed = targetSpeed; }
            public float Total => RampDown + Wait + RampUp;
        }

        // (rampDown, wait, rampUp, targetSpeed) in real seconds - the original's numbers, one row per FreezeType 0..14
        static readonly Timing[] table =
        {
            new Timing(0.01f,   0.28f,  0.1f,   0f),       //  0 HeroDamage
            new Timing(0.04f,   0.024f, 0.04f,  0f),       //  1 EnemyDeath
            new Timing(0f,      0.35f,  0.1f,   0f),       //  2 BossDeathStrike
            new Timing(0.01f,   0.25f,  0.1f,   0f),       //  3 NailClashEffect
            new Timing(0f,      0.25f,  0.1f,   0f),       //  4 BossStun
            new Timing(0.04f,   0.015f, 0.04f,  0f),       //  5 EnemyDeathShort
            new Timing(0.0001f, 0.02f,  0.0001f,0.0001f),  //  6 QuickFreeze
            new Timing(0f,      0.1f,   0f,     0f),       //  7 ZapFreeze
            new Timing(0.04f,   0.03f,  0.04f,  0f),       //  8 WitchBindHit
            new Timing(0.001f,  0.15f,  0.05f,  0f),       //  9 HeroDamageShort
            new Timing(0.1f,    1.15f,  0.1f,   0.05f),    // 10 BossDeathSlow
            new Timing(0.5f,    3f,     0.3f,   0.1f),     // 11 RaceWinSlow
            new Timing(0.1f,    1f,     0.75f,  0.25f),    // 12 EnemyBattleEndSlow
            new Timing(0.04f,   0.06f,  0.04f,  0f),       // 13 BigEnemyDeathSlow
            new Timing(0.01f,   0.4f,   0.1f,   0f),       // 14 BindBreak
        };

        static int active;                 // the original's GameManager.timeSlowedCount
        static Runner runner;

        /// <summary>True while any freeze is running (from its first ramp-down to the end of its ramp-up).</summary>
        public static bool IsFrozen => active > 0;
        public static int ActiveCount => active;
        public static int StartedCount { get; private set; }

        /// <summary>The timing of a freeze type; FreezeType.None has no freeze at all (zero time, game speed stays 1).</summary>
        public static Timing GetTiming(FreezeType type) => type == FreezeType.None ? new Timing(0f, 0f, 0f, 1f) : table[(int)type];

        /// <summary>Starts a freeze. FreezeType.None does nothing.</summary>
        public static void Start(FreezeType type)
        {
            if (type == FreezeType.None) return;
            Start(GetTiming(type));
        }

        public static void Start(Timing timing)
        {
            if (runner == null)
            {
                var go = new GameObject("FreezeMoment (runner)") { hideFlags = HideFlags.HideInHierarchy };
                Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<Runner>();
            }
            StartedCount++;
            runner.StartCoroutine(Routine(timing));
        }

        /// <summary>Stops every freeze at once and puts the game back to full speed (death, scene change, tests).</summary>
        public static void CancelAll()
        {
            if (runner != null) runner.StopAllCoroutines();
            active = 0;
            GameTime.ResetAll();
        }

        static IEnumerator Routine(Timing t)
        {
            active++;
            var control = GameTime.Create(1f, GameTime.ControlType.Multiply);
            try
            {
                yield return GameTime.RampTo(control, t.TargetSpeed, t.RampDown);
                for (float timer = 0f; timer < t.Wait; timer += Time.unscaledDeltaTime) yield return null;
                yield return GameTime.RampTo(control, 1f, t.RampUp);
            }
            finally
            {
                GameTime.Release(control);
                active = Mathf.Max(0, active - 1);
            }
        }

        sealed class Runner : MonoBehaviour
        {
            void OnDestroy() { active = 0; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() { active = 0; StartedCount = 0; runner = null; }
    }
}
