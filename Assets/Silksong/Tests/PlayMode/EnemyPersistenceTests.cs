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
    // Milestone 4 task 4: enemies that stay dead. Rooms are built in code and "left" by unloading their scene, like
    // RoomMemoryTests. The storage is PersistentBool's fallback PlayerData (no world), except the save round trip,
    // which runs the real GameSession quit / continue in a temp folder (EnemyPersistenceSaveTests below).
    public class EnemyPersistenceTests
    {
        const string EnemyA = "Test_Enemy_A";
        const string EnemyB = "Test_Enemy_B";
        const float dt = 0.02f;

        string roomName;
        SimulationMode2D savedMode;
        SilksongEnemyConfig enemyConfig;
        string stubsPath;
        readonly List<UnityEngine.Object> spawned = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            PersistentBool.ResetStatics();
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            stubsPath = Path.Combine(Path.GetTempPath(), "enemy-persist-stubs-" + Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
            roomName = "EnPersist_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            enemyConfig = ScriptableObject.CreateInstance<SilksongEnemyConfig>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var o in spawned) if (o != null) UnityEngine.Object.Destroy(o);
            spawned.Clear();
            yield return null;
            var scene = SceneManager.GetSceneByName(roomName);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (enemyConfig != null) UnityEngine.Object.DestroyImmediate(enemyConfig);
            Physics2D.simulationMode = savedMode;
            PersistentBool.ResetStatics();
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            Stubs.ResetForTests();
            Stubs.StubsDocPath = null;
            if (File.Exists(stubsPath)) File.Delete(stubsPath);
        }

        sealed class Room
        {
            public Scene Scene;
            public EnemyHealth A, B;
            public EnemyPersistence PA, PB;
        }

        static EnemyHealth AddEnemy(Scene scene, SilksongEnemyConfig cfg, string name, string id, bool staysDead, Vector2 pos, out EnemyPersistence persistence)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
            box.isTrigger = true;
            var health = go.AddComponent<EnemyHealth>();
            health.Configure(cfg);
            go.AddComponent<EnemyContactDamage>().Configure(cfg);
            persistence = go.AddComponent<EnemyPersistence>();
            persistence.Configure(id, staysDead);
            SceneManager.MoveGameObjectToScene(go, scene);
            return health;
        }

        // Builds a room with enemy A (id EnemyA) and, when withB, enemy B (id EnemyB). Start has not run yet.
        Room BuildRoom(bool staysDeadA = true, bool withB = false)
        {
            var scene = SceneManager.CreateScene(roomName);
            var room = new Room { Scene = scene };
            room.A = AddEnemy(scene, enemyConfig, "EnemyA", EnemyA, staysDeadA, new Vector2(0f, 0.5f), out room.PA);
            if (withB) room.B = AddEnemy(scene, enemyConfig, "EnemyB", EnemyB, true, new Vector2(10f, 0.5f), out room.PB);
            return room;
        }

        IEnumerator Leave(Room room) { yield return SceneManager.UnloadSceneAsync(room.Scene); }

        static HitInstance Kill() => new HitInstance { Damage = 99, Direction = AttackDirection.Normal, Source = null };

        static bool Appears(EnemyHealth e) => e.gameObject.activeSelf;

        [UnityTest]
        public IEnumerator FirstVisit_EnemyIsAlive_AndNothingIsWritten()
        {
            var room = BuildRoom();
            yield return null;
            Assert.IsTrue(Appears(room.A));
            Assert.IsFalse(room.A.IsDead);
            Assert.IsFalse(PersistentBool.Fallback.sceneData.HasBool(roomName, EnemyA), "reading creates no entry");
        }

        [UnityTest]
        public IEnumerator Kill_WritesTheFlagAtOnce()
        {
            var room = BuildRoom();
            yield return null;
            Assert.IsTrue(room.A.TakeHit(Kill()));
            Assert.IsTrue(room.A.IsDead);
            Assert.IsTrue(PersistentBool.Fallback.sceneData.GetBool(roomName, EnemyA), "written on Died, not on leaving");
        }

        [UnityTest]
        public IEnumerator Killed_ThenLeaveAndReEnter_EnemyIsGone()
        {
            var first = BuildRoom();
            yield return null;
            first.A.TakeHit(Kill());
            yield return Leave(first);

            var second = BuildRoom();
            yield return null;
            Assert.IsFalse(Appears(second.A), "the stays-dead enemy never appears");
            Assert.IsTrue(second.PA.IsHiddenAsDead);
            Assert.AreEqual(0, second.A.GetComponent<EnemyContactDamage>().DamageDealt);
        }

        [UnityTest]
        public IEnumerator ReplacedPlayerData_WithTheFlag_StartsDead()
        {
            var loaded = PlayerData.CreateNew();
            loaded.sceneData.SetBool(roomName, EnemyA, true);
            PersistentBool.Fallback = loaded;

            var room = BuildRoom();
            yield return null;
            Assert.IsFalse(Appears(room.A));
        }

        [UnityTest]
        public IEnumerator ReplacedPlayerData_WithoutTheFlag_StartsAlive()
        {
            var first = BuildRoom();
            yield return null;
            first.A.TakeHit(Kill());
            yield return Leave(first);

            PersistentBool.Fallback = PlayerData.CreateNew();   // a save that never killed it
            var second = BuildRoom();
            yield return null;
            Assert.IsTrue(Appears(second.A));
            Assert.IsFalse(second.A.IsDead);
        }

        [UnityTest]
        public IEnumerator Reapply_HidesALiveEnemy_WhenTheNewDataSaysDead()
        {
            var room = BuildRoom();
            yield return null;
            Assert.IsTrue(Appears(room.A));

            var loaded = PlayerData.CreateNew();
            loaded.sceneData.SetBool(roomName, EnemyA, true);
            PersistentBool.Fallback = loaded;
            room.PA.Reapply();
            Assert.IsFalse(Appears(room.A));
        }

        [UnityTest]
        public IEnumerator StaysDeadOff_EnemyReturns_AndWritesNothing()
        {
            var first = BuildRoom(staysDeadA: false);
            yield return null;
            first.A.TakeHit(Kill());
            Assert.IsTrue(first.A.IsDead);
            Assert.IsFalse(PersistentBool.Fallback.sceneData.HasBool(roomName, EnemyA), "nothing written");
            yield return Leave(first);

            // Even a stored true flag is ignored.
            PersistentBool.Fallback.sceneData.SetBool(roomName, EnemyA, true);
            var second = BuildRoom(staysDeadA: false);
            yield return null;
            Assert.IsTrue(Appears(second.A), "an ordinary enemy returns on every visit");
            Assert.IsFalse(second.A.IsDead);
        }

        [UnityTest]
        public IEnumerator TwoEnemies_WithDifferentIds_AreIndependent()
        {
            var first = BuildRoom(withB: true);
            yield return null;
            first.A.TakeHit(Kill());
            Assert.IsTrue(PersistentBool.Fallback.sceneData.GetBool(roomName, EnemyA));
            Assert.IsFalse(PersistentBool.Fallback.sceneData.HasBool(roomName, EnemyB));
            yield return Leave(first);

            var second = BuildRoom(withB: true);
            yield return null;
            Assert.IsFalse(Appears(second.A));
            Assert.IsTrue(Appears(second.B), "B was never killed");
        }

        [UnityTest]
        public IEnumerator EmptyId_LogsTheSharedPersistentWarning()
        {
            var scene = SceneManager.CreateScene(roomName);
            EnemyPersistence p;
            AddEnemy(scene, enemyConfig, "NoIdEnemy", "", true, Vector2.zero, out p);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[PersistentBool\] 'NoIdEnemy' has no Id"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DuplicateIds_LogTheSharedPersistentWarning()
        {
            var scene = SceneManager.CreateScene(roomName);
            EnemyPersistence p;
            AddEnemy(scene, enemyConfig, "Dup0", "same", true, Vector2.zero, out p);
            AddEnemy(scene, enemyConfig, "Dup1", "same", true, new Vector2(5f, 0f), out p);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[PersistentBool\] 'same' in scene '" + roomName + "' is used by two objects"));
            yield return null;
        }

        // ---------- the dead enemy cannot hurt the hero ----------

        void StepWorld(HeroController hero, FakeHeroInput input, EnemyHealth enemy, int count)
        {
            for (int i = 0; i < count; i++)
            {
                hero.StepFixed(dt);
                Physics2D.Simulate(dt);
                var c = enemy.GetComponent<EnemyContactDamage>();
                if (c != null && c.isActiveAndEnabled) c.Tick();
                input.EndFixedStep(dt);
            }
        }

        [UnityTest]
        public IEnumerator DeadEnemy_AfterReEntry_DoesNotHurtTheHero_ButAReturningOneDoes()
        {
            Physics2D.simulationMode = SimulationMode2D.Script;
            var heroConfig = ScriptableObject.CreateInstance<HeroConfig>();
            spawned.Add(heroConfig);
            var ground = new GameObject("Ground");
            spawned.Add(ground);
            ground.AddComponent<BoxCollider2D>().size = new Vector2(400f, 1f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            var heroGo = new GameObject("Hero");
            spawned.Add(heroGo);
            heroGo.transform.position = new Vector3(0f, 0.51f, 0f);
            heroGo.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 1f);
            var hero = heroGo.AddComponent<HeroController>();
            hero.ManualStepping = true;
            hero.Config = heroConfig;
            var input = new FakeHeroInput();
            hero.Input = input;
            hero.Health.DeathRespawnDelaySeconds = -1f;
            Physics2D.SyncTransforms();

            var first = BuildRoom();
            yield return null;
            first.A.TakeHit(Kill());
            yield return Leave(first);

            var second = BuildRoom();
            yield return null;
            Physics2D.SyncTransforms();
            StepWorld(hero, input, second.A, 10);
            Assert.AreEqual(hero.Health.MaxHealth, hero.Health.Health, "the dead enemy deals no contact damage");
            yield return Leave(second);

            // Control: the same enemy with stays-dead off comes back after re-entry and does hurt.
            var third = BuildRoom(staysDeadA: false);
            yield return null;
            Physics2D.SyncTransforms();
            StepWorld(hero, input, third.A, 10);
            Assert.Less(hero.Health.Health, hero.Health.MaxHealth, "a returning enemy still hurts");
        }
    }

    // The full save round trip: kill, quit through GameSession, continue the slot, the enemy is still dead.
    public class EnemyPersistenceSaveTests
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
        const string EnemyId = "Save_Enemy_1";

        CodeSceneSource source;
        string tempRoot, savedRoot, stubsPath;
        Scene runnerScene;
        HeroConfig config;
        FakeHeroInput input;
        RoomTransitionSettings settings;
        RoomWorldBootstrap world;
        SilksongEnemyConfig enemyConfig;

        [SetUp]
        public void SetUp()
        {
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            PersistentBool.ResetStatics();
            stubsPath = Path.Combine(Path.GetTempPath(), "enemy-save-stubs-" + Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
            savedRoot = SaveSystem.Root;
            tempRoot = Path.Combine(Path.GetTempPath(), "enemy-saves-" + Guid.NewGuid().ToString("N"));
            SaveSystem.Root = tempRoot;
            SaveSlots.ResetForTests();
            runnerScene = SceneManager.GetActiveScene();
            enemyConfig = ScriptableObject.CreateInstance<SilksongEnemyConfig>();
            source = new CodeSceneSource();
            source.Builders[FirstRoom] = BuildRoom;
            settings = new RoomTransitionSettings { fadeWaitSeconds = 0f, heroWaitSeconds = 0f, fadeOutSeconds = 0.05f, fadeInSeconds = 0.05f };
#if UNITY_EDITOR
            config = HeroConfigConsistency.LoadProjectAsset();
#endif
        }

        void BuildRoom(Scene scene)
        {
            var floor = new GameObject("Floor");
            floor.AddComponent<BoxCollider2D>().size = new Vector2(60f, 1f);
            floor.transform.position = new Vector3(30f, -0.5f, 0f);
            SceneManager.MoveGameObjectToScene(floor, scene);

            var boundsGo = new GameObject("Bounds");
            var bounds = boundsGo.AddComponent<RoomBounds>();
            bounds.Configure(new Rect(0f, 0f, 60f, 40f));
            SceneManager.MoveGameObjectToScene(boundsGo, scene);
            var infoGo = new GameObject("Info");
            infoGo.AddComponent<RoomInfo>().Configure(FirstRoom, 1, "Zone 1", bounds, "ZONE_1");
            SceneManager.MoveGameObjectToScene(infoGo, scene);

            var startGo = new GameObject("StartSpot");
            startGo.transform.position = new Vector3(10f, 1f, 0f);
            var m = startGo.AddComponent<RespawnMarker>();
            m.Configure(RespawnRules.StartMarkerName, true);
            m.SetBench(true);
            SceneManager.MoveGameObjectToScene(startGo, scene);

            // Far from the hero's start so nothing touches it.
            var go = new GameObject("SaveEnemy");
            go.transform.position = new Vector3(40f, 1f, 0f);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
            box.isTrigger = true;
            go.AddComponent<EnemyHealth>().Configure(enemyConfig);
            go.AddComponent<EnemyPersistence>().Configure(EnemyId, true);
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
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
            if (enemyConfig != null) UnityEngine.Object.DestroyImmediate(enemyConfig);
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

        static EnemyPersistence FindEnemy() =>
            UnityEngine.Object.FindFirstObjectByType<EnemyPersistence>(FindObjectsInactive.Include);

        [UnityTest]
        public IEnumerator KillQuitContinue_TheEnemyIsStillDead()
        {
            var result = new GameStartResult();
            yield return Begin(0, false, result);
            Assert.IsTrue(result.Success, result.Message);

            var enemy = FindEnemy();
            Assert.IsNotNull(enemy);
            Assert.IsTrue(enemy.gameObject.activeSelf, "alive in a new game");
            Assert.IsTrue(enemy.GetComponent<EnemyHealth>().TakeHit(new HitInstance { Damage = 99, Direction = AttackDirection.Normal, Source = null }));
            Assert.IsTrue(world.PlayerData.sceneData.GetBool(FirstRoom, EnemyId), "written into the world's data");

            yield return GameSession.Quit();
            world = null;
            Assert.IsNull(RoomWorldBootstrap.Instance);

            var again = new GameStartResult();
            yield return Begin(0, true, again);
            Assert.IsTrue(again.Success, again.Message);
            Assert.IsTrue(world.PlayerData.sceneData.GetBool(FirstRoom, EnemyId), "the flag came back from the file");
            var enemy2 = FindEnemy();
            Assert.IsNotNull(enemy2);
            Assert.IsFalse(enemy2.gameObject.activeSelf, "still dead after continue");
            Assert.IsTrue(enemy2.IsHiddenAsDead);
        }
    }
}
