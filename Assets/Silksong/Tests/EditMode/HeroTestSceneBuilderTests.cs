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
    public class HeroTestSceneBuilderTests
    {
        // Built into a throwaway folder so a test run never overwrites the owner's real scratch scene.
        const string TempFolder = "Assets/Silksong/Tests/Temp";
        const string TempScene = TempFolder + "/HeroTestSceneTemp.unity";
        const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempFolder);
            if (Directory.Exists(TempFolder)) Directory.Delete(TempFolder, true);
            if (File.Exists(TempFolder + ".meta")) File.Delete(TempFolder + ".meta");
            AssetDatabase.Refresh();
        }

        static Scene OpenAdditive(string path) => EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        static T[] All<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();

        static int CountObjects(Scene scene) =>
            scene.GetRootGameObjects().Sum(g => g.GetComponentsInChildren<Transform>(true).Length);

        [Test]
        public void Build_CreatesSceneWithHeroAndConfig()
        {
            HeroTestSceneBuilder.Build(TempScene);
            Assert.IsTrue(File.Exists(TempScene), "scene file written");

            var scene = OpenAdditive(TempScene);
            try
            {
                var heroes = All<HeroController>(scene);
                Assert.AreEqual(1, heroes.Length, "exactly one HeroController");
                Assert.IsNotNull(heroes[0].GetComponent<HeroActions>(), "HeroActions on the hero");
                Assert.IsNotNull(heroes[0].GetComponent<Rigidbody2D>());
                Assert.IsNotNull(heroes[0].GetComponent<Collider2D>());

                var actionsAsset = new SerializedObject(heroes[0].GetComponent<HeroActions>()).FindProperty("actionsAsset");
                Assert.IsNotNull(actionsAsset.objectReferenceValue, "HeroActions has the input actions asset");

                var cams = All<HeroTestCameraFollow>(scene);
                Assert.AreEqual(1, cams.Length, "camera with HeroTestCameraFollow");
                Assert.IsNotNull(cams[0].GetComponent<Camera>());
                Assert.AreSame(heroes[0].transform, cams[0].Target, "camera follows the hero");

                var huds = All<HeroDebugHud>(scene);
                Assert.AreEqual(1, huds.Length, "one HeroDebugHud");
                var config = AssetDatabase.LoadAssetAtPath<HeroConfig>(HeroTestSceneBuilder.ConfigPath);
                Assert.IsNotNull(config, "HeroConfig asset exists");
                Assert.AreSame(config, huds[0].Config, "hero is wired to the HeroConfig asset");
                Assert.AreSame(heroes[0], huds[0].Hero);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void Build_IsRepeatable()
        {
            HeroTestSceneBuilder.Build(TempScene);
            int configsBefore = AssetDatabase.FindAssets("t:HeroConfig").Length;
            var first = OpenAdditive(TempScene);
            int objectsFirst = CountObjects(first);
            EditorSceneManager.CloseScene(first, true);

            HeroTestSceneBuilder.Build(TempScene);
            var second = OpenAdditive(TempScene);
            try
            {
                Assert.AreEqual(objectsFirst, CountObjects(second), "same object count after a second build");
                Assert.AreEqual(1, All<HeroController>(second).Length);
            }
            finally { EditorSceneManager.CloseScene(second, true); }
            Assert.AreEqual(configsBefore, AssetDatabase.FindAssets("t:HeroConfig").Length, "config asset not duplicated");
        }

        [Test]
        public void Build_ContainsSlopesAtThirtyAndFortyFiveDegrees()
        {
            HeroTestSceneBuilder.Build(TempScene);
            var scene = OpenAdditive(TempScene);
            try
            {
                var angles = All<Collider2D>(scene)
                    .Select(c => Mathf.Abs(Mathf.DeltaAngle(0f, c.transform.eulerAngles.z)))
                    .ToArray();
                Assert.IsTrue(angles.Any(a => Mathf.Abs(a - 30f) < 0.5f), "a collider rotated about 30 degrees");
                Assert.IsTrue(angles.Any(a => Mathf.Abs(a - 45f) < 0.5f), "a collider rotated about 45 degrees");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void Build_DoesNotTouchSampleScene()
        {
            Assert.IsTrue(File.Exists(SampleScenePath), "precondition: SampleScene exists");
            var before = File.ReadAllBytes(SampleScenePath);
            var timeBefore = File.GetLastWriteTimeUtc(SampleScenePath);

            HeroTestSceneBuilder.Build(TempScene);

            CollectionAssert.AreEqual(before, File.ReadAllBytes(SampleScenePath), "SampleScene content unchanged");
            Assert.AreEqual(timeBefore, File.GetLastWriteTimeUtc(SampleScenePath), "SampleScene not rewritten");
        }

        [Test]
        public void Build_SucceedsWhenLabelFontIsUnavailable()
        {
            var original = HeroTestSceneBuilder.LabelFontProvider;
            HeroTestSceneBuilder.LabelFontProvider = () => null;
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("No label font"));
                HeroTestSceneBuilder.Build(TempScene);
            }
            finally { HeroTestSceneBuilder.LabelFontProvider = original; }

            var scene = OpenAdditive(TempScene);
            try
            {
                Assert.AreEqual(1, All<HeroController>(scene).Length, "scene still has the hero");
                Assert.AreEqual(0, All<TextMesh>(scene).Length, "no labels without a font");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void Build_IsRepeatableWhileTheSceneIsOpen()
        {
            HeroTestSceneBuilder.Build(TempScene);
            var open = OpenAdditive(TempScene);
            int objects = CountObjects(open);

            // The menu leaves the built scene open, so a rebuild must cope with it being loaded.
            HeroTestSceneBuilder.Build(TempScene);

            var again = OpenAdditive(TempScene);
            try
            {
                Assert.AreEqual(objects, CountObjects(again));
                Assert.AreEqual(1, All<HeroController>(again).Length);
                int loadedCopies = 0;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).path == TempScene) loadedCopies++;
                Assert.AreEqual(1, loadedCopies);
            }
            finally { EditorSceneManager.CloseScene(again, true); }
        }

        [Test]
        public void HeroDebugHud_Wire_SetsConfigAndInput()
        {
            var heroGo = new GameObject("Hero", typeof(Rigidbody2D), typeof(CapsuleCollider2D));
            var hudGo = new GameObject("Hud");
            var config = ScriptableObject.CreateInstance<HeroConfig>();
            try
            {
                var hero = heroGo.AddComponent<HeroController>();
                var actions = heroGo.AddComponent<HeroActions>();
                var hud = hudGo.AddComponent<HeroDebugHud>();
                var so = new SerializedObject(hud);
                so.FindProperty("hero").objectReferenceValue = hero;
                so.FindProperty("actions").objectReferenceValue = actions;
                so.FindProperty("config").objectReferenceValue = config;
                so.ApplyModifiedPropertiesWithoutUndo();

                hud.Wire();

                Assert.AreSame(config, hero.Config);
                Assert.AreSame(actions, hero.Input);
            }
            finally
            {
                Object.DestroyImmediate(heroGo);
                Object.DestroyImmediate(hudGo);
                Object.DestroyImmediate(config);
            }
        }
    }
}
