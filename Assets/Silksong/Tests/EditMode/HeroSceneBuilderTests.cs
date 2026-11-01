using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hollowknight.Silksong.Editor;

namespace Hollowknight.Silksong.Tests
{
    // Task 11b: the Silksong hero room builder. Needs the imported hero animations for the build tests; without them
    // the one test that applies checks the build refuses with the instruction to import first.
    public class HeroSceneBuilderTests
    {
        const string TempFolder = "Assets/Silksong/Tests/Temp";
        const string TempScene = TempFolder + "/HeroRoomTemp.unity";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempFolder);
            if (Directory.Exists(TempFolder)) Directory.Delete(TempFolder, true);
            if (File.Exists(TempFolder + ".meta")) File.Delete(TempFolder + ".meta");
            AssetDatabase.Refresh();
        }

        static bool ArtImported => HeroSceneBuilder.MissingClips().Count == 0;

        [Test]
        public void ScenePath_MatchesPlayModeTests_AndIsNotThePlaceholderScene()
        {
            // The PlayMode assembly cannot see the Editor one, so its copy of the path is checked in the source text.
            StringAssert.Contains("\"" + HeroSceneBuilder.ScenePath + "\"", File.ReadAllText("Assets/Silksong/Tests/PlayMode/RoomSceneTests.cs"));
            Assert.AreNotEqual(HeroTestSceneBuilder.DefaultScenePath, HeroSceneBuilder.ScenePath, "must not overwrite the committed placeholder scene");
        }

        [Test]
        public void Build_WithoutImportedArt_FailsWithImportInstruction_AndWritesNothing()
        {
            if (ArtImported) Assert.Ignore("Art is imported; the failure path does not apply.");
            var ex = Assert.Throws<System.InvalidOperationException>(() => HeroSceneBuilder.Build(TempScene, true));
            StringAssert.Contains("Silksong/Import Hero Animations", ex.Message);
            Assert.IsFalse(File.Exists(TempScene));
        }

        [Test]
        public void Build_RefusesWithoutDiscardConfirmation()
        {
            Assert.Throws<System.InvalidOperationException>(() => HeroSceneBuilder.Build(TempScene, false));
        }

        [Test]
        public void Build_ProducesSceneWhereHeroIsConfiguredOnlyByProjectConfig()
        {
            if (!ArtImported) Assert.Ignore("Run Silksong/Import Hero Animations first.");
            HeroSceneBuilder.Build(TempScene, true);
            var scene = EditorSceneManager.OpenScene(TempScene, OpenSceneMode.Single);
            try
            {
                var all = scene.GetRootGameObjects();
                T[] Find<T>() where T : Component => all.SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();

                var hero = Find<HeroController>().Single();
                var box = hero.GetComponent<BoxCollider2D>();
                Assert.AreEqual(SilksongUnits.HeroColliderSize, box.size);
                Assert.AreEqual(SilksongUnits.HeroColliderOffset, box.offset);
                Assert.IsNull(hero.GetComponent<CapsuleCollider2D>());
                Assert.IsNotNull(hero.GetComponent<HeroHealth>());

                var asset = AssetDatabase.LoadAssetAtPath<HeroConfig>(HeroTestSceneBuilder.ConfigPath);
                var boot = Find<HeroSceneBootstrap>().Single();
                Assert.AreSame(asset, new SerializedObject(boot).FindProperty("config").objectReferenceValue, "bootstrap holds the project asset");
                // No other component serializes a HeroConfig.
                foreach (var mb in Find<MonoBehaviour>())
                {
                    var it = new SerializedObject(mb).GetIterator();
                    while (it.NextVisible(true))
                        if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue is HeroConfig c)
                            Assert.AreSame(asset, c, mb.GetType().Name);
                }

                Assert.AreEqual(1, Find<RespawnMarker>().Length);
                Assert.AreEqual(1, Find<GameCamera>().Length);
                Assert.AreEqual(1, Find<RoomBounds>().Length);
                Assert.GreaterOrEqual(Find<TrainingDummy>().Length, 1);
                Assert.IsNotNull(Find<UnitySpriteAnimator>().Single());
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }
    }
}
