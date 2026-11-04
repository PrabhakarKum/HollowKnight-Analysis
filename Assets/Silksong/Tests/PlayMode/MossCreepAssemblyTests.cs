using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // MossCreep.Create on plain code-built ground, with a real hero body, without the generated rooms. Script-mode
    // physics and explicit Tick + Simulate pairs, like EnemyWalkerTests / HazardZoneTests.
    public class MossCreepAssemblyTests
    {
        const float dt = 0.02f;
        readonly List<Object> spawned = new List<Object>();
        SimulationMode2D savedMode;
        string stubsPath;
        SilksongEnemyConfig config;
        HeroConfig heroConfig;
        HeroController hero;

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            PersistentBool.ResetStatics();
            stubsPath = Path.Combine(Path.GetTempPath(), "mosscreep-tests-stubs-" + System.Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
            config = ScriptableObject.CreateInstance<SilksongEnemyConfig>();
            heroConfig = ScriptableObject.CreateInstance<HeroConfig>();

            var ground = Track(new GameObject("Ground"));
            ground.AddComponent<BoxCollider2D>().size = new Vector2(40f, 1f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in spawned) if (o != null) Object.DestroyImmediate(o);
            spawned.Clear();
            if (config != null) Object.DestroyImmediate(config);
            if (heroConfig != null) Object.DestroyImmediate(heroConfig);
            hero = null;
            PersistentBool.ResetStatics();
            Physics2D.simulationMode = savedMode;
            Stubs.StubsDocPath = null;
            Stubs.ResetForTests();
            if (File.Exists(stubsPath)) File.Delete(stubsPath);
        }

        T Track<T>(T o) where T : Object { spawned.Add(o); return o; }

        void MakeHero(float x)
        {
            var go = Track(new GameObject("Hero"));
            go.transform.position = new Vector3(x, 0.51f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 1f);
            hero = go.AddComponent<HeroController>();
            hero.ManualStepping = true;
            hero.Config = heroConfig;
            hero.Input = new FakeHeroInput();
            hero.Health.DeathRespawnDelaySeconds = -1f;
            Physics2D.SyncTransforms();
        }

        MossCreep MakeCreep(float x, bool facingRight, string id = "Test_MossCreep_1")
        {
            var creep = MossCreep.Create(config, id, new Vector2(x, MossCreep.RootYOnFloor(config, 0f)), facingRight);
            Track(creep.gameObject);
            creep.Walker.ManualStepping = true;
            Physics2D.SyncTransforms();
            return creep;
        }

        void Step(MossCreep creep, int count)
        {
            for (int i = 0; i < count; i++)
            {
                creep.Walker.Tick(dt);
                Physics2D.Simulate(dt);
                creep.Contact.Tick();
            }
        }

        [Test]
        public void Create_BuildsEveryPiece_FromTheConfig()
        {
            config.maxHealth = 3;
            config.contactDamage = 2;
            config.walkSpeed = 4f;
            config.staysDead = false;
            var creep = MakeCreep(0f, true);

            Assert.IsTrue(creep.IsAssembled);
            Assert.AreEqual(3, creep.Health.Health);
            Assert.AreEqual(3, creep.Health.MaxHealth);
            Assert.AreEqual(2, creep.Contact.DamageDealt);
            Assert.AreEqual(4f, creep.Walker.Params.WalkSpeed);
            Assert.AreEqual(config.gravityScale, creep.Body.gravityScale);
            Assert.AreEqual(config.mass, creep.Body.mass);
            Assert.AreEqual(RigidbodyType2D.Dynamic, creep.Body.bodyType);
            Assert.AreEqual("Test_MossCreep_1", creep.Persistence.Id);
            Assert.IsFalse(creep.Persistence.StaysDead, "stays-dead comes from the config");
            Assert.IsNotNull(creep.Flash);

            Assert.IsFalse(creep.SolidCollider.isTrigger);
            Assert.IsTrue(creep.ContactTrigger.isTrigger);
            Assert.AreEqual(config.colliderSize, creep.SolidCollider.size);
            Assert.AreEqual(config.colliderOffset, creep.SolidCollider.offset);
            Assert.AreEqual(config.colliderSize, creep.ContactTrigger.size);
            Assert.AreEqual(2, creep.GetComponents<Collider2D>().Length, "no collider on the placeholder child");
            Assert.AreEqual(0, creep.GetComponentsInChildren<Collider2D>().Length - 2);

            var renderers = creep.GetComponentsInChildren<SpriteRenderer>();
            Assert.GreaterOrEqual(renderers.Length, 1, "a visible placeholder");
            foreach (var r in renderers) Assert.IsTrue(r.enabled);
        }

        [Test]
        public void Create_WithoutAnExplicitId_UsesThePrefixAndTheObjectName()
        {
            config.persistenceIdPrefix = "crawler";
            var creep = MakeCreep(0f, true, null);
            Assert.AreEqual("crawler-MossCreep", creep.Persistence.Id);
        }

        [Test]
        public void ConfigSpeed_IsTheSpeedItWalksAt()
        {
            config.walkSpeed = 5f;
            var creep = MakeCreep(0f, true);
            Step(creep, 20);   // settle onto the floor
            creep.Walker.Tick(dt);   // what the walker commands, before the physics step lets friction take a little off
            Assert.AreEqual(5f, Mathf.Abs(creep.Body.linearVelocity.x), 0.01f);
        }

        [Test]
        public void Shove_And_Died_ReachTheWalker()
        {
            var creep = MakeCreep(0f, true);
            var source = Track(new GameObject("Source"));
            source.transform.position = new Vector3(-3f, 0.5f, 0f);

            creep.Health.TakeHit(new HitInstance { Damage = 1, Direction = AttackDirection.Normal, Source = source });
            Assert.IsTrue(creep.Walker.Brain.IsShoved, "a sideways hit shoves the walker");
            Assert.AreNotEqual(EnemyState.Dead, creep.Walker.State);

            creep.Health.TakeHit(new HitInstance { Damage = 1, Direction = AttackDirection.Normal, Source = source });
            Assert.IsTrue(creep.Health.IsDead);
            Assert.AreEqual(EnemyState.Dead, creep.Walker.State, "death stops the walker");
            Assert.AreEqual(0, creep.Contact.DamageDealt);
        }

        [Test]
        public void TheHero_IsNotBlockedByTheEnemy_NorTreatedAsAWall_ButIsStillHurtOnTouch()
        {
            MakeHero(0f);
            var creep = MakeCreep(4f, false);   // walks left, straight at the hero
            var heroBody = hero.GetComponent<Collider2D>();
            Assert.IsTrue(Physics2D.GetIgnoreCollision(heroBody, creep.SolidCollider), "the solid collider and the hero's body ignore each other");

            int before = hero.Health.Health;
            float startY = hero.transform.position.y;
            float minX = creep.transform.position.x;
            for (int i = 0; i < 200 && creep.transform.position.x > -4f; i++)
            {
                Step(creep, 1);
                minX = Mathf.Min(minX, creep.transform.position.x);
            }
            Assert.Less(minX, -3f, "the enemy walked through the hero's place without turning (hero is not a wall)");
            Assert.AreEqual(-1, creep.Walker.Facing, "still facing left: it never turned");
            Assert.AreEqual(before - config.contactDamage, hero.Health.Health, "touching the hero cost exactly the contact damage once (then invulnerable)");
            Assert.AreEqual(startY, hero.transform.position.y, 0.15f, "the enemy did not lift the hero (its hurt recoil may slide it sideways, but the hero is never stepped here)");
        }
    }
}
