using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // Test double for IHeroInput that follows the same edge contract as HeroActions: a press or
    // release set by the test stays visible until the test calls EndFixedStep, exactly like the real
    // latch that is cleared after the FixedUpdate that consumed it.
    public class FakeHeroInput : IHeroInput
    {
        public class FakeButton
        {
            public bool IsPressed;
            public bool WasPressed;
            public bool WasReleased;
            public float HeldSeconds;

            public void Press()
            {
                if (IsPressed) return;
                IsPressed = true;
                WasPressed = true;
            }

            public void Release()
            {
                if (!IsPressed) return;
                IsPressed = false;
                WasReleased = true;
                HeldSeconds = 0f;
            }

            public InputButton Snapshot() => new InputButton(IsPressed, WasPressed, WasReleased, HeldSeconds);
        }

        public Vector2 Move { get; set; }
        public readonly FakeButton JumpButton = new FakeButton();
        public readonly FakeButton DashButton = new FakeButton();
        public readonly FakeButton AttackButton = new FakeButton();

        public InputButton Jump => JumpButton.Snapshot();
        public InputButton Dash => DashButton.Snapshot();
        public InputButton Attack => AttackButton.Snapshot();

        // Call after each fixed step the hero has had the chance to read.
        public void EndFixedStep(float fixedDeltaTime)
        {
            Clear(JumpButton, fixedDeltaTime);
            Clear(DashButton, fixedDeltaTime);
            Clear(AttackButton, fixedDeltaTime);
        }

        static void Clear(FakeButton b, float dt)
        {
            b.WasPressed = false;
            b.WasReleased = false;
            if (b.IsPressed) b.HeldSeconds += dt;
        }
    }
}
