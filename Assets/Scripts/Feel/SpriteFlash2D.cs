using UnityEngine;

namespace Hollowknight.Feel
{
    /// <summary>
    /// Turns a sprite into a flat white silhouette for a moment (the hit flash). Counts in real time, so the flash shows
    /// during a hit freeze. Swaps in the Hollowknight/SpriteFlash material only while flashing and puts the original back after;
    /// if the shader is missing it quietly does nothing.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFlash2D : MonoBehaviour
    {
        static Material flashMaterial;
        static bool searched;
        static readonly int FlashAmount = Shader.PropertyToID("_FlashAmount");
        static readonly int FlashColor = Shader.PropertyToID("_FlashColor");

        SpriteRenderer sr;
        Material original;
        MaterialPropertyBlock block;
        float until;

        public static int FlashCount { get; private set; }
        public bool IsFlashing => Time.unscaledTime < until;

        static Material Shared
        {
            get
            {
                if (flashMaterial == null && !searched)
                {
                    searched = true;
                    var shader = Shader.Find("Hollowknight/SpriteFlash");
                    if (shader != null) flashMaterial = new Material(shader) { name = "SpriteFlash (runtime)", hideFlags = HideFlags.HideAndDontSave };
                }
                return flashMaterial;
            }
        }

        void Awake() { sr = GetComponent<SpriteRenderer>(); }

        /// <summary>Flashes the sprite for this many real seconds.</summary>
        public void Flash(float seconds, Color? color = null)
        {
            if (seconds <= 0f) return;
            var mat = Shared;
            if (mat == null || sr == null) return;
            FlashCount++;
            if (!IsFlashing) original = sr.sharedMaterial;
            sr.sharedMaterial = mat;
            if (block == null) block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);
            block.SetFloat(FlashAmount, 1f);
            block.SetColor(FlashColor, color ?? Color.white);
            sr.SetPropertyBlock(block);
            until = Time.unscaledTime + seconds;
            enabled = true;
        }

        void Update()
        {
            if (IsFlashing) return;
            Restore();
        }

        void OnDisable() { if (original != null) Restore(); }

        void Restore()
        {
            if (original != null && sr != null)
            {
                sr.sharedMaterial = original;
                sr.SetPropertyBlock(null);
            }
            original = null;
            enabled = false;
        }
    }
}
