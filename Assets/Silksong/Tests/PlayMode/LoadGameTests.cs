using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hollowknight.Silksong.Tests
{
    // Milestone 3 task 5: starting a game in a slot (new / continue) and leaving it. A real world in code-built rooms
    // (Tut_01 with the Start marker, a wall and a pickup; a second room with a bench; two bare rooms), saves in a temp
    // folder. SaveSystem.Root, the slot selection and every scene are restored in teardown.
    public class LoadGameTests
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

        const string FirstRoom = "Tut_01";
        const string WallId = "Ld_Wall";
        const string PickupId = "Ld_Pickup";

        CodeSceneSource source;
        string nameB, nameC, nameD, tempRoot, savedRoot, stubsPath;
        Scene runnerScene;
        int sceneCountAtStart;
        HeroConfig config;
        FakeHeroInput input;
        RoomTransitionSettings settings;
        RoomWorldBootstrap world;

        [SetUp]
        public void SetUp()
        {
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            PersistentBool.ResetStatics();
            stubsPath = Path.Combine(Path.GetTempPath(), "load-stubs-" + Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
            savedRoot = SaveSystem.Root;
            tempRoot = Path.Combine(Path.GetTempPath(), "load-saves-" + Guid.NewGuid().ToString("N"));
            SaveSystem.Root = tempRoot;
            SaveSlots.ResetForTests();
            runnerScene = SceneManager.GetActiveScene();
            RemoveStrays();
            sceneCountAtStart = SceneManager.sceneCount;

            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            nameB = "LdB_" + id;
            nameC = "LdC_" + id;
            nameD = "LdD_" + id;
            source = new CodeSceneSource();
            source.Builders[FirstRoom] = s => BuildRoom(s, FirstRoom, 0f, 1, nameB, RoomKind.First);
            source.Builders[nameB] = s => BuildRoom(s, nameB, 200f, 2, FirstRoom, RoomKind.BenchRoom);
            source.Builders[nameC] = s => BuildRoom(s, nameC, 400f, 3, FirstRoom, RoomKind.StartOnly);
            source.Builders[nameD] = s => BuildRoom(s, nameD, 600f, 4, FirstRoom, RoomKind.Bare);
            settings = new RoomTransitionSettings { fadeWaitSeconds = 0f, heroWaitSeconds = 0f, fadeOutSeconds = 0.05f, fadeInSeconds = 0.05f };
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
            // Whatever the test left running goes through the same exit the game uses.
            if (RoomWorldBootstrap.Instance != null) yield return GameSession.Quit(false);
            if (world != null) UnityEngine.Object.Destroy(world.gameObject);
            world = null;
            yield return null;
            if (runnerScene.IsValid()) SceneManager.SetActiveScene(runnerScene);
            foreach (var name in source.Created)
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
            RemoveStrays();
            PersistentBool.ResetStatics();
            SaveSlots.ResetForTests();
            SaveSystem.Root = savedRoot;
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            if (File.Exists(stubsPath)) File.Delete(stubsPath);
            Stubs.StubsDocPath = null;
            Stubs.ResetForTests();
        }

        enum RoomKind { First, BenchRoom, StartOnly, Bare }

        // Floor (top at y 0), one gate to another room, RoomInfo with bounds, then the markers of the kind.
        static void BuildRoom(Scene scene, string name, float x0, int zoneId, string otherRoom, RoomKind kind)
        {
            var floor = new GameObject("Floor");
            floor.AddComponent<BoxCollider2D>().size = new Vector2(60f, 1f);
            floor.transform.position = new Vector3(x0 + 30f, -0.5f, 0f);
            SceneManager.MoveGameObjectToScene(floor, scene);

            bool right = kind == RoomKind.First;
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

            if (kind == RoomKind.First || kind == RoomKind.StartOnly)
            {
                var startGo = new GameObject("StartSpot");
                startGo.transform.position = new Vector3(x0 + 10f, 1f, 0f);
                var m = startGo.AddComponent<RespawnMarker>();
                m.Configure(RespawnRules.StartMarkerName, true);
                m.SetBench(kind == RoomKind.First);
                SceneManager.MoveGameObjectToScene(startGo, scene);
            }
            if (kind == RoomKind.First)
            {
                var wallGo = new GameObject("Wall");
                wallGo.transform.position = new Vector3(x0 + 40f, 2f, 0f);
                wallGo.AddComponent<BoxCollider2D>().size = new Vector2(1f, 4f);
                wallGo.AddComponent<BreakableWall>().Configure(WallId);
                SceneManager.MoveGameObjectToScene(wallGo, scene);

                var pickGo = new GameObject("Pickup");
                pickGo.transform.position = new Vector3(x0 + 50f, 1f, 0f);
                var pc = pickGo.AddComponent<BoxCollider2D>();
                pc.isTrigger = true;
                pc.size = new Vector2(1f, 1f);
                pickGo.AddComponent<Pickup>().Configure(PickupId);
                SceneManager.MoveGameObjectToScene(pickGo, scene);
            }
            if (kind == RoomKind.BenchRoom)
            {
                var benchGo = new GameObject("BenchSpot");
                benchGo.transform.position = new Vector3(x0 + 30f, 1f, 0f);
                var area = benchGo.AddComponent<BoxCollider2D>();
                area.isTrigger = true;
                area.size = new Vector2(6f, 6f);
                benchGo.AddComponent<RespawnMarker>().Configure("Bench", true);
                benchGo.AddComponent<Bench>();
                SceneManager.MoveGameObjectToScene(benchGo, scene);
            }
        }

        // ---------- helpers ----------

        RoomWorldBootstrap Factory(PlayerData d)
        {
            input = new FakeHeroInput();
            var w = RoomWorldBootstrap.Create(config, input, settings, source, d);
            w.Hero.Health.DeathRespawnDelaySeconds = -1f;
            return w;
        }

        IEnumerator Begin(int slot, bool cont, GameStartResult result)
        {
            yield return GameSession.StartGame(slot, cont, Factory, result);
            world = result.World;
        }

        string SlotFile(int slot) => Path.Combine(tempRoot, "user" + slot + ".json");

        static void WriteSave(int slot, Action<PlayerData> edit)
        {
            var d = PlayerData.CreateNew();
            edit(d);
            Assert.IsTrue(SaveSystem.TrySave(d, slot));
        }

        IEnumerator Until(Func<bool> condition, string what, float timeout = 8f)
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

        static int Count<T>() where T : UnityEngine.Object =>
            UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        void AssertNoWorld()
        {
            Assert.IsNull(RoomWorldBootstrap.Instance);
            Assert.AreEqual(0, Count<RoomWorldBootstrap>());
            Assert.AreEqual(0, Count<HeroController>());
            Assert.AreEqual(0, Count<GameCamera>());
            Assert.AreEqual(0, Count<RoomTransitionManager>());
        }

        // ---------- new game ----------

        [UnityTest]
        public IEnumerator NewGame_WritesAFreshSlot_AndStartsInTut01OnTheStartMarker()
        {
            var result = new GameStartResult();
            yield return Begin(0, false, result);

            Assert.IsTrue(result.Done);
            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(FirstRoom, world.Loader.CurrentRoom.name);
            Assert.AreEqual(0, SaveSlots.CurrentSlot);
            Assert.AreEqual(0, world.Hero.Health.SaveSlot);
            Assert.AreEqual(10f, world.Hero.transform.position.x, 0.5f);
            Assert.AreEqual(5, world.PlayerData.health);
            Assert.AreEqual(FirstRoom, world.PlayerData.respawnScene);
            Assert.AreEqual(RespawnRules.StartMarkerName, world.PlayerData.respawnMarker, "the flagged Start marker is the bench");

            PlayerData saved;
            Assert.AreEqual(SaveSystem.SlotFileState.Valid, SaveSystem.Peek(0, out saved));
            Assert.AreEqual(5, saved.health);
            Assert.AreEqual(FirstRoom, saved.currentScene);
        }

        [UnityTest]
        public IEnumerator NewGame_OverAnExistingSave_StartsFresh()
        {
            WriteSave(2, d => { d.health = 1; d.respawnScene = nameB; d.respawnMarker = "Bench"; });
            var result = new GameStartResult();
            yield return Begin(2, false, result);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(FirstRoom, world.Loader.CurrentRoom.name);
            Assert.AreEqual(5, world.PlayerData.health);
        }

        // ---------- continue ----------

        [UnityTest]
        public IEnumerator Continue_PutsTheHeroOnTheSavedMarker_WithSavedHealth_AndSilkZero()
        {
            WriteSave(1, d => { d.respawnScene = nameB; d.respawnMarker = "Bench"; d.health = 3; d.silk = 7; });
            var result = new GameStartResult();
            yield return Begin(1, true, result);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(nameB, world.Loader.CurrentRoom.name);
            Assert.AreEqual(230f, world.Hero.transform.position.x, 0.5f, "on the bench");
            Assert.AreEqual(3, world.Hero.Health.Health);
            Assert.AreEqual(0, world.PlayerData.silk);
            Assert.AreEqual(1, SaveSlots.CurrentSlot);
            Assert.AreEqual(1, world.Hero.Health.SaveSlot);
            Assert.AreEqual(nameB, world.PlayerData.respawnScene);
            Assert.AreEqual("Bench", world.PlayerData.respawnMarker);
            Assert.AreSame(world.PlayerData, world.Hero.Health.Data, "one PlayerData");
            Assert.AreSame(world.PlayerData.sceneData, PersistentBool.CurrentSceneData(), "rooms read the same scene data");
            Assert.AreEqual(nameB, world.PlayerData.hazardRespawn.scene, "the hazard spot is the marker the hero stands on");
        }

        [UnityTest]
        public IEnumerator Continue_UsesTheBench_NotTheRoomRecordedInCurrentScene()
        {
            WriteSave(1, d => { d.respawnScene = nameB; d.respawnMarker = "Bench"; d.currentScene = FirstRoom; });
            var result = new GameStartResult();
            yield return Begin(1, true, result);
            Assert.AreEqual(nameB, world.Loader.CurrentRoom.name, "the bench wins over currentScene");
        }

        [UnityTest]
        public IEnumerator BenchRestInRoomB_ContinuesInRoomB_AtTheBench()
        {
            var result = new GameStartResult();
            yield return Begin(0, false, result);
            Assert.IsTrue(result.Success, result.Message);

            // Walk to room B through its gate, then rest at the bench.
            var gate = GateRegistry.Find("right1", world.Loader.CurrentRoom);
            world.Manager.RequestTransition(gate.BuildRequest());
            yield return Until(() => world.Loader.CurrentRoomName == nameB && !world.Manager.IsTransitionRunning && !world.Hero.ControlLocked, "room B");

            world.PlayerData.health = 2;
            world.PlaceHero(new Vector2(230f, 1f));
            yield return Until(() => world.Hero.IsGrounded && world.Hero.State == HeroState.Grounded, "landing");
            yield return Fixed(5);
            var bench = UnityEngine.Object.FindFirstObjectByType<Bench>();
            input.Move = new Vector2(0f, 1f);
            yield return Until(() => bench.RestCount == 1, "the rest");
            input.Move = Vector2.zero;

            // Wander off, then leave the game.
            world.PlaceHero(new Vector2(215f, 1f));
            yield return GameSession.Quit();
            world = null;
            AssertNoWorld();

            var again = new GameStartResult();
            yield return Begin(0, true, again);
            Assert.IsTrue(again.Success, again.Message);
            Assert.AreEqual(nameB, world.Loader.CurrentRoom.name);
            Assert.AreEqual(230f, world.Hero.transform.position.x, 0.5f, "back at the bench, not where the hero wandered to");
            Assert.AreEqual(world.PlayerData.maxHealth, world.Hero.Health.Health, "the rest healed before saving");
        }

        // ---------- rooms that remember, through the disk ----------

        [UnityTest]
        public IEnumerator BrokenWall_AndCollectedPickup_StayAfterQuitAndContinue()
        {
            var result = new GameStartResult();
            yield return Begin(0, false, result);
            yield return Fixed(2);

            var wall = UnityEngine.Object.FindFirstObjectByType<BreakableWall>();
            var pickup = UnityEngine.Object.FindFirstObjectByType<Pickup>();
            Assert.IsFalse(wall.IsBroken);
            Assert.IsFalse(pickup.IsCollected);
            wall.Break();
            pickup.Collect();

            yield return GameSession.Quit();
            world = null;
            AssertNoWorld();
            Assert.IsFalse(SceneManager.GetSceneByName(FirstRoom).isLoaded, "the room was unloaded");

            // The disk holds the flags.
            PlayerData onDisk;
            Assert.AreEqual(SaveSystem.SlotFileState.Valid, SaveSystem.Peek(0, out onDisk));
            Assert.IsTrue(onDisk.sceneData.GetBool(FirstRoom, WallId));
            Assert.IsTrue(onDisk.sceneData.GetBool(FirstRoom, PickupId));

            var again = new GameStartResult();
            yield return Begin(0, true, again);
            Assert.IsTrue(again.Success, again.Message);
            yield return Fixed(2);

            wall = UnityEngine.Object.FindFirstObjectByType<BreakableWall>();
            pickup = UnityEngine.Object.FindFirstObjectByType<Pickup>();
            Assert.IsTrue(wall.IsBroken, "the wall is still broken");
            Assert.IsFalse(wall.GetComponent<Collider2D>().enabled);
            Assert.IsTrue(pickup.IsCollected, "the pickup is still collected");
        }

        // ---------- missing marker ----------

        [UnityTest]
        public IEnumerator MissingMarker_FallsBackToTheRoomsBench_WithAWarning()
        {
            WriteSave(1, d => { d.respawnScene = nameB; d.respawnMarker = "Gone"; });
            LogAssert.Expect(LogType.Warning, new Regex("Marker 'Gone' was not found"));
            var result = new GameStartResult();
            yield return Begin(1, true, result);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(230f, world.Hero.transform.position.x, 0.5f, "on the room's bench");
            Assert.AreEqual("Bench", world.PlayerData.respawnMarker, "the fallback bench is now the respawn");
        }

        [UnityTest]
        public IEnumerator MissingMarker_NoBench_FallsBackToTheStartMarker()
        {
            WriteSave(1, d => { d.respawnScene = nameC; d.respawnMarker = "Gone"; });
            LogAssert.Expect(LogType.Warning, new Regex("Marker 'Gone' was not found.*Start"));
            var result = new GameStartResult();
            yield return Begin(1, true, result);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(410f, world.Hero.transform.position.x, 0.5f);
        }

        [UnityTest]
        public IEnumerator MissingMarker_NothingAtAll_FallsBackToTheFirstGate()
        {
            WriteSave(1, d => { d.respawnScene = nameD; d.respawnMarker = "Gone"; });
            LogAssert.Expect(LogType.Warning, new Regex("Marker 'Gone' was not found.*no bench or Start"));
            LogAssert.Expect(LogType.Warning, new Regex("has no respawn marker"));
            var result = new GameStartResult();
            yield return Begin(1, true, result);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(nameD, world.Loader.CurrentRoom.name);
            Assert.AreEqual(601f, world.Hero.transform.position.x, 1f, "at the first gate");
        }

        // ---------- refused slots ----------

        [UnityTest]
        public IEnumerator CorruptSlot_CannotBeStartedOrContinued_AndNothingIsCreated()
        {
            Directory.CreateDirectory(tempRoot);
            File.WriteAllText(SlotFile(0), "{ this is not json");
            string before = File.ReadAllText(SlotFile(0));

            var newGame = new GameStartResult();
            yield return Begin(0, false, newGame);
            Assert.IsTrue(newGame.Done);
            Assert.IsFalse(newGame.Success);
            Assert.IsNull(newGame.World);
            AssertNoWorld();
            Assert.AreEqual(-1, SaveSlots.CurrentSlot);

            var cont = new GameStartResult();
            yield return Begin(0, true, cont);
            Assert.IsFalse(cont.Success);
            AssertNoWorld();
            Assert.AreEqual(-1, SaveSlots.CurrentSlot);
            Assert.AreEqual(before, File.ReadAllText(SlotFile(0)), "the file is untouched");
        }

        [UnityTest]
        public IEnumerator NewerVersionSlot_CannotBeStartedOrContinued()
        {
            Directory.CreateDirectory(tempRoot);
            var newer = PlayerData.CreateNew();
            newer.version = PlayerData.CurrentVersion + 1;
            File.WriteAllText(SlotFile(3), JsonUtility.ToJson(newer));
            string before = File.ReadAllText(SlotFile(3));

            var a = new GameStartResult();
            yield return Begin(3, false, a);
            var b = new GameStartResult();
            yield return Begin(3, true, b);

            Assert.IsFalse(a.Success);
            Assert.IsFalse(b.Success);
            AssertNoWorld();
            Assert.AreEqual(-1, SaveSlots.CurrentSlot);
            Assert.AreEqual(before, File.ReadAllText(SlotFile(3)));
        }

        [UnityTest]
        public IEnumerator ContinueOnAnEmptySlot_Fails()
        {
            var result = new GameStartResult();
            yield return Begin(1, true, result);
            Assert.IsFalse(result.Success);
            AssertNoWorld();
            Assert.IsFalse(File.Exists(SlotFile(1)));
        }

        [UnityTest]
        public IEnumerator StartingWhileAGameRuns_IsRefused()
        {
            var first = new GameStartResult();
            yield return Begin(0, false, first);
            var hero = world.Hero;

            var second = new GameStartResult();
            yield return GameSession.StartGame(1, false, Factory, second);
            Assert.IsFalse(second.Success);
            Assert.AreSame(hero, RoomWorldBootstrap.Instance.Hero);
            Assert.AreEqual(1, Count<HeroController>());
            Assert.AreEqual(0, SaveSlots.CurrentSlot, "still the first slot");
            Assert.IsFalse(File.Exists(SlotFile(1)));
        }

        // ---------- quitting ----------

        [UnityTest]
        public IEnumerator TwoSessions_LeaveExactlyOneWorld_AndNothingBehind()
        {
            var a = new GameStartResult();
            yield return Begin(0, false, a);
            Assert.IsTrue(a.Success, a.Message);
            yield return GameSession.Quit();
            world = null;
            AssertNoWorld();
            Assert.AreEqual(-1, SaveSlots.CurrentSlot);
            Assert.AreEqual(sceneCountAtStart, SceneManager.sceneCount, "no room scene left loaded");

            var b = new GameStartResult();
            yield return Begin(1, false, b);
            Assert.IsTrue(b.Success, b.Message);
            Assert.AreEqual(1, SaveSlots.CurrentSlot);
            Assert.AreEqual(1, Count<RoomWorldBootstrap>());
            Assert.AreEqual(1, Count<HeroController>());
            Assert.AreEqual(1, Count<GameCamera>());
            Assert.AreEqual(1, Count<RoomTransitionManager>());
            Assert.AreEqual(1, Count<ScreenFader>());

            yield return GameSession.Quit();
            world = null;
            AssertNoWorld();
            Assert.AreEqual(sceneCountAtStart, SceneManager.sceneCount);
            Assert.IsTrue(File.Exists(SlotFile(0)));
            Assert.IsTrue(File.Exists(SlotFile(1)));
        }

        [UnityTest]
        public IEnumerator Quit_WithNoGame_IsHarmless()
        {
            yield return GameSession.Quit();
            AssertNoWorld();
            Assert.AreEqual(-1, SaveSlots.CurrentSlot);
        }

        [UnityTest]
        public IEnumerator Quit_WhileDead_DoesNotWriteAZeroHealthSave()
        {
            var result = new GameStartResult();
            yield return Begin(0, false, result);
            world.Hero.Health.TakeHit(new HitInstance { Damage = 99, Direction = AttackDirection.Normal, Source = world.gameObject });
            Assert.IsTrue(world.Hero.Health.IsDead);

            yield return GameSession.Quit();
            world = null;

            PlayerData saved;
            Assert.AreEqual(SaveSystem.SlotFileState.Valid, SaveSystem.Peek(0, out saved));
            Assert.AreEqual(5, saved.health);
        }

        // ---------- respawns in a loaded game ----------

        [UnityTest]
        public IEnumerator DeathInAnotherRoom_RespawnsAtTheLoadedBench_AndTheSlotNeverHoldsZeroHealth()
        {
            WriteSave(1, d => { d.respawnScene = nameB; d.respawnMarker = "Bench"; d.health = 2; });
            var result = new GameStartResult();
            yield return Begin(1, true, result);
            Assert.IsTrue(result.Success, result.Message);
            world.Hero.Health.DeathRespawnDelaySeconds = 0.2f;

            // Go to the first room, away from the bench.
            var gate = GateRegistry.Find("left1", world.Loader.CurrentRoom);
            world.Manager.RequestTransition(gate.BuildRequest());
            yield return Until(() => world.Loader.CurrentRoomName == FirstRoom && !world.Manager.IsTransitionRunning && !world.Hero.ControlLocked, "the first room");
            yield return Fixed(3);

            world.Hero.Health.TakeHit(new HitInstance { Damage = 99, Direction = AttackDirection.Normal, Source = world.gameObject });
            Assert.IsTrue(world.Hero.Health.IsDead);

            float start = Time.unscaledTime;
            while (world.Hero.Health.IsDead || world.Loader.CurrentRoomName != nameB || world.Manager.IsTransitionRunning)
            {
                if (Time.unscaledTime - start > 10f) Assert.Fail("Timed out waiting for the respawn");
                PlayerData snapshot;
                if (SaveSystem.Peek(1, out snapshot) == SaveSystem.SlotFileState.Valid)
                    Assert.Greater(snapshot.health, 0, "a slot never holds health 0");
                yield return null;
            }
            yield return Fixed(3);

            Assert.AreEqual(nameB, world.Loader.CurrentRoom.name);
            Assert.AreEqual(230f, world.Hero.transform.position.x, 1f, "at the bench");
            Assert.AreEqual(world.PlayerData.maxHealth, world.Hero.Health.Health);
            PlayerData saved;
            Assert.AreEqual(SaveSystem.SlotFileState.Valid, SaveSystem.Peek(1, out saved));
            Assert.AreEqual(saved.maxHealth, saved.health, "the respawn saved to the selected slot, after the refill");
            Assert.AreEqual(nameB, saved.respawnScene);
        }

        [UnityTest]
        public IEnumerator DirectRoomPlay_NeverWritesASave()
        {
            // The world a room in the editor creates: no GameSession, no slot.
            input = new FakeHeroInput();
            world = RoomWorldBootstrap.Create(config, input, settings, source);
            world.Hero.Health.DeathRespawnDelaySeconds = -1f;
            yield return world.StartInRoom(nameB, "left1");

            Assert.AreEqual(-1, world.Hero.Health.SaveSlot);
            Assert.AreEqual(-1, SaveSlots.CurrentSlot);
            world.Hero.Health.RespawnAt(RespawnMarker.Find("Bench", world.Loader.CurrentRoom));
            Assert.IsFalse(Directory.Exists(tempRoot) && Directory.GetFiles(tempRoot).Length > 0, "no file written");
        }
    }
}
