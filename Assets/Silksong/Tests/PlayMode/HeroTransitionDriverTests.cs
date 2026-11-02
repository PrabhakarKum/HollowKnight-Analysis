using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // Same deterministic setup as the other hero tests: Script-mode physics, explicit StepFixed + Simulate pairs. The
    // EnterRoom coroutine is advanced by hand (one MoveNext per step), so no real time passes. Floor top at y = 0.
    // Every timing test runs at 50 and 100 Hz.
    public class HeroTransitionDriverTests
    {
        const float HeroHalfWidth = 0.25f;
        const float HeroHalfHeight = 0.5f;

        readonly List<Object> spawned = new List<Object>();
        SimulationMode2D savedMode;
        float dt;
        HeroConfig config;
        FakeHeroInput input;
        HeroController hero;
        HeroTransitionDriver driver;
        IEnumerator entering;
        string stubsPath;
        int landedCount;

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            dt = 0.02f;
            config = ScriptableObject.CreateInstance<HeroConfig>();
            stubsPath = Path.Combine(Path.GetTempPath(), "transition-tests-stubs-" + System.Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in spawned) if (o != null) Object.DestroyImmediate(o);
            spawned.Clear();
            if (config != null) Object.DestroyImmediate(config);
            Physics2D.simulationMode = savedMode;
            Stubs.StubsDocPath = null;
            if (File.Exists(stubsPath)) File.Delete(stubsPath);
        }

        void BuildWorld(float heroX = 0f, float startHeight = 0f)
        {
            input = new FakeHeroInput();
            landedCount = 0;
            entering = null;

            var ground = new GameObject("Ground");
            spawned.Add(ground);
            ground.AddComponent<BoxCollider2D>().size = new Vector2(400f, 1f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);

            var go = new GameObject("Hero");
            spawned.Add(go);
            go.transform.position = new Vector3(heroX, HeroHalfHeight + 0.01f + startHeight, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(HeroHalfWidth * 2f, HeroHalfHeight * 2f);
            hero = go.AddComponent<HeroController>();
            hero.ManualStepping = true;
            hero.Config = config;
            hero.Input = input;
            hero.Health.DeathRespawnDelaySeconds = -1f;
            hero.Landed += () => landedCount++;
            driver = hero.Transition;
            Physics2D.SyncTransforms();
        }

        TransitionPoint MakeGate(string name, Vector2 pos, Vector2 offset)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 1f);
            box.isTrigger = true;
            var gate = go.AddComponent<TransitionPoint>();
            gate.Configure("Other", "right1", offset);
            Physics2D.SyncTransforms();
            return gate;
        }

        void Step(int count)
        {
            for (int i = 0; i < count; i++)
            {
                hero.StepFixed(dt);
                Physics2D.Simulate(dt);
                input.EndFixedStep(dt);
                if (entering != null && !entering.MoveNext()) entering = null;
            }
        }

        int Steps(float seconds) => Mathf.RoundToInt(seconds / dt);

        void Settle()
        {
            Step(Steps(0.2f));
            Assert.IsTrue(hero.IsGrounded, "test setup: hero should stand on the ground");
        }

        void StartEntering(TransitionPoint gate, float delay = 0f)
        {
            entering = driver.EnterRoom(gate, delay);
            if (!entering.MoveNext()) entering = null;
        }

        // Steps until the controls come back; fails if that takes longer than maxSeconds. Returns steps taken.
        int StepUntilUnlocked(float maxSeconds)
        {
            int max = Steps(maxSeconds), n = 0;
            while (hero.ControlLocked && n < max) { Step(1); n++; }
            Assert.IsFalse(hero.ControlLocked, "controls should have come back within " + maxSeconds + " s");
            return n;
        }

        bool Hit(int damage)
        {
            var src = new GameObject("HitSource");
            spawned.Add(src);
            src.transform.position = new Vector3(hero.transform.position.x - 2f, 0.5f, 0f);
            return hero.Health.TakeHit(new HitInstance { Damage = damage, Direction = AttackDirection.Normal, Source = src });
        }

        // ------------------------------------------------------------------ leaving

        [TestCase(0.02f)]
        [TestCase(0.01f)]
        public void LeaveRight_MovesOutAtRunSpeed_AndIgnoresInput(float step)
        {
            dt = step;
            BuildWorld();
            Settle();

            driver.LeaveRoom(GatePosition.Right);
            // Pressing everything must change nothing.
            input.Move = new Vector2(-1f, 0f);
            input.JumpButton.Press();
            input.DashButton.Press();
            Step(Steps(0.2f));

            Assert.IsTrue(hero.ControlLocked);
            Assert.AreEqual(config.RunSpeed, hero.Velocity.x, 0.01f);
            Assert.AreEqual(0f, hero.Velocity.y, 0.1f, "the jump press must be ignored");
            Assert.IsTrue(hero.FacingRight);
            Assert.IsFalse(hero.Dash.IsDashing);
        }

        [Test]
        public void LeaveLeft_MovesOutAtNegativeRunSpeed()
        {
            BuildWorld();
            Settle();
            driver.LeaveRoom(GatePosition.Left);
            Step(Steps(0.2f));
            Assert.AreEqual(-config.RunSpeed, hero.Velocity.x, 0.01f);
            Assert.IsFalse(hero.FacingRight);
        }

        [Test]
        public void LeaveTop_PushesUpward()
        {
            BuildWorld();
            Settle();
            driver.LeaveRoom(GatePosition.Top);
            Step(2);
            Assert.AreEqual(config.TransitionLeaveUpSpeed, hero.Velocity.y, 0.01f);
            Assert.AreEqual(0f, hero.Velocity.x, 0.01f);
        }

        [Test]
        public void LeaveBottom_KeepsFallingUnderGravity()
        {
            BuildWorld(0f, 5f);
            driver.LeaveRoom(GatePosition.Bottom);
            Step(5);
            Assert.Less(hero.Velocity.y, 0f);
            Assert.AreEqual(0f, hero.Velocity.x, 0.01f);
        }

        [Test]
        public void Leaving_HeroCannotBeHurt()
        {
            BuildWorld();
            Settle();
            driver.LeaveRoom(GatePosition.Right);
            Step(3);
            Assert.IsTrue(driver.IsInvulnerable);
            Assert.IsFalse(Hit(1));
            Assert.AreEqual(hero.Health.MaxHealth, hero.Health.Health);
        }

        [Test]
        public void Leaving_CancelsADashInProgress()
        {
            BuildWorld();
            Settle();
            input.DashButton.Press();
            Step(2);
            Assert.IsTrue(hero.Dash.IsDashing, "test setup");
            driver.LeaveRoom(GatePosition.Right);
            Step(1);
            Assert.IsFalse(hero.Dash.IsDashing);
        }

        [Test]
        public void TransitionState_FacingIsWorldSpace_AndBusyOnlyWhenStunned()
        {
            BuildWorld();
            Settle();
            hero.FacingRight = true;
            Assert.IsTrue(driver.GetTransitionState().FacingRight);
            hero.FacingRight = false;
            Assert.IsFalse(driver.GetTransitionState().FacingRight);
            Assert.IsFalse(driver.GetTransitionState().Busy);
            driver.LeaveRoom(GatePosition.Left);
            Assert.IsFalse(driver.GetTransitionState().Busy, "a locked hero is not busy");
            Assert.IsFalse(driver.GetTransitionState().InPosition, "not in position while leaving");
            Assert.AreSame(hero.GetComponent<Rigidbody2D>(), driver.Body);
        }

        // ------------------------------------------------------------------ entering sideways

        [TestCase(0.02f)]
        [TestCase(0.01f)]
        public void EnterLeftGate_PlacesOnGround_FacesInward_WalksThenReturnsControl(float step)
        {
            dt = step;
            BuildWorld(-20f);
            Settle();
            var gate = MakeGate("left1", new Vector2(10f, 4f), new Vector2(1f, 0f));

            driver.LeaveRoom(GatePosition.Right);
            hero.FacingRight = false;
            StartEntering(gate);

            Assert.AreEqual(11f, hero.transform.position.x, 0.001f, "gate x plus entry offset");
            Assert.AreEqual(HeroHalfHeight, hero.transform.position.y, 0.05f, "snapped down onto the floor");
            Assert.IsTrue(hero.FacingRight, "arriving at a left gate faces right");
            Assert.IsTrue(hero.ControlLocked);
            Assert.IsTrue(driver.GetTransitionState().InPosition);

            float startX = hero.transform.position.x;
            // Input during the lock is ignored.
            input.JumpButton.Press();
            int steps = StepUntilUnlocked(0.5f);

            Assert.AreEqual(config.TransitionWalkInSeconds, steps * dt, dt * 2.5f, "walk lasts about 0.33 s");
            Assert.AreEqual(config.RunSpeed * config.TransitionWalkInSeconds, hero.transform.position.x - startX, 0.35f);
            Assert.AreEqual(HeroHalfHeight, hero.transform.position.y, 0.05f, "stayed on the floor");
            Assert.IsFalse(driver.IsInvulnerable);
            Assert.AreEqual(0, landedCount, "arriving must not raise Landed");
        }

        [Test]
        public void EnterRightGate_FacesLeftAndWalksLeft()
        {
            BuildWorld();
            Settle();
            var gate = MakeGate("right1", new Vector2(10f, 1f), new Vector2(-1f, 0f));
            StartEntering(gate);
            Assert.AreEqual(9f, hero.transform.position.x, 0.001f);
            Assert.IsFalse(hero.FacingRight);
            Step(Steps(0.2f));
            Assert.Less(hero.Velocity.x, -config.RunSpeed + 0.1f);
            StepUntilUnlocked(0.5f);
            Assert.Less(hero.transform.position.x, 9f - 2f);
        }

        [Test]
        public void EnterSideways_InvulnerableUntilControlReturns_ThenHurtableAgain()
        {
            BuildWorld();
            Settle();
            var gate = MakeGate("left1", new Vector2(5f, 1f), Vector2.zero);
            StartEntering(gate);
            Step(Steps(0.1f));
            Assert.IsTrue(hero.ControlLocked);
            Assert.IsFalse(Hit(1), "cannot be hurt while entering");
            StepUntilUnlocked(0.5f);
            Assert.IsTrue(Hit(1), "hurtable once control is back");
        }

        [Test]
        public void EnterSideways_DelayWaitsBeforeWalking()
        {
            BuildWorld();
            Settle();
            var gate = MakeGate("left1", new Vector2(5f, 1f), Vector2.zero);
            StartEntering(gate, 0.5f);
            float x = hero.transform.position.x;
            Step(Steps(0.4f));
            Assert.AreEqual(x, hero.transform.position.x, 0.01f, "still waiting");
            Assert.IsTrue(hero.ControlLocked);
            int steps = StepUntilUnlocked(1f);
            Assert.Greater(steps * dt, 0.1f + config.TransitionWalkInSeconds - 0.05f);
            Assert.IsNull(entering, "the coroutine ends when control returns");
        }

        [Test]
        public void EnterSideways_ArrivingAfterAJump_RaisesNoLanded()
        {
            BuildWorld();
            Settle();
            input.JumpButton.Press();
            Step(Steps(0.3f));
            input.JumpButton.Release();
            Step(Steps(1.2f));
            Assert.IsTrue(hero.IsGrounded, "test setup: landed again");
            landedCount = 0;

            var gate = MakeGate("left1", new Vector2(5f, 3f), Vector2.zero);
            StartEntering(gate);
            StepUntilUnlocked(0.5f);
            Step(Steps(0.3f));
            Assert.AreEqual(0, landedCount);
        }

        [Test]
        public void AfterEntering_NormalInputWorksAgain()
        {
            BuildWorld();
            Settle();
            var gate = MakeGate("left1", new Vector2(5f, 1f), Vector2.zero);
            StartEntering(gate);
            StepUntilUnlocked(0.5f);
            Step(Steps(0.2f));
            input.JumpButton.Press();
            Step(2);
            Assert.Greater(hero.Velocity.y, 1f, "jump works after unlocking");
            Assert.AreSame(input, hero.Input);
        }

        // ------------------------------------------------------------------ entering vertically

        [TestCase(0.02f)]
        [TestCase(0.01f)]
        public void EnterTopGate_HoversThenDropsAndReturnsControl(float step)
        {
            dt = step;
            BuildWorld();
            Settle();
            var gate = MakeGate("top1", new Vector2(3f, 6f), Vector2.zero);
            StartEntering(gate, 0.2f);

            Assert.AreEqual(3f, hero.transform.position.x, 0.001f);
            Assert.AreEqual(6f, hero.transform.position.y, 0.001f);
            Step(Steps(0.1f));
            Assert.AreEqual(6f, hero.transform.position.y, 0.01f, "hovers during the delay");

            Step(Steps(0.2f));
            Assert.Less(hero.Velocity.y, config.TransitionDropSpeed + 0.5f, "dropping");
            Assert.IsTrue(hero.ControlLocked);
            Assert.IsFalse(Hit(1));

            StepUntilUnlocked(0.6f);
            Assert.IsFalse(driver.IsInvulnerable);
            Assert.Less(hero.transform.position.y, 6f);
        }

        [TestCase(0.02f)]
        [TestCase(0.01f)]
        public void EnterBottomGate_StartsAboveAndIsThrownTheWayItFaces(float step)
        {
            dt = step;
            BuildWorld();
            Settle();
            var gate = MakeGate("bot1", new Vector2(3f, 1f), Vector2.zero);
            hero.FacingRight = true;
            StartEntering(gate);

            Assert.AreEqual(1f + config.TransitionBottomLift, hero.transform.position.y, 0.001f);
            Step(Steps(config.TransitionBottomSeconds) - 1);
            Assert.AreEqual(config.TransitionBottomSpeedX, hero.Velocity.x, 0.01f);
            Assert.AreEqual(config.TransitionBottomSpeedY, hero.Velocity.y, 0.01f);
            Assert.IsTrue(hero.ControlLocked);

            StepUntilUnlocked(0.5f);
            Assert.IsFalse(driver.IsInvulnerable);
        }

        // ------------------------------------------------------------------ recovery

        [Test]
        public void RestoreControl_UnlocksAndClearsInvulnerability_AndEndsTheCoroutine()
        {
            BuildWorld();
            Settle();
            var gate = MakeGate("left1", new Vector2(5f, 1f), Vector2.zero);
            StartEntering(gate, 5f);
            Step(3);
            Assert.IsTrue(hero.ControlLocked);

            ((ITransitionHeroRecovery)driver).RestoreControl();
            Step(1);

            Assert.IsFalse(hero.ControlLocked);
            Assert.IsFalse(driver.IsInvulnerable);
            Assert.IsNull(entering);
            Assert.IsTrue(Hit(1));
        }

        [Test]
        public void EnterRoom_WithoutGate_DoesNotStayLocked()
        {
            BuildWorld();
            Settle();
            driver.LeaveRoom(GatePosition.Right);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("EnterRoom needs"));
            var e = driver.EnterRoom(null, 0f);
            Assert.IsFalse(e.MoveNext());
            Assert.IsFalse(hero.ControlLocked);
        }
    }
}
