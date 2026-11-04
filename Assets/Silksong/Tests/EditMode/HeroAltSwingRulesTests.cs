using NUnit.Framework;

namespace Hollowknight.Silksong.Tests
{
    // The alternate sideways swing toggle (study 15 section 3).
    public class HeroAltSwingRulesTests
    {
        [Test]
        public void ResetSeconds_IsTheOriginalsRecoveryPlusResetTime()
        {
            Assert.AreEqual(0.65f, HeroAltSwingRules.ResetSeconds, 1e-6f);
        }

        [Test]
        public void ShouldReset_OnlyAfterMoreThanTheLimit_OrWhenTheDirectionChanged()
        {
            Assert.IsFalse(HeroAltSwingRules.ShouldReset(0.3f, false));
            Assert.IsFalse(HeroAltSwingRules.ShouldReset(0.65f, false), "exactly the limit is still inside");
            Assert.IsTrue(HeroAltSwingRules.ShouldReset(0.66f, false));
            Assert.IsTrue(HeroAltSwingRules.ShouldReset(0.1f, true));
        }

        [Test]
        public void FlagAfterReset_KeepsAnArmedFlagInsideTheWindow_AndClearsItOtherwise()
        {
            Assert.IsTrue(HeroAltSwingRules.FlagAfterReset(true, 0.4f, false));
            Assert.IsFalse(HeroAltSwingRules.FlagAfterReset(true, 0.7f, false));
            Assert.IsFalse(HeroAltSwingRules.FlagAfterReset(true, 0.4f, true));
            Assert.IsFalse(HeroAltSwingRules.FlagAfterReset(false, 0.1f, false));
        }

        [Test]
        public void SideSwings_GoFirstAlternateFirstAlternate()
        {
            bool flag = false;
            var used = new System.Collections.Generic.List<bool>();
            for (int i = 0; i < 4; i++)
            {
                flag = HeroAltSwingRules.FlagAfterReset(flag, 0.45f, false);
                bool alt = flag;
                flag = HeroAltSwingRules.FlagAfterSideSwing(alt);
                used.Add(alt);
            }
            CollectionAssert.AreEqual(new[] { false, true, false, true }, used);
        }

        [Test]
        public void BodyClip_PicksTheNames()
        {
            Assert.AreEqual("Slash", HeroAltSwingRules.BodyClip(false));
            Assert.AreEqual("SlashAlt", HeroAltSwingRules.BodyClip(true));
            Assert.AreEqual("WallSlash", HeroAltSwingRules.WallBodyClip);
        }

        [Test]
        public void EffectClip_UsesTheAlternateOnlyForASidewaysAlternate()
        {
            Assert.AreEqual("SlashAltEffect", HeroSlashEffectRules.ClipFor(AttackDirection.Normal, true));
            Assert.AreEqual("SlashEffect", HeroSlashEffectRules.ClipFor(AttackDirection.Normal, false));
            Assert.AreEqual("SlashUpEffect", HeroSlashEffectRules.ClipFor(AttackDirection.Up, true));
            Assert.AreEqual("SlashDownEffect", HeroSlashEffectRules.ClipFor(AttackDirection.Down, true));
        }

        [Test]
        public void WallEffect_SitsOnTheSwingSide()
        {
            Assert.AreEqual(0.61f, HeroSlashEffectRules.LocalPosition(AttackDirection.Normal, true, true).x, 1e-6f);
            Assert.AreEqual(-0.61f, HeroSlashEffectRules.LocalPosition(AttackDirection.Normal, false, true).x, 1e-6f);
        }
    }
}
