using UnityEngine;

namespace Hollowknight.Enemies
{
    /// <summary>Plays a list of sprites at a frame rate, once (then destroys itself) or looping. Used for muzzle flashes, impacts and spinning blades.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFlipbook2D : MonoBehaviour
    {
        public static int PlayedCount { get; private set; }

        Sprite[] frames;
        float fps = 24f;
        bool loop;
        float t;
        SpriteRenderer sr;

        /// <summary>Spawns a one-shot flipbook effect at a position.</summary>
        public static SpriteFlipbook2D PlayOnce(Vector2 position, Sprite[] frames, float fps, bool flipX = false, float scale = 1f, int sortingOrder = 8)
        {
            if (frames == null || frames.Length == 0) return null;
            var go = new GameObject("Flipbook");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder; sr.flipX = flipX;
            var fb = go.AddComponent<SpriteFlipbook2D>();
            fb.Init(frames, fps, false);
            PlayedCount++;
            return fb;
        }

        public void Init(Sprite[] sprites, float framesPerSecond, bool looping)
        {
            sr = GetComponent<SpriteRenderer>();
            frames = sprites; fps = Mathf.Max(1f, framesPerSecond); loop = looping; t = 0f;
            if (frames != null && frames.Length > 0) sr.sprite = frames[0];
        }

        void Update()
        {
            if (frames == null || frames.Length == 0) return;
            t += Time.deltaTime;
            int i = Mathf.FloorToInt(t * fps);
            if (i >= frames.Length)
            {
                if (!loop) { Destroy(gameObject); return; }
                i %= frames.Length;
            }
            sr.sprite = frames[i];
        }
    }
}
