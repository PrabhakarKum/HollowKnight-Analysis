#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Hollowknight.Enemies;
using Look = Hollowknight.Enemies.EnemyLook;

namespace Hollowknight.EditorTools
{
    /// <summary>
    /// Makes the enemy art: recoloured COPIES of the DeadRevolver "Pixel Prototype Player" frames (the original pack is never touched),
    /// written to Assets/EnemyArt/&lt;Type&gt;/. For each enemy type it copies only the animation clips that type needs, recolours every
    /// frame, re-imports them with the pack's import settings (32 pixels per unit, point filter), rebuilds each clip, and builds an
    /// AnimatorController with one state per clip. Projectile and effect frames (bullet, arrow, blades, muzzle flash, impacts) are
    /// copied and tinted the same way.
    ///
    /// The generated folder is derived from a licensed pack, so it is in .gitignore: only this script is committed. After cloning,
    /// re-import the pack, run Hollowknight &gt; Generate Enemy Variants, then Hollowknight &gt; Build Enemy Test Arena.
    /// </summary>
    public static class EnemyArtGenerator
    {
        public const string Pack = "Assets/DeadRevolver/PixelPrototypePlayerSprites/Art/";
        public const string Out = "Assets/EnemyArt/";

        // the palettes and clip lists live in Hollowknight.Enemies.EnemyBuilder (shared with the builder)
        static EnemyLook[] Looks => EnemyBuilder.Looks;

        // clips that loop; every other copied clip plays once
        static readonly HashSet<string> Looping = new HashSet<string>
        {
            "Idle", "Walk", "Run", "Sprint", "JumpMid", "JumpFall", "Stunned", "GunWalk", "GunRun", "GunAim", "BowAim",
        };

        [MenuItem("Hollowknight/Generate Enemy Variants")]
        public static void Generate()
        {
            if (!Directory.Exists(Path.GetFullPath(Pack)))
            {
                Debug.LogError("The DeadRevolver pack is not at " + Pack + " - re-import it first (it is not part of the repository).");
                return;
            }

            if (AssetDatabase.IsValidFolder(Out.TrimEnd('/'))) AssetDatabase.DeleteAsset(Out.TrimEnd('/'));
            Directory.CreateDirectory(Path.GetFullPath(Out));

            // ---- phase 1: write the recoloured PNGs (the import postprocessor below applies the pack's sprite settings)
            var plan = new List<(Look look, string clip, List<(float time, string dst)> frames)>();
            int written = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var look in Looks)
                {
                    foreach (var clipName in look.clips)
                    {
                        var src = AssetDatabase.LoadAssetAtPath<AnimationClip>(Pack + "Animations/" + clipName + ".anim");
                        if (src == null) { Debug.LogWarning("Enemy art: pack clip not found: " + clipName); continue; }
                        var binding = AnimationUtility.GetObjectReferenceCurveBindings(src).FirstOrDefault(b => b.propertyName == "m_Sprite");
                        var keys = AnimationUtility.GetObjectReferenceCurve(src, binding);
                        var frames = new List<(float, string)>();
                        foreach (var k in keys)
                        {
                            var sprite = k.value as Sprite;
                            if (sprite == null) continue;
                            string srcPath = AssetDatabase.GetAssetPath(sprite);
                            string dst = Out + look.name + "/Sprites/" + clipName + "/" + Path.GetFileName(srcPath);
                            if (!File.Exists(Path.GetFullPath(dst))) { RecolorPng(srcPath, dst, look); written++; }
                            frames.Add((k.time, dst));
                        }
                        plan.Add((look, clipName, frames));
                    }
                    foreach (var folder in look.projectileFolders)
                    {
                        string srcDir = Pack + "Sprites/" + folder;
                        if (!AssetDatabase.IsValidFolder(srcDir)) { Debug.LogWarning("Enemy art: folder not found: " + srcDir); continue; }
                        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { srcDir }))
                        {
                            string srcPath = AssetDatabase.GUIDToAssetPath(guid);
                            string dst = Out + look.name + "/" + (folder.StartsWith("FX") ? "FX/" : "Projectile/") + Path.GetFileName(Path.GetDirectoryName(srcPath)) + "/" + Path.GetFileName(srcPath);
                            if (!File.Exists(Path.GetFullPath(dst))) { RecolorPng(srcPath, dst, look); written++; }
                        }
                    }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // ---- phase 2: rebuild the clips and one controller per enemy type
            foreach (var look in Looks)
            {
                string dir = Out + look.name + "/";
                Directory.CreateDirectory(Path.GetFullPath(dir + "Clips"));
                var controller = AnimatorController.CreateAnimatorControllerAtPath(dir + look.name + ".controller");
                var sm = controller.layers[0].stateMachine;
                AnimatorState idleState = null;
                foreach (var (l, clipName, frames) in plan.Where(p => p.look == look))
                {
                    var src = AssetDatabase.LoadAssetAtPath<AnimationClip>(Pack + "Animations/" + clipName + ".anim");
                    var clip = new AnimationClip { frameRate = src.frameRate, name = clipName };
                    var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
                    var keys = new List<ObjectReferenceKeyframe>();
                    foreach (var (time, dst) in frames)
                    {
                        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(dst);
                        if (sprite != null) keys.Add(new ObjectReferenceKeyframe { time = time, value = sprite });
                    }
                    AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
                    var settings = AnimationUtility.GetAnimationClipSettings(src);
                    settings.loopTime = Looping.Contains(clipName);
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    AssetDatabase.CreateAsset(clip, dir + "Clips/" + clipName + ".anim");

                    var state = sm.AddState(clipName);
                    state.motion = clip;
                    if (clipName == "Idle") idleState = state;
                }
                if (idleState != null) sm.defaultState = idleState;
                EditorUtility.SetDirty(controller);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Enemy art generated in " + Out + " (" + written + " recoloured frames, " + Looks.Length + " enemy types). The folder is git-ignored; it is derived from the licensed pack.");
        }

        // ------------------------------------------------------------------ recolouring

        static void RecolorPng(string srcAssetPath, string dstAssetPath, Look look)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Path.GetFullPath(srcAssetPath)));
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0) continue;
                px[i] = Recolor(px[i], look);
            }
            tex.SetPixels32(px);
            tex.Apply();
            string full = Path.GetFullPath(dstAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>Grey / white body pixels go onto the enemy's colour ramp by brightness; saturated accent pixels (the weapons) are set to the accent hue.</summary>
        public static Color32 Recolor(Color32 c, Look look)
        {
            Color col = c;
            Color.RGBToHSV(col, out float h, out float s, out float v);
            Color result;
            if (s < 0.4f)
            {
                float lum = Mathf.Pow(col.grayscale, 1.6f);                       // push the mid greys darker so the shading reads
                result = lum < 0.55f ? Color.Lerp(look.shadow, look.mid, lum / 0.55f) : Color.Lerp(look.mid, look.light, (lum - 0.55f) / 0.45f);
            }
            else
            {
                result = Color.HSVToRGB(look.accentHue, Mathf.Clamp01(s * 0.95f), Mathf.Clamp01(v * 0.95f));
            }
            result.a = col.a;
            return result;
        }
    }

    /// <summary>Applies the pack's sprite import settings to everything generated under Assets/EnemyArt (so each file imports once, correctly).</summary>
    public class EnemyArtImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(EnemyArtGenerator.Out)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 32f;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            bool centred = assetPath.Contains("/Projectile/") || assetPath.Contains("/FX/");
            settings.spriteAlignment = (int)(centred ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);   // characters stand on their pivot
            ti.SetTextureSettings(settings);
        }
    }
}
#endif
