using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Hollowknight.Silksong.Tests
{
    public class HeroActionsTests : InputTestFixture
    {
        const string AssetPath = "Assets/Silksong/Input/HeroInputActions.inputactions";

        GameObject go;
        HeroActions hero;

        public override void Setup()
        {
            base.Setup();
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.IsNotNull(asset, "Missing " + AssetPath);
#else
            InputActionAsset asset = null;
            Assert.Fail("These tests load the actions asset through the editor.");
#endif
            go = new GameObject("HeroActionsTest");
            hero = go.AddComponent<HeroActions>();
            // The tests stand in for FixedUpdate by calling EndFixedStep, so frame count and
            // fixed-step count are independent and deterministic.
            hero.ManualFixedStepping = true;
            hero.Initialize(asset);
        }

        public override void TearDown()
        {
            // Immediate, so OnDisable unbinds the actions while the input system is still the
            // test's, before the base class restores the real one.
            Object.DestroyImmediate(go);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Jump_WasPressed_TrueOnlyOnPressFrame()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            Press(keyboard.spaceKey);
            yield return null;
            Assert.IsTrue(hero.Jump.WasPressed, "press frame");
            Assert.IsTrue(hero.Jump.IsPressed);
            hero.EndFixedStep();

            yield return null;
            Assert.IsFalse(hero.Jump.WasPressed, "next frame");
            Assert.IsTrue(hero.Jump.IsPressed, "still held");
            hero.EndFixedStep();

            Release(keyboard.spaceKey);
            yield return null;
            Assert.IsTrue(hero.Jump.WasReleased, "release frame");
            Assert.IsFalse(hero.Jump.IsPressed);
        }

        [UnityTest]
        public IEnumerator Move_OppositeKeysHeld_ReturnsZeroX()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            // Positive controls: the bindings really produce movement, so zero below is the
            // cancellation and not an unbound action.
            Press(keyboard.aKey);
            yield return null;
            Assert.Less(hero.Move.x, 0f, "A alone");
            Release(keyboard.aKey);

            Press(keyboard.dKey);
            yield return null;
            Assert.Greater(hero.Move.x, 0f, "D alone");

            Press(keyboard.aKey);
            yield return null;
            Assert.AreEqual(0f, hero.Move.x);
        }

        [UnityTest]
        public IEnumerator Gamepad_LeftStickLeft_MovesLeft()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();

            Set(gamepad.leftStick, new Vector2(-1f, 0f));
            yield return null;

            Assert.Less(hero.Move.x, 0f);
        }

        [UnityTest]
        public IEnumerator ReleaseAll_WhileJumpHeld_ClearsPressedState()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            Press(keyboard.spaceKey);
            yield return null;
            Assert.IsTrue(hero.Jump.IsPressed, "precondition: jump held");
            hero.EndFixedStep();

            hero.ReleaseAll();

            Assert.IsFalse(hero.Jump.IsPressed);
            Assert.IsTrue(hero.Jump.WasReleased, "release is reported");
            Assert.AreEqual(0f, hero.Jump.HeldSeconds);
            hero.EndFixedStep();

            // Space is still physically down: it must not come back as a new press.
            yield return null;
            Assert.IsFalse(hero.Jump.IsPressed, "held key stays off");
            Assert.IsFalse(hero.Jump.WasPressed);

            // Only a real release and a new press revive it.
            Release(keyboard.spaceKey);
            yield return null;
            Press(keyboard.spaceKey);
            yield return null;
            Assert.IsTrue(hero.Jump.WasPressed);
            Assert.IsTrue(hero.Jump.IsPressed);
        }

        [UnityTest]
        public IEnumerator FocusLost_WhileJumpHeld_ReleasesJump()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            Press(keyboard.spaceKey);
            yield return null;

            // SendMessage reaches the private Unity callback the engine itself would call.
            hero.SendMessage("OnApplicationFocus", false);

            Assert.IsFalse(hero.Jump.IsPressed);
            Assert.IsTrue(hero.Jump.WasReleased);
        }

        [UnityTest]
        public IEnumerator ReleaseAll_WhileJumpNotHeld_DoesNotSwallowNextPress()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            hero.ReleaseAll();
            Press(keyboard.spaceKey);
            yield return null;

            Assert.IsTrue(hero.Jump.WasPressed);
        }

        [UnityTest]
        public IEnumerator PressDuringFrameWithNoFixedStep_IsSeenByNextFixedStep()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            Press(keyboard.spaceKey);
            yield return null;
            yield return null; // a second frame passes with no fixed step in it

            Assert.IsTrue(hero.Jump.WasPressed, "still pending for the next fixed step");
            hero.EndFixedStep();
            Assert.IsFalse(hero.Jump.WasPressed, "consumed by that step");
            Assert.IsTrue(hero.Jump.IsPressed);
        }

        [UnityTest]
        public IEnumerator TwoFixedStepsInOneFrame_ReportPressOnce()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            Press(keyboard.spaceKey);
            yield return null;

            bool step1 = hero.Jump.WasPressed;
            hero.EndFixedStep();
            bool step2 = hero.Jump.WasPressed;
            hero.EndFixedStep();

            Assert.IsTrue(step1, "first step sees the press");
            Assert.IsFalse(step2, "second step must not see it again");
        }

        [UnityTest]
        public IEnumerator ReleaseEdge_IsLatchedTheSameWay()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            Press(keyboard.spaceKey);
            yield return null;
            hero.EndFixedStep();

            Release(keyboard.spaceKey);
            yield return null;
            yield return null; // no fixed step yet
            Assert.IsTrue(hero.Jump.WasReleased, "pending for the next fixed step");
            Assert.AreEqual(0f, hero.Jump.HeldSeconds);

            hero.EndFixedStep();
            Assert.IsFalse(hero.Jump.WasReleased, "reported once");
        }

        [UnityTest]
        public IEnumerator HeldSeconds_GrowsWhileHeldAndResetsOnRelease()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            Press(keyboard.spaceKey);
            yield return null;
            Assert.AreEqual(0f, hero.Jump.HeldSeconds, "press frame");
            yield return null;
            yield return null;
            Assert.Greater(hero.Jump.HeldSeconds, 0f);

            Release(keyboard.spaceKey);
            yield return null;
            Assert.AreEqual(0f, hero.Jump.HeldSeconds);
        }
    }
}
