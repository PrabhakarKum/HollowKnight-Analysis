using System.IO;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Hollowknight.Silksong.Tests.EditMode
{
    public class SaveSystemTests
    {
        string tempRoot;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "SilksongSaveTests_" + System.Guid.NewGuid().ToString("N"));
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
        public void Save_ThenLoad_RoundTripsHealthAndRespawn()
        {
            var data = PlayerData.CreateNew();
            data.health = 3;
            data.respawnMarker = "Bench_A";

            Assert.IsTrue(SaveSystem.TrySave(data, 1));
            var loaded = SaveSystem.LoadOrDefault(1);

            Assert.AreEqual(3, loaded.health);
            Assert.AreEqual("Bench_A", loaded.respawnMarker);
        }

        [Test]
        public void Load_MissingFile_ReturnsNewGameDefaults()
        {
            Assert.AreEqual(PlayerData.CreateNew().health, SaveSystem.LoadOrDefault(9).health);
        }

        [Test]
        public void Load_CorruptFile_ReturnsDefaultsAndKeepsBackup()
        {
            string path = Path.Combine(tempRoot, "user2.json");
            File.WriteAllText(path, "{not json");

            // The system logs a warning when it quarantines the file; that is expected here.
            LogAssert.ignoreFailingMessages = true;
            PlayerData loaded;
            try { loaded = SaveSystem.LoadOrDefault(2); }
            finally { LogAssert.ignoreFailingMessages = false; }

            Assert.AreEqual(PlayerData.CreateNew().health, loaded.health);
            Assert.IsFalse(File.Exists(path));
            Assert.IsTrue(File.Exists(path + ".corrupt"));
            Assert.AreEqual("{not json", File.ReadAllText(path + ".corrupt"));
        }

        [Test]
        public void Load_NewerVersion_ReturnsDefaultsWithoutOverwriting()
        {
            string path = Path.Combine(tempRoot, "user3.json");
            File.WriteAllText(path, "{\"version\":999,\"health\":1,\"maxHealth\":1}");
            byte[] before = File.ReadAllBytes(path);

            LogAssert.ignoreFailingMessages = true;
            PlayerData loaded;
            try { loaded = SaveSystem.LoadOrDefault(3); }
            finally { LogAssert.ignoreFailingMessages = false; }

            Assert.AreEqual(PlayerData.CreateNew().health, loaded.health);
            Assert.AreEqual(before, File.ReadAllBytes(path));
        }
    }
}
