using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // The enemy core with a real hero: the needle hits it, it hurts the hero on touch. Same deterministic setup as
    // HazardZoneTests/HeroAttackTests: Script-mode physics, explicit StepFixed + Simulate pairs, and the enemy's
    // own Tick called by hand (FixedUpdate does not follow manual stepping).
    public class EnemyCoreTests
    {
        const float dt = 0.02f;

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
            stubsPath = Path.Combine(Path.GetTempPath(), "enemy-core-stubs-" + System.Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            PersistentBool.ResetStatics();

            heroConfig = ScriptableObject.CreateInstance<HeroConfig>();
            enemyConfig = ScriptableObject.CreateInstance<SilksongEnemyConfig>();
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
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            PersistentBool.ResetStatics();
            Stubs.ResetForTests();
            Stubs.StubsDocPath = null;
            if (File.Exists(stubsPath)) File.Delete(stubsPath);
        }

        T Track<T>(T o) where T : Object { spawned.Add(o); return o; }

        EnemyHealth MakeEnemy(Vector2 center, bool withSprite = false)
        {
            var go = Track(new GameObject("Enemy"));
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 1f);
            box.isTrigger = true;
            if (withSprite)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            }
            var health = go.AddComponent<EnemyHealth>();
            var contact = go.AddComponent<EnemyContactDamage>();
            contacts.Add(contact);
            go.AddComponent<EnemyHitFlash>().ManualStepping = true;
            health.Configure(enemyConfig);
            Physics2D.SyncTransforms();
            return health;
        }

        // An enemy standing on the hero's body.
        EnemyHealth MakeEnemyOnHero() => MakeEnemy(new Vector2(0f, 0.5f));

        // An enemy in reach of a right-facing swing.
        EnemyHealth MakeEnemyInReach() => MakeEnemy(new Vector2(1.0f, 0.5f));

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

        static int Steps(float seconds) => Mathf.RoundToInt(seconds / dt);

        void Swing()
        {
            input.AttackButton.Release();
            input.AttackButton.Press();
            Step(1);
        }

        HitInstance HitFrom(float sourceX, int damage = 1, AttackDirection dir = AttackDirection.Normal)
        {
            var src = Track(new GameObject("Source"));
            src.transform.position = new Vector3(sourceX, 0f, 0f);
            return new HitInstance { Damage = damage, Direction = dir, Source = src };
        }

        [Test]
        public void NailHit_ReducesHealth_AndDoesNotHurtTheHero()
        {
            var enemy = MakeEnemyInReach();
            Step(Steps(0.2f));
            Assert.AreEqual(2, enemy.Health);

            Swing();
            Step(Steps(0.1f));

            Assert.AreEqual(1, enemy.Health, "one needle hit costs the damage of the hit (1)");
            Assert.IsFalse(enemy.IsDead);
            Assert.AreEqual(hero.Health.MaxHealth, hero.Health.Health, "the swing touching the enemy must not hurt the hero");
        }

        [Test]
        public void TwoNailHits_Kill_WithDefaultConfig_AndDiedFiresOnce()
        {
            var enemy = MakeEnemyInReach();
            int died = 0;
            enemy.Died += () => died++;
            Step(Steps(0.2f));

            Swing();
            Step(Steps(heroConfig.AttackActiveSeconds + heroConfig.AttackCooldownSeconds));
            Assert.AreEqual(1, enemy.Health);
            Swing();
            Step(Steps(0.1f));

            Assert.IsTrue(enemy.IsDead);
            Assert.AreEqual(0, enemy.Health);
            Assert.AreEqual(1, died);
            Step(Steps(0.5f));
            Assert.AreEqual(1, died, "no second Died");
        }

        [Test]
        public void Events_HitAndShoved_PointAwayFromTheSource()
        {
            var enemy = MakeEnemy(new Vector2(5f, 3f));
            enemyConfig.maxHealth = 10;
            enemy.Configure(enemyConfig);
            var hits = new List<HitInstance>();
            var shoves = new List<Vector2>();
            enemy.Hit += h => hits.Add(h);
            enemy.Shoved += (v, s) => shoves.Add(new Vector2(v, s));

            Assert.IsTrue(enemy.TakeHit(HitFrom(2f)));
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(1, shoves.Count);
            Assert.AreEqual(15f, shoves[0].x, 1e-4f, "source on the left: shoved right at 15");
            Assert.AreEqual(0.15f, shoves[0].y, 1e-4f);

            Assert.IsTrue(enemy.TakeHit(HitFrom(9f)));
            Assert.AreEqual(2, shoves.Count);
            Assert.AreEqual(-15f, shoves[1].x, 1e-4f, "source on the right: shoved left");
        }

        [Test]
        public void UpAndDownHits_DoNotShove()
        {
            var enemy = MakeEnemy(new Vector2(5f, 3f));
            enemyConfig.maxHealth = 10;
            enemy.Configure(enemyConfig);
            int shoves = 0, hits = 0;
            enemy.Shoved += (v, s) => shoves++;
            enemy.Hit += _ => hits++;
            enemy.TakeHit(HitFrom(2f, 1, AttackDirection.Up));
            enemy.TakeHit(HitFrom(2f, 1, AttackDirection.Down));
            Assert.AreEqual(2, hits);
            Assert.AreEqual(0, shoves);
        }

        [Test]
        public void NoInvulnerability_TwoQuickHitsBothLand()
        {
            var enemy = MakeEnemy(new Vector2(5f, 3f));
            Assert.IsTrue(enemy.TakeHit(HitFrom(2f)));
            Assert.IsTrue(enemy.TakeHit(HitFrom(2f)), "no window after a hit");
            Assert.AreEqual(0, enemy.Health);
            Assert.IsTrue(enemy.IsDead);
        }

        [Test]
        public void ZeroDamageHit_IsRefused()
        {
            var enemy = MakeEnemy(new Vector2(5f, 3f));
            int hits = 0;
            enemy.Hit += _ => hits++;
            Assert.IsFalse(enemy.TakeHit(HitFrom(2f, 0)));
            Assert.AreEqual(0, hits);
            Assert.AreEqual(2, enemy.Health);
        }

        [Test]
        public void DeadEnemy_RefusesFurtherHits()
        {
            var enemy = MakeEnemy(new Vector2(5f, 3f));
            int hits = 0, died = 0, shoves = 0;
            enemy.Hit += _ => hits++;
            enemy.Died += () => died++;
            enemy.Shoved += (v, s) => shoves++;
            enemy.TakeHit(HitFrom(2f));
            enemy.TakeHit(HitFrom(2f));
            Assert.IsTrue(enemy.IsDead);
            Assert.AreEqual(2, hits);
            Assert.AreEqual(2, shoves, "the killing blow shoves too");

            Assert.IsFalse(enemy.TakeHit(HitFrom(2f)));
            Assert.AreEqual(2, hits);
            Assert.AreEqual(1, died);
            Assert.AreEqual(0, enemy.Health);
        }

        [Test]
        public void Death_HidesRenderersAndColliders_AndRemovesTheObject()
        {
            var enemy = MakeEnemy(new Vector2(5f, 3f), withSprite: true);
            var sr = enemy.GetComponent<SpriteRenderer>();
            var box = enemy.GetComponent<Collider2D>();
            bool hiddenInHandler = false;
            enemy.Died += () => hiddenInHandler = !sr.enabled && !box.enabled;
            enemy.TakeHit(HitFrom(2f, 5));
            Assert.IsTrue(hiddenInHandler, "renderer and collider are off before Died is raised");
            Assert.IsFalse(enemy.gameObject.activeSelf, "default remove delay 0 disables the object at once");
        }

        [Test]
        public void Contact_DamagesTheHeroByOne_AndNotAgainInsideTheWindow()
        {
            MakeEnemyOnHero();
            Assert.AreEqual(5, hero.Health.Health);
            Step(2);
            Assert.AreEqual(4, hero.Health.Health, "touching costs 1");

            Step(Steps(heroConfig.HurtInvulnerableSeconds * 0.5f));
            Assert.AreEqual(4, hero.Health.Health, "the hero's invulnerability is the only cooldown");
        }

        [Test]
        public void DyingEnemy_StopsDealingContactDamage()
        {
            var enemy = MakeEnemyOnHero();
            var contact = enemy.GetComponent<EnemyContactDamage>();
            int damageSeenInDiedHandler = -1;
            enemy.Died += () => damageSeenInDiedHandler = contact.DamageDealt;

            // Killed before the first physics step, while it overlaps the hero.
            enemy.TakeHit(HitFrom(-3f, 5));
            Assert.AreEqual(0, damageSeenInDiedHandler, "damage is zero by the time Died is raised");
            Assert.AreEqual(0, contact.DamageDealt);

            Step(Steps(0.3f));
            Assert.AreEqual(5, hero.Health.Health, "a dead enemy never hurts");
        }

        [Test]
        public void Contact_StopsAfterDeath_EvenOncePreviousWindowHasEnded()
        {
            var enemy = MakeEnemyOnHero();
            Step(2);
            Assert.AreEqual(4, hero.Health.Health);

            enemy.TakeHit(HitFrom(-3f, 5));
            Step(Steps(heroConfig.HurtInvulnerableSeconds * 1.5f));
            Assert.AreEqual(4, hero.Health.Health, "no hit after death");
        }

        [Test]
        public void Flash_RunsAndEnds()
        {
            var enemy = MakeEnemy(new Vector2(5f, 3f), withSprite: true);
            enemyConfig.maxHealth = 10;
            enemy.Configure(enemyConfig);
            var flash = enemy.GetComponent<EnemyHitFlash>();
            var sr = enemy.GetComponent<SpriteRenderer>();
            var baseColor = sr.color;
            Assert.IsFalse(flash.IsFlashing);

            enemy.TakeHit(HitFrom(2f));
            Assert.IsTrue(flash.IsFlashing);
            Assert.AreEqual(0.85f, flash.CurrentStrength, 1e-4f);
            Assert.Greater(sr.color.r, baseColor.r, "tinted toward white");

            flash.Advance(0.06f);
            Assert.IsTrue(flash.IsFlashing, "still in the 0.07 s hold");
            Assert.AreEqual(0.85f, flash.CurrentStrength, 1e-4f);

            flash.Advance(0.04f);
            Assert.IsTrue(flash.IsFlashing);
            Assert.Less(flash.CurrentStrength, 0.85f, "falling");
            Assert.Greater(flash.CurrentStrength, 0f);

            flash.Advance(0.05f);
            Assert.IsFalse(flash.IsFlashing, "over after hold + fall = 0.12 s");
            Assert.AreEqual(0f, flash.CurrentStrength);
            Assert.AreEqual(baseColor.r, sr.color.r, 1e-4f, "colour restored");
        }

        [Test]
        public void Flash_WithoutSpriteRenderer_IsANoOp()
        {
            var enemy = MakeEnemy(new Vector2(5f, 3f));
            enemyConfig.maxHealth = 10;
            enemy.Configure(enemyConfig);
            var flash = enemy.GetComponent<EnemyHitFlash>();
            Assert.DoesNotThrow(() => enemy.TakeHit(HitFrom(2f)));
            Assert.IsFalse(flash.IsFlashing);
            Assert.DoesNotThrow(() => flash.Advance(1f));
        }
    }
}
