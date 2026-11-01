using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // The test-room damage source. Same deterministic setup as HeroHealthTests (Script-mode physics, explicit
    // StepFixed + Simulate pairs). The zone's own FixedUpdate does not run under manual stepping, so the tests call
    // zone.Tick() after each Simulate, as FixedUpdate would.
    public class HazardZoneTests
    {
        readonly List<Object> spawned = new List<Object>();
        SimulationMode2D savedMode;
        const float dt = 0.02f;
        HeroConfig config;
        FakeHeroInput input;
        HeroController hero;
        HazardZone zone;
        RespawnMarker marker;
        string stubsPath;

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            config = ScriptableObject.CreateInstance<HeroConfig>();
            stubsPath = Path.Combine(Path.GetTempPath(), "hazard-tests-stubs-" + System.Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();

            input = new FakeHeroInput();
            var ground = Track(new GameObject("Ground"));
            ground.AddComponent<BoxCollider2D>().size = new Vector2(400f, 1f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);

            var go = Track(new GameObject("Hero"));
            go.transform.position = new Vector3(0f, 0.51f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 1f);
            hero = go.AddComponent<HeroController>();
            hero.ManualStepping = true;
            hero.Config = config;
            hero.Input = input;
            hero.Health.DeathRespawnDelaySeconds = -1f;

            // Wide and tall, so the knockback cannot carry the hero out of it.
            var zoneGo = Track(new GameObject("Spikes"));
            zoneGo.transform.position = new Vector3(0f, 1f, 0f);
            var box = zoneGo.AddComponent<BoxCollider2D>();
            box.size = new Vector2(100f, 4f);
            box.isTrigger = true;
            zone = zoneGo.AddComponent<HazardZone>();

            var mGo = Track(new GameObject("Marker"));
            mGo.transform.position = new Vector3(-200f, 0.51f, 0f);
            marker = mGo.AddComponent<RespawnMarker>();
            marker.Configure("Start", true);
            Physics2D.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in spawned) if (o != null) Object.DestroyImmediate(o);
            spawned.Clear();
            if (config != null) Object.DestroyImmediate(config);
            Physics2D.simulationMode = savedMode;
            Stubs.StubsDocPath = null;
            if (File.Exists(stubsPath)) File.Delete(stubsPath);
        }

        T Track<T>(T o) where T : Object { spawned.Add(o); return o; }

        void Step(int count)
        {
            for (int i = 0; i < count; i++)
            {
                hero.StepFixed(dt);
                Physics2D.Simulate(dt);
                zone.Tick();
                input.EndFixedStep(dt);
            }
        }

        static int Steps(float seconds) => Mathf.RoundToInt(seconds / dt);

        [Test]
        public void Overlap_DamagesHeroOnce_PerInvulnerabilityWindow()
        {
            int damaged = 0;
            hero.Health.Damaged += () => damaged++;
            Assert.AreEqual(5, hero.Health.Health);

            Step(2);
            Assert.AreEqual(4, hero.Health.Health, "touching the zone costs 1");
            Assert.AreEqual(HazardZone.Damage, 5 - hero.Health.Health);
            Assert.AreEqual(1, damaged);

            // Still overlapping for most of the invulnerability window: no second hit.
            Step(Steps(config.HurtInvulnerableSeconds * 0.8f));
            Assert.AreEqual(4, hero.Health.Health);
            Assert.AreEqual(1, damaged);

            // Once the window has ended, the hero is still inside, so the next hit lands (one more, not many).
            Step(Steps(config.HurtInvulnerableSeconds * 0.4f));
            Assert.AreEqual(3, hero.Health.Health);
            Assert.AreEqual(2, damaged);
        }

        [Test]
        public void KillHit_ThenAutoRespawn_ReturnsHeroToMarker()
        {
            // The same hit the K key sends: damage equal to the current health.
            hero.Health.RespawnAt(marker);
            hero.Health.DeathRespawnDelaySeconds = 1.6f;
            var killer = new HitInstance { Damage = Mathf.Max(1, hero.Health.Health), Direction = AttackDirection.Normal };
            Assert.IsTrue(hero.Health.TakeHit(killer));
            Assert.IsTrue(hero.Health.IsDead);
            Assert.AreEqual(0, hero.Health.Health);

            Step(Steps(1.6f * 0.5f));
            Assert.IsTrue(hero.Health.IsDead, "still dead before the delay");

            Step(Steps(1.6f * 0.5f) + 3);
            Assert.IsFalse(hero.Health.IsDead, "respawned after the delay");
            Assert.AreEqual(hero.Health.MaxHealth, hero.Health.Health);
            Assert.AreEqual(marker.transform.position.x, hero.transform.position.x, 0.5f);
        }

        [Test]
        public void HeroDiesInZone_RespawnsOutside_IsNotHitAtTheMarker()
        {
            // One health left, so the zone's first hit kills the hero while it is still listed inside.
            hero.Health.Data.health = 1;
            Step(2);
            Assert.IsTrue(hero.Health.IsDead, "test setup: the zone killed the hero");

            // The respawn teleports the hero far outside the zone; the zone's next check comes before any physics
            // step could send the exit event.
            hero.Health.RespawnAt(marker);
            zone.Tick();

            Assert.AreEqual(hero.Health.MaxHealth, hero.Health.Health, "a respawned hero must not be hurt at the marker");
            Assert.AreEqual(0, zone.InsideCount, "stale entry pruned");
        }
    }
}
