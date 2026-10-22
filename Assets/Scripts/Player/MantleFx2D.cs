using UnityEngine;
using Hollowknight.Feel;

namespace Hollowknight.Player
{
    /// <summary>
    /// Our own pixel-art look for pulling up onto a ledge (nothing taken from the game's effects), on the shared pixel particle layer:
    /// - grabbing the ledge: a few grit pixels shake loose from the ledge corner and fall;
    /// - standing up on it: a small puff of dust pixels sprays sideways from the feet.
    /// Same warm-grey dust palette as the sprint effect, so the movement effects look like one set.
    /// </summary>
    [RequireComponent(typeof(PlayerController2D))]
    public class MantleFx2D : MonoBehaviour
    {
        static readonly Color DustLight = new Color(0.82f, 0.78f, 0.70f);
        static readonly Color DustDark = new Color(0.42f, 0.40f, 0.38f);

        PlayerController2D controller;
        Collider2D col;

        void Awake()
        {
            controller = GetComponent<PlayerController2D>();
            col = GetComponent<Collider2D>();
        }

        void OnEnable()
        {
            controller.MantleStarted += OnMantleStarted;
            controller.MantleFinished += OnMantleFinished;
        }

        void OnDisable()
        {
            controller.MantleStarted -= OnMantleStarted;
            controller.MantleFinished -= OnMantleFinished;
        }

        void OnMantleStarted(Vector2 corner)
        {
            var fx = PixelFxLayer.Instance;
            for (int i = 0; i < 9; i++)
            {
                // grit knocked loose from the ledge edge: drops with a little sideways drift
                fx.Emit(corner + new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(-0.05f, 0.1f)),
                    new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(0f, 1.5f)), Random.Range(0.4f, 0.7f),
                    DustLight, DustDark, sizePx: Random.value < 0.5f ? 3 : 2, drag: 0.5f, gravity: 10f);
            }
        }

        void OnMantleFinished()
        {
            var fx = PixelFxLayer.Instance;
            Vector2 feet = new Vector2(col.bounds.center.x, col.bounds.min.y + 0.04f);
            for (int i = 0; i < 14; i++)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                fx.Emit(feet + new Vector2(side * Random.Range(0f, 0.3f), 0f), new Vector2(side * Random.Range(1f, 4.5f), Random.Range(0.4f, 2f)),
                    Random.Range(0.35f, 0.6f), DustLight, DustDark, sizePx: Random.value < 0.5f ? 4 : 3, drag: 3.5f, gravity: -0.5f);
            }
        }
    }
}
