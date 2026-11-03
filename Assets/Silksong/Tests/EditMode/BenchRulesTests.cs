using NUnit.Framework;

namespace Hollowknight.Silksong.Tests
{
    // Task 4 (saving): the pure bench decisions.
    public class BenchRulesTests
    {
        static bool Can(bool inside = true, HeroState state = HeroState.Grounded, bool grounded = true, float speed = 0f,
                        bool dead = false, bool locked = false, bool transition = false, bool resting = false, float cooldown = 0f)
            => BenchRules.CanRest(inside, state, grounded, speed, dead, locked, transition, resting, cooldown);

        [Test]
        public void StandingStillInsideTheBench_CanRest() => Assert.IsTrue(Can());

        [Test]
        public void OutsideTheTrigger_CannotRest() => Assert.IsFalse(Can(inside: false));

        [Test]
        public void Airborne_CannotRest()
        {
            Assert.IsFalse(Can(grounded: false));
            Assert.IsFalse(Can(state: HeroState.Falling));
            Assert.IsFalse(Can(state: HeroState.Rising));
        }

        [TestCase(HeroState.Dashing)]
        [TestCase(HeroState.Attacking)]
        [TestCase(HeroState.Hurt)]
        [TestCase(HeroState.Dead)]
        [TestCase(HeroState.WallClinging)]
        public void BusyStates_CannotRest(HeroState state) => Assert.IsFalse(Can(state: state));

        [Test]
        public void DeadLockedTransitionOrAlreadyResting_CannotRest()
        {
            Assert.IsFalse(Can(dead: true));
            Assert.IsFalse(Can(locked: true));
            Assert.IsFalse(Can(transition: true));
            Assert.IsFalse(Can(resting: true));
        }

        [Test]
        public void Cooldown_BlocksRest() => Assert.IsFalse(Can(cooldown: 0.1f));

        [Test]
        public void MovingTooFast_CannotRest()
        {
            Assert.IsTrue(Can(speed: BenchRules.MaxRestSpeed));
            Assert.IsFalse(Can(speed: 3f));
            Assert.IsFalse(Can(speed: -3f));
        }

        [Test]
        public void UpEdge_FiresOnlyOnTheStepTheStickGoesUp()
        {
            Assert.IsTrue(BenchRules.IsUpEdge(false, 1f, 0.5f));
            Assert.IsFalse(BenchRules.IsUpEdge(true, 1f, 0.5f), "held");
            Assert.IsFalse(BenchRules.IsUpEdge(false, 0.5f, 0.5f), "threshold itself is not up");
            Assert.IsFalse(BenchRules.IsUpEdge(false, -1f, 0.5f));
        }
    }
}
