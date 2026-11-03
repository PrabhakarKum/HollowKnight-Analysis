using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // Pure rules and the config numbers of the enemy core (docs/study/14-enemies.md, section 7).
    public class EnemyRulesTests
    {
        [Test]
        public void Defaults_AreTheStudyNumbers()
        {
            Assert.AreEqual(2, SilksongEnemyConfigDefaults.MaxHealth);
            Assert.AreEqual(1, SilksongEnemyConfigDefaults.ContactDamage);
            Assert.AreEqual(EnemyDamageType.Enemy, SilksongEnemyConfigDefaults.DamageType);
            Assert.AreEqual(0.07f, SilksongEnemyConfigDefaults.FlashHoldSeconds);
            Assert.AreEqual(0.05f, SilksongEnemyConfigDefaults.FlashFallSeconds);
            Assert.AreEqual(0.85f, SilksongEnemyConfigDefaults.FlashStrength);
            Assert.AreEqual(15f, SilksongEnemyConfigDefaults.ShoveSpeed);
            Assert.AreEqual(0.15f, SilksongEnemyConfigDefaults.ShoveSeconds);
            Assert.AreEqual(3f, SilksongEnemyConfigDefaults.WalkSpeed);
            Assert.AreEqual(1.1f, SilksongEnemyConfigDefaults.GroundBackRayLength);
            Assert.AreEqual(0.6f, SilksongEnemyConfigDefaults.GroundBackRayHeight);
            Assert.AreEqual(0.2f, SilksongEnemyConfigDefaults.WallRayDistance);
            Assert.AreEqual(0.1f, SilksongEnemyConfigDefaults.WallRaySkin);
            Assert.AreEqual(0.2f, SilksongEnemyConfigDefaults.LedgeRayForwardPadding);
            Assert.AreEqual(1.1f, SilksongEnemyConfigDefaults.LedgeRayLength);
            Assert.AreEqual(new Vector2(1.64f, 1.11f), SilksongEnemyConfigDefaults.ColliderSize);
            Assert.AreEqual(new Vector2(-0.03f, -0.20f), SilksongEnemyConfigDefaults.ColliderOffset);
            Assert.IsTrue(SilksongEnemyConfigDefaults.StaysDead);
        }

        [Test]
        public void NewConfigInstance_StartsAtTheDefaults()
        {
            var c = ScriptableObject.CreateInstance<SilksongEnemyConfig>();
            try
            {
                Assert.AreEqual(SilksongEnemyConfigDefaults.MaxHealth, c.maxHealth);
                Assert.AreEqual(SilksongEnemyConfigDefaults.ContactDamage, c.contactDamage);
                Assert.AreEqual(SilksongEnemyConfigDefaults.FlashHoldSeconds, c.flashHoldSeconds);
                Assert.AreEqual(SilksongEnemyConfigDefaults.FlashFallSeconds, c.flashFallSeconds);
                Assert.AreEqual(SilksongEnemyConfigDefaults.FlashStrength, c.flashStrength);
                Assert.AreEqual(SilksongEnemyConfigDefaults.ShoveSpeed, c.shoveSpeed);
                Assert.AreEqual(SilksongEnemyConfigDefaults.ShoveSeconds, c.shoveSeconds);
                Assert.AreEqual(SilksongEnemyConfigDefaults.WalkSpeed, c.walkSpeed);
                Assert.AreEqual(SilksongEnemyConfigDefaults.LedgeRayLength, c.ledgeRayLength);
                Assert.IsTrue(c.staysDead);
                Assert.IsFalse(string.IsNullOrEmpty(c.persistenceIdPrefix));
            }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void ApplyDamage_SubtractsAndClampsAtZero()
        {
            Assert.AreEqual(1, EnemyRules.ApplyDamage(2, 1));
            Assert.AreEqual(0, EnemyRules.ApplyDamage(1, 5));
            Assert.AreEqual(2, EnemyRules.ApplyDamage(2, -3), "negative damage does not heal");
        }

        [Test]
        public void CanTakeHit_RefusesDeadAndZeroDamage()
        {
            Assert.IsTrue(EnemyRules.CanTakeHit(false, 1));
            Assert.IsFalse(EnemyRules.CanTakeHit(true, 1));
            Assert.IsFalse(EnemyRules.CanTakeHit(false, 0));
            Assert.IsTrue(EnemyRules.IsLethal(0));
            Assert.IsFalse(EnemyRules.IsLethal(1));
        }

        [Test]
        public void Shove_GoesAwayFromTheSource()
        {
            Assert.AreEqual(1f, EnemyRules.ShoveDirection(5f, 2f, true));
            Assert.AreEqual(-1f, EnemyRules.ShoveDirection(1f, 2f, true));
            Assert.AreEqual(1f, EnemyRules.ShoveDirection(2f, 2f, true));
            Assert.AreEqual(1f, EnemyRules.ShoveDirection(0f, 0f, false));
            Assert.AreEqual(-15f, EnemyRules.ShoveVelocityX(-1f, 15f));
            Assert.AreEqual(2.25f, EnemyRules.ShoveDistance(15f, 0.15f), 1e-5f);
            Assert.IsTrue(EnemyRules.ShovesSideways(AttackDirection.Normal));
            Assert.IsFalse(EnemyRules.ShovesSideways(AttackDirection.Up));
            Assert.IsFalse(EnemyRules.ShovesSideways(AttackDirection.Down));
        }

        [Test]
        public void FlashStrength_HoldsThenFallsToZero()
        {
            const float hold = 0.07f, fall = 0.05f, s = 0.85f;
            Assert.AreEqual(0f, EnemyRules.FlashStrength(-0.01f, hold, fall, s));
            Assert.AreEqual(s, EnemyRules.FlashStrength(0f, hold, fall, s), 1e-5f);
            Assert.AreEqual(s, EnemyRules.FlashStrength(hold, hold, fall, s), 1e-5f);
            Assert.AreEqual(s * 0.5f, EnemyRules.FlashStrength(hold + fall * 0.5f, hold, fall, s), 1e-4f);
            Assert.AreEqual(0f, EnemyRules.FlashStrength(hold + fall, hold, fall, s), 1e-5f);
            Assert.AreEqual(0f, EnemyRules.FlashStrength(1f, hold, fall, s));
            Assert.AreEqual(0.12f, EnemyRules.FlashDuration(hold, fall), 1e-5f);
        }

        [Test]
        public void ContactDamage_IsZeroWhenDead()
        {
            Assert.AreEqual(1, EnemyRules.ContactDamage(1, false));
            Assert.AreEqual(0, EnemyRules.ContactDamage(1, true));
        }
    }
}
