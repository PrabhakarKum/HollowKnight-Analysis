using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // Same deterministic setup as HeroDashTests: Script-mode physics, explicit StepFixed + Simulate pairs,
    // input edges cleared after each step. The world has a floor (top at y = 0) and a tall wall to the right
    // of the hero whose left face is at x = WallFaceX.
    public class HeroWallTests
    {
        const float HeroHalfWidth = 0.25f;
        const float HeroHalfHeight = 0.5f;
        const float WallFaceX = 1f;

        readonly List<UnityEngine.Object> spawned = new List<UnityEngine.Object>();
        SimulationMode2D savedMode;
        float dt = 0.02f;
        HeroConfig config;
        FakeHeroInput input;
        HeroController hero;
        string stubsPath;

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            dt = 0.02f;
            config = ScriptableObject.CreateInstance<HeroConfig>();
            stubsPath = Path.Combine(Path.GetTempPath(), "wall-tests-stubs-" + System.Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                ClearWorld();
                if (config != null) UnityEngine.Object.DestroyImmediate(config);
            }
            finally
            {
                Physics2D.simulationMode = savedMode;
                Stubs.StubsDocPath = null;
                if (File.Exists(stubsPath)) File.Delete(stubsPath);
            }
        }

        void ClearWorld()
        {
            foreach (var o in spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            spawned.Clear();
            hero = null;
        }

        // Floor plus a wall to the right; the hero starts touching the wall face, startHeight above the floor.
        void BuildWorld(float startHeight)
        {
            ClearWorld();
            input = new FakeHeroInput();

            var ground = new GameObject("Ground");
            spawned.Add(ground);
            var gbox = ground.AddComponent<BoxCollider2D>();
            gbox.size = new Vector2(400f, 1f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);

            var wall = new GameObject("Wall");
            spawned.Add(wall);
            var wbox = wall.AddComponent<BoxCollider2D>();
            wbox.size = new Vector2(1f, 400f);
            wall.transform.position = new Vector3(WallFaceX + 0.5f, 190f, 0f);

            var go = new GameObject("Hero");
            spawned.Add(go);
            go.transform.position = new Vector3(WallFaceX - HeroHalfWidth - 0.01f, HeroHalfHeight + 0.01f + startHeight, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(HeroHalfWidth * 2f, HeroHalfHeight * 2f);
            hero = go.AddComponent<HeroController>();
            hero.ManualStepping = true;
            hero.Config = config;
            hero.Input = input;
            Physics2D.SyncTransforms();
        }

        void Step(int count)
        {
            for (int i = 0; i < count; i++)
            {
                hero.StepFixed(dt);
                Physics2D.Simulate(dt);
                input.EndFixedStep(dt);
            }
        }

        int Steps(float seconds) => Mathf.RoundToInt(seconds / dt);

        static readonly Vector2 Right = new Vector2(1f, 0f);
        static readonly Vector2 Left = new Vector2(-1f, 0f);

        // High up, falling beside the wall, pressing into it, until the cling has started.
        void ClingToWall()
        {
            BuildWorld(60f);
            input.Move = Right;
            Step(Steps(0.3f));
            Assert.AreEqual(HeroState.WallClinging, hero.State, "test setup: hero should be clinging");
        }

        void PressJump()
        {
            input.JumpButton.Press();
            Step(1);
        }

        void SetState(HeroState state)
        {
            var setter = typeof(HeroController).GetProperty("State").GetSetMethod(true);
            setter.Invoke(hero, new object[] { state });
        }

        [Test]
        public void WallCling_PressingTowardWallWhileFalling_CapsFallSpeedAtWallSlideSpeed()
        {
            BuildWorld(60f);
            // Free fall beside the wall first so the cap has something to cut.
            Step(Steps(0.3f));
            Assert.Less(hero.Velocity.y, -config.WallSlideSpeed - 1f, "test setup: falling faster than the slide cap");
            Assert.AreNotEqual(HeroState.WallClinging, hero.State);

            input.Move = Right;
            Step(2);
            Assert.AreEqual(HeroState.WallClinging, hero.State);
            Assert.IsTrue(hero.Wall.IsClinging);
            Assert.IsTrue(hero.Wall.IsTouchingWall);
            Step(Steps(0.5f));
            Assert.AreEqual(-config.WallSlideSpeed, hero.Velocity.y, 0.05f);
            Assert.AreEqual(0f, hero.Velocity.x, 0.01f, "no sideways drift while clinging");
            Assert.IsTrue(hero.FacingRight, "faces the wall");
        }

        [Test]
        public void WallCling_NotPressingTowardWall_DoesNotCling()
        {
            BuildWorld(60f);
            Step(Steps(0.5f));
            Assert.AreNotEqual(HeroState.WallClinging, hero.State);
            Assert.IsFalse(hero.Wall.IsClinging);
            Assert.Less(hero.Velocity.y, -config.WallSlideSpeed - 1f, "keeps falling at full speed");
        }

        [Test]
        public void WallCling_PressingAwayFromWall_DoesNotCling()
        {
            BuildWorld(60f);
            input.Move = Left;
            Step(Steps(0.5f));
            Assert.IsFalse(hero.Wall.IsClinging);
        }

        [Test]
        public void WallCling_WhileGrounded_DoesNotCling()
        {
            BuildWorld(0f);
            input.Move = Right;
            Step(Steps(0.5f));
            Assert.IsTrue(hero.IsGrounded);
            Assert.AreEqual(HeroState.Grounded, hero.State);
            Assert.IsFalse(hero.Wall.IsClinging);
        }

        [Test]
        public void WallCling_LandingOnTheFloor_EndsTheCling()
        {
            BuildWorld(3f);
            input.Move = Right;
            Step(Steps(1.5f));
            Assert.IsTrue(hero.IsGrounded, "test setup: slid down to the floor");
            Assert.IsFalse(hero.Wall.IsClinging);
            Assert.AreEqual(HeroState.Grounded, hero.State);
        }

        [Test]
        public void WallCling_HoldingAwayLongEnough_ReleasesAndFacesAway()
        {
            ClingToWall();
            input.Move = Left;
            Step(1);
            Assert.IsTrue(hero.Wall.IsClinging, "a single step away does not let go");
            Step(Steps(0.2f));
            Assert.IsFalse(hero.Wall.IsClinging);
            Assert.IsFalse(hero.FacingRight, "turns away from the wall");
            Assert.Less(hero.Velocity.x, 0f);
        }

        [Test]
        public void WallCling_FreshDownPress_Releases()
        {
            ClingToWall();
            input.Move = new Vector2(0f, -1f);
            Step(2);
            Assert.IsFalse(hero.Wall.IsClinging);
            Assert.Less(hero.Velocity.y, 0f);
        }

        [Test]
        public void WallCling_LogsStubsForUnbuiltStickAndAcceleration()
        {
            ClingToWall();
            var reported = new List<string>(Stubs.Reported);
            CollectionAssert.Contains(reported, "Hero wall stick delay");
            CollectionAssert.Contains(reported, "Hero wall slide acceleration");
        }

        [Test]
        public void WallJump_PushesAwayFromWall_AndIgnoresHorizontalInputForLockSeconds()
        {
            ClingToWall();
            // Keep holding toward the wall: the push must win for the whole lock.
            PressJump();
            Assert.AreEqual(-config.WallJumpSpeedX, hero.Velocity.x, 0.01f);
            Assert.AreEqual(config.WallJumpSpeedY, hero.Velocity.y, 0.01f);
            Assert.AreEqual(HeroState.Rising, hero.State);
            Assert.IsFalse(hero.FacingRight, "faces away from the wall");
            input.JumpButton.Release();

            int guard = Steps(config.WallJumpLockSeconds) - 2;
            float previous = hero.Velocity.x;
            for (int i = 0; i < guard; i++)
            {
                Step(1);
                Assert.Less(hero.Velocity.x, -config.RunSpeed + 0.01f, "still pushed away inside the lock");
                Assert.GreaterOrEqual(hero.Velocity.x, previous - 0.001f, "push decays toward run speed");
                previous = hero.Velocity.x;
            }

            Step(4);
            Assert.AreEqual(config.RunSpeed, hero.Velocity.x, 0.01f, "input control is back after the lock");
        }

        [Test]
        public void WallJump_HeldButton_KeepsRiseSpeedForHoldMaxThenGravityTakesOver()
        {
            ClingToWall();
            PressJump();
            Step(Steps(config.JumpHoldMaxSeconds) - 3);
            Assert.AreEqual(config.WallJumpSpeedY, hero.Velocity.y, 0.05f, "constant rise while held");
            Step(Steps(0.2f));
            Assert.Less(hero.Velocity.y, config.WallJumpSpeedY - 1f, "gravity after the hold");
        }

        [Test]
        public void WallJump_EarlyRelease_CutsTheRise()
        {
            ClingToWall();
            PressJump();
            input.JumpButton.Release();
            Step(2);
            Assert.Less(hero.Velocity.y, config.WallJumpSpeedY * config.JumpReleaseMultiplier + 0.1f);
        }

        [Test]
        public void WallJump_DoesNotStartWhenNotClinging()
        {
            BuildWorld(60f);
            Step(Steps(0.2f));
            PressJump();
            Assert.Greater(hero.Velocity.y, -40f);
            Assert.AreEqual(0f, hero.Velocity.x, 0.01f, "no push without a cling");
            CollectionAssert.Contains(new List<string>(Stubs.Reported), "Hero wall jump chain");
        }

        float WallJumpPathAt(float stepSeconds, out float y)
        {
            dt = stepSeconds;
            ClingToWall();
            PressJump();
            input.JumpButton.Release();
            Step(Steps(0.25f));
            // Measured from where the cling ended, so differences in the cling approach do not count.
            y = hero.transform.position.y;
            return hero.transform.position.x;
        }

        [Test]
        public void WallJump_HasSamePathAtFiftyAndOneHundredHertz()
        {
            float x50 = WallJumpPathAt(0.02f, out float y50);
            float x100 = WallJumpPathAt(0.01f, out float y100);
            // Both runs cling from the same start, but the cling begins at slightly different steps; compare
            // the horizontal push distance (from the wall) and the rise relative to the jump height.
            Assert.AreEqual(x50, x100, 0.2f);
            Assert.AreEqual(y50, y100, 0.6f);
        }

        [Test]
        public void WallJump_FromCling_RefreshesAirDashAndClearsCooldown()
        {
            BuildWorld(60f);
            Step(2);
            // Dash toward the wall in the air (facing right): spends the air dash and starts the cooldown.
            input.DashButton.Press();
            Step(1);
            input.DashButton.Release();
            Assert.AreEqual(HeroState.Dashing, hero.State);
            Step(Steps(config.DashSeconds) + 2);
            Assert.IsFalse(hero.Dash.CanAirDash, "test setup: air dash spent");

            // Cling to the wall: air dash and cooldown come back.
            input.Move = Right;
            Step(Steps(0.1f));
            Assert.IsTrue(hero.Wall.IsClinging);
            Assert.IsTrue(hero.Dash.CanAirDash, "cling refreshes the air dash");

            PressJump();
            input.JumpButton.Release();
            Assert.IsTrue(hero.Dash.CanAirDash);
            // Still inside the old cooldown: the dash only works because the cooldown was cleared.
            input.Move = Vector2.zero;
            input.DashButton.Press();
            Step(1);
            input.DashButton.Release();
            Assert.AreEqual(HeroState.Dashing, hero.State);
            Assert.Less(hero.Velocity.x, 0f, "dashes the way it faces, away from the wall");
        }

        [Test]
        public void WallDash_TowardTheWall_LogsStubForDashEndingAtWall()
        {
            BuildWorld(60f);
            Step(2);
            input.DashButton.Press();
            Step(1);
            input.DashButton.Release();
            Step(2);
            CollectionAssert.Contains(new List<string>(Stubs.Reported), "Hero dash ends at wall");
        }

        [Test]
        public void WallCling_WhenHurt_EndsClingAndDoesNotOverwriteState()
        {
            ClingToWall();
            SetState(HeroState.Hurt);
            Step(1);
            Assert.IsFalse(hero.Wall.IsClinging);
            // HeroMovement no longer rewrites Hurt/Dead (the damage task), so the state now survives the step.
            // The full real-API version of this is Hurt_WhileClinging_EndsClingAndStateStaysHurt in HeroHealthTests.
            Assert.AreEqual(HeroState.Hurt, hero.State, "neither the wall nor movement may overwrite hurt");
            // The slide cap is no longer applied, so gravity keeps pulling past it.
            Assert.Less(hero.Velocity.y, -config.WallSlideSpeed - 0.5f);
        }

        [Test]
        public void WallCling_WhenDead_EndsClingAndWallJumpIsIgnored()
        {
            ClingToWall();
            SetState(HeroState.Dead);
            input.JumpButton.Press();
            Step(1);
            input.JumpButton.Release();
            Assert.IsFalse(hero.Wall.IsClinging);
            Assert.IsFalse(hero.Wall.IsWallJumpLocked);
            Assert.Less(hero.Velocity.y, 0f, "keeps falling, no wall jump rise when dead");
        }

        [Test]
        public void WallCling_DisableThenEnable_ClearsClingAndLock()
        {
            ClingToWall();
            hero.Wall.enabled = false;
            hero.Wall.enabled = true;
            Assert.IsFalse(hero.Wall.IsClinging);
            Assert.IsFalse(hero.Wall.IsWallJumpLocked);
        }
    }
}
