using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // Task 8: hazard respawn markers, triggers and the spike zone's use of them. Same deterministic setup as
    // HazardZoneTests (Script-mode physics, explicit StepFixed + Simulate + zone.Tick).
    public class HazardRespawnTests
    {
        readonly List<Object> spawned = new List<Object>();
        SimulationMode2D savedMode;
        const float dt = 0.02f;
        HeroConfig config;
        FakeHeroInput input;
        HeroController hero;
        HazardZone zone;
        string stubsPath;

        static float RootAboveFeet => SilksongUnits.HeroColliderSize.y * 0.5f - SilksongUnits.HeroColliderOffset.y + 0.01f;

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            config = ScriptableObject.CreateInstance<HeroConfig>();
            stubsPath = Path.Combine(Path.GetTempPath(), "hazres-tests-stubs-" + System.Guid.NewGuid() + ".md");
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

            var zoneGo = Track(new GameObject("Spikes"));
            zoneGo.transform.position = new Vector3(0f, 1f, 0f);
            var box = zoneGo.AddComponent<BoxCollider2D>();
            box.size = new Vector2(10f, 4f);
            box.isTrigger = true;
            zone = zoneGo.AddComponent<HazardZone>();
            zone.SetRespawnsHero(true);
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

        HazardRespawnMarker NewMarker(Vector2 pos, bool right = true)
        {
            var go = Track(new GameObject("HazardMarker"));
            go.transform.position = pos;
            Physics2D.SyncTransforms();
            var m = go.AddComponent<HazardRespawnMarker>();
            m.Configure(right);
            return m;
        }

        [Test]
        public void Marker_FindsGroundBelow_AndSpawnsHeroRootAboveIt()
        {
            var m = NewMarker(new Vector2(30f, 3f));
            Assert.IsTrue(m.HasGround);
            Assert.AreEqual(30f, m.SpawnPosition.x, 0.001f);
            Assert.AreEqual(RootAboveFeet, m.SpawnPosition.y, 0.02f);
        }

        [Test]
        public void Marker_WithNoGround_UsesItsOwnPosition_WithoutThrowing()
        {
            var m = NewMarker(new Vector2(30f, -50f));   // below the ground: nothing under it
            Assert.IsFalse(m.HasGround);
            Assert.AreEqual(-50f + RootAboveFeet, m.SpawnPosition.y, 0.02f);
        }

        [Test]
        public void Marker_IgnoresTriggersWhenLookingForGround()
        {
            var t = Track(new GameObject("Trig"));
            t.transform.position = new Vector3(30f, 1.5f, 0f);
            var box = t.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(4f, 2f);
            var m = NewMarker(new Vector2(30f, 3f));
            Assert.AreEqual(RootAboveFeet, m.SpawnPosition.y, 0.02f);
        }

        [Test]
        public void Trigger_StoresMarkerSpot_WithTheRoomName()
        {
            var m = NewMarker(new Vector2(30f, 3f), false);
            var tgo = Track(new GameObject("HazardTrigger"));
            tgo.AddComponent<BoxCollider2D>().isTrigger = true;
            var trig = tgo.AddComponent<HazardRespawnTrigger>();
            trig.Configure(m);
            var data = PlayerData.CreateNew();

            Assert.IsTrue(trig.Apply(data));

            var spot = data.hazardRespawn;
            Assert.IsTrue(spot.IsSet);
            Assert.AreEqual(tgo.scene.name, spot.scene);
            Assert.AreEqual(m.SpawnPosition, spot.position);
            Assert.IsFalse(spot.facingRight);
        }

        [Test]
        public void Trigger_WithoutMarker_StoresNothing()
        {
            var tgo = Track(new GameObject("HazardTrigger"));
            tgo.AddComponent<BoxCollider2D>().isTrigger = true;
            var trig = tgo.AddComponent<HazardRespawnTrigger>();
            var data = PlayerData.CreateNew();
            Assert.IsFalse(trig.Apply(data));
            Assert.IsFalse(data.hazardRespawn.IsSet);
        }

        [Test]
        public void SpikeHit_PutsHeroAtTheSavedSpot_OfThisRoom_AfterPayingTheHit()
        {
            hero.Health.Data.SetHazardRespawn(zone.gameObject.scene.name, "m", new Vector2(40f, 0.51f), false);
            Step(2);
            Assert.AreEqual(4, hero.Health.Health, "the hit still costs 1");
            Assert.AreEqual(40f, hero.transform.position.x, 0.5f);
            Assert.IsFalse(hero.FacingRight);
            Assert.IsFalse(hero.Health.IsStunned, "no knockback after the respawn");
            Assert.IsFalse(hero.Health.IsDead);
        }

        [Test]
        public void StaleSpotFromAnotherRoom_IsIgnoredAndCleared_FallsBackToTheRoomStartMarker()
        {
            var mGo = Track(new GameObject("StartMarker"));
            mGo.transform.position = new Vector3(-30f, 0.51f, 0f);
            mGo.AddComponent<RespawnMarker>().Configure("Start", true);
            hero.Health.Data.SetHazardRespawn("SomeOtherRoom", "m", new Vector2(40f, 0.51f), false);

            Step(2);

            Assert.AreEqual(-30f, hero.transform.position.x, 0.5f, "went to Start, not to the other room's spot");
            Assert.IsFalse(hero.Health.Data.hazardRespawn.IsSet, "stale spot cleared");
        }

        [Test]
        public void ZoneWithRespawnOff_KeepsTheOldHurtAndKnockbackBehaviour()
        {
            zone.SetRespawnsHero(false);
            hero.Health.Data.SetHazardRespawn(zone.gameObject.scene.name, "m", new Vector2(40f, 0.51f), false);
            Step(2);
            Assert.AreEqual(4, hero.Health.Health);
            Assert.Less(Mathf.Abs(hero.transform.position.x), 5f, "not moved to the spot");
        }

        [Test]
        public void KillingSpikeHit_IsADeath_NotAHazardRespawn()
        {
            hero.Health.Data.health = 1;
            hero.Health.Data.SetHazardRespawn(zone.gameObject.scene.name, "m", new Vector2(40f, 0.51f), false);
            Step(2);
            Assert.IsTrue(hero.Health.IsDead);
            Assert.Less(Mathf.Abs(hero.transform.position.x), 5f);
        }
    }
}
