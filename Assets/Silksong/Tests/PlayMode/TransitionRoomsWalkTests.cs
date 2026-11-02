using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Hollowknight.Silksong.Tests
{
    // Task 7: the generated grey-box rooms, played for real. Tut_01 is loaded as the first scene; its RoomWorldStarter
    // creates the world, then the hero is sent through every gate pair of the study table (Tut_01 <-> Tut_01b twice,
    // Tut_01 <-> Tut_02 twice) with the real scene files loaded by path (the engine-deferred load path of the loader).
    //
    // The tests never build the rooms (building needs edit mode and hangs a batch run that then enters play mode).
    // Build them first with the menu or -executeMethod Hollowknight.Silksong.Editor.TransitionRoomsBuilder.BuildBatch;
    // a missing or outdated scene fails at once with that instruction.
    public class TransitionRoomsWalkTests
    {
        // Same folder as TransitionRoomsBuilder.Folder (an EditMode test checks they agree).
        const string RoomsFolder = "Assets/Scenes/Rooms";
        static readonly string[] Rooms = { "Tut_01", "Tut_01b", "Tut_02" };

        const string BuildHint = "Run the menu Silksong/Build Transition Rooms (after Silksong/Import Hero Animations for the hero art), or -executeMethod Hollowknight.Silksong.Editor.TransitionRoomsBuilder.BuildBatch before -runTests.";

        // Each step: the room the hero is in, the gate it leaves by, and the room and gate it must arrive at.
        static readonly string[][] Steps =
        {
            new[] { "Tut_01", "right1", "Tut_01b", "left1" },
            new[] { "Tut_01b", "left1", "Tut_01", "right1" },
            new[] { "Tut_01", "right2", "Tut_01b", "left2" },
            new[] { "Tut_01b", "left2", "Tut_01", "right2" },
            new[] { "Tut_01", "left2", "Tut_02", "right1" },
            new[] { "Tut_02", "right1", "Tut_01", "left2" },
            new[] { "Tut_01", "left3", "Tut_02", "right2" },
            new[] { "Tut_02", "right2", "Tut_01", "left3" },
        };

        static string PathOf(string room) => RoomsFolder + "/" + room + ".unity";

        // Fails fast when a room is absent or older than the code that builds it.
        static void RequireScenes()
        {
#if UNITY_EDITOR
            foreach (var room in Rooms)
            {
                Assert.IsTrue(File.Exists(PathOf(room)), PathOf(room) + " does not exist. " + BuildHint);
                var sceneTime = File.GetLastWriteTimeUtc(PathOf(room));
                foreach (var src in new[] { "Assets/Silksong/Editor/TransitionRoomsBuilder.cs", "Assets/Silksong/Rooms/RoomWorldStarter.cs" })
                    if (File.Exists(src))
                        Assert.GreaterOrEqual(sceneTime, File.GetLastWriteTimeUtc(src), PathOf(room) + " is older than " + src + "; rebuild it. " + BuildHint);
            }
#else
            Assert.Ignore("Generated rooms are loaded by path in the Editor only.");
#endif
        }

        RoomWorldBootstrap world;

        [SetUp]
        public void SetUp()
        {
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            Stubs.StubsDocPath = Path.Combine(Path.GetTempPath(), "rooms-walk-stubs-" + Guid.NewGuid() + ".md");
            Stubs.ResetForTests();
            // Strays from earlier fixtures would give two worlds; the starter does nothing when one exists.
            foreach (var b in UnityEngine.Object.FindObjectsByType<RoomWorldBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(b.gameObject);
            foreach (var h in UnityEngine.Object.FindObjectsByType<HeroController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(h.gameObject);
            foreach (var c in UnityEngine.Object.FindObjectsByType<GameCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(c.gameObject);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (world != null) UnityEngine.Object.Destroy(world.gameObject);
            world = null;
            yield return null;
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            if (!string.IsNullOrEmpty(Stubs.StubsDocPath) && File.Exists(Stubs.StubsDocPath)) File.Delete(Stubs.StubsDocPath);
            Stubs.StubsDocPath = null;
            Stubs.ResetForTests();
        }

        IEnumerator Until(Func<bool> condition, string what, float timeout = 20f)
        {
            float start = Time.unscaledTime;
            while (!condition())
            {
                if (Time.unscaledTime - start > timeout) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        // Opens Tut_01 as the first scene and waits until its starter has made the world and the hero can be played.
        IEnumerator StartWorld()
        {
            RequireScenes();
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(PathOf("Tut_01"), new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield break;
#endif
            yield return Until(() => RoomWorldBootstrap.Instance != null && RoomWorldBootstrap.Instance.Loader != null
                && RoomWorldBootstrap.Instance.Loader.CurrentRoomName == "Tut_01", "the starter to create the world in Tut_01");
            world = RoomWorldBootstrap.Instance;
            yield return Until(() => world.Hero != null && world.Hero.IsGrounded && !world.Hero.ControlLocked, "the hero to land on the start floor");
        }

        [UnityTest]
        public IEnumerator PressPlayInTut01_CreatesTheWorld_WithOneHero_AtTheStartPoint()
        {
            yield return StartWorld();

            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<HeroController>(FindObjectsSortMode.None).Length, "heroes");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<GameCamera>(FindObjectsSortMode.None).Length, "cameras");
            Assert.AreEqual(GameState.Playing, world.Manager.State);
            Assert.IsNotNull(world.Hero.Config, "the hero must have the project HeroConfig");
            Assert.IsNotNull(world.Hero.Input, "the hero must have input");

            var room = SceneManager.GetSceneByName("Tut_01");
            Assert.AreEqual(room, SceneManager.GetActiveScene());
            Assert.IsNotNull(GateRegistry.Find("right1", room));
            Assert.AreEqual("Tut_01", world.PlayerData.currentScene);
            // Starts on the floor in the left-middle of the room, away from every gate.
            Assert.Greater(world.Hero.transform.position.x, 5f);
            Assert.Less(world.Hero.transform.position.y, 3f);
        }

        [UnityTest]
        public IEnumerator EveryGatePair_ThereAndBack_SameHero_OneRoomAtATime()
        {
            yield return StartWorld();
            var hero = world.Hero;
            var manager = world.Manager;

            foreach (var step in Steps)
            {
                string from = step[0], gateName = step[1], to = step[2], arrivalName = step[3];
                Assert.AreEqual(from, world.Loader.CurrentRoomName, "before " + from + "/" + gateName);

                var gate = GateRegistry.Find(gateName, SceneManager.GetSceneByName(from));
                Assert.AreEqual(gateName, gate.name, from + " has no gate " + gateName);
                Assert.AreEqual(to, gate.TargetScene);
                Assert.AreEqual(arrivalName, gate.EntryGate);

                int started = 0, finished = 0;
                Action<TransitionRequest> onStarted = r => started++;
                Action<bool> onFinished = ok => finished++;
                manager.TransitionStarted += onStarted;
                manager.TransitionFinished += onFinished;
                manager.RequestTransition(gate.BuildRequest());
                yield return Until(() => started > 0, "the change " + from + "/" + gateName + " to start");
                yield return Until(() => finished > 0 && !manager.IsTransitionRunning && manager.State == GameState.Playing && !hero.ControlLocked,
                    "the change " + from + "/" + gateName + " to finish");
                manager.TransitionStarted -= onStarted;
                manager.TransitionFinished -= onFinished;

                Assert.AreEqual(to, world.Loader.CurrentRoomName, "room after " + from + "/" + gateName);
                Assert.AreSame(hero, world.Hero, "the hero must be the same object");
                Assert.AreEqual(to, SceneManager.GetActiveScene().name);

                var newScene = SceneManager.GetSceneByName(to);
                var arrival = GateRegistry.Find(arrivalName, newScene);
                Assert.AreEqual(arrivalName, arrival.name);
                Vector2 heroPos = hero.transform.position;
                Vector2 gatePos = arrival.transform.position;
                Assert.Less(Mathf.Abs(heroPos.x - gatePos.x), 8f, "hero x after arriving at " + to + "/" + arrivalName);
                Assert.Less(Mathf.Abs(heroPos.y - gatePos.y), 4f, "hero y after arriving at " + to + "/" + arrivalName);
                Assert.IsTrue(hero.IsGrounded || hero.Velocity.y <= 0.01f, "hero should be standing at " + to + "/" + arrivalName);

                // Exactly one room scene left loaded (the old one is gone).
                int loadedRooms = 0;
                foreach (var name in Rooms)
                {
                    var s = SceneManager.GetSceneByName(name);
                    if (s.IsValid() && s.isLoaded) loadedRooms++;
                }
                Assert.AreEqual(1, loadedRooms, "loaded room scenes after " + from + "/" + gateName);
            }
        }
    }
}
