using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hollowknight.Silksong.Editor
{
    // Builds Assets/Scenes/Rooms/StartScreen.unity: an empty scene with one StartScreen object. The screen is wired
    // to the same assets TransitionRoomsBuilder gives RoomWorldStarter (HeroConfig, input actions, hero sprite and
    // clips; LoadAssets is reused). The scene is generated and gitignored (the Assets/Scenes/Rooms/ folder is), and is
    // NOT added to Build Settings. Rooms are found by FallbackRoomSceneSource (Build Settings first, then the path
    // Assets/Scenes/Rooms/<name>.unity), so build the transition rooms too (Silksong > Build Transition Rooms).
    public static class StartScreenBuilder
    {
        public const string SceneName = "StartScreen";
        public static readonly string ScenePath = RoomWorldStarter.RoomsFolder + "/" + SceneName + ".unity";

        [MenuItem("Silksong/Build Start Screen")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                string path = Build(ScenePath, true);
                EditorSceneManager.OpenScene(path);
                Debug.Log("[StartScreenBuilder] Built " + path + ". Opened it; press Play.");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[StartScreenBuilder] " + e.Message);
            }
        }

        // -executeMethod Hollowknight.Silksong.Editor.StartScreenBuilder.BuildBatch
        public static void BuildBatch()
        {
            try
            {
                string path = Build(ScenePath, true);
                Debug.Log("[StartScreenBuilder] Built " + path);
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[StartScreenBuilder] FAILED: " + e.Message);
                EditorApplication.Exit(1);
            }
        }

        // Builds the scene at the path and returns it. Replaces whatever scenes are open without saving them.
        public static string Build(string path, bool discardOpenScenes)
        {
            if (!discardOpenScenes)
                throw new System.InvalidOperationException("Build replaces all open scenes without saving; pass discardOpenScenes: true to confirm.");

            // Fail before touching any scene, with the instruction the owner needs.
            TransitionRoomsBuilder.LoadAssets();
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                // Loaded after the scene switch: switching can unload assets fetched earlier.
                var assets = TransitionRoomsBuilder.LoadAssets();
                var go = new GameObject("StartScreen");
                go.AddComponent<StartScreen>().Configure(assets.Config, assets.Actions, assets.HeroSprite, assets.HeroClips);
                if (!EditorSceneManager.SaveScene(scene, path))
                    throw new System.InvalidOperationException("Could not save " + path);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            AssetDatabase.Refresh();
            return path;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
