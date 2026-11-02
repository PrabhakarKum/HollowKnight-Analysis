using System.Collections;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // Full-screen black overlay faded by alpha. Drawn with IMGUI on top of everything: it needs no Canvas, no
    // camera, no packages, and works the same in URP, because IMGUI is drawn after rendering.
    // Fades run on unscaled time so a paused or slowed game still fades.
    public class ScreenFader : MonoBehaviour, IScreenFader
    {
        RoomTransitionSettings settings = new RoomTransitionSettings();

        Texture2D pixel;
        float alpha;
        // Only the newest fade may move the alpha; an older, still running fade stops itself.
        int fadeId;

        public RoomTransitionSettings Settings => settings;

        // Shares the settings object with the transition manager so fade lengths have one source.
        public void Configure(RoomTransitionSettings shared)
        {
            if (shared != null) settings = shared;
        }

        // 0 = game visible, 1 = fully black.
        public float Alpha => alpha;

        // Jumps without animating (start-up, tests, emergency restore). Also cancels any running fade.
        public void SetAlpha(float value)
        {
            fadeId++;
            Apply(Mathf.Clamp01(value));
        }

        // OnGUI is only called while the behaviour is enabled, so keep it off while nothing is drawn.
        void Apply(float value)
        {
            alpha = value;
            enabled = value > 0f;
        }

        public IEnumerator FadeOut() => Fade(1f, settings.fadeOutSeconds);

        public IEnumerator FadeIn() => Fade(0f, settings.fadeInSeconds);

        IEnumerator Fade(float target, float seconds)
        {
            int id = ++fadeId;
            if (seconds <= 0f)
            {
                Apply(target);
                yield break;
            }

            float start = alpha;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                Apply(Mathf.Lerp(start, target, Mathf.Clamp01(t / seconds)));
                yield return null;
                if (id != fadeId) yield break;   // a newer fade took over
            }
            if (id == fadeId) Apply(target);
        }

        void Awake()
        {
            pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
            enabled = alpha > 0f;
        }

        void OnDestroy()
        {
            if (pixel != null) Destroy(pixel);
        }

        void OnGUI()
        {
            if (alpha <= 0f || pixel == null || Event.current.type != EventType.Repaint) return;
            var saved = GUI.color;
            GUI.depth = -1000;   // in front of any other IMGUI
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), pixel);
            GUI.color = saved;
        }
    }
}
