using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // What the hero feels when its needle lands on something (docs/study/14e-hit-feel.md): silk, sideways recoil,
    // up-hit rise cap, downspike bounce and the 2-step enemy-contact grace. Same deterministic setup as
    // EnemyCoreTests: Script-mode physics, StepFixed + Simulate pairs, the enemy's Tick called by hand.
    // Floor top is y = 0; the hero stands at the origin facing right.
    public class HeroHitFeelTests
    {
        const float dt = 0.02f;

        // A hittable with no IHitFeedbackTarget, like a wall or a dummy.
        class PlainHittable : MonoBehaviour, IHittable
        {
            public bool TakeHit(HitInstance hit) => true;
        }

        readonly List<Object> spawned = new List<Object>();
        readonly List<EnemyContactDamage> contacts = new List<EnemyContactDamage>();
        SimulationMode2D savedMode;
        HeroConfig heroConfig;
        SilksongEnemyConfig enemyConfig;
        FakeHeroInput input;
        HeroController hero;
        string stubsPath;

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            stubsPath = Path.Combine(Path.GetTempPath(), "hitfeel-stubs-" + System.Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
            PersistentBool.ResetStatics();

            heroConfig = ScriptableObject.CreateInstance<HeroConfig>();
            enemyConfig = ScriptableObject.CreateInstance<SilksongEnemyConfig>();
            enemyConfig.maxHealth = 50;   // many hits survive: these tests are about the hero
            input = new FakeHeroInput();

            var ground = Track(new GameObject("Ground"));
            ground.AddComponent<BoxCollider2D>().size = new Vector2(400f, 1f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);

            var go = Track(new GameObject("Hero"));
            go.transform.position = new Vector3(0f, 0.51f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 1f);
            hero = go.AddComponent<HeroController>();
            hero.ManualStepping = true;
            hero.Config = heroConfig;
            hero.Input = input;
            hero.Health.DeathRespawnDelaySeconds = -1f;
            Physics2D.SyncTransforms();
            Step(10);   // settle on the floor
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in spawned) if (o != null) Object.DestroyImmediate(o);
            spawned.Clear();
            contacts.Clear();
            if (heroConfig != null) Object.DestroyImmediate(heroConfig);
            if (enemyConfig != null) Object.DestroyImmediate(enemyConfig);
            Physics2D.simulationMode = savedMode;
            PersistentBool.ResetStatics();
            Stubs.ResetForTests();
            Stubs.StubsDocPath = null;
            if (File.Exists(stubsPath)) File.Delete(stubsPath);
        }

        T Track<T>(T o) where T : Object { spawned.Add(o); return o; }

        EnemyHealth MakeEnemy(Vector2 center, Vector2? size = null)
        {
            var go = Track(new GameObject("Enemy"));
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = size ?? Vector2.one;
            box.isTrigger = true;
            var health = go.AddComponent<EnemyHealth>();
            var contact = go.AddComponent<EnemyContactDamage>();
            contacts.Add(contact);
            health.Configure(enemyConfig);
            Physics2D.SyncTransforms();
            return health;
        }

        // In reach of a right-facing swing, not touching the hero's body.
        EnemyHealth MakeEnemyInReach() => MakeEnemy(new Vector2(1.0f, 0.5f));

        GameObject MakePlain(Vector2 center)
        {
            var go = Track(new GameObject("Plain"));
            go.transform.position = center;
            go.AddComponent<BoxCollider2D>().isTrigger = true;
            go.AddComponent<PlainHittable>();
            Physics2D.SyncTransforms();
            return go;
        }

        void Step(int count)
        {
            for (int i = 0; i < count; i++)
            {
                hero.StepFixed(dt);
                Physics2D.Simulate(dt);
                foreach (var c in contacts) if (c != null && c.isActiveAndEnabled) c.Tick();
                input.EndFixedStep(dt);
            }
        }

        void Swing(Vector2 move)
        {
            input.Move = move;
            input.AttackButton.Release();
            input.AttackButton.Press();
            Step(1);
        }

        void LiftHero(float y, float velocityY)
        {
            hero.transform.position = new Vector3(0f, y, 0f);
            var body = hero.GetComponent<Rigidbody2D>();
            body.position = new Vector2(0f, y);
            body.linearVelocity = new Vector2(0f, velocityY);
            Physics2D.SyncTransforms();
        }

        // ------------------------------------------------------------------ silk

        [Test]
        public void LandedNailHit_OnEnemy_GivesOneSilk()
        {
            MakeEnemyInReach();
            Assert.AreEqual(0, hero.Health.Data.silk, "test setup");
            Swing(Vector2.zero);
            Assert.AreEqual(1, hero.Health.Data.silk);
        }

        [Test]
        public void Silk_CapsAtMaxSilk()
        {
            hero.Health.Data.silk = hero.Health.Data.maxSilk;
            MakeEnemyInReach();
            Swing(Vector2.zero);
            Assert.AreEqual(hero.Health.Data.maxSilk, hero.Health.Data.silk);
        }

        [Test]
        public void SilkAmount_FollowsTheConfig()
        {
            heroConfig.SilkPerNailHit = 3;
            MakeEnemyInReach();
            Swing(Vector2.zero);
            Assert.AreEqual(3, hero.Health.Data.silk);
        }

        [Test]
        public void SwingThatHitsNothing_GivesNoSilk()
        {
            Swing(Vector2.zero);
            Assert.AreEqual(0, hero.Health.Data.silk);
        }

        [Test]
        public void SameEnemyInOneSwing_GivesSilkOnce()
        {
            MakeEnemyInReach();
            Swing(Vector2.zero);
            Step(10);   // the box stays live; the same enemy is not hit twice in one swing
            Assert.AreEqual(1, hero.Health.Data.silk);
        }

        [Test]
        public void HitOnBreakableWall_GivesNoSilkAndNoRecoil()
        {
            var go = Track(new GameObject("Wall"));
            go.transform.position = new Vector3(1.2f, 0.5f, 0f);
            go.AddComponent<BoxCollider2D>().size = Vector2.one;   // solid
            var wall = go.AddComponent<BreakableWall>();
            wall.Configure("hitfeel-wall");
            Physics2D.SyncTransforms();

            Swing(Vector2.zero);
            Assert.IsTrue(wall.IsBroken, "test setup: the swing reached the wall");
            Assert.AreEqual(0, hero.Health.Data.silk);
            Assert.IsFalse(hero.Attack.IsRecoiling);
            Assert.AreEqual(0f, hero.Velocity.x, 0.01f);
        }

        [Test]
        public void HitOnTargetWithoutFeedbackHook_GivesNoSilkAndNoRecoil()
        {
            MakePlain(new Vector2(1.0f, 0.5f));
            int hits = 0;
            hero.Attack.HitLanded += (_, __) => hits++;
            Swing(Vector2.zero);
            Assert.AreEqual(1, hits, "test setup: the hit landed");
            Assert.AreEqual(0, hero.Health.Data.silk);
            Assert.IsFalse(hero.Attack.IsRecoiling);
            Assert.AreEqual(0f, hero.Velocity.x, 0.01f);
        }

        [Test]
        public void SwingAtADeadEnemy_GivesNoSilk()
        {
            var enemy = MakeEnemyInReach();
            enemy.TakeHit(new HitInstance { Damage = 999, Direction = AttackDirection.Normal, Source = hero.gameObject });
            Assert.IsTrue(enemy.IsDead);
            Swing(Vector2.zero);
            Assert.AreEqual(0, hero.Health.Data.silk);
        }

        // ------------------------------------------------------------------ sideways recoil

        [Test]
        public void SidewaysHit_PushesHeroAwayForEightSteps_ThenControlReturns()
        {
            MakeEnemyInReach();
            // The player keeps holding right (toward the enemy); the push must win for its steps.
            Swing(new Vector2(1f, 0f));
            Assert.IsTrue(hero.Attack.IsRecoiling);
            Assert.AreEqual(-heroConfig.NailRecoilSpeed, hero.Velocity.x, 0.01f, "step 1 (the hit step)");
            for (int i = 2; i <= heroConfig.NailRecoilSteps; i++)
            {
                Step(1);
                Assert.AreEqual(-heroConfig.NailRecoilSpeed, hero.Velocity.x, 0.01f, "push step " + i);
            }
            Assert.IsFalse(hero.Attack.IsRecoiling);
            Step(1);
            Assert.AreEqual(heroConfig.RunSpeed, hero.Velocity.x, 0.01f, "run input is back");
        }

        [Test]
        public void SidewaysHit_FacingLeft_PushesRight()
        {
            MakeEnemy(new Vector2(-1.0f, 0.5f));
            Swing(new Vector2(-1f, 0f));
            Assert.AreEqual(heroConfig.NailRecoilSpeed, hero.Velocity.x, 0.01f);
        }

        [Test]
        public void SidewaysRecoil_StepCountAndSpeedFollowTheConfig()
        {
            heroConfig.NailRecoilSteps = 3;
            heroConfig.NailRecoilSpeed = 6f;
            MakeEnemyInReach();
            Swing(Vector2.zero);
            Assert.AreEqual(-6f, hero.Velocity.x, 0.01f);
            Step(2);
            Assert.AreEqual(-6f, hero.Velocity.x, 0.01f);
            Step(1);
            Assert.AreEqual(0f, hero.Velocity.x, 0.01f, "three steps are over");
        }

        [Test]
        public void SidewaysRecoil_EndsWhenTheHeroIsHurt()
        {
            MakeEnemyInReach();
            Swing(Vector2.zero);
            Assert.IsTrue(hero.Attack.IsRecoiling);
            Assert.IsTrue(hero.Health.TakeHit(new HitInstance { Damage = 1, Direction = AttackDirection.Normal, Source = Track(new GameObject("Spike")) }));
            Assert.IsFalse(hero.Attack.IsRecoiling);
            Step(1);
            Assert.AreEqual(HeroState.Hurt, hero.State);
            Assert.IsFalse(hero.Attack.IsRecoiling);
        }

        [Test]
        public void SidewaysRecoil_DoesNotOverrideADash()
        {
            MakeEnemyInReach();
            Swing(Vector2.zero);
            Assert.IsTrue(hero.Attack.IsRecoiling);
            input.Move = new Vector2(-1f, 0f);   // dash AWAY from the enemy: dashing into it hurts the hero and cancels the dash
            input.DashButton.Release();
            input.DashButton.Press();
            Step(1);
            string diag = " state=" + hero.State + " health=" + hero.Health.Health + "/" + hero.Health.MaxHealth + " grounded=" + hero.IsGrounded + " x=" + hero.transform.position.x;
            Assert.IsTrue(hero.Dash.IsDashing, "test setup: the dash started" + diag);
            Assert.IsFalse(hero.Attack.IsRecoiling);
            Assert.Less(hero.Velocity.x, -(heroConfig.NailRecoilSpeed + 1f), "the dash owns the velocity" + diag);
        }

        [Test]
        public void DeadHero_DoesNotSwingSoGetsNoSilkOrRecoil()
        {
            hero.Health.TakeHit(new HitInstance { Damage = 99, Direction = AttackDirection.Normal, Source = Track(new GameObject("Spike")) });
            Assert.IsTrue(hero.Health.IsDead, "test setup");
            MakeEnemyInReach();
            Swing(Vector2.zero);
            Assert.IsFalse(hero.Attack.IsRecoiling);
            Assert.AreEqual(0, hero.Health.Data.silk);
        }

        // ------------------------------------------------------------------ up hit

        [Test]
        public void UpHit_OnEnemy_CutsARiseToZero()
        {
            LiftHero(5.51f, 12f);
            MakeEnemy(new Vector2(0f, 5.51f + 1.2f));
            Swing(new Vector2(0f, 1f));
            Assert.AreEqual(AttackDirection.Up, hero.Attack.CurrentDirection);
            Assert.AreEqual(1, hero.Health.Data.silk, "test setup: the hit landed");
            Assert.AreEqual(0f, hero.Velocity.y, 0.01f);
        }

        [Test]
        public void UpHit_WhileFalling_DoesNotChangeTheFall()
        {
            LiftHero(5.51f, -6f);
            MakeEnemy(new Vector2(0f, 5.51f + 1.2f));
            Swing(new Vector2(0f, 1f));
            Assert.AreEqual(1, hero.Health.Data.silk, "test setup: the hit landed");
            Assert.Less(hero.Velocity.y, -5f);
        }

        [Test]
        public void UpHit_OnTargetWithoutFeedbackHook_KeepsRising()
        {
            LiftHero(5.51f, 12f);
            MakePlain(new Vector2(0f, 5.51f + 1.2f));
            Swing(new Vector2(0f, 1f));
            Assert.Greater(hero.Velocity.y, 5f);
        }

        // ------------------------------------------------------------------ downspike

        [Test]
        public void DownHit_OnEnemy_BouncesAndGivesSilk()
        {
            LiftHero(5.51f, -4f);
            Step(1);
            MakeEnemy(new Vector2(0f, 5.0f), new Vector2(1f, 3f));
            Swing(new Vector2(0f, -1f));
            Assert.AreEqual(AttackDirection.Down, hero.Attack.CurrentDirection);
            Assert.IsTrue(hero.Attack.IsBouncing);
            Assert.AreEqual(heroConfig.PogoBounceSpeed, hero.Velocity.y, 0.01f);
            Assert.AreEqual(1, hero.Health.Data.silk);
            // The air dash is given back by the bounce (existing behaviour); the double jump does not exist yet,
            // so its stub still logs.
            CollectionAssert.Contains(new List<string>(Stubs.Reported), "Hero bounce refreshes double jump");
        }

        [Test]
        public void DownHit_OnTargetWithoutFeedbackHook_KeepsTheOldPogo_NoSilk()
        {
            LiftHero(5.51f, -4f);
            Step(1);
            MakePlain(new Vector2(0f, 5.3f));
            Swing(new Vector2(0f, -1f));
            Assert.IsTrue(hero.Attack.IsBouncing, "any hittable without an active NonBouncer still pogoes");
            Assert.AreEqual(0, hero.Health.Data.silk);
        }

        [Test]
        public void DownHit_OnEnemyWithActiveNonBouncer_DoesNotBounce_ButStillGivesSilk()
        {
            LiftHero(5.51f, -4f);
            Step(1);
            var enemy = MakeEnemy(new Vector2(0f, 5.0f), new Vector2(1f, 3f));
            enemy.gameObject.AddComponent<NonBouncer>();
            Swing(new Vector2(0f, -1f));
            Assert.IsFalse(hero.Attack.IsBouncing);
            Assert.IsFalse(hero.Health.IsInContactGrace, "no bounce, no grace");
            Assert.AreEqual(1, hero.Health.Data.silk);
        }

        [Test]
        public void DownHit_GivesTwoStepsOfEnemyContactGrace_ThenTheEnemyHurts()
        {
            LiftHero(5.51f, -4f);
            Step(1);
            // A tall enemy under and overlapping the hero's body, spawned just before the swing step.
            MakeEnemy(new Vector2(0f, 4.5f), new Vector2(1f, 3f));
            int max = hero.Health.MaxHealth;

            Swing(new Vector2(0f, -1f));   // the downward hit; grace starts
            Assert.IsTrue(hero.Health.IsInContactGrace);
            Assert.AreEqual(max, hero.Health.Health, "contact ignored on the hit step");
            Step(1);
            Assert.AreEqual(max, hero.Health.Health, "contact ignored on the next step");
            Assert.IsTrue(hero.Health.IsInContactGrace);
            Step(1);
            Assert.IsFalse(hero.Health.IsInContactGrace);
            Assert.AreEqual(max - 1, hero.Health.Health, "grace over: the enemy hurts");
        }

        [Test]
        public void ContactGrace_DoesNotBlockHazards()
        {
            LiftHero(5.51f, -4f);
            Step(1);
            MakeEnemy(new Vector2(0f, 4.5f), new Vector2(1f, 3f));
            Swing(new Vector2(0f, -1f));
            Assert.IsTrue(hero.Health.IsInContactGrace);
            // Spikes call HeroHealth.TakeHit directly (HazardZone), which the grace does not touch.
            bool landed = hero.Health.TakeHit(new HitInstance { Damage = 1, Direction = AttackDirection.Normal, Source = Track(new GameObject("Spikes")) });
            Assert.IsTrue(landed);
            Assert.AreEqual(hero.Health.MaxHealth - 1, hero.Health.Health);
        }

        [Test]
        public void SidewaysHit_GivesNoContactGrace()
        {
            MakeEnemyInReach();
            Swing(Vector2.zero);
            Assert.IsFalse(hero.Health.IsInContactGrace);
        }

        [Test]
        public void ContactGraceLength_FollowsTheConfig()
        {
            heroConfig.DownspikeGraceSteps = 5;
            LiftHero(5.51f, -4f);
            Step(1);
            MakeEnemy(new Vector2(0f, 4.5f), new Vector2(1f, 3f));
            Swing(new Vector2(0f, -1f));
            Assert.AreEqual(5, hero.Health.ContactGraceStepsRemaining);
        }
    }
}
