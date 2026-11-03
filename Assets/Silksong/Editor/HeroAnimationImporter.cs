using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hollowknight.Silksong.Editor
{
    // Which Silksong (Knight library) clip plays for each animation name the hero code asks for. The reasoning
    // for every row is in docs/study/07-hero-animation-mapping.md.
    public static class HeroAnimationMap
    {
        public struct Entry
        {
            public string HeroName;     // the name hero code passes to ISpriteAnimator.Play
            public string SourceClip;   // the clip's name in the Knight library
            public bool PlayedByHero;   // true: hero code plays it now; false: imported for a later task
        }

        public static readonly Entry[] Entries =
        {
            new Entry { HeroName = "Idle",      SourceClip = "Idle",       PlayedByHero = true },
            new Entry { HeroName = "Run",       SourceClip = "Run",        PlayedByHero = true },
            new Entry { HeroName = "Jump",      SourceClip = "Airborne",   PlayedByHero = true },
            new Entry { HeroName = "Fall",      SourceClip = "Fall",       PlayedByHero = true },
            new Entry { HeroName = "Dash",      SourceClip = "Dash",       PlayedByHero = true },
            new Entry { HeroName = "WallCling", SourceClip = "Wall Cling", PlayedByHero = true },
            new Entry { HeroName = "Slash",     SourceClip = "Slash",      PlayedByHero = true },
            new Entry { HeroName = "SlashUp",   SourceClip = "UpSlash",    PlayedByHero = true },
            new Entry { HeroName = "SlashDown", SourceClip = "DownSpike",  PlayedByHero = true },
            new Entry { HeroName = "Hurt",      SourceClip = "Recoil",     PlayedByHero = true },
            new Entry { HeroName = "Death",     SourceClip = "Death",      PlayedByHero = true },
            new Entry { HeroName = "Turn",      SourceClip = "Turn",       PlayedByHero = true },
            new Entry { HeroName = "Land",      SourceClip = "Land",       PlayedByHero = false },
            new Entry { HeroName = "WallSlide", SourceClip = "Wall Slide", PlayedByHero = true },
            new Entry { HeroName = "WallJump",  SourceClip = "Walljump",   PlayedByHero = false },
            new Entry { HeroName = "SlashAlt",  SourceClip = "SlashAlt",   PlayedByHero = false },
        };

        // Rows whose source clip is not in the library (or whose hero name is not listed at all). Empty = good.
        public static List<string> FindProblems(IEnumerable<string> libraryClipNames)
        {
            var have = new HashSet<string>(libraryClipNames);
            var problems = new List<string>();
            foreach (var e in Entries)
                if (!have.Contains(e.SourceClip))
                    problems.Add(e.HeroName + " -> '" + e.SourceClip + "' is not in the library" + (e.PlayedByHero ? " (hero code plays it)" : ""));
            var mapped = new HashSet<string>();
            foreach (var e in Entries) mapped.Add(e.HeroName);
            foreach (var n in HeroClipNames.PlayedByHeroCode)
                if (!mapped.Contains(n)) problems.Add(n + " is played by hero code but has no mapping row");
            foreach (var n in HeroClipNames.ImportedForLater)
                if (!mapped.Contains(n)) problems.Add(n + " is listed for import but has no mapping row");
            return problems;
        }
    }

    // Brings the hero's clips in from the export. Run by menu, or from batch mode:
    //   Unity -batchmode -projectPath <project> -executeMethod Hollowknight.Silksong.Editor.HeroAnimationImporter.ImportBatch -quit
    // Output goes under HeroClipNames.ImportFolder (inside the gitignored Imported/ folder).
    public static class HeroAnimationImporter
    {
        public const string DefaultLibraryPath = @"D:\Asset HollowKnight Bundles\ExportedProject\Assets\Animations\Knight.prefab";
        public const string LibraryEnvVar = "SILKSONG_KNIGHT_ANIMATION_LIBRARY";

        [MenuItem("Silksong/Import Hero Animations")]
        public static void ImportFromMenu()
        {
            try
            {
                var report = Import(ResolveLibraryPath());
                Debug.Log(report);
            }
            catch (Exception e)
            {
                Debug.LogError("[HeroAnimationImporter] FAILED: " + e);
            }
        }

        public static void ImportBatch()
        {
            try
            {
                Debug.Log(Import(ResolveLibraryPath()));
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[HeroAnimationImporter] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        static string ResolveLibraryPath()
        {
            string fromEnv = Environment.GetEnvironmentVariable(LibraryEnvVar);
            return string.IsNullOrEmpty(fromEnv) ? DefaultLibraryPath : fromEnv;
        }

        // Converts every row of HeroAnimationMap into outputFolder. Throws if a row's clip is missing.
        public static string Import(string libraryFile, string outputFolder = HeroClipNames.ImportFolder)
        {
            var library = Tk2dAnimationConverter.LoadLibrary(libraryFile);
            var names = new List<string>();
            foreach (var c in library.Clips) names.Add(c.Name);
            var problems = HeroAnimationMap.FindProblems(names);
            if (problems.Count > 0) throw new InvalidOperationException("Hero animation mapping problems:\n  " + string.Join("\n  ", problems));

            var sb = new StringBuilder("[HeroAnimationImporter] Imported from " + libraryFile + "\n");
            foreach (var e in HeroAnimationMap.Entries)
            {
                var clip = Tk2dAnimationConverter.ConvertFrom(library, libraryFile, e.SourceClip, outputFolder, e.HeroName);
                sb.AppendLine("  " + e.HeroName + " <- " + e.SourceClip + ": " + clip.length.ToString("0.###") + " s, "
                              + clip.frameRate + " fps, loop " + clip.isLooping + ", " + clip.events.Length + " event(s)");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return sb.ToString();
        }
    }
}
