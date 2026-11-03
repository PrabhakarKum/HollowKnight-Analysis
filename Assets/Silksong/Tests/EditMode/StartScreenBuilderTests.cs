using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Hollowknight.Silksong.Editor;

namespace Hollowknight.Silksong.Tests
{
    // Task 6: the generated start screen scene. Built into a temp folder so the real Assets/Scenes/Rooms is untouched.
    public class StartScreenBuilderTests
    {
        const string TempFolder = "Assets/Silksong/Tests/TempStart";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempFolder);
            if (Directory.Exists(TempFolder)) Directory.Delete(TempFolder, true);
            if (File.Exists(TempFolder + ".meta")) File.Delete(TempFolder + ".meta");
            AssetDatabase.Refresh();
        }

        [Test]
        public void ScenePath_IsInTheGeneratedRoomsFolder()
        {
            Assert.AreEqual("Assets/Scenes/Rooms/StartScreen.unity", StartScreenBuilder.ScenePath);
            string ignore = File.ReadAllText(Path.Combine(Application.dataPath, "..", ".gitignore"));
            StringAssert.Contains("/Assets/Scenes/Rooms/", ignore);
        }

        [Test]
        public void Build_WithoutDiscardFlag_Throws_AndWritesNothing()
        {
            Assert.Throws<System.InvalidOperationException>(() => StartScreenBuilder.Build(TempFolder + "/StartScreen.unity", false));
            Assert.IsFalse(Directory.Exists(TempFolder));
        }

        [Test]
        public void Build_WritesASceneWithOneWiredStartScreen_AndLeavesBuildSettingsAlone()
        {
            var before = EditorBuildSettings.scenes.Select(s => s.path + ":" + s.enabled).ToArray();
            string path = StartScreenBuilder.Build(TempFolder + "/StartScreen.unity", true);
            Assert.IsTrue(File.Exists(path), path);

            try
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Assert.AreEqual("StartScreen", scene.name);
                StartScreen screen = null;
                int count = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var s in root.GetComponentsInChildren<StartScreen>(true)) { screen = s; count++; }
                Assert.AreEqual(1, count);
                Assert.IsNotNull(screen.Config, "HeroConfig");
                Assert.IsNotNull(screen.ActionsAsset, "input actions");
                Assert.IsNotNull(screen.HeroSprite, "hero sprite");
                Assert.IsNotNull(screen.HeroClips, "hero clips (may be empty without the import)");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            var after = EditorBuildSettings.scenes.Select(s => s.path + ":" + s.enabled).ToArray();
            CollectionAssert.AreEqual(before, after);
        }
    }
}
