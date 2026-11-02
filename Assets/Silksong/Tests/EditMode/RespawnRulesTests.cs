using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // Task 8: the pure respawn decisions.
    public class RespawnRulesTests
    {
        static HazardRespawn Spot(string scene) => new HazardRespawn("m", new Vector2(3f, 4f), true, scene);

        [Test]
        public void SpotFromTheSameRoom_IsUsable()
        {
            Assert.IsTrue(RespawnRules.IsHazardSpotUsable(Spot("B"), "B"));
            Assert.IsFalse(RespawnRules.IsHazardSpotStale(Spot("B"), "B"));
        }

        [Test]
        public void SpotFromAnotherRoom_IsStale_AndNotUsable()
        {
            Assert.IsFalse(RespawnRules.IsHazardSpotUsable(Spot("A"), "B"));
            Assert.IsTrue(RespawnRules.IsHazardSpotStale(Spot("A"), "B"));
        }

        [Test]
        public void SpotWithNoScene_NeverCounts_AndIsStale()
        {
            var old = new HazardRespawn("m", Vector2.one, true);
            Assert.IsFalse(RespawnRules.IsHazardSpotUsable(old, "B"));
            Assert.IsTrue(RespawnRules.IsHazardSpotStale(old, "B"));
        }

        [Test]
        public void UnsetSpotOrEmptyRoom_IsNeitherUsableNorStale()
        {
            Assert.IsFalse(RespawnRules.IsHazardSpotUsable(new HazardRespawn(), "B"));
            Assert.IsFalse(RespawnRules.IsHazardSpotStale(new HazardRespawn(), "B"));
            Assert.IsFalse(RespawnRules.IsHazardSpotStale(null, "B"));
            Assert.IsFalse(RespawnRules.IsHazardSpotUsable(Spot("B"), ""));
        }

        [Test]
        public void ChooseHazardSpot_PrefersSaved_ThenRoomStart_ThenNothing()
        {
            Assert.AreEqual(RespawnRules.HazardSpotSource.Saved, RespawnRules.ChooseHazardSpot(Spot("B"), "B", true));
            Assert.AreEqual(RespawnRules.HazardSpotSource.RoomStart, RespawnRules.ChooseHazardSpot(Spot("A"), "B", true));
            Assert.AreEqual(RespawnRules.HazardSpotSource.RoomStart, RespawnRules.ChooseHazardSpot(new HazardRespawn(), "B", true));
            Assert.AreEqual(RespawnRules.HazardSpotSource.None, RespawnRules.ChooseHazardSpot(Spot("A"), "B", false));
        }

        [Test]
        public void SetHazardRespawn_StoresSceneNameAndFacing()
        {
            var data = PlayerData.CreateNew();
            data.SetHazardRespawn("B", "mk", new Vector2(1f, 2f), false);
            Assert.AreEqual("B", data.hazardRespawn.scene);
            Assert.AreEqual("mk", data.hazardRespawn.markerName);
            Assert.AreEqual(new Vector2(1f, 2f), data.hazardRespawn.position);
            Assert.IsFalse(data.hazardRespawn.facingRight);
        }

        [Test]
        public void SceneName_SurvivesAJsonRoundTrip()
        {
            var data = PlayerData.CreateNew();
            data.SetHazardRespawn("B", "mk", new Vector2(1f, 2f), true);
            var back = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
            Assert.AreEqual("B", back.hazardRespawn.scene);
        }

        [Test]
        public void DeathInOtherRoom_GoesToTheBenchRoom()
        {
            var t = RespawnRules.ChooseDeathTarget("Tut_01b", "Tut_01", "Bench");
            Assert.IsFalse(t.InPlace);
            Assert.AreEqual("Tut_01", t.Scene);
            Assert.AreEqual("Bench", t.Marker);
        }

        [Test]
        public void DeathInTheBenchRoom_RespawnsInPlace()
        {
            Assert.IsTrue(RespawnRules.ChooseDeathTarget("Tut_01", "Tut_01", "Bench").InPlace);
        }

        [Test]
        public void EmptyRespawnData_UsesTheStartRoomAndStartMarker()
        {
            var t = RespawnRules.ChooseDeathTarget("Tut_02", "", "Whatever");
            Assert.AreEqual("Tut_01", t.Scene);
            Assert.AreEqual("Start", t.Marker);
            var t2 = RespawnRules.ChooseDeathTarget("Tut_02", "Tut_01", "");
            Assert.AreEqual("Start", t2.Marker);
        }

        [Test]
        public void UnknownDeathRoom_IsNeverInPlace()
        {
            Assert.IsFalse(RespawnRules.ChooseDeathTarget("", "Tut_01", "Start").InPlace);
        }
    }
}
