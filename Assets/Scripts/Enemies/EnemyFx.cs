using UnityEngine;
using Hollowknight.Feel;

namespace Hollowknight.Enemies
{
    /// <summary>Pixel effects for enemies on the shared pixel particle layer (our own, in each enemy's colours).</summary>
    public static class EnemyFx
    {
        /// <summary>A small spark where the nail connects: a few pixels fly away from the hero.</summary>
        public static void HitSpark(Vector2 at, int awayX, EnemyConfig cfg)
        {
            var fx = PixelFxLayer.Instance;
            for (int i = 0; i < 8; i++)
            {
                float ang = Random.Range(-0.9f, 0.9f);
                var dir = new Vector2(awayX * Mathf.Cos(ang), Mathf.Sin(ang));
                bool accent = Random.value < 0.3f;
                fx.Emit(at, dir * Random.Range(4f, 9f), Random.Range(0.15f, 0.3f),
                    accent ? cfg.accent : Color.white, accent ? cfg.accent : cfg.bodyLight, sizePx: Random.value < 0.4f ? 3 : 2, drag: 5f);
            }
        }

        /// <summary>The death burst: body-coloured pixels spray out and fall, with a few accent sparks.</summary>
        public static void DeathBurst(Vector2 at, EnemyConfig cfg)
        {
            var fx = PixelFxLayer.Instance;
            for (int i = 0; i < 26; i++)
            {
                float ang = Random.value * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.8f + 0.4f);
                bool accent = Random.value < 0.25f;
                fx.Emit(at + new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(-0.4f, 0.4f)), dir * Random.Range(2.5f, 8f), Random.Range(0.45f, 0.9f),
                    accent ? cfg.accent : cfg.bodyLight, accent ? cfg.accent : cfg.bodyMid, sizePx: Random.value < 0.45f ? 3 : 2, drag: 2.5f, gravity: 9f);
            }
        }

        /// <summary>A "!" made of pixels above the head: the tell before an attack.</summary>
        public static void Exclaim(Vector2 above, EnemyConfig cfg)
        {
            var fx = PixelFxLayer.Instance;
            Color c0 = Color.white, c1 = cfg.accent;
            for (int i = 0; i < 4; i++)
                fx.Emit(above + new Vector2(0f, 0.17f + i * PixelFx.Pixel * 3f), Vector2.zero, 0.4f, c0, c1, sizePx: 3);
            fx.Emit(above, Vector2.zero, 0.4f, c0, c1, sizePx: 3);
        }

        /// <summary>A dotted aim line from the weapon toward the target, flashed once during the wind-up of a ranged attack.</summary>
        public static void AimLine(Vector2 from, Vector2 to, EnemyConfig cfg, float maxLength = 6f)
        {
            var fx = PixelFxLayer.Instance;
            Vector2 d = to - from; float len = Mathf.Min(d.magnitude, maxLength);
            if (len < 0.5f) return;
            d.Normalize();
            for (float t = 0.5f; t < len; t += 0.45f)
                fx.Emit(from + d * t, Vector2.zero, 0.16f, cfg.accent, cfg.accent, sizePx: 2);
        }

        /// <summary>A short dust puff for landings.</summary>
        public static void Dust(Vector2 feet)
        {
            var fx = PixelFxLayer.Instance;
            for (int i = 0; i < 6; i++)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                fx.Emit(feet, new Vector2(side * Random.Range(0.8f, 3f), Random.Range(0.3f, 1.5f)), Random.Range(0.25f, 0.45f),
                    new Color(0.82f, 0.78f, 0.70f), new Color(0.42f, 0.40f, 0.38f), sizePx: 3, drag: 3f, gravity: -0.5f);
            }
        }
    }
}
