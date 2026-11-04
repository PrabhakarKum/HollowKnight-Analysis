using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hollowknight.Silksong.Tests
{
    // Regression: start a game, quit to the menu, start another. The REAL RoomWorldFactory.Create (no override)
    // used to throw 'Scene with name "RoomWorldHost" already exists' the second time, because the empty host scene
    // of the first session was never unloaded. The rooms here are built from code (no scene assets).
    public class RealFactoryTests
    {
        const string AssetPath = "Assets/Silksong/Input/HeroInputActions.inputactions";
        const string EmptyName = "RealFactoryEmpty";

        string tempRoot, savedRoot, stubsPath, roomName;
        Scene empty;
        int baseline;
        HeroConfig config;
        InputActionAsset actionsAsset;

        [SetUp]
        public void SetUp()
        {
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            PersistentBool.ResetStatics();
            stubsPath = Path.Combine(Path.GetTempPath(), "realfactory-stubs-" + Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
            savedRoot = SaveSystem.Root;
            tempRoot = Path.Combine(Path.GetTempPath(), "realfactory-saves-" + Guid.NewGuid().ToString("N"));
            SaveSystem.Root = tempRoot;
            SaveSlots.ResetForTests();

            // Reuse the empty scene a previous test left: CreateScene throws if the name is taken.
            empty = SceneManager.GetSceneByName(EmptyName);
            if (!empty.IsValid() || !empty.isLoaded) empty = SceneManager.CreateScene(EmptyName);
            SceneManager.SetActiveScene(empty);
            baseline = SceneManager.sceneCount;
            roomName = "RfRoom_" + Guid.NewGuid().ToString("N").Substring(0, 8);
#if UNITY_EDITOR
            config = HeroConfigConsistency.LoadProjectAsset();
            actionsAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
#endif
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (RoomWorldBootstrap.Instance != null) yield return GameSession.Quit(false);
            var bootstrap = RoomWorldBootstrap.Instance;
            if (bootstrap != null) UnityEngine.Object.Destroy(bootstrap.gameObject);
            yield return null;
            if (!empty.IsValid() || !empty.isLoaded) empty = SceneManager.CreateScene(EmptyName);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s == empty || !s.isLoaded || s.name.StartsWith("InitTestScene")) continue;   // the test runner's own scene: unloading it stalls the teardown
                yield return SceneManager.UnloadSceneAsync(s);
            }
            foreach (var b in UnityEngine.Object.FindObjectsByType<RoomWorldBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b != null) UnityEngine.Object.Destroy(b.gameObject);
            foreach (var h in UnityEngine.Object.FindObjectsByType<HeroController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (h != null) UnityEngine.Object.Destroy(h.gameObject);
            foreach (var c in UnityEngine.Object.FindObjectsByType<GameCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c != null) UnityEngine.Object.Destroy(c.gameObject);
            yield return null;
            yield return null;
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            PersistentBool.ResetStatics();
            SaveSlots.ResetForTests();
            SaveSystem.Root = savedRoot;
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            if (File.Exists(stubsPath)) File.Delete(stubsPath);
            Stubs.StubsDocPath = null;
            Stubs.ResetForTests();
        }

        // A room scene with a floor and RoomInfo + bounds, created the way a loader would.
        Scene BuildRoom()
        {
            var scene = SceneManager.CreateScene(roomName);
            var floor = new GameObject("Floor");
            floor.AddComponent<BoxCollider2D>().size = new Vector2(60f, 1f);
            floor.transform.position = new Vector3(30f, -0.5f, 0f);
            SceneManager.MoveGameObjectToScene(floor, scene);
            var boundsGo = new GameObject("Bounds");
            var bounds = boundsGo.AddComponent<RoomBounds>();
            bounds.Configure(new Rect(0f, 0f, 60f, 40f));
            SceneManager.MoveGameObjectToScene(boundsGo, scene);
            var infoGo = new GameObject("Info");
            infoGo.AddComponent<RoomInfo>().Configure(roomName, 1, "Zone 1", bounds, "ZONE_1");
            SceneManager.MoveGameObjectToScene(infoGo, scene);
            return scene;
        }

        // One game session through the real factory: world, adopted room, checks, then Quit.
        IEnumerator Session()
        {
            Assert.IsNotNull(config, "HeroConfig asset missing");
            Assert.IsNotNull(actionsAsset, "Input actions asset missing");

            RoomWorldBootstrap world = null;
            Assert.DoesNotThrow(() => world = RoomWorldFactory.Create(config, actionsAsset, null, null, false, null));
            Assert.IsNotNull(world);
            var room = BuildRoom();
            world.PlaceHero(new Vector2(10f, 1f));
            world.AdoptRoom(room);
            yield return null;

            Assert.AreSame(world, RoomWorldBootstrap.Instance);
            Assert.AreEqual(1, Count<HeroController>(), "heroes");
            Assert.AreEqual(1, Count<GameCamera>(), "cameras");
            Assert.AreEqual(1, Count<RoomTransitionManager>(), "managers");
            Assert.AreEqual(1, Count<RoomWorldBootstrap>(), "bootstraps");
            // The active scene is the room, not the host.
            Assert.AreEqual(roomName, SceneManager.GetActiveScene().name);

            yield return GameSession.Quit(false);
            Assert.IsNull(RoomWorldBootstrap.Instance);
            Assert.AreEqual(0, Count<HeroController>(), "heroes after quit");
            Assert.AreEqual(0, Count<RoomWorldBootstrap>(), "bootstraps after quit");
        }

        static int Count<T>() where T : UnityEngine.Object =>
            UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        [UnityTest]
        public IEnumerator CreateQuitCreate_TwiceWithTheRealFactory_DoesNotThrow()
        {
            yield return Session();
            yield return Session();
        }

        [UnityTest]
        public IEnumerator CreateQuit_ThreeTimes_ReturnsToTheBaselineSceneCount()
        {
            yield return Session();
            yield return Session();
            yield return Session();
            yield return null;
            Assert.AreEqual(baseline, SceneManager.sceneCount, "Quit must unload the room and the leftover host scene");
            Assert.IsFalse(SceneManager.GetSceneByName(RoomWorldFactory.HostSceneName).isLoaded, "host scene still loaded");
        }

        [UnityTest]
        public IEnumerator Create_WithAHostSceneAlreadyLoaded_ReusesIt()
        {
            var host = SceneManager.CreateScene(RoomWorldFactory.HostSceneName);
            int before = SceneManager.sceneCount;
            RoomWorldBootstrap world = null;
            Assert.DoesNotThrow(() => world = RoomWorldFactory.Create(config, actionsAsset, null, null, false, null));
            Assert.IsNotNull(world);
            Assert.AreEqual(before, SceneManager.sceneCount, "no second host scene");
            Assert.AreEqual(empty, SceneManager.GetActiveScene(), "the previous active scene is restored");
            yield return GameSession.Quit(false);
            Assert.IsFalse(host.isLoaded);
        }
    }
}
