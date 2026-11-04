using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hollowknight.Silksong.Tests
{
    // Acceptance for the attack visuals: the whole chain from a button press to the body clip, the nail swipe child
    // and the hitbox, for every direction. The hero is a plain test hero in open air stepped by hand (no generated
    // rooms, no GameSession, no RoomWorldFactory). Body and effect clips are the imported ones when they exist
    // (Silksong > Import Hero Animations), otherwise clips built in code with the same names; either way the test
    // asserts clip names and states, never frame art. Owner-side judgement is in docs/study/Attack-checklist.md.
    public class AttackVisualsAcceptanceTests
    {
        const float Dt = 0.02f;
        const float Fps = 20f;
        const string EffectObject = "NailSwipeEffect";

        static readonly string[] AllClips =
        {
            "Idle", "Run", "Jump", "Fall", "Dash", "WallCling", "WallSlide", "Slash", "SlashAlt", "SlashUp", "SlashDown",
            "WallSlash", "SlashEffect", "SlashAltEffect", "SlashUpEffect", "SlashDownEffect"
        };

        readonly List<Object> spawned = new List<Object>();
        SimulationMode2D savedMode;
        HeroConfig config;
        FakeHeroInput input;
        HeroController hero;
        UnitySpriteAnimator body;
        string stubsPath;
        int importedCount;

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            config = ScriptableObject.CreateInstance<HeroConfig>();
            stubsPath = Path.Combine(Path.GetTempPath(), "attack-visuals-stubs-" + System.Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
            BuildHero();
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

        // The imported clip asset when present (never destroyed here), else a small clip built in code.
        AnimationClip GetClip(string name)
        {
            var imported = AssetDatabase.LoadAssetAtPath<AnimationClip>(HeroClipNames.ClipPath(name));
            if (imported != null) { importedCount++; return imported; }
            var clip = new AnimationClip { name = name, frameRate = Fps };
            spawned.Add(clip);
            var tex = new Texture2D(4, 4);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            spawned.Add(tex);
            spawned.Add(sprite);
            var keys = new ObjectReferenceKeyframe[5];
            for (int i = 0; i < keys.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprite };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        void BuildHero()
        {
            input = new FakeHeroInput();
            var go = new GameObject("Hero");
            spawned.Add(go);
            go.transform.position = new Vector3(0f, 60f, 0f);   // open air: a down press is a real down swing
            go.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 1f);
            hero = go.AddComponent<HeroController>();
            hero.ManualStepping = true;
            hero.Config = config;
            hero.Input = input;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.AddComponent<SpriteRenderer>().sortingOrder = 5;
            visual.AddComponent<Animator>();
            body = visual.AddComponent<UnitySpriteAnimator>();
            foreach (var n in AllClips) body.Register(GetClip(n));
            var loop = AssetDatabase.LoadAssetAtPath<AnimationClip>(HeroClipNames.LoopClipPath("SlashDown"));
            if (loop != null) body.Register(loop);
            hero.Animator = body;
            Physics2D.SyncTransforms();
        }

        void Step(int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                hero.StepFixed(Dt);
                Physics2D.Simulate(Dt);
                input.EndFixedStep(Dt);
            }
        }

        void Swing(Vector2 move)
        {
            input.Move = move;
            input.AttackButton.Release();
            input.AttackButton.Press();
            Step();
            input.AttackButton.Release();
        }

        // Steps with the stick held for the given seconds of step clock (a fixed, bounded number of steps).
        void Wait(Vector2 move, float seconds)
        {
            input.Move = move;
            Step(Mathf.CeilToInt(seconds / Dt));
        }

        HeroSlashEffect Effect() => hero.transform.Find(EffectObject).GetComponent<HeroSlashEffect>();
        BoxCollider2D Box() => (BoxCollider2D)hero.Attack.Hitbox;

        void AssertSwing(string bodyClip, string effectClip)
        {
            Assert.IsTrue(hero.Attack.IsAttacking, "swinging");
            Assert.IsTrue(body.IsPlaying(bodyClip), "body clip " + bodyClip);
            Assert.AreEqual(effectClip, Effect().CurrentClip);
            Assert.IsTrue(Effect().IsShown, "swipe visible");
        }

        // The swing clip must survive the first steps of the swing (movement would play Fall).
        void AssertClipHolds(string bodyClip)
        {
            for (int i = 0; i < 6; i++)
            {
                Step();
                Assert.IsTrue(hero.Attack.IsAttacking, "step " + i);
                Assert.IsTrue(body.IsPlaying(bodyClip), bodyClip + " overwritten at step " + i);
            }
        }

        [Test]
        public void AllSwingClipsAreRegistered_ImportedOnesWhenPresent()
        {
            TestContext.WriteLine("imported clips used: " + importedCount + " of " + AllClips.Length);
            foreach (var n in AllClips) Assert.IsTrue(body.TryGetClip(n, out _), n);
        }

        [Test]
        public void SideSwing_PlaysSlash_AndTheSwipeIsFlippedByFacing()
        {
            Swing(Vector2.left);
            AssertSwing("Slash", "SlashEffect");
            Assert.IsFalse(hero.FacingRight);
            Assert.IsFalse(Effect().Renderer.flipX, "facing left: not flipped");
            Assert.AreEqual(new Vector3(0.9f, 1f, 1f), Effect().transform.localScale);
            Assert.AreEqual(config.AttackSideSize, Box().size);
            Assert.AreEqual(new Vector2(-config.AttackSideOffset.x, config.AttackSideOffset.y), Box().offset);
            AssertClipHolds("Slash");

            Wait(Vector2.right, 0.5f);
            Swing(Vector2.right);
            Assert.IsTrue(hero.FacingRight);
            Assert.IsTrue(Effect().Renderer.flipX, "facing right: flipped");
            Assert.AreEqual(config.AttackSideSize, Box().size);
            Assert.AreEqual(config.AttackSideOffset, Box().offset);
        }

        [Test]
        public void SecondQuickSwing_IsSlashAlt_AndTheAltSwipe()
        {
            Swing(Vector2.left);
            AssertSwing("Slash", "SlashEffect");
            Wait(Vector2.left, 0.44f);
            Swing(Vector2.left);
            AssertSwing("SlashAlt", "SlashAltEffect");
            Assert.AreEqual(config.AttackSideSize, Box().size);
            AssertClipHolds("SlashAlt");
        }

        [Test]
        public void AfterTheResetTime_TheNextSwingIsSlashAgain()
        {
            Swing(Vector2.left);
            Wait(Vector2.left, 0.44f);
            Swing(Vector2.left);
            AssertSwing("SlashAlt", "SlashAltEffect");
            Wait(Vector2.left, 0.70f);
            Swing(Vector2.left);
            AssertSwing("Slash", "SlashEffect");
        }

        [Test]
        public void UpSwing_PlaysSlashUp_WithTheUpSwipeAndTheConfigBox()
        {
            Swing(Vector2.up);
            AssertSwing("SlashUp", "SlashUpEffect");
            Assert.AreEqual(AttackDirection.Up, hero.Attack.CurrentDirection);
            Assert.AreEqual(new Vector3(1f, 0.9f, 1f), Effect().transform.localScale);
            Assert.AreEqual(config.AttackUpSize, Box().size);
            Assert.AreEqual(config.AttackUpOffset, Box().offset);
            AssertClipHolds("SlashUp");
        }

        [Test]
        public void DownSwing_PlaysSlashDown_WithTheDownSwipeOnTheFacingSide_AndTheConfigBox()
        {
            Wait(Vector2.left, 0.04f);   // face left
            Swing(Vector2.down);
            AssertSwing("SlashDown", "SlashDownEffect");
            Assert.AreEqual(AttackDirection.Down, hero.Attack.CurrentDirection);
            Assert.AreEqual(0.69f, Effect().transform.localPosition.x, 1e-5f, "facing left: native side");
            Assert.AreEqual(config.AttackDownSize, Box().size);
            Assert.AreEqual(config.AttackDownOffset, Box().offset);
            AssertClipHolds("SlashDown");
        }

        // The swipe is drawn for the thrust window only; its own Update hides it, so this needs real frames.
        // Bounded by elapsed time (0.5 s) with a large frame cap, always yielding (batch-mode frames can be very short).
        [UnityTest]
        public IEnumerator DownSwipe_IsHiddenAfterAboutATenthOfASecond_WhileTheSwingStaysOn()
        {
            Swing(Vector2.down);
            Assert.IsTrue(Effect().IsShown);
            float waited = 0f;
            for (int frame = 0; frame < 20000 && waited < 0.5f && Effect().IsShown; frame++)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.IsFalse(Effect().IsShown, "hidden after the thrust window");
            Assert.LessOrEqual(waited, 0.5f);
            Assert.IsTrue(hero.Attack.IsAttacking, "the swing (hitbox time) is unaffected");
            Assert.IsTrue(body.IsPlaying("SlashDown"));
        }

        [Test]
        public void SwipeIsGone_WhenTheSwingEnds()
        {
            Swing(Vector2.left);
            Wait(Vector2.left, 0.5f);
            Assert.IsFalse(hero.Attack.IsAttacking);
            Assert.IsFalse(Effect().IsShown);
            Assert.IsFalse(hero.Attack.HitboxActive);
        }
    }
}
