using System.Collections;
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

        [SetUp]
        public void SetUp()
        {
            sprites = new Sprite[Frames];
            for (int i = 0; i < Frames; i++)
            {
                var tex = new Texture2D(4, 4);
                sprites[i] = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
                sprites[i].name = "frame" + i;
            }
            go = new GameObject("animator-under-test");
            sr = go.AddComponent<SpriteRenderer>();
            go.AddComponent<Animator>();
            anim = go.AddComponent<UnitySpriteAnimator>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(go);
        }

        // Built in code so the test has no dependency on imported Silksong art.
        AnimationClip MakeClip(string name, bool loop, string eventFunction = null, int eventFrame = 0)
        {
            var clip = new AnimationClip { name = name, frameRate = Fps };
            var keys = new ObjectReferenceKeyframe[Frames];
            for (int i = 0; i < Frames; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprites[i] };
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (eventFunction != null)
            {
                AnimationUtility.SetAnimationEvents(clip, new[]
                {
                    new AnimationEvent { time = eventFrame / Fps, functionName = eventFunction, intParameter = eventFrame }
                });
            }
            return clip;
        }

        [UnityTest]
        public IEnumerator Play_NonLoopingClip_RaisesCompletedOnce()
        {
            anim.Register(MakeClip("Once", loop: false));
            var completed = new System.Collections.Generic.List<string>();
            anim.Completed += c => completed.Add(c);

            anim.Play("Once");
            Assert.IsTrue(anim.IsPlaying("Once"));

            // Clip lasts 0.4 s; wait well past it to catch any repeat raise.
            float end = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < end) yield return null;

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
            anim.Register(MakeClip("Evt", loop: false, eventFunction: nameof(UnitySpriteAnimator.OnFrameEvent), eventFrame: 1));
            string gotClip = null;
            int gotFrame = -1;
            anim.FrameEvent += (c, f) => { gotClip = c; gotFrame = f; };

            anim.Play("Evt");
            float end = Time.realtimeSinceStartup + 1.5f;
            while (gotClip == null && Time.realtimeSinceStartup < end) yield return null;

            Assert.AreEqual("Evt", gotClip);
            Assert.AreEqual(1, gotFrame);
        }
    }
}
