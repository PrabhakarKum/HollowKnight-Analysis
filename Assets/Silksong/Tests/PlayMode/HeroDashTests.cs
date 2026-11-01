using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // Same deterministic setup as HeroMovementTests: Script-mode physics, explicit StepFixed + Simulate
    // pairs, input edges cleared after each step like the real latch.
    public class HeroDashTests
    {
        const float HeroHalfHeight = 0.5f;

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
            // Stubs append to a doc in the editor; keep test runs out of the real docs/study/STUBS.md.
            stubsPath = Path.Combine(Path.GetTempPath(), "dash-tests-stubs-" + System.Guid.NewGuid() + ".md");
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

        // Flat ground whose top is y = 0, very wide; the hero starts startHeight above it.
        void BuildWorld(float startHeight)
        {
            ClearWorld();
            input = new FakeHeroInput();

            var ground = new GameObject("Ground");
            spawned.Add(ground);
            var box = ground.AddComponent<BoxCollider2D>();
            box.size = new Vector2(400f, 1f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);

            var go = new GameObject("Hero");
            spawned.Add(go);
            go.transform.position = new Vector3(0f, HeroHalfHeight + 0.01f + startHeight, 0f);
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

        int CooldownSteps() => Mathf.CeilToInt(config.DashCooldownSeconds / dt) + 2;

        // Steps until the dash state ends (with a safety cap) and returns how many steps it lasted.
        int StepUntilDashEnds()
        {
            int steps = 0;
            while (hero.State == HeroState.Dashing && steps < 1000) { Step(1); steps++; }
            Assert.Less(steps, 1000, "dash never ended");
            return steps;
        }

        // Presses dash for one step and returns after that step.
        void PressDash()
        {
            input.DashButton.Press();
            Step(1);
            input.DashButton.Release();
        }

        [Test]
        public void Dash_Ground_TravelsDashSpeedTimesDashSecondsAndEntersDashingState()
        {
            BuildWorld(0f);
            Settle();
            float startX = hero.transform.position.x;

            PressDash();
            Assert.AreEqual(HeroState.Dashing, hero.State);
            StepUntilDashEnds();

            Assert.AreEqual(config.DashSpeed * config.DashSeconds, hero.transform.position.x - startX, 0.05f);
        }

        [Test]
        public void Dash_DuringCooldown_IsIgnored()
        {
            BuildWorld(0f);
            Settle();
            PressDash();
            StepUntilDashEnds();

            // Inside the cooldown (which runs from the dash start) and past the dash itself.
            Step(2);
            PressDash();
            Assert.AreNotEqual(HeroState.Dashing, hero.State, "a press inside the cooldown must be ignored");

            // Once the whole cooldown has passed a new press works.
            Step(CooldownSteps());
            PressDash();
            Assert.AreEqual(HeroState.Dashing, hero.State);
        }

        [Test]
        public void AirDash_OnlyOncePerAirtime_RefreshedOnLanding()
        {
            // High enough that the whole sequence happens before landing.
            BuildWorld(60f);
            Step(2);
            Assert.IsFalse(hero.IsGrounded);
            Assert.IsTrue(hero.Dash.CanAirDash);

            PressDash();
            Assert.AreEqual(HeroState.Dashing, hero.State, "the first air dash should work");
            Assert.IsFalse(hero.Dash.CanAirDash);
            StepUntilDashEnds();

            // Wait out the cooldown while still in the air; the second air dash must still be refused.
            Step(CooldownSteps());
            Assert.IsFalse(hero.IsGrounded, "test setup: hero should still be falling");
            PressDash();
            Assert.AreNotEqual(HeroState.Dashing, hero.State, "only one air dash per airtime");

            // Land, then jump: the air dash is back.
            int guard = 0;
            while (!hero.IsGrounded && guard++ < 1000) Step(1);
            Assert.IsTrue(hero.IsGrounded, "hero should land");
            Assert.IsTrue(hero.Dash.CanAirDash, "the allowance is back the moment the hero lands");
            Step(CooldownSteps());
            input.JumpButton.Press();
            Step(1);
            input.JumpButton.Release();
            Step(2);
            Assert.IsFalse(hero.IsGrounded);
            Assert.IsTrue(hero.Dash.CanAirDash, "landing refreshes the air dash");
            PressDash();
            Assert.AreEqual(HeroState.Dashing, hero.State);
        }

        [Test]
        public void Dash_TapWithinOneFixedStep_StillDashes()
        {
            BuildWorld(0f);
            Settle();
            input.DashButton.Press();
            input.DashButton.Release();
            Step(1);
            Assert.AreEqual(HeroState.Dashing, hero.State);
            Assert.AreEqual(config.DashSpeed, hero.Velocity.x, 0.01f);
        }

        float DashDistanceAt(float stepSeconds)
        {
            dt = stepSeconds;
            BuildWorld(0f);
            Settle();
            float startX = hero.transform.position.x;
            PressDash();
            StepUntilDashEnds();
            return hero.transform.position.x - startX;
        }

        [Test]
        public void Dash_HasSamePathAtFiftyAndOneHundredHertz()
        {
            float at50 = DashDistanceAt(0.02f);
            float at100 = DashDistanceAt(0.01f);
            Assert.Greater(at50, 0f);
            Assert.AreEqual(at50, at100, at50 * 0.02f);
        }

        [Test]
        public void Dash_ZeroesGravityDuringDashAndRestoresAfter()
        {
            BuildWorld(60f);
            Step(2);
            Assert.Less(hero.Velocity.y, 0f, "test setup: falling before the dash");

            PressDash();
            float yAtStart = hero.transform.position.y;
            while (hero.State == HeroState.Dashing)
            {
                Assert.AreEqual(0f, hero.Velocity.y, 0.001f, "no vertical speed during the dash");
                Assert.AreEqual(yAtStart, hero.transform.position.y, 0.01f, "no falling during the dash");
                Step(1);
            }

            Step(3);
            Assert.Less(hero.Velocity.y, -0.5f, "gravity is back after the dash");
            Assert.Less(hero.transform.position.y, yAtStart);
        }

        [Test]
        public void Dash_EndsAndMovementResumes()
        {
            BuildWorld(0f);
            Settle();
            PressDash();
            StepUntilDashEnds();
            Assert.AreEqual(HeroState.Grounded, hero.State);
            Step(2);
            Assert.AreEqual(0f, hero.Velocity.x, 0.01f, "standing still after the dash");

            input.Move = new Vector2(1f, 0f);
            Step(3);
            Assert.AreEqual(config.RunSpeed, hero.Velocity.x, 0.01f, "run control is back");
        }

        [Test]
        public void Dash_PressDuringDash_IsIgnored()
        {
            BuildWorld(0f);
            Settle();
            PressDash();
            int stepsSoFar = 1;
            // A second press in the middle of the dash must neither restart nor extend it.
            PressDash();
            stepsSoFar++;
            int total = stepsSoFar + StepUntilDashEnds();
            // Moving steps, plus the one step on which the dash hands back to movement.
            int expected = Mathf.CeilToInt(config.DashSeconds / dt - 0.001f) + 1;
            Assert.AreEqual(expected, total, "a press during the dash must not extend it");
        }

        [Test]
        public void Dash_AirDownWithNoSideways_LogsStubAndDashesSideways()
        {
            BuildWorld(60f);
            Step(2);
            input.Move = new Vector2(0f, -1f);
            PressDash();
            Assert.AreEqual(HeroState.Dashing, hero.State);
            Assert.AreEqual(config.DashSpeed, hero.Velocity.x, 0.01f, "falls back to a normal sideways dash");
            Assert.AreEqual(0f, hero.Velocity.y, 0.001f);
            CollectionAssert.Contains(new List<string>(Stubs.Reported), "Hero down dash");
        }

        // Hurt or Dead set mid-dash must end the dash instead of being overwritten (HeroDash.FixedStep). It is
        // not tested here because HeroController.State has an internal setter and no damage API exists yet;
        // Task 10 (damage) should add a test once hurt can be triggered through the real API.

        [Test]
        public void Dash_FacingDirectionFollowsRule()
        {
            BuildWorld(0f);
            Settle();

            // No input: the way the hero already faces (right by default).
            Assert.IsTrue(hero.FacingRight);
            PressDash();
            Assert.Greater(hero.Velocity.x, 0f);
            StepUntilDashEnds();
            Step(CooldownSteps());

            // Held left: dash left and turn to face left.
            input.Move = new Vector2(-1f, 0f);
            PressDash();
            Assert.Less(hero.Velocity.x, 0f);
            Assert.IsFalse(hero.FacingRight);
            StepUntilDashEnds();
            input.Move = Vector2.zero;
            Step(CooldownSteps());

            // No input again: it keeps the left facing.
            PressDash();
            Assert.Less(hero.Velocity.x, 0f);
            Assert.IsFalse(hero.FacingRight);
            StepUntilDashEnds();
            Step(CooldownSteps());

            // Held right turns it back.
            input.Move = new Vector2(1f, 0f);
            PressDash();
            Assert.Greater(hero.Velocity.x, 0f);
            Assert.IsTrue(hero.FacingRight);
        }
    }
}
