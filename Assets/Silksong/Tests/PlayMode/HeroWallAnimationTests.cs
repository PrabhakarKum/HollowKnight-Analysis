using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // Which clip the hero shows on a wall: WallSlide while it slides down, WallCling while it hangs still, nothing
    // replayed each step, the normal clips again after letting go. Same world as HeroWallTests, with a fake animator.
    public class HeroWallAnimationTests
    {
        sealed class FakeAnimator : ISpriteAnimator
        {
            readonly HashSet<string> missing = new HashSet<string>();
            public string Current;
            public readonly List<string> Log = new List<string>();
            public event Action<string> Completed;
            public event Action<string, int> FrameEvent;

            public void Lack(string clip) => missing.Add(clip);
            public void Play(string clip)
            {
                if (missing.Contains(clip)) return;   // unknown clip: nothing changes
                if (Current == clip) return;
                Current = clip;
                Log.Add(clip);
            }
            public void PlayFromFrame(string clip, int frame) => Play(clip);
            public bool IsPlaying(string clip) => Current == clip;
            void Unused() { Completed?.Invoke(null); FrameEvent?.Invoke(null, 0); }
        }

        const float WallFaceX = 1f;
        readonly List<UnityEngine.Object> spawned = new List<UnityEngine.Object>();
        SimulationMode2D savedMode;
        const float dt = 0.02f;
        HeroConfig config;
        FakeHeroInput input;
        HeroController hero;
        FakeAnimator anim;
        string stubsPath;

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            config = ScriptableObject.CreateInstance<HeroConfig>();
            stubsPath = Path.Combine(Path.GetTempPath(), "wallanim-stubs-" + Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
            anim = new FakeAnimator();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            spawned.Clear();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
            Physics2D.simulationMode = savedMode;
            Stubs.StubsDocPath = null;
            if (File.Exists(stubsPath)) File.Delete(stubsPath);
            Stubs.ResetForTests();
        }

        void BuildWorld(float startHeight)
        {
            input = new FakeHeroInput();
            var ground = new GameObject("Ground");
            spawned.Add(ground);
            ground.AddComponent<BoxCollider2D>().size = new Vector2(400f, 1f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            var wall = new GameObject("Wall");
            spawned.Add(wall);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 400f);
            wall.transform.position = new Vector3(WallFaceX + 0.5f, 190f, 0f);
            var go = new GameObject("Hero");
            spawned.Add(go);
            go.transform.position = new Vector3(WallFaceX - 0.25f - 0.01f, 0.5f + 0.01f + startHeight, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 1f);
            hero = go.AddComponent<HeroController>();
            hero.ManualStepping = true;
            hero.Config = config;
            hero.Input = input;
            hero.Animator = anim;
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

        static readonly Vector2 Right = new Vector2(1f, 0f);

        [Test]
        public void SlidingDownTheWall_PlaysWallSlide()
        {
            BuildWorld(60f);
            input.Move = Right;
            Step(15);
            Assert.AreEqual(HeroState.WallClinging, hero.State);
            Assert.Less(hero.GetComponent<Rigidbody2D>().linearVelocity.y, -1f, "test setup: the hero slides");
            Assert.AreEqual("WallSlide", anim.Current);
        }

        [Test]
        public void HangingStillOnTheWall_PlaysWallCling()
        {
            config.WallSlideSpeed = 0f;   // the fall is capped at nothing: the hero hangs
            BuildWorld(60f);
            input.Move = Right;
            Step(15);
            Assert.AreEqual(HeroState.WallClinging, hero.State);
            Assert.AreEqual("WallCling", anim.Current);
            CollectionAssert.DoesNotContain(anim.Log, "WallSlide");
        }

        [Test]
        public void ASlide_IsPlayedOnceNotEveryStep()
        {
            BuildWorld(60f);
            input.Move = Right;
            Step(10);
            int slideStarts = anim.Log.FindAll(c => c == "WallSlide").Count;
            Step(30);
            Assert.AreEqual(1, slideStarts);
            Assert.AreEqual(1, anim.Log.FindAll(c => c == "WallSlide").Count, "no replay while sliding");
            Assert.LessOrEqual(anim.Log.FindAll(c => c == "Fall").Count, 1, "movement must not play Fall between the wall clips every step");
            Assert.AreEqual("WallSlide", anim.Current);
        }

        [Test]
        public void LettingGo_ReturnsToTheNormalClips()
        {
            BuildWorld(60f);
            input.Move = Right;
            Step(15);
            Assert.AreEqual("WallSlide", anim.Current);
            input.Move = new Vector2(-1f, 0f);   // hold away long enough to let go
            Step(10);
            Assert.AreNotEqual(HeroState.WallClinging, hero.State);
            Assert.AreNotEqual("WallSlide", anim.Current, "movement code plays its own clip again");
            Assert.AreNotEqual("WallCling", anim.Current);
        }

        [Test]
        public void AMissingWallSlideClip_BehavesAsAnUnknownClip_AndNothingThrows()
        {
            anim.Lack("WallSlide");
            BuildWorld(60f);
            input.Move = Right;
            Assert.DoesNotThrow(() => Step(15));
            Assert.AreEqual(HeroState.WallClinging, hero.State, "movement is unchanged");
            Assert.AreNotEqual("WallSlide", anim.Current);
        }
    }
}
