#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Hollowknight.Enemies;

namespace Hollowknight.EditorTools
{
    /// <summary>Menu items for the enemy test arena. The building itself is in Hollowknight.Enemies.EnemyBuilder (shared with the playtest bot).</summary>
    public static class EnemyArenaBuilder
    {
        [MenuItem("Hollowknight/Build Enemy Test Arena")]
        public static void BuildArena()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop play mode first."); return; }
            if (!AssetDatabase.IsValidFolder("Assets/EnemyArt/Gunner")) { Debug.LogError("Run Hollowknight > Generate Enemy Variants first."); return; }
            if (Object.FindFirstObjectByType<Hollowknight.Player.PlayerController2D>() == null)
            {
                Debug.LogError("The open scene has no hero (PlayerController2D). Open Assets/Scenes/SampleScene.unity first - the arena must not be built into another scene.");
                return;
            }
            var old = GameObject.Find(EnemyBuilder.RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject(EnemyBuilder.RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build enemy test arena");

            // feet positions: ground top is y -0.5, "Platform High" top is y 3.75
            EnemyBuilder.BuildEnemy(EnemyKind.Brawler, new Vector2(-12f, -0.5f), root.transform);
            EnemyBuilder.BuildEnemy(EnemyKind.BladeThrower, new Vector2(-8.5f, -0.5f), root.transform);
            EnemyBuilder.BuildEnemy(EnemyKind.Archer, new Vector2(-6f, 3.75f), root.transform);
            EnemyBuilder.BuildEnemy(EnemyKind.Gunner, new Vector2(7.5f, -0.5f), root.transform);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("Enemy test arena built: Brawler (west end), Blade Thrower, Archer on the high platform, Gunner (east). They notice you within ~8 units, then hunt you. Hit them with the nail.");
        }

        [MenuItem("Hollowknight/Reset Enemy Configs To Defaults")]
        public static void ResetConfigs()
        {
            foreach (EnemyKind k in System.Enum.GetValues(typeof(EnemyKind))) EnemyBuilder.GetConfig(k, reset: true);
            AssetDatabase.SaveAssets();
            Debug.Log("Enemy configs reset to their defaults in " + EnemyBuilder.ConfigDir);
        }
    }
}
#endif
