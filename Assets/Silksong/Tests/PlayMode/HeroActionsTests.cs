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
            hero.Initialize(asset);
        }

        public override void TearDown()
        {
            Object.Destroy(go);
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

            yield return null;
            Assert.IsFalse(hero.Jump.WasPressed, "next frame");
            Assert.IsTrue(hero.Jump.IsPressed, "still held");

            Release(keyboard.spaceKey);
            yield return null;
            Assert.IsTrue(hero.Jump.WasReleased, "release frame");
            Assert.IsFalse(hero.Jump.IsPressed);
        }

        [UnityTest]
        public IEnumerator Move_OppositeKeysHeld_ReturnsZeroX()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            Press(keyboard.aKey);
            Press(keyboard.dKey);
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

            hero.ReleaseAll();

            Assert.IsFalse(hero.Jump.IsPressed);
        }
    }
}
