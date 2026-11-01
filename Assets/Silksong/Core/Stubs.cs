using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
#endif

namespace Hollowknight.Silksong
{
    // Marks features that exist in the original but are not built yet. Logs once per feature per
    // session so a stub in an Update loop cannot flood the console.
    public static class Stubs
    {
        static readonly HashSet<string> Seen = new HashSet<string>();

        public static IReadOnlyCollection<string> Reported => Seen;

        // Tests point this at a temp file; null means the real docs/study/STUBS.md.
        public static string StubsDocPath { get; set; }

        public static void NotImplemented(string feature)
        {
            if (string.IsNullOrEmpty(feature) || !Seen.Add(feature)) return;
            Debug.LogWarning($"[Stub] Not implemented yet: {feature}");
#if UNITY_EDITOR
            AppendToDoc(feature);
#endif
        }

        public static void ResetForTests()
        {
            Seen.Clear();
        }

#if UNITY_EDITOR
        // Keeps the stub list in the repo current without anyone editing it by hand.
        static void AppendToDoc(string feature)
        {
            try
            {
                string path = StubsDocPath ?? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "study", "STUBS.md"));
                string line = "- " + feature;
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                if (!File.Exists(path))
                {
                    File.WriteAllText(path, "# Stubs\n\nFeatures that call `Stubs.NotImplemented`, added automatically in the Editor.\n\n" + line + "\n");
                    return;
                }

                foreach (var existing in File.ReadAllLines(path))
                    if (existing.Trim() == line) return;

                File.AppendAllText(path, line + "\n");
            }
            catch (System.Exception e)
            {
                // A doc write failure must never break gameplay code that merely hit a stub.
                Debug.LogWarning($"[Stub] Could not update STUBS.md: {e.Message}");
            }
        }
#endif
    }
}
