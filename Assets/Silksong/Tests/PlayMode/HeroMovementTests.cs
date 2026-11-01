using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // The hero lives on real Rigidbody2D / Collider2D physics, but the tests own the clock: physics
    // runs in Script mode and every step is an explicit StepFixed + Physics2D.Simulate pair, followed by
    // clearing the fake input's edges (the lifetime HeroActions gives them). So a slow frame can never
    // add or skip a step, and window boundaries can be tested to the exact step.
    public class HeroMovementTests
    {
        const float HeroHalfHeight = 0.5f;
        const float DropHeight = 1.5f;

        readonly List<UnityEngine.Object> spawned = new List<UnityEngine.Object>();
        SimulationMode2D savedMode;
        float dt = 0.02f;
        HeroConfig config;
        FakeHeroInput input;
        HeroController hero;

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            dt = 0.02f;
            config = ScriptableObject.CreateInstance<HeroConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            ClearWorld();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
            Physics2D.simulationMode = savedMode;
        }

        // Immediate destruction so a second world in the same test never overlaps the first.
        void ClearWorld()
        {
            foreach (var o in spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            spawned.Clear();
            hero = null;
        }

        // Flat ground whose top is y = 0, or ground that ends at x = 0 (a ledge). Always builds a fresh
        // fake input so a button held in an earlier world cannot leak into this one.
        void BuildWorld(bool ledge, float startX, float startHeight)
        {
            ClearWorld();
            input = new FakeHeroInput();

            var ground = new GameObject("Ground");
            spawned.Add(ground);
            var box = ground.AddComponent<BoxCollider2D>();
            box.size = new Vector2(100f, 1f);
            ground.transform.position = ledge ? new Vector3(-50f, -0.5f, 0f) : new Vector3(0f, -0.5f, 0f);

            var go = new GameObject("Hero");
            spawned.Add(go);
            go.transform.position = new Vector3(startX, HeroHalfHeight + 0.01f + startHeight, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.5f, 1f);
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

        void Settle()
        {
            Step(Mathf.RoundToInt(0.2f / dt));
            Assert.IsTrue(hero.IsGrounded, "hero should be standing on the ground after settling");
        }

        int StepsFor(float seconds) => Mathf.CeilToInt(seconds / dt - 0.001f);
        // Whole steps that fit inside a window, counting the first step as age 0.
        int StepsInside(float seconds) => Mathf.FloorToInt(seconds / dt + 0.001f);

        [Test]
        public void Run_HoldingRight_ReachesConfigRunSpeed()
        {
            BuildWorld(false, 0f, 0f);
            Settle();
            input.Move = new Vector2(1f, 0f);
            Step(10);
            Assert.AreEqual(config.RunSpeed, hero.Velocity.x, 0.01f);
            Assert.IsTrue(hero.FacingRight);

            input.Move = new Vector2(-1f, 0f);
            Step(10);
            Assert.AreEqual(-config.RunSpeed, hero.Velocity.x, 0.01f);
            Assert.IsFalse(hero.FacingRight);
        }

        [Test]
        public void Move_BelowHorizontalThreshold_DoesNotRun()
        {
            BuildWorld(false, 0f, 0f);
            Settle();
            input.Move = new Vector2(config.HorizontalPressThreshold - 0.01f, 0f);
            Step(10);
            Assert.AreEqual(0f, hero.Velocity.x, 0.01f);
        }

        [Test]
        public void Move_AboveHorizontalThreshold_Runs()
        {
            BuildWorld(false, 0f, 0f);
            Settle();
            input.Move = new Vector2(config.HorizontalPressThreshold + 0.01f, 0f);
            Step(10);
            // The original treats a tilted stick as a plain "right" press, so speed is not scaled.
            Assert.AreEqual(config.RunSpeed, hero.Velocity.x, 0.01f);
        }

        [Test]
        public void Move_VerticalThreshold_UsesLargerThanHorizontal()
        {
            BuildWorld(false, 0f, 0f);
            Settle();

            input.Move = new Vector2(0f, config.VerticalPressThreshold - 0.01f);
            Step(2);
            Assert.IsFalse(hero.WantsUp, "0.49 up is below the vertical threshold");

            input.Move = new Vector2(0f, config.VerticalPressThreshold + 0.01f);
            Step(2);
            Assert.IsTrue(hero.WantsUp);

            input.Move = new Vector2(0f, -(config.VerticalPressThreshold - 0.01f));
            Step(2);
            Assert.IsFalse(hero.WantsDown);

            input.Move = new Vector2(0f, -(config.VerticalPressThreshold + 0.01f));
            Step(2);
            Assert.IsTrue(hero.WantsDown);
            Assert.IsFalse(hero.WantsUp);
        }

        // Apex height above the floor for a jump held for the whole run, or tapped for one step.
        float MeasureApex(float stepLength, bool hold)
        {
            dt = stepLength;
            BuildWorld(false, 0f, 0f);
            Settle();
            float startY = hero.transform.position.y;
            float maxY = startY;
            input.JumpButton.Press();
            int total = Mathf.RoundToInt(1.6f / dt);
            for (int i = 0; i < total; i++)
            {
                if (!hold && i == 1) input.JumpButton.Release();
                Step(1);
                maxY = Mathf.Max(maxY, hero.transform.position.y);
            }
            return maxY - startY;
        }

        [Test]
        public void Jump_HeldFullDuration_ReachesHigherApexThanTap()
        {
            float held = MeasureApex(0.02f, true);
            float tap = MeasureApex(0.02f, false);

            Assert.Greater(held, 0.5f, "held jump should leave the ground clearly");
            Assert.Greater(tap, 0.1f, "even a tap must jump");
            Assert.Greater(held, tap * 2f, $"full hold ({held:F3}) should be much higher than a tap ({tap:F3})");
        }

        [Test]
        public void Jump_Released_CutsRisingVelocity()
        {
            BuildWorld(false, 0f, 0f);
            Settle();
            input.JumpButton.Press();
            // Past the minimum hold and still inside the maximum, so the jump is still being sustained.
            Step(StepsFor(config.JumpHoldMinSeconds) + 2);
            float before = hero.Velocity.y;
            Assert.AreEqual(config.JumpSpeed, before, 0.5f, "still sustaining the jump");

            input.JumpButton.Release();
            Step(1);
            float after = hero.Velocity.y;
            Assert.Greater(after, 0f, "release cuts the rise, it does not stop it dead");
            Assert.Less(after, before * 0.6f, "release roughly halves the rising velocity");
        }

        [Test]
        public void Jump_TapWithinOneFixedStep_StillJumps()
        {
            BuildWorld(false, 0f, 0f);
            Settle();
            float startY = hero.transform.position.y;
            // Press and release before the hero's next step: both edges are latched into that step.
            input.JumpButton.Press();
            input.JumpButton.Release();
            Step(1);
            Assert.AreEqual(config.JumpSpeed, hero.Velocity.y, 0.5f, "a one-step tap must still start a jump");

            // It behaves as a minimum-hold tap: rises, then is cut, ending far below a full jump.
            float maxY = hero.transform.position.y;
            for (int i = 0; i < 40; i++) { Step(1); maxY = Mathf.Max(maxY, hero.transform.position.y); }
            float apex = maxY - startY;
            Assert.Greater(apex, 0.1f);
            Assert.Less(apex, 2.5f);
        }

        // Steps a drop from `height` takes before the hero reports a landing.
        int MeasureLanding(float height)
        {
            BuildWorld(false, 0f, height);
            bool landed = false;
            hero.Landed += () => landed = true;
            int steps = 0;
            while (!landed && steps < 300) { Step(1); steps++; }
            Assert.IsTrue(landed, "drop should land");
            return steps;
        }

        // Repeats the drop; beforeStep(i) runs before step i (1-based) so tests choose when to press.
        void Drop(float height, int landing, Action<int> beforeStep)
        {
            BuildWorld(false, 0f, height);
            for (int i = 1; i <= landing; i++)
            {
                beforeStep(i);
                Step(1);
            }
        }

        void PressBeforeLanding(int stepsBefore, bool alsoRelease = false)
        {
            int landing = MeasureLanding(DropHeight);
            Drop(DropHeight, landing, i =>
            {
                if (i == landing - stepsBefore)
                {
                    input.JumpButton.Press();
                    if (alsoRelease) input.JumpButton.Release();
                }
            });
        }

        [Test]
        public void JumpBuffer_PressedWithinConfigWindowBeforeLanding_JumpsOnLanding()
        {
            PressBeforeLanding(1);
            Assert.Greater(hero.Velocity.y, 5f, "buffered press should turn into a jump on landing");
        }

        [Test]
        public void JumpBuffer_JustOutsideWindow_DoesNotJump()
        {
            PressBeforeLanding(StepsFor(config.JumpBufferSeconds) + 2);
            Assert.LessOrEqual(hero.Velocity.y, 0.5f, "press older than the buffer window must be forgotten");
            Assert.IsTrue(hero.IsGrounded);
        }

        [Test]
        public void JumpBuffer_LastStepInsideWindow_Jumps()
        {
            PressBeforeLanding(StepsInside(config.JumpBufferSeconds));
            Assert.Greater(hero.Velocity.y, 5f, "a press exactly JumpBufferSeconds old still counts");
        }

        [Test]
        public void JumpBuffer_FirstStepOutsideWindow_DoesNotJump()
        {
            PressBeforeLanding(StepsInside(config.JumpBufferSeconds) + 1);
            Assert.LessOrEqual(hero.Velocity.y, 0.5f, "one step older than the window must not jump");
        }

        [Test]
        public void Jump_TapWithinOneFixedStep_ViaBuffer_JumpsOnLanding()
        {
            PressBeforeLanding(1, alsoRelease: true);
            Assert.Greater(hero.Velocity.y, 5f, "a tap seen in one step arms the buffer even though the button is up at landing");
        }

        [Test]
        public void Jump_ReleasedLaterStep_DropsBuffer()
        {
            int landing = MeasureLanding(DropHeight);
            Drop(DropHeight, landing, i =>
            {
                if (i == landing - 2) input.JumpButton.Press();
                if (i == landing - 1) input.JumpButton.Release();
            });
            Assert.LessOrEqual(hero.Velocity.y, 0.5f, "release in a later step cancels the queued jump");
            Assert.IsTrue(hero.IsGrounded);
        }

        void WalkOffLedge()
        {
            BuildWorld(true, -3f, 0f);
            Settle();
            input.Move = new Vector2(1f, 0f);
            int guard = 0;
            while (hero.IsGrounded && guard++ < 300) Step(1);
            Assert.IsFalse(hero.IsGrounded, "hero should have walked off the ledge");
        }

        // The step on which the hero first reports not grounded has age 0; this presses jump so that
        // it is processed on the step of the given age (in steps).
        void PressAtAge(int ageInSteps)
        {
            WalkOffLedge();
            Step(ageInSteps - 1);
            input.JumpButton.Press();
            Step(1);
        }

        [Test]
        public void Coyote_JumpWithinConfigWindowAfterWalkingOffLedge_Jumps()
        {
            PressAtAge(2);
            Assert.Greater(hero.Velocity.y, 5f, "jump pressed shortly after leaving the ledge should work");
        }

        [Test]
        public void Coyote_AfterWindow_DoesNotJump()
        {
            PressAtAge(StepsFor(config.CoyoteSeconds) + 3);
            Assert.Less(hero.Velocity.y, 0f, "no air jump once the coyote window is over");
        }

        [Test]
        public void Coyote_LastStepInsideWindow_Jumps()
        {
            PressAtAge(StepsInside(config.CoyoteSeconds));
            Assert.Greater(hero.Velocity.y, 5f);
        }

        [Test]
        public void Coyote_FirstStepOutsideWindow_DoesNotJump()
        {
            PressAtAge(StepsInside(config.CoyoteSeconds) + 1);
            Assert.Less(hero.Velocity.y, 0f);
        }

        [Test]
        public void Fall_ReachesFloorWithinSkinWidth()
        {
            BuildWorld(false, 0f, 0.5f);
            Step(Mathf.RoundToInt(1f / dt));
            float gap = hero.transform.position.y - HeroHalfHeight; // floor top is y = 0
            Assert.IsTrue(hero.IsGrounded);
            Assert.LessOrEqual(gap, Physics2D.defaultContactOffset * 2f, $"hero hovers {gap:F3} above the floor");
        }

        [Test]
        public void Gravity_AccelerationMatchesConfig()
        {
            BuildWorld(false, 0f, 50f);
            Step(10);
            Assert.AreEqual(-config.Gravity * 10 * dt, hero.Velocity.y, 0.01f);
        }

        [Test]
        public void Fall_CapsAtFallSpeedMax()
        {
            BuildWorld(false, 0f, 300f);
            Step(Mathf.RoundToInt(2f / dt));
            Assert.AreEqual(-config.FallSpeedMax, hero.Velocity.y, 0.01f);
        }

        [Test]
        public void Apex_IsSameAcrossPhysicsRates()
        {
            // A fresh world and a fresh fake input per rate: a button still held from the first run
            // would make the second Press() a no-op and the hero would never jump.
            float apex50 = MeasureApex(0.02f, true);
            float apex100 = MeasureApex(0.01f, true);

            Assert.Greater(apex50, 1f);
            Assert.AreEqual(apex50, apex100, apex50 * 0.05f,
                $"apex at 50 Hz ({apex50:F3}) and 100 Hz ({apex100:F3}) must agree within 5%");
        }
    }
}
