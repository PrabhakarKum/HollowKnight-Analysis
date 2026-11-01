using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hollowknight.Silksong.Tests
{
    // PlayMode because the hero lives on real Rigidbody2D / Collider2D physics. Every test drives the
    // hero one real fixed step at a time (Step) and clears the fake input's edges after each step,
    // which is the same lifetime HeroActions gives them.
    public class HeroMovementTests
    {
        const float HeroHalfHeight = 0.5f;

        readonly System.Collections.Generic.List<Object> spawned = new System.Collections.Generic.List<Object>();
        float savedFixedDelta;
        HeroConfig config;
        FakeHeroInput input;
        HeroController hero;

        [SetUp]
        public void SetUp()
        {
            savedFixedDelta = Time.fixedDeltaTime;
            Time.fixedDeltaTime = 0.02f;
            config = ScriptableObject.CreateInstance<HeroConfig>();
            spawned.Add(config);
            input = new FakeHeroInput();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in spawned) if (o != null) Object.Destroy(o);
            spawned.Clear();
            Time.fixedDeltaTime = savedFixedDelta;
        }

        // Flat ground whose top is y = 0, or ground that ends at x = 0 (a ledge) so the hero can walk
        // off it. startX is the hero's centre; startHeight lifts it above the floor for landing tests.
        void BuildWorld(bool ledge, float startX, float startHeight)
        {
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
            hero.Config = config;
            hero.Input = input;
        }

        IEnumerator Step(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForFixedUpdate();
                input.EndFixedStep(Time.fixedDeltaTime);
            }
        }

        // Lets the hero settle on the ground so the first real assertion is not about spawning.
        IEnumerator Settle()
        {
            yield return Step(10);
            Assert.IsTrue(hero.IsGrounded, "hero should be standing on the ground after settling");
        }

        // Jump and release timings are expressed in whole steps so the tests do not depend on float
        // sums landing exactly on a config boundary.
        int StepsFor(float seconds) => Mathf.CeilToInt(seconds / Time.fixedDeltaTime - 0.001f);

        [UnityTest]
        public IEnumerator Run_HoldingRight_ReachesConfigRunSpeed()
        {
            BuildWorld(false, 0f, 0f);
            yield return Settle();
            input.Move = new Vector2(1f, 0f);
            yield return Step(10);
            Assert.AreEqual(config.RunSpeed, hero.Velocity.x, 0.01f);
            Assert.IsTrue(hero.FacingRight);

            input.Move = new Vector2(-1f, 0f);
            yield return Step(10);
            Assert.AreEqual(-config.RunSpeed, hero.Velocity.x, 0.01f);
            Assert.IsFalse(hero.FacingRight);
        }

        [UnityTest]
        public IEnumerator Move_BelowHorizontalThreshold_DoesNotRun()
        {
            BuildWorld(false, 0f, 0f);
            yield return Settle();
            input.Move = new Vector2(config.HorizontalPressThreshold - 0.01f, 0f);
            yield return Step(10);
            Assert.AreEqual(0f, hero.Velocity.x, 0.01f);
        }

        [UnityTest]
        public IEnumerator Move_AboveHorizontalThreshold_Runs()
        {
            BuildWorld(false, 0f, 0f);
            yield return Settle();
            input.Move = new Vector2(config.HorizontalPressThreshold + 0.01f, 0f);
            yield return Step(10);
            // The original treats a tilted stick as a plain "right" press, so speed is not scaled.
            Assert.AreEqual(config.RunSpeed, hero.Velocity.x, 0.01f);
        }

        [UnityTest]
        public IEnumerator Move_VerticalThreshold_UsesLargerThanHorizontal()
        {
            BuildWorld(false, 0f, 0f);
            yield return Settle();

            input.Move = new Vector2(0f, config.VerticalPressThreshold - 0.01f);
            yield return Step(2);
            Assert.IsFalse(hero.WantsUp, "0.49 up is below the vertical threshold");

            input.Move = new Vector2(0f, config.VerticalPressThreshold + 0.01f);
            yield return Step(2);
            Assert.IsTrue(hero.WantsUp);

            input.Move = new Vector2(0f, -(config.VerticalPressThreshold - 0.01f));
            yield return Step(2);
            Assert.IsFalse(hero.WantsDown);

            input.Move = new Vector2(0f, -(config.VerticalPressThreshold + 0.01f));
            yield return Step(2);
            Assert.IsTrue(hero.WantsDown);
            Assert.IsFalse(hero.WantsUp);
        }

        // Returns the apex height above the starting floor position for a jump that is either held
        // for as long as the test runs or tapped for a single step.
        float apexResult;
        IEnumerator MeasureApex(bool hold)
        {
            yield return Settle();
            float startY = hero.transform.position.y;
            float maxY = startY;
            input.JumpButton.Press();
            int total = StepsFor(1.6f);
            for (int i = 0; i < total; i++)
            {
                if (!hold && i == 1) input.JumpButton.Release();
                yield return Step(1);
                maxY = Mathf.Max(maxY, hero.transform.position.y);
            }
            apexResult = maxY - startY;
        }

        [UnityTest]
        public IEnumerator Jump_HeldFullDuration_ReachesHigherApexThanTap()
        {
            BuildWorld(false, 0f, 0f);
            yield return MeasureApex(hold: true);
            float held = apexResult;
            input.JumpButton.Release();
            yield return Step(2);
            Object.Destroy(hero.gameObject);

            // Second world for the tap so the first jump cannot leave the hero mid-air.
            BuildWorld(false, 20f, 0f);
            yield return MeasureApex(hold: false);
            float tap = apexResult;

            Assert.Greater(held, 0.5f, "held jump should leave the ground clearly");
            Assert.Greater(tap, 0.1f, "even a tap must jump");
            Assert.Greater(held, tap * 2f, "full hold should be much higher than a tap");
        }

        [UnityTest]
        public IEnumerator Jump_Released_CutsRisingVelocity()
        {
            BuildWorld(false, 0f, 0f);
            yield return Settle();
            input.JumpButton.Press();
            // Past the minimum hold and still inside the maximum, so the jump is still being sustained.
            yield return Step(StepsFor(config.JumpHoldMinSeconds) + 2);
            float before = hero.Velocity.y;
            Assert.AreEqual(config.JumpSpeed, before, 0.5f, "still sustaining the jump");

            input.JumpButton.Release();
            yield return Step(1);
            float after = hero.Velocity.y;
            Assert.Greater(after, 0f, "release cuts the rise, it does not stop it dead");
            Assert.Less(after, before * 0.6f, "release roughly halves the rising velocity");
        }

        // Measures how many steps a drop takes to land, so a later identical drop can press jump a
        // chosen number of steps before the landing step.
        int landingStep;
        IEnumerator MeasureLanding(float height)
        {
            BuildWorld(false, 0f, height);
            bool landed = false;
            hero.Landed += () => landed = true;
            landingStep = 0;
            while (!landed && landingStep < 200)
            {
                yield return Step(1);
                landingStep++;
            }
            Assert.IsTrue(landed, "drop should land");
            Object.Destroy(hero.gameObject);
            yield return null;
        }

        IEnumerator DropAndPressBeforeLanding(float height, int stepsBefore)
        {
            BuildWorld(false, 0f, height);
            input.Move = Vector2.zero;
            for (int i = 1; i <= landingStep; i++)
            {
                if (i == landingStep - stepsBefore) input.JumpButton.Press();
                yield return Step(1);
            }
        }

        [UnityTest]
        public IEnumerator JumpBuffer_PressedWithinConfigWindowBeforeLanding_JumpsOnLanding()
        {
            const float height = 1.5f;
            yield return MeasureLanding(height);
            // One step (0.02 s) before landing is inside the 0.04 s window.
            Assert.Less(Time.fixedDeltaTime, config.JumpBufferSeconds);
            yield return DropAndPressBeforeLanding(height, 1);
            Assert.Greater(hero.Velocity.y, 5f, "buffered press should turn into a jump on landing");
        }

        [UnityTest]
        public IEnumerator JumpBuffer_JustOutsideWindow_DoesNotJump()
        {
            const float height = 1.5f;
            yield return MeasureLanding(height);
            int outside = StepsFor(config.JumpBufferSeconds) + 2;
            Assume.That(landingStep > outside + 1, "drop is too short for this check");
            yield return DropAndPressBeforeLanding(height, outside);
            Assert.LessOrEqual(hero.Velocity.y, 0.5f, "press older than the buffer window must be forgotten");
            Assert.IsTrue(hero.IsGrounded);
        }

        IEnumerator WalkOffLedge()
        {
            BuildWorld(true, -3f, 0f);
            yield return Settle();
            input.Move = new Vector2(1f, 0f);
            int guard = 0;
            while (hero.IsGrounded && guard++ < 300) yield return Step(1);
            Assert.IsFalse(hero.IsGrounded, "hero should have walked off the ledge");
        }

        [UnityTest]
        public IEnumerator Coyote_JumpWithinConfigWindowAfterWalkingOffLedge_Jumps()
        {
            yield return WalkOffLedge();
            yield return Step(1); // one step, 0.02 s, after leaving: inside the window
            Assert.Less(Time.fixedDeltaTime * 2f, config.CoyoteSeconds + 0.0001f);
            input.JumpButton.Press();
            yield return Step(1);
            Assert.Greater(hero.Velocity.y, 5f, "jump pressed shortly after leaving the ledge should work");
        }

        [UnityTest]
        public IEnumerator Coyote_AfterWindow_DoesNotJump()
        {
            yield return WalkOffLedge();
            yield return Step(StepsFor(config.CoyoteSeconds) + 3);
            input.JumpButton.Press();
            yield return Step(1);
            Assert.Less(hero.Velocity.y, 0f, "no air jump once the coyote window is over");
        }

        [UnityTest]
        public IEnumerator Apex_IsSameAcrossPhysicsRates()
        {
            Time.fixedDeltaTime = 0.02f;
            BuildWorld(false, 0f, 0f);
            yield return MeasureApex(hold: true);
            float apex50 = apexResult;
            Object.Destroy(hero.gameObject);
            yield return null;

            Time.fixedDeltaTime = 0.01f;
            BuildWorld(false, 20f, 0f);
            yield return MeasureApex(hold: true);
            float apex100 = apexResult;

            Assert.Greater(apex50, 1f);
            Assert.AreEqual(apex50, apex100, apex50 * 0.05f,
                $"apex at 50 Hz ({apex50:F3}) and 100 Hz ({apex100:F3}) must agree within 5%");
        }
    }
}
