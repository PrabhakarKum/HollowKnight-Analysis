using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hollowknight.Silksong.Tests
{
    // Task 8: hazard and death respawn across rooms, with the real persistent world (hero, camera, manager) and two
    // rooms built in code (same pattern as RoomWorldLoadingTests). A: x 0..60, B: x 200..260. Both have a "Start"
    // RespawnMarker, a HazardRespawnMarker at the left gate, a spike patch far from the gates, and a hazard trigger.
    public class RespawnAcrossRoomsTests
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

        CodeSceneSource source;
        string nameA, nameB;
        Scene runnerScene;
        HeroConfig config;
        RoomWorldBootstrap world;
        // Variations of room B's left1 gate, read when B is built.
        bool bGateHasMarker = true;
        bool bGateNonHazard;

        [SetUp]
        public void SetUp()
        {
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            Stubs.StubsDocPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rac-stubs-" + Guid.NewGuid() + ".md");
            Stubs.ResetForTests();
            runnerScene = SceneManager.GetActiveScene();
            RemoveStrays();

            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            nameA = "RaA_" + id;
            nameB = "RaB_" + id;
            source = new CodeSceneSource();
            source.Builders[nameA] = s => BuildRoom(s, nameA, 0f, 1, nameB, true, false);
            source.Builders[nameB] = s => BuildRoom(s, nameB, 200f, 2, nameA, bGateHasMarker, bGateNonHazard);
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
            yield return null;
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            Stubs.StubsDocPath = null;
            Stubs.ResetForTests();
        }

        // Floor, left1 / right1 gates, a Start marker, a hazard marker + trigger, spikes and a RoomInfo with bounds.
        static void BuildRoom(Scene scene, string name, float x0, int zoneId, string other, bool gateMarker, bool nonHazard)
        {
            Put(scene, MakeFloor(x0));

            var left = Gate(scene, "left1", new Vector2(x0 + 1f, 2f), other, "right1");
            var right = Gate(scene, "right1", new Vector2(x0 + 59f, 2f), other, "left1");
            if (gateMarker)
            {
                var gm = Marker(scene, "GateHazard", new Vector2(x0 + 6f, 3f), true);
                left.ConfigureHazard(gm, nonHazard);
            }
            else if (nonHazard) left.ConfigureHazard(null, true);

            var start = new GameObject("Start");
            start.transform.position = new Vector3(x0 + 20f, 1f, 0f);
            start.AddComponent<RespawnMarker>().Configure("Start", true);
            Put(scene, start);

            // The hazard marker a trigger points to, and the trigger the tests walk the hero into.
            var spot = Marker(scene, "TriggerSpot", new Vector2(x0 + 40f, 3f), false);
            var trigGo = new GameObject("HazardTrigger");
            trigGo.transform.position = new Vector3(x0 + 40f, 1f, 0f);
            var tb = trigGo.AddComponent<BoxCollider2D>();
            tb.isTrigger = true;
            tb.size = new Vector2(2f, 2f);
            trigGo.AddComponent<HazardRespawnTrigger>().Configure(spot);
            Put(scene, trigGo);

            var spikes = new GameObject("Spikes");
            spikes.transform.position = new Vector3(x0 + 50f, 1f, 0f);
            var sb = spikes.AddComponent<BoxCollider2D>();
            sb.isTrigger = true;
            sb.size = new Vector2(2f, 2f);
            spikes.AddComponent<HazardZone>().SetRespawnsHero(true);
            Put(scene, spikes);

            var boundsGo = new GameObject("Bounds");
            var bounds = boundsGo.AddComponent<RoomBounds>();
            bounds.Configure(new Rect(x0, 0f, 60f, 40f));
            Put(scene, boundsGo);

            var infoGo = new GameObject("Info");
            infoGo.AddComponent<RoomInfo>().Configure(name, zoneId, "Zone " + zoneId, bounds, "ZONE_" + zoneId);
            Put(scene, infoGo);
        }

        static GameObject MakeFloor(float x0)
        {
            var floor = new GameObject("Floor");
            floor.AddComponent<BoxCollider2D>().size = new Vector2(60f, 1f);
            floor.transform.position = new Vector3(x0 + 30f, -0.5f, 0f);
            return floor;
        }

        static void Put(Scene scene, GameObject go) => SceneManager.MoveGameObjectToScene(go, scene);

        static HazardRespawnMarker Marker(Scene scene, string name, Vector2 pos, bool right)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            Put(scene, go);
            Physics2D.SyncTransforms();
            var m = go.AddComponent<HazardRespawnMarker>();
            m.Configure(right);
            return m;
        }

        static TransitionPoint Gate(Scene scene, string gateName, Vector2 pos, string target, string arrival)
        {
            var go = new GameObject(gateName);
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 4f);
            box.isTrigger = true;
            Put(scene, go);
            var gate = go.AddComponent<TransitionPoint>();
            gate.Configure(target, arrival, Vector2.zero);
            return gate;
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

        // Sends a request and waits until it has started and then fully finished with the hero free.
        IEnumerator RequestAndWait(TransitionRequest request)
        {
            var manager = world.Manager;
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
                    && !world.Hero.ControlLocked, "the transition to finish");
            }
            finally
            {
                manager.TransitionStarted -= onStarted;
                manager.TransitionFinished -= onFinished;
            }
        }

        IEnumerator Leave(string room, string gateName)
        {
            var gate = GateRegistry.Find(gateName, SceneManager.GetSceneByName(room));
            yield return RequestAndWait(gate.BuildRequest());
        }

        // ---------- gate arrival ----------

        [UnityTest]
        public IEnumerator GateArrival_SetsTheHazardSpotToTheGatesMarker_InTheNewRoom()
        {
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            yield return Leave(nameA, "right1");

            var marker = GameObject.Find("GateHazard").GetComponent<HazardRespawnMarker>();
            Assert.AreEqual(nameB, marker.gameObject.scene.name);
            var spot = world.PlayerData.hazardRespawn;
            Assert.IsTrue(spot.IsSet);
            Assert.AreEqual(nameB, spot.scene);
            Assert.AreEqual("GateHazard", spot.markerName);
            Assert.AreEqual(marker.SpawnPosition.x, spot.position.x, 0.01f);
            Assert.AreEqual(marker.SpawnPosition.y, spot.position.y, 0.01f);
            Assert.AreSame(world.PlayerData, world.Hero.Health.Data, "one player record");
        }

        [UnityTest]
        public IEnumerator NonHazardGate_DoesNotSetASpot_AndClearsOneLeftFromTheOldRoom()
        {
            bGateNonHazard = true;
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            world.PlayerData.SetHazardRespawn(nameA, "OldSpot", new Vector2(10f, 1f), true);

            yield return Leave(nameA, "right1");

            Assert.IsFalse(world.PlayerData.hazardRespawn.IsSet, "stale spot from room A is gone and no new one was set");
        }

        [UnityTest]
        public IEnumerator GateWithoutMarker_UsesTheHerosArrivalPosition()
        {
            bGateHasMarker = false;
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            yield return Leave(nameA, "right1");

            var spot = world.PlayerData.hazardRespawn;
            Assert.IsTrue(spot.IsSet);
            Assert.AreEqual(nameB, spot.scene);
            Assert.AreEqual(201f, spot.position.x, 1f, "at the arrival gate");
        }

        // ---------- hazard in room B ----------

        static int Count<T>() where T : UnityEngine.Object =>
            UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        IEnumerator InB_StartedFromA()
        {
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            yield return Leave(nameA, "right1");
        }

        [UnityTest]
        public IEnumerator TouchingATrigger_InB_StoresItsMarker_ForRoomB()
        {
            yield return InB_StartedFromA();
            world.PlaceHero(new Vector2(240f, 1.5f));
            yield return Until(() => world.PlayerData.hazardRespawn.markerName == "TriggerSpot", "the trigger to store its marker");

            Assert.AreEqual(nameB, world.PlayerData.hazardRespawn.scene);
            Assert.AreEqual(240f, world.PlayerData.hazardRespawn.position.x, 0.01f);
            Assert.IsFalse(world.PlayerData.hazardRespawn.facingRight);
        }

        [UnityTest]
        public IEnumerator SpikeHit_InB_PutsTheHeroAtTheSavedSpot_InsideB()
        {
            yield return InB_StartedFromA();
            world.PlaceHero(new Vector2(240f, 1.5f));
            yield return Until(() => world.PlayerData.hazardRespawn.markerName == "TriggerSpot", "the trigger to store its marker");

            world.PlaceHero(new Vector2(250f, 1.5f));   // into the spikes
            yield return Until(() => world.Hero.Health.Health < world.Hero.Health.MaxHealth, "the spikes to hit");
            yield return Until(() => Mathf.Abs(world.Hero.transform.position.x - 240f) < 1f, "the hero to reappear at the saved spot");

            Assert.AreEqual(world.Hero.Health.MaxHealth - 1, world.Hero.Health.Health);
            Assert.IsFalse(world.Hero.Health.IsDead);
            Assert.IsFalse(world.Hero.FacingRight, "faces the way the marker says");
            Assert.AreEqual(nameB, SceneManager.GetActiveScene().name, "still in room B");
            Assert.AreEqual(1, Count<HeroController>());
        }

        [UnityTest]
        public IEnumerator SpikeHit_WithAStaleSpotFromRoomA_FallsBackToBStartMarker()
        {
            yield return InB_StartedFromA();
            world.PlayerData.SetHazardRespawn(nameA, "OldSpot", new Vector2(10f, 1f), true);

            world.PlaceHero(new Vector2(250f, 1.5f));
            yield return Until(() => world.Hero.Health.Health < world.Hero.Health.MaxHealth, "the spikes to hit");
            yield return Until(() => Mathf.Abs(world.Hero.transform.position.x - 220f) < 1f, "the hero to reappear at B's Start");

            Assert.IsFalse(world.PlayerData.hazardRespawn.IsSet, "the stale spot was cleared");
            Assert.AreEqual(nameB, SceneManager.GetActiveScene().name);
        }

        // ---------- death ----------

        void BenchInA()
        {
            world.PlayerData.respawnScene = nameA;
            world.PlayerData.respawnMarker = "Start";
            world.Hero.Health.DeathRespawnDelaySeconds = 0.05f;
        }

        void Kill()
        {
            var h = world.Hero.Health;
            Assert.IsTrue(h.TakeHit(new HitInstance { Damage = h.Health, Direction = AttackDirection.Normal }));
            Assert.IsTrue(h.IsDead);
        }

        IEnumerator UntilRespawned() => Until(() => !world.Hero.Health.IsDead && !world.Manager.IsTransitionRunning
            && world.Manager.State == GameState.Playing && !world.Hero.ControlLocked, "the hero to respawn", 8f);

        [UnityTest]
        public IEnumerator Death_InB_WithBenchInA_LoadsA_SameHero_FullHealth_ControlBack_CameraFollows()
        {
            yield return InB_StartedFromA();
            BenchInA();
            int heroId = world.Hero.GetInstanceID();
            int finished = 0;
            world.Manager.TransitionFinished += ok => { if (ok) finished++; };

            Kill();
            yield return UntilRespawned();
            yield return Until(() => finished > 0, "the respawn transition to finish");

            Assert.IsTrue(SceneManager.GetSceneByName(nameA).isLoaded, "back in the bench room");
            Assert.IsFalse(SceneManager.GetSceneByName(nameB).isLoaded);
            Assert.AreEqual(nameA, SceneManager.GetActiveScene().name);
            Assert.AreEqual(heroId, world.Hero.GetInstanceID(), "same hero object");
            Assert.AreEqual(1, Count<HeroController>());
            Assert.AreEqual(world.Hero.Health.MaxHealth, world.Hero.Health.Health);
            Assert.AreEqual(20f, world.Hero.transform.position.x, 1f, "at the Start marker");
            Assert.IsFalse(world.Hero.ControlLocked);
            Assert.AreEqual(nameA, world.PlayerData.currentScene);

            var infoA = RoomWorldBootstrap.FindRoomInfo(SceneManager.GetSceneByName(nameA));
            Assert.AreSame(infoA.Bounds, world.RoomCamera.Bounds, "camera bounds are room A's");
            Assert.IsTrue(infoA.Bounds.Area.Contains(world.RoomCamera.transform.position), "camera snapped into A");
            Assert.IsFalse(world.RoomCamera.IsFrozen);
            Assert.AreEqual(nameA, world.PlayerData.hazardRespawn.scene, "hazard spot follows to the bench room");
        }

        [UnityTest]
        public IEnumerator Death_InTheBenchRoom_RespawnsInPlace_WithoutATransition()
        {
            MakeWorld();
            yield return world.StartInRoom(nameA, "right1");
            BenchInA();
            int started = 0;
            world.Manager.TransitionStarted += r => started++;

            Kill();
            yield return UntilRespawned();

            Assert.AreEqual(0, started);
            Assert.AreEqual(nameA, SceneManager.GetActiveScene().name);
            Assert.AreEqual(20f, world.Hero.transform.position.x, 1f);
            Assert.AreEqual(world.Hero.Health.MaxHealth, world.Hero.Health.Health);
        }

        [UnityTest]
        public IEnumerator SecondDeath_Works_AndLeavesOneOfEverything()
        {
            yield return InB_StartedFromA();
            BenchInA();
            Kill();
            yield return UntilRespawned();
            yield return Until(() => SceneManager.GetSceneByName(nameA).isLoaded && !SceneManager.GetSceneByName(nameB).isLoaded, "room A only");

            yield return Leave(nameA, "right1");
            Assert.IsTrue(SceneManager.GetSceneByName(nameB).isLoaded);
            Kill();
            yield return UntilRespawned();
            yield return Until(() => !SceneManager.GetSceneByName(nameB).isLoaded, "room B to unload");
            yield return null;

            Assert.IsTrue(SceneManager.GetSceneByName(nameA).isLoaded);
            Assert.AreEqual(1, Count<HeroController>());
            Assert.AreEqual(1, Count<GameCamera>());
            Assert.AreEqual(1, Count<RoomTransitionManager>());
            Assert.AreEqual(1, Count<ScreenFader>());
            Assert.AreEqual(2, GateRegistry.Count, "only the live room's gates are registered");
            Assert.AreEqual(world.Hero.Health.MaxHealth, world.Hero.Health.Health);
        }

        [UnityTest]
        public IEnumerator Death_WithABenchMarkerMissingFromTheRoom_FallsBackToItsStartMarker()
        {
            yield return InB_StartedFromA();
            BenchInA();
            world.PlayerData.respawnMarker = "No Such Marker";
            Kill();
            yield return UntilRespawned();

            Assert.AreEqual(nameA, SceneManager.GetActiveScene().name);
            Assert.AreEqual(20f, world.Hero.transform.position.x, 1f);
        }
    }
}
