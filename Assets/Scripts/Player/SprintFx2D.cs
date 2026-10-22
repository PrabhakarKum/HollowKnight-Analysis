using UnityEngine;
using Hollowknight.Feel;

namespace Hollowknight.Player
{
    /// <summary>
    /// Our own pixel-art look for sprinting (nothing taken from the game's effects), built on the same pixel particle layer
    /// as the bind effect:
    /// - while sprinting on the ground: small square dust puffs kicked up behind the feet, plus short cream / teal speed
    ///   pixels streaming back past the body;
    /// - starting a sprint: a low burst of dust behind the hero;
    /// - a skid (sprint ends at speed): dust sprays forward along the ground;
    /// - a sprint jump: dust bursts out sideways from the take-off point and a few speed pixels trail the launch.
    /// Dust is warm grey, speed pixels reuse the bind palette (cream / teal) so the effects look like one set.
    /// </summary>
    [RequireComponent(typeof(PlayerController2D))]
    public class SprintFx2D : MonoBehaviour
    {
        static readonly Color DustLight = new Color(0.82f, 0.78f, 0.70f);
        static readonly Color DustDark = new Color(0.42f, 0.40f, 0.38f);
        static readonly Color Cream = new Color(1f, 0.95f, 0.72f);
        static readonly Color Teal = new Color(0.30f, 0.88f, 0.80f);

        PlayerController2D controller;
        Collider2D col;
        float dustDebt, streakDebt;

        Vector2 Feet => new Vector2(col.bounds.center.x, col.bounds.min.y + 0.04f);

        void Awake()
        {
            controller = GetComponent<PlayerController2D>();
            col = GetComponent<Collider2D>();
        }

        void OnEnable()
        {
            controller.SprintStarted += OnSprintStarted;
            controller.SprintEnded += OnSprintEnded;
            controller.ShuttleJumped += OnShuttleJumped;
        }

        void OnDisable()
        {
            controller.SprintStarted -= OnSprintStarted;
            controller.SprintEnded -= OnSprintEnded;
            controller.ShuttleJumped -= OnShuttleJumped;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!controller.IsSprinting) { dustDebt = streakDebt = 0f; return; }

            int f = controller.FacingDirection;
            var fx = PixelFxLayer.Instance;

            if (controller.IsGrounded)
            {
                dustDebt += 34f * dt;
                while (dustDebt >= 1f)
                {
                    dustDebt -= 1f;
                    Vector2 p = Feet + new Vector2(-f * Random.Range(0.1f, 0.35f), Random.Range(0f, 0.06f));
                    fx.Emit(p, new Vector2(-f * Random.Range(0.4f, 2.4f), Random.Range(0.8f, 2.6f)), Random.Range(0.4f, 0.7f),
                        DustLight, DustDark, sizePx: Random.value < 0.5f ? 4 : 3, drag: 2.5f, gravity: -1.5f);
                }
            }

            streakDebt += 14f * dt;
            while (streakDebt >= 1f)
            {
                streakDebt -= 1f;
                Vector2 p = (Vector2)col.bounds.center + new Vector2(f * 0.1f + Random.Range(-0.3f, 0.3f), Random.Range(-0.5f, 0.5f));
                bool teal = Random.value < 0.3f;
                var c0 = teal ? Teal : Cream; var c1 = teal ? Teal : DustLight;
                float life = Random.Range(0.16f, 0.26f), speed = Random.Range(9f, 13f);
                // a speed streak is two pixels in a row, so it reads as a short dash
                fx.Emit(p, new Vector2(-f * speed, 0f), life, c0, c1, sizePx: 2);
                fx.Emit(p + new Vector2(-f * 3f * PixelFx.Pixel, 0f), new Vector2(-f * speed, 0f), life, c0, c1, sizePx: 2);
            }
        }

        void OnSprintStarted()
        {
            if (!controller.IsGrounded) return;
            int f = controller.FacingDirection;
            var fx = PixelFxLayer.Instance;
            for (int i = 0; i < 14; i++)
                fx.Emit(Feet + new Vector2(-f * Random.Range(0f, 0.4f), 0f), new Vector2(-f * Random.Range(1f, 5f), Random.Range(0.5f, 2.5f)),
                    Random.Range(0.4f, 0.7f), DustLight, DustDark, sizePx: Random.value < 0.5f ? 4 : 3, drag: 3f, gravity: -1f);
        }

        void OnSprintEnded(bool skid)
        {
            if (!skid) return;
            int f = controller.FacingDirection;
            var fx = PixelFxLayer.Instance;
            // the hero slides on, so the dust leaps forward along the ground
            for (int i = 0; i < 18; i++)
                fx.Emit(Feet + new Vector2(f * Random.Range(0f, 0.4f), 0f), new Vector2(f * Random.Range(2f, 8f), Random.Range(0.4f, 2.6f)),
                    Random.Range(0.35f, 0.65f), DustLight, DustDark, sizePx: Random.value < 0.5f ? 4 : 3, drag: 4f, gravity: -1f);
        }

        void OnShuttleJumped()
        {
            var fx = PixelFxLayer.Instance;
            for (int i = 0; i < 16; i++)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                fx.Emit(Feet, new Vector2(side * Random.Range(1.5f, 6f), Random.Range(0.3f, 2.2f)), Random.Range(0.35f, 0.6f),
                    DustLight, DustDark, sizePx: Random.value < 0.5f ? 4 : 3, drag: 3.5f, gravity: -0.5f);
            }
            int f = controller.FacingDirection;
            for (int i = 0; i < 8; i++)
            {
                bool teal = Random.value < 0.35f;
                fx.Emit((Vector2)col.bounds.center + new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(-0.5f, 0.3f)),
                    new Vector2(-f * Random.Range(5f, 11f), Random.Range(-1f, 1f)), Random.Range(0.18f, 0.32f),
                    teal ? Teal : Cream, teal ? Teal : DustLight, sizePx: 2, drag: 2f);
            }
        }
    }
}
