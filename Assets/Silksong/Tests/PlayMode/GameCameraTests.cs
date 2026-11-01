using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // GameCamera stepped by hand with a fake subject, so no physics or input is involved and every number is
    // deterministic. Values asserted are the original's (docs/study/08-camera.md).
    public class GameCameraTests
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
        string stubsPath;

        [SetUp]
        public void SetUp()
        {
            stubsPath = Path.Combine(Path.GetTempPath(), "camera-tests-stubs-" + System.Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
            Build();
        }

        [TearDown]
        public void TearDown()
        {
            if (heroGo != null) Object.DestroyImmediate(heroGo);
            if (camGo != null) Object.DestroyImmediate(camGo);
            Stubs.StubsDocPath = null;
            if (File.Exists(stubsPath)) File.Delete(stubsPath);
        }

        void Build()
        {
            if (heroGo != null) Object.DestroyImmediate(heroGo);
            if (camGo != null) Object.DestroyImmediate(camGo);
            heroGo = new GameObject("Hero");
            heroGo.transform.position = new Vector3(100f, 50f, 0f);
            camGo = new GameObject("Main Camera");
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = SilksongUnits.CameraOrthographicSize;
            cam.aspect = 16f / 9f;
            gameCam = camGo.AddComponent<GameCamera>();
            gameCam.ManualStepping = true;
            subject = new FakeSubject();
            gameCam.Target = heroGo.transform;   // resets Subject, so assign the fake after
            gameCam.Subject = subject;
        }

        void Run(float seconds, float dt)
        {
            int n = Mathf.RoundToInt(seconds / dt);
            for (int i = 0; i < n; i++) gameCam.Step(dt);
        }

        // ---------- snapping ----------

        [Test]
        public void FirstStep_SnapsOntoTheTarget_WithLookAheadFacingTheSameWay()
        {
            gameCam.Step(0.02f);
            Assert.AreEqual(101f, camGo.transform.position.x, 1e-4f, "hero x + 1 unit of look-ahead");
            Assert.AreEqual(50f, camGo.transform.position.y, 1e-4f);
            Assert.AreEqual(-10f, camGo.transform.position.z, 1e-6f);
        }

        [Test]
        public void SnapFacingLeft_PutsTheLookAheadOnTheLeft()
        {
            subject.FacingRight = false;
            gameCam.SnapToTarget();
            Assert.AreEqual(99f, camGo.transform.position.x, 1e-4f);
        }

        [Test]
        public void NewTarget_SnapsToIt()
        {
            gameCam.Step(0.02f);
            var other = new GameObject("Other");
            try
            {
                other.transform.position = new Vector3(-30f, 7f, 0f);
                gameCam.Target = other.transform;
                gameCam.Step(0.02f);
                Assert.AreEqual(-30f, camGo.transform.position.x, 1.01f);
                Assert.AreEqual(7f, camGo.transform.position.y, 1e-3f);
            }
            finally { Object.DestroyImmediate(other); }
        }

        [Test]
        public void NoTarget_OrZeroDelta_DoesNothingAndDoesNotThrow()
        {
            gameCam.Target = null;
            Assert.DoesNotThrow(() => gameCam.Step(0.02f));
            gameCam.Target = heroGo.transform;
            gameCam.Subject = subject;
            gameCam.Step(0.02f);
            var before = camGo.transform.position;
            gameCam.Step(0f);
            Assert.AreEqual(before, camGo.transform.position);
        }

        // ---------- look-ahead ----------

        [TestCase(0.02f)]
        [TestCase(0.01f)]
        public void LookAhead_RampsAtSixUnitsPerSecond_AndFlipsWithFacing(float dt)
        {
            gameCam.SnapToTarget();
            Assert.AreEqual(1f, gameCam.LookAheadOffset, 1e-5f);
            subject.FacingRight = false;
            Run(0.1f, dt);   // 6 units per second: 0.1 s moves the offset by 0.6, from +1 to +0.4
            Assert.AreEqual(0.4f, gameCam.LookAheadOffset, 1e-3f);
            Run(0.2f, dt);
            Assert.AreEqual(-0.8f, gameCam.LookAheadOffset, 1e-3f);
            Run(0.1f, dt);
            Assert.AreEqual(-1f, gameCam.LookAheadOffset, 1e-4f);
            Run(1f, dt);
            Assert.AreEqual(-1f, gameCam.LookAheadOffset, 1e-5f, "clamped at one unit");
        }

        [Test]
        public void DashLead_OnlyWhileDashingFast()
        {
            gameCam.SnapToTarget();
            subject.State = HeroState.Dashing;
            subject.Velocity = new Vector2(28f, 0f);
            gameCam.Step(0.02f);
            Assert.AreEqual(1f, gameCam.DashOffset, 1e-5f);
            subject.Velocity = new Vector2(3f, 0f);
            gameCam.Step(0.02f);
            Assert.AreEqual(0f, gameCam.DashOffset, 1e-5f, "slower than 5 u/s is not a dash lead");
            subject.Velocity = new Vector2(28f, 0f);
            subject.State = HeroState.Grounded;
            gameCam.Step(0.02f);
            Assert.AreEqual(0f, gameCam.DashOffset, 1e-5f);
            subject.State = HeroState.Dashing;
            subject.FacingRight = false;
            subject.Velocity = new Vector2(-28f, 0f);
            gameCam.Step(0.02f);
            Assert.AreEqual(-1f, gameCam.DashOffset, 1e-5f);
        }

        // ---------- falling ----------

        [TestCase(0.02f)]
        [TestCase(0.01f)]
        public void FastFall_LooksDownByUpToOneUnit_AtFourUnitsPerSecond(float dt)
        {
            gameCam.SnapToTarget();
            subject.State = HeroState.Falling;
            subject.IsGrounded = false;
            subject.Velocity = new Vector2(0f, -25f);
            Run(0.1f, dt);
            Assert.AreEqual(-0.4f, gameCam.FallOffset, 1e-3f);
            Run(1f, dt);
            Assert.AreEqual(-1f, gameCam.FallOffset, 1e-5f);
            subject.Velocity = new Vector2(0f, -20f);
            gameCam.Step(dt);
            Assert.AreEqual(0f, gameCam.FallOffset, 1e-5f, "below 22 u/s the look-down is gone");
        }

        // ---------- look up / down ----------

        [Test]
        public void LookUp_NeedsTheHoldDelay_ThenShowsSixUnitsAbove()
        {
            gameCam.SnapToTarget();
            subject.Velocity = Vector2.zero;
            subject.WantsUp = true;
            Run(0.8f, 0.02f);
            Assert.AreEqual(0f, gameCam.LookOffsetY, 1e-5f, "0.85 s has not passed yet");
            Run(0.1f, 0.02f);
            Assert.AreEqual(6f, gameCam.LookOffsetY, 0.05f);
            subject.WantsUp = false;
            gameCam.Step(0.02f);
            Assert.AreEqual(0f, gameCam.LookOffsetY, 1e-5f);
        }

        [Test]
        public void LookDown_GoesBelow_AndMovingCancelsTheHold()
        {
            gameCam.SnapToTarget();
            subject.WantsDown = true;
            Run(0.5f, 0.02f);
            subject.Velocity = new Vector2(5f, 0f);   // starts moving: the hold restarts
            Run(0.5f, 0.02f);
            subject.Velocity = Vector2.zero;
            Run(0.5f, 0.02f);
            Assert.AreEqual(0f, gameCam.LookOffsetY, 1e-5f, "only 0.5 s of standing still since the restart");
            Run(0.5f, 0.02f);
            Assert.AreEqual(-6f, gameCam.LookOffsetY, 0.05f);
        }

        // ---------- frame-rate independence ----------

        [Test]
        public void Smooth_StaticTarget_LandsInTheSamePlaceAt50And100Hz()
        {
            float x50 = 0f, v50 = 0f, x100 = 0f, v100 = 0f;
            for (int i = 0; i < 50; i++) x50 = GameCamera.Smooth(x50, 10f, ref v50, 0.2f, 0.02f);
            for (int i = 0; i < 100; i++) x100 = GameCamera.Smooth(x100, 10f, ref v100, 0.2f, 0.01f);
            Assert.AreEqual(x50, x100, 1e-3f);
            Assert.AreEqual(v50, v100, 1e-2f);
            // It also actually converges on the goal.
            Assert.AreEqual(10f, x100, 0.01f);
        }

        [Test]
        public void Smooth_ZeroSmoothTime_SnapsToTheGoal()
        {
            float v = 3f;
            Assert.AreEqual(5f, GameCamera.Smooth(1f, 5f, ref v, 0f, 0.02f));
            Assert.AreEqual(0f, v);
        }

        [Test]
        public void Follow_RunningHero_Matches_AtFiftyAndOneHundredHertz()
        {
            float at50 = RunTrack(0.02f), at100 = RunTrack(0.01f);
            Assert.AreEqual(at50, at100, 0.15f, "same hero path, same camera, regardless of step rate");
        }

        // Hero runs right at 8.25 u/s for 2 s; returns the camera x.
        float RunTrack(float dt)
        {
            Build();
            gameCam.SnapToTarget();
            int n = Mathf.RoundToInt(2f / dt);
            for (int i = 1; i <= n; i++)
            {
                heroGo.transform.position = new Vector3(100f + 8.25f * i * dt, 50f, 0f);
                subject.Velocity = new Vector2(8.25f, 0f);
                gameCam.Step(dt);
            }
            return camGo.transform.position.x;
        }

        [Test]
        public void Follow_RunningHero_CameraTrailsTheHeroByASteadyShortDistance()
        {
            float cam = RunTrack(0.02f);
            float hero = heroGo.transform.position.x;
            // Look-ahead puts the goal one unit ahead; the two eases trail it by roughly speed * damp times.
            Assert.Greater(cam, hero - 2f);
            Assert.Less(cam, hero + 1.5f);
        }

        // ---------- stubs ----------

        [Test]
        public void UnbuiltCameraRules_AreRegisteredAsStubs()
        {
            // Awake already ran in SetUp.
            bool lockAreas = false, shake = false;
            foreach (var s in Stubs.Reported)
            {
                if (s.Contains("lock areas")) lockAreas = true;
                if (s.Contains("screen shake")) shake = true;
            }
            Assert.IsTrue(lockAreas && shake);
        }
    }
}
