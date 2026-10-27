using UnityEngine;
using Hollowknight.Feel;

namespace Hollowknight.Player
{
    /// <summary>
    /// The impact burst when the hero is hurt: a round shock ring, a spray of sparks thrown along the knock-back, and a short streak
    /// through the hit. Our own pixel art, drawn in code. It moves in real time so it plays out DURING the hit freeze - the frozen frame
    /// then reads as an impact instead of the game just stopping.
    /// </summary>
    public static class HeroHitFx
    {
        public static int PlayedCount { get; private set; }

        static readonly Color hot = new Color(1f, 1f, 0.92f), warm = new Color(1f, 0.55f, 0.22f), cool = new Color(0.9f, 0.18f, 0.12f);

        /// <summary>'at' is where it connected; awayX is the direction the hero is knocked (-1 or 1).</summary>
        public static void Play(Vector2 at, int awayX)
        {
            PlayedCount++;
            var fx = PixelFxLayer.Instance;

            // the shock ring: sixteen pixels leaving the point together, so they form a circle that grows
            const int ring = 16;
            for (int i = 0; i < ring; i++)
            {
                float a = i / (float)ring * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                fx.Emit(at + dir * 0.12f, dir * 5.5f, 0.26f, hot, warm, sizePx: 3, drag: 5f, unscaled: true);
            }

            // sparks thrown the way the hero is knocked
            for (int i = 0; i < 12; i++)
            {
                float a = Random.Range(-0.8f, 0.8f);
                var dir = new Vector2(awayX * Mathf.Cos(a), Mathf.Sin(a));
                fx.Emit(at, dir * Random.Range(5f, 12f), Random.Range(0.25f, 0.45f), hot, cool,
                    sizePx: Random.value < 0.4f ? 3 : 2, drag: 4f, gravity: 8f, unscaled: true);
            }

            // a streak through the hit (the slash mark): a short line of pixels along the knock-back axis
            for (int i = -3; i <= 3; i++)
                fx.Emit(at + new Vector2(awayX * i * PixelFx.Pixel * 3f, 0f), Vector2.zero, 0.2f, hot, warm, sizePx: i == 0 ? 3 : 2, unscaled: true);
        }
    }
}
