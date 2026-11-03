using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hollowknight.Silksong.Tests.EditMode
{
    public class SaveSlotsTests
    {
        string tempRoot;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "SilksongSlotTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            SaveSystem.Root = tempRoot;
            SaveSlots.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            SaveSlots.ResetForTests();
            SaveSystem.Root = null;
            Stubs.StubsDocPath = null;
            Stubs.ResetForTests();
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }

        string Slot(int n) => Path.Combine(tempRoot, "user" + n + ".json");

        string Snapshot() => string.Join("|", Directory.GetFiles(tempRoot).OrderBy(f => f)
            .Select(f => Path.GetFileName(f) + ":" + File.ReadAllText(f)));

        void WriteNewer(int n)
        {
            var d = PlayerData.CreateNew();
            d.version = PlayerData.CurrentVersion + 1;
            File.WriteAllText(Slot(n), JsonUtility.ToJson(d));
        }

        [Test]
        public void SlotCount_IsFour() => Assert.AreEqual(4, SaveSlots.SlotCount);

        [Test]
        public void EmptySlots_ReportEmpty_AndCreateNoFiles()
        {
            for (int i = 0; i < SaveSlots.SlotCount; i++)
                Assert.AreEqual(SlotState.Empty, SaveSlots.Summary(i).State);
            Assert.AreEqual(0, Directory.GetFiles(tempRoot).Length);
        }

        [Test]
        public void StartNew_ThenSummary_IsOkWithZoneAndMaxHealth()
        {
            var r = SaveSlots.StartNew(1);
            Assert.IsTrue(r.Success);
            Assert.IsTrue(File.Exists(Slot(1)), "written immediately");
            var s = SaveSlots.Summary(1);
            Assert.AreEqual(SlotState.Ok, s.State);
            Assert.AreEqual("TUT_01", s.MapZone);
            Assert.AreEqual(5, s.MaxHealth);
        }

        [Test]
        public void StartNew_OnOkSlot_Overwrites()
        {
            var first = SaveSlots.StartNew(0).Data;
            first.health = 2;
            SaveSystem.TrySave(first, 0);
            Assert.IsTrue(SaveSlots.StartNew(0).Success);
            Assert.AreEqual(5, SaveSlots.Load(0).Data.health);
        }

        [Test]
        public void Load_ResetsSilk_KeepsHealth()
        {
            var d = PlayerData.CreateNew();
            d.health = 3;
            d.silk = 6;
            SaveSystem.TrySave(d, 2);

            var r = SaveSlots.Load(2);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(0, r.Data.silk);
            Assert.AreEqual(3, r.Data.health);
        }

        [Test]
        public void Load_EmptySlot_Fails()
        {
            var r = SaveSlots.Load(3);
            Assert.IsFalse(r.Success);
            Assert.IsNotEmpty(r.Message);
            Assert.IsNull(r.Data);
        }

        [Test]
        public void NewerVersion_IsRefusedByStartNewAndLoad_AndLeftByteIdentical()
        {
            WriteNewer(1);
            byte[] before = File.ReadAllBytes(Slot(1));

            Assert.AreEqual(SlotState.NewerVersion, SaveSlots.Summary(1).State);
            var n = SaveSlots.StartNew(1);
            Assert.IsFalse(n.Success);
            Assert.IsNotEmpty(n.Message);
            var l = SaveSlots.Load(1);
            Assert.IsFalse(l.Success);
            Assert.IsNotEmpty(l.Message);

            CollectionAssert.AreEqual(before, File.ReadAllBytes(Slot(1)));
            Assert.AreEqual(1, Directory.GetFiles(tempRoot).Length);
        }

        [Test]
        public void Corrupt_SummaryAndLoadLeaveFile_StartNewRefuses()
        {
            File.WriteAllText(Slot(2), "{not json");
            byte[] before = File.ReadAllBytes(Slot(2));

            Assert.AreEqual(SlotState.Corrupt, SaveSlots.Summary(2).State);
            Assert.IsFalse(SaveSlots.Load(2).Success);
            Assert.IsFalse(SaveSlots.StartNew(2).Success);

            CollectionAssert.AreEqual(before, File.ReadAllBytes(Slot(2)));
            Assert.AreEqual(1, Directory.GetFiles(tempRoot).Length, "no quarantine copy from read-only calls");
        }

        [Test]
        public void Summary_NeverChangesAnyFile()
        {
            SaveSlots.StartNew(0);
            File.WriteAllText(Slot(1), "{}");
            WriteNewer(2);
            string before = Snapshot();

            for (int i = 0; i < SaveSlots.SlotCount; i++) SaveSlots.Summary(i);

            Assert.AreEqual(before, Snapshot());
        }

        [Test]
        public void Delete_OkSlot_RemovesFile()
        {
            SaveSlots.StartNew(0);
            Assert.IsTrue(SaveSlots.Delete(0).Success);
            Assert.IsFalse(File.Exists(Slot(0)));
            Assert.AreEqual(SlotState.Empty, SaveSlots.Summary(0).State);
        }

        [Test]
        public void Delete_EmptySlot_Succeeds()
        {
            Assert.IsTrue(SaveSlots.Delete(3).Success);
        }

        [Test]
        public void Delete_CorruptSlot_MovesBytesAside_ThenStartNewWorks()
        {
            File.WriteAllText(Slot(1), "{garbage");
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Save\]"));

            Assert.IsTrue(SaveSlots.Delete(1).Success);

            Assert.IsFalse(File.Exists(Slot(1)));
            var kept = Directory.GetFiles(tempRoot, "user1.json*.corrupt");
            Assert.AreEqual(1, kept.Length);
            Assert.AreEqual("{garbage", File.ReadAllText(kept[0]));
            Assert.IsTrue(SaveSlots.StartNew(1).Success);
        }

        [Test]
        public void Delete_NewerSlot_KeepsBytesAside()
        {
            WriteNewer(0);
            string original = File.ReadAllText(Slot(0));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Save\]"));

            Assert.IsTrue(SaveSlots.Delete(0).Success);

            var kept = Directory.GetFiles(tempRoot, "user0.json*.newer");
            Assert.AreEqual(1, kept.Length);
            Assert.AreEqual(original, File.ReadAllText(kept[0]));
        }

        [Test]
        public void Delete_CurrentSlot_ClearsSelection()
        {
            SaveSlots.StartNew(2);
            SaveSlots.Select(2);
            SaveSlots.Delete(2);
            Assert.AreEqual(-1, SaveSlots.CurrentSlot);
        }

        [Test]
        public void Autosave_WithNoCurrentSlot_IsNoOp()
        {
            Assert.AreEqual(-1, SaveSlots.CurrentSlot);
            Assert.IsFalse(SaveSlots.Autosave(PlayerData.CreateNew()));
            Assert.AreEqual(0, Directory.GetFiles(tempRoot).Length);
        }

        [Test]
        public void Autosave_WithSlot_Writes()
        {
            Assert.IsTrue(SaveSlots.Select(3));
            var d = PlayerData.CreateNew();
            d.health = 2;
            Assert.IsTrue(SaveSlots.Autosave(d));
            Assert.IsTrue(File.Exists(Slot(3)));
            Assert.AreEqual(2, SaveSystem.LoadOrDefault(3).health);
        }

        [Test]
        public void Select_OutOfRange_IsRejected()
        {
            Assert.IsFalse(SaveSlots.Select(4));
            Assert.IsFalse(SaveSlots.Select(-1));
            Assert.AreEqual(-1, SaveSlots.CurrentSlot);
        }

        [Test]
        public void RoundTrip_HazardRespawn_SceneBools_CurrentScene_Survive()
        {
            var started = SaveSlots.StartNew(0).Data;
            SaveSlots.Select(0);
            started.currentScene = "Tut_02";
            started.SetHazardRespawn("Tut_02", "Hazard Marker A", new Vector2(3.5f, -1f), false);
            started.sceneData.SetBool("Tut_02", "wall_1", true);
            Assert.IsTrue(SaveSlots.Autosave(started));

            var r = SaveSlots.Load(0);
            Assert.IsTrue(r.Success);
            Assert.AreEqual("Tut_02", r.Data.currentScene);
            Assert.IsTrue(r.Data.hazardRespawn.IsSet);
            Assert.AreEqual("Hazard Marker A", r.Data.hazardRespawn.markerName);
            Assert.AreEqual(new Vector2(3.5f, -1f), r.Data.hazardRespawn.position);
            Assert.IsFalse(r.Data.hazardRespawn.facingRight);
            Assert.AreEqual("Tut_02", r.Data.hazardRespawn.scene);
            Assert.IsTrue(r.Data.sceneData.GetBool("Tut_02", "wall_1"));
        }

        [Test]
        public void PlayTimeText_ReportsStubOnce()
        {
            Stubs.ResetForTests();
            Stubs.StubsDocPath = Path.Combine(tempRoot, "stubs.md");
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Stub\]"));
            Assert.AreEqual("", SaveSlots.PlayTimeText(0));
            SaveSlots.PlayTimeText(1); // second call must not log again
        }
    }
}