using System.Collections.Generic;
using UnityEngine;

namespace Hollowknight.Feel
{
    /// <summary>
    /// Original pixel-art effect helpers, drawn in code so they match the hero's pixel sprites (32 pixels per unit).
    /// Nothing here is taken from the game's art: square pixels, stepped movement on the pixel grid, and a dithered
    /// glow made from a 4x4 ordered-dither pattern.
    /// </summary>
    public static class PixelFx
    {
        /// <summary>One art pixel in world units (the hero sprites are 32 pixels per unit).</summary>
        public const float Pixel = 1f / 32f;

        static Sprite square;
        static readonly Dictionary<int, Sprite> auras = new Dictionary<int, Sprite>();

        // 4x4 ordered-dither thresholds (0..15 / 16)
        static readonly int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        /// <summary>A white 1x1 pixel sprite that is exactly one art pixel big; scale it to make bigger square "pixels".</summary>
        public static Sprite Square
        {
            get
            {
                if (square == null)
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f / Pixel);
                    square.name = "PixelFx.Square";
                }
                return square;
            }
        }

        public static Vector2 Snap(Vector2 p) => new Vector2(Mathf.Round(p.x / Pixel) * Pixel, Mathf.Round(p.y / Pixel) * Pixel);

        /// <summary>
        /// A round glow of the given pixel width, in four dithered brightness steps, tinted from the edge colour to the
        /// core colour. Its size in world units is sizePx / 32, so one texture pixel is one art pixel.
        /// </summary>
        public static Sprite Aura(int sizePx, Color edge, Color core)
        {
            int key = sizePx * 397 ^ edge.GetHashCode() * 31 ^ core.GetHashCode();
            if (auras.TryGetValue(key, out var cached) && cached != null) return cached;

            var tex = new Texture2D(sizePx, sizePx, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            float r = sizePx * 0.5f;
            for (int y = 0; y < sizePx; y++)
            {
                for (int x = 0; x < sizePx; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float v = 1f - Mathf.Sqrt(dx * dx + dy * dy) / r;              // 1 at the centre, 0 at the rim
                    if (v <= 0f) { tex.SetPixel(x, y, Color.clear); continue; }
                    float level = Mathf.Clamp01(v * 1.25f);
                    float dither = (bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
                    float q = Mathf.Floor(level * 3f + dither) / 3f;              // 0, 1/3, 2/3, 1 with ordered dithering between steps
                    var c = Color.Lerp(edge, core, level);
                    c.a = q;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, sizePx, sizePx), new Vector2(0.5f, 0.5f), 1f / Pixel);
            s.name = "PixelFx.Aura" + sizePx;
            auras[key] = s;
            return s;
        }
    }

    /// <summary>
    /// Draws and moves every pixel particle in the scene (one square sprite each, pooled). Created on first use.
    /// Particles step on the 32-pixels-per-unit grid so they look like pixel art, not smooth motion.
    /// </summary>
    public class PixelFxLayer : MonoBehaviour
    {
        static PixelFxLayer instance;

        public static PixelFxLayer Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("PixelFx");
                    instance = go.AddComponent<PixelFxLayer>();
                }
                return instance;
            }
        }

        struct Particle
        {
            public SpriteRenderer sr;
            public Vector2 pos, vel;
            public float life, maxLife, drag, gravity;
            public Color from, to;
            public int sizePx;
            public Transform home;
            public Vector2 homeOffset;
            public float homePull;
            public bool unscaled;
        }

        const int MaxLive = 400;
        readonly List<Particle> live = new List<Particle>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();

        public int LiveCount => live.Count;
        public int TotalEmitted { get; private set; }
        public int SortingOrder = 9;

        /// <summary>
        /// Emits one square pixel particle. Colour fades from 'from' to 'to' over its life and it blinks out at the end.
        /// If 'home' is set it is pulled toward that point (and absorbed on arrival), which is how motes gather on the hero.
        /// 'unscaled' makes it move in real time, so it keeps going while the game is frozen (hit-stop impact bursts).
        /// </summary>
        public void Emit(Vector2 pos, Vector2 vel, float life, Color from, Color to, int sizePx = 2,
                         float drag = 0f, float gravity = 0f, Transform home = null, Vector2 homeOffset = default, float homePull = 0f,
                         bool unscaled = false)
        {
            if (live.Count >= MaxLive) return;
            SpriteRenderer sr = pool.Count > 0 ? pool.Pop() : NewRenderer();
            sr.gameObject.SetActive(true);
            sr.transform.localScale = Vector3.one * sizePx;          // the square sprite is already exactly one art pixel big
            live.Add(new Particle
            {
                sr = sr, pos = pos, vel = vel, life = life, maxLife = life, drag = drag, gravity = gravity,
                from = from, to = to, sizePx = sizePx, home = home, homeOffset = homeOffset, homePull = homePull, unscaled = unscaled
            });
            TotalEmitted++;
            Apply(live.Count - 1);
        }

        SpriteRenderer NewRenderer()
        {
            var go = new GameObject("p");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelFx.Square;
            sr.sortingOrder = SortingOrder;
            return sr;
        }

        void Update()
        {
            float scaledDt = Time.deltaTime, realDt = Time.unscaledDeltaTime;
            if (scaledDt <= 0f && realDt <= 0f) return;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                float dt = p.unscaled ? realDt : scaledDt;
                if (dt <= 0f) continue;                                // frozen (hit-stop): scaled particles hold still
                p.life -= dt;
                bool absorbed = false;
                if (p.home != null && p.homePull > 0f)
                {
                    Vector2 target = (Vector2)p.home.position + p.homeOffset;
                    Vector2 to = target - p.pos;
                    float dist = to.magnitude;
                    if (dist < 0.18f) absorbed = true;
                    else p.vel += to / dist * p.homePull * dt;
                }
                p.vel.y -= p.gravity * dt;
                if (p.drag > 0f) p.vel *= Mathf.Max(0f, 1f - p.drag * dt);
                p.pos += p.vel * dt;
                live[i] = p;

                if (p.life <= 0f || absorbed)
                {
                    p.sr.gameObject.SetActive(false);
                    pool.Push(p.sr);
                    live.RemoveAt(i);
                    continue;
                }
                Apply(i);
            }
        }

        void Apply(int i)
        {
            var p = live[i];
            float t = 1f - p.life / p.maxLife;                         // 0 at birth, 1 at death
            var c = Color.Lerp(p.from, p.to, t);
            // blink out over the last 30% of life (stepped, not a smooth fade, to stay pixel-like)
            if (t > 0.7f) c.a *= (Mathf.FloorToInt(t * 24f) % 2 == 0) ? 1f : 0.35f;
            p.sr.color = c;
            Vector2 s = PixelFx.Snap(p.pos);
            p.sr.transform.position = new Vector3(s.x, s.y, 0f);
        }

        void OnDestroy() { if (instance == this) instance = null; }
    }
}
