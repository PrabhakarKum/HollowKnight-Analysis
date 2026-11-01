using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hollowknight.Silksong.Tests
{
    public class SpriteAnimatorTests
    {
        const float Fps = 10f;
        const int Frames = 4;

        GameObject go;
        SpriteRenderer sr;
        UnitySpriteAnimator anim;
        Sprite[] sprites;
        // Everything the tests create, so nothing leaks between tests.
        readonly List<Object> created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            created.Clear();
            sprites = new Sprite[Frames];
            for (int i = 0; i < Frames; i++)
            {
                var tex = new Texture2D(4, 4);
                sprites[i] = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
                sprites[i].name = "frame" + i;
                created.Add(tex);
                created.Add(sprites[i]);
            }
            go = new GameObject("animator-under-test");
            sr = go.AddComponent<SpriteRenderer>();
            go.AddComponent<Animator>();
            anim = go.AddComponent<UnitySpriteAnimator>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
        }

        // Built in code so the test has no dependency on imported Silksong art.
        AnimationClip MakeClip(string name, bool loop, params int[] eventFrames)
        {
            var clip = new AnimationClip { name = name, frameRate = Fps };
            created.Add(clip);
            var keys = new ObjectReferenceKeyframe[Frames];
            for (int i = 0; i < Frames; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprites[i] };
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (eventFrames.Length > 0)
            {
                var events = new AnimationEvent[eventFrames.Length];
                for (int i = 0; i < events.Length; i++)
                    events[i] = new AnimationEvent
                    {
                        time = eventFrames[i] / Fps,
                        functionName = nameof(UnitySpriteAnimator.OnFrameEvent),
                        intParameter = eventFrames[i]
                    };
                AnimationUtility.SetAnimationEvents(clip, events);
            }
            return clip;
        }

        static IEnumerator WaitSeconds(float s)
        {
            float end = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        [UnityTest]
        public IEnumerator Play_NonLoopingClip_RaisesCompletedOnce()
        {
            anim.Register(MakeClip("Once", loop: false));
            var completed = new List<string>();
            anim.Completed += c => completed.Add(c);

            anim.Play("Once");
            Assert.IsTrue(anim.IsPlaying("Once"));

            // Clip lasts 0.4 s; wait well past it to catch any repeat raise.
            yield return WaitSeconds(1.5f);

            CollectionAssert.AreEqual(new[] { "Once" }, completed);
            Assert.IsFalse(anim.IsPlaying("Once"));
        }

        [UnityTest]
        public IEnumerator PlayFromFrame_StartsAtGivenFrame()
        {
            anim.Register(MakeClip("Loop", loop: true));

            anim.PlayFromFrame("Loop", 2);
            yield return null;

            // Frame 2 lasts 0.1 s and one test frame is far shorter, so the sprite must still be it.
            Assert.AreSame(sprites[2], sr.sprite);
        }

        [UnityTest]
        public IEnumerator FrameEvent_RaisedWithClipNameAndFrame()
        {
            anim.Register(MakeClip("Evt", loop: false, 1));
            string gotClip = null;
            int gotFrame = -1;
            anim.FrameEvent += (c, f) => { gotClip = c; gotFrame = f; };

            anim.Play("Evt");
            float end = Time.realtimeSinceStartup + 1.5f;
            while (gotClip == null && Time.realtimeSinceStartup < end) yield return null;

            Assert.AreEqual("Evt", gotClip);
            Assert.AreEqual(1, gotFrame);
        }

        [UnityTest]
        public IEnumerator Play_LoopingClip_NeverRaisesCompleted()
        {
            anim.Register(MakeClip("Loop", loop: true));
            int completed = 0;
            anim.Completed += _ => completed++;

            anim.Play("Loop");
            yield return WaitSeconds(1.2f);

            Assert.AreEqual(0, completed);
            Assert.IsTrue(anim.IsPlaying("Loop"));
        }

        [UnityTest]
        public IEnumerator Play_AThenB_NeverRaisesCompletedForA()
        {
            anim.Register(MakeClip("A", loop: false));
            anim.Register(MakeClip("B", loop: true));
            var completed = new List<string>();
            anim.Completed += completed.Add;

            anim.Play("A");
            yield return null;
            anim.Play("B");
            yield return WaitSeconds(1.0f);

            CollectionAssert.IsEmpty(completed);
            Assert.IsFalse(anim.IsPlaying("A"));
            Assert.IsTrue(anim.IsPlaying("B"));
        }

        [UnityTest]
        public IEnumerator Play_SameLoopingClipAgain_KeepsCurrentFrame()
        {
            anim.Register(MakeClip("Loop", loop: true));
            anim.Play("Loop");
            yield return WaitSeconds(0.25f);
            var before = sr.sprite;
            Assert.AreNotSame(sprites[0], before, "clip should have advanced past frame 0");

            anim.Play("Loop");

            Assert.AreSame(before, sr.sprite);
        }

        [UnityTest]
        public IEnumerator Play_RepeatedOnIntroWithLoopPart_DoesNotRestart()
        {
            anim.Register(MakeClip("Jump", loop: false));
            anim.Register(MakeClip("Jump Loop", loop: true));
            anim.Play("Jump");
            yield return WaitSeconds(0.25f);
            var before = sr.sprite;
            Assert.AreNotSame(sprites[0], before, "test setup: playback has moved on from frame 0");
            // Hero code calls Play every step; that must not send the intro back to frame 0.
            for (int i = 0; i < 5; i++) anim.Play("Jump");
            Assert.AreSame(before, sr.sprite);
            Assert.IsTrue(anim.IsPlaying("Jump"));
        }

        [UnityTest]
        public IEnumerator Intro_ChainsIntoLoopPart_WithoutRaisingCompleted_AndRepeatPlayStaysPut()
        {
            anim.Register(MakeClip("Jump", loop: false));
            anim.Register(MakeClip("Jump Loop", loop: true));
            var completed = new List<string>();
            anim.Completed += c => completed.Add(c);
            anim.Play("Jump");
            yield return WaitSeconds(0.9f);   // intro is 0.4 s; the loop part keeps going
            Assert.IsEmpty(completed, "Completed is not raised for an intro that chains");
            Assert.IsTrue(anim.IsPlaying("Jump"), "still counts as Jump while the loop part runs");
            Assert.IsTrue(anim.IsPlaying("Jump Loop"));
            anim.Play("Jump");
            Assert.IsTrue(anim.IsPlaying("Jump Loop"), "repeat Play does not drop back to the intro");
            yield return WaitSeconds(0.9f);
            Assert.IsEmpty(completed);
        }

        [UnityTest]
        public IEnumerator Play_OtherClip_StillSwitchesAwayFromLoopPart()
        {
            anim.Register(MakeClip("Jump", loop: false));
            anim.Register(MakeClip("Jump Loop", loop: true));
            anim.Register(MakeClip("Run", loop: true));
            anim.Play("Jump");
            yield return WaitSeconds(0.6f);
            anim.Play("Run");
            Assert.IsTrue(anim.IsPlaying("Run"));
            Assert.IsFalse(anim.IsPlaying("Jump"));
            anim.Play("Jump");
            Assert.IsTrue(anim.IsPlaying("Jump"), "a later Play(Jump) starts the intro again");
        }

        [UnityTest]
        public IEnumerator Play_FinishedNonLoopingClip_Restarts()
        {
            anim.Register(MakeClip("Once", loop: false));
            int completed = 0;
            anim.Completed += _ => completed++;

            anim.Play("Once");
            yield return WaitSeconds(0.8f);
            Assert.AreEqual(1, completed);

            anim.Play("Once");
            Assert.IsTrue(anim.IsPlaying("Once"));
            yield return WaitSeconds(0.8f);
            Assert.AreEqual(2, completed);
        }

        [Test]
        public void Play_ManyTimes_DoesNotGrowGraphCount()
        {
            anim.Register(MakeClip("A", loop: false));
            anim.Register(MakeClip("B", loop: true));
            anim.Play("A");
            int baseline = UnitySpriteAnimator.LiveGraphCount;

            for (int i = 0; i < 25; i++)
            {
                anim.Play(i % 2 == 0 ? "A" : "B");
                anim.PlayFromFrame("B", i % Frames);
            }

            Assert.AreEqual(baseline, UnitySpriteAnimator.LiveGraphCount);
        }

        [UnityTest]
        public IEnumerator EventOnFrameZero_Fires()
        {
            anim.Register(MakeClip("Z", loop: false, 0));
            var frames = new List<int>();
            anim.FrameEvent += (c, f) => frames.Add(f);

            anim.Play("Z");
            yield return WaitSeconds(0.15f);

            CollectionAssert.AreEqual(new[] { 0 }, frames);
        }

        [UnityTest]
        public IEnumerator EventOnStartFrame_Fires()
        {
            anim.Register(MakeClip("S", loop: false, 1, 2));
            var frames = new List<int>();
            anim.FrameEvent += (c, f) => frames.Add(f);

            anim.PlayFromFrame("S", 2);
            yield return WaitSeconds(0.15f);

            // Frame 1 is before the start so it is skipped; frame 2 fires exactly once.
            CollectionAssert.AreEqual(new[] { 2 }, frames);
        }

        [UnityTest]
        public IEnumerator EventOnStartFrame_FiresOnceAcrossFirstTwoUpdates()
        {
            anim.Register(MakeClip("S2", loop: false, 2));
            var frames = new List<int>();
            anim.FrameEvent += (c, f) => frames.Add(f);

            anim.PlayFromFrame("S2", 2);
            yield return null;
            yield return null;
            CollectionAssert.AreEqual(new[] { 2 }, frames);

            yield return WaitSeconds(0.3f);
            CollectionAssert.AreEqual(new[] { 2 }, frames);
        }

        [Test]
        public void Play_BeforeAwake_StillWorks()
        {
            var fresh = new GameObject("fresh");
            created.Add(fresh);
            fresh.SetActive(false);
            fresh.AddComponent<SpriteRenderer>();
            fresh.AddComponent<Animator>();
            var a = fresh.AddComponent<UnitySpriteAnimator>();

            a.Register(MakeClip("L", loop: true));
            a.Play("L");

            Assert.IsTrue(a.IsPlaying("L"));
        }

        [Test]
        public void Disable_ResetsPlaybackState()
        {
            anim.Register(MakeClip("L", loop: true));
            anim.Play("L");

            go.SetActive(false);
            go.SetActive(true);

            Assert.IsFalse(anim.IsPlaying("L"));
        }
    }
}
