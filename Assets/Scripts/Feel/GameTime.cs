using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hollowknight.Feel
{
    /// <summary>
    /// The one place that writes Time.timeScale, like the original TimeManager: several things can want to slow or stop
    /// the game at once (a hit freeze, a camera freeze-frame, a boss slow-down), so each keeps its own factor and
    /// the game runs at their product.
    ///   timeScale = max(0, base * cameraShake * debug * (every "multiply" control)), then every "min" control caps it.
    /// </summary>
    public static class GameTime
    {
        public enum ControlType { Multiply = 0, Min = 1 }

        /// <summary>One independent time factor. Create one, change Scale, dispose it when done.</summary>
        public sealed class Control
        {
            public readonly ControlType Type;
            float scale;
            internal Control(float scale, ControlType type) { this.scale = scale; Type = type; }
            public float Scale { get => scale; set { scale = Mathf.Max(0f, value); Apply(); } }
        }

        static float baseScale = 1f, cameraShakeScale = 1f, debugScale = 1f;
        static readonly List<Control> controls = new List<Control>();

        /// <summary>The "slow base" scale (pause menus and the like would use this).</summary>
        public static float BaseScale { get => baseScale; set { baseScale = Mathf.Max(0f, value); Apply(); } }
        /// <summary>Set to 0 for a few frames by the camera's freeze-frames.</summary>
        public static float CameraShakeScale { get => cameraShakeScale; set { cameraShakeScale = Mathf.Max(0f, value); Apply(); } }
        public static float DebugScale { get => debugScale; set { debugScale = Mathf.Clamp(value, 0f, 100f); Apply(); } }
        public static int ActiveControls => controls.Count;

        public static Control Create(float scale = 1f, ControlType type = ControlType.Multiply)
        {
            var c = new Control(Mathf.Max(0f, scale), type);
            controls.Add(c);
            Apply();
            return c;
        }

        public static void Release(Control c)
        {
            if (c == null) return;
            controls.Remove(c);
            Apply();
        }

        /// <summary>Back to normal speed; drops every control. Used when play starts, a scene reloads, or a test ends.</summary>
        public static void ResetAll()
        {
            controls.Clear();
            baseScale = cameraShakeScale = debugScale = 1f;
            Apply();
        }

        static void Apply()
        {
            float s = baseScale * cameraShakeScale * debugScale;
            for (int i = 0; i < controls.Count; i++)
                if (controls[i].Type == ControlType.Multiply) s *= controls[i].Scale;
            for (int i = 0; i < controls.Count; i++)
                if (controls[i].Type == ControlType.Min) s = Mathf.Min(s, controls[i].Scale);
            Time.timeScale = Mathf.Max(0f, s);
        }

        /// <summary>Moves a control's factor to a target over `duration` real seconds (the original's SetTimeScale routine).</summary>
        public static IEnumerator RampTo(Control c, float target, float duration)
        {
            if (duration <= 0f) { c.Scale = target; yield break; }
            float from = c.Scale, t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                c.Scale = Mathf.Lerp(from, target, Mathf.Clamp01(t / duration));
                yield return null;
            }
            c.Scale = target;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() { ResetAll(); }
    }
}
