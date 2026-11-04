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
    // Milestone 4 task 6: the first enemy on the REAL generated rooms. The Moss Creep in Tut_01b, the real nail, the
    // real gates, GameSession, SaveSlots and a temp save folder (SaveSystem.Root is restored in teardown). Style, fixture
    // and teardown follow M3AcceptanceTests. The rooms are never built here: build them with Silksong/Build Transition
    // Rooms (or -executeMethod Hollowknight.Silksong.Editor.TransitionRoomsBuilder.BuildBatch); an outdated scene fails
    // at once with that instruction.
    //
    // Positions come from the generated rooms: the "Bump" (the enemy's stage) and the "MossStep" (where the hero stands to
    // reach it) are looked up by name in the loaded Tut_01b, the enemy from the live MossCreep, the hero's feet from the
    // hero's collider constants. The step exists because the hero's side swing covers only 1.1..2.3 above its feet (see
    // docs/study/14f-placing-the-enemy.md); the swings here are real (a held Attack button on the fake input).
    //
    // Not covered (manual, docs/study/M4-checklist.md): how the placeholder looks, whether the flash shows on green,
    // the feel of recoil and shove, fairness of the placement, whether the nail reaches the enemy comfortably.
    public class M4AcceptanceTests
    {
        const string RoomsFolder = "Assets/Scenes/Rooms";
        static readonly string[] Rooms = { "Tut_01", "Tut_01b", "Tut_02" };
        const string BuildHint = "Run the menu Silksong/Build Transition Rooms (after Silksong/Import Hero Animations for the hero art), or -executeMethod Hollowknight.Silksong.Editor.TransitionRoomsBuilder.BuildBatch before -runTests.";

        // The explicit save id the builder gives the Tut_01b enemy (TransitionRoomsBuilder.Tut01bMossCreepId; the builder is
        // in the Editor assembly, which this assembly does not reference).
        const string EnemyId = "Tut_01b_MossCreep_1";

        static string PathOf(string room) => RoomsFolder + "/" + room + ".unity";

        static void RequireScenes()
        {
#if UNITY_EDITOR
            foreach (var room in Rooms)
            {
                Assert.IsTrue(File.Exists(PathOf(room)), PathOf(room) + " does not exist. " + BuildHint);
                var sceneTime = File.GetLastWriteTimeUtc(PathOf(room));
                foreach (var src in new[] { "Assets/Silksong/Editor/TransitionRoomsBuilder.cs", "Assets/Silksong/Rooms/RoomWorldStarter.cs", "Assets/Silksong/Enemy/MossCreep.cs" })
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
        readonly List<UnityEngine.Object> clones = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            RequireScenes();
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            PersistentBool.ResetStatics();
            stubsPath = Path.Combine(Path.GetTempPath(), "m4-accept-stubs-" + Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
            savedRoot = SaveSystem.Root;
            tempRoot = Path.Combine(Path.GetTempPath(), "m4-accept-saves-" + Guid.NewGuid().ToString("N"));
            SaveSystem.Root = tempRoot;
            SaveSlots.ResetForTests();
            RemoveStrays();
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
            if (world != null) UnityEngine.Object.Destroy(world.gameObject);
            world = null;
            yield return null;
            // Reuse the empty scene a previous test left behind: CreateScene throws if the name is already taken.
            var empty = SceneManager.GetSceneByName("M4AcceptanceEmpty");
            if (!empty.IsValid() || !empty.isLoaded) empty = SceneManager.CreateScene("M4AcceptanceEmpty");
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s == empty || !s.isLoaded || s.name.StartsWith("InitTestScene")) continue;   // the test runner's own scene: unloading it stalls the teardown
                yield return SceneManager.UnloadSceneAsync(s);
            }
            foreach (var b in UnityEngine.Object.FindObjectsByType<RoomWorldBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b != null) UnityEngine.Object.Destroy(b.gameObject);
            foreach (var h in UnityEngine.Object.FindObjectsByType<HeroController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (h != null) UnityEngine.Object.Destroy(h.gameObject);
            foreach (var c in UnityEngine.Object.FindObjectsByType<GameCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c != null) UnityEngine.Object.Destroy(c.gameObject);
            foreach (var e in UnityEngine.Object.FindObjectsByType<MossCreep>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (e != null) UnityEngine.Object.Destroy(e.gameObject);
            yield return null;
            yield return null;
            foreach (var o in clones) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            clones.Clear();
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

        // ---------- helpers (same as M3AcceptanceTests) ----------

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

        static float RootAboveFeet => SilksongUnits.HeroColliderSize.y * 0.5f - SilksongUnits.HeroColliderOffset.y;

        string SlotFile(int slot) => Path.Combine(tempRoot, "user" + slot + ".json");

        PlayerData OnDisk(int slot)
        {
            PlayerData d;
            Assert.AreEqual(SaveSystem.SlotFileState.Valid, SaveSystem.Peek(slot, out d), "slot " + slot + " must hold a readable save");
            return d;
        }

        RoomWorldBootstrap Factory(PlayerData d)
        {
            input = new FakeHeroInput();
            var w = RoomWorldBootstrap.Create(config, input, null, RoomWorldFactory.MakeSource(), d);
            w.Hero.Health.DeathRespawnDelaySeconds = 0.05f;
            return w;
        }

        IEnumerator Begin(int slot, bool cont)
        {
            lastResult = new GameStartResult();
            yield return GameSession.StartGame(slot, cont, Factory, lastResult);
            Assert.IsTrue(lastResult.Done);
            world = lastResult.World;
            Assert.IsTrue(lastResult.Success, lastResult.Message);
            yield return Settle();
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
        }

        TransitionPoint Gate(string room, string gateName)
        {
            var gate = GateRegistry.Find(gateName, SceneManager.GetSceneByName(room));
            Assert.AreEqual(gateName, gate.name, room + " has no gate " + gateName);
            return gate;
        }

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

        IEnumerator RestAtTut01Bench()
        {
            Assert.AreEqual("Tut_01", world.Loader.CurrentRoomName);
            var bench = UnityEngine.Object.FindFirstObjectByType<Bench>();
            Assert.IsNotNull(bench, "Tut_01 has its bench");
            int before = bench.RestCount;
            input.Move = Vector2.zero;
            var m = bench.transform.position;
            world.PlaceHero(new Vector2(m.x, m.y + 0.04f));
            yield return Until(() => world.Hero.IsGrounded && !world.Hero.ControlLocked, "the hero to stand at the bench");
            yield return Fixed(6);
            input.Move = new Vector2(0f, 1f);
            yield return Until(() => bench.RestCount == before + 1, "the rest to finish", 15f);
            input.Move = Vector2.zero;
            yield return Until(() => !world.Hero.ControlLocked, "control after the rest");
        }

        // ---------- the enemy ----------

        // The one Moss Creep of the loaded Tut_01b (active or hidden).
        static MossCreep FindCreep()
        {
            var all = UnityEngine.Object.FindObjectsByType<MossCreep>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, all.Length, "exactly one Moss Creep object is loaded");
            return all[0];
        }

        static Bounds SolidBounds(string room, string objectName)
        {
            var scene = SceneManager.GetSceneByName(room);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<BoxCollider2D>(true))
                    if (c.name == objectName) return c.bounds;
            Assert.Fail(room + " has no box named " + objectName + ". " + BuildHint);
            return default;
        }

        IEnumerator ToTut01b()
        {
            yield return GoThroughGate("Tut_01", "right1", "Tut_01b");
        }

        // Notes of one landed swing.
        sealed class SwingReport
        {
            public bool Landed, RecoilSeen, ShoveSeen;
            public int SilkBefore, SilkAfter, HealthBefore, HealthAfter;
        }

        // Stands the hero on the MossStep facing the stage, waits for the enemy to come to the step end of its stretch,
        // then swings with the real Attack button until one hit lands. Fills the report from what it observed.
        IEnumerator LandOneRealHit(MossCreep creep, SwingReport report)
        {
            var hero = world.Hero;
            var step = SolidBounds("Tut_01b", "MossStep");
            float heroX = step.max.x - 0.35f;
            float heroY = step.max.y + RootAboveFeet + 0.05f;
            report.HealthBefore = creep.Health.Health;
            report.SilkBefore = world.PlayerData.silk;

            yield return Until(() => !hero.Health.IsStunned && !hero.ControlLocked && !hero.Attack.IsAttacking && hero.Attack.CooldownRemaining <= 0f,
                "the hero to be ready to swing", 10f);
            input.Move = Vector2.zero;
            world.PlaceHeroAt(new Vector2(heroX, heroY), true);
            yield return Until(() => hero.IsGrounded, "the hero to stand on the step", 5f);

            float start = Time.unscaledTime;
            while (creep.Health.Health == report.HealthBefore)
            {
                if (Time.unscaledTime - start > 40f) Assert.Fail("Timed out: the nail never landed on the enemy (hero x " + heroX + ", enemy min x " + creep.SolidCollider.bounds.min.x + ")");
                yield return new WaitForFixedUpdate();
                bool inReach = creep.SolidCollider.bounds.min.x <= heroX + 1.0f;
                if (!inReach || hero.Attack.IsAttacking || hero.Attack.CooldownRemaining > 0f || hero.Health.IsStunned) continue;

                input.AttackButton.Press();
                for (int i = 0; i < 18; i++)
                {
                    yield return new WaitForFixedUpdate();
                    if (i == 1)
                    {
                        input.EndFixedStep(Time.fixedDeltaTime);
                        input.AttackButton.Release();
                        input.EndFixedStep(Time.fixedDeltaTime);
                    }
                    if (hero.Attack.IsRecoiling) report.RecoilSeen = true;
                    if (creep.Walker.Brain.IsShoved) report.ShoveSeen = true;
                }
            }
            report.Landed = true;
            report.HealthAfter = creep.Health.Health;
            report.SilkAfter = world.PlayerData.silk;
        }

        // Kills the enemy with two real swings and returns after it is dead.
        IEnumerator KillWithTwoSwings(MossCreep creep)
        {
            for (int i = 0; i < 2; i++)
            {
                var r = new SwingReport();
                yield return LandOneRealHit(creep, r);
                // Let the swing and its cooldown finish before the next one.
                yield return Until(() => !world.Hero.Attack.IsAttacking && world.Hero.Attack.CooldownRemaining <= 0f, "the swing to finish", 5f);
            }
            Assert.IsTrue(creep.Health.IsDead);
        }

        // ---------- (a) it exists and walks ----------

        [UnityTest]
        public IEnumerator Enemy_InTut01b_IsAlive_WithTheConfigNumbers_Visible_AndTurnsAtItsLedges()
        {
            yield return Begin(0, false);
            yield return ToTut01b();

            var creep = FindCreep();
            Assert.IsTrue(creep.gameObject.activeInHierarchy);
            Assert.AreEqual(EnemyId, creep.Persistence.Id);
            var cfg = creep.Config;
            Assert.AreEqual(SilksongEnemyConfigDefaults.MaxHealth, cfg.maxHealth);
            Assert.AreEqual(SilksongEnemyConfigDefaults.ContactDamage, cfg.contactDamage);
            Assert.AreEqual(SilksongEnemyConfigDefaults.WalkSpeed, cfg.walkSpeed);
            Assert.AreEqual(cfg.maxHealth, creep.Health.Health, "full health");
            Assert.IsFalse(creep.Health.IsDead);
            Assert.AreEqual(cfg.contactDamage, creep.Contact.DamageDealt);
            Assert.AreEqual(cfg.walkSpeed, creep.Walker.Params.WalkSpeed);
            Assert.AreEqual(cfg.staysDead, creep.Persistence.StaysDead);
            Assert.AreEqual(EnemyState.Walking, creep.Walker.State);

            var renderers = creep.GetComponentsInChildren<SpriteRenderer>();
            Assert.GreaterOrEqual(renderers.Length, 1, "a visible placeholder");
            foreach (var r in renderers) { Assert.IsTrue(r.enabled); Assert.IsNotNull(r.sprite); }
            Assert.AreEqual(EnemyPlaceholderView.BodyColor.g, renderers[0].color.g, 0.05f, "the green placeholder body");

            // Eight seconds of watching: it must stay on the Bump, turn at both ends and walk at its speed.
            var bump = SolidBounds("Tut_01b", "Bump");
            float minX = float.MaxValue, maxX = float.MinValue, lowest = float.MaxValue;
            int turns = 0;
            int facing = creep.Walker.Facing;
            bool sawWalkSpeed = false;
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < 8f)
            {
                yield return new WaitForFixedUpdate();
                var b = creep.SolidCollider.bounds;
                minX = Mathf.Min(minX, b.min.x);
                maxX = Mathf.Max(maxX, b.max.x);
                lowest = Mathf.Min(lowest, b.min.y);
                if (creep.Walker.Facing != facing) { turns++; facing = creep.Walker.Facing; }
                if (Mathf.Abs(creep.Body.linearVelocity.x) > cfg.walkSpeed - 0.5f) sawWalkSpeed = true;
            }
            Assert.IsTrue(sawWalkSpeed, "it walked at about its walk speed");
            Assert.GreaterOrEqual(turns, 2, "it turned round at the ends of the Bump (at least twice in eight seconds)");
            Assert.GreaterOrEqual(minX, bump.min.x - 0.4f, "never left the stretch on the left");
            Assert.LessOrEqual(maxX, bump.max.x + 0.4f, "never left the stretch on the right");
            Assert.GreaterOrEqual(lowest, bump.max.y - 0.15f, "never fell off the Bump");
            Assert.IsFalse(creep.Health.IsDead);
        }

        // ---------- (b) touching it hurts ----------

        [UnityTest]
        public IEnumerator WalkingIntoTheEnemy_CostsOneHealth_AndTheHeroIsThenInvulnerable()
        {
            yield return Begin(0, false);
            yield return ToTut01b();
            var creep = FindCreep();
            var hero = world.Hero;
            var bump = SolidBounds("Tut_01b", "Bump");
            int before = hero.Health.Health;
            Assert.AreEqual(hero.Health.MaxHealth, before);

            // The hero stands on the Bump inside the enemy (the hero is never blocked by it).
            float y = bump.max.y + RootAboveFeet + 0.05f;
            input.Move = Vector2.zero;
            world.PlaceHero(new Vector2(creep.transform.position.x, y));
            yield return Until(() => hero.Health.Health < before, "the touch to hurt the hero", 5f);
            Assert.AreEqual(before - creep.Config.contactDamage, hero.Health.Health, "exactly the contact damage");
            Assert.IsTrue(hero.Health.IsInvulnerable);
            Assert.Greater(hero.Health.InvulnerableRemaining, 0.3f);

            // Staying in it: no second hit inside the invulnerability window.
            int after = hero.Health.Health;
            for (int i = 0; i < 8; i++)
            {
                world.PlaceHero(new Vector2(creep.transform.position.x, y));
                yield return Fixed(4);
                if (hero.Health.InvulnerableRemaining < 0.2f) break;
                Assert.AreEqual(after, hero.Health.Health, "no second hit while invulnerable");
            }
            Assert.IsFalse(creep.Health.IsDead, "touching is not hitting");
            Assert.AreEqual(creep.Config.maxHealth, creep.Health.Health);
        }

        // ---------- (c) two real nail swings ----------

        [UnityTest]
        public IEnumerator TwoRealSwings_Kill_GiveSilk_Recoil_AndTheCorpseIsHarmless()
        {
            yield return Begin(0, false);
            yield return ToTut01b();
            var creep = FindCreep();
            var hero = world.Hero;
            int died = 0;
            creep.Health.Died += () => died++;
            int maxHealth = creep.Health.MaxHealth;
            Assert.AreEqual(0, world.PlayerData.silk);
            int heroHealth = hero.Health.Health;

            var first = new SwingReport();
            yield return LandOneRealHit(creep, first);
            Assert.AreEqual(maxHealth - 1, first.HealthAfter, "one needle hit costs 1");
            Assert.AreEqual(first.SilkBefore + 1, first.SilkAfter, "silk +1 for the landed hit");
            Assert.IsTrue(first.RecoilSeen, "the hero recoiled from the hit");
            Assert.IsTrue(first.ShoveSeen, "the enemy was shoved");
            Assert.GreaterOrEqual(creep.Flash.FlashCount, 1, "the white flash ran");
            Assert.IsFalse(creep.Health.IsDead);
            Assert.AreEqual(0, died);
            Assert.AreEqual(heroHealth, hero.Health.Health, "swinging at it never hurt the hero");

            yield return Until(() => !hero.Attack.IsAttacking && hero.Attack.CooldownRemaining <= 0f, "the swing to finish", 5f);
            var deathSpot = (Vector2)creep.transform.position;
            var second = new SwingReport();
            yield return LandOneRealHit(creep, second);
            Assert.IsTrue(creep.Health.IsDead);
            Assert.AreEqual(1, died, "Died fires once");
            Assert.AreEqual(second.SilkBefore + 1, second.SilkAfter, "silk +1 for the second hit too");
            Assert.AreEqual(2, world.PlayerData.silk);
            Assert.AreEqual(EnemyState.Dead, creep.Walker.State, "death stopped the walker");
            Assert.AreEqual(0, creep.Contact.DamageDealt, "a dead enemy does not hurt");
            Assert.IsTrue(world.PlayerData.sceneData.GetBool("Tut_01b", EnemyId), "the death is written at once");
            Assert.IsFalse(creep.gameObject.activeSelf, "removed from the room");

            // Standing where it died: nothing hurts the hero.
            int health = hero.Health.Health;
            world.PlaceHero(new Vector2(deathSpot.x, SolidBounds("Tut_01b", "Bump").max.y + RootAboveFeet + 0.05f));
            yield return Fixed(30);
            Assert.AreEqual(health, hero.Health.Health, "the dead enemy deals nothing");
        }

        // ---------- (d) leave and come back ----------

        [UnityTest]
        public IEnumerator DeadEnemy_StaysDead_AfterLeavingTut01b_AndComingBack()
        {
            yield return Begin(0, false);
            yield return ToTut01b();
            yield return KillWithTwoSwings(FindCreep());

            yield return GoThroughGate("Tut_01b", "left1", "Tut_01");
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<MossCreep>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "Tut_01 holds no enemy");
            yield return GoThroughGate("Tut_01", "right1", "Tut_01b");

            AssertEnemyHiddenAsDead("after returning to Tut_01b");
            yield return Fixed(50);
            AssertEnemyHiddenAsDead("a second later");
            var hero = world.Hero;
            int health = hero.Health.Health;
            world.PlaceHero(new Vector2(SolidBounds("Tut_01b", "Bump").center.x, SolidBounds("Tut_01b", "Bump").max.y + RootAboveFeet + 0.05f));
            yield return Fixed(40);
            Assert.AreEqual(health, hero.Health.Health, "nothing hurts the hero on the Bump");
        }

        void AssertEnemyHiddenAsDead(string what)
        {
            var creep = FindCreep();
            Assert.IsFalse(creep.gameObject.activeSelf, "the enemy is hidden " + what);
            Assert.IsTrue(creep.Persistence.IsHiddenAsDead, "hidden because it is flagged dead " + what);
            Assert.IsTrue(world.PlayerData.sceneData.GetBool("Tut_01b", EnemyId), "flag " + what);
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<MossCreep>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length, "no live enemy " + what);
        }

        // ---------- (e) save, quit, continue ----------

        [UnityTest]
        public IEnumerator FullRoundTrip_KillEnemy_RestAtBench_Quit_Continue_StillDead_HeroAtBench()
        {
            yield return Begin(0, false);
            yield return ToTut01b();
            yield return KillWithTwoSwings(FindCreep());
            yield return GoThroughGate("Tut_01b", "left1", "Tut_01");
            yield return RestAtTut01Bench();
            yield return Quit();

            var saved = OnDisk(0);
            Assert.IsTrue(saved.sceneData.GetBool("Tut_01b", EnemyId), "the dead enemy is on disk");
            Assert.AreEqual("Tut_01", saved.respawnScene);

            yield return Begin(0, true);
            Assert.AreEqual("Tut_01", world.Loader.CurrentRoomName, "continue lands in the bench room");
            var bench = UnityEngine.Object.FindFirstObjectByType<Bench>();
            Assert.AreEqual(bench.transform.position.x, world.Hero.transform.position.x, 0.6f, "the hero stands at the bench");
            Assert.IsTrue(world.PlayerData.sceneData.GetBool("Tut_01b", EnemyId), "still flagged after the load");
            yield return GoThroughGate("Tut_01", "right1", "Tut_01b");
            AssertEnemyHiddenAsDead("after Continue");
        }

        // ---------- (f) the other slot ----------

        [UnityTest]
        public IEnumerator OtherSlot_HasItsOwnLivingEnemy()
        {
            yield return Begin(0, false);
            yield return ToTut01b();
            yield return KillWithTwoSwings(FindCreep());
            yield return Quit();
            Assert.IsTrue(OnDisk(0).sceneData.GetBool("Tut_01b", EnemyId));

            yield return Begin(1, false);
            Assert.AreEqual(1, SaveSlots.CurrentSlot);
            Assert.IsFalse(world.PlayerData.sceneData.GetBool("Tut_01b", EnemyId), "slot 1 knows nothing about slot 0's kill");
            yield return ToTut01b();
            var creep = FindCreep();
            Assert.IsTrue(creep.gameObject.activeSelf, "slot 1's enemy is alive");
            Assert.IsFalse(creep.Persistence.IsHiddenAsDead);
            Assert.IsFalse(creep.Health.IsDead);
            Assert.AreEqual(creep.Config.maxHealth, creep.Health.Health);
            Assert.AreEqual(EnemyState.Walking, creep.Walker.State);
            Assert.IsTrue(OnDisk(0).sceneData.GetBool("Tut_01b", EnemyId), "slot 0 is untouched");
        }

        // ---------- (g) stays-dead off ----------

        [UnityTest]
        public IEnumerator StaysDeadOff_EnemyComesBack_WhileTheDefaultStaysDead()
        {
            yield return Begin(0, false);
            yield return ToTut01b();
            var real = FindCreep();
            var parent = real.transform.parent;

            var returning = UnityEngine.Object.Instantiate(real.Config);
            returning.staysDead = false;
            clones.Add(returning);
            var staying = UnityEngine.Object.Instantiate(real.Config);
            staying.staysDead = true;
            clones.Add(staying);

            // Two code-built variants on the open floor left of the step, each with its own id.
            Vector2 spotA = new Vector2(34.5f, MossCreep.RootYOnFloor(returning, 0f));
            Vector2 spotB = new Vector2(31.5f, MossCreep.RootYOnFloor(staying, 0f));
            var a = MossCreep.Create(returning, "Test_Returning_1", spotA, true, parent);
            var b = MossCreep.Create(staying, "Test_Staying_1", spotB, true, parent);
            yield return Fixed(5);
            Assert.IsFalse(a.Persistence.StaysDead);
            Assert.IsTrue(b.Persistence.StaysDead);

            var hit = new HitInstance { Damage = 99, Direction = AttackDirection.Normal, Source = world.Hero.gameObject };
            Assert.IsTrue(a.Health.TakeHit(hit));
            Assert.IsTrue(b.Health.TakeHit(hit));
            Assert.IsTrue(a.Health.IsDead);
            Assert.IsTrue(b.Health.IsDead);
            Assert.IsFalse(world.PlayerData.sceneData.GetBool("Tut_01b", "Test_Returning_1"), "stays-dead off writes nothing");
            Assert.IsTrue(world.PlayerData.sceneData.GetBool("Tut_01b", "Test_Staying_1"), "stays-dead on writes the flag");

            // Leave and come back: the room is rebuilt, so the variants are built again the same way.
            yield return GoThroughGate("Tut_01b", "left1", "Tut_01");
            yield return GoThroughGate("Tut_01", "right1", "Tut_01b");
            var parent2 = FindCreep().transform.parent;
            var a2 = MossCreep.Create(returning, "Test_Returning_1", spotA, true, parent2);
            var b2 = MossCreep.Create(staying, "Test_Staying_1", spotB, true, parent2);
            yield return Fixed(5);

            Assert.IsTrue(a2.gameObject.activeSelf, "the stays-dead-off enemy is back");
            Assert.IsFalse(a2.Health.IsDead);
            Assert.AreEqual(returning.maxHealth, a2.Health.Health);
            Assert.IsFalse(a2.Persistence.IsHiddenAsDead);
            Assert.IsFalse(b2.gameObject.activeSelf, "the stays-dead enemy is still gone");
            Assert.IsTrue(b2.Persistence.IsHiddenAsDead);
        }
    }
}
