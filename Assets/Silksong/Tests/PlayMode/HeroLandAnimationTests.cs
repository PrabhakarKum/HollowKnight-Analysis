using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // The landing animation: Land plays once when the hero touches down standing still, Idle follows only after
    // it finished, and anything the player does cuts it at once. Uses a fake animator whose clips end only when
    // the test says so, and the same hand-stepped physics as HeroMovementTests.
    public class HeroLandAnimationTests
    {
        const float HeroHalfHeight = 0.5f;

        // Non-looping clips are "playing" until Finish(); Idle, Run, Fall and Jump loop. Play of a clip that is
        // already the current looping one is a no-op, like UnitySpriteAnimator.
        sealed class FakeAnimator : ISpriteAnimator
        {
            static readonly HashSet<string> Once = new HashSet<string> { "Land", "Slash", "SlashUp", "SlashDown", "Hurt", "Dash", "Death" };
            public string Current;
            public bool Finished;
            public readonly List<string> Log = new List<string>();

            public event Action<string> Completed;
            public event Action<string, int> FrameEvent;

            public void Play(string clip)
            {
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
        float dt = 0.02f;
        HeroConfig config;
        FakeHeroInput input;
        HeroController hero;
        FakeAnimator anim;
        string stubsPath;
        int landed;

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            config = ScriptableObject.CreateInstance<HeroConfig>();
            stubsPath = Path.Combine(Path.GetTempPath(), "land-tests-stubs-" + Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
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

        void BuildWorld(float startHeight)
        {
            input = new FakeHeroInput();
            anim = new FakeAnimator();
            landed = 0;
            var ground = new GameObject("Ground");
            spawned.Add(ground);
            ground.AddComponent<BoxCollider2D>().size = new Vector2(400f, 1f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);

            var go = new GameObject("Hero");
            spawned.Add(go);
            go.transform.position = new Vector3(0f, HeroHalfHeight + 0.01f + startHeight, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 1f);
            hero = go.AddComponent<HeroController>();
            hero.ManualStepping = true;
            hero.Config = config;
            hero.Input = input;
            hero.Animator = anim;
            hero.Health.DeathRespawnDelaySeconds = -1f;
            hero.Landed += () => landed++;
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

        // Falls from a height and stops on the step Landed was raised.
        void FallUntilLanded()
        {
            for (int i = 0; i < 200 && landed == 0; i++) Step(1);
            Assert.AreEqual(1, landed, "test setup: the hero should have landed");
        }

        [Test]
        public void Landing_Standing_PlaysLandAndHoldsItWhileItRuns()
        {
            BuildWorld(1.5f);
            FallUntilLanded();
            Assert.AreEqual("Land", anim.Current);
            Step(10); // the per-step Idle request must not cut it
            Assert.IsTrue(anim.IsPlaying("Land"));
            Assert.AreEqual(1, anim.Log.FindAll(c => c == "Land").Count, "Land started exactly once");
            Assert.AreEqual("Land", anim.Log[anim.Log.Count - 1], "nothing was played after Land started");
        }

        [Test]
        public void Landing_IdleFollowsOnlyAfterLandCompleted()
        {
            BuildWorld(1.5f);
            FallUntilLanded();
            Step(5);
            Assert.AreEqual("Land", anim.Current);
            anim.Finish();
            Step(1);
            Assert.AreEqual("Idle", anim.Current);
            Step(5);
            Assert.AreEqual("Idle", anim.Current);
            Assert.AreEqual(1, anim.Log.FindAll(c => c == "Land").Count, "Land is not replayed after it finished");
        }

        [Test]
        public void Landing_ThenRunning_CutsLandAtOnce()
        {
            BuildWorld(1.5f);
            FallUntilLanded();
            Step(3);
            Assert.IsTrue(anim.IsPlaying("Land"));
            input.Move = new Vector2(1f, 0f);
            Step(1);
            Assert.AreEqual("Run", anim.Current, "Run is never blocked by Land");
            input.Move = Vector2.zero;
            Step(3);
            Assert.AreEqual("Idle", anim.Current, "a cut Land does not come back");
        }

        [Test]
        public void Landing_WhileHoldingMove_GoesStraightToRun()
        {
            BuildWorld(1.5f);
            input.Move = new Vector2(1f, 0f);
            FallUntilLanded();
            Assert.AreEqual("Run", anim.Current);
            Assert.IsFalse(anim.Log.Contains("Land"));
        }

        [Test]
        public void Landing_ThenJump_CutsLand()
        {
            BuildWorld(1.5f);
            FallUntilLanded();
            Step(3);
            input.JumpButton.Press();
            Step(1);
            Assert.AreEqual("Jump", anim.Current);
            input.JumpButton.Release();
        }

        [Test]
        public void Landing_ThenDash_CutsLand()
        {
            BuildWorld(1.5f);
            FallUntilLanded();
            Step(3);
            input.DashButton.Press();
            Step(1);
            Assert.AreEqual("Dash", anim.Current);
            // Let the dash end and the clip finish: Land must not come back.
            anim.Finish();
            Step(40);
            Assert.AreEqual("Idle", anim.Current);
            Assert.AreEqual(1, anim.Log.FindAll(c => c == "Land").Count);
        }

        [Test]
        public void Landing_ThenAttack_CutsLand()
        {
            BuildWorld(1.5f);
            FallUntilLanded();
            Step(3);
            input.AttackButton.Press();
            Step(1);
            Assert.AreEqual("Slash", anim.Current, "the swing clip is not replaced by Land or Idle on its first step");
            Step(2);
            Assert.AreEqual("Slash", anim.Current);
            anim.Finish();
            Step(config.AttackActiveSeconds > 0 ? Mathf.CeilToInt(config.AttackActiveSeconds / dt) + 2 : 20);
            Assert.AreEqual("Idle", anim.Current);
            Assert.AreEqual(1, anim.Log.FindAll(c => c == "Land").Count);
        }

        [Test]
        public void Landing_ThenHurt_CutsLand_AndItDoesNotComeBack()
        {
            BuildWorld(1.5f);
            FallUntilLanded();
            Step(3);
            var src = new GameObject("HitSource");
            spawned.Add(src);
            src.transform.position = new Vector3(-2f, 0.5f, 0f);
            Assert.IsTrue(hero.Health.TakeHit(new HitInstance { Damage = 1, Direction = AttackDirection.Normal, Source = src }));
            Assert.AreEqual("Hurt", anim.Current);
            anim.Finish();
            Step(Mathf.CeilToInt(2f / dt));
            Assert.AreNotEqual("Land", anim.Current);
            Assert.AreEqual(1, anim.Log.FindAll(c => c == "Land").Count);
        }

        [Test]
        public void Spawn_OnTheFloor_PlaysIdle_NotLand()
        {
            BuildWorld(0f);
            Step(30);
            Assert.AreEqual(0, landed, "everAirborne rule: spawning on the floor raises no Landed");
            Assert.AreEqual("Idle", anim.Current);
            Assert.IsFalse(anim.Log.Contains("Land"));
        }

        [Test]
        public void Respawn_AtStart_PlaysIdle_NotLand()
        {
            BuildWorld(1.5f);
            FallUntilLanded();
            anim.Finish();
            Step(5);
            var markerGo = new GameObject("Marker");
            spawned.Add(markerGo);
            markerGo.transform.position = new Vector3(10f, HeroHalfHeight + 0.01f, 0f);
            var marker = markerGo.AddComponent<RespawnMarker>();
            marker.Configure("Start", true);
            int landsBefore = anim.Log.FindAll(c => c == "Land").Count;
            hero.Health.RespawnAt(marker);
            Step(30);
            Assert.AreEqual(landsBefore, anim.Log.FindAll(c => c == "Land").Count, "respawning on the floor does not play Land");
            Assert.AreEqual("Idle", anim.Current);
        }
    }
}
