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
    // Task 9: Milestone 2 acceptance on the REAL generated rooms (Tut_01, Tut_01b, Tut_02) with real fade timings.
    // Tut_01 is opened as the first scene; its RoomWorldStarter makes the world. The hero's input is then replaced by a
    // FakeHeroInput so the test can walk it into the real gate triggers (the facing rule and the trigger path are
    // exercised, nothing is requested by hand in the walk tests).
    //
    // Covered: all 8 gate crossings walked for real (placed a few units inside the gate, then walked out; the second
    // crossing of each pair walks out of the arrival gate straight after arriving), the facing rule (push-back),
    // a spike hit in each room, death in Tut_01b with the bench in Tut_01, a second death, a second round trip, and
    // object counts staying at one. Not covered (manual, see docs/study/M2-checklist.md): jump or dash into a gate,
    // the look of the fade, the walk-in timing by eye, stubbed exits (there are no gates for them).
    //
    // The tests never build the rooms. Build them first with Silksong/Build Transition Rooms or
    // -executeMethod Hollowknight.Silksong.Editor.TransitionRoomsBuilder.BuildBatch; a missing or outdated scene
    // fails at once with that instruction.
    public class M2AcceptanceTests
    {
        const string RoomsFolder = "Assets/Scenes/Rooms";
        static readonly string[] Rooms = { "Tut_01", "Tut_01b", "Tut_02" };
        const string BuildHint = "Run the menu Silksong/Build Transition Rooms (after Silksong/Import Hero Animations for the hero art), or -executeMethod Hollowknight.Silksong.Editor.TransitionRoomsBuilder.BuildBatch before -runTests.";

        // room, gate, target room, arrival gate
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

        // Spike patches of the builder: room, x of the hazard trigger (stores the respawn spot), x of the spikes,
        // floor height, x of the stored marker.
        static readonly float[][] SpikeData =
        {
            new[] { 27f, 31f, 7.5f, 27f },     // Tut_01
            new[] { 31f, 28.75f, 0f, 31f },    // Tut_01b (left patch)
            new[] { 22f, 25f, 0f, 22f },       // Tut_02
        };

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

        RoomWorldBootstrap world;
        FakeHeroInput input;
        int startHealth;

        [SetUp]
        public void SetUp()
        {
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            Stubs.StubsDocPath = Path.Combine(Path.GetTempPath(), "m2-accept-stubs-" + Guid.NewGuid() + ".md");
            Stubs.ResetForTests();
            // Strays from earlier fixtures would give two worlds; the starter does nothing when one exists.
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
            world = null;
            yield return null;
            // Leave nothing behind: an empty active scene, every room and the host scene unloaded, no stray world objects.
            var empty = SceneManager.CreateScene("M2AcceptanceEmpty");
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
            yield return null;
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

        static int Count<T>() where T : UnityEngine.Object =>
            UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

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
            input = new FakeHeroInput();
            world.Hero.Input = input;
            startHealth = world.Hero.Health.Health;
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
            float rootAboveFeet = SilksongUnits.HeroColliderSize.y * 0.5f - SilksongUnits.HeroColliderOffset.y;
            input.Move = Vector2.zero;
            world.PlaceHero(new Vector2(x, feet + rootAboveFeet + 0.05f));
            yield return Until(() => world.Hero.IsGrounded && !world.Hero.ControlLocked, "the hero to stand inside " + room + "/" + gateName);
        }

        // Holds the move key toward the gate, waits for the change to start and finish. The test never sends a request.
        IEnumerator WalkOutAndWait(string room, string gateName)
        {
            var gate = Gate(room, gateName);
            var manager = world.Manager;
            var hero = world.Hero;
            int started = 0, finished = 0;
            bool sawLocked = false;
            Action<TransitionRequest> onStarted = r => started++;
            Action<bool> onFinished = ok => { if (ok) finished++; };
            manager.TransitionStarted += onStarted;
            manager.TransitionFinished += onFinished;
            try
            {
                input.Move = new Vector2(gate.Position == GatePosition.Left ? -1f : 1f, 0f);
                yield return Until(() => started > 0, "walking into " + room + "/" + gateName + " to start the change", 15f);
                input.Move = Vector2.zero;
                float start = Time.unscaledTime;
                while (!(finished > 0 && !manager.IsTransitionRunning && manager.State == GameState.Playing && !hero.ControlLocked))
                {
                    if (hero.ControlLocked) sawLocked = true;
                    if (Time.unscaledTime - start > 20f) Assert.Fail("Timed out waiting for the change " + room + "/" + gateName + " to finish");
                    yield return null;
                }
            }
            finally
            {
                manager.TransitionStarted -= onStarted;
                manager.TransitionFinished -= onFinished;
            }
            Assert.IsTrue(sawLocked, "the hero must be locked while the change runs");
        }

        void AssertWorldIntact(string expectedRoom, int heroId, string what)
        {
            Assert.AreEqual(expectedRoom, world.Loader.CurrentRoomName, "room " + what);
            Assert.AreEqual(expectedRoom, SceneManager.GetActiveScene().name, "active scene " + what);
            Assert.AreSame(world, RoomWorldBootstrap.Instance, "world " + what);
            Assert.AreEqual(heroId, world.Hero.GetInstanceID(), "same hero " + what);
            Assert.AreEqual(1, Count<HeroController>(), "heroes " + what);
            Assert.AreEqual(1, Count<GameCamera>(), "cameras " + what);
            Assert.AreEqual(1, Count<RoomTransitionManager>(), "managers " + what);
            Assert.AreEqual(1, Count<ScreenFader>(), "faders " + what);
            Assert.AreEqual(1, Count<RoomWorldBootstrap>(), "worlds " + what);
            int loaded = 0;
            foreach (var name in Rooms)
            {
                var s = SceneManager.GetSceneByName(name);
                if (s.IsValid() && s.isLoaded) loaded++;
            }
            Assert.AreEqual(1, loaded, "loaded rooms " + what);
            Assert.IsFalse(world.Hero.ControlLocked, "control " + what);
            Assert.AreEqual(GameState.Playing, world.Manager.State, "state " + what);
            Assert.AreEqual(expectedRoom, world.PlayerData.currentScene, "PlayerData.currentScene " + what);
            Assert.IsFalse(world.RoomCamera.IsFrozen, "camera frozen " + what);
            var info = RoomWorldBootstrap.FindRoomInfo(SceneManager.GetSceneByName(expectedRoom));
            Assert.AreSame(info.Bounds, world.RoomCamera.Bounds, "camera bounds " + what);
            Assert.IsTrue(info.Bounds.Area.Contains(world.RoomCamera.transform.position), "camera inside the room " + what);
        }

        // One crossing of the table, walked for real, with every check after it.
        IEnumerator DoStep(string[] step, bool placeFirst)
        {
            string from = step[0], gateName = step[1], to = step[2], arrivalName = step[3];
            int heroId = world.Hero.GetInstanceID();
            int health = world.Hero.Health.Health;
            Assert.AreEqual(from, world.Loader.CurrentRoomName, "before " + from + "/" + gateName);
            var gate = Gate(from, gateName);
            Assert.AreEqual(to, gate.TargetScene);
            Assert.AreEqual(arrivalName, gate.EntryGate);

            if (placeFirst) yield return PlaceInsideGate(from, gateName);
            yield return WalkOutAndWait(from, gateName);

            string what = "after " + from + "/" + gateName;
            AssertWorldIntact(to, heroId, what);
            Assert.AreEqual(health, world.Hero.Health.Health, "hero health " + what);
            var arrival = Gate(to, arrivalName);
            Vector2 heroPos = world.Hero.transform.position;
            Assert.Less(Mathf.Abs(heroPos.x - arrival.transform.position.x), 8f, "hero x " + what);
            Assert.Less(Mathf.Abs(heroPos.y - arrival.transform.position.y), 4f, "hero y " + what);
            Assert.IsTrue(world.Hero.IsGrounded || world.Hero.Velocity.y <= 0.01f, "hero standing " + what);
        }

        IEnumerator FullRound()
        {
            for (int i = 0; i < Steps.Length; i++)
                yield return DoStep(Steps[i], i % 2 == 0);   // the second step of a pair walks straight back out of the arrival gate
        }

        // ---------- tests ----------

        [UnityTest]
        public IEnumerator Start_Tut01_OneOfEverything_FullHealth_BenchIsTut01()
        {
            yield return StartWorld();
            AssertWorldIntact("Tut_01", world.Hero.GetInstanceID(), "at the start");
            Assert.AreEqual(world.Hero.Health.MaxHealth, world.Hero.Health.Health);
            Assert.AreEqual("Tut_01", world.PlayerData.respawnScene);
        }

        [UnityTest]
        public IEnumerator EveryGatePair_WalkedThereAndBack_ThroughTheRealTriggers()
        {
            yield return StartWorld();
            yield return FullRound();
            AssertWorldIntact("Tut_01", world.Hero.GetInstanceID(), "after the full round");
            Assert.AreEqual(startHealth, world.Hero.Health.Health);
        }

        [UnityTest]
        public IEnumerator FacingTheWrongWay_InAGate_PushesTheHeroBack_NoChange()
        {
            yield return StartWorld();
            foreach (var pair in new[] { new[] { "right1", "left" }, new[] { "left3", "right" } })
            {
                string gateName = pair[0];
                bool faceRight = pair[1] == "right";
                var gate = Gate("Tut_01", gateName);
                yield return PlaceInsideGate("Tut_01", gateName);
                // Face away from the gate: left for a right gate, right for a left gate.
                input.Move = new Vector2(faceRight ? 1f : -1f, 0f);
                yield return Until(() => world.Hero.FacingRight == faceRight, "the hero to face away from " + gateName, 5f);
                input.Move = Vector2.zero;

                int started = 0;
                Action<TransitionRequest> onStarted = r => started++;
                world.Manager.TransitionStarted += onStarted;
                var b = gate.GetComponent<Collider2D>().bounds;
                float rootAboveFeet = SilksongUnits.HeroColliderSize.y * 0.5f - SilksongUnits.HeroColliderOffset.y;
                float insideX = gate.Position == GatePosition.Left ? b.max.x - 0.1f : b.min.x + 0.1f;
                world.PlaceHero(new Vector2(insideX, b.min.y + rootAboveFeet + 0.05f));
                for (int i = 0; i < 25; i++) yield return new WaitForFixedUpdate();
                world.Manager.TransitionStarted -= onStarted;

                Assert.AreEqual(0, started, "no change may start when facing away at " + gateName);
                Assert.AreEqual("Tut_01", world.Loader.CurrentRoomName);
                float x = world.Hero.transform.position.x;
                if (gate.Position == GatePosition.Left) Assert.Greater(x, b.max.x - 0.01f, "pushed back out of " + gateName);
                else Assert.Less(x, b.min.x + 0.01f, "pushed back out of " + gateName);

                // Turning to face outward and walking in now does leave.
                yield return WalkOutAndWait("Tut_01", gateName);
                AssertWorldIntact(gate.TargetScene, world.Hero.GetInstanceID(), "after leaving " + gateName + " facing outward");
                // Back to Tut_01 for the next case, through the arrival gate.
                var back = GateRegistry.Find(gate.EntryGate, SceneManager.GetSceneByName(gate.TargetScene));
                yield return WalkOutAndWait(gate.TargetScene, back.name);
                Assert.AreEqual("Tut_01", world.Loader.CurrentRoomName);
            }
        }

        [UnityTest]
        public IEnumerator SpikeHit_InEachRoom_RespawnsInThatRoom()
        {
            yield return StartWorld();
            var order = new[] { "Tut_01", "Tut_01b", "Tut_02" };
            // Travel Tut_01 -> Tut_01b -> Tut_01 -> Tut_02 by real gates as needed.
            for (int i = 0; i < order.Length; i++)
            {
                string room = order[i];
                if (room == "Tut_01b") { yield return PlaceInsideGate("Tut_01", "right1"); yield return WalkOutAndWait("Tut_01", "right1"); }
                if (room == "Tut_02")
                {
                    yield return PlaceInsideGate("Tut_01b", "left1");                // the hero is mid-room after the spikes
                    yield return WalkOutAndWait("Tut_01b", "left1");                 // back to Tut_01
                    yield return PlaceInsideGate("Tut_01", "left3");
                    yield return WalkOutAndWait("Tut_01", "left3");
                }
                Assert.AreEqual(room, world.Loader.CurrentRoomName);

                var d = SpikeData[i];
                float feet = d[2];
                float rootAboveFeet = SilksongUnits.HeroColliderSize.y * 0.5f - SilksongUnits.HeroColliderOffset.y;
                int before = world.Hero.Health.Health;
                yield return Until(() => !world.Hero.Health.IsInvulnerable, "invulnerability to end before the spikes in " + room);
                input.Move = Vector2.zero;
                world.PlaceHero(new Vector2(d[0], feet + rootAboveFeet + 0.3f));
                yield return Until(() => world.PlayerData.hazardRespawn.IsSet && world.PlayerData.hazardRespawn.scene == room
                    && Mathf.Abs(world.PlayerData.hazardRespawn.position.x - d[3]) < 1.5f, "the trigger in " + room + " to store its spot");
                world.PlaceHero(new Vector2(d[1], feet + rootAboveFeet + 0.3f));
                yield return Until(() => world.Hero.Health.Health < before, "the spikes in " + room + " to hit");
                yield return Until(() => Mathf.Abs(world.Hero.transform.position.x - d[3]) < 1.5f, "the hero to reappear at the saved spot in " + room);

                Assert.AreEqual(before - 1, world.Hero.Health.Health, "one health lost in " + room);
                Assert.IsFalse(world.Hero.Health.IsDead);
                Assert.AreEqual(room, SceneManager.GetActiveScene().name, "still in " + room);
                Assert.AreEqual(1, Count<HeroController>());
            }
        }

        IEnumerator KillAndWaitForRespawn()
        {
            var h = world.Hero.Health;
            h.DeathRespawnDelaySeconds = 0.05f;
            yield return Until(() => h.TakeHit(new HitInstance { Damage = h.Health, Direction = AttackDirection.Normal }), "the hero to be hittable");
            Assert.IsTrue(h.IsDead);
            yield return Until(() => !h.IsDead && !world.Manager.IsTransitionRunning && world.Manager.State == GameState.Playing
                && !world.Hero.ControlLocked, "the hero to respawn", 15f);
            yield return Until(() => !world.Manager.IsTransitionRunning, "the respawn change to end");
        }

        [UnityTest]
        public IEnumerator Death_InTut01b_ReturnsToTheTut01Bench_ThenSecondDeath_ThenSecondRoundTrip()
        {
            yield return StartWorld();
            int heroId = world.Hero.GetInstanceID();

            yield return DoStep(Steps[0], true);                       // Tut_01 -> Tut_01b
            Assert.AreEqual("Tut_01b", world.Loader.CurrentRoomName);
            yield return KillAndWaitForRespawn();
            AssertWorldIntact("Tut_01", heroId, "after the first death");
            Assert.AreEqual(world.Hero.Health.MaxHealth, world.Hero.Health.Health, "full health at the bench");
            Assert.AreEqual(14f, world.Hero.transform.position.x, 2f, "at the bench (Tut_01 Start)");

            yield return DoStep(Steps[0], true);                       // out again, then die a second time
            yield return KillAndWaitForRespawn();
            AssertWorldIntact("Tut_01", heroId, "after the second death");
            Assert.AreEqual(world.Hero.Health.MaxHealth, world.Hero.Health.Health);

            yield return FullRound();
            AssertWorldIntact("Tut_01", heroId, "after the second full round");
            Assert.AreEqual(world.Hero.Health.MaxHealth, world.Hero.Health.Health);
        }
    }
}
