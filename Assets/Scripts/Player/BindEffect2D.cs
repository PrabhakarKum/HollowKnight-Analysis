using UnityEngine;
using Hollowknight.Feel;

namespace Hollowknight.Player
{
    /// <summary>
    /// Our own pixel-art look for silk and bind (nothing taken from the game's effects):
    /// - gaining silk: a few warm motes fly from the thing you hit into the hero;
    /// - binding: a dithered amber glow swells around the hero while square pixel motes spiral in and are absorbed,
    ///   faster and denser as the bind progresses;
    /// - finished: the glow flashes, two rings of pixels and a spray of sparks burst outward (teal sparks among the amber);
    /// - broken: the motes drop away as a few falling embers and the glow cuts out.
    /// Palette: cream core, amber edge, teal accents - chosen to read against the hero's sprites, not to copy anything.
    /// </summary>
    [RequireComponent(typeof(PlayerSilk2D))]
    public class BindEffect2D : MonoBehaviour
    {
        [Tooltip("Where the glow is centred, relative to the player object's pivot. The pivot is the middle of the collider, so the chest is just below it while the hero kneels to bind.")]
        [SerializeField] float centerOffsetY = -0.05f;
        [SerializeField] int auraSizePx = 64;

        static readonly Color Cream = new Color(1f, 0.95f, 0.72f);
        static readonly Color Amber = new Color(0.98f, 0.60f, 0.16f);
        static readonly Color Teal = new Color(0.30f, 0.88f, 0.80f);
        static readonly Color Ember = new Color(0.75f, 0.28f, 0.10f);

        PlayerSilk2D silk;
        SpriteRenderer aura;
        SpriteRenderer heroRenderer;
        float spawnDebt;
        float flash;                     // 1 right after a finished bind, fades to 0

        /// <summary>Current opacity of the glow (for tests / debugging).</summary>
        public float AuraAlpha => aura != null && aura.enabled ? aura.color.a : 0f;

        Vector2 Center => (Vector2)transform.position + new Vector2(0f, centerOffsetY);

        void Awake()
        {
            silk = GetComponent<PlayerSilk2D>();
            var animator = GetComponentInChildren<Animator>();
            heroRenderer = animator != null ? animator.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();

            var go = new GameObject("Bind Glow (pixel)");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, centerOffsetY, 0f);
            aura = go.AddComponent<SpriteRenderer>();
            aura.sprite = PixelFx.Aura(auraSizePx, Amber, Cream);
            aura.sortingOrder = (heroRenderer != null ? heroRenderer.sortingOrder : 0) - 1;     // behind the hero
            aura.enabled = false;
        }

        void OnEnable()
        {
            silk.SilkGained += OnSilkGained;
            silk.BindStarted += OnBindStarted;
            silk.BindCompleted += OnBindCompleted;
            silk.BindCancelled += OnBindCancelled;
        }

        void OnDisable()
        {
            silk.SilkGained -= OnSilkGained;
            silk.BindStarted -= OnBindStarted;
            silk.BindCompleted -= OnBindCompleted;
            silk.BindCancelled -= OnBindCancelled;
            if (aura != null) aura.enabled = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (flash > 0f) flash = Mathf.Max(0f, flash - dt / 0.35f);

            if (silk.IsBinding)
            {
                float p = silk.BindProgress;
                // glow: swells with progress, steps in 1/16 sizes and pulses in coarse steps (pixel-art, not smooth)
                float pulse = (Mathf.FloorToInt(Time.time * 10f) % 2 == 0) ? 1f : 0.88f;
                float scale = Mathf.Round(Mathf.Lerp(0.55f, 1.0f, p) * 16f) / 16f;
                ShowAura(Mathf.Lerp(0.30f, 0.80f, p) * pulse, scale);

                // motes gather: rate and speed grow with progress
                spawnDebt += Mathf.Lerp(16f, 46f, p) * dt;
                while (spawnDebt >= 1f) { spawnDebt -= 1f; EmitGatheringMote(p); }
            }
            else if (flash > 0f)
            {
                ShowAura(flash, Mathf.Lerp(1.0f, 1.5f, 1f - flash));         // finished: flare outward while fading
            }
            else
            {
                if (aura.enabled) aura.enabled = false;
                spawnDebt = 0f;
            }
        }

        void ShowAura(float alpha, float scale)
        {
            aura.enabled = true;
            aura.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            aura.transform.localScale = Vector3.one * scale;
        }

        void EmitGatheringMote(float progress)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float radius = Random.Range(1.0f, 1.5f);
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 pos = Center + dir * radius;
            Vector2 tangent = new Vector2(-dir.y, dir.x);                    // a gentle spiral as they fall in
            bool accent = Random.value < 0.12f;
            PixelFxLayer.Instance.Emit(pos, tangent * 2.2f, 0.8f,
                accent ? Teal : Cream, accent ? Teal : Amber,
                sizePx: Random.value < 0.35f ? 3 : 2, drag: 0f, gravity: 0f,
                home: transform, homeOffset: new Vector2(0f, centerOffsetY), homePull: Mathf.Lerp(16f, 30f, progress));
        }

        // ------------------------------------------------------------------ one-off moments

        void OnSilkGained(int amount, Vector2 from)
        {
            int motes = 2 + amount;
            for (int i = 0; i < motes; i++)
            {
                Vector2 jitter = new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(-0.25f, 0.25f));
                PixelFxLayer.Instance.Emit(from + jitter, new Vector2(Random.Range(-2f, 2f), Random.Range(1f, 4f)), 0.7f,
                    Cream, Amber, sizePx: 2, drag: 1.5f, gravity: 0f,
                    home: transform, homeOffset: new Vector2(0f, centerOffsetY), homePull: 28f);
            }
        }

        void OnBindStarted()
        {
            flash = 0f;
            spawnDebt = 0f;
        }

        void OnBindCompleted()
        {
            flash = 1f;
            var fx = PixelFxLayer.Instance;
            // two rings of single pixels expanding at slightly different speeds
            for (int ring = 0; ring < 2; ring++)
            {
                int n = 20 + ring * 8;
                float speed = 5f + ring * 2.5f;
                for (int i = 0; i < n; i++)
                {
                    float a = (i / (float)n) * Mathf.PI * 2f;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    fx.Emit(Center + d * 0.3f, d * speed, 0.45f + ring * 0.1f, ring == 0 ? Cream : Teal, Amber, sizePx: 2, drag: 4f);
                }
            }
            // a spray of sparks
            for (int i = 0; i < 28; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                bool accent = Random.value < 0.25f;
                fx.Emit(Center, d * Random.Range(2.5f, 9f), Random.Range(0.4f, 0.9f),
                    accent ? Teal : Cream, accent ? Teal : Ember, sizePx: Random.value < 0.4f ? 3 : 2, drag: 3.5f, gravity: 4f);
            }
        }

        void OnBindCancelled(float progress)
        {
            if (aura != null && flash <= 0f) aura.enabled = false;
            // the gathered motes drop away as a few embers
            int n = 4 + Mathf.RoundToInt(progress * 10f);
            for (int i = 0; i < n; i++)
            {
                Vector2 pos = Center + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.3f, 0.5f));
                PixelFxLayer.Instance.Emit(pos, new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(0.5f, 2f)), 0.6f,
                    Amber, Ember, sizePx: 2, drag: 1f, gravity: 9f);
            }
        }
    }
}
