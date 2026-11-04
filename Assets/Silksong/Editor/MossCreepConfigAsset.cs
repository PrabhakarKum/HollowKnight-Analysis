using UnityEditor;
using UnityEngine;

namespace Hollowknight.Silksong.Editor
{
    // The Moss Creep's config asset lives in its own folder, NOT the owner's Assets/ScriptableObjects/Enemies (the old
    // prototype's). It is generated and gitignored like the rooms: the field defaults of SilksongEnemyConfig are the
    // study numbers, so a fresh instance already holds them. EnsureAsset creates the asset when it is missing and
    // never overwrites an existing one (the owner may tune it). The room builder calls it, so building the rooms is
    // enough.
    public static class MossCreepConfigAsset
    {
        public const string Folder = "Assets/ScriptableObjects/SilksongEnemies";
        public const string Path = Folder + "/MossCreep.asset";

        [MenuItem("Silksong/Build Moss Creep Config")]
        public static void BuildFromMenu()
        {
            var config = EnsureAsset();
            Selection.activeObject = config;
            Debug.Log("[MossCreepConfigAsset] " + Path + " is ready.");
        }

        public static SilksongEnemyConfig EnsureAsset() => EnsureAsset(Path);

        // Same, for any path (the tests use a temp folder).
        public static SilksongEnemyConfig EnsureAsset(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<SilksongEnemyConfig>(path);
            if (existing != null) return existing;

            EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            var created = ScriptableObject.CreateInstance<SilksongEnemyConfig>();
            created.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<SilksongEnemyConfig>(path);
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
