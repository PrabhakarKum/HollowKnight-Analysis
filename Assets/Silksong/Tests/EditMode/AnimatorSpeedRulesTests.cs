using NUnit.Framework;

namespace Hollowknight.Silksong.Tests
{
    // The pure rule for which clip speeds the animator accepts.
    public class AnimatorSpeedRulesTests
    {
        [TestCase(1f, 1f)]
        [TestCase(1.5f, 1.5f)]
        [TestCase(0.25f, 0.25f)]
        [TestCase(2f, 2f)]
        public void PositiveSpeed_IsKept(float input, float expected)
        {
            Assert.AreEqual(expected, AnimatorSpeedRules.Sanitise(input));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonPositiveOrNonFinite_FallsBackToDefault(float input)
        {
            Assert.AreEqual(AnimatorSpeedRules.DefaultSpeed, AnimatorSpeedRules.Sanitise(input));
        }

        [Test]
        public void DefaultSpeed_IsOne()
        {
            Assert.AreEqual(1f, AnimatorSpeedRules.DefaultSpeed);
        }
    }
}
