using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hollowknight.Silksong.Editor
{
    // Asset-writing layer of the tk2d animation converter (the pure parsing, geometry and planning is in
    // Tk2dAnimationData). Editor only. Reads the exported animation library and its sprite collection, cuts each
    // sprite out of the atlas PNG into outputFolder/Sprites, and writes an AnimationClip that swaps
    // SpriteRenderer.m_Sprite at i / fps with frame events as AnimationEvents.
    //
    // Frame events: functionName "OnFrameEvent" (what UnitySpriteAnimator forwards), intParameter = frame index,
    // stringParameter = the tk2d eventInfo, floatParameter = the tk2d eventFloat. The tk2d eventInt is not kept
    // (an AnimationEvent has only one int, and it carries the frame index).
    public static class Tk2dAnimationConverter
    {
        public const string EventFunctionName = "OnFrameEvent";

        // Test/tool seam: where to find a sprite collection prefab for a guid. Null = search the export's
        // Collections folder (the export root is the "Assets" folder above the animation file).
        public static Func<string, string, string> CollectionResolver;

        static readonly Dictionary<string, string> collectionByGuid = new Dictionary<string, string>();

        // exportedAnimationPath: a tk2d animation library file, optionally followed by '#' and a clip name
        // ("...\Animations\Knight.prefab#Run"). Without a clip name the library must hold exactly one clip.
        // The clip is written to outputFolder (a project-relative "Assets/..." folder) and returned.
        public static AnimationClip Convert(string exportedAnimationPath, string outputFolder)
        {
            SplitPath(exportedAnimationPath, out string file, out string clipName);
            return Convert(file, clipName, outputFolder, null);
        }

        // Same, with the clip picked by name and saved under outputName (default: the tk2d name). For a
        // LoopSection clip the loop part is written next to it as "<name> Loop"; only the main clip is returned.
        public static AnimationClip Convert(string libraryFile, string clipName, string outputFolder, string outputName)
        {
            var library = LoadLibrary(libraryFile);
            return ConvertFrom(library, libraryFile, clipName, outputFolder, outputName);
        }

        public class Library
        {
            public List<Tk2dClip> Clips;
            public Dictionary<string, List<Tk2dSpriteDef>> CollectionsByGuid = new Dictionary<string, List<Tk2dSpriteDef>>();
            public Dictionary<string, string> CollectionFolderByGuid = new Dictionary<string, string>();
        }

        public static Library LoadLibrary(string libraryFile)
        {
            if (!File.Exists(libraryFile)) throw new FileNotFoundException("Exported animation file not found", libraryFile);
            return new Library { Clips = Tk2dAnimationData.ParseClips(File.ReadAllText(libraryFile)) };
        }

        public static AnimationClip ConvertFrom(Library library, string libraryFile, string clipName, string outputFolder, string outputName)
        {
            Tk2dClip clip = FindClip(library, clipName);
            var plans = Tk2dAnimationData.Plan(clip, outputName);
            if (plans.UnsupportedFeature != null) Stubs.NotImplemented(plans.UnsupportedFeature);

            EnsureFolder(outputFolder);
            string spriteFolder = outputFolder + "/Sprites";
            EnsureFolder(spriteFolder);

            var sprites = LoadSprites(library, libraryFile, clip, spriteFolder);
            var main = WriteClip(plans.Main, sprites, outputFolder);
            if (plans.LoopPart != null) WriteClip(plans.LoopPart, sprites, outputFolder);
            AssetDatabase.SaveAssets();
            return main;
        }

        // ---------- clips ----------

        static AnimationClip WriteClip(ClipPlan plan, Sprite[] spritesBySourceFrame, string folder)
        {
            var clip = new AnimationClip { name = plan.Name, frameRate = plan.FrameRate };
            var keys = new ObjectReferenceKeyframe[plan.SourceFrames.Count];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / plan.FrameRate, value = spritesBySourceFrame[plan.SourceFrames[i]] };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = plan.Loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var events = new AnimationEvent[plan.Events.Count];
            for (int i = 0; i < events.Length; i++)
            {
                var e = plan.Events[i];
                events[i] = new AnimationEvent
                {
                    time = e.Time,
                    functionName = EventFunctionName,
                    intParameter = e.FrameIndex,
                    stringParameter = e.Info,
                    floatParameter = e.EventFloat
                };
            }
            AnimationUtility.SetAnimationEvents(clip, events);

            string path = folder + "/" + SafeFileName(plan.Name) + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                // Keep the asset (and its guid, so scenes keep their references) and replace its contents.
                EditorUtility.CopySerialized(clip, existing);
                UnityEngine.Object.DestroyImmediate(clip);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // ---------- sprites ----------

        // One imported Sprite per frame of the clip, in the clip's frame order.
        static Sprite[] LoadSprites(Library library, string libraryFile, Tk2dClip clip, string spriteFolder)
        {
            string guid = clip.CollectionGuid;
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Clip '" + clip.Name + "' has no sprite collection");
            if (!library.CollectionsByGuid.TryGetValue(guid, out var defs))
            {
                string collectionFile = FindCollection(libraryFile, guid);
                defs = Tk2dAnimationData.ParseSpriteDefinitions(File.ReadAllText(collectionFile));
                library.CollectionsByGuid[guid] = defs;
                library.CollectionFolderByGuid[guid] = Path.GetDirectoryName(collectionFile);
            }
            string atlasFolder = library.CollectionFolderByGuid[guid];

            var atlases = new Dictionary<int, AtlasPixels>();
            var result = new Sprite[clip.Frames.Count];
            var byId = new Dictionary<int, Sprite>();
            try
            {
                var pending = new List<(int id, string path, SpriteCut cut)>();
                for (int i = 0; i < clip.Frames.Count; i++)
                {
                    int id = clip.Frames[i].SpriteId;
                    if (byId.ContainsKey(id) || pending.Exists(p => p.id == id)) continue;
                    if (id < 0 || id >= defs.Count) throw new InvalidOperationException("Sprite id " + id + " is outside the collection (" + defs.Count + ") for clip " + clip.Name);
                    var def = defs[id];
                    if (!def.HasGeometry) throw new InvalidOperationException("Sprite " + id + " (" + def.Name + ") has no geometry");

                    if (!atlases.TryGetValue(def.MaterialId, out var atlas))
                    {
                        string atlasFile = Path.Combine(atlasFolder, "atlas" + def.MaterialId + ".png");
                        if (!File.Exists(atlasFile)) throw new FileNotFoundException("Atlas for material " + def.MaterialId + " not found", atlasFile);
                        atlas = AtlasPixels.Load(atlasFile);
                        atlases[def.MaterialId] = atlas;
                    }
                    var cut = Tk2dAnimationData.ComputeCut(def, atlas.Width, atlas.Height);
                    if (cut.ExtentDeviation > 0.01f)
                        Debug.LogWarning("[Tk2dAnimationConverter] Sprite " + def.Name + ": cut size differs from tk2d extents by " + cut.ExtentDeviation);

                    string pngPath = spriteFolder + "/" + SafeFileName(def.Name) + ".png";
                    string fullPath = Path.GetFullPath(pngPath);
                    var pixels = Tk2dAnimationData.ExtractPixels(atlas.Pixels, atlas.Width, cut);
                    var tex = new Texture2D(cut.Width, cut.Height, TextureFormat.RGBA32, false);
                    tex.SetPixels32(pixels);
                    tex.Apply();
                    byte[] png = tex.EncodeToPNG();
                    UnityEngine.Object.DestroyImmediate(tex);
                    if (!File.Exists(fullPath) || !SameBytes(File.ReadAllBytes(fullPath), png))
                        File.WriteAllBytes(fullPath, png);
                    pending.Add((id, pngPath, cut));
                }

                AssetDatabase.Refresh();
                foreach (var p in pending)
                {
                    ApplySpriteSettings(p.path, p.cut);
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p.path);
                    if (sprite == null) throw new InvalidOperationException("Could not import sprite " + p.path);
                    byId[p.id] = sprite;
                }
            }
            finally
            {
                atlases.Clear();
            }
            for (int i = 0; i < result.Length; i++) result[i] = byId[clip.Frames[i].SpriteId];
            return result;
        }

        static void ApplySpriteSettings(string path, SpriteCut cut)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("No importer for " + path);
            var current = new TextureImporterSettings();
            importer.ReadTextureSettings(current);
            // Already imported with these settings (a second clip sharing the sprite): skip the slow reimport.
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                && Mathf.Approximately(importer.spritePixelsPerUnit, cut.PixelsPerUnit)
                && current.spriteAlignment == (int)SpriteAlignment.Custom
                && Mathf.Approximately(current.spritePivot.x, cut.PivotX) && Mathf.Approximately(current.spritePivot.y, cut.PivotY)
                && importer.filterMode == FilterMode.Bilinear && !importer.mipmapEnabled)
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = cut.PixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;   // the collection's textureFilterMode is 1 (bilinear)
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = new Vector2(cut.PivotX, cut.PivotY);
            importer.SetTextureSettings(s);
            importer.SaveAndReimport();
        }

        sealed class AtlasPixels
        {
            public int Width, Height;
            public Color32[] Pixels;

            public static AtlasPixels Load(string file)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!tex.LoadImage(File.ReadAllBytes(file))) throw new InvalidOperationException("Could not decode " + file);
                    return new AtlasPixels { Width = tex.width, Height = tex.height, Pixels = tex.GetPixels32() };
                }
                finally { UnityEngine.Object.DestroyImmediate(tex); }
            }
        }

        // ---------- lookups ----------

        static Tk2dClip FindClip(Library library, string clipName)
        {
            return Tk2dAnimationData.FindClip(library.Clips, clipName);
        }

        static string FindCollection(string libraryFile, string guid)
        {
            if (collectionByGuid.TryGetValue(guid, out var cached) && File.Exists(cached)) return cached;
            string found = null;
            if (CollectionResolver != null) found = CollectionResolver(libraryFile, guid);
            if (found == null)
            {
                // The export root is the "Assets" folder; the library sits in Assets/Animations.
                string dir = Path.GetDirectoryName(Path.GetFullPath(libraryFile));
                while (!string.IsNullOrEmpty(dir) && Path.GetFileName(dir) != "Assets") dir = Path.GetDirectoryName(dir);
                if (!string.IsNullOrEmpty(dir))
                {
                    string collections = Path.Combine(dir, "Collections");
                    if (Directory.Exists(collections))
                    {
                        foreach (var meta in Directory.EnumerateFiles(collections, "*.prefab.meta", SearchOption.AllDirectories))
                        {
                            if (!File.ReadAllText(meta).Contains("guid: " + guid)) continue;
                            found = meta.Substring(0, meta.Length - ".meta".Length);
                            break;
                        }
                    }
                }
            }
            if (found == null || !File.Exists(found)) throw new FileNotFoundException("Sprite collection with guid " + guid + " not found next to " + libraryFile);
            collectionByGuid[guid] = found;
            return found;
        }

        // ---------- small helpers ----------

        public static void SplitPath(string path, out string file, out string clipName)
        {
            int hash = path.LastIndexOf('#');
            if (hash < 0) { file = path; clipName = null; }
            else { file = path.Substring(0, hash); clipName = path.Substring(hash + 1); }
        }

        public static string SafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("Output folder must be inside Assets: " + folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
