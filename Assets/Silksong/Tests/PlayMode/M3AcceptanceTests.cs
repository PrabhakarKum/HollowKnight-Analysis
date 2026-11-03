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
    // Milestone 3 task 7: saving and loading on the REAL generated rooms (Tut_01, Tut_01b, Tut_02), with the real
    // GameSession, SaveSlots and StartScreen, real fade timings, a FakeHeroInput, and a temp save folder
    // (SaveSystem.Root is restored in teardown). Style and teardown follow M2AcceptanceTests.
    //
    // The tests never build the rooms. Build them first with Silksong/Build Transition Rooms or
    // -executeMethod Hollowknight.Silksong.Editor.TransitionRoomsBuilder.BuildBatch; a missing or outdated scene
    // fails at once with that instruction.
    //
    // Game tests start through GameSession.StartGame with a factory that builds the bootstrap with the fake input
    // (RoomWorldBootstrap.Create + RoomWorldFactory.MakeSource, so rooms load from the generated assets). The
    // direct-play test opens Tut_01 itself, like pressing Play on the room, and so goes through RoomWorldStarter.
    //
    // Not covered (manual, see docs/study/M3-checklist.md): how the start screen looks and reads, Space or Enter
    // reaching the hero too, the one-frame flash of a broken wall, whether the nail really reaches the wall on the
    // Tut_01 shelf (the wall is hit through IHittable.TakeHit here, not with a swing), the feel of the bench fade.
    public class M3AcceptanceTests
    {
        const string RoomsFolder = "Assets/Scenes/Rooms";
        static readonly string[] Rooms = { "Tut_01", "Tut_01b", "Tut_02" };
        const string BuildHint = "Run the menu Silksong/Build Transition Rooms (after Silksong/Import Hero Animations for the hero art), or -executeMethod Hollowknight.Silksong.Editor.TransitionRoomsBuilder.BuildBatch before -runTests.";

        // The explicit save ids the builder gives the Tut_01 wall and pickup (TransitionRoomsBuilder.Tut01WallId and
        // Tut01PickupId; the builder is in the Editor assembly, which tests do not reference).
        const string WallId = "Tut_01_BreakableWall_1";
        const string PickupId = "Tut_01_Pickup_1";

        static string PathOf(string room) => RoomsFolder + "/" + room + ".unity";

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

        string tempRoot, savedRoot, stubsPath;
        HeroConfig config;
        RoomWorldBootstrap world;
        FakeHeroInput input;
        GameStartResult lastResult;
        StartScreen screen;
        int factoryCalls;

        [SetUp]
        public void SetUp()
        {
            RequireScenes();
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            PersistentBool.ResetStatics();
            stubsPath = Path.Combine(Path.GetTempPath(), "m3-accept-stubs-" + Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
            savedRoot = SaveSystem.Root;
            tempRoot = Path.Combine(Path.GetTempPath(), "m3-accept-saves-" + Guid.NewGuid().ToString("N"));
            SaveSystem.Root = tempRoot;
            SaveSlots.ResetForTests();
            factoryCalls = 0;
            RemoveStrays();
#if UNITY_EDITOR
            config = HeroConfigConsistency.LoadProjectAsset();
#endif
        }

        static void RemoveStrays()
        {
            // Strays from earlier fixtures would give two worlds.
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
            foreach (var s in UnityEngine.Object.FindObjectsByType<StartScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s != null) UnityEngine.Object.DestroyImmediate(s.gameObject);
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Whatever the test left running goes through the same exit the game uses (no save).
            if (RoomWorldBootstrap.Instance != null) yield return GameSession.Quit(false);
            if (screen != null) UnityEngine.Object.Destroy(screen.gameObject);
            screen = null;
            if (world != null) UnityEngine.Object.Destroy(world.gameObject);
            world = null;
            yield return null;
            // Leave nothing behind: an empty active scene, every room and the host scene unloaded, no stray world objects.
            // Reuse the empty scene a previous test left behind: CreateScene throws if the name is already taken.
            var empty = SceneManager.GetSceneByName("M3AcceptanceEmpty");
            if (!empty.IsValid() || !empty.isLoaded) empty = SceneManager.CreateScene("M3AcceptanceEmpty");
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s == empty || !s.isLoaded) continue;
                yield return SceneManager.UnloadSceneAsync(s);
            }
            foreach (var b in UnityEngine.Object.FindObjectsByType<RoomWorldBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b != null) UnityEngine.Object.Destroy(b.gameObject);
            foreach (var h in UnityEngine.Object.FindObjectsByType<HeroController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (h != null) UnityEngine.Object.Destroy(h.gameObject);
            foreach (var c in UnityEngine.Object.FindObjectsByType<GameCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c != null) UnityEngine.Object.Destroy(c.gameObject);
            foreach (var s in UnityEngine.Object.FindObjectsByType<StartScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s != null) UnityEngine.Object.Destroy(s.gameObject);
            yield return null;
            yield return null;
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            PersistentBool.ResetStatics();
            SaveSlots.ResetForTests();
            SaveSystem.Root = savedRoot;
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            if (!string.IsNullOrEmpty(stubsPath) && File.Exists(stubsPath)) File.Delete(stubsPath);
            Stubs.StubsDocPath = null;
            Stubs.ResetForTests();
        }

        // ---------- helpers ----------

        IEnumerator Until(Func<bool> condition, string what, float timeout = 20f)
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

        static float RootAboveFeet => SilksongUnits.HeroColliderSize.y * 0.5f - SilksongUnits.HeroColliderOffset.y;

        string SlotFile(int slot) => Path.Combine(tempRoot, "user" + slot + ".json");

        PlayerData OnDisk(int slot)
        {
            PlayerData d;
            Assert.AreEqual(SaveSystem.SlotFileState.Valid, SaveSystem.Peek(slot, out d), "slot " + slot + " must hold a readable save");
            return d;
        }

        // The world a StartGame or the start screen asks for: real settings (real fade timings), the generated rooms,
        // the fake input. The death delay is short so a death test does not wait 1.6 s.
        RoomWorldBootstrap Factory(PlayerData d)
        {
            factoryCalls++;
            input = new FakeHeroInput();
            var w = RoomWorldBootstrap.Create(config, input, null, RoomWorldFactory.MakeSource(), d);
            w.Hero.Health.DeathRespawnDelaySeconds = 0.05f;
            return w;
        }

        // Starts a game through GameSession and waits for the hero to stand (when it started).
        IEnumerator Begin(int slot, bool cont)
        {
            lastResult = new GameStartResult();
            yield return GameSession.StartGame(slot, cont, Factory, lastResult);
            Assert.IsTrue(lastResult.Done);
            world = lastResult.World;
            if (lastResult.Success) yield return Settle();
        }

        IEnumerator Settle()
        {
            yield return Until(() => world.Hero.IsGrounded && !world.Hero.ControlLocked && !world.Manager.IsTransitionRunning, "the hero to stand on the floor");
            yield return Fixed(3);
        }

        IEnumerator Quit()
        {
            yield return GameSession.Quit();
            world = null;
            Assert.IsNull(RoomWorldBootstrap.Instance);
            Assert.AreEqual(0, Count<HeroController>(), "no hero after quitting");
            Assert.AreEqual(0, Count<GameCamera>());
            Assert.AreEqual(-1, SaveSlots.CurrentSlot);
        }

        int LoadedRooms()
        {
            int loaded = 0;
            foreach (var name in Rooms)
            {
                var s = SceneManager.GetSceneByName(name);
                if (s.IsValid() && s.isLoaded) loaded++;
            }
            return loaded;
        }

        void AssertOneOfEverything(string room, string what)
        {
            Assert.AreEqual(1, Count<RoomWorldBootstrap>(), "worlds " + what);
            Assert.AreEqual(1, Count<HeroController>(), "heroes " + what);
            Assert.AreEqual(1, Count<GameCamera>(), "cameras " + what);
            Assert.AreEqual(1, Count<RoomTransitionManager>(), "managers " + what);
            Assert.AreEqual(1, Count<ScreenFader>(), "faders " + what);
            Assert.AreEqual(1, LoadedRooms(), "loaded rooms " + what);
            Assert.AreEqual(room, world.Loader.CurrentRoomName, "room " + what);
            Assert.AreSame(world, RoomWorldBootstrap.Instance, "world " + what);
        }

        Bench FindBench() => UnityEngine.Object.FindFirstObjectByType<Bench>();
        BreakableWall FindWall() => UnityEngine.Object.FindFirstObjectByType<BreakableWall>();
        Pickup FindPickup() => UnityEngine.Object.FindFirstObjectByType<Pickup>();

        void AssertWall(bool broken, string what)
        {
            var wall = FindWall();
            Assert.IsNotNull(wall, "the Tut_01 wall exists " + what);
            Assert.AreEqual(broken, wall.IsBroken, "wall broken " + what);
            Assert.AreEqual(!broken, wall.GetComponent<Collider2D>().enabled, "wall collider " + what);
            Assert.AreEqual(!broken, wall.GetComponent<Renderer>().enabled, "wall renderer " + what);
        }

        void AssertPickup(bool collected, string what)
        {
            var pickup = FindPickup();
            Assert.IsNotNull(pickup, "the Tut_01 pickup exists " + what);
            Assert.AreEqual(collected, pickup.IsCollected, "pickup collected " + what);
            Assert.AreEqual(!collected, pickup.GetComponent<Collider2D>().enabled, "pickup collider " + what);
        }

        // The wall takes the nail through its public IHittable path.
        void HitWall()
        {
            var wall = FindWall();
            Assert.IsFalse(wall.IsBroken);
            IHittable target = wall;
            Assert.IsTrue(target.TakeHit(new HitInstance { Damage = 1, Direction = AttackDirection.Normal }), "the first hit lands");
            Assert.IsTrue(wall.IsBroken);
        }

        // Stands the hero on the floor under the pickup; the real trigger collects it. The spot comes from the real
        // object (its centre is half a unit above the floor it stands on).
        IEnumerator CollectPickupByTouching()
        {
            var pickup = FindPickup();
            Assert.IsFalse(pickup.IsCollected);
            var p = pickup.transform.position;
            input.Move = Vector2.zero;
            world.PlaceHero(new Vector2(p.x, p.y - 0.5f + RootAboveFeet + 0.05f));
            yield return Until(() => pickup.IsCollected, "the hero to collect the pickup by touching it", 5f);
        }

        TransitionPoint Gate(string room, string gateName)
        {
            var gate = GateRegistry.Find(gateName, SceneManager.GetSceneByName(room));
            Assert.AreEqual(gateName, gate.name, room + " has no gate " + gateName);
            return gate;
        }

        // Stands the hero on the gate's floor a few units inside the room, then waits until it is grounded and still.
        IEnumerator PlaceInsideGate(string room, string gateName)
        {
            var gate = Gate(room, gateName);
            var area = RoomWorldBootstrap.FindRoomInfo(SceneManager.GetSceneByName(room)).Bounds.Area;
            float x = gate.Position == GatePosition.Left ? area.xMin + 4f : area.xMax - 4f;
            float feet = gate.GetComponent<Collider2D>().bounds.min.y;
            input.Move = Vector2.zero;
            world.PlaceHero(new Vector2(x, feet + RootAboveFeet + 0.05f));
            yield return Until(() => world.Hero.IsGrounded && !world.Hero.ControlLocked, "the hero to stand inside " + room + "/" + gateName);
        }

        // Holds the move key toward the gate, waits for the change to start and finish. The test never sends a request.
        IEnumerator WalkOutAndWait(string room, string gateName)
        {
            var gate = Gate(room, gateName);
            var manager = world.Manager;
            var hero = world.Hero;
            int started = 0, finished = 0;
            Action<TransitionRequest> onStarted = r => started++;
            Action<bool> onFinished = ok => { if (ok) finished++; };
            manager.TransitionStarted += onStarted;
            manager.TransitionFinished += onFinished;
            try
            {
                input.Move = new Vector2(gate.Position == GatePosition.Left ? -1f : 1f, 0f);
                yield return Until(() => started > 0, "walking into " + room + "/" + gateName + " to start the change", 15f);
                input.Move = Vector2.zero;
                yield return Until(() => finished > 0 && !manager.IsTransitionRunning && manager.State == GameState.Playing && !hero.ControlLocked,
                    "the change " + room + "/" + gateName + " to finish");
            }
            finally
            {
                manager.TransitionStarted -= onStarted;
                manager.TransitionFinished -= onFinished;
            }
        }

        IEnumerator GoThroughGate(string room, string gateName, string expectedTarget)
        {
            Assert.AreEqual(room, world.Loader.CurrentRoomName);
            yield return PlaceInsideGate(room, gateName);
            yield return WalkOutAndWait(room, gateName);
            Assert.AreEqual(expectedTarget, world.Loader.CurrentRoomName);
            yield return Settle();
        }

        // Stands the hero in the Tut_01 bench trigger (at the Start marker), still, then presses up until it rests.
        IEnumerator RestAtTut01Bench()
        {
            Assert.AreEqual("Tut_01", world.Loader.CurrentRoomName);
            var bench = FindBench();
            Assert.IsNotNull(bench, "Tut_01 has its bench");
            int before = bench.RestCount;
            input.Move = Vector2.zero;
            var m = bench.transform.position;   // the Start marker: root a hair above the floor
            world.PlaceHero(new Vector2(m.x, m.y + 0.04f));
            yield return Until(() => world.Hero.IsGrounded && !world.Hero.ControlLocked, "the hero to stand at the bench");
            yield return Fixed(6);
            input.Move = new Vector2(0f, 1f);
            yield return Until(() => bench.RestCount == before + 1, "the rest to finish", 15f);
            input.Move = Vector2.zero;
            yield return Until(() => !world.Hero.ControlLocked, "control after the rest");
        }

        // Hits the hero for one (waiting out any invulnerability), then waits until it is free and standing.
        IEnumerator DamageHero()
        {
            var h = world.Hero.Health;
            int before = h.Health;
            yield return Until(() => h.TakeHit(new HitInstance { Damage = 1, Direction = AttackDirection.Normal }), "the hero to be hittable", 10f);
            Assert.AreEqual(before - 1, h.Health);
            yield return Until(() => !h.IsStunned && world.Hero.IsGrounded && !world.Hero.ControlLocked, "the hero to recover from the hit");
        }

        // Kills the hero in the current room and waits for the death respawn, watching the disk every frame: the slot
        // may never hold health 0.
        IEnumerator DieAndWaitForRespawn(int slot)
        {
            var h = world.Hero.Health;
            yield return Until(() => h.TakeHit(new HitInstance { Damage = h.Health, Direction = AttackDirection.Normal }), "the hero to be hittable", 10f);
            Assert.IsTrue(h.IsDead);
            float start = Time.unscaledTime;
            while (h.IsDead || world.Manager.IsTransitionRunning || world.Manager.State != GameState.Playing || world.Hero.ControlLocked)
            {
                if (Time.unscaledTime - start > 20f) Assert.Fail("Timed out waiting for the death respawn");
                PlayerData snapshot;
                if (SaveSystem.Peek(slot, out snapshot) == SaveSystem.SlotFileState.Valid)
                    Assert.Greater(snapshot.health, 0, "a slot never holds health 0");
                yield return null;
            }
            yield return Fixed(3);
        }

        // ---------- 1. new game ----------

        [UnityTest]
        public IEnumerator NewGame_Slot0_StartsInTut01_AndWritesTheSlot()
        {
            yield return Begin(0, false);
            Assert.IsTrue(lastResult.Success, lastResult.Message);

            AssertOneOfEverything("Tut_01", "after a new game");
            Assert.AreEqual(0, SaveSlots.CurrentSlot);
            Assert.AreEqual(0, world.Hero.Health.SaveSlot);
            Assert.AreEqual(world.Hero.Health.MaxHealth, world.Hero.Health.Health, "full health");
            Assert.AreEqual("TUT_01", world.PlayerData.mapZone);
            Assert.AreEqual("Tut_01", world.PlayerData.respawnScene);
            Assert.AreEqual(RespawnRules.StartMarkerName, world.PlayerData.respawnMarker, "the bench is Tut_01's Start marker");
            Assert.IsNotNull(FindBench(), "Tut_01 has a Bench");
            Assert.AreEqual(FindBench().transform.position.x, world.Hero.transform.position.x, 0.6f, "the hero stands on the bench");
            AssertWall(false, "at the start");
            AssertPickup(false, "at the start");

            var saved = OnDisk(0);
            Assert.AreEqual(saved.maxHealth, saved.health);
            Assert.AreEqual("TUT_01", saved.mapZone);
            Assert.AreEqual("Tut_01", saved.respawnScene);
            Assert.AreEqual(RespawnRules.StartMarkerName, saved.respawnMarker);
            Assert.AreEqual("Tut_01", saved.currentScene);
            Assert.IsFalse(saved.sceneData.GetBool("Tut_01", WallId), "nothing is broken yet");
            Assert.IsFalse(File.Exists(SlotFile(1)), "the other slots are untouched");
        }

        // ---------- 2. the full round trip ----------

        [UnityTest]
        public IEnumerator FullRoundTrip_BrokenWall_Pickup_Bench_Quit_Continue()
        {
            yield return Begin(0, false);
            Assert.IsTrue(lastResult.Success, lastResult.Message);
            int maxHealth = world.Hero.Health.MaxHealth;

            // Tut_01: break the wall, collect the pickup by touching it.
            HitWall();
            yield return CollectPickupByTouching();
            AssertWall(true, "after the hit");
            AssertPickup(true, "after the touch");
            Assert.IsTrue(world.PlayerData.sceneData.GetBool("Tut_01", WallId), "written into the world's data at once");
            Assert.IsTrue(world.PlayerData.sceneData.GetBool("Tut_01", PickupId));

            // Through the real gate to Tut_01b, get hurt, come back.
            yield return GoThroughGate("Tut_01", "right1", "Tut_01b");
            yield return DamageHero();
            Assert.AreEqual(maxHealth - 1, world.Hero.Health.Health);
            yield return GoThroughGate("Tut_01b", "left1", "Tut_01");
            Assert.AreEqual(maxHealth - 1, world.Hero.Health.Health, "no healing on the way");

            // Rest at the real bench: full health, saved.
            world.PlayerData.silk = 3;   // the load rule must bring it back as 0
            var bench = FindBench();
            yield return RestAtTut01Bench();
            Assert.AreEqual(maxHealth, world.Hero.Health.Health, "the rest healed");
            Assert.AreEqual(1, bench.SavesWritten, "the rest wrote the slot");
            Assert.AreEqual("Tut_01", world.PlayerData.respawnScene);

            // Wander off into Tut_01b, then leave the game from there.
            yield return GoThroughGate("Tut_01", "right1", "Tut_01b");
            yield return Quit();
            Assert.AreEqual(0, LoadedRooms(), "the rooms were unloaded");

            var saved = OnDisk(0);
            Assert.AreEqual("Tut_01", saved.respawnScene);
            Assert.AreEqual(RespawnRules.StartMarkerName, saved.respawnMarker);
            Assert.AreEqual(maxHealth, saved.health, "full health on disk");
            Assert.AreEqual(3, saved.silk, "silk is stored as it was; only loading resets it");
            Assert.IsTrue(saved.sceneData.GetBool("Tut_01", WallId), "broken wall on disk");
            Assert.IsTrue(saved.sceneData.GetBool("Tut_01", PickupId), "collected pickup on disk");

            // Continue.
            yield return Begin(0, true);
            Assert.IsTrue(lastResult.Success, lastResult.Message);
            AssertOneOfEverything("Tut_01", "after Continue");
            Assert.AreEqual(FindBench().transform.position.x, world.Hero.transform.position.x, 0.6f, "the hero stands at the bench, not where it left");
            Assert.AreEqual(maxHealth, world.Hero.Health.Health);
            Assert.AreEqual(0, world.PlayerData.silk, "silk is 0 after a load");
            Assert.AreEqual("Tut_01", world.PlayerData.respawnScene);
            Assert.AreSame(world.PlayerData, world.Hero.Health.Data, "one PlayerData");
            AssertWall(true, "after Continue");
            AssertPickup(true, "after Continue");
            Assert.IsFalse(world.Hero.ControlLocked);
        }

        // ---------- 3. two slots ----------

        [UnityTest]
        public IEnumerator TwoSlots_AreIndependent()
        {
            yield return Begin(0, false);
            Assert.IsTrue(lastResult.Success, lastResult.Message);
            HitWall();
            yield return Quit();
            Assert.IsTrue(OnDisk(0).sceneData.GetBool("Tut_01", WallId));

            yield return Begin(1, false);
            Assert.IsTrue(lastResult.Success, lastResult.Message);
            AssertOneOfEverything("Tut_01", "in slot 1");
            Assert.AreEqual(1, SaveSlots.CurrentSlot);
            AssertWall(false, "in the new slot 1");
            Assert.IsFalse(OnDisk(1).sceneData.GetBool("Tut_01", WallId));
            yield return Quit();

            yield return Begin(0, true);
            Assert.IsTrue(lastResult.Success, lastResult.Message);
            AssertWall(true, "slot 0 continued again");
            Assert.AreEqual(0, SaveSlots.CurrentSlot);
            Assert.IsFalse(OnDisk(1).sceneData.GetBool("Tut_01", WallId), "slot 1 is still its own");
        }

        // ---------- 4. death in Tut_01b ----------

        [UnityTest]
        public IEnumerator DeathInTut01b_AfterResting_ReturnsToTheBench_AndContinueLandsThere()
        {
            yield return Begin(0, false);
            Assert.IsTrue(lastResult.Success, lastResult.Message);
            int maxHealth = world.Hero.Health.MaxHealth;
            int heroId = world.Hero.GetInstanceID();

            yield return RestAtTut01Bench();
            yield return GoThroughGate("Tut_01", "right1", "Tut_01b");
            yield return DieAndWaitForRespawn(0);

            AssertOneOfEverything("Tut_01", "after the death");
            Assert.AreEqual(heroId, world.Hero.GetInstanceID(), "the same hero");
            Assert.IsFalse(world.Hero.Health.IsDead);
            Assert.AreEqual(maxHealth, world.Hero.Health.Health, "full health at the bench");
            Assert.AreEqual(FindBench().transform.position.x, world.Hero.transform.position.x, 2f, "at the Tut_01 bench");
            var afterDeath = OnDisk(0);
            Assert.AreEqual(maxHealth, afterDeath.health, "the respawn saved after the refill");
            Assert.AreEqual("Tut_01", afterDeath.respawnScene);

            yield return Quit();
            var saved = OnDisk(0);
            Assert.Greater(saved.health, 0);
            Assert.AreEqual("Tut_01", saved.respawnScene);

            yield return Begin(0, true);
            Assert.IsTrue(lastResult.Success, lastResult.Message);
            AssertOneOfEverything("Tut_01", "after Continue");
            Assert.AreEqual(FindBench().transform.position.x, world.Hero.transform.position.x, 0.6f);
            Assert.AreEqual(maxHealth, world.Hero.Health.Health);
        }

        // ---------- 5 and 6. slots that cannot be understood ----------

        // Writes bytes into slot 2, then checks the whole refusal story: summary, StartNew, Load, GameSession either
        // way, bytes unchanged, delete moves the file aside, the slot is Empty, a new game works.
        IEnumerator RefusedSlotStory(byte[] bytes, SlotState expected)
        {
            Directory.CreateDirectory(tempRoot);
            File.WriteAllBytes(SlotFile(2), bytes);

            Assert.AreEqual(expected, SaveSlots.Summary(2).State);
            Assert.IsFalse(SaveSlots.StartNew(2).Success, "StartNew refuses");
            Assert.IsFalse(SaveSlots.Load(2).Success, "Load refuses");
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(SlotFile(2)), "unchanged after the slot calls");

            yield return Begin(2, false);
            Assert.IsFalse(lastResult.Success);
            Assert.IsNull(RoomWorldBootstrap.Instance);
            yield return Begin(2, true);
            Assert.IsFalse(lastResult.Success);
            Assert.IsNull(RoomWorldBootstrap.Instance);
            Assert.AreEqual(0, factoryCalls, "no world was ever built");
            Assert.AreEqual(-1, SaveSlots.CurrentSlot);
            Assert.AreEqual(0, Count<HeroController>());
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(SlotFile(2)), "unchanged after the game starts");

            // Delete moves it aside (the bytes are kept in another file) and the slot is empty.
            Assert.IsTrue(SaveSlots.Delete(2).Success);
            Assert.IsFalse(File.Exists(SlotFile(2)));
            bool keptAside = false;
            foreach (var f in Directory.GetFiles(tempRoot))
                if (Path.GetFileName(f).StartsWith("user2", StringComparison.Ordinal) && !f.EndsWith(".tmp", StringComparison.Ordinal))
                {
                    var kept = File.ReadAllBytes(f);
                    if (kept.Length == bytes.Length) { CollectionAssert.AreEqual(bytes, kept); keptAside = true; }
                }
            Assert.IsTrue(keptAside, "the old file is kept beside the slot under another name");
            Assert.AreEqual(SlotState.Empty, SaveSlots.Summary(2).State);

            // A new game now works in the slot.
            yield return Begin(2, false);
            Assert.IsTrue(lastResult.Success, lastResult.Message);
            AssertOneOfEverything("Tut_01", "in the cleaned slot");
            Assert.AreEqual(2, SaveSlots.CurrentSlot);
            Assert.AreEqual(SlotState.Ok, SaveSlots.Summary(2).State);
            Assert.AreEqual("Tut_01", OnDisk(2).respawnScene);
        }

        [UnityTest]
        public IEnumerator CorruptSlot_IsNotOverwritten()
        {
            yield return RefusedSlotStory(System.Text.Encoding.UTF8.GetBytes("{ this is not json at all"), SlotState.Corrupt);
        }

        [UnityTest]
        public IEnumerator NewerVersionSlot_IsRefused()
        {
            var newer = PlayerData.CreateNew();
            newer.version = PlayerData.CurrentVersion + 1;
            yield return RefusedSlotStory(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(newer)), SlotState.NewerVersion);
        }

        // ---------- 7. the same round trip through the start screen ----------

        void SelectSlot(int slot)
        {
            for (int i = 0; i < SaveSlots.SlotCount && screen.Model.Selected != slot; i++) screen.MoveDown();
            Assert.AreEqual(slot, screen.Model.Selected);
        }

        IEnumerator WaitScreenIdle()
        {
            yield return null;
            yield return Until(() => !screen.IsBusy, "the screen to finish");
        }

        [UnityTest]
        public IEnumerator StartScreen_DrivesTheRoundTrip_Continue_SecondSlot_AndDelete()
        {
            var go = new GameObject("StartScreen");
            screen = go.AddComponent<StartScreen>();
            screen.WorldFactoryOverride = Factory;
            yield return null;
            Assert.IsTrue(screen.IsScreenVisible);
            Assert.AreEqual(4, screen.Model.Rows.Count);

            // Slot 1 (index 0), empty: Submit starts a new game.
            SelectSlot(0);
            screen.Submit();
            yield return WaitScreenIdle();
            Assert.IsTrue(screen.LastResult.Success, screen.LastResult.Message);
            world = RoomWorldBootstrap.Instance;
            Assert.IsFalse(screen.IsScreenVisible, "hidden while the game runs");
            yield return Settle();
            AssertOneOfEverything("Tut_01", "after Submit on an empty slot");
            Assert.AreEqual(0, SaveSlots.CurrentSlot);
            Assert.IsTrue(File.Exists(SlotFile(0)));
            HitWall();
            yield return CollectPickupByTouching();

            // Back to the menu (what Escape does).
            yield return screen.QuitToMenu();
            yield return null;
            world = null;
            Assert.IsTrue(screen.IsScreenVisible);
            Assert.IsNull(RoomWorldBootstrap.Instance);
            Assert.AreEqual(SlotState.Ok, screen.Model.Rows[0].State, "the row was re-read");
            Assert.IsTrue(OnDisk(0).sceneData.GetBool("Tut_01", WallId));

            // Submit on the same slot continues.
            SelectSlot(0);
            screen.Submit();
            yield return WaitScreenIdle();
            Assert.IsTrue(screen.LastResult.Success, screen.LastResult.Message);
            world = RoomWorldBootstrap.Instance;
            yield return Settle();
            AssertOneOfEverything("Tut_01", "after Continue through the screen");
            AssertWall(true, "after Continue through the screen");
            AssertPickup(true, "after Continue through the screen");
            Assert.AreEqual(world.Hero.Health.MaxHealth, world.Hero.Health.Health);

            // Slot 2 is a separate new game.
            yield return screen.QuitToMenu();
            yield return null;
            world = null;
            SelectSlot(1);
            screen.Submit();
            yield return WaitScreenIdle();
            Assert.IsTrue(screen.LastResult.Success, screen.LastResult.Message);
            world = RoomWorldBootstrap.Instance;
            yield return Settle();
            AssertOneOfEverything("Tut_01", "in the second slot");
            Assert.AreEqual(1, SaveSlots.CurrentSlot);
            AssertWall(false, "in the second slot");
            AssertPickup(false, "in the second slot");

            yield return screen.QuitToMenu();
            yield return null;
            world = null;

            // Asking again over a good save: Cancel changes nothing.
            SelectSlot(0);
            screen.RequestNew();
            Assert.AreEqual(StartScreenPhase.ConfirmNew, screen.Model.Phase);
            screen.Cancel();
            Assert.AreEqual(StartScreenPhase.Browsing, screen.Model.Phase);
            Assert.IsNull(RoomWorldBootstrap.Instance);
            Assert.IsTrue(OnDisk(0).sceneData.GetBool("Tut_01", WallId), "slot 1 still holds the broken wall");

            // Delete: Cancel keeps the save, two confirmations remove it.
            screen.RequestDelete();
            Assert.AreEqual(StartScreenPhase.ConfirmDelete1, screen.Model.Phase);
            screen.Cancel();
            Assert.IsTrue(File.Exists(SlotFile(0)));
            screen.RequestDelete();
            screen.Submit();
            Assert.AreEqual(StartScreenPhase.ConfirmDelete2, screen.Model.Phase);
            Assert.IsTrue(File.Exists(SlotFile(0)), "one confirmation is not enough");
            screen.Submit();
            Assert.IsFalse(File.Exists(SlotFile(0)));
            Assert.AreEqual(SlotState.Empty, screen.Model.Rows[0].State);
            Assert.IsTrue(File.Exists(SlotFile(1)), "the other slot is untouched");
            Assert.AreEqual(3, factoryCalls, "one world per start (new, continue, new); none for the cancelled and deleted commands");
        }

        // ---------- 8. direct room play writes nothing ----------

        [UnityTest]
        public IEnumerator DirectRoomPlay_WritesNothing_AfterABenchRestAndARoomChange()
        {
#if UNITY_EDITOR
            // Pressing Play on Tut_01: the room's RoomWorldStarter makes the world; no GameSession, no slot.
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(PathOf("Tut_01"), new LoadSceneParameters(LoadSceneMode.Single));
            yield return Until(() => RoomWorldBootstrap.Instance != null && RoomWorldBootstrap.Instance.Loader != null
                && RoomWorldBootstrap.Instance.Loader.CurrentRoomName == "Tut_01", "the starter to create the world in Tut_01");
            world = RoomWorldBootstrap.Instance;
            yield return Until(() => world.Hero != null && world.Hero.IsGrounded && !world.Hero.ControlLocked, "the hero to land on the start floor");
            input = new FakeHeroInput();
            world.Hero.Input = input;

            Assert.AreEqual(-1, SaveSlots.CurrentSlot);
            Assert.AreEqual(-1, world.Hero.Health.SaveSlot);
            world.PlayerData.health = world.PlayerData.maxHealth - 1;
            var bench = FindBench();
            yield return RestAtTut01Bench();
            Assert.AreEqual(world.PlayerData.maxHealth, world.Hero.Health.Health, "the rest still heals");
            Assert.AreEqual(0, bench.SavesWritten, "but writes nothing");

            yield return GoThroughGate("Tut_01", "right1", "Tut_01b");
            Assert.AreEqual(0, world.AutosaveCount, "a room change writes nothing");
            Assert.AreEqual(-1, SaveSlots.CurrentSlot);

            bool noFiles = !Directory.Exists(tempRoot) || Directory.GetFileSystemEntries(tempRoot).Length == 0;
            Assert.IsTrue(noFiles, "the save folder is still empty");
#else
            Assert.Ignore("Generated rooms are loaded by path in the Editor only.");
            yield break;
#endif
        }
    }
}
