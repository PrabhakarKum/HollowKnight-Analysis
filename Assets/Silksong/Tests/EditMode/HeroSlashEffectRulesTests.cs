using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // The pure rules of the nail swipe (study 15 sections 1 and 9).
    public class HeroSlashEffectRulesTests
    {
        [Test]
        public void ClipFor_PicksTheEffectOfTheSwingDirection()
        {
            Assert.AreEqual("SlashEffect", HeroSlashEffectRules.ClipFor(AttackDirection.Normal));
            Assert.AreEqual("SlashUpEffect", HeroSlashEffectRules.ClipFor(AttackDirection.Up));
            Assert.AreEqual("SlashDownEffect", HeroSlashEffectRules.ClipFor(AttackDirection.Down));
        }

        [Test]
        public void Scale_MatchesThePrefab()
        {
            Assert.AreEqual(new Vector3(0.9f, 1f, 1f), HeroSlashEffectRules.LocalScale(AttackDirection.Normal));
            Assert.AreEqual(new Vector3(1f, 0.9f, 1f), HeroSlashEffectRules.LocalScale(AttackDirection.Up));
            Assert.AreEqual(new Vector3(1.2f, 1.2f, 1f), HeroSlashEffectRules.LocalScale(AttackDirection.Down));
        }

        [Test]
        public void Position_SideAndUpSitOnTheHeroRoot_InFrontByZ()
        {
            foreach (bool right in new[] { false, true })
            {
                var side = HeroSlashEffectRules.LocalPosition(AttackDirection.Normal, right, false);
                var up = HeroSlashEffectRules.LocalPosition(AttackDirection.Up, right, false);
                Assert.AreEqual(0f, side.x); Assert.AreEqual(0f, side.y); Assert.AreEqual(-0.001f, side.z);
                Assert.AreEqual(0f, up.x); Assert.AreEqual(0f, up.y); Assert.AreEqual(-0.001f, up.z);
            }
        }

        [Test]
        public void Position_DownOffsetFollowsFacing()
        {
            var left = HeroSlashEffectRules.LocalPosition(AttackDirection.Down, false, false);
            var right = HeroSlashEffectRules.LocalPosition(AttackDirection.Down, true, false);
            Assert.AreEqual(0.69f, left.x, 1e-5f);
            Assert.AreEqual(-0.69f, right.x, 1e-5f);
            Assert.AreEqual(-0.37f, left.y, 1e-5f);
            Assert.AreEqual(-0.37f, right.y, 1e-5f);
            Assert.AreEqual(-0.001f, left.z);
        }

        [Test]
        public void Position_WallSwipeOffsetFollowsSwingSide()
        {
            Assert.AreEqual(0.61f, HeroSlashEffectRules.LocalPosition(AttackDirection.Normal, true, true).x, 1e-5f);
            Assert.AreEqual(-0.61f, HeroSlashEffectRules.LocalPosition(AttackDirection.Normal, false, true).x, 1e-5f);
        }

        [Test]
        public void VisibleSeconds_DownIsTheThrustWindow_OthersTheWholeClip()
        {
            Assert.AreEqual(0.10f, HeroSlashEffectRules.DownVisibleSeconds);
            Assert.AreEqual(0.10f, HeroSlashEffectRules.VisibleSeconds(AttackDirection.Down, 0.15f), 1e-6f);
            Assert.AreEqual(0.05f, HeroSlashEffectRules.VisibleSeconds(AttackDirection.Down, 0.05f), 1e-6f);
            Assert.AreEqual(0.20f, HeroSlashEffectRules.VisibleSeconds(AttackDirection.Normal, 0.20f), 1e-6f);
            Assert.AreEqual(0.25f, HeroSlashEffectRules.VisibleSeconds(AttackDirection.Up, 0.25f), 1e-6f);
        }

        [Test]
        public void FlipX_OnlyWhenGoingRight()
        {
            Assert.IsTrue(HeroSlashEffectRules.FlipX(true));
            Assert.IsFalse(HeroSlashEffectRules.FlipX(false));
        }
    }
}
