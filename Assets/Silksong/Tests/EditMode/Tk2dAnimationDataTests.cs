using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Hollowknight.Silksong.Tests
{
    using Hollowknight.Silksong.Editor;

    // Pure layer of the tk2d converter: parsing, atlas geometry, timeline planning, hero clip mapping.
    // Fixture text is a small slice of the real export (spike Check C, Knight library clip "Run" and two of its
    // sprite definitions). Nothing here reads the export or touches assets.
    public class Tk2dAnimationDataTests
    {
        const string Guid = "4eb1fd6b386eb6b4fb112603bd5f62d2";

        // Same layout as Animations/Knight.prefab: 2-space list items, 4-space clip fields, 6-space frame fields.
        const string ClipsYaml = @"MonoBehaviour:
  m_Name:
  version: 3
  clips:
  - name: Run
    frames:
    - spriteCollection: {fileID: 114724804207875695, guid: 4eb1fd6b386eb6b4fb112603bd5f62d2, type: 2}
      spriteId: 1162
      triggerEvent: 0
      eventInfo:
      eventInt: 0
      eventFloat: 0
    - spriteCollection: {fileID: 114724804207875695, guid: 4eb1fd6b386eb6b4fb112603bd5f62d2, type: 2}
      spriteId: 1164
      triggerEvent: 0
      eventInfo:
      eventInt: 0
      eventFloat: 0
    - spriteCollection: {fileID: 114724804207875695, guid: 4eb1fd6b386eb6b4fb112603bd5f62d2, type: 2}
      spriteId: 1171
      triggerEvent: 0
      eventInfo:
      eventInt: 0
      eventFloat: 0
    - spriteCollection: {fileID: 114724804207875695, guid: 4eb1fd6b386eb6b4fb112603bd5f62d2, type: 2}
      spriteId: 1170
      triggerEvent: 1
      eventInfo: Footstep
      eventInt: 0
      eventFloat: 0
    - spriteCollection: {fileID: 114724804207875695, guid: 4eb1fd6b386eb6b4fb112603bd5f62d2, type: 2}
      spriteId: 1166
      triggerEvent: 0
      eventInfo:
      eventInt: 0
      eventFloat: 0
    - spriteCollection: {fileID: 114724804207875695, guid: 4eb1fd6b386eb6b4fb112603bd5f62d2, type: 2}
      spriteId: 1168
      triggerEvent: 0
      eventInfo:
      eventInt: 0
      eventFloat: 0
    - spriteCollection: {fileID: 114724804207875695, guid: 4eb1fd6b386eb6b4fb112603bd5f62d2, type: 2}
      spriteId: 1167
      triggerEvent: 0
      eventInfo:
      eventInt: 0
      eventFloat: 0
    - spriteCollection: {fileID: 114724804207875695, guid: 4eb1fd6b386eb6b4fb112603bd5f62d2, type: 2}
      spriteId: 1165
      triggerEvent: 0
      eventInfo:
      eventInt: 0
      eventFloat: 0
    - spriteCollection: {fileID: 114724804207875695, guid: 4eb1fd6b386eb6b4fb112603bd5f62d2, type: 2}
      spriteId: 1163
      triggerEvent: 1
      eventInfo: Footstep
      eventInt: 7
      eventFloat: 0.5
    - spriteCollection: {fileID: 114724804207875695, guid: 4eb1fd6b386eb6b4fb112603bd5f62d2, type: 2}
      spriteId: 1169
      triggerEvent: 0
      eventInfo:
      eventInt: 0
      eventFloat: 0
    fps: 15
    loopStart: 6
    wrapMode: 0
  - name: Fall
    frames:
    - spriteCollection: {fileID: 114724804207875695, guid: 4eb1fd6b386eb6b4fb112603bd5f62d2, type: 2}
      spriteId: 5
      triggerEvent: 0
      eventInfo:
      eventInt: 0
      eventFloat: 0
    fps: 15
    loopStart: 4
    wrapMode: 1
  - name:
    frames: []
    fps: 30
    loopStart: 0
    wrapMode: 0
";

        // Sprite definitions exactly as exported: id 0 is an empty definition, id 1 is Hornet_run_new0000 (not
        // flipped), id 2 is Hornet_run_new0008 (flipped). Other keys of the real file are included so the parser
        // has to skip them.
        const string DefsYaml = @"MonoBehaviour:
  version: 3
  spriteDefinitions:
  - name: empty_sprite
    boundsData:
    colliderConvex: 0
    attachPoints: []
  - name: Hornet_run_new0000
    boundsData:
    - {x: 0.01562506, y: -0.03125006, z: 0}
    - {x: 2.46875, y: 3.0625, z: 0}
    untrimmedBoundsData:
    - {x: 0.0078125, y: -0.0078125, z: 0}
    - {x: 5.953125, y: 3.890625, z: 0}
    texelSize: {x: 0.015625, y: 0.015625}
    positions:
    - {x: -1.21875, y: -1.5625, z: 0}
    - {x: 1.2500001, y: -1.5625, z: 0}
    - {x: -1.21875, y: 1.4999999, z: 0}
    - {x: 1.2500001, y: 1.4999999, z: 0}
    normals: []
    tangents: []
    uvs:
    - {x: 0.23852563, y: 0.47558618}
    - {x: 0.27709937, y: 0.47558618}
    - {x: 0.23852563, y: 0.52343726}
    - {x: 0.27709937, y: 0.52343726}
    normalizedUvs:
    - {x: 0, y: 0}
    - {x: 1, y: 0}
    - {x: 0, y: 1}
    - {x: 1, y: 1}
    indices: 000000000300000001000000020000000300000000000000
    materialId: 3
    flipped: 0
    attachPoints: []
  - name: Hornet_run_new0008
    boundsData:
    - {x: 0.03906256, y: 0.0078125, z: 0}
    - {x: 2.328125, y: 2.953125, z: 0}
    texelSize: {x: 0.015625, y: 0.015625}
    positions:
    - {x: -1.125, y: -1.46875, z: 0}
    - {x: 1.2031251, y: -1.46875, z: 0}
    - {x: -1.125, y: 1.484375, z: 0}
    - {x: 1.2031251, y: 1.484375, z: 0}
    uvs:
    - {x: 0.72314477, y: 0.8430178}
    - {x: 0.72314477, y: 0.8793943}
    - {x: 0.7692869, y: 0.8430178}
    - {x: 0.7692869, y: 0.8793943}
    materialId: 1
    flipped: 1
  premultipliedAlpha: 0
  textureFilterMode: 1
";

        static Tk2dClip MakeClip(Tk2dWrapMode wrap, int frames, float fps = 10f, int loopStart = 0, params int[] eventFrames)
        {
            var clip = new Tk2dClip { Name = "Test", Fps = fps, LoopStart = loopStart, WrapMode = wrap };
            for (int i = 0; i < frames; i++)
                clip.Frames.Add(new Tk2dFrame { SpriteId = 100 + i, TriggerEvent = eventFrames.Contains(i), EventInfo = eventFrames.Contains(i) ? "Ev" + i : "" });
            return clip;
        }

        // ---------- parsing ----------

        [Test]
        public void ParseClips_ReadsNameFpsWrapLoopStartFramesAndEvents()
        {
            var clips = Tk2dAnimationData.ParseClips(ClipsYaml);
            Assert.AreEqual(3, clips.Count);
            var run = clips[0];
            Assert.AreEqual("Run", run.Name);
            Assert.AreEqual(15f, run.Fps);
            Assert.AreEqual(6, run.LoopStart);
            Assert.AreEqual(Tk2dWrapMode.Loop, run.WrapMode);
            Assert.AreEqual(Guid, run.CollectionGuid);
            CollectionAssert.AreEqual(new[] { 1162, 1164, 1171, 1170, 1166, 1168, 1167, 1165, 1163, 1169 }, run.Frames.Select(f => f.SpriteId).ToArray());
            Assert.AreEqual(new[] { 3, 8 }, run.Frames.Select((f, i) => (f, i)).Where(p => p.f.TriggerEvent).Select(p => p.i).ToArray());
            Assert.AreEqual("Footstep", run.Frames[3].EventInfo);
            Assert.AreEqual(7, run.Frames[8].EventInt);
            Assert.AreEqual(0.5f, run.Frames[8].EventFloat);
        }

        [Test]
        public void ParseClips_ReadsLoopSectionMode_AndKeepsTheUnnamedTrailingClip()
        {
            var clips = Tk2dAnimationData.ParseClips(ClipsYaml);
            Assert.AreEqual(Tk2dWrapMode.LoopSection, clips[1].WrapMode);
            Assert.AreEqual(4, clips[1].LoopStart);
            Assert.AreEqual("", clips[2].Name);
            Assert.AreEqual(0, clips[2].Frames.Count);
        }

        [Test]
        public void ParseClips_NoClipsSection_ReturnsEmpty()
        {
            Assert.AreEqual(0, Tk2dAnimationData.ParseClips("MonoBehaviour:\n  m_Name:\n").Count);
            Assert.AreEqual(0, Tk2dAnimationData.ParseClips(null).Count);
        }

        [Test]
        public void ParseClips_WindowsLineEndings_Work()
        {
            var clips = Tk2dAnimationData.ParseClips(ClipsYaml.Replace("\n", "\r\n"));
            Assert.AreEqual(3, clips.Count);
            Assert.AreEqual(15f, clips[0].Fps);
            Assert.AreEqual("Footstep", clips[0].Frames[3].EventInfo);
        }

        [Test]
        public void ParseSpriteDefinitions_ReadsExtentsUvsTexelMaterialAndFlip()
        {
            var defs = Tk2dAnimationData.ParseSpriteDefinitions(DefsYaml);
            Assert.AreEqual(3, defs.Count);
            Assert.IsFalse(defs[0].HasGeometry, "an empty definition keeps its id but has no geometry");
            Assert.AreEqual(1, defs[1].Id);
            Assert.AreEqual("Hornet_run_new0000", defs[1].Name);
            Assert.IsTrue(defs[1].HasGeometry);
            Assert.AreEqual(3, defs[1].MaterialId);
            Assert.IsFalse(defs[1].Flipped);
            Assert.AreEqual(0.015625f, defs[1].TexelX, 1e-8f);
            Assert.AreEqual(-1.21875f, defs[1].XMin, 1e-6f);
            Assert.AreEqual(1.2500001f, defs[1].XMax, 1e-6f);
            Assert.AreEqual(-1.5625f, defs[1].YMin, 1e-6f);
            Assert.AreEqual(1.4999999f, defs[1].YMax, 1e-6f);
            Assert.AreEqual(0.23852563f, defs[1].UMin, 1e-7f);
            Assert.AreEqual(0.27709937f, defs[1].UMax, 1e-7f);
            Assert.AreEqual(0.47558618f, defs[1].VMin, 1e-7f);
            Assert.AreEqual(0.52343726f, defs[1].VMax, 1e-7f);
            Assert.IsTrue(defs[2].Flipped);
            Assert.AreEqual(1, defs[2].MaterialId);
        }

        [Test]
        public void ParseSpriteDefinitions_BoundsAndNormalizedUvsAreNotMistakenForGeometry()
        {
            var d = Tk2dAnimationData.ParseSpriteDefinitions(DefsYaml)[1];
            // boundsData has x up to 2.46875 and untrimmedBoundsData up to 5.95; neither may widen the extents.
            Assert.Less(d.XMax, 1.3f);
            Assert.Less(d.UMax, 0.3f);
        }

        // ---------- geometry ----------

        [Test]
        public void ComputeCut_UnflippedSprite_UsesUvBoxPpuAndOriginPivot()
        {
            var d = Tk2dAnimationData.ParseSpriteDefinitions(DefsYaml)[1];
            var cut = Tk2dAnimationData.ComputeCut(d, 4096, 4096);
            Assert.IsFalse(cut.Rotated);
            Assert.AreEqual(158, cut.Width);
            Assert.AreEqual(196, cut.Height);
            Assert.AreEqual(158, cut.StoredW);
            Assert.AreEqual(64f, cut.PixelsPerUnit, 1e-3f);
            // The tk2d origin is 1.21875 units (78 px) from the left edge and 1.5625 units (100 px) from the bottom.
            Assert.AreEqual(78f / 158f, cut.PivotX, 1e-4f);
            Assert.AreEqual(100f / 196f, cut.PivotY, 1e-4f);
            Assert.Less(cut.ExtentDeviation, 0.01f, "pixel size times texel must match the tk2d position extents");
        }

        [Test]
        public void ComputeCut_FlippedSprite_SwapsStoredWidthAndHeight()
        {
            var d = Tk2dAnimationData.ParseSpriteDefinitions(DefsYaml)[2];
            var cut = Tk2dAnimationData.ComputeCut(d, 4096, 4096);
            Assert.IsTrue(cut.Rotated);
            Assert.AreEqual(189, cut.StoredW);
            Assert.AreEqual(149, cut.StoredH);
            Assert.AreEqual(149, cut.Width);
            Assert.AreEqual(189, cut.Height);
            Assert.Less(cut.ExtentDeviation, 0.01f);
        }

        [Test]
        public void ComputeCut_SpriteWithoutGeometry_Throws()
        {
            var d = Tk2dAnimationData.ParseSpriteDefinitions(DefsYaml)[0];
            Assert.Throws<System.ArgumentException>(() => Tk2dAnimationData.ComputeCut(d, 4096, 4096));
        }

        [Test]
        public void ExtractPixels_Unrotated_CopiesTheRectangle()
        {
            int[] atlas = AtlasOfIndexes(4, 4);
            var cut = new SpriteCut { AtlasX = 1, AtlasY = 1, StoredW = 2, StoredH = 3, Width = 2, Height = 3, Rotated = false };
            var px = Tk2dAnimationData.ExtractPixels(atlas, 4, cut);
            // rows bottom to top: y = 1,2,3; columns x = 1,2
            CollectionAssert.AreEqual(new[] { 5, 6, 9, 10, 13, 14 }, px);
        }

        [Test]
        public void ExtractPixels_Rotated_SpriteXRunsAlongAtlasV()
        {
            int[] atlas = AtlasOfIndexes(4, 4);
            // Stored 2 wide x 3 tall in the atlas (u = 1..2, v = 1..3); upright sprite is 3 wide x 2 tall.
            var cut = new SpriteCut { AtlasX = 1, AtlasY = 1, StoredW = 2, StoredH = 3, Width = 3, Height = 2, Rotated = true };
            var px = Tk2dAnimationData.ExtractPixels(atlas, 4, cut);
            // sprite (x, y) = atlas (u = 1 + y, v = 1 + x)
            CollectionAssert.AreEqual(new[] { 5, 9, 13, 6, 10, 14 }, px);
        }

        static int[] AtlasOfIndexes(int w, int h)
        {
            var a = new int[w * h];
            for (int i = 0; i < a.Length; i++) a[i] = i;
            return a;
        }

        // ---------- planning ----------

        [Test]
        public void Plan_LoopClip_KeepsFrameOrderFpsLengthAndEvents_WithNoClosingKey()
        {
            var run = Tk2dAnimationData.ParseClips(ClipsYaml)[0];
            var set = Tk2dAnimationData.Plan(run);
            Assert.IsNull(set.LoopPart);
            Assert.IsNull(set.UnsupportedFeature);
            Assert.AreEqual("Run", set.Main.Name);
            Assert.AreEqual(15f, set.Main.FrameRate);
            Assert.IsTrue(set.Main.Loop);
            CollectionAssert.AreEqual(Enumerable.Range(0, 10).ToArray(), set.Main.SourceFrames, "one key per frame, in source order");
            Assert.AreEqual(10f / 15f, set.Main.Length, 1e-6f, "length is frames / fps: no extra closing key");
            Assert.AreEqual(2, set.Main.Events.Count);
            Assert.AreEqual(3f / 15f, set.Main.Events[0].Time, 1e-6f);
            Assert.AreEqual(3, set.Main.Events[0].FrameIndex);
            Assert.AreEqual("Footstep", set.Main.Events[0].Info);
            Assert.AreEqual(8, set.Main.Events[1].FrameIndex);
            Assert.AreEqual(0.5f, set.Main.Events[1].EventFloat);
        }

        [Test]
        public void Plan_Once_DoesNotLoop()
        {
            var set = Tk2dAnimationData.Plan(MakeClip(Tk2dWrapMode.Once, 5, 20f));
            Assert.IsFalse(set.Main.Loop);
            Assert.AreEqual(5, set.Main.SourceFrames.Count);
            Assert.IsNull(set.UnsupportedFeature);
        }

        [Test]
        public void Plan_Single_IsOneFrameNonLooping()
        {
            var set = Tk2dAnimationData.Plan(MakeClip(Tk2dWrapMode.Single, 1, 30f));
            Assert.IsFalse(set.Main.Loop);
            Assert.AreEqual(1, set.Main.SourceFrames.Count);
        }

        [Test]
        public void Plan_PingPong_UnrollsForwardThenBackWithoutRepeatingTheEnds()
        {
            var set = Tk2dAnimationData.Plan(MakeClip(Tk2dWrapMode.PingPong, 4, 10f, 0, 2));
            Assert.IsTrue(set.Main.Loop);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 2, 1 }, set.Main.SourceFrames);
            Assert.AreEqual(6 / 10f, set.Main.Length, 1e-6f);
            // The event frame is shown twice per cycle, so it fires twice; indexes are timeline positions.
            Assert.AreEqual(new[] { 2, 4 }, set.Main.Events.Select(e => e.FrameIndex).ToArray());
        }

        [Test]
        public void Plan_PingPong_TwoFrames_IsJustTheTwoFrames()
        {
            var set = Tk2dAnimationData.Plan(MakeClip(Tk2dWrapMode.PingPong, 2));
            CollectionAssert.AreEqual(new[] { 0, 1 }, set.Main.SourceFrames);
        }

        [Test]
        public void Plan_LoopSection_SplitsIntoIntroAndLoopClips_AndFlagsTheStub()
        {
            var set = Tk2dAnimationData.Plan(MakeClip(Tk2dWrapMode.LoopSection, 7, 12f, 4, 5), "Fall");
            Assert.AreEqual("Fall", set.Main.Name);
            Assert.IsFalse(set.Main.Loop);
            Assert.AreEqual(7, set.Main.SourceFrames.Count);
            Assert.IsNotNull(set.LoopPart);
            Assert.AreEqual("Fall Loop", set.LoopPart.Name);
            Assert.IsTrue(set.LoopPart.Loop);
            CollectionAssert.AreEqual(new[] { 4, 5, 6 }, set.LoopPart.SourceFrames);
            Assert.AreEqual(1, set.LoopPart.Events.Count, "event on frame 5 is the second frame of the loop part");
            Assert.AreEqual(1, set.LoopPart.Events[0].FrameIndex);
            Assert.IsNull(set.UnsupportedFeature);
        }

        [Test]
        public void Plan_LoopSection_LoopStartPastTheEnd_HasNoLoopPart()
        {
            var set = Tk2dAnimationData.Plan(MakeClip(Tk2dWrapMode.LoopSection, 3, 12f, 9));
            Assert.IsNull(set.LoopPart);
            Assert.IsFalse(set.Main.Loop);
        }

        [TestCase(Tk2dWrapMode.RandomFrame)]
        [TestCase(Tk2dWrapMode.RandomLoop)]
        public void Plan_RandomModes_PlayAsPlainLoopAndAreFlaggedAsStubs(Tk2dWrapMode mode)
        {
            var set = Tk2dAnimationData.Plan(MakeClip(mode, 4));
            Assert.IsTrue(set.Main.Loop);
            Assert.IsNotNull(set.UnsupportedFeature);
        }

        [Test]
        public void Plan_OutputNameOverridesTheClipName()
        {
            var set = Tk2dAnimationData.Plan(MakeClip(Tk2dWrapMode.Loop, 2), "Jump");
            Assert.AreEqual("Jump", set.Main.Name);
        }

        [Test]
        public void Plan_BadInput_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => Tk2dAnimationData.Plan(MakeClip(Tk2dWrapMode.Loop, 0)));
            Assert.Throws<System.ArgumentException>(() => Tk2dAnimationData.Plan(MakeClip(Tk2dWrapMode.Loop, 3, 0f)));
            Assert.Throws<System.ArgumentException>(() => Tk2dAnimationData.Plan(MakeClip((Tk2dWrapMode)42, 3)));
            Assert.Throws<System.ArgumentNullException>(() => Tk2dAnimationData.Plan(null));
        }

        [Test]
        public void WrapModeNumbers_MatchTheExport()
        {
            Assert.AreEqual(0, (int)Tk2dWrapMode.Loop);
            Assert.AreEqual(1, (int)Tk2dWrapMode.LoopSection);
            Assert.AreEqual(2, (int)Tk2dWrapMode.Once);
            Assert.AreEqual(3, (int)Tk2dWrapMode.PingPong);
            Assert.AreEqual(4, (int)Tk2dWrapMode.RandomFrame);
            Assert.AreEqual(5, (int)Tk2dWrapMode.RandomLoop);
            Assert.AreEqual(6, (int)Tk2dWrapMode.Single);
        }

        [Test]
        public void FindClip_DuplicateNames_FirstInFileOrderWins()
        {
            var a = new Tk2dClip { Name = "Idle", Fps = 10f };
            var b = new Tk2dClip { Name = "Run" };
            var c = new Tk2dClip { Name = "Idle", Fps = 99f };
            var list = new List<Tk2dClip> { a, b, c };
            Assert.AreSame(a, Tk2dAnimationData.FindClip(list, "Idle"));
            Assert.AreSame(b, Tk2dAnimationData.FindClip(list, "Run"));
        }

        [Test]
        public void FindClip_MissingNameThrows_AndNoNameNeedsASingleClipLibrary()
        {
            var a = new Tk2dClip { Name = "A" };
            Assert.Throws<KeyNotFoundException>(() => Tk2dAnimationData.FindClip(new List<Tk2dClip> { a }, "B"));
            Assert.AreSame(a, Tk2dAnimationData.FindClip(new List<Tk2dClip> { a }, null));
            Assert.Throws<System.ArgumentException>(() => Tk2dAnimationData.FindClip(new List<Tk2dClip> { a, a }, null));
        }

        // ---------- converter path syntax ----------

        [Test]
        public void SplitPath_ReadsOptionalClipName()
        {
            Tk2dAnimationConverter.SplitPath(@"D:\x\Knight.prefab#Run", out var file, out var clip);
            Assert.AreEqual(@"D:\x\Knight.prefab", file);
            Assert.AreEqual("Run", clip);
            Tk2dAnimationConverter.SplitPath(@"D:\x\Knight.prefab", out file, out clip);
            Assert.AreEqual(@"D:\x\Knight.prefab", file);
            Assert.IsNull(clip);
        }

        [Test]
        public void SafeFileName_ReplacesCharactersAFileCannotHave()
        {
            Assert.AreEqual("Wall Cling", Tk2dAnimationConverter.SafeFileName("Wall Cling"));
            Assert.AreEqual("a_b", Tk2dAnimationConverter.SafeFileName("a/b"));
        }

        // ---------- hero mapping ----------

        // Real clip names in the Knight library that the mapping uses (checked against the export).
        static readonly string[] KnightNames =
        {
            "Idle", "Run", "Airborne", "Fall", "Dash", "Wall Cling", "Slash", "UpSlash", "DownSpike", "Recoil",
            "Death", "Turn", "Land", "Wall Slide", "Walljump", "SlashAlt",
            "SlashEffect", "SlashEffectAlt", "UpSlashEffect", "DownSlashEffect", "Wall Slash"
        };

        [Test]
        public void HeroMap_EveryNameHeroCodePlaysIsMapped_AndTheKnightLibraryHasTheSources()
        {
            Assert.IsEmpty(HeroAnimationMap.FindProblems(KnightNames));
        }

        [Test]
        public void HeroMap_CoversHeroClipNames_ExactlyAndOnce()
        {
            var mapped = HeroAnimationMap.Entries.Select(e => e.HeroName).ToList();
            Assert.AreEqual(mapped.Count, mapped.Distinct().Count(), "no hero name mapped twice");
            foreach (var n in HeroClipNames.PlayedByHeroCode) Assert.Contains(n, mapped);
            foreach (var n in HeroClipNames.ImportedForLater) Assert.Contains(n, mapped);
            Assert.AreEqual(HeroClipNames.PlayedByHeroCode.Length + HeroClipNames.ImportedForLater.Length, mapped.Count);
            foreach (var e in HeroAnimationMap.Entries)
                Assert.AreEqual(HeroClipNames.PlayedByHeroCode.Contains(e.HeroName), e.PlayedByHero, e.HeroName);
        }

        [Test]
        public void HeroMap_HasTheNailSwipeEffectRows()
        {
            string[,] rows =
            {
                { "SlashEffect", "SlashEffect" }, { "SlashAltEffect", "SlashEffectAlt" },
                { "SlashUpEffect", "UpSlashEffect" }, { "SlashDownEffect", "DownSlashEffect" }, { "WallSlash", "Wall Slash" }
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                var row = HeroAnimationMap.Entries.Single(e => e.HeroName == rows[i, 0]);
                Assert.AreEqual(rows[i, 1], row.SourceClip, rows[i, 0]);
            }
            // The alternate swing and the wall slash are played by hero code now.
            Assert.IsTrue(HeroAnimationMap.Entries.Single(e => e.HeroName == "SlashAlt").PlayedByHero);
            Assert.IsTrue(HeroAnimationMap.Entries.Single(e => e.HeroName == "SlashAltEffect").PlayedByHero);
            Assert.IsTrue(HeroAnimationMap.Entries.Single(e => e.HeroName == "SlashEffect").PlayedByHero);
            Assert.IsTrue(HeroAnimationMap.Entries.Single(e => e.HeroName == "SlashUpEffect").PlayedByHero);
            Assert.IsTrue(HeroAnimationMap.Entries.Single(e => e.HeroName == "SlashDownEffect").PlayedByHero);
            Assert.IsTrue(HeroAnimationMap.Entries.Single(e => e.HeroName == "WallSlash").PlayedByHero);
        }

        [Test]
        public void HeroMap_FlagsAnEffectClipMissingFromTheLibrary()
        {
            var problems = HeroAnimationMap.FindProblems(KnightNames.Where(n => n != "UpSlashEffect"));
            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("SlashUpEffect", problems[0]);
        }

        [Test]
        public void HeroMap_FlagsASourceClipMissingFromTheLibrary()
        {
            var problems = HeroAnimationMap.FindProblems(KnightNames.Where(n => n != "Recoil"));
            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("Hurt", problems[0]);
            StringAssert.Contains("Recoil", problems[0]);
        }
    }
}
