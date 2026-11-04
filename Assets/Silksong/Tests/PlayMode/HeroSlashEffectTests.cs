using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hollowknight.Silksong.Tests
{
    // The nail swipe child: shown at swing start per direction and facing, hidden when its clip ends, hidden when the
    // swing is cut short, and absent for a hero without effect clips. Clips are built in code; the hero is stepped by
    // hand (as in HeroAttackTests) while the effect's own animator runs on real frames.
    public class HeroSlashEffectTests
    {
        const float Dt = 0.02f;
        const float Fps = 20f;
        const string EffectObject = "NailSwipeEffect";

        readonly List<Object> spawned = new List<Object>();
        SimulationMode2D savedMode;
        HeroConfig config;
        FakeHeroInput input;
        HeroController hero;
        string stubsPath;

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            config = ScriptableObject.CreateInstance<HeroConfig>();
            stubsPath = Path.Combine(Path.GetTempPath(), "slash-effect-stubs-" + System.Guid.NewGuid() + ".md");
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
            Stubs.ResetForTests();
        }

        AnimationClip MakeClip(string name, int frames)
        {
            var clip = new AnimationClip { name = name, frameRate = Fps };
            spawned.Add(clip);
            var keys = new ObjectReferenceKeyframe[frames];
            var tex = new Texture2D(4, 4);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            spawned.Add(tex);
            spawned.Add(sprite);
            for (int i = 0; i < frames; i++) keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprite };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        // A hero 3 units above nothing (so a down press is a real down swing) with body clips and, optionally, the
        // three effect clips: SlashEffect 0.2 s, SlashUpEffect 0.25 s, SlashDownEffect 0.15 s.
        void BuildHero(bool withEffectClips)
        {
            input = new FakeHeroInput();
            var go = new GameObject("Hero");
            spawned.Add(go);
            go.transform.position = new Vector3(0f, 30f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 1f);
            hero = go.AddComponent<HeroController>();
            hero.ManualStepping = true;
            hero.Config = config;
            hero.Input = input;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 5;
            visual.AddComponent<Animator>();
            var anim = visual.AddComponent<UnitySpriteAnimator>();
            anim.Register(MakeClip("Slash", 5));
            anim.Register(MakeClip("SlashUp", 7));
            anim.Register(MakeClip("SlashDown", 4));
            if (withEffectClips)
            {
                anim.Register(MakeClip("SlashEffect", 4));
                anim.Register(MakeClip("SlashUpEffect", 5));
                anim.Register(MakeClip("SlashDownEffect", 3));
            }
            hero.Animator = anim;
            Physics2D.SyncTransforms();
        }

        void Swing(Vector2 move)
        {
            input.Move = move;
            input.AttackButton.Release();
            input.AttackButton.Press();
            hero.StepFixed(Dt);
            Physics2D.Simulate(Dt);
            input.EndFixedStep(Dt);
        }

        // Runs a step holding a direction so movement turns the hero that way (FacingRight has no public setter).
        void Face(bool right)
        {
            input.Move = new Vector2(right ? 1f : -1f, 0f);
            hero.StepFixed(Dt);
            Physics2D.Simulate(Dt);
            input.EndFixedStep(Dt);
            Assert.AreEqual(right, hero.FacingRight);
        }

        Transform Effect() => hero.transform.Find(EffectObject);
        SpriteRenderer EffectRenderer() => Effect().GetComponent<SpriteRenderer>();

        [Test]
        public void SideSwing_ShowsTheSwipe_FlippedOnlyWhenSwingingRight()
        {
            BuildHero(true);
            Swing(Vector2.right);
            Assert.IsNotNull(Effect());
            Assert.IsTrue(EffectRenderer().enabled);
            Assert.IsTrue(EffectRenderer().flipX);
            Assert.AreEqual(new Vector3(0.9f, 1f, 1f), Effect().localScale);
            Assert.AreEqual(-0.001f, Effect().localPosition.z, 1e-6f);
            Assert.AreEqual(6, EffectRenderer().sortingOrder, "one above the body");
        }

        [UnityTest]
        public IEnumerator SideSwing_Left_IsNotFlipped()
        {
            BuildHero(true);
            Swing(Vector2.left);
            Assert.IsTrue(EffectRenderer().enabled);
            Assert.IsFalse(EffectRenderer().flipX);
            yield return null;
        }

        [Test]
        public void UpSwing_UsesTheUpSwipe_AndFlipsWithFacing()
        {
            BuildHero(true);
            Face(true);
            Swing(Vector2.up);
            Assert.IsTrue(EffectRenderer().enabled);
            Assert.AreEqual(new Vector3(1f, 0.9f, 1f), Effect().localScale);
            Assert.IsTrue(EffectRenderer().flipX, "hero faces right");
            Assert.AreEqual(0f, Effect().localPosition.x);
        }

        [Test]
        public void DownSwing_UsesTheDownSwipe_WithOffsetFollowingFacing()
        {
            BuildHero(true);
            Face(false);
            Swing(Vector2.down);
            Assert.AreEqual(AttackDirection.Down, hero.Attack.CurrentDirection);
            Assert.IsTrue(EffectRenderer().enabled);
            Assert.AreEqual(new Vector3(1.2f, 1.2f, 1f), Effect().localScale);
            Assert.AreEqual(0.69f, Effect().localPosition.x, 1e-5f);
            Assert.AreEqual(-0.37f, Effect().localPosition.y, 1e-5f);
            Assert.IsFalse(EffectRenderer().flipX);
        }

        [UnityTest]
        public IEnumerator Swipe_IsHiddenWhenItsClipCompletes()
        {
            BuildHero(true);
            Swing(Vector2.left);
            Assert.IsTrue(EffectRenderer().enabled);
            // SlashEffect is 4 frames at 20 fps = 0.2 s; the hero is not stepped any more, so only the clip ends it.
            float t = 0f;
            while (t < 0.1f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(EffectRenderer().enabled, "still showing halfway");
            t = 0f;
            while (t < 0.4f) { t += Time.deltaTime; yield return null; }
            Assert.IsFalse(EffectRenderer().enabled, "hidden after the clip length");
        }

        [Test]
        public void Swipe_IsHiddenWhenTheSwingIsCutShort()
        {
            BuildHero(true);
            Swing(Vector2.left);
            Assert.IsTrue(EffectRenderer().enabled);
            typeof(HeroController).GetProperty("State").GetSetMethod(true).Invoke(hero, new object[] { HeroState.Hurt });
            hero.StepFixed(Dt);
            Assert.IsFalse(hero.Attack.IsAttacking);
            Assert.IsFalse(EffectRenderer().enabled);
        }

        [Test]
        public void Swipe_DoesNotChangeTheHitbox()
        {
            BuildHero(true);
            Swing(Vector2.left);
            var withEffect = hero.Attack.Hitbox.size;
            Object.DestroyImmediate(hero.gameObject);
            BuildHero(false);
            Swing(Vector2.left);
            Assert.AreEqual(withEffect, hero.Attack.Hitbox.size);
        }

        [Test]
        public void NoEffectClips_NoEffectObject_AndAStubIsLogged()
        {
            BuildHero(false);
            Swing(Vector2.left);
            Assert.IsNull(Effect());
            Assert.IsTrue(hero.Attack.IsAttacking, "the swing itself still works");
            Assert.IsTrue(Stubs.Reported.Count > 0);
            bool found = false;
            foreach (var s in Stubs.Reported) if (s.Contains("nail swipe")) found = true;
            Assert.IsTrue(found);
        }

        [Test]
        public void HeroWithoutAnimator_GetsNoEffect()
        {
            BuildHero(true);
            hero.Animator = null;
            Swing(Vector2.left);
            Assert.IsNull(Effect());
        }
    }
}
