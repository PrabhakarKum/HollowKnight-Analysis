using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hollowknight.Silksong.Tests.EditMode
{
    public class SaveSystemTests
    {
        static readonly Regex SaveLog = new Regex(@"\[Save\]");
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

        string Slot(int n) => Path.Combine(tempRoot, "user" + n + ".json");

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
            Assert.AreEqual(0, Directory.GetFiles(tempRoot).Length, "loading must not create files");
        }

        [Test]
        public void Load_CorruptFile_ReturnsDefaultsAndKeepsBackup()
        {
            string path = Slot(2);
            File.WriteAllText(path, "{not json");

            LogAssert.Expect(LogType.Warning, SaveLog);
            var loaded = SaveSystem.LoadOrDefault(2);

            Assert.AreEqual(PlayerData.CreateNew().health, loaded.health);
            Assert.IsFalse(File.Exists(path));
            Assert.IsTrue(File.Exists(path + ".corrupt"));
            Assert.AreEqual("{not json", File.ReadAllText(path + ".corrupt"));
        }

        [Test]
        public void Load_NewerVersion_ReturnsDefaultsWithoutOverwriting()
        {
            string path = Slot(3);
            File.WriteAllText(path, "{\"version\":999,\"health\":1,\"maxHealth\":1}");
            byte[] before = File.ReadAllBytes(path);

            LogAssert.Expect(LogType.Warning, SaveLog);
            var loaded = SaveSystem.LoadOrDefault(3);

            Assert.AreEqual(PlayerData.CreateNew().health, loaded.health);
            Assert.AreEqual(before, File.ReadAllBytes(path));
        }

        [Test]
        public void Save_OverExistingFile_ReplacesAtomically()
        {
            var data = PlayerData.CreateNew();
            data.health = 4;
            Assert.IsTrue(SaveSystem.TrySave(data, 1));
            data.health = 2;
            Assert.IsTrue(SaveSystem.TrySave(data, 1));

            Assert.AreEqual(2, SaveSystem.LoadOrDefault(1).health);
            Assert.IsFalse(File.Exists(Slot(1) + ".tmp"), "temp file must not be left behind");
            Assert.AreEqual(1, Directory.GetFiles(tempRoot).Length);
        }

        [Test]
        public void Save_DoesNotMutateCallersVersion()
        {
            var data = PlayerData.CreateNew();
            data.version = 0;
            Assert.IsTrue(SaveSystem.TrySave(data, 1));
            Assert.AreEqual(0, data.version);
        }

        [Test]
        public void Load_EmptyObjectFile_ReturnsDefaultsAndQuarantines()
        {
            string path = Slot(4);
            File.WriteAllText(path, "{}");

            LogAssert.Expect(LogType.Warning, SaveLog);
            var loaded = SaveSystem.LoadOrDefault(4);

            Assert.AreEqual(PlayerData.CreateNew().health, loaded.health);
            Assert.IsFalse(File.Exists(path));
            Assert.IsTrue(File.Exists(path + ".corrupt"));
        }

        [Test]
        public void Load_TruncatedFile_ReturnsDefaultsAndQuarantines()
        {
            // Cut off after "health": parseable or not, the missing maxHealth must reject it.
            string path = Slot(5);
            File.WriteAllText(path, "{\"version\":1,\"health\":3");

            LogAssert.Expect(LogType.Warning, SaveLog);
            var loaded = SaveSystem.LoadOrDefault(5);

            Assert.AreEqual(PlayerData.CreateNew().health, loaded.health);
            Assert.IsFalse(File.Exists(path));
            Assert.IsTrue(File.Exists(path + ".corrupt"));
        }

        [Test]
        public void Save_WhenExistingFileIsNewerVersion_DoesNotOverwrite()
        {
            string path = Slot(6);
            File.WriteAllText(path, "{\"version\":999,\"health\":1,\"maxHealth\":1}");
            byte[] before = File.ReadAllBytes(path);

            LogAssert.Expect(LogType.Warning, SaveLog);
            bool saved = SaveSystem.TrySave(PlayerData.CreateNew(), 6);

            Assert.IsFalse(saved);
            Assert.AreEqual(before, File.ReadAllBytes(path));
        }

        [Test]
        public void Quarantine_TwiceKeepsBothCorruptFiles()
        {
            string path = Slot(7);

            File.WriteAllText(path, "first bad");
            LogAssert.Expect(LogType.Warning, SaveLog);
            SaveSystem.LoadOrDefault(7);

            File.WriteAllText(path, "second bad");
            LogAssert.Expect(LogType.Warning, SaveLog);
            SaveSystem.LoadOrDefault(7);

            var contents = Directory.GetFiles(tempRoot, "*.corrupt").Select(File.ReadAllText).OrderBy(s => s).ToArray();
            CollectionAssert.AreEqual(new[] { "first bad", "second bad" }, contents);
        }
    }
}
