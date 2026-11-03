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
    // Milestone 3 task 6: the start screen driving real game starts. Code-built rooms (Tut_01 with a Start marker, a
    // second room with a bench), a fake hero input, saves in a temp folder. The screen is driven through its public
    // commands (the same ones the keys call). Everything is torn down: world, screen, scenes, statics, SaveSystem.Root.
    public class StartScreenTests
    {
        sealed class CodeSceneSource : IRoomSceneSource
        {
            public readonly Dictionary<string, Action<Scene>> Builders = new Dictionary<string, Action<Scene>>();
            public readonly List<string> Created = new List<string>();
            public bool Exists(string sceneName) { return Builders.ContainsKey(sceneName); }
            public AsyncOperation BeginLoad(string sceneName)
            {
                var scene = SceneManager.CreateScene(sceneName);
                Created.Add(sceneName);
                Builders[sceneName](scene);
                return null;
            }
        }

        const string FirstRoom = "Tut_01";

        CodeSceneSource source;
        string benchRoom, tempRoot, savedRoot, stubsPath;
        Scene runnerScene;
        HeroConfig config;
        RoomTransitionSettings settings;
        StartScreen screen;
        int factoryCalls;

        [SetUp]
        public void SetUp()
        {
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            PersistentBool.ResetStatics();
            stubsPath = Path.Combine(Path.GetTempPath(), "startscreen-stubs-" + Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
            savedRoot = SaveSystem.Root;
            tempRoot = Path.Combine(Path.GetTempPath(), "startscreen-saves-" + Guid.NewGuid().ToString("N"));
            SaveSystem.Root = tempRoot;
            SaveSlots.ResetForTests();
            runnerScene = SceneManager.GetActiveScene();
            RemoveStrays();
            factoryCalls = 0;

            benchRoom = "SsB_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            source = new CodeSceneSource();
            source.Builders[FirstRoom] = s => BuildRoom(s, 0f, 1, true);
            source.Builders[benchRoom] = s => BuildRoom(s, 200f, 2, false);
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
            foreach (var s in UnityEngine.Object.FindObjectsByType<StartScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s != null) UnityEngine.Object.DestroyImmediate(s.gameObject);
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (RoomWorldBootstrap.Instance != null) yield return GameSession.Quit(false);
            if (screen != null) UnityEngine.Object.Destroy(screen.gameObject);
            screen = null;
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

        // Floor (top at y 0), RoomInfo with bounds, a Start marker (first room, flagged as the bench) or a Bench.
        static void BuildRoom(Scene scene, float x0, int zoneId, bool first)
        {
            var floor = new GameObject("Floor");
            floor.AddComponent<BoxCollider2D>().size = new Vector2(60f, 1f);
            floor.transform.position = new Vector3(x0 + 30f, -0.5f, 0f);
            SceneManager.MoveGameObjectToScene(floor, scene);

            var boundsGo = new GameObject("Bounds");
            var bounds = boundsGo.AddComponent<RoomBounds>();
            bounds.Configure(new Rect(x0, 0f, 60f, 40f));
            SceneManager.MoveGameObjectToScene(boundsGo, scene);
            var infoGo = new GameObject("Info");
            infoGo.AddComponent<RoomInfo>().Configure(scene.name, zoneId, "Zone " + zoneId, bounds, "ZONE_" + zoneId);
            SceneManager.MoveGameObjectToScene(infoGo, scene);

            if (first)
            {
                var startGo = new GameObject("StartSpot");
                startGo.transform.position = new Vector3(x0 + 10f, 1f, 0f);
                var m = startGo.AddComponent<RespawnMarker>();
                m.Configure(RespawnRules.StartMarkerName, true);
                m.SetBench(true);
                SceneManager.MoveGameObjectToScene(startGo, scene);
            }
            else
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

        StartScreen MakeScreen(Func<PlayerData, RoomWorldBootstrap> factory = null)
        {
            var go = new GameObject("StartScreen");
            screen = go.AddComponent<StartScreen>();
            screen.WorldFactoryOverride = factory ?? DefaultFactory;
            return screen;
        }

        RoomWorldBootstrap DefaultFactory(PlayerData d)
        {
            factoryCalls++;
            var w = RoomWorldBootstrap.Create(config, new FakeHeroInput(), settings, source, d);
            w.Hero.Health.DeathRespawnDelaySeconds = -1f;
            return w;
        }

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

        IEnumerator WaitIdle()
        {
            yield return null;
            yield return Until(() => !screen.IsBusy, "the screen to finish");
        }

        static int Count<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }

        // ---------- tests ----------

        [UnityTest]
        public IEnumerator ScreenStartsVisible_WithFourSlotRows()
        {
            MakeScreen();
            yield return null;
            Assert.IsTrue(screen.IsScreenVisible);
            Assert.AreEqual(4, screen.Model.Rows.Count);
            StringAssert.Contains("Slot 1", screen.ListText());
            StringAssert.Contains("Empty", screen.ListText());
            Assert.IsNull(RoomWorldBootstrap.Instance);
        }

        [UnityTest]
        public IEnumerator NewOnEmptySlot_StartsAGame_AndWritesTheSlot()
        {
            MakeScreen();
            screen.Submit();
            yield return WaitIdle();

            Assert.IsTrue(screen.LastResult.Success, screen.LastResult.Message);
            Assert.IsFalse(screen.IsScreenVisible, "the screen is hidden while a game runs");
            Assert.AreEqual(FirstRoom, RoomWorldBootstrap.Instance.Loader.CurrentRoom.name);
            Assert.AreEqual(0, SaveSlots.CurrentSlot);
            Assert.IsTrue(File.Exists(Path.Combine(tempRoot, "user0.json")), "the new game is written to the slot");
        }

        [UnityTest]
        public IEnumerator Continue_GoesToTheSavedBench()
        {
            WriteSave(1, d => { d.respawnScene = benchRoom; d.respawnMarker = "Bench"; d.health = 3; });
            MakeScreen();
            screen.MoveDown();
            Assert.AreEqual(SlotState.Ok, screen.Model.SelectedRow.State);
            screen.Submit();
            yield return WaitIdle();

            Assert.IsTrue(screen.LastResult.Success, screen.LastResult.Message);
            var world = RoomWorldBootstrap.Instance;
            Assert.AreEqual(benchRoom, world.Loader.CurrentRoom.name);
            Assert.AreEqual(230f, world.Hero.transform.position.x, 0.5f);
            Assert.AreEqual(3, world.Hero.Health.Health);
            Assert.AreEqual(1, SaveSlots.CurrentSlot);
        }

        [UnityTest]
        public IEnumerator QuitToMenu_ShowsTheScreenAgain_AndASecondSlotStartsWithOneOfEverything()
        {
            MakeScreen();
            screen.Submit();
            yield return WaitIdle();
            Assert.IsNotNull(RoomWorldBootstrap.Instance);

            yield return screen.QuitToMenu();
            yield return null;

            Assert.IsNull(RoomWorldBootstrap.Instance);
            Assert.IsTrue(screen.IsScreenVisible);
            Assert.AreEqual(SlotState.Ok, screen.Model.Rows[0].State, "rows are re-read: slot 1 now holds the game");
            Assert.AreEqual(-1, SaveSlots.CurrentSlot);
            Assert.AreEqual(0, Count<HeroController>());

            screen.MoveDown();
            screen.Submit();
            yield return WaitIdle();

            Assert.IsTrue(screen.LastResult.Success, screen.LastResult.Message);
            Assert.AreEqual(1, SaveSlots.CurrentSlot);
            Assert.AreEqual(1, Count<RoomWorldBootstrap>());
            Assert.AreEqual(1, Count<HeroController>());
            Assert.AreEqual(1, Count<GameCamera>());
            Assert.AreEqual(1, Count<RoomTransitionManager>());
            Assert.IsTrue(File.Exists(Path.Combine(tempRoot, "user0.json")), "slot 1 is untouched");
            Assert.IsTrue(File.Exists(Path.Combine(tempRoot, "user1.json")));
        }

        [UnityTest]
        public IEnumerator InputIsIgnored_WhileAGameRuns()
        {
            MakeScreen();
            screen.Submit();
            yield return WaitIdle();
            int calls = factoryCalls;
            screen.Submit();
            screen.RequestNew();
            screen.RequestDelete();
            yield return null;
            Assert.AreEqual(calls, factoryCalls);
            Assert.AreEqual(1, Count<HeroController>());
        }

        [UnityTest]
        public IEnumerator DoubleSubmitInOneFrame_StartsOnlyOneGame()
        {
            MakeScreen();
            screen.Submit();
            screen.Submit();
            yield return WaitIdle();
            Assert.AreEqual(1, factoryCalls);
            Assert.AreEqual(1, Count<HeroController>());
        }

        [UnityTest]
        public IEnumerator FailedStart_ShowsAMessage_AndTheScreenComesBack()
        {
            MakeScreen(d => null);
            screen.Submit();
            yield return WaitIdle();

            Assert.IsFalse(screen.LastResult.Success);
            Assert.IsTrue(screen.IsScreenVisible);
            Assert.IsFalse(string.IsNullOrEmpty(screen.Model.Message));
            Assert.AreEqual(screen.LastResult.Message, screen.Model.Message);
            Assert.AreEqual(-1, SaveSlots.CurrentSlot);
            Assert.IsNull(RoomWorldBootstrap.Instance);
        }

        [UnityTest]
        public IEnumerator DamagedSlot_ExplainsAndStartsNothing()
        {
            Directory.CreateDirectory(tempRoot);
            File.WriteAllText(Path.Combine(tempRoot, "user0.json"), "{not json");
            MakeScreen();
            yield return null;
            screen.Model.Refresh();
            screen.Submit();
            yield return null;
            Assert.AreEqual(0, factoryCalls);
            Assert.AreEqual(StartScreenModel.DamagedMessage, screen.Model.Message);
            Assert.IsTrue(screen.IsScreenVisible);
        }

        [UnityTest]
        public IEnumerator DeleteThroughTheScreen_AsksTwice()
        {
            WriteSave(0, d => { });
            MakeScreen();
            yield return null;
            screen.RequestDelete();
            screen.Submit();
            Assert.IsTrue(File.Exists(Path.Combine(tempRoot, "user0.json")));
            screen.Submit();
            Assert.IsFalse(File.Exists(Path.Combine(tempRoot, "user0.json")));
            Assert.AreEqual(0, factoryCalls);
        }
    }
}
