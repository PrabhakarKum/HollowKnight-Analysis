using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // Same deterministic setup as the dash, wall and attack tests: Script-mode physics, explicit StepFixed +
    // Simulate pairs, input edges cleared after each step. Floor top at y = 0; for the wall tests a tall wall to
    // the right of the hero whose left face is at x = WallFaceX. Hits are delivered by calling TakeHit directly,
    // exactly as an enemy's damage component would, from a source object placed where the test wants it.
    public class HeroHealthTests
    {
        const float HeroHalfWidth = 0.25f;
        const float HeroHalfHeight = 0.5f;
        const float WallFaceX = 1f;

        readonly List<UnityEngine.Object> spawned = new List<UnityEngine.Object>();
        SimulationMode2D savedMode;
        float dt = 0.02f;
        HeroConfig config;
        FakeHeroInput input;
        HeroController hero;
        string stubsPath;
        string saveRoot;
        int damagedCount, diedCount, respawnedCount, attackedCount;

        static readonly Vector2 Right = new Vector2(1f, 0f);
        static readonly Vector2 Down = new Vector2(0f, -1f);

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            dt = 0.02f;
            config = ScriptableObject.CreateInstance<HeroConfig>();
            stubsPath = Path.Combine(Path.GetTempPath(), "health-tests-stubs-" + System.Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                ClearWorld();
                if (config != null) UnityEngine.Object.DestroyImmediate(config);
            }
            finally
            {
                Physics2D.simulationMode = savedMode;
                Stubs.StubsDocPath = null;
                if (File.Exists(stubsPath)) File.Delete(stubsPath);
                SaveSystem.Root = null;
                if (saveRoot != null && Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
                saveRoot = null;
            }
        }

        void ClearWorld()
        {
            foreach (var o in spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            spawned.Clear();
            hero = null;
        }

        void BuildWorld(float startHeight, bool withWall = false)
        {
            ClearWorld();
            input = new FakeHeroInput();
            damagedCount = diedCount = respawnedCount = attackedCount = 0;

            var ground = new GameObject("Ground");
            spawned.Add(ground);
            var gbox = ground.AddComponent<BoxCollider2D>();
            gbox.size = new Vector2(400f, 1f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);

            float x = 0f;
            if (withWall)
            {
                var wall = new GameObject("Wall");
                spawned.Add(wall);
                var wbox = wall.AddComponent<BoxCollider2D>();
                wbox.size = new Vector2(1f, 400f);
                wall.transform.position = new Vector3(WallFaceX + 0.5f, 190f, 0f);
                x = WallFaceX - HeroHalfWidth - 0.01f;
            }

            var go = new GameObject("Hero");
            spawned.Add(go);
            go.transform.position = new Vector3(x, HeroHalfHeight + 0.01f + startHeight, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(HeroHalfWidth * 2f, HeroHalfHeight * 2f);
            hero = go.AddComponent<HeroController>();
            hero.ManualStepping = true;
            hero.Config = config;
            hero.Input = input;
            // No automatic respawn unless a test asks for it.
            hero.Health.DeathRespawnDelaySeconds = -1f;
            hero.Health.Damaged += () => damagedCount++;
            hero.Health.Died += () => diedCount++;
            hero.Health.Respawned += () => respawnedCount++;
            hero.Attack.Attacked += _ => attackedCount++;
            Physics2D.SyncTransforms();
        }

        void Step(int count)
        {
            for (int i = 0; i < count; i++)
            {
                hero.StepFixed(dt);
                Physics2D.Simulate(dt);
                input.EndFixedStep(dt);
            }
        }

        int Steps(float seconds) => Mathf.RoundToInt(seconds / dt);

        void Settle()
        {
            Step(Steps(0.2f));
            Assert.IsTrue(hero.IsGrounded, "hero should be standing on the ground after settling");
        }

        // A hit from a source object at (sourceX, 0.5), like an enemy's body.
        bool Hit(int damage, float sourceX)
        {
            var src = new GameObject("HitSource");
            spawned.Add(src);
            src.transform.position = new Vector3(sourceX, 0.5f, 0f);
            return hero.Health.TakeHit(new HitInstance { Damage = damage, Direction = AttackDirection.Normal, Source = src });
        }

        // Lethal hit from the left (the hero starts with 5 health).
        void Kill() => Assert.IsTrue(Hit(hero.Health.Health, -2f));

        RespawnMarker MakeMarker(string name, Vector2 pos, bool facingRight = true)
        {
            var go = new GameObject("Marker_" + name);
            spawned.Add(go);
            go.transform.position = pos;
            var m = go.AddComponent<RespawnMarker>();
            m.Configure(name, facingRight);
            return m;
        }

        GameObject MakeTarget(Vector2 center, Vector2 size, params System.Type[] components)
        {
            var go = new GameObject("Target");
            spawned.Add(go);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = size;
            box.isTrigger = true;
            foreach (var t in components) go.AddComponent(t);
            Physics2D.SyncTransforms();
            return go;
        }

        void ClingToWall()
        {
            BuildWorld(60f, withWall: true);
            input.Move = Right;
            Step(Steps(0.3f));
            Assert.AreEqual(HeroState.WallClinging, hero.State, "test setup: hero should be clinging");
        }

        // ------------------------------------------------------------------ the plan's seven tests

        [Test]
        public void Damage_ReducesHealthByAmount_AndStartsInvulnerability()
        {
            BuildWorld(0f);
            Settle();
            Assert.AreEqual(5, hero.Health.Health);

            Assert.IsTrue(Hit(2, -2f));

            Assert.AreEqual(3, hero.Health.Health);
            Assert.IsTrue(hero.Health.IsInvulnerable);
            Assert.AreEqual(config.HurtInvulnerableSeconds, hero.Health.InvulnerableRemaining, 0.001f);
            Assert.AreEqual(HeroState.Hurt, hero.State);
            Assert.AreEqual(1, damagedCount);
            Assert.AreEqual(0, diedCount);
            Assert.AreEqual(3, hero.Health.Data.health, "health is stored in PlayerData");
        }

        [Test]
        public void Damage_DuringInvulnerability_IsIgnored()
        {
            BuildWorld(0f);
            Settle();
            Assert.IsTrue(Hit(1, -2f));
            // Past the stun but still inside the invulnerability window.
            Step(Steps(config.HurtStunSeconds) + 5);
            Assert.IsTrue(hero.Health.IsInvulnerable, "test setup");
            Assert.IsFalse(hero.Health.IsStunned, "test setup");

            Assert.IsFalse(Hit(1, -2f));

            Assert.AreEqual(4, hero.Health.Health);
            Assert.AreEqual(1, damagedCount);
        }

        [Test]
        public void Damage_AppliesKnockbackAndStunsForConfigSeconds()
        {
            BuildWorld(0f);
            Settle();
            float startX = hero.transform.position.x;

            Assert.IsTrue(Hit(1, startX - 2f));

            // Every stunned step forces the knockback speed away from the source (here to the right).
            int stunned = 0;
            while (hero.State == HeroState.Hurt && stunned < 100)
            {
                Step(1);
                if (hero.State == HeroState.Hurt)
                {
                    stunned++;
                    Assert.AreEqual(config.HurtKnockbackX, hero.Velocity.x, 0.01f);
                    Assert.AreEqual(config.HurtKnockbackY, hero.Velocity.y, 0.01f);
                }
            }
            Assert.AreEqual(config.HurtStunSeconds, stunned * dt, dt);
            Assert.AreEqual(config.HurtKnockbackX * config.HurtStunSeconds, hero.transform.position.x - startX, 0.5f);
            // The knockback stops dead when the stun is over and control comes back the same step.
            Assert.AreEqual(0f, hero.Velocity.x, 0.01f);
            Assert.AreNotEqual(HeroState.Hurt, hero.State);
            Assert.IsFalse(hero.Health.IsStunned);
        }

        [Test]
        public void Death_WhileDashing_ClearsDashAndStateOnRespawn()
        {
            BuildWorld(0f);
            Settle();
            input.DashButton.Press();
            Step(1);
            input.DashButton.Release();
            Assert.IsTrue(hero.Dash.IsDashing, "test setup");
            Assert.AreEqual(HeroState.Dashing, hero.State);

            Kill();
            Assert.IsFalse(hero.Dash.IsDashing);
            Step(Steps(config.DashSeconds) + 2);
            Assert.AreEqual(HeroState.Dead, hero.State, "the dash must not overwrite Dead");
            Assert.AreEqual(0f, hero.Velocity.x, 0.01f, "no dash speed left on a dead hero");

            var marker = MakeMarker("Bench", new Vector2(6f, HeroHalfHeight + 0.01f));
            hero.Health.RespawnAt(marker);
            Settle();
            Assert.AreEqual(HeroState.Grounded, hero.State);
            Assert.IsFalse(hero.Dash.IsDashing);

            // The dash cooldown the first dash started is gone: a dash works at once.
            input.DashButton.Press();
            Step(1);
            input.DashButton.Release();
            Assert.IsTrue(hero.Dash.IsDashing);
            Assert.AreEqual(config.DashSpeed, hero.Velocity.x, 0.01f);
        }

        [Test]
        public void Death_WhileClingingToWall_ClearsClingOnRespawn()
        {
            ClingToWall();
            Kill();
            Assert.IsFalse(hero.Wall.IsClinging);
            Step(3);
            Assert.AreEqual(HeroState.Dead, hero.State, "the wall must not re-assert the cling over Dead");
            Assert.AreEqual(0f, hero.Velocity.y, 0.01f);

            var marker = MakeMarker("Bench", new Vector2(-6f, 5f), facingRight: false);
            hero.Health.RespawnAt(marker);
            input.Move = Vector2.zero;
            Step(2);

            Assert.IsFalse(hero.Wall.IsClinging);
            Assert.IsFalse(hero.Wall.IsWallJumpLocked);
            Assert.AreNotEqual(HeroState.WallClinging, hero.State);
            Assert.AreEqual(HeroState.Falling, hero.State);
            Assert.Less(hero.Velocity.y, 0f, "falls freely, no cling cap or stuck zero speed");
            Assert.IsFalse(hero.FacingRight, "faces the way the marker says");
        }

        [Test]
        public void Respawn_RestoresHealthAtMarker_AndClearsInvulnerability()
        {
            BuildWorld(0f);
            Settle();
            Assert.IsTrue(Hit(3, -2f));
            Assert.AreEqual(2, hero.Health.Health);
            Assert.IsTrue(hero.Health.IsInvulnerable);

            var marker = MakeMarker("Bench", new Vector2(10f, 3f));
            hero.Health.RespawnAt(marker);

            Assert.AreEqual(hero.Health.MaxHealth, hero.Health.Health);
            Assert.AreEqual(10f, hero.transform.position.x, 0.001f);
            Assert.AreEqual(3f, hero.transform.position.y, 0.001f);
            Assert.AreEqual(Vector2.zero, hero.Velocity);
            Assert.IsFalse(hero.Health.IsInvulnerable);
            Assert.IsFalse(hero.Health.IsStunned);
            Assert.IsFalse(hero.Health.IsDead);
            Assert.AreEqual(1, respawnedCount);
            Assert.AreEqual("Bench", hero.Health.Data.respawnMarker, "the marker name is stored in PlayerData");
            Assert.AreSame(marker, hero.Health.CurrentMarker);

            // No leftover invulnerability: the next hit lands.
            Assert.IsTrue(Hit(1, 8f));
        }

        [Test]
        public void Respawn_AfterDeathMidAttack_AllowsAttackAgainImmediately()
        {
            BuildWorld(0f);
            Settle();
            input.AttackButton.Press();
            Step(1);
            input.AttackButton.Release();
            Assert.IsTrue(hero.Attack.IsAttacking, "test setup");
            Assert.AreEqual(1, attackedCount);

            Kill();
            Assert.IsFalse(hero.Attack.IsAttacking);
            Assert.IsFalse(hero.Attack.HitboxActive);
            Step(3);

            var marker = MakeMarker("Bench", new Vector2(6f, HeroHalfHeight + 0.01f));
            hero.Health.RespawnAt(marker);
            Settle();

            // The swing cooldown would still be running; respawn cleared it.
            Assert.AreEqual(0f, hero.Attack.CooldownRemaining, 0.0001f);
            input.AttackButton.Press();
            Step(1);
            input.AttackButton.Release();
            Assert.AreEqual(2, attackedCount);
            Assert.IsTrue(hero.Attack.IsAttacking);
            Assert.IsTrue(hero.Attack.HitboxActive);
        }

        // ------------------------------------------------------------------ extras: knockback and timing

        [TestCase(-2f, 1f)]    // source on the left: pushed right, hero turns to face the source (left)
        [TestCase(2f, -1f)]    // source on the right: pushed left, hero faces right
        public void Knockback_IsAwayFromSource_AndHeroFacesIt(float sourceOffset, float expectedSign)
        {
            BuildWorld(0f);
            Settle();
            Assert.IsTrue(Hit(1, hero.transform.position.x + sourceOffset));
            Step(1);
            Assert.AreEqual(expectedSign * config.HurtKnockbackX, hero.Velocity.x, 0.01f);
            Assert.AreEqual(sourceOffset > 0f, hero.FacingRight, "faces the thing that hit it");
        }

        [Test]
        public void Knockback_WithoutSource_IsAwayFromFacing()
        {
            BuildWorld(0f);
            Settle();
            Assert.IsTrue(hero.FacingRight, "test setup");
            Assert.IsTrue(hero.Health.TakeHit(new HitInstance { Damage = 1 }));
            Step(1);
            Assert.Less(hero.Velocity.x, 0f);
        }

        [Test]
        public void Knockback_UsesConfigVerticalSpeedAndTurnsGravityOffWhileStunned()
        {
            config.HurtKnockbackY = 5f;
            BuildWorld(0f);
            Settle();
            Assert.IsTrue(Hit(1, -2f));
            Step(3);
            Assert.AreEqual(5f, hero.Velocity.y, 0.01f, "vertical knockback is forced, gravity does not eat it");
            Assert.IsFalse(hero.IsGrounded);
        }

        [TestCase(0.02f)]
        [TestCase(0.01f)]
        [TestCase(0.007f)]
        public void Timers_AreSecondsAndMatchAt50And100Hz(float step)
        {
            dt = step;
            BuildWorld(0f);
            Settle();
            Assert.IsTrue(Hit(1, -2f));

            int n = 0;
            int stunnedSteps = 0;
            int invulnerableSteps = 0;
            bool stunDone = false;
            while (n < 400 && (hero.Health.IsInvulnerable || !stunDone))
            {
                Step(1);
                n++;
                if (hero.State == HeroState.Hurt) stunnedSteps++; else stunDone = true;
                if (hero.Health.IsInvulnerable) invulnerableSteps++;
            }
            Assert.AreEqual(config.HurtStunSeconds, stunnedSteps * dt, 0.021f, "stun seconds at dt " + dt);
            Assert.AreEqual(config.HurtInvulnerableSeconds, (invulnerableSteps + 1) * dt, 0.021f, "invulnerability seconds at dt " + dt);
            Assert.IsTrue(Hit(1, -2f), "vulnerable again once the window is over");
        }

        // Regression: the float countdown can undershoot zero (e.g. -2e-9 at dt 0.01). The stun must still end,
        // hand the state back and release the velocity, whatever the step length.
        [TestCase(0.02f)]
        [TestCase(0.01f)]
        [TestCase(0.007f)]
        [TestCase(0.013f)]
        public void Stun_AlwaysEnds_AndStateReturnsToGrounded(float step)
        {
            dt = step;
            BuildWorld(0f);
            Settle();
            Assert.IsTrue(Hit(1, -2f));
            Assert.IsTrue(hero.Health.IsStunned);

            int n = 0;
            while (hero.State == HeroState.Hurt && n < 400) { Step(1); n++; }

            Assert.Less(n, 400, "the stun must end (dt " + step + ")");
            Assert.IsFalse(hero.Health.IsStunned);
            Assert.AreEqual(0f, hero.Velocity.x, 0.01f);
            Step(3);
            Assert.AreEqual(HeroState.Grounded, hero.State);
        }

        [Test]
        public void Hit_OnTheGround_DoesNotRaiseLandedAgain()
        {
            BuildWorld(0f);
            Settle();
            int landed = 0;
            hero.Landed += () => landed++;
            Assert.IsTrue(Hit(1, -2f));
            Step(Steps(config.HurtStunSeconds) + 6);
            Assert.AreEqual(0, landed, "the hero never left the floor");
        }

        [Test]
        public void Invulnerability_EndsAfterConfigSeconds()
        {
            BuildWorld(0f);
            Settle();
            Assert.IsTrue(Hit(1, -2f));
            Step(Steps(config.HurtInvulnerableSeconds) - 2);
            Assert.IsTrue(hero.Health.IsInvulnerable);
            Assert.IsFalse(Hit(1, -2f));
            Step(4);
            Assert.IsFalse(hero.Health.IsInvulnerable);
            Assert.IsTrue(Hit(1, -2f));
        }

        [Test]
        public void Hit_WithZeroDamage_DoesNothing()
        {
            BuildWorld(0f);
            Settle();
            Assert.IsFalse(Hit(0, -2f));
            Assert.AreEqual(5, hero.Health.Health);
            Assert.IsFalse(hero.Health.IsInvulnerable);
            Assert.AreEqual(0, damagedCount);
        }

        [Test]
        public void Hit_CancelsJumpInProgress()
        {
            BuildWorld(0f);
            Settle();
            input.JumpButton.Press();
            Step(3);
            Assert.Greater(hero.Velocity.y, 0f, "test setup: rising with the button held");

            Assert.IsTrue(Hit(1, -2f));
            Step(Steps(config.HurtStunSeconds) + 4);
            // Still holding jump: a jump that survived the hit would keep forcing the rise speed.
            Assert.Less(hero.Velocity.y, 0f);
        }

        // ------------------------------------------------------------------ extras: hurt ends other states

        [Test]
        public void Hurt_EndsDash_AndDashDoesNotOverwriteHurt()
        {
            BuildWorld(0f);
            Settle();
            input.DashButton.Press();
            Step(1);
            input.DashButton.Release();
            Assert.IsTrue(hero.Dash.IsDashing, "test setup");

            Assert.IsTrue(Hit(1, hero.transform.position.x - 2f));
            Assert.IsFalse(hero.Dash.IsDashing);
            Step(1);
            Assert.AreEqual(HeroState.Hurt, hero.State);
            Assert.AreEqual(config.HurtKnockbackX, hero.Velocity.x, 0.01f, "knockback, not dash speed");
        }

        [Test]
        public void Hurt_WhileClinging_EndsClingAndStateStaysHurt()
        {
            ClingToWall();
            // Source on the far side of the hero from the wall's point of view: to the right, so the push is left.
            Assert.IsTrue(Hit(1, 3f));
            Assert.IsFalse(hero.Wall.IsClinging);

            int hurtSteps = 0;
            while (hero.State == HeroState.Hurt && hurtSteps < 100)
            {
                Step(1);
                if (hero.State != HeroState.Hurt) break;
                hurtSteps++;
                Assert.IsFalse(hero.Wall.IsClinging);
                Assert.AreNotEqual(HeroState.WallClinging, hero.State);
                Assert.Less(hero.Velocity.x, 0f, "pushed away from the wall by the knockback");
            }
            Assert.Greater(hurtSteps, 3, "State stays Hurt for the whole stun");
            Assert.AreNotEqual(HeroState.Hurt, hero.State, "and is handed back when the stun is over");
            Assert.IsFalse(hero.Wall.IsWallJumpLocked);
        }

        [Test]
        public void Hurt_EndsSwingAndHitbox_ButCooldownKeepsRunning()
        {
            BuildWorld(0f);
            Settle();
            input.AttackButton.Press();
            Step(1);
            input.AttackButton.Release();
            Assert.IsTrue(hero.Attack.HitboxActive, "test setup");

            Assert.IsTrue(Hit(1, -2f));
            Assert.IsFalse(hero.Attack.IsAttacking);
            Assert.IsFalse(hero.Attack.HitboxActive);
            Assert.Greater(hero.Attack.CooldownRemaining, 0f);

            // A press during the stun starts nothing.
            input.AttackButton.Press();
            Step(1);
            input.AttackButton.Release();
            Assert.AreEqual(1, attackedCount);
        }

        [Test]
        public void Hurt_EndsPogoBounce()
        {
            BuildWorld(20f);
            Step(3);
            MakeTarget((Vector2)hero.transform.position + new Vector2(0f, -0.2f), Vector2.one,
                typeof(HealthManager), typeof(TrainingDummy));
            input.Move = Down;
            input.AttackButton.Press();
            Step(1);
            input.AttackButton.Release();
            Assert.IsTrue(hero.Attack.IsBouncing, "test setup");

            Assert.IsTrue(Hit(1, -2f));
            Assert.IsFalse(hero.Attack.IsBouncing);
            Step(2);
            Assert.AreEqual(config.HurtKnockbackY, hero.Velocity.y, 0.01f, "no pogo speed during the stun");
        }

        // ------------------------------------------------------------------ extras: death

        [Test]
        public void Death_RaisesDiedOnce_AndDeadHeroIgnoresFurtherHits()
        {
            BuildWorld(0f);
            Settle();
            Kill();
            Assert.IsTrue(hero.Health.IsDead);
            Assert.AreEqual(0, hero.Health.Health);
            Assert.AreEqual(HeroState.Dead, hero.State);
            Assert.AreEqual(1, diedCount);
            Assert.AreEqual(1, damagedCount);

            Assert.IsFalse(Hit(3, -2f));
            Step(Steps(config.HurtInvulnerableSeconds) + 5);
            Assert.IsFalse(Hit(3, 2f));

            Assert.AreEqual(1, diedCount, "Died is raised once");
            Assert.AreEqual(1, damagedCount);
            Assert.AreEqual(HeroState.Dead, hero.State, "movement does not rewrite Dead");
            Assert.AreEqual(0f, hero.Velocity.x, 0.001f);
            Assert.AreEqual(0, hero.Health.Health);
        }

        [Test]
        public void Death_AutoRespawnsAtLastMarkerAfterDelay()
        {
            BuildWorld(0f);
            Settle();
            var marker = MakeMarker("Bench", new Vector2(8f, HeroHalfHeight + 0.01f));
            hero.Health.RespawnAt(marker);
            Settle();
            hero.Health.DeathRespawnDelaySeconds = 0.5f;
            respawnedCount = 0;

            Kill();
            Step(Steps(0.5f) - 3);
            Assert.IsTrue(hero.Health.IsDead, "still dead before the delay is over");
            Step(6);
            Assert.IsFalse(hero.Health.IsDead);
            Assert.AreEqual(1, respawnedCount);
            Assert.AreEqual(hero.Health.MaxHealth, hero.Health.Health);
            Assert.AreEqual(8f, hero.transform.position.x, 0.2f);
            Assert.AreNotEqual(HeroState.Dead, hero.State);
        }

        [Test]
        public void Death_WithNoMarker_StaysDead()
        {
            BuildWorld(0f);
            Settle();
            hero.Health.DeathRespawnDelaySeconds = 0.2f;
            Kill();
            Step(Steps(1f));
            Assert.IsTrue(hero.Health.IsDead);
            Assert.AreEqual(0, respawnedCount);
        }

        // ------------------------------------------------------------------ extras: respawn, disable, save

        [Test]
        public void Respawn_AfterDeath_RunsAndJumpsNormally()
        {
            BuildWorld(0f);
            Settle();
            Kill();
            var marker = MakeMarker("Bench", new Vector2(6f, HeroHalfHeight + 0.01f));
            hero.Health.RespawnAt(marker);
            Settle();
            Assert.AreEqual(HeroState.Grounded, hero.State);

            input.Move = Right;
            input.JumpButton.Press();
            Step(2);
            Assert.AreEqual(config.RunSpeed, hero.Velocity.x, 0.01f);
            Assert.Greater(hero.Velocity.y, 0f);
        }

        [Test]
        public void Disable_WhileHurt_ResetsHealthStateAndDoesNotLeaveHeroStuckHurt()
        {
            BuildWorld(0f);
            Settle();
            Assert.IsTrue(Hit(1, -2f));
            Assert.AreEqual(HeroState.Hurt, hero.State);

            hero.Health.enabled = false;
            hero.Health.enabled = true;

            Assert.IsFalse(hero.Health.IsInvulnerable);
            Assert.IsFalse(hero.Health.IsStunned);
            Assert.AreNotEqual(HeroState.Hurt, hero.State);
            Step(3);
            Assert.AreNotEqual(HeroState.Hurt, hero.State);
        }

        [Test]
        public void Disable_WhileDead_ComesBackAlive()
        {
            BuildWorld(0f);
            Settle();
            Kill();
            hero.Health.enabled = false;
            hero.Health.enabled = true;
            Assert.IsFalse(hero.Health.IsDead);
            Step(3);
            Assert.AreNotEqual(HeroState.Dead, hero.State);
        }

        [Test]
        public void Respawn_WithSaveSlot_StoresMarkerNameInSave()
        {
            saveRoot = Path.Combine(Path.GetTempPath(), "health-tests-saves-" + System.Guid.NewGuid());
            SaveSystem.Root = saveRoot;
            BuildWorld(0f);
            hero.Health.SaveSlot = 2;
            Kill();
            hero.Health.RespawnAt(MakeMarker("Bench Two", new Vector2(4f, 3f)));

            var loaded = SaveSystem.LoadOrDefault(2);
            Assert.AreEqual("Bench Two", loaded.respawnMarker);
            Assert.AreEqual(loaded.maxHealth, loaded.health, "full health is what was saved");
        }

        [Test]
        public void Respawn_WithoutSaveSlot_WritesNoFile()
        {
            saveRoot = Path.Combine(Path.GetTempPath(), "health-tests-saves-" + System.Guid.NewGuid());
            SaveSystem.Root = saveRoot;
            BuildWorld(0f);
            hero.Health.RespawnAt(MakeMarker("Bench", new Vector2(4f, 3f)));
            Assert.IsFalse(Directory.Exists(saveRoot));
        }

        [Test]
        public void RespawnAt_WithNullMarker_DoesNothing()
        {
            BuildWorld(0f);
            Settle();
            Assert.IsTrue(Hit(2, -2f));
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "[Hero] RespawnAt needs a marker.");
            hero.Health.RespawnAt(null);
            Assert.AreEqual(3, hero.Health.Health);
            Assert.AreEqual(0, respawnedCount);
        }

        [Test]
        public void RespawnMarker_FindsEnabledMarkerByName_AndFallsBackToObjectName()
        {
            BuildWorld(0f);
            var named = MakeMarker("Alpha", Vector2.zero);
            var go = new GameObject("Unnamed Marker");
            spawned.Add(go);
            var plain = go.AddComponent<RespawnMarker>();
            Assert.AreEqual("Unnamed Marker", plain.MarkerName);
            Assert.AreSame(named, RespawnMarker.Find("Alpha"));
            Assert.AreSame(plain, RespawnMarker.Find("Unnamed Marker"));
            named.enabled = false;
            Assert.IsNull(RespawnMarker.Find("Alpha"));
            Assert.IsNull(RespawnMarker.Find(null));
        }

        // ------------------------------------------------------------------ extras: owner rulings

        [Test]
        public void HitStop_StaysOff_TimeScaleUntouched_AndStubIsLogged()
        {
            BuildWorld(0f);
            Settle();
            float before = Time.timeScale;
            Assert.IsTrue(Hit(1, -2f));
            Step(5);
            Assert.AreEqual(before, Time.timeScale, "no hit-stop on damage");
            CollectionAssert.Contains(new List<string>(Stubs.Reported), "Hero damage hit-stop");
        }

        [Test]
        public void OwnAttack_NeverHurtsTheHero()
        {
            BuildWorld(0f);
            Settle();
            // A dummy big enough to overlap both the hero and its swing box, so the swing lands a real hit.
            var dummy = MakeTarget(new Vector2(0.8f, 0.5f), new Vector2(3f, 1f), typeof(HealthManager), typeof(TrainingDummy))
                .GetComponent<TrainingDummy>();
            input.AttackButton.Press();
            Step(1);
            input.AttackButton.Release();
            Step(Steps(config.AttackActiveSeconds));
            Assert.AreEqual(1, dummy.HitCount, "the swing did hit the dummy");

            Assert.AreEqual(5, hero.Health.Health);
            Assert.IsFalse(hero.Health.IsInvulnerable);
            Assert.AreEqual(0, damagedCount);

            // Hit instances naming the hero (or its hitbox child) as source are refused outright.
            Assert.IsFalse(hero.Health.TakeHit(new HitInstance { Damage = 1, Source = hero.gameObject }));
            Assert.IsFalse(hero.Health.TakeHit(new HitInstance { Damage = 1, Source = hero.Attack.Hitbox.gameObject }));
            Assert.AreEqual(5, hero.Health.Health);
        }

        [Test]
        public void HeroHealth_IsHittable()
        {
            BuildWorld(0f);
            IHittable hittable = hero.Health;
            Assert.IsNotNull(hittable);
            Assert.IsTrue(hittable.TakeHit(new HitInstance { Damage = 1 }));
        }

        [Test]
        public void Hit_LogsUnbuiltFeatureStubs()
        {
            BuildWorld(0f);
            Settle();
            Kill();
            var reported = new List<string>(Stubs.Reported);
            CollectionAssert.Contains(reported, "Hero damage effects and sound");
            CollectionAssert.Contains(reported, "Hero death animation and corpse");
            CollectionAssert.Contains(reported, "Hero death screen");
            CollectionAssert.Contains(reported, "Hero silk loss and cocoon on death");
        }
    }
}
