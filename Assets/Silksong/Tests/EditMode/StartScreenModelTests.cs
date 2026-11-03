using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests.EditMode
{
    // Milestone 3 task 6: the start screen's logic (rows, selection, New / Continue / Delete, the two delete
    // questions) with no scene. Saves live in a temp folder; SaveSystem.Root is restored in teardown.
    public class StartScreenModelTests
    {
        string tempRoot;
        string savedRoot;

        [SetUp]
        public void SetUp()
        {
            savedRoot = SaveSystem.Root;
            tempRoot = Path.Combine(Path.GetTempPath(), "SilksongStartModel_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            SaveSystem.Root = tempRoot;
            SaveSlots.ResetForTests();
            Stubs.StubsDocPath = Path.Combine(tempRoot, "stubs.md");
            Stubs.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            SaveSlots.ResetForTests();
            SaveSystem.Root = savedRoot;
            Stubs.StubsDocPath = null;
            Stubs.ResetForTests();
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }

        string SlotFile(int n) { return Path.Combine(tempRoot, "user" + n + ".json"); }

        void WriteOk(int n, string zone = "TUT_01", int maxHealth = 5)
        {
            var d = PlayerData.CreateNew();
            d.mapZone = zone;
            d.maxHealth = maxHealth;
            d.health = maxHealth;
            Assert.IsTrue(SaveSystem.TrySave(d, n));
        }

        void WriteNewer(int n)
        {
            var d = PlayerData.CreateNew();
            d.version = PlayerData.CurrentVersion + 1;
            File.WriteAllText(SlotFile(n), JsonUtility.ToJson(d));
        }

        void WriteCorrupt(int n) { File.WriteAllText(SlotFile(n), "{not json"); }

        [Test]
        public void FourRows_AllEmpty_ByDefault()
        {
            var m = new StartScreenModel();
            Assert.AreEqual(4, m.Rows.Count);
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual("Slot " + (i + 1), m.Rows[i].Title);
                Assert.AreEqual(SlotState.Empty, m.Rows[i].State);
                Assert.AreEqual("Empty", m.Rows[i].Detail);
                Assert.IsTrue(m.Rows[i].CanStartNew);
                Assert.IsFalse(m.Rows[i].CanContinue);
                Assert.IsFalse(m.Rows[i].CanDelete);
            }
            Assert.AreEqual(0, m.Selected);
        }

        [Test]
        public void OkRow_ShowsZoneAndMaxHealth_AndOffersContinueNewDelete()
        {
            WriteOk(1, "CRAWLSPACE", 7);
            var row = new StartScreenModel().Rows[1];
            Assert.AreEqual(SlotState.Ok, row.State);
            StringAssert.Contains("CRAWLSPACE", row.Detail);
            StringAssert.Contains("7", row.Detail);
            Assert.IsTrue(row.CanContinue && row.CanStartNew && row.CanDelete);
        }

        [Test]
        public void CorruptAndNewerRows_ShowPlainWarnings_AndOfferOnlyDelete()
        {
            WriteCorrupt(0);
            WriteNewer(1);
            var m = new StartScreenModel();
            Assert.AreEqual(SlotState.Corrupt, m.Rows[0].State);
            Assert.AreEqual(SlotState.NewerVersion, m.Rows[1].State);
            StringAssert.Contains("damaged", m.Rows[0].Detail);
            StringAssert.Contains("newer version", m.Rows[1].Detail);
            foreach (var row in new[] { m.Rows[0], m.Rows[1] })
            {
                Assert.IsTrue(row.CanDelete);
                Assert.IsFalse(row.CanContinue);
                Assert.IsFalse(row.CanStartNew);
            }
        }

        [Test]
        public void Selection_WrapsBothWays()
        {
            var m = new StartScreenModel();
            m.MoveUp();
            Assert.AreEqual(3, m.Selected);
            m.MoveDown();
            Assert.AreEqual(0, m.Selected);
            m.MoveDown(); m.MoveDown();
            Assert.AreEqual(2, m.Selected);
        }

        [Test]
        public void Submit_OnEmpty_StartsNew_OnOk_Continues()
        {
            WriteOk(1);
            var m = new StartScreenModel();
            Assert.AreEqual(StartScreenAction.StartNew, m.Submit());
            m.MoveDown();
            Assert.AreEqual(StartScreenAction.Continue, m.Submit());
        }

        [Test]
        public void Submit_OnCorruptOrNewer_ExplainsAndDoesNothing_FilesUntouched()
        {
            WriteCorrupt(0);
            WriteNewer(1);
            string a = File.ReadAllText(SlotFile(0)), b = File.ReadAllText(SlotFile(1));
            var m = new StartScreenModel();

            Assert.AreEqual(StartScreenAction.None, m.Submit());
            Assert.AreEqual("This save is damaged. Delete it to use the slot.", m.Message);
            Assert.AreEqual(StartScreenAction.None, m.RequestNew());
            m.MoveDown();
            Assert.AreEqual(StartScreenAction.None, m.Submit());
            Assert.AreEqual("This save is from a newer version. Delete it to use the slot.", m.Message);
            Assert.AreEqual(StartScreenAction.None, m.RequestNew());

            Assert.AreEqual(a, File.ReadAllText(SlotFile(0)));
            Assert.AreEqual(b, File.ReadAllText(SlotFile(1)));
        }

        [Test]
        public void NewOverOkSave_NeedsAConfirm_AndCancelKeepsTheSave()
        {
            WriteOk(0);
            var m = new StartScreenModel();
            Assert.AreEqual(StartScreenAction.None, m.RequestNew());
            Assert.AreEqual(StartScreenPhase.ConfirmNew, m.Phase);
            Assert.IsTrue(m.Cancel());
            Assert.AreEqual(StartScreenPhase.Browsing, m.Phase);

            m.RequestNew();
            Assert.AreEqual(StartScreenAction.StartNew, m.Submit(), "the second press confirms");
            Assert.AreEqual(StartScreenPhase.Browsing, m.Phase);
        }

        [Test]
        public void NewOnEmptySlot_StartsAtOnce()
        {
            var m = new StartScreenModel();
            Assert.AreEqual(StartScreenAction.StartNew, m.RequestNew());
        }

        [Test]
        public void Delete_NeedsTwoConfirms_ThenRemovesTheSave()
        {
            WriteOk(0);
            var m = new StartScreenModel();
            m.RequestDelete();
            Assert.AreEqual(StartScreenPhase.ConfirmDelete1, m.Phase);
            Assert.AreEqual("Delete this save?", m.Message);
            Assert.IsTrue(File.Exists(SlotFile(0)));

            m.Submit();
            Assert.AreEqual(StartScreenPhase.ConfirmDelete2, m.Phase);
            Assert.AreEqual("Really delete? This cannot be undone.", m.Message);
            Assert.IsTrue(File.Exists(SlotFile(0)), "still there after one confirm");

            Assert.AreEqual(StartScreenAction.None, m.Submit());
            Assert.AreEqual(StartScreenPhase.Browsing, m.Phase);
            Assert.IsFalse(File.Exists(SlotFile(0)));
            Assert.AreEqual(SlotState.Empty, m.Rows[0].State);
        }

        [Test]
        public void Delete_CancelAtEitherStep_KeepsTheSave()
        {
            WriteOk(0);
            var m = new StartScreenModel();

            m.RequestDelete();
            Assert.IsTrue(m.Cancel());
            Assert.AreEqual(StartScreenPhase.Browsing, m.Phase);

            m.RequestDelete();
            m.Submit();
            Assert.IsTrue(m.Cancel());
            Assert.AreEqual(StartScreenPhase.Browsing, m.Phase);

            Assert.IsTrue(File.Exists(SlotFile(0)));
            Assert.AreEqual(SlotState.Ok, new StartScreenModel().Rows[0].State);
        }

        [Test]
        public void Delete_OnCorruptSlot_MovesTheFileAside_AndFreesTheSlot()
        {
            WriteCorrupt(2);
            var m = new StartScreenModel();
            m.Select(2);
            m.RequestDelete();
            m.Submit();
            m.Submit();
            Assert.IsFalse(File.Exists(SlotFile(2)));
            Assert.AreEqual(SlotState.Empty, m.Rows[2].State);
            Assert.Greater(Directory.GetFiles(tempRoot, "*user2*").Length, 0, "the damaged bytes are kept aside, not destroyed");
        }

        [Test]
        public void Delete_OnEmptySlot_SaysNothingToDelete()
        {
            var m = new StartScreenModel();
            m.RequestDelete();
            Assert.AreEqual(StartScreenPhase.Browsing, m.Phase);
            Assert.AreEqual("Nothing to delete.", m.Message);
        }

        [Test]
        public void MovingIsIgnoredDuringAQuestion_AndDeleteTargetsTheSelectedSlot()
        {
            WriteOk(0);
            WriteOk(1);
            var m = new StartScreenModel();
            m.RequestDelete();
            m.MoveDown();
            Assert.AreEqual(0, m.Selected);
            m.Cancel();
            m.MoveDown();
            m.RequestDelete();
            m.Submit();
            m.Submit();
            Assert.AreEqual(SlotState.Ok, m.Rows[0].State);
            Assert.AreEqual(SlotState.Empty, m.Rows[1].State);
        }

        [Test]
        public void Cancel_WhenBrowsing_DoesNothing()
        {
            Assert.IsFalse(new StartScreenModel().Cancel());
        }

        [Test]
        public void ShowMessage_SetsTheStatusLine_AndMovingClearsIt()
        {
            var m = new StartScreenModel();
            m.ShowMessage("Could not load.");
            Assert.AreEqual("Could not load.", m.Message);
            m.MoveDown();
            Assert.AreEqual("", m.Message);
        }
    }
}
