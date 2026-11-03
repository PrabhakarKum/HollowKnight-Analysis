using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hollowknight.Silksong.Tests
{
    // Task 4 (saving): resting at a bench and the autosave after a room change. A real world (hero, fader, manager) in
    // two code-built rooms, real physics and real time, saves written to a temp folder. SaveSystem.Root and the slot
    // selection are restored in teardown.
    public class BenchRestTests
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
        string nameA, nameB, tempRoot, savedRoot, stubsPath;
        Scene runnerScene;
        HeroConfig config;
        RoomWorldBootstrap world;
        FakeHeroInput input;
        Bench bench;

        [SetUp]
        public void SetUp()
        {
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            stubsPath = Path.Combine(Path.GetTempPath(), "bench-stubs-" + Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
            savedRoot = SaveSystem.Root;
            tempRoot = Path.Combine(Path.GetTempPath(), "bench-saves-" + Guid.NewGuid().ToString("N"));
            SaveSystem.Root = tempRoot;
            SaveSlots.ResetForTests();
            runnerScene = SceneManager.GetActiveScene();
            RemoveStrays();

            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            nameA = "BnA_" + id;
            nameB = "BnB_" + id;
            source = new CodeSceneSource();
            source.Builders[nameA] = s => BuildRoom(s, nameA, 0f, 1, nameB, true);
            source.Builders[nameB] = s => BuildRoom(s, nameB, 200f, 2, nameA, false);
#if UNITY_EDITOR
            config = HeroConfigConsistency.LoadProjectAsset();
#endif
        }

        static void RemoveStrays()
        {
            foreach (var b in UnityEngine.Object.FindObjectsByType<RoomWorldBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(b.gameObject);
            foreach (var m in UnityEngine.Object.FindObjectsByType<RoomTransitionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (m != null) UnityEngine.Object.DestroyImmediate(m.gameObject);
            foreach (var f in UnityEngine.Object.FindObjectsByType<ScreenFader>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (f != null) UnityEngine.Object.DestroyImmediate(f.gameObject);
            foreach (var h in UnityEngine.Object.FindObjectsByType<HeroController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (h != null) UnityEngine.Object.DestroyImmediate(h.gameObject);
            foreach (var c in UnityEngine.Object.FindObjectsByType<GameCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c != null) UnityEngine.Object.DestroyImmediate(c.gameObject);
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
            bench = null;
            yield return null;
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            SaveSlots.ResetForTests();
            SaveSystem.Root = savedRoot;
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            if (File.Exists(stubsPath)) File.Delete(stubsPath);
            Stubs.StubsDocPath = null;
            Stubs.ResetForTests();
        }

        // Floor (top at y 0), one gate to the other room, RoomInfo with bounds; room A also holds the bench.
        static void BuildRoom(Scene scene, string name, float x0, int zoneId, string otherRoom, bool withBench)
        {
            var floor = new GameObject("Floor");
            floor.AddComponent<BoxCollider2D>().size = new Vector2(60f, 1f);
            floor.transform.position = new Vector3(x0 + 30f, -0.5f, 0f);
            SceneManager.MoveGameObjectToScene(floor, scene);

            bool right = withBench;
            var gateGo = new GameObject(right ? "right1" : "left1");
            gateGo.transform.position = new Vector2(right ? x0 + 59f : x0 + 1f, 2f);
            var box = gateGo.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 4f);
            box.isTrigger = true;
            SceneManager.MoveGameObjectToScene(gateGo, scene);
            gateGo.AddComponent<TransitionPoint>().Configure(otherRoom, right ? "left1" : "right1", Vector2.zero);

            var boundsGo = new GameObject("Bounds");
            var bounds = boundsGo.AddComponent<RoomBounds>();
            bounds.Configure(new Rect(x0, 0f, 60f, 40f));
            SceneManager.MoveGameObjectToScene(boundsGo, scene);
            var infoGo = new GameObject("Info");
            infoGo.AddComponent<RoomInfo>().Configure(name, zoneId, "Zone " + zoneId, bounds, "ZONE_" + zoneId);
            SceneManager.MoveGameObjectToScene(infoGo, scene);

            if (!withBench) return;
            var benchGo = new GameObject("BenchSpot");
            benchGo.transform.position = new Vector3(x0 + 30f, 1f, 0f);
            var area = benchGo.AddComponent<BoxCollider2D>();
            area.isTrigger = true;
            area.size = new Vector2(6f, 6f);   // y -2..4: the hero is inside while standing or falling from 3.5
            benchGo.AddComponent<RespawnMarker>().Configure("Bench", true);
            benchGo.AddComponent<Bench>();
            SceneManager.MoveGameObjectToScene(benchGo, scene);
        }

        void MakeWorld(float fadeSeconds = 0.1f)
        {
            var settings = new RoomTransitionSettings { fadeWaitSeconds = 0f, heroWaitSeconds = 0f, fadeOutSeconds = fadeSeconds, fadeInSeconds = fadeSeconds };
            input = new FakeHeroInput();
            world = RoomWorldBootstrap.Create(config, input, settings, source);
            world.Hero.Health.DeathRespawnDelaySeconds = -1f;
        }

        // World in room A, hero standing on the floor in the middle of the bench trigger.
        IEnumerator StartAtBench()
        {
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            bench = UnityEngine.Object.FindFirstObjectByType<Bench>();
            Assert.IsNotNull(bench);
            world.PlaceHero(new Vector2(30f, 1f));
            yield return Until(() => world.Hero.IsGrounded && world.Hero.State == HeroState.Grounded, "the hero to land");
            yield return Fixed(5);   // trigger events and a settled velocity
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

        static IEnumerator Fixed(int steps)
        {
            for (int i = 0; i < steps; i++) yield return new WaitForFixedUpdate();
        }

        void PressUp() => input.Move = new Vector2(0f, 1f);
        void ReleaseUp() => input.Move = Vector2.zero;

        string SlotFile(int slot) => Path.Combine(tempRoot, "user" + slot + ".json");

        [UnityTest]
        public IEnumerator Rest_HealsToFull_AndSetsTheRespawn()
        {
            yield return StartAtBench();
            world.PlayerData.health = 2;
            world.PlayerData.respawnScene = "Elsewhere";
            world.PlayerData.respawnMarker = "Other";
            int silk = world.PlayerData.silk = 3;

            PressUp();
            yield return Until(() => bench.RestCount == 1, "the rest to finish");

            Assert.AreEqual(world.PlayerData.maxHealth, world.PlayerData.health);
            Assert.AreEqual(nameA, world.PlayerData.respawnScene);
            Assert.AreEqual("Bench", world.PlayerData.respawnMarker);
            Assert.AreEqual(silk, world.PlayerData.silk, "silk is left alone");
        }

        [UnityTest]
        public IEnumerator Rest_WritesASave_WithTheChangesInIt()
        {
            yield return StartAtBench();
            SaveSlots.Select(1);
            world.PlayerData.health = 2;

            PressUp();
            yield return Until(() => bench.RestCount == 1, "the rest to finish");

            Assert.AreEqual(1, bench.SavesWritten);
            Assert.IsTrue(File.Exists(SlotFile(1)));
            PlayerData saved;
            Assert.AreEqual(SaveSystem.SlotFileState.Valid, SaveSystem.Peek(1, out saved));
            Assert.AreEqual(saved.maxHealth, saved.health, "saved after healing");
            Assert.AreEqual(nameA, saved.respawnScene);
            Assert.AreEqual("Bench", saved.respawnMarker);
        }

        [UnityTest]
        public IEnumerator Rest_WithNoSelectedSlot_WritesNothing_ButStillHeals()
        {
            yield return StartAtBench();
            world.PlayerData.health = 1;

            PressUp();
            yield return Until(() => bench.RestCount == 1, "the rest to finish");

            Assert.AreEqual(0, bench.SavesWritten);
            Assert.IsFalse(Directory.Exists(tempRoot) && Directory.GetFiles(tempRoot).Length > 0, "no file written");
            Assert.AreEqual(world.PlayerData.maxHealth, world.PlayerData.health);
        }

        [UnityTest]
        public IEnumerator Rest_LocksControlDuringTheFade_AndGivesItBack()
        {
            yield return StartAtBench();
            PressUp();
            yield return Until(() => bench.IsResting, "the rest to start");
            Assert.IsTrue(world.Hero.ControlLocked, "locked while resting");

            bool rested = false;
            bench.Rested += () => rested = true;
            yield return Until(() => rested, "Rested");
            Assert.IsFalse(world.Hero.ControlLocked);
            Assert.IsFalse(world.Hero.Health.TransitionInvulnerable);
            Assert.IsFalse(bench.IsResting);
            Assert.AreEqual(0f, world.Fader.Alpha, 1e-4f, "screen is visible again");
        }

        [UnityTest]
        public IEnumerator NoUpPress_NoRest()
        {
            yield return StartAtBench();
            yield return Fixed(20);
            Assert.AreEqual(0, bench.RestCount);
            Assert.IsFalse(bench.IsResting);
        }

        [UnityTest]
        public IEnumerator HoldingUp_RestsOnce()
        {
            yield return StartAtBench();
            PressUp();
            yield return Until(() => bench.RestCount == 1, "the rest to finish");
            // Longer than the cooldown, up still held.
            float end = Time.unscaledTime + BenchRules.CooldownSeconds + 0.6f;
            while (Time.unscaledTime < end) yield return null;
            Assert.AreEqual(1, bench.RestCount);

            // Release and press again: a new rest.
            ReleaseUp();
            yield return Fixed(3);
            PressUp();
            yield return Until(() => bench.RestCount == 2, "the second rest");
        }

        [UnityTest]
        public IEnumerator Airborne_DoesNotRest_EvenWhenUpWasPressedInTheAir()
        {
            yield return StartAtBench();
            world.PlaceHero(new Vector2(30f, 3.5f));
            PressUp();
            yield return Fixed(2);
            Assert.IsFalse(world.Hero.IsGrounded, "precondition: still in the air");
            Assert.AreEqual(0, bench.RestCount);

            yield return Until(() => world.Hero.IsGrounded, "landing");
            yield return Fixed(10);
            Assert.AreEqual(0, bench.RestCount, "up was pressed before landing and is only held, not pressed anew");
        }

        [UnityTest]
        public IEnumerator DeadHero_DoesNotRest()
        {
            yield return StartAtBench();
            world.Hero.Health.TakeHit(new HitInstance { Damage = 99, Direction = AttackDirection.Normal, Source = bench.gameObject });
            Assert.IsTrue(world.Hero.Health.IsDead);
            PressUp();
            yield return Fixed(10);
            Assert.AreEqual(0, bench.RestCount);
            Assert.IsFalse(bench.IsResting);
        }

        [UnityTest]
        public IEnumerator HeroLeavingTheRoom_ControlLocked_DoesNotRest()
        {
            yield return StartAtBench();
            world.Hero.Transition.LeaveRoom(GatePosition.Right);
            Assert.IsTrue(world.Hero.ControlLocked);
            PressUp();
            yield return Fixed(10);
            Assert.AreEqual(0, bench.RestCount);
            world.Hero.Transition.RestoreControl();
        }

        [UnityTest]
        public IEnumerator RoomChange_AutosavesOnce_AfterItFinished()
        {
            MakeWorld(0f);
            yield return world.StartInRoom(nameA, "right1");
            SaveSlots.Select(2);
            Assert.AreEqual(0, world.AutosaveCount);

            var gate = GateRegistry.Find("right1", SceneManager.GetSceneByName(nameA));
            world.Manager.RequestTransition(gate.BuildRequest());
            yield return Until(() => world.AutosaveCount > 0 && !world.Manager.IsTransitionRunning && !world.Hero.ControlLocked, "the autosave");
            yield return Fixed(5);

            Assert.AreEqual(1, world.AutosaveCount);
            PlayerData saved;
            Assert.AreEqual(SaveSystem.SlotFileState.Valid, SaveSystem.Peek(2, out saved));
            Assert.AreEqual(nameB, saved.currentScene, "saved after the room was recorded");
        }

        [UnityTest]
        public IEnumerator RoomChange_WithNoSelectedSlot_WritesNothing()
        {
            MakeWorld(0f);
            yield return world.StartInRoom(nameA, "right1");
            var gate = GateRegistry.Find("right1", SceneManager.GetSceneByName(nameA));
            world.Manager.RequestTransition(gate.BuildRequest());
            yield return Until(() => world.PlayerData.currentScene == nameB && !world.Manager.IsTransitionRunning && !world.Hero.ControlLocked, "the room change");
            Assert.AreEqual(0, world.AutosaveCount);
            Assert.IsFalse(Directory.Exists(tempRoot) && Directory.GetFiles(tempRoot).Length > 0);
        }

        [UnityTest]
        public IEnumerator FailedRoomChange_DoesNotSave()
        {
            MakeWorld(0f);
            yield return world.StartInRoom(nameA, "right1");
            SaveSlots.Select(2);
            var gate = GateRegistry.Find("right1", SceneManager.GetSceneByName(nameA));
            gate.Configure("NoSuchRoom_" + nameA, "left1", Vector2.zero);

            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("cannot be loaded"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(FailedOld));
            bool finished = false;
            world.Manager.TransitionFinished += ok => finished = true;
            world.Manager.RequestTransition(gate.BuildRequest());
            yield return Until(() => finished && !world.Manager.IsTransitionRunning && !world.Hero.ControlLocked, "the failed change");

            Assert.AreEqual(0, world.AutosaveCount);
            Assert.IsFalse(File.Exists(SlotFile(2)));
        }

        [UnityTest]
        public IEnumerator DeadHero_DoesNotAutosave_OnAFinishedChange()
        {
            MakeWorld(0f);
            yield return world.StartInRoom(nameA, "right1");
            SaveSlots.Select(2);
            world.Hero.Health.TakeHit(new HitInstance { Damage = 99, Direction = AttackDirection.Normal, Source = world.gameObject });
            Assert.IsTrue(world.Hero.Health.IsDead);

            // The finish handler is private; call it the way the manager's event does.
            var method = typeof(RoomWorldBootstrap).GetMethod("OnTransitionFinished", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, "RoomWorldBootstrap.OnTransitionFinished was renamed; update this test");
            method.Invoke(world, new object[] { true });

            Assert.AreEqual(0, world.AutosaveCount);
            Assert.IsFalse(File.Exists(SlotFile(2)));
        }
    }
}
