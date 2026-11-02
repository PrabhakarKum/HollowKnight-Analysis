using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // Camera on room entry: swap bounds, snap to the hero at once, hold still for a moment, then follow again.
    // Stepped by hand like GameCameraTests. Numbers are the original's (docs/study/08-camera.md, "Entry snap").
    public class GameCameraEntryTests
    {
        class FakeSubject : ICameraSubject
        {
            public bool FacingRight { get; set; } = true;
            public HeroState State { get; set; } = HeroState.Grounded;
            public bool IsGrounded { get; set; } = true;
            public Vector2 Velocity { get; set; }
            public bool WantsUp { get; set; }
            public bool WantsDown { get; set; }
        }

        GameObject heroGo, camGo;
        GameCamera gameCam;
        FakeSubject subject;
        RoomBounds roomA, roomB;
        string stubsPath;

        [SetUp]
        public void SetUp()
        {
            stubsPath = Path.Combine(Path.GetTempPath(), "camera-entry-stubs-" + System.Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
            roomA = RoomBounds.Create(new Rect(0f, 0f, 100f, 60f), "RoomA");
            roomB = RoomBounds.Create(new Rect(500f, 0f, 100f, 60f), "RoomB");
            heroGo = new GameObject("Hero");
            heroGo.transform.position = new Vector3(50f, 30f, 0f);
            camGo = new GameObject("Main Camera");
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = SilksongUnits.CameraOrthographicSize;
            cam.aspect = 16f / 9f;
            gameCam = camGo.AddComponent<GameCamera>();
            gameCam.ManualStepping = true;
            gameCam.Bounds = roomA;
            subject = new FakeSubject();
            gameCam.Target = heroGo.transform;
            gameCam.Subject = subject;
            gameCam.Step(0.02f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(heroGo);
            Object.DestroyImmediate(camGo);
            if (roomA != null) Object.DestroyImmediate(roomA.gameObject);
            if (roomB != null) Object.DestroyImmediate(roomB.gameObject);
            Stubs.StubsDocPath = null;
            if (File.Exists(stubsPath)) File.Delete(stubsPath);
        }

        float CamX => camGo.transform.position.x;
        float CamY => camGo.transform.position.y;

        void Run(float seconds, float dt)
        {
            int n = Mathf.RoundToInt(seconds / dt);
            for (int i = 0; i < n; i++) gameCam.Step(dt);
        }

        [Test]
        public void SnapTo_InNewBounds_JumpsAtOnce_WithNoSmoothing()
        {
            gameCam.Bounds = roomB;
            heroGo.transform.position = new Vector3(550f, 30f, 0f);
            subject.FacingRight = true;
            gameCam.SnapTo(new Vector2(550f, 30f));
            Assert.AreEqual(551f, CamX, 1e-4f, "hero x + 1 look-ahead, no step needed");
            Assert.AreEqual(30f, CamY, 1e-4f);
            Assert.AreEqual(-10f, camGo.transform.position.z, 1e-6f);
        }

        [Test]
        public void SnapTo_ClampsToTheNewRoom_NotTheOldOne()
        {
            gameCam.Bounds = roomB;
            heroGo.transform.position = new Vector3(550f, 2f, 0f);
            gameCam.SnapTo(new Vector2(550f, 2f));
            Assert.AreEqual(8.3f, CamY, 1e-4f, "bottom inset of the new room");
            Assert.GreaterOrEqual(CamX, 514.6f - 1e-4f);
        }

        [Test]
        public void SnapTo_UsesTheGivenPosition_EvenIfTheTargetHasNotMovedYet()
        {
            gameCam.Bounds = roomB;
            gameCam.SnapTo(new Vector2(560f, 40f));   // hero object still at the old room's spot
            Assert.AreEqual(561f, CamX, 1e-4f);
            Assert.AreEqual(40f, CamY, 1e-4f);
        }

        [Test]
        public void SnapTo_HoldsStill_ForPointOneSeconds_ThenFollowsAgain()
        {
            Assert.AreEqual(0.1f, gameCam.Settings.EntryHoldSeconds, 1e-6f);
            gameCam.Bounds = roomB;
            heroGo.transform.position = new Vector3(550f, 30f, 0f);
            gameCam.SnapTo(new Vector2(550f, 30f));
            Assert.IsTrue(gameCam.IsHolding);
            // Hero walks away while the camera is held.
            heroGo.transform.position = new Vector3(556f, 30f, 0f);
            Run(0.08f, 0.02f);
            Assert.IsTrue(gameCam.IsHolding);
            Assert.AreEqual(551f, CamX, 1e-4f, "still frozen at 0.08 s");
            Run(0.04f, 0.02f);
            Assert.IsFalse(gameCam.IsHolding, "released after 0.1 s");
            Run(1f, 0.02f);
            Assert.Greater(CamX, 556f, "following again, toward hero + look-ahead");
        }

        [Test]
        public void AfterTheHold_NoCatchUpJump_FromTheHeroMovingDuringTheHold()
        {
            gameCam.Bounds = roomB;
            heroGo.transform.position = new Vector3(550f, 30f, 0f);
            gameCam.SnapTo(new Vector2(550f, 30f));
            heroGo.transform.position = new Vector3(580f, 30f, 0f);   // big teleport during the hold
            Run(0.12f, 0.02f);
            // The first free step eases, it does not carry the follow target by the whole 30 units.
            gameCam.Step(0.02f);
            Assert.Less(CamX, 575f);
        }

        [Test]
        public void SnapTo_RaisesPositionedAtHero_WhenTheHoldEnds()
        {
            int fired = 0;
            gameCam.PositionedAtHero += () => fired++;
            gameCam.Bounds = roomB;
            gameCam.SnapTo(new Vector2(550f, 30f));
            Assert.IsTrue(gameCam.IsHolding);
            Run(0.06f, 0.02f);
            Assert.AreEqual(0, fired);
            Run(0.06f, 0.02f);
            Assert.AreEqual(1, fired);
            Assert.IsFalse(gameCam.IsHolding);
        }

        // ---------- hero at a side edge ----------
        // The original has an inward-facing nudge branch, but it cannot run without a lock area (docs/study/08-camera.md),
        // so at an edge the camera lands on the clamped position whichever way the hero faces.

        [Test]
        public void LeftEdge_LandsOnTheClamp_FacingInwardOrAtTheWall([Values(true, false)] bool facingRight)
        {
            gameCam.Bounds = roomB;
            gameCam.SnapTo(new Vector2(510f, 30f), facingRight);
            Assert.AreEqual(514.6f, CamX, 1e-4f, "clamped to the left limit, no nudge");
        }

        [Test]
        public void RightEdge_LandsOnTheClamp_FacingInwardOrAtTheWall([Values(true, false)] bool facingRight)
        {
            gameCam.Bounds = roomB;
            gameCam.SnapTo(new Vector2(590f, 30f), facingRight);
            Assert.AreEqual(585.4f, CamX, 1e-4f, "clamped to the right limit, no nudge");
        }

        [Test]
        public void MiddleOfTheRoom_IsHeroPlusLookAhead()
        {
            gameCam.Bounds = roomB;
            gameCam.SnapTo(new Vector2(550f, 30f), false);
            Assert.AreEqual(549f, CamX, 1e-4f, "hero x - 1 look-ahead");
        }

        // ---------- snap while frozen or held ----------

        [Test]
        public void SnapTo_WhileFrozen_Unfreezes_AndStillHoldsThenFollows()
        {
            gameCam.Freeze();
            gameCam.Bounds = roomB;
            gameCam.SnapTo(new Vector2(550f, 30f));
            Assert.IsFalse(gameCam.IsFrozen);
            Assert.AreEqual(551f, CamX, 1e-4f);
            Run(0.12f, 0.02f);
            Assert.IsFalse(gameCam.IsHolding);
            heroGo.transform.position = new Vector3(560f, 30f, 0f);
            Run(1f, 0.02f);
            Assert.Greater(CamX, 555f, "following after the hold");
        }

        [Test]
        public void SnapTo_DuringARunningHold_RestartsTheHold_AndFiresOnce()
        {
            int fired = 0;
            gameCam.PositionedAtHero += () => fired++;
            gameCam.Bounds = roomB;
            gameCam.SnapTo(new Vector2(550f, 30f));
            Run(0.08f, 0.02f);
            gameCam.SnapTo(new Vector2(560f, 30f));
            Assert.AreEqual(561f, CamX, 1e-4f);
            Run(0.08f, 0.02f);
            Assert.IsTrue(gameCam.IsHolding, "0.16 s since the first snap but only 0.08 s since the second");
            Assert.AreEqual(0, fired);
            Run(0.04f, 0.02f);
            Assert.IsFalse(gameCam.IsHolding);
            Assert.AreEqual(1, fired);
        }


        // ---------- freeze ----------

        [Test]
        public void Freeze_HoldsInPlace_UntilUnfrozen()
        {
            heroGo.transform.position = new Vector3(40f, 30f, 0f);
            gameCam.Freeze();
            float x = CamX;
            Run(1f, 0.02f);
            Assert.AreEqual(x, CamX, 1e-6f);
            Assert.IsTrue(gameCam.IsFrozen);
            gameCam.Unfreeze();
            Run(1f, 0.02f);
            Assert.Less(CamX, x - 5f);
        }

        // ---------- bounds swap ----------

        [Test]
        public void SwappingBounds_Mid_Motion_NeverProducesNaN_AndEndsInsideTheNewRoom()
        {
            subject.Velocity = new Vector2(8f, 0f);
            for (int i = 0; i < 20; i++)
            {
                heroGo.transform.position += new Vector3(0.16f, 0f, 0f);
                gameCam.Step(0.02f);
            }
            gameCam.Bounds = roomB;
            heroGo.transform.position = new Vector3(550f, 30f, 0f);
            Run(2f, 0.02f);
            Assert.IsFalse(float.IsNaN(CamX));
            Assert.GreaterOrEqual(CamX, 514.6f - 1e-3f);
            Assert.LessOrEqual(CamX, 585.4f + 1e-3f);
        }

        [Test]
        public void SwappingBounds_ToNull_OrADestroyedRoom_IsSafe()
        {
            var temp = RoomBounds.Create(new Rect(0f, 0f, 100f, 60f), "Temp");
            gameCam.Bounds = temp;
            Object.DestroyImmediate(temp.gameObject);
            Assert.DoesNotThrow(() => gameCam.Step(0.02f));
            gameCam.Bounds = null;
            Assert.DoesNotThrow(() => gameCam.Step(0.02f));
            Assert.DoesNotThrow(() => gameCam.SnapTo(new Vector2(10f, 10f)));
        }

        [Test]
        public void EnterRoom_TakesTheBoundsFromTheRoomInfo()
        {
            var go = new GameObject("RoomInfo");
            var info = go.AddComponent<RoomInfo>();
            info.Configure("Tut_01b", 13, "Moss Cave", roomB);
            gameCam.EnterRoom(info);
            Assert.AreSame(roomB, gameCam.Bounds);
            Object.DestroyImmediate(go);
        }

        // ---------- single source rule ----------

        [Test]
        public void EntryValues_LiveOnGameCameraSettings_NotOnHeroConfig()
        {
            // Only guards by name: no HeroConfig field (public or not) may be called Entry* or *Nudge*. A copy under
            // another name would not be caught.
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            foreach (var fi in typeof(HeroConfig).GetFields(flags))
                Assert.IsFalse(fi.Name.Contains("Entry") || fi.Name.Contains("Nudge"), fi.Name);
            Assert.AreEqual(0.1f, gameCam.Settings.EntryHoldSeconds, 1e-6f);
        }
    }

    public class RoomInfoTests
    {
        string stubsPath;

        // Awake reports a stub; keep that out of the real docs/study/STUBS.md.
        [SetUp]
        public void SetUp()
        {
            stubsPath = Path.Combine(Path.GetTempPath(), "roominfo-stubs-" + System.Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            Stubs.StubsDocPath = null;
            if (File.Exists(stubsPath)) File.Delete(stubsPath);
        }

        [Test]
        public void Configure_ExposesSceneZoneAndBounds()
        {
            var b = RoomBounds.Create(new Rect(0f, 0f, 10f, 10f));
            var go = new GameObject("Info");
            var info = go.AddComponent<RoomInfo>();
            info.Configure("Tut_02", 13, "Moss Cave", b);
            Assert.AreEqual("Tut_02", info.SceneName);
            Assert.AreEqual(13, info.MapZoneId);
            Assert.AreEqual("Moss Cave", info.MapZoneName);
            Assert.AreSame(b, info.Bounds);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(b.gameObject);
        }
    }
}
