#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Hollowknight.Player;

namespace Hollowknight.EditorTools
{
    /// <summary>
    /// Measures the left/right extent of the OPAQUE pixels of each sprite in chosen animation clips and stores it on
    /// PlayerVisuals2D. (Sprite.vertices is a looser outline than the visible art, which left a gap at walls.)
    /// </summary>
    public static class SpriteEdgeBaker
    {
        static readonly string[] Clips = { "WallSlide", "WallJump" };
        const byte AlphaThreshold = 10;

        [MenuItem("Hollowknight/Bake Sprite Edges")]
        public static void Bake()
        {
            var visuals = Object.FindFirstObjectByType<PlayerVisuals2D>();
            if (visuals == null) { Debug.LogError("No PlayerVisuals2D in the scene."); return; }
            var animator = visuals.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) { Debug.LogError("No Animator controller."); return; }

            var sprites = new List<Sprite>();
            foreach (var clip in animator.runtimeAnimatorController.animationClips.Where(c => Clips.Contains(c.name)))
                foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    if (b.type == typeof(SpriteRenderer))
                        foreach (var kf in AnimationUtility.GetObjectReferenceCurve(clip, b))
                            if (kf.value is Sprite s && !sprites.Contains(s)) sprites.Add(s);

            var names = new List<string>(); var mins = new List<float>(); var maxs = new List<float>();
            var textureCache = new Dictionary<string, Texture2D>();
            foreach (var s in sprites)
            {
                if (!TryMeasure(s, textureCache, out float minX, out float maxX)) continue;
                names.Add(s.name); mins.Add(minX); maxs.Add(maxX);
                Debug.Log($"[SpriteEdgeBaker] {s.name}: opaque x [{minX:F3}, {maxX:F3}]  mesh x [{s.vertices.Min(v => v.x):F3}, {s.vertices.Max(v => v.x):F3}]");
            }
            foreach (var t in textureCache.Values) Object.DestroyImmediate(t);

            var so = new SerializedObject(visuals);
            Fill(so.FindProperty("edgeSpriteNames"), names.Count, (p, i) => p.stringValue = names[i]);
            Fill(so.FindProperty("edgeMinX"), mins.Count, (p, i) => p.floatValue = mins[i]);
            Fill(so.FindProperty("edgeMaxX"), maxs.Count, (p, i) => p.floatValue = maxs[i]);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(visuals);
            Debug.Log($"[SpriteEdgeBaker] baked {names.Count} sprites from {string.Join(", ", Clips)}");
        }

        static void Fill(SerializedProperty array, int count, System.Action<SerializedProperty, int> set)
        {
            array.arraySize = count;
            for (int i = 0; i < count; i++) set(array.GetArrayElementAtIndex(i), i);
        }

        /// <summary>Opaque left/right edge of the sprite in local units relative to its pivot, unflipped.</summary>
        public static bool TryMeasure(Sprite s, Dictionary<string, Texture2D> cache, out float minX, out float maxX)
        {
            minX = maxX = 0f;
            var path = AssetDatabase.GetAssetPath(s.texture);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            if (!cache.TryGetValue(path, out var tex))
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path))) return false;   // works without Read/Write enabled
                cache[path] = tex;
            }

            int x0 = Mathf.RoundToInt(s.rect.x), y0 = Mathf.RoundToInt(s.rect.y);
            int w = Mathf.RoundToInt(s.rect.width), h = Mathf.RoundToInt(s.rect.height);
            var px = tex.GetPixels32();
            int colMin = int.MaxValue, colMax = int.MinValue;
            for (int y = 0; y < h; y++)
            {
                int row = (y0 + y) * tex.width;
                for (int x = 0; x < w; x++)
                {
                    if (px[row + x0 + x].a <= AlphaThreshold) continue;
                    if (x < colMin) colMin = x;
                    if (x > colMax) colMax = x;
                }
            }
            if (colMin == int.MaxValue) return false;

            float ppu = s.pixelsPerUnit;
            minX = (colMin - s.pivot.x) / ppu;           // left edge of the leftmost opaque pixel
            maxX = (colMax + 1 - s.pivot.x) / ppu;       // right edge of the rightmost opaque pixel
            return true;
        }
    }
}
#endif
