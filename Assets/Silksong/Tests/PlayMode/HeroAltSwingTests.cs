using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // Alternate sideways swing and wall slash: which body clip and which swipe each swing shows, the reset rules,
    // and that neither movement nor wall code replaces the swing clip. The hero is stepped by hand; the body
    // animator is real but never advanced (no frames pass in a plain [Test]), so a clip stays "playing".
    public class HeroAltSwingTests
    {
        const float Dt = 0.02f;
        const float Fps = 20f;
        const float WallFaceX = 1f;
        const string EffectObject = "NailSwipeEffect";

        readonly List<Object> spawned = new List<Object>();
        SimulationMode2D savedMode;
        HeroConfig config;
        FakeHeroInput input;
        HeroController hero;
        UnitySpriteAnimator body;
        string stubsPath;

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            config = ScriptableObject.CreateInstance<HeroConfig>();
            stubsPath = Path.Combine(Path.GetTempPath(), "alt-swing-stubs-" + System.Guid.NewGuid() + ".md");
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

        UnitySpriteAnimator MakeBody(string[] clipNames)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(hero.transform, false);
            visual.AddComponent<SpriteRenderer>().sortingOrder = 5;
            visual.AddComponent<Animator>();
            var anim = visual.AddComponent<UnitySpriteAnimator>();
            foreach (var n in clipNames) anim.Register(MakeClip(n, 5));
            return anim;
        }

        static readonly string[] AllClips =
        {
            "Slash", "SlashAlt", "SlashUp", "SlashDown", "WallSlash", "WallSlide", "WallCling", "Fall", "Idle", "Run", "Jump",
            "SlashEffect", "SlashAltEffect", "SlashUpEffect", "SlashDownEffect"
        };

        // Open air hero (high up, no ground), optionally beside a tall wall on its right.
        void BuildHero(bool withWall, float y = 60f)
        {
            input = new FakeHeroInput();
            if (withWall)
            {
                var wall = new GameObject("Wall");
                spawned.Add(wall);
                wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 400f);
                wall.transform.position = new Vector3(WallFaceX + 0.5f, 190f, 0f);
            }
            var go = new GameObject("Hero");
            spawned.Add(go);
            go.transform.position = withWall ? new Vector3(WallFaceX - 0.25f - 0.01f, y, 0f) : new Vector3(0f, y, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 1f);
            hero = go.AddComponent<HeroController>();
            hero.ManualStepping = true;
            hero.Config = config;
            hero.Input = input;
            body = MakeBody(AllClips);
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

        static int Steps(float seconds) => Mathf.CeilToInt(seconds / Dt);

        // Presses attack with the given stick and steps once.
        void Swing(Vector2 move)
        {
            input.Move = move;
            input.AttackButton.Release();
            input.AttackButton.Press();
            Step();
            input.AttackButton.Release();
        }

        // Waits with the stick held; long enough for the swing and the cooldown to end by default.
        void Wait(Vector2 move, float seconds)
        {
            input.Move = move;
            Step(Steps(seconds));
        }

        HeroSlashEffect Effect() => hero.transform.Find(EffectObject).GetComponent<HeroSlashEffect>();

        void AssertSwing(string bodyClip, string effectClip)
        {
            Assert.IsTrue(hero.Attack.IsAttacking);
            Assert.IsTrue(body.IsPlaying(bodyClip), "body clip " + bodyClip);
            Assert.AreEqual(effectClip, Effect().CurrentClip);
            Assert.IsTrue(Effect().IsShown);
        }

        [Test]
        public void TwoSideSwings_AlternateTheBodyClipAndTheSwipe()
        {
            BuildHero(false);
            Swing(Vector2.left);
            AssertSwing("Slash", "SlashEffect");
            Wait(Vector2.left, 0.44f);
            Swing(Vector2.left);
            AssertSwing("SlashAlt", "SlashAltEffect");
            Wait(Vector2.left, 0.44f);
            Swing(Vector2.left);
            AssertSwing("Slash", "SlashEffect");
        }

        [Test]
        public void ResetsToTheFirstSwing_WhenMoreThanTheLimitPassedSinceTheLastSwingStarted()
        {
            BuildHero(false);
            Swing(Vector2.left);
            Wait(Vector2.left, 0.70f);
            Swing(Vector2.left);
            AssertSwing("Slash", "SlashEffect");
        }

        [Test]
        public void StillAlternates_WhenTheGapIsInsideTheWindow()
        {
            BuildHero(false);
            Swing(Vector2.left);
            Wait(Vector2.left, 0.50f);
            Swing(Vector2.left);
            AssertSwing("SlashAlt", "SlashAltEffect");
        }

        [Test]
        public void ADirectionChange_ResetsTheToggle()
        {
            BuildHero(false);
            Swing(Vector2.left);                 // first, arms the alternate
            Wait(Vector2.left, 0.44f);
            Swing(Vector2.up);                   // up swing: direction changed
            AssertSwing("SlashUp", "SlashUpEffect");
            Wait(Vector2.left, 0.44f);
            Swing(Vector2.left);                 // back to sideways: the first swing, not the alternate
            AssertSwing("Slash", "SlashEffect");
        }

        [Test]
        public void UpSwing_NeverUsesAnAlternate()
        {
            BuildHero(false);
            Swing(Vector2.up);
            AssertSwing("SlashUp", "SlashUpEffect");
            Wait(Vector2.zero, 0.44f);
            Swing(Vector2.up);
            AssertSwing("SlashUp", "SlashUpEffect");
        }

        [Test]
        public void HeroWithoutAlternateClips_KeepsThePlainSwing()
        {
            BuildHero(false);
            Object.DestroyImmediate(body.gameObject);
            body = MakeBody(new[] { "Slash", "SlashUp", "SlashDown", "SlashEffect", "SlashUpEffect", "SlashDownEffect" });
            hero.Animator = body;
            Swing(Vector2.left);
            Wait(Vector2.left, 0.44f);
            Swing(Vector2.left);
            AssertSwing("Slash", "SlashEffect");
        }

        [Test]
        public void TheHitboxSizeIsTheSameForBothSwings()
        {
            BuildHero(false);
            Swing(Vector2.left);
            var first = ((BoxCollider2D)hero.Attack.Hitbox).size;
            Wait(Vector2.left, 0.44f);
            Swing(Vector2.left);
            Assert.IsTrue(body.IsPlaying("SlashAlt"));
            Assert.AreEqual(first, ((BoxCollider2D)hero.Attack.Hitbox).size);
            Assert.AreEqual(config.AttackSideSize, ((BoxCollider2D)hero.Attack.Hitbox).size);
        }

        [Test]
        public void MovementDoesNotOverwriteTheAlternateClipDuringTheSwing()
        {
            BuildHero(false);
            Swing(Vector2.left);
            Wait(Vector2.left, 0.44f);
            Swing(Vector2.left);
            for (int i = 0; i < 6; i++)
            {
                Step();
                Assert.IsTrue(body.IsPlaying("SlashAlt"), "step " + i);
            }
        }

        // ------------------------------------------------------------------ wall

        void ClingToWall()
        {
            input.Move = new Vector2(1f, 0f);
            Step(15);
            Assert.AreEqual(HeroState.WallClinging, hero.State, "test setup: clinging");
        }

        [Test]
        public void WallSwing_PlaysWallSlash_AndTheWallSwipeOnTheOpenSide()
        {
            BuildHero(true);
            ClingToWall();
            Swing(new Vector2(1f, 0f));   // pressing toward the wall
            Assert.AreEqual(AttackDirection.Normal, hero.Attack.CurrentDirection);
            AssertSwing("WallSlash", "SlashEffect");
            var fx = Effect();
            Assert.AreEqual(-0.61f, fx.transform.localPosition.x, 1e-5f, "the swing goes left, away from the wall");
            Assert.IsFalse(fx.Renderer.flipX);
            Assert.AreEqual(new Vector3(0.9f, 1f, 1f), fx.transform.localScale);
        }

        [Test]
        public void WallCodeAndMovement_DoNotOverwriteWallSlashDuringTheSwing()
        {
            BuildHero(true);
            ClingToWall();
            Swing(new Vector2(1f, 0f));
            for (int i = 0; i < 6; i++)
            {
                Step();
                Assert.IsTrue(hero.Attack.IsAttacking);
                Assert.IsTrue(body.IsPlaying("WallSlash"), "step " + i + " (HeroWall would play WallSlide)");
            }
        }

        [Test]
        public void WallSwing_HitboxSizeIsUnchanged()
        {
            BuildHero(true);
            ClingToWall();
            Swing(new Vector2(1f, 0f));
            Assert.AreEqual(config.AttackSideSize, ((BoxCollider2D)hero.Attack.Hitbox).size);
        }

        [Test]
        public void WallSwing_DoesNotChangeTheToggle()
        {
            BuildHero(true);
            // Armed by a first swing in the air with the stick neutral (the hero stays beside the wall, not clinging).
            Swing(Vector2.zero);
            AssertSwing("Slash", "SlashEffect");
            Wait(Vector2.zero, 0.44f);
            input.Move = new Vector2(1f, 0f);
            Step(10);
            Assert.AreEqual(HeroState.WallClinging, hero.State, "test setup: clinging");
            Swing(new Vector2(1f, 0f));
            AssertSwing("WallSlash", "SlashEffect");
            // Wait out the cooldown, leave the wall by moving the hero away, then swing sideways inside the window.
            Wait(new Vector2(1f, 0f), 0.42f);
            hero.transform.position = new Vector3(-10f, hero.transform.position.y, 0f);
            Physics2D.SyncTransforms();
            input.Move = Vector2.left;
            Step(2);
            Assert.IsFalse(hero.Wall.IsClinging, "test setup: off the wall");
            Swing(Vector2.left);
            AssertSwing("SlashAlt", "SlashAltEffect");
        }
    }
}
