using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // Every expectation below comes from TransitionPoint.TryDoTransition (TransitionPoint.cs lines 194-292
    // of the decompiled source) unless a comment says otherwise.
    public class TransitionRulesTests
    {
        static HeroTransitionState Hero(bool facingRight = true, bool backSprinting = false, bool busy = false,
            float velocityY = 0f, bool inPosition = true)
            => new HeroTransitionState
            {
                FacingRight = facingRight, BackSprinting = backSprinting, Busy = busy,
                VelocityY = velocityY, InPosition = inPosition
            };

        static TransitionDecision Eval(GatePosition gate, HeroTransitionState hero,
            GameState state = GameState.Playing, bool running = false, bool hasTarget = true)
            => TransitionRules.Evaluate(gate, state, running, hasTarget, hero);

        // ---- facing outward (study section 1, step 3) ----

        [Test]
        public void RightGate_FacingRight_Leaves()
        {
            Assert.AreEqual(TransitionDecision.Leave, Eval(GatePosition.Right, Hero(facingRight: true)));
        }

        [Test]
        public void RightGate_FacingLeft_IsPushedBack()
        {
            Assert.AreEqual(TransitionDecision.PushBack, Eval(GatePosition.Right, Hero(facingRight: false)));
        }

        [Test]
        public void LeftGate_FacingLeft_Leaves()
        {
            Assert.AreEqual(TransitionDecision.Leave, Eval(GatePosition.Left, Hero(facingRight: false)));
        }

        [Test]
        public void LeftGate_FacingRight_IsPushedBack()
        {
            Assert.AreEqual(TransitionDecision.PushBack, Eval(GatePosition.Left, Hero(facingRight: true)));
        }

        [Test]
        public void BackSprinting_ReversesTheFacingRule()
        {
            // A hero sprinting backwards moves opposite to the way it faces, so "outward" flips.
            Assert.AreEqual(TransitionDecision.Leave, Eval(GatePosition.Right, Hero(facingRight: false, backSprinting: true)));
            Assert.AreEqual(TransitionDecision.PushBack, Eval(GatePosition.Right, Hero(facingRight: true, backSprinting: true)));
            Assert.AreEqual(TransitionDecision.Leave, Eval(GatePosition.Left, Hero(facingRight: true, backSprinting: true)));
            Assert.AreEqual(TransitionDecision.PushBack, Eval(GatePosition.Left, Hero(facingRight: false, backSprinting: true)));
        }

        [TestCase(GatePosition.Top)]
        [TestCase(GatePosition.Bottom)]
        public void VerticalGates_DoNotCheckFacing_TheSwitchOnlyCoversLeftAndRight(GatePosition gate)
        {
            // The original has no facing test for top/bottom, only the busy test; vertical push-back exists
            // for that busy case. Documented deviation from the task text ("outward on all four sides").
            Assert.AreEqual(TransitionDecision.Leave, Eval(gate, Hero(facingRight: true)));
            Assert.AreEqual(TransitionDecision.Leave, Eval(gate, Hero(facingRight: false)));
        }

        [TestCase(GatePosition.Top)]
        [TestCase(GatePosition.Right)]
        [TestCase(GatePosition.Bottom)]
        [TestCase(GatePosition.Left)]
        public void BusyHero_IsPushedBackOnEverySide(GatePosition gate)
        {
            // Binding, recoiling or in a cutscene move: pushed back even when facing outward.
            bool outward = gate != GatePosition.Left;
            Assert.AreEqual(TransitionDecision.PushBack, Eval(gate, Hero(facingRight: outward, busy: true)));
        }

        // ---- game state (section 1, step 2) ----

        [TestCase(GameState.Loading)]
        [TestCase(GameState.EnteringLevel)]
        [TestCase(GameState.Paused)]
        [TestCase(GameState.ExitingLevel)]
        [TestCase(GameState.Cutscene)]
        public void NothingTriggersUnlessPlaying(GameState state)
        {
            Assert.AreEqual(TransitionDecision.Ignore, Eval(GatePosition.Right, Hero(), state));
        }

        [Test]
        public void IgnoredStatesNeverPushBack()
        {
            // Wrong-way hero while not playing: the original returns before the facing check.
            Assert.AreEqual(TransitionDecision.Ignore, Eval(GatePosition.Right, Hero(facingRight: false), GameState.Paused));
        }

        [Test]
        public void Door_IsIgnoredEvenWhileEnteringLevel()
        {
            Assert.AreEqual(TransitionDecision.Ignore,
                Eval(GatePosition.Door, Hero(velocityY: -5f), GameState.EnteringLevel));
        }

        [Test]
        public void UnknownPosition_WithBusyHero_StillLeaves_AsInTheOriginal()
        {
            // The original's push-back only covers the four sides; a busy hero at an unnamed gate falls
            // through to the target check and leaves. Pinned so a later change is deliberate.
            Assert.AreEqual(TransitionDecision.Leave, Eval(GatePosition.Unknown, Hero(busy: true)));
            Assert.AreEqual(TransitionDecision.Ignore, Eval(GatePosition.Unknown, Hero(busy: true), hasTarget: false));
        }

        [Test]
        public void EnteringLevel_AllowsOnlyABottomGateWhileTheHeroMovesDown()
        {
            var falling = Hero(velocityY: -5f, inPosition: true);
            Assert.AreEqual(TransitionDecision.Leave, Eval(GatePosition.Bottom, falling, GameState.EnteringLevel));
            Assert.AreEqual(TransitionDecision.Ignore, Eval(GatePosition.Bottom, Hero(velocityY: 0f), GameState.EnteringLevel));
            Assert.AreEqual(TransitionDecision.Ignore, Eval(GatePosition.Bottom, Hero(velocityY: 3f), GameState.EnteringLevel));
            Assert.AreEqual(TransitionDecision.Ignore, Eval(GatePosition.Bottom, Hero(velocityY: -5f, inPosition: false), GameState.EnteringLevel));
            Assert.AreEqual(TransitionDecision.Ignore, Eval(GatePosition.Left, Hero(facingRight: false, velocityY: -5f), GameState.EnteringLevel));
        }

        // ---- running transition (GameManager.BeginSceneTransitionRoutine line 629) ----

        [Test]
        public void SecondTriggerWhileATransitionRuns_IsIgnored()
        {
            Assert.AreEqual(TransitionDecision.Ignore, Eval(GatePosition.Right, Hero(), running: true));
            // Also does not push back: the hero is already walking out.
            Assert.AreEqual(TransitionDecision.Ignore, Eval(GatePosition.Right, Hero(facingRight: false), running: true));
        }

        // ---- target data (TransitionPoint.cs line ~283) ----

        [Test]
        public void MissingTarget_DoesNotLeave_ButStillPushesBackAWrongWayHero()
        {
            Assert.AreEqual(TransitionDecision.Ignore, Eval(GatePosition.Right, Hero(), hasTarget: false));
            Assert.AreEqual(TransitionDecision.PushBack, Eval(GatePosition.Right, Hero(facingRight: false), hasTarget: false));
        }

        // ---- doors and unknown names ----

        [Test]
        public void Door_IsNotTouchTriggered()
        {
            // Doors need an interact press (EnterDoorSequence); that path is a stub in this milestone.
            Assert.AreEqual(TransitionDecision.Ignore, Eval(GatePosition.Door, Hero()));
        }

        [Test]
        public void UnknownPosition_BehavesLikeTheOriginal_LeavesWhenTargetIsSet()
        {
            Assert.AreEqual(TransitionDecision.Leave, Eval(GatePosition.Unknown, Hero()));
        }

        // ---- push-back distance (TransitionPoint.cs lines 255-290) ----

        [Test]
        public void PushBackOffset_MovesTheHeroJustOutsideTheGateBox()
        {
            var gate = new Bounds(new Vector3(10f, 0f, 0f), new Vector3(2f, 6f, 1f));   // x 9..11, y -3..3
            var hero = new Bounds(new Vector3(10.2f, 0f, 0f), new Vector3(1f, 2f, 1f));  // x 9.7..10.7, y -1..1

            AssertOffset(new Vector2(9f - 10.7f, 0f), TransitionRules.PushBackOffset(GatePosition.Right, gate, hero));
            AssertOffset(new Vector2(11f - 9.7f, 0f), TransitionRules.PushBackOffset(GatePosition.Left, gate, hero));
            AssertOffset(new Vector2(0f, -3f - 1f), TransitionRules.PushBackOffset(GatePosition.Top, gate, hero));
            AssertOffset(new Vector2(0f, 3f + 1f), TransitionRules.PushBackOffset(GatePosition.Bottom, gate, hero));
        }

        [Test]
        public void PushBackOffset_DoorAndUnknown_AreZero()
        {
            var b = new Bounds(Vector3.zero, Vector3.one);
            Assert.AreEqual(Vector2.zero, TransitionRules.PushBackOffset(GatePosition.Door, b, b));
            Assert.AreEqual(Vector2.zero, TransitionRules.PushBackOffset(GatePosition.Unknown, b, b));
        }

        static void AssertOffset(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f);
            Assert.AreEqual(expected.y, actual.y, 1e-4f);
        }
    }
}
