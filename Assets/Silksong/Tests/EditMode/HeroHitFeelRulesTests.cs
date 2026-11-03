using NUnit.Framework;

namespace Hollowknight.Silksong.Tests
{
    public class HeroHitFeelRulesTests
    {
        [TestCase(0, 1, 9, 1)]
        [TestCase(8, 1, 9, 9)]
        [TestCase(9, 1, 9, 9)]
        [TestCase(12, 1, 9, 9)]   // already above the cap: pulled back, never grows
        [TestCase(3, 0, 9, 3)]
        [TestCase(3, -2, 9, 3)]   // a negative amount never takes silk away
        [TestCase(5, 1, -1, 0)]   // nonsense max reads as zero
        public void AddSilk_AddsAndClampsAtMax(int silk, int amount, int max, int expected)
        {
            Assert.AreEqual(expected, HeroHitFeelRules.AddSilk(silk, amount, max));
        }

        [Test]
        public void GivesSilk_NeedsDamageAndATargetThatGivesIt()
        {
            Assert.IsTrue(HeroHitFeelRules.GivesSilk(1, true));
            Assert.IsFalse(HeroHitFeelRules.GivesSilk(0, true));
            Assert.IsFalse(HeroHitFeelRules.GivesSilk(1, false));
        }

        [Test]
        public void CanRecoilSideways_OnlyWhenTheHeroIsInControl()
        {
            Assert.IsTrue(HeroHitFeelRules.CanRecoilSideways(false, false, false, false, false, false));
            Assert.IsFalse(HeroHitFeelRules.CanRecoilSideways(true, false, false, false, false, false), "already recoiling");
            Assert.IsFalse(HeroHitFeelRules.CanRecoilSideways(false, true, false, false, false, false), "dashing");
            Assert.IsFalse(HeroHitFeelRules.CanRecoilSideways(false, false, true, false, false, false), "clinging");
            Assert.IsFalse(HeroHitFeelRules.CanRecoilSideways(false, false, false, true, false, false), "wall-jump locked");
            Assert.IsFalse(HeroHitFeelRules.CanRecoilSideways(false, false, false, false, true, false), "hurt or dead");
            Assert.IsFalse(HeroHitFeelRules.CanRecoilSideways(false, false, false, false, false, true), "control locked");
        }

        [Test]
        public void RecoilDirection_PushesAwayFromTheSwing()
        {
            Assert.AreEqual(-1f, HeroHitFeelRules.RecoilDirection(true));
            Assert.AreEqual(1f, HeroHitFeelRules.RecoilDirection(false));
        }

        [TestCase(10f, 0f, 0f)]
        [TestCase(0.5f, 0f, 0f)]
        [TestCase(0f, 0f, 0f)]
        [TestCase(-4f, 0f, -4f)]   // falling is left alone
        [TestCase(10f, 2f, 2f)]
        public void CapRiseSpeed_CutsOnlyAFasterRise(float vy, float cap, float expected)
        {
            Assert.AreEqual(expected, HeroHitFeelRules.CapRiseSpeed(vy, cap), 1e-5f);
        }

        [TestCase(false, false, false, true)]    // older hittable, no NonBouncer: keeps the pogo
        [TestCase(false, false, true, false)]    // active NonBouncer blocks it
        [TestCase(true, true, false, true)]      // enemy
        [TestCase(true, false, false, false)]    // target says it does not bounce
        [TestCase(true, true, true, false)]
        public void BounceAllowed_Table(bool hasFeedback, bool bounces, bool nonBouncerActive, bool expected)
        {
            Assert.AreEqual(expected, HeroHitFeelRules.BounceAllowed(hasFeedback, bounces, nonBouncerActive));
        }

        [Test]
        public void DefaultConfig_HasTheStudyNumbers()
        {
            var cfg = UnityEngine.ScriptableObject.CreateInstance<HeroConfig>();
            try
            {
                Assert.AreEqual(1, cfg.SilkPerNailHit);
                Assert.AreEqual(3.75f, cfg.NailRecoilSpeed, 1e-5f);
                Assert.AreEqual(8, cfg.NailRecoilSteps);
                Assert.AreEqual(0f, cfg.UpRecoilMaxRiseSpeed, 1e-5f);
                Assert.AreEqual(2, cfg.DownspikeGraceSteps);
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }
    }
}
