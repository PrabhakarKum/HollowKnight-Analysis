using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hollowknight.Silksong.Tests
{
    // Real room swapping with the persistent bootstrap. Rooms are scenes built in code and handed to the loader through
    // a test IRoomSceneSource (scenes that are live at once, so the loader's hide-then-activate path is what runs).
    // The engine-deferred path (allowSceneActivation) needs scene files and is covered by the grey-box rooms task.
    // Real physics and real time are used, with all transition waits set to zero.
    public class RoomWorldLoadingTests
    {
        sealed class CodeSceneSource : IRoomSceneSource
        {
            public readonly Dictionary<string, Action<Scene>> Builders = new Dictionary<string, Action<Scene>>();
            public readonly List<string> Created = new List<string>();
            public bool Exists(string sceneName) => Builders.ContainsKey(sceneName);
            public AsyncOperation BeginLoad(string sceneName)
            {
                var scene = SceneManager.CreateScene(sceneName);
                Created.Add(sceneName);
                Builders[sceneName](scene);
                return null;
            }
        }

        const string FailedOld = @"\[Transition\] Failed: .*Returning to the old room";

        CodeSceneSource source;
        string nameA, nameB;
        Scene runnerScene;
        HeroConfig config;
        RoomWorldBootstrap world;

        [SetUp]
        public void SetUp()
        {
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            Stubs.StubsDocPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rw-stubs-" + Guid.NewGuid() + ".md");
            Stubs.ResetForTests();
            runnerScene = SceneManager.GetActiveScene();

            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            nameA = "RwA_" + id;
            nameB = "RwB_" + id;
            source = new CodeSceneSource();
            // A: x 0..60, B: x 200..260. Same gate names in both on purpose.
            source.Builders[nameA] = s => BuildRoom(s, nameA, 0f, 1, "Zone A", null, nameB);
            source.Builders[nameB] = s => BuildRoom(s, nameB, 200f, 2, "Zone B", nameA, null);
#if UNITY_EDITOR
            config = HeroConfigConsistency.LoadProjectAsset();
#endif
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (world != null) UnityEngine.Object.Destroy(world.gameObject);
            yield return null;
            if (runnerScene.IsValid()) SceneManager.SetActiveScene(runnerScene);
            foreach (var name in source.Created)
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            world = null;
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            Stubs.StubsDocPath = null;
            Stubs.ResetForTests();
        }

        // Floor, two gates and a RoomInfo with bounds. leftTarget / rightTarget: scene the gate leads to (null = none),
        // always arriving at the gate on the opposite side.
        static void BuildRoom(Scene scene, string name, float x0, int zoneId, string zone, string leftTarget, string rightTarget)
        {
            var floor = new GameObject("Floor");
            floor.AddComponent<BoxCollider2D>().size = new Vector2(60f, 1f);
            floor.transform.position = new Vector3(x0 + 30f, -0.5f, 0f);
            SceneManager.MoveGameObjectToScene(floor, scene);

            AddGate(scene, "left1", new Vector2(x0 + 1f, 2f), leftTarget, "right1");
            AddGate(scene, "right1", new Vector2(x0 + 59f, 2f), rightTarget, "left1");

            var boundsGo = new GameObject("Bounds");
            var bounds = boundsGo.AddComponent<RoomBounds>();
            bounds.Configure(new Rect(x0, 0f, 60f, 40f));
            SceneManager.MoveGameObjectToScene(boundsGo, scene);

            var infoGo = new GameObject("Info");
            var info = infoGo.AddComponent<RoomInfo>();
            info.Configure(name, zoneId, zone, bounds);
            SceneManager.MoveGameObjectToScene(infoGo, scene);
        }

        static void AddGate(Scene scene, string gateName, Vector2 pos, string target, string arrival)
        {
            var go = new GameObject(gateName);
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 4f);
            box.isTrigger = true;
            SceneManager.MoveGameObjectToScene(go, scene);
            var gate = go.AddComponent<TransitionPoint>();
            if (target != null) gate.Configure(target, arrival, Vector2.zero);
        }

        RoomWorldBootstrap MakeWorld()
        {
            var settings = new RoomTransitionSettings { fadeWaitSeconds = 0f, heroWaitSeconds = 0f, fadeOutSeconds = 0f, fadeInSeconds = 0f };
            world = RoomWorldBootstrap.Create(config, new FakeHeroInput(), settings, source);
            return world;
        }

        IEnumerator Until(Func<bool> condition, string what, float timeout = 5f)
        {
            float start = Time.unscaledTime;
            while (!condition())
            {
                if (Time.unscaledTime - start > timeout) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        IEnumerator Idle() => Until(() => !world.Manager.IsTransitionRunning && world.Manager.State == GameState.Playing
            && !world.Hero.ControlLocked, "transition to finish and hero control");

        // Sends the hero through the named gate of the room it is in.
        IEnumerator Leave(string room, string gateName)
        {
            var gate = GateRegistry.Find(gateName, SceneManager.GetSceneByName(room));
            world.Manager.RequestTransition(gate.BuildRequest());
            yield return Idle();
        }

        static int Count<T>() where T : UnityEngine.Object =>
            UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        [UnityTest]
        public IEnumerator LeaveA_ArriveInB_AtNamedGate_HeroIsTheSameObject()
        {
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            int heroId = world.Hero.GetInstanceID();
            var arrivedAt = Vector2.zero;
            world.Manager.HeroPlaced += (s, g) => arrivedAt = world.Hero.transform.position;

            yield return Leave(nameA, "right1");

            Assert.IsFalse(SceneManager.GetSceneByName(nameA).isLoaded, "room A is gone");
            Assert.AreEqual(nameB, SceneManager.GetActiveScene().name);
            Assert.AreEqual(heroId, world.Hero.GetInstanceID(), "same hero object");
            // A's right1 leads to B's left1 at x 201; B's right1 is at 259, so this proves the named gate was used.
            Assert.AreEqual(201f, arrivedAt.x, 0.6f);
            Assert.IsFalse(world.Hero.ControlLocked);
            Assert.AreEqual(GameState.Playing, world.Manager.State);
        }

        [UnityTest]
        public IEnumerator Camera_UsesRoomBBounds_AndIsInsideAfterSnap()
        {
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            yield return Leave(nameA, "right1");

            var sceneB = SceneManager.GetSceneByName(nameB);
            var infoB = RoomWorldBootstrap.FindRoomInfo(sceneB);
            Assert.AreSame(infoB.Bounds, world.RoomCamera.Bounds, "camera bounds are room B's");
            Assert.IsTrue(infoB.Bounds.Area.Contains(world.RoomCamera.transform.position), "camera is inside B");
            Assert.IsFalse(world.RoomCamera.IsFrozen);
        }

        [UnityTest]
        public IEnumerator Reentry_AtoBtoAtoB_LeavesOneOfEverything()
        {
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            int heroId = world.Hero.GetInstanceID();

            yield return Leave(nameA, "right1");
            yield return Leave(nameB, "left1");
            Assert.IsTrue(SceneManager.GetSceneByName(nameA).isLoaded, "back in A");
            Assert.IsFalse(SceneManager.GetSceneByName(nameB).isLoaded);
            yield return Leave(nameA, "right1");

            yield return null;
            Assert.AreEqual(1, Count<HeroController>());
            Assert.AreEqual(1, Count<GameCamera>());
            Assert.AreEqual(1, Count<RoomTransitionManager>());
            Assert.AreEqual(1, Count<ScreenFader>());
            Assert.AreEqual(heroId, world.Hero.GetInstanceID());
            Assert.AreEqual(2, GateRegistry.Count, "only the live room's two gates are registered");
            Assert.AreSame(world.Manager, TransitionPoint.Handler);
        }

        [UnityTest]
        public IEnumerator SameNamedGates_ResolveToTheNewRoom()
        {
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            yield return Leave(nameA, "right1");

            var sceneB = SceneManager.GetSceneByName(nameB);
            var gate = GateRegistry.Find("left1", sceneB);
            Assert.AreEqual(sceneB, gate.gameObject.scene);
            Assert.AreEqual(201f, gate.transform.position.x, 0.01f);
        }

        [UnityTest]
        public IEnumerator PlayerData_FollowsTheRoom()
        {
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            Assert.AreEqual(nameA, world.PlayerData.respawnScene);
            Assert.AreEqual("Zone A", world.PlayerData.mapZone);

            yield return Leave(nameA, "right1");
            Assert.AreEqual(nameB, world.PlayerData.respawnScene);
            Assert.AreEqual("Zone B", world.PlayerData.mapZone);
        }

        [UnityTest]
        public IEnumerator MissingScene_LeavesHeroInA_WithControl()
        {
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            var gate = GateRegistry.Find("right1", SceneManager.GetSceneByName(nameA));
            // A gate to a room that does not exist.
            gate.Configure("NoSuchRoom_" + nameA, "left1", Vector2.zero);

            // The manager logs the loader's exception first, then its own summary.
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("cannot be loaded"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(FailedOld));
            world.Manager.RequestTransition(gate.BuildRequest());
            yield return Idle();

            Assert.IsTrue(SceneManager.GetSceneByName(nameA).isLoaded, "A is still loaded");
            Assert.AreEqual(nameA, SceneManager.GetActiveScene().name);
            Assert.IsFalse(world.Hero.ControlLocked, "hero has control");
            Assert.IsFalse(world.RoomCamera.IsFrozen, "camera is released");
            Assert.AreEqual(nameA, world.PlayerData.respawnScene);
        }

        [UnityTest]
        public IEnumerator SecondBootstrap_RemovesItself_AndCreateReturnsTheRunningWorld()
        {
            MakeWorld();
            Assert.AreSame(world, RoomWorldBootstrap.Create(config, new FakeHeroInput()));

            var extra = new GameObject("ExtraWorld").AddComponent<RoomWorldBootstrap>();
            yield return null;
            Assert.IsTrue(extra == null, "the duplicate destroyed itself");
            Assert.AreSame(world, RoomWorldBootstrap.Instance);
            Assert.AreEqual(1, Count<HeroController>());
            Assert.AreEqual(1, Count<RoomTransitionManager>());
        }

        [UnityTest]
        public IEnumerator Teardown_ClearsStatics_AndRemovesPersistentObjects()
        {
            MakeWorld();
            UnityEngine.Object.Destroy(world.gameObject);
            yield return null;
            Assert.IsNull(RoomWorldBootstrap.Instance);
            Assert.IsNull(TransitionPoint.Handler);
            Assert.AreEqual(0, Count<HeroController>());
            Assert.AreEqual(0, Count<GameCamera>());
            world = null;
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator Hero_UsesTheProjectHeroConfigAsset_NotACopy()
        {
            MakeWorld();
            yield return null;
            Assert.AreSame(HeroConfigConsistency.LoadProjectAsset(), world.Hero.Config);
            Assert.IsEmpty(HeroConfigConsistency.FindWiringProblems(HeroConfigConsistency.LoadProjectAsset()));
        }
#endif

        [UnityTest]
        public IEnumerator ManagerEvents_AreRaisedInOrder()
        {
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            var events = new List<string>();
            world.Manager.TransitionStarted += r => events.Add("started");
            world.Manager.RoomActivated += (s, g) => events.Add("activated:" + s.name);
            world.Manager.HeroPlaced += (s, g) => events.Add("placed:" + g.name);
            world.Manager.TransitionFinished += ok => events.Add("finished:" + ok);

            yield return Leave(nameA, "right1");

            CollectionAssert.AreEqual(new[] { "started", "activated:" + nameB, "placed:left1", "finished:True" }, events);
        }

        [UnityTest]
        public IEnumerator Loader_KeepsRoomHidden_UntilActivated_ThenGatesAreRegistered()
        {
            var loader = new AdditiveRoomLoader(source);
            yield return loader.LoadHidden(nameA);
            yield return loader.ActivateAndUnloadPrevious();
            var sceneA = SceneManager.GetSceneByName(nameA);
            Assert.IsNotNull(GateRegistry.FirstGate(sceneA));

            yield return loader.LoadHidden(nameB);
            var sceneB = SceneManager.GetSceneByName(nameB);
            Assert.IsTrue(sceneB.isLoaded);
            foreach (var root in sceneB.GetRootGameObjects()) Assert.IsFalse(root.activeSelf, root.name + " is hidden");
            Assert.IsNull(GateRegistry.FirstGate(sceneB), "hidden room has no registered gates");
            Assert.IsNotNull(GateRegistry.FirstGate(sceneA), "old room still live");

            yield return loader.ActivateAndUnloadPrevious();
            foreach (var root in sceneB.GetRootGameObjects()) Assert.IsTrue(root.activeSelf, root.name + " is live");
            Assert.IsNotNull(GateRegistry.Find("left1", sceneB), "gates registered once the step finishes");
            Assert.AreEqual(sceneB, SceneManager.GetActiveScene());
            Assert.IsFalse(SceneManager.GetSceneByName(nameA).isLoaded, "previous room unloaded");
            Assert.AreEqual(sceneB, loader.CurrentRoom);
        }

        [UnityTest]
        public IEnumerator Loader_NeverUnloadsAProtectedScene()
        {
            var loader = new AdditiveRoomLoader(source);
            yield return loader.LoadHidden(nameA);
            yield return loader.ActivateAndUnloadPrevious();
            loader.Protect(SceneManager.GetSceneByName(nameA));

            yield return loader.LoadHidden(nameB);
            yield return loader.ActivateAndUnloadPrevious();
            Assert.IsTrue(SceneManager.GetSceneByName(nameA).isLoaded, "protected scene stays");
        }
    }
}
