using System.Collections;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // Full-screen black overlay faded by alpha. Drawn with IMGUI on top of everything: it needs no Canvas, no
    // camera, no packages, and works the same in URP, because IMGUI is drawn after rendering.
    // Fades run on unscaled time so a paused or slowed game still fades.
    public class ScreenFader : MonoBehaviour, IScreenFader
    {
        public RoomTransitionSettings Settings = new RoomTransitionSettings();

        Texture2D pixel;
        float alpha;

        // 0 = game visible, 1 = fully black.
        public float Alpha => alpha;

        // Jumps without animating (used at start-up and by tests).
        public void SetAlpha(float value) => alpha = Mathf.Clamp01(value);

        public IEnumerator FadeOut() => Fade(1f, Settings.fadeOutSeconds);

        public IEnumerator FadeIn() => Fade(0f, Settings.fadeInSeconds);

        IEnumerator Fade(float target, float seconds)
        {
            if (seconds <= 0f)
            {
                alpha = target;
                yield break;
            }

            float start = alpha;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / seconds));
                yield return null;
            }
            alpha = target;
        }

        void Awake()
        {
            pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
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
