using NUnit.Framework;

namespace Hollowknight.Silksong.Tests
{
    // The pure "should the hero play Turn" decision.
    public class HeroTurnRulesTests
    {
        [Test]
        public void GroundedReversal_PlaysTurn()
        {
            Assert.IsTrue(HeroTurnRules.ShouldPlayTurn(true, HeroState.Grounded, HeroState.Grounded, false));
        }

        [Test]
        public void NoReversal_NoTurn()
        {
            Assert.IsFalse(HeroTurnRules.ShouldPlayTurn(false, HeroState.Grounded, HeroState.Grounded, false));
        }

        [TestCase(HeroState.Rising)]
        [TestCase(HeroState.Falling)]
        [TestCase(HeroState.Dashing)]
        [TestCase(HeroState.WallClinging)]
        [TestCase(HeroState.Attacking)]
        [TestCase(HeroState.Hurt)]
        [TestCase(HeroState.Dead)]
        public void ReversalOutsideTheGroundedState_NoTurn(HeroState state)
        {
            Assert.IsFalse(HeroTurnRules.ShouldPlayTurn(true, state, HeroState.Grounded, false));
        }

        [Test]
        public void ReversalRightAfterWallCling_NoTurn()
        {
            Assert.IsFalse(HeroTurnRules.ShouldPlayTurn(true, HeroState.Grounded, HeroState.WallClinging, false));
        }

        [Test]
        public void ReversalDuringScriptedMove_NoTurn()
        {
            Assert.IsFalse(HeroTurnRules.ShouldPlayTurn(true, HeroState.Grounded, HeroState.Grounded, true));
        }

        [Test]
        public void IdleOrRunIsHeld_OnlyWhileOurTurnIsPlaying()
        {
            Assert.IsTrue(HeroTurnRules.ShouldHoldIdleOrRun(true, true));
            Assert.IsFalse(HeroTurnRules.ShouldHoldIdleOrRun(true, false), "the clip finished or never started");
            Assert.IsFalse(HeroTurnRules.ShouldHoldIdleOrRun(false, true), "a Turn we did not start");
        }
    }
}
