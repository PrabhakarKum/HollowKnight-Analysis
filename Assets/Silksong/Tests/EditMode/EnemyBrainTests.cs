using NUnit.Framework;

namespace Hollowknight.Silksong.Tests
{
    public class EnemyBrainTests
    {
        const float dt = 0.02f;

        static EnemyProbes Clear => new EnemyProbes { GroundUnderBackFoot = true, WallAhead = false, GroundAhead = true };
        static EnemyProbes Wall => new EnemyProbes { GroundUnderBackFoot = true, WallAhead = true, GroundAhead = true };
        static EnemyProbes Ledge => new EnemyProbes { GroundUnderBackFoot = true, WallAhead = false, GroundAhead = false };
        static EnemyProbes Airborne => new EnemyProbes { GroundUnderBackFoot = false, WallAhead = true, GroundAhead = false };

        static EnemyBrain NewBrain(int facing = 1) => new EnemyBrain(3f, facing);

        [Test]
        public void Walks_AtSpeedThree_InFacingDirection()
        {
            var right = NewBrain(1).Step(dt, Clear);
            Assert.AreEqual(3f, right.VelocityX, 1e-5f);
            Assert.AreEqual(EnemyState.Walking, right.State);
            Assert.AreEqual(1, right.Facing);
            var left = NewBrain(-1).Step(dt, Clear);
            Assert.AreEqual(-3f, left.VelocityX, 1e-5f);
            Assert.AreEqual(-1, left.Facing);
        }

        [Test]
        public void WallAhead_Turns()
        {
            var b = NewBrain(1);
            var o = b.Step(dt, Wall);
            Assert.IsTrue(o.Turned);
            Assert.AreEqual(EnemyState.Turning, o.State);
            Assert.AreEqual(-1, o.Facing);
            Assert.AreEqual(0f, o.VelocityX, 1e-5f);
        }

        [Test]
        public void LedgeAhead_Turns()
        {
            var b = NewBrain(1);
            var o = b.Step(dt, Ledge);
            Assert.IsTrue(o.Turned);
            Assert.AreEqual(-1, o.Facing);
            Assert.AreEqual(EnemyState.Turning, o.State);
        }

        [Test]
        public void NoGroundUnderBackFoot_NeverTurns()
        {
            var b = NewBrain(1);
            for (int i = 0; i < 10; i++)
            {
                var o = b.Step(dt, Airborne);
                Assert.IsFalse(o.Turned);
                Assert.AreEqual(EnemyState.Walking, o.State);
                Assert.AreEqual(1, o.Facing);
                Assert.AreEqual(3f, o.VelocityX, 1e-5f);
            }
        }

        [Test]
        public void Turning_TakesExactlyOneStep_ThenWalksTheOtherWay()
        {
            var b = NewBrain(1);
            Assert.AreEqual(EnemyState.Turning, b.Step(dt, Wall).State);
            // Next step still sees the wall in the probes (stale), but the turn step does not re-check.
            var next = b.Step(dt, Wall);
            Assert.AreEqual(EnemyState.Walking, next.State);
            Assert.IsFalse(next.Turned);
            Assert.AreEqual(-3f, next.VelocityX, 1e-5f);
            Assert.AreEqual(-1, next.Facing);
        }

        [Test]
        public void TurnSteps_Two_HoldsForTwoSteps()
        {
            var b = new EnemyBrain(3f, 1, 2);
            Assert.AreEqual(EnemyState.Turning, b.Step(dt, Wall).State);
            Assert.AreEqual(EnemyState.Turning, b.Step(dt, Clear).State);
            Assert.AreEqual(EnemyState.Walking, b.Step(dt, Clear).State);
        }

        [Test]
        public void FacingFlips_EachTurn()
        {
            var b = NewBrain(1);
            b.Step(dt, Wall); b.Step(dt, Clear);
            Assert.AreEqual(-1, b.Facing);
            b.Step(dt, Ledge); b.Step(dt, Clear);
            Assert.AreEqual(1, b.Facing);
        }

        [Test]
        public void Shove_AddsVelocity_ForItsDuration_ThenStops()
        {
            var b = NewBrain(1);
            b.Shove(15f, 0.15f);
            int shovedSteps = 0;
            for (int i = 0; i < 20; i++)
            {
                var o = b.Step(dt, Clear);
                if (o.VelocityX > 3.5f)
                {
                    shovedSteps++;
                    Assert.AreEqual(18f, o.VelocityX, 1e-4f, "walk 3 plus shove 15");
                }
                else
                {
                    Assert.AreEqual(3f, o.VelocityX, 1e-4f, "plain walking once the shove is over");
                }
            }
            Assert.That(shovedSteps, Is.InRange(7, 8), "0.15 s at 0.02 s per step");
            Assert.IsFalse(b.IsShoved);
        }

        [Test]
        public void Shove_Leftwards_SubtractsAndKeepsWalking()
        {
            var b = NewBrain(1);
            b.Shove(-15f, 0.15f);
            Assert.AreEqual(-12f, b.Step(dt, Clear).VelocityX, 1e-4f);
            Assert.AreEqual(EnemyState.Walking, b.State);
        }

        [Test]
        public void WeakerShove_IsIgnored_StrongerReplaces()
        {
            var b = NewBrain(1);
            b.Shove(15f, 0.15f);
            b.Shove(5f, 1f);
            Assert.AreEqual(18f, b.Step(dt, Clear).VelocityX, 1e-4f, "weaker ignored");
            b.Shove(20f, 0.1f);
            Assert.AreEqual(23f, b.Step(dt, Clear).VelocityX, 1e-4f, "stronger replaces");
        }

        [Test]
        public void NoTurn_WhileShoved()
        {
            var b = NewBrain(1);
            b.Shove(15f, 0.15f);
            var o = b.Step(dt, Wall);
            Assert.IsFalse(o.Turned);
            Assert.AreEqual(EnemyState.Walking, o.State);
        }

        [Test]
        public void Kill_ZeroVelocity_IgnoresProbesAndShoves()
        {
            var b = NewBrain(1);
            b.Shove(15f, 1f);
            b.Kill();
            Assert.AreEqual(EnemyState.Dead, b.State);
            foreach (var p in new[] { Clear, Wall, Ledge, Airborne })
            {
                var o = b.Step(dt, p);
                Assert.AreEqual(0f, o.VelocityX);
                Assert.AreEqual(EnemyState.Dead, o.State);
                Assert.IsFalse(o.Turned);
                Assert.AreEqual(1, o.Facing);
            }
            b.Shove(15f, 1f);
            Assert.AreEqual(0f, b.Step(dt, Clear).VelocityX);
        }
    }
}
