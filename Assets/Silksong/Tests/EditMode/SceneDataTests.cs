using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests.EditMode
{
    public class SceneDataTests
    {
        string tempRoot;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "SilksongSceneDataTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            SaveSystem.Root = tempRoot;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.Root = null;
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }

        [Test]
        public void UnsetBool_ReadsFalse_AndIntReadsZero()
        {
            var d = new SceneData();
            Assert.IsFalse(d.GetBool("Tut_01", "Breakable 1"));
            Assert.AreEqual(0, d.GetInt("Tut_01", "Counter"));
            Assert.IsFalse(d.HasBool("Tut_01", "Breakable 1"));
        }

        [Test]
        public void SetBool_IsScopedBySceneAndId()
        {
            var d = new SceneData();
            d.SetBool("Tut_01", "Breakable 1", true);
            Assert.IsTrue(d.GetBool("Tut_01", "Breakable 1"));
            Assert.IsFalse(d.GetBool("Tut_01b", "Breakable 1"));
            Assert.IsFalse(d.GetBool("Tut_01", "Breakable 2"));
            d.SetBool("Tut_01", "Breakable 1", false);
            Assert.IsFalse(d.GetBool("Tut_01", "Breakable 1"));
            Assert.IsTrue(d.HasBool("Tut_01", "Breakable 1"));
        }

        [Test]
        public void SetInt_OverwritesAndIsIndependentOfBools()
        {
            var d = new SceneData();
            d.SetInt("A", "x", 3);
            d.SetInt("A", "x", 7);
            d.SetBool("A", "x", true);
            Assert.AreEqual(7, d.GetInt("A", "x"));
            Assert.IsTrue(d.GetBool("A", "x"));
        }

        [Test]
        public void PersistentBool_SavesOnLeave_RestoresOnReturn()
        {
            var d = new SceneData();
            var go = new GameObject("Breakable 1");
            var go2 = new GameObject("Breakable 1");
            try
            {
                var p = go.AddComponent<PersistentBool>();
                p.Configure(d, "Tut_01", null);
                Assert.AreEqual("Breakable 1", p.ItemId, "falls back to the object name");
                p.Value = true;
                p.Save();

                var p2 = go2.AddComponent<PersistentBool>();
                p2.Configure(d, "Tut_01", null);
                p2.Restore();
                Assert.IsTrue(p2.Value);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(go2);
            }
        }

        [Test]
        public void PlayerData_NewGame_HasEmptySceneAndHazardData()
        {
            var pd = PlayerData.CreateNew();
            Assert.IsNotNull(pd.sceneData);
            Assert.IsNotNull(pd.hazardRespawn);
            Assert.IsFalse(pd.hazardRespawn.IsSet);
            Assert.IsFalse(string.IsNullOrEmpty(pd.mapZone));
        }

        [Test]
        public void SaveRoundTrip_KeepsSceneDataMapZoneRespawnAndHazard()
        {
            var pd = PlayerData.CreateNew();
            pd.mapZone = "TUT_02";
            pd.respawnScene = "Tut_02";
            pd.respawnMarker = "Bench_B";
            pd.sceneData.SetBool("Tut_01", "Breakable 1", true);
            pd.sceneData.SetInt("Tut_01b", "Hits", 4);
            pd.hazardRespawn = new HazardRespawn("Hazard Marker 3", new Vector2(12.5f, -3f), false);

            Assert.IsTrue(SaveSystem.TrySave(pd, 1));
            var loaded = SaveSystem.LoadOrDefault(1);

            Assert.AreEqual("TUT_02", loaded.mapZone);
            Assert.AreEqual("Tut_02", loaded.respawnScene);
            Assert.AreEqual("Bench_B", loaded.respawnMarker);
            Assert.IsTrue(loaded.sceneData.GetBool("Tut_01", "Breakable 1"));
            Assert.AreEqual(4, loaded.sceneData.GetInt("Tut_01b", "Hits"));
            Assert.IsTrue(loaded.hazardRespawn.IsSet);
            Assert.AreEqual("Hazard Marker 3", loaded.hazardRespawn.markerName);
            Assert.AreEqual(12.5f, loaded.hazardRespawn.position.x, 1e-4f);
            Assert.AreEqual(-3f, loaded.hazardRespawn.position.y, 1e-4f);
            Assert.IsFalse(loaded.hazardRespawn.facingRight);
        }

        [Test]
        public void Load_OldVersion1SaveWithoutNewKeys_StillLoadsWithEmptyDefaults()
        {
            string path = Path.Combine(tempRoot, "user2.json");
            File.WriteAllText(path,
                "{\"version\":1,\"health\":4,\"maxHealth\":5,\"silk\":2,\"maxSilk\":9,\"respawnScene\":\"Tut_01\",\"respawnMarker\":\"M\"}");

            var loaded = SaveSystem.LoadOrDefault(2);

            Assert.AreEqual(4, loaded.health);
            Assert.AreEqual("M", loaded.respawnMarker);
            Assert.IsNotNull(loaded.sceneData);
            Assert.IsFalse(loaded.sceneData.GetBool("Tut_01", "anything"));
            Assert.IsNotNull(loaded.hazardRespawn);
            Assert.IsFalse(loaded.hazardRespawn.IsSet);
            Assert.IsFalse(File.Exists(path + ".corrupt"));
        }
    }
}
