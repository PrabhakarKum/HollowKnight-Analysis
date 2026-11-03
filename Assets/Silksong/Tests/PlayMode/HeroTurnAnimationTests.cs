using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // The Turn clip: requested on a grounded facing reversal, protected from Idle/Run while it plays, and cut at once
    // by anything airborne, a dash or a hit. A fake animator whose non-looping clips end only when the test says so.
    public class HeroTurnAnimationTests
    {
        sealed class FakeAnimator : ISpriteAnimator
        {
            static readonly HashSet<string> Once = new HashSet<string> { "Turn", "Land", "Slash", "SlashUp", "SlashDown", "Hurt", "Dash", "Death" };
            readonly HashSet<string> missing = new HashSet<string>();
            public string Current;
            public bool Finished;
            public readonly List<string> Log = new List<string>();

            public event Action<string> Completed;
            public event Action<string, int> FrameEvent;

            public void Lack(string clip) => missing.Add(clip);

            public void Play(string clip)
            {
                if (missing.Contains(clip)) return; // like an unknown clip: nothing changes
                if (Current == clip && !Finished && !Once.Contains(clip)) return;
                Current = clip;
                Finished = false;
                Log.Add(clip);
            }
            public void PlayFromFrame(string clip, int frame) => Play(clip);
            public bool IsPlaying(string clip) => Current == clip && !Finished;
            public void Finish()
            {
                if (Current == null || Finished || !Once.Contains(Current)) return;
                Finished = true;
                Completed?.Invoke(Current);
            }
            void Unused() { FrameEvent?.Invoke(null, 0); }
        }

        readonly List<UnityEngine.Object> spawned = new List<UnityEngine.Object>();
        SimulationMode2D savedMode;
        const float dt = 0.02f;
        HeroConfig config;
        FakeHeroInput input;
        HeroController hero;
        Rigidbody2D heroBody;
        FakeAnimator anim;
        string stubsPath;

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            config = ScriptableObject.CreateInstance<HeroConfig>();
            stubsPath = Path.Combine(Path.GetTempPath(), "turn-tests-stubs-" + Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();

            input = new FakeHeroInput();
            anim = new FakeAnimator();
            var ground = new GameObject("Ground");
            spawned.Add(ground);
            ground.AddComponent<BoxCollider2D>().size = new Vector2(400f, 1f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            var go = new GameObject("Hero");
            spawned.Add(go);
            go.transform.position = new Vector3(0f, 0.51f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 1f);
            hero = go.AddComponent<HeroController>();
            heroBody = go.GetComponent<Rigidbody2D>();
            hero.ManualStepping = true;
            hero.Config = config;
            hero.Input = input;
            hero.Animator = anim;
            hero.Health.DeathRespawnDelaySeconds = -1f;
            Physics2D.SyncTransforms();
            Step(8); // settle on the floor, facing right
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                foreach (var o in spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
                spawned.Clear();
                if (config != null) UnityEngine.Object.DestroyImmediate(config);
            }
            finally
            {
                Physics2D.simulationMode = savedMode;
                Stubs.StubsDocPath = null;
                if (File.Exists(stubsPath)) File.Delete(stubsPath);
            }
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

        void Left() => input.Move = new Vector2(-1f, 0f);
        void Right() => input.Move = new Vector2(1f, 0f);

        [Test]
        public void SettlingOnTheFloor_PlaysNoTurn()
        {
            Assert.IsTrue(hero.IsGrounded);
            Assert.IsFalse(anim.Log.Contains("Turn"));
        }

        [Test]
        public void GroundedReversal_RequestsTurnOnce_AndFacingChangesAtOnce()
        {
            Left();
            Step(1);
            Assert.IsFalse(hero.FacingRight, "facing never waits for the clip");
            Assert.AreEqual("Turn", anim.Current);
            Step(10);
            Assert.AreEqual(1, anim.Log.FindAll(c => c == "Turn").Count);
        }

        [Test]
        public void SameDirectionAgain_PlaysNoSecondTurn()
        {
            Left();
            Step(1);
            anim.Finish();
            Step(10);
            Assert.AreEqual(1, anim.Log.FindAll(c => c == "Turn").Count);
            Assert.AreEqual("Run", anim.Current);
        }

        [Test]
        public void IdleRequestDuringTurn_IsIgnored_ThenIdleFollows()
        {
            Left();
            Step(1);
            input.Move = Vector2.zero;
            Step(10); // the per-step Idle request must not cut Turn
            Assert.IsTrue(anim.IsPlaying("Turn"));
            Assert.AreEqual("Turn", anim.Log[anim.Log.Count - 1]);
            anim.Finish();
            Step(1);
            Assert.AreEqual("Idle", anim.Current);
        }

        [Test]
        public void RunRequestDuringTurn_IsIgnored_ThenRunFollows()
        {
            Left();
            Step(11);
            Assert.AreEqual("Turn", anim.Current);
            Assert.AreEqual("Turn", anim.Log[anim.Log.Count - 1]);
            anim.Finish();
            Step(1);
            Assert.AreEqual("Run", anim.Current);
        }

        [Test]
        public void JumpDuringTurn_TakesOverAtOnce()
        {
            Left();
            Step(1);
            input.Move = Vector2.zero;
            input.JumpButton.Press();
            Step(1);
            Assert.AreEqual("Jump", anim.Current);
            input.JumpButton.Release();
            Step(60);
            Assert.AreEqual(1, anim.Log.FindAll(c => c == "Turn").Count, "a cut Turn does not come back");
        }

        [Test]
        public void DashDuringTurn_TakesOver()
        {
            Left();
            Step(1);
            input.Move = Vector2.zero;
            input.DashButton.Press();
            Step(1);
            Assert.AreEqual("Dash", anim.Current);
        }

        [Test]
        public void ReversalDuringAJump_PlaysNoTurn_EvenAfterLanding()
        {
            input.JumpButton.Press();
            Step(1);
            Left();
            Step(5);
            Assert.IsFalse(hero.IsGrounded);
            Assert.IsFalse(anim.Log.Contains("Turn"));
            input.JumpButton.Release();
            Step(80);
            Assert.IsTrue(hero.IsGrounded);
            Assert.IsFalse(anim.Log.Contains("Turn"), "the turn happened in the air");
        }

        [Test]
        public void AnimatorWithoutATurnClip_BehavesAsBefore()
        {
            anim.Lack("Turn");
            Left();
            Step(1);
            Assert.AreEqual("Run", anim.Current);
            Right();
            Step(1);
            Assert.AreEqual("Run", anim.Current);
            input.Move = Vector2.zero;
            Step(2);
            Assert.AreEqual("Idle", anim.Current);
            Assert.IsFalse(anim.Log.Contains("Turn"));
        }

        [Test]
        public void TurnWhileRunning_DoesNotChangeRunSpeed()
        {
            Right();
            Step(10);
            Assert.AreEqual(config.RunSpeed, heroBody.linearVelocity.x, 1e-4f);
            Left();
            Step(1);
            Assert.AreEqual("Turn", anim.Current);
            Assert.AreEqual(-config.RunSpeed, heroBody.linearVelocity.x, 1e-4f);
        }
    }
}
