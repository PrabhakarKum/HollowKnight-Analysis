using UnityEngine;

namespace Hollowknight.Player
{
    /// <summary>Plain IMGUI health masks in the top-left corner (placeholder until there is real UI).</summary>
    public class HealthHud2D : MonoBehaviour
    {
        [SerializeField] PlayerHealth2D health;
        [SerializeField] float size = 26f, gap = 8f;

        PlayerSilk2D silk;

        void Awake()
        {
            if (health == null) health = FindFirstObjectByType<PlayerHealth2D>();
            if (health != null) silk = health.GetComponent<PlayerSilk2D>();
        }

        void OnGUI()
        {
            if (health == null) return;
            var old = GUI.color;
            DrawSilk();
            for (int i = 0; i < health.MaxHealth; i++)
            {
                var r = new Rect(16f + i * (size + gap), 16f, size, size);
                GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
                GUI.DrawTexture(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), Texture2D.whiteTexture);
                GUI.color = i < health.Health ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.3f, 0.3f, 0.35f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
            }
            if (health.IsDead)
            {
                GUI.color = Color.white;
                GUI.Label(new Rect(16f, 16f + size + 8f, 200f, 24f), "DEAD");
            }
            GUI.color = old;
        }

        /// <summary>Silk as a row of small amber pips under the masks; they pulse when a bind is possible and drain while binding.</summary>
        void DrawSilk()
        {
            if (silk == null) return;
            const float pip = 12f, pipGap = 4f;
            float y = 16f + size + 12f;
            float draining = silk.IsBinding ? silk.BindProgress * silk.TuningAsset.bindCost : 0f;   // visual only: the silk is spent when the bind finishes
            bool pulse = silk.BindReady && !silk.IsBinding && Mathf.FloorToInt(Time.unscaledTime * 6f) % 2 == 0;
            var old = GUI.color;
            for (int i = 0; i < silk.MaxSilk; i++)
            {
                var r = new Rect(16f + i * (pip + pipGap), y, pip, pip);
                GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
                GUI.DrawTexture(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), Texture2D.whiteTexture);
                bool filled = i < silk.Silk - Mathf.FloorToInt(draining);
                GUI.color = filled ? (pulse ? new Color(1f, 0.95f, 0.72f) : new Color(0.98f, 0.60f, 0.16f)) : new Color(0.28f, 0.24f, 0.2f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
            }
            GUI.color = old;
        }
    }
}
