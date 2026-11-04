using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // The pure parts of MossCreep: the config -> walker mapping, the save id rule and the floor placement.
    // The assembled enemy is covered in PlayMode (M4AcceptanceTests, MossCreepAssemblyTests).
    public class MossCreepTests
    {
        SilksongEnemyConfig config;

        [SetUp]
        public void SetUp() => config = ScriptableObject.CreateInstance<SilksongEnemyConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(config);

        [Test]
        public void DefaultConfig_MapsToTheWalkersStudyNumbers()
        {
            var p = MossCreep.WalkParamsFrom(config);
            var d = EnemyWalkParams.Default;
            Assert.AreEqual(d.WalkSpeed, p.WalkSpeed);
            Assert.AreEqual(d.BackFootHeight, p.BackFootHeight);
            Assert.AreEqual(d.BackFootDown, p.BackFootDown);
            Assert.AreEqual(d.WallRayFrontDistance, p.WallRayFrontDistance);
            Assert.AreEqual(d.WallRaySkin, p.WallRaySkin);
            Assert.AreEqual(d.LedgeAheadPadding, p.LedgeAheadPadding);
            Assert.AreEqual(d.LedgeAheadDown, p.LedgeAheadDown);
            Assert.AreEqual(d.ShoveSpeed, p.ShoveSpeed);
            Assert.AreEqual(d.ShoveSeconds, p.ShoveSeconds);
            Assert.AreEqual(d.GravityScale, p.GravityScale);
            Assert.AreEqual(d.Mass, p.Mass);
        }

        [Test]
        public void EveryWalkField_FollowsTheConfig()
        {
            config.walkSpeed = 4.5f;
            config.groundBackRayHeight = 0.7f;
            config.groundBackRayLength = 1.3f;
            config.wallRayDistance = 0.4f;
            config.wallRaySkin = 0.15f;
            config.ledgeRayForwardPadding = 0.25f;
            config.ledgeRayLength = 1.4f;
            config.shoveSpeed = 12f;
            config.shoveSeconds = 0.3f;
            config.gravityScale = 2f;
            config.mass = 3f;
            var p = MossCreep.WalkParamsFrom(config);
            Assert.AreEqual(4.5f, p.WalkSpeed);
            Assert.AreEqual(0.7f, p.BackFootHeight);
            Assert.AreEqual(1.3f, p.BackFootDown);
            Assert.AreEqual(0.4f, p.WallRayFrontDistance);
            Assert.AreEqual(0.15f, p.WallRaySkin);
            Assert.AreEqual(0.25f, p.LedgeAheadPadding);
            Assert.AreEqual(1.4f, p.LedgeAheadDown);
            Assert.AreEqual(12f, p.ShoveSpeed);
            Assert.AreEqual(0.3f, p.ShoveSeconds);
            Assert.AreEqual(2f, p.GravityScale);
            Assert.AreEqual(3f, p.Mass);
            Assert.IsFalse(p.SpriteFacesRight, "the original art faces left");
        }

        [Test]
        public void NullConfig_GivesTheDefaults()
        {
            Assert.AreEqual(EnemyWalkParams.Default.WalkSpeed, MossCreep.WalkParamsFrom(null).WalkSpeed);
        }

        [Test]
        public void PersistenceId_IsTheExplicitIdElsePrefixAndObjectName()
        {
            Assert.AreEqual("Tut_01b_MossCreep_1", MossCreep.PersistenceIdFor(config, "Tut_01b_MossCreep_1", "MossCreep"));
            Assert.AreEqual("enemy-MossCreep", MossCreep.PersistenceIdFor(config, null, "MossCreep"));
            config.persistenceIdPrefix = "crawler";
            Assert.AreEqual("crawler-Spot", MossCreep.PersistenceIdFor(config, "", "Spot"));
        }

        [Test]
        public void RootYOnFloor_PutsTheColliderBottomAHairAboveTheFloor()
        {
            float y = MossCreep.RootYOnFloor(config, 2f);
            float bottom = y + config.colliderOffset.y - config.colliderSize.y * 0.5f;
            Assert.AreEqual(2f + MossCreep.SpawnClearance, bottom, 1e-4f);
        }
    }
}
