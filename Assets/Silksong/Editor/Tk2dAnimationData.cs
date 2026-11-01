using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hollowknight.Silksong.Editor
{
    // Pure layer of the tk2d animation converter: reads the exported YAML text and does all the geometry and
    // timeline planning with plain numbers. It touches no Unity assets, so EditMode tests feed it small strings.
    // The asset-writing layer is Tk2dAnimationConverter.

    // tk2d's wrap modes (the numbers are what the export stores in wrapMode).
    public enum Tk2dWrapMode
    {
        Loop = 0,         // plays through and starts over from frame 0
        LoopSection = 1,  // plays through once, then loops from loopStart to the end
        Once = 2,         // plays through and stops on the last frame
        PingPong = 3,     // forward then backward, repeating
        RandomFrame = 4,  // picks a random frame
        RandomLoop = 5,   // loops from a random frame
        Single = 6        // holds one frame
    }

    public class Tk2dFrame
    {
        public int SpriteId;
        public string CollectionGuid;
        public bool TriggerEvent;
        public string EventInfo = "";
        public int EventInt;
        public float EventFloat;
    }

    public class Tk2dClip
    {
        public string Name = "";
        public float Fps = 30f;
        public int LoopStart;
        public Tk2dWrapMode WrapMode;
        public List<Tk2dFrame> Frames = new List<Tk2dFrame>();

        // The collection the first frame points at (all frames of a clip use the same one in the export).
        public string CollectionGuid => Frames.Count > 0 ? Frames[0].CollectionGuid : null;
    }

    public class Tk2dSpriteDef
    {
        public int Id;
        public string Name = "";
        public int MaterialId;
        public bool Flipped;
        public float TexelX, TexelY;
        // Bounding box of the uvs (0..1 of the atlas) and of the positions (tk2d units, origin = sprite origin).
        public float UMin, VMin, UMax, VMax;
        public float XMin, YMin, XMax, YMax;
        public bool HasGeometry;
    }

    // Where a sprite lives in its atlas and how to rebuild it.
    public struct SpriteCut
    {
        public int AtlasX, AtlasY;     // lower-left of the stored rectangle in the atlas, in pixels
        public int StoredW, StoredH;   // size of that rectangle as stored (swapped when flipped)
        public int Width, Height;      // size of the upright sprite
        public bool Rotated;           // stored turned 90 degrees
        public float PivotX, PivotY;   // 0..1 within the upright sprite; puts the tk2d origin on the pivot
        public float PixelsPerUnit;
        public float ExtentDeviation;  // |pixel size * texel - positions extent|, a sanity check (0 when exact)
    }

    public class PlanEvent
    {
        public float Time;
        public int FrameIndex;   // position on the planned timeline (equals the source frame unless unrolled)
        public string Info = "";
        public int EventInt;
        public float EventFloat;
    }

    // What one Unity clip should contain.
    public class ClipPlan
    {
        public string Name;
        public float FrameRate;
        public bool Loop;
        public List<int> SourceFrames = new List<int>();   // timeline order: index into the tk2d clip's frames
        public List<PlanEvent> Events = new List<PlanEvent>();
        public float Length => SourceFrames.Count / FrameRate;
    }

    // The clips a tk2d clip becomes: always a main clip; LoopSection also gets a second clip for the loop part.
    public class ClipPlanSet
    {
        public ClipPlan Main;
        public ClipPlan LoopPart;        // null unless LoopSection with a valid loopStart
        public string UnsupportedFeature; // name for Stubs.NotImplemented when Unity cannot express the mode
    }

    public static class Tk2dAnimationData
    {
        // ---------- parsing ----------

        // Reads the `clips:` list of a tk2d animation library (the Animations/*.prefab files).
        public static List<Tk2dClip> ParseClips(string yaml)
        {
            var clips = new List<Tk2dClip>();
            if (yaml == null) return clips;
            string[] lines = yaml.Split('\n');
            int i = 0;
            while (i < lines.Length && Clean(lines[i]) != "  clips:") i++;
            i++;
            Tk2dClip clip = null;
            Tk2dFrame frame = null;
            for (; i < lines.Length; i++)
            {
                string line = Clean(lines[i]);
                if (line.Length == 0) continue;
                int indent = Indent(line);
                if (indent <= 2 && !line.StartsWith("  - ")) break;   // next top-level field: the list is over

                if (indent == 2 && line.StartsWith("  - name:"))
                {
                    clip = new Tk2dClip { Name = Unquote(Value(line, "  - name:")) };
                    clips.Add(clip);
                    frame = null;
                    continue;
                }
                if (clip == null) continue;

                string t = line.Trim();
                if (indent == 4)
                {
                    if (t.StartsWith("fps:")) clip.Fps = Float(Value(t, "fps:"), 30f);
                    else if (t.StartsWith("loopStart:")) clip.LoopStart = Int(Value(t, "loopStart:"));
                    else if (t.StartsWith("wrapMode:")) clip.WrapMode = (Tk2dWrapMode)Int(Value(t, "wrapMode:"));
                    else if (t.StartsWith("- spriteCollection:"))
                    {
                        frame = new Tk2dFrame { CollectionGuid = GuidOf(t) };
                        clip.Frames.Add(frame);
                    }
                }
                else if (indent == 6 && frame != null)
                {
                    if (t.StartsWith("spriteId:")) frame.SpriteId = Int(Value(t, "spriteId:"));
                    else if (t.StartsWith("triggerEvent:")) frame.TriggerEvent = Int(Value(t, "triggerEvent:")) != 0;
                    else if (t.StartsWith("eventInfo:")) frame.EventInfo = Unquote(Value(t, "eventInfo:"));
                    else if (t.StartsWith("eventInt:")) frame.EventInt = Int(Value(t, "eventInt:"));
                    else if (t.StartsWith("eventFloat:")) frame.EventFloat = Float(Value(t, "eventFloat:"), 0f);
                }
            }
            return clips;
        }

        // Reads the `spriteDefinitions:` list of a tk2d sprite collection (Collections/*/*.prefab). The list index
        // is the sprite id, so empty definitions are kept (HasGeometry false) to keep ids lined up.
        public static List<Tk2dSpriteDef> ParseSpriteDefinitions(string yaml)
        {
            var defs = new List<Tk2dSpriteDef>();
            if (yaml == null) return defs;
            string[] lines = yaml.Split('\n');
            int i = 0;
            while (i < lines.Length && Clean(lines[i]) != "  spriteDefinitions:") i++;
            i++;
            Tk2dSpriteDef def = null;
            string list = null;
            bool haveUv = false, havePos = false;
            for (; i < lines.Length; i++)
            {
                string line = Clean(lines[i]);
                if (line.Length == 0) continue;
                int indent = Indent(line);
                if (indent <= 2 && !line.StartsWith("  - ")) break;

                if (indent == 2 && line.StartsWith("  - name:"))
                {
                    def = new Tk2dSpriteDef { Id = defs.Count, Name = Unquote(Value(line, "  - name:")) };
                    defs.Add(def);
                    list = null;
                    haveUv = havePos = false;
                    continue;
                }
                if (def == null) continue;

                string t = line.Trim();
                if (indent == 4 && t.StartsWith("- {"))
                {
                    if (list == "positions")
                    {
                        float x = Axis(t, 'x'), y = Axis(t, 'y');
                        if (!havePos) { def.XMin = def.XMax = x; def.YMin = def.YMax = y; havePos = true; }
                        else { def.XMin = Math.Min(def.XMin, x); def.XMax = Math.Max(def.XMax, x); def.YMin = Math.Min(def.YMin, y); def.YMax = Math.Max(def.YMax, y); }
                    }
                    else if (list == "uvs")
                    {
                        float u = Axis(t, 'x'), v = Axis(t, 'y');
                        if (!haveUv) { def.UMin = def.UMax = u; def.VMin = def.VMax = v; haveUv = true; }
                        else { def.UMin = Math.Min(def.UMin, u); def.UMax = Math.Max(def.UMax, u); def.VMin = Math.Min(def.VMin, v); def.VMax = Math.Max(def.VMax, v); }
                    }
                }
                else if (indent == 4)
                {
                    // A "key: value" line ends any list; a bare "key:" starts one (positions:, uvs:, boundsData:, ...).
                    list = t.EndsWith(":") ? t.Substring(0, t.Length - 1) : null;
                    if (t.StartsWith("texelSize:")) { def.TexelX = Axis(t, 'x'); def.TexelY = Axis(t, 'y'); }
                    else if (t.StartsWith("materialId:")) def.MaterialId = Int(Value(t, "materialId:"));
                    else if (t.StartsWith("flipped:")) def.Flipped = Int(Value(t, "flipped:")) != 0;
                }
                def.HasGeometry = havePos && haveUv;
            }
            return defs;
        }

        // Picks a clip by name. The export holds duplicate names (534 clips, 501 distinct), so the rule is fixed:
        // the FIRST clip with that name in file order wins. With no name, the library must hold exactly one clip.
        public static Tk2dClip FindClip(IList<Tk2dClip> clips, string clipName)
        {
            if (string.IsNullOrEmpty(clipName))
            {
                if (clips.Count == 1) return clips[0];
                throw new ArgumentException("The library holds " + clips.Count + " clips; name one with 'path#ClipName'");
            }
            for (int i = 0; i < clips.Count; i++) if (clips[i].Name == clipName) return clips[i];
            throw new KeyNotFoundException("No clip named '" + clipName + "' in the library");
        }

        // ---------- geometry ----------

        // The recipe from the spike (docs/study/00-spike-report.md, Check C): the pixel rectangle is the uv box
        // times the atlas size; a flipped sprite is stored turned 90 degrees (its x runs along atlas v, its y
        // along atlas u); pixels per unit is 1 / texelSize; the pivot puts the tk2d origin (0,0) on the pivot.
        public static SpriteCut ComputeCut(Tk2dSpriteDef def, int atlasWidth, int atlasHeight)
        {
            if (def == null || !def.HasGeometry) throw new ArgumentException("Sprite definition has no geometry");
            if (def.TexelX <= 0f || def.TexelY <= 0f) throw new ArgumentException("Sprite definition has no texel size: " + def.Name);
            double u0 = def.UMin * (double)atlasWidth, u1 = def.UMax * (double)atlasWidth;
            double v0 = def.VMin * (double)atlasHeight, v1 = def.VMax * (double)atlasHeight;
            var cut = new SpriteCut
            {
                AtlasX = (int)Math.Round(u0),
                AtlasY = (int)Math.Round(v0),
                StoredW = (int)Math.Round(u1 - u0),
                StoredH = (int)Math.Round(v1 - v0),
                Rotated = def.Flipped
            };
            cut.Width = def.Flipped ? cut.StoredH : cut.StoredW;
            cut.Height = def.Flipped ? cut.StoredW : cut.StoredH;
            if (cut.Width <= 0 || cut.Height <= 0) throw new ArgumentException("Sprite has an empty rectangle: " + def.Name);
            cut.PixelsPerUnit = 1f / def.TexelX;
            cut.PivotX = (float)(-def.XMin / def.TexelX / cut.Width);
            cut.PivotY = (float)(-def.YMin / def.TexelY / cut.Height);
            double devW = Math.Abs(cut.Width * (double)def.TexelX - (def.XMax - def.XMin));
            double devH = Math.Abs(cut.Height * (double)def.TexelY - (def.YMax - def.YMin));
            cut.ExtentDeviation = (float)Math.Max(devW, devH);
            return cut;
        }

        // Rebuilds the upright sprite's pixels from an atlas (rows bottom to top, like Texture2D.GetPixels).
        public static T[] ExtractPixels<T>(T[] atlas, int atlasWidth, SpriteCut cut)
        {
            var result = new T[cut.Width * cut.Height];
            for (int y = 0; y < cut.Height; y++)
            {
                for (int x = 0; x < cut.Width; x++)
                {
                    // Stored (not rotated): sprite x along u, sprite y along v. Rotated: sprite x along v, y along u.
                    int su = cut.Rotated ? y : x;
                    int sv = cut.Rotated ? x : y;
                    result[y * cut.Width + x] = atlas[(cut.AtlasY + sv) * atlasWidth + (cut.AtlasX + su)];
                }
            }
            return result;
        }

        // ---------- timeline planning ----------

        // Turns a tk2d clip into the key order, looping and events of the Unity clip(s). Keys sit at i / fps and
        // there is no closing key, so the Unity clip's length is exactly frameCount / fps.
        public static ClipPlanSet Plan(Tk2dClip clip, string outputName = null)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            if (clip.Frames.Count == 0) throw new ArgumentException("Clip has no frames: " + clip.Name);
            if (clip.Fps <= 0f) throw new ArgumentException("Clip has no frame rate: " + clip.Name);
            string name = string.IsNullOrEmpty(outputName) ? clip.Name : outputName;
            int n = clip.Frames.Count;
            var set = new ClipPlanSet();

            switch (clip.WrapMode)
            {
                case Tk2dWrapMode.Loop:
                    set.Main = Build(clip, name, Range(0, n), true);
                    break;
                case Tk2dWrapMode.Once:
                case Tk2dWrapMode.Single:
                    set.Main = Build(clip, name, Range(0, n), false);
                    break;
                case Tk2dWrapMode.PingPong:
                {
                    // Forward then back without repeating the end frames, so looping the unrolled clip is seamless.
                    var order = Range(0, n);
                    for (int k = n - 2; k >= 1; k--) order.Add(k);
                    set.Main = Build(clip, name, order, true);
                    break;
                }
                case Tk2dWrapMode.LoopSection:
                {
                    // Unity has one loop flag per clip, no loop section. Intro clip plays once; the loop part is a
                    // separate looping clip. UnitySpriteAnimator chains them (intro, then "<name> Loop").
                    set.Main = Build(clip, name, Range(0, n), false);
                    int start = clip.LoopStart;
                    if (start >= 0 && start < n)
                        set.LoopPart = Build(clip, name + " Loop", Range(start, n - start), true);
                    break;
                }
                case Tk2dWrapMode.RandomFrame:
                case Tk2dWrapMode.RandomLoop:
                    // Unity has no random frame order; play it as a plain loop.
                    set.Main = Build(clip, name, Range(0, n), true);
                    set.UnsupportedFeature = "tk2d RandomFrame/RandomLoop wrap modes (played as a plain loop)";
                    break;
                default:
                    throw new ArgumentException("Unknown tk2d wrap mode " + (int)clip.WrapMode + " in clip " + clip.Name);
            }
            return set;
        }

        static List<int> Range(int start, int count)
        {
            var l = new List<int>(count);
            for (int k = 0; k < count; k++) l.Add(start + k);
            return l;
        }

        static ClipPlan Build(Tk2dClip clip, string name, List<int> order, bool loop)
        {
            var plan = new ClipPlan { Name = name, FrameRate = clip.Fps, Loop = loop, SourceFrames = order };
            for (int pos = 0; pos < order.Count; pos++)
            {
                var f = clip.Frames[order[pos]];
                if (!f.TriggerEvent) continue;
                plan.Events.Add(new PlanEvent
                {
                    Time = pos / clip.Fps,
                    FrameIndex = pos,
                    Info = f.EventInfo ?? "",
                    EventInt = f.EventInt,
                    EventFloat = f.EventFloat
                });
            }
            return plan;
        }

        // ---------- small text helpers ----------

        static string Clean(string line) => line.TrimEnd('\r');

        static int Indent(string line)
        {
            int n = 0;
            while (n < line.Length && line[n] == ' ') n++;
            return n;
        }

        static string Value(string line, string key)
        {
            int at = line.IndexOf(key, StringComparison.Ordinal);
            return at < 0 ? "" : line.Substring(at + key.Length).Trim();
        }

        static string Unquote(string v)
        {
            if (v.Length >= 2 && ((v[0] == '\'' && v[v.Length - 1] == '\'') || (v[0] == '"' && v[v.Length - 1] == '"')))
                v = v.Substring(1, v.Length - 2).Replace("''", "'");
            return v;
        }

        static int Int(string v) => int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) ? r : 0;

        static float Float(string v, float fallback) =>
            float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ? r : fallback;

        // "{x: 0.5, y: 2}" style: the number after "x:" / "y:".
        static float Axis(string text, char axis)
        {
            string key = axis + ":";
            int at = text.IndexOf(key, StringComparison.Ordinal);
            if (at < 0) return 0f;
            int s = at + key.Length;
            int e = s;
            while (e < text.Length && text[e] != ',' && text[e] != '}') e++;
            return Float(text.Substring(s, e - s).Trim(), 0f);
        }

        static string GuidOf(string text)
        {
            int at = text.IndexOf("guid:", StringComparison.Ordinal);
            if (at < 0) return null;
            int s = at + 5;
            while (s < text.Length && text[s] == ' ') s++;
            int e = s;
            while (e < text.Length && text[e] != ',' && text[e] != '}' && text[e] != ' ') e++;
            return text.Substring(s, e - s);
        }
    }
}
