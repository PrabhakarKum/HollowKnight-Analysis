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
            RemoveStrays();

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

        // Earlier fixtures can leave a hero, camera or manager behind in a scene that stays loaded (for example the
        // hero room scene). Counting tests would then see two of everything, so remove what this fixture did not make.
        static void RemoveStrays()
        {
            int removed = 0;
            foreach (var b in UnityEngine.Object.FindObjectsByType<RoomWorldBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { UnityEngine.Object.DestroyImmediate(b.gameObject); removed++; }
            foreach (var m in UnityEngine.Object.FindObjectsByType<RoomTransitionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { if (m != null) UnityEngine.Object.DestroyImmediate(m.gameObject); removed++; }
            foreach (var f in UnityEngine.Object.FindObjectsByType<ScreenFader>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { if (f != null) UnityEngine.Object.DestroyImmediate(f.gameObject); removed++; }
            foreach (var h in UnityEngine.Object.FindObjectsByType<HeroController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { if (h != null) UnityEngine.Object.DestroyImmediate(h.gameObject); removed++; }
            foreach (var c in UnityEngine.Object.FindObjectsByType<GameCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { if (c != null) UnityEngine.Object.DestroyImmediate(c.gameObject); removed++; }
            if (removed > 0) Debug.LogWarning("[RoomWorldLoadingTests] Removed " + removed + " leftover world objects from an earlier fixture.");
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
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
            // Standalone managers go last, after the scenes are unloaded.
            foreach (var go in extras) if (go != null) UnityEngine.Object.Destroy(go);
            extras.Clear();
            yield return null;
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
            // Bottom gate under the floor (the hero can never touch it; used as an arrival spot).
            AddGate(scene, "bot1", new Vector2(x0 + 30f, -1.5f), null, "top1", new Vector2(4f, 1f));

            var boundsGo = new GameObject("Bounds");
            var bounds = boundsGo.AddComponent<RoomBounds>();
            bounds.Configure(new Rect(x0, 0f, 60f, 40f));
            SceneManager.MoveGameObjectToScene(boundsGo, scene);

            var infoGo = new GameObject("Info");
            var info = infoGo.AddComponent<RoomInfo>();
            info.Configure(name, zoneId, zone, bounds, zone.ToUpperInvariant().Replace(' ', '_'));
            SceneManager.MoveGameObjectToScene(infoGo, scene);
        }

        static void AddGate(Scene scene, string gateName, Vector2 pos, string target, string arrival, Vector2? size = null)
        {
            var go = new GameObject(gateName);
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = size ?? new Vector2(1f, 4f);
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

        // Sends a request and waits for the whole change: first until it has started, then until it has finished
        // (TransitionFinished raised, not running, Playing, and optionally a final condition). Listeners are added
        // before the request so no event is missed.
        IEnumerator RequestAndWait(RoomTransitionManager manager, TransitionRequest request, Func<bool> alsoTrue = null)
        {
            int started = 0, finished = 0;
            Action<TransitionRequest> onStarted = r => started++;
            Action<bool> onFinished = ok => finished++;
            manager.TransitionStarted += onStarted;
            manager.TransitionFinished += onFinished;
            try
            {
                manager.RequestTransition(request);
                yield return Until(() => started > 0, "the transition to start");
                yield return Until(() => finished > 0 && !manager.IsTransitionRunning && manager.State == GameState.Playing
                    && (alsoTrue == null || alsoTrue()), "the transition to finish");
            }
            finally
            {
                manager.TransitionStarted -= onStarted;
                manager.TransitionFinished -= onFinished;
            }
        }

        IEnumerator RequestAndWaitWorld(TransitionRequest request) =>
            RequestAndWait(world.Manager, request, () => !world.Hero.ControlLocked);

        // Sends the hero through the named gate of the room it is in.
        IEnumerator Leave(string room, string gateName)
        {
            var gate = GateRegistry.Find(gateName, SceneManager.GetSceneByName(room));
            yield return RequestAndWaitWorld(gate.BuildRequest());
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
            Assert.AreEqual(3, GateRegistry.Count, "only the live room's three gates are registered");
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
            world.PlayerData.respawnScene = "BenchRoom";   // a bench / marker spot; room changes must not touch it
            yield return world.StartInRoom(nameA, "right1");
            Assert.AreEqual(nameA, world.PlayerData.currentScene);
            Assert.AreEqual("ZONE_A", world.PlayerData.mapZone, "stable key, not the display name");

            yield return Leave(nameA, "right1");
            Assert.AreEqual(nameB, world.PlayerData.currentScene);
            Assert.AreEqual("ZONE_B", world.PlayerData.mapZone);
            Assert.AreEqual("BenchRoom", world.PlayerData.respawnScene, "respawnScene is bench-only");
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
            yield return RequestAndWaitWorld(gate.BuildRequest());

            Assert.IsTrue(SceneManager.GetSceneByName(nameA).isLoaded, "A is still loaded");
            Assert.AreEqual(nameA, SceneManager.GetActiveScene().name);
            Assert.IsFalse(world.Hero.ControlLocked, "hero has control");
            Assert.IsFalse(world.RoomCamera.IsFrozen, "camera is released");
            Assert.AreEqual(nameA, world.PlayerData.currentScene);
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

        // ---- fix round: same room, timeouts, abandon, fail-after-swap, placement, pairing ----

        sealed class InstantFader : IScreenFader
        {
            public IEnumerator FadeOut() { yield break; }
            public IEnumerator FadeIn() { yield break; }
        }

        // A hero stub. refuse = it never takes control (like a hero without input).
        sealed class StubHero : ITransitionHero, ITransitionHeroRecovery
        {
            readonly bool refuse;
            public StubHero(bool refuseControl) { refuse = refuseControl; }
            public bool ControlLocked { get; private set; }
            public Rigidbody2D Body => null;
            public HeroTransitionState GetTransitionState() => new HeroTransitionState { InPosition = true };
            public void LeaveRoom(GatePosition gate) { if (!refuse) ControlLocked = true; }
            public IEnumerator EnterRoom(TransitionPoint gate, float delay)
            {
                yield return null;
                ControlLocked = false;
            }
            public void RestoreControl() { ControlLocked = false; }
        }

        sealed class FakeCleanupLoader : IRoomLoader, IRoomLoaderCleanup
        {
            public int Abandoned, Finished;
            public string CurrentRoomName => null;
            public IEnumerator LoadHidden(string sceneName) { yield return null; }
            public IEnumerator ActivateAndUnloadPrevious() { yield return null; throw new InvalidOperationException("activate-boom"); }
            public void AbandonPending() { Abandoned++; }
            public IEnumerator FinishAbandon() { Finished++; yield break; }
        }

        // The "load" is a real engine operation that is not a scene load and does not finish at once.
        sealed class SlowSource : IRoomSceneSource
        {
            public bool Exists(string sceneName) => true;
            public AsyncOperation BeginLoad(string sceneName) => Resources.UnloadUnusedAssets();
        }

        static RoomTransitionSettings Instant() =>
            new RoomTransitionSettings { fadeWaitSeconds = 0f, heroWaitSeconds = 0f, fadeOutSeconds = 0f, fadeInSeconds = 0f };

        readonly List<GameObject> extras = new List<GameObject>();

        RoomTransitionManager MakeStandaloneManager(IRoomLoader loader, ITransitionHero hero)
        {
            var go = new GameObject("StandaloneManager");
            extras.Add(go);
            // Outside any room scene: a real room swap unloads the active scene, and a manager living in it would be
            // destroyed mid-transition (OnDestroy aborts the run).
            UnityEngine.Object.DontDestroyOnLoad(go);
            var manager = go.AddComponent<RoomTransitionManager>();
            manager.Configure(loader, new InstantFader(), hero, Instant());
            return manager;
        }

        [UnityTest]
        public IEnumerator SameRoomRequest_IsRejected_BeforeAnythingMoves()
        {
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            var gate = GateRegistry.Find("right1", SceneManager.GetSceneByName(nameA));
            gate.Configure(nameA, "left1", Vector2.zero);
            int started = 0;
            world.Manager.TransitionStarted += r => started++;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("targetIsCurrentRoom=True"));
            world.Manager.RequestTransition(gate.BuildRequest());
            yield return null;

            Assert.AreEqual(0, started);
            Assert.AreEqual(GameState.Playing, world.Manager.State);
            Assert.IsFalse(world.Manager.IsTransitionRunning);
            Assert.IsFalse(world.Hero.ControlLocked);
            Assert.IsTrue(SceneManager.GetSceneByName(nameA).isLoaded);
        }

        [UnityTest]
        public IEnumerator Loader_RefusesTheLiveRoomsName()
        {
            var loader = new AdditiveRoomLoader(source);
            yield return loader.LoadHidden(nameA);
            yield return loader.ActivateAndUnloadPrevious();
            Assert.Throws<InvalidOperationException>(() => loader.LoadHidden(nameA).MoveNext());
        }

        [UnityTest]
        public IEnumerator Loader_TimesOut_WhenALoadNeverGetsReady()
        {
            var loader = new AdditiveRoomLoader(new SlowSource(), new RoomTransitionSettings { loadTimeoutSeconds = 0f });
            var steps = loader.LoadHidden("Slow");
            Exception err = null;
            while (true)
            {
                try { if (!steps.MoveNext()) break; }
                catch (Exception e) { err = e; break; }
                yield return null;
            }
            Assert.IsNotNull(err, "the wait gave up with an error");
            Assert.IsFalse(loader.HasPending, "nothing stays pending");
        }

        [UnityTest]
        public IEnumerator Loader_LoadingTwice_AbandonsTheFirstHiddenRoom()
        {
            var loader = new AdditiveRoomLoader(source);
            yield return loader.LoadHidden(nameA);
            Assert.IsTrue(SceneManager.GetSceneByName(nameA).isLoaded);
            yield return loader.LoadHidden(nameB);
            yield return Until(() => !SceneManager.GetSceneByName(nameA).isLoaded, "abandoned room A to unload");
            yield return loader.ActivateAndUnloadPrevious();
            Assert.AreEqual(nameB, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator Loader_AbandonPending_LeavesNoExtraScene()
        {
            var loader = new AdditiveRoomLoader(source);
            yield return loader.LoadHidden(nameA);
            loader.AbandonPending();
            Assert.IsFalse(loader.HasPending);
            yield return loader.FinishAbandon();
            yield return Until(() => !SceneManager.GetSceneByName(nameA).isLoaded, "abandoned room to unload");
        }

        [UnityTest]
        public IEnumerator FailedChange_AsksTheLoaderToCleanUp()
        {
            var loader = new FakeCleanupLoader();
            var manager = MakeStandaloneManager(loader, new StubHero(false));
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("activate-boom"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(FailedOld));
            yield return RequestAndWait(manager, new TransitionRequest { TargetScene = "Elsewhere", EntryGate = "left1", LeaveDirection = GatePosition.Right });
            Assert.GreaterOrEqual(loader.Abandoned, 1);
            Assert.AreEqual(1, loader.Finished);
        }

        [UnityTest]
        public IEnumerator HeroPlaced_IsSkipped_WhenTheHeroNeverTookControl()
        {
            var loader = new AdditiveRoomLoader(source);
            yield return loader.LoadHidden(nameA);
            yield return loader.ActivateAndUnloadPrevious();
            var manager = MakeStandaloneManager(loader, new StubHero(true));
            int placed = 0;
            bool finishedOk = false;
            manager.HeroPlaced += (s, g) => placed++;
            manager.TransitionFinished += ok => finishedOk = ok;

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("hero was not placed"));
            var gate = GateRegistry.Find("right1", SceneManager.GetSceneByName(nameA));
            yield return RequestAndWait(manager, gate.BuildRequest());

            Assert.AreEqual(0, placed);
            Assert.IsTrue(finishedOk);
        }

        [UnityTest]
        public IEnumerator FailAfterSwap_CameraFollowsTheLiveRoom_AndHeroIsFree()
        {
            string nameC = "RwC_" + nameA.Substring(4);
            source.Builders[nameC] = s =>
            {
                var boundsGo = new GameObject("Bounds");
                var bounds = boundsGo.AddComponent<RoomBounds>();
                bounds.Configure(new Rect(400f, 0f, 60f, 40f));
                SceneManager.MoveGameObjectToScene(boundsGo, s);
                var infoGo = new GameObject("Info");
                infoGo.AddComponent<RoomInfo>().Configure(nameC, 3, "Zone C", bounds, "ZONE_C");
                SceneManager.MoveGameObjectToScene(infoGo, s);
            };
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            var gate = GateRegistry.Find("right1", SceneManager.GetSceneByName(nameA));
            gate.Configure(nameC, "left1", Vector2.zero);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("no gates in the searched room"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[Transition\] Failed: .*old room is already gone"));
            yield return RequestAndWaitWorld(gate.BuildRequest());

            var infoC = RoomWorldBootstrap.FindRoomInfo(SceneManager.GetSceneByName(nameC));
            Assert.AreSame(infoC.Bounds, world.RoomCamera.Bounds, "camera follows the room that is live");
            Assert.IsFalse(world.RoomCamera.IsFrozen);
            Assert.IsFalse(world.Hero.ControlLocked);
            Assert.AreEqual(nameC, world.PlayerData.currentScene);
        }

        [UnityTest]
        public IEnumerator SupersededRun_StillPairsStartedWithFinished()
        {
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            var events = new List<string>();
            world.Manager.TransitionStarted += r => events.Add("started");
            world.Manager.TransitionFinished += ok => events.Add("finished:" + ok);

            world.Manager.RequestTransition(GateRegistry.Find("right1", SceneManager.GetSceneByName(nameA)).BuildRequest());
            yield return Until(() => world.Manager.State == GameState.EnteringLevel, "the walk-in");
            // Leaving again during the walk-in is the bottom-gate exception: the new run takes over the hero.
            world.Manager.RequestTransition(GateRegistry.Find("left1", SceneManager.GetSceneByName(nameB)).BuildRequest());
            yield return Idle();

            CollectionAssert.AreEqual(new[] { "started", "finished:False", "started", "finished:True" }, events);
        }

        [UnityTest]
        public IEnumerator ArrivingAtABottomGate_DoesNotImmediatelyLeaveAgain()
        {
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            // Documented behaviour: the arrival gate is NOT locked out. The hero is put 3 units above it and thrown
            // up, so it only touches the gate again if it falls back into it, which is the original's bottom-gate
            // exception. Here the gate sits under the floor, so the hero lands and the change ends.
            var gate = GateRegistry.Find("right1", SceneManager.GetSceneByName(nameA));
            gate.Configure(nameB, "bot1", Vector2.zero);
            int started = 0;
            world.Manager.TransitionStarted += r => started++;

            yield return RequestAndWaitWorld(gate.BuildRequest());

            Assert.AreEqual(1, started);
            Assert.AreEqual(nameB, SceneManager.GetActiveScene().name);
            Assert.AreEqual(230f, world.Hero.transform.position.x, 3f);
            Assert.Greater(world.Hero.transform.position.y, 0f, "standing on the floor, not below it");
        }

        [UnityTest]
        public IEnumerator ThrowingEventListener_IsLogged_AndDoesNotStopTheChange()
        {
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            int later = 0;
            world.Manager.TransitionStarted += r => throw new InvalidOperationException("listener-boom");
            world.Manager.TransitionStarted += r => later++;

            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("listener-boom"));
            yield return Leave(nameA, "right1");

            Assert.AreEqual(1, later, "the next listener still ran");
            Assert.IsTrue(SceneManager.GetSceneByName(nameB).isLoaded);
        }
    }
}
