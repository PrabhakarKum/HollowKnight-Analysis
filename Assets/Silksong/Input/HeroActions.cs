using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowknight.Silksong
{
    // Reads HeroInputActions and exposes it as IHeroInput. State is sampled exactly once per
    // Update, so every consumer in a frame sees the same edges (WasPressed cannot be consumed
    // twice or missed).
    // Runs before default-order scripts so hero code in the same Update sees this frame's input.
    [DefaultExecutionOrder(-100)]
    public class HeroActions : MonoBehaviour, IHeroInput
    {
        // Held-state tracking for one button, advanced once per Update.
        struct ButtonState
        {
            public bool Pressed, WasPressed, WasReleased, Suppressed;
            public float Held;

            public InputButton ToButton() => new InputButton(Pressed, WasPressed, WasReleased, Held);

            public void Advance(bool physicallyDown, float dt)
            {
                // After ReleaseAll a still-held key must not look like a fresh press: wait for
                // the physical release before the button can come alive again.
                if (Suppressed)
                {
                    if (!physicallyDown) Suppressed = false;
                    physicallyDown = false;
                }

                WasPressed = physicallyDown && !Pressed;
                WasReleased = !physicallyDown && Pressed;
                Pressed = physicallyDown;
                Held = Pressed ? (WasPressed ? 0f : Held + dt) : 0f;
            }

            public void ForceRelease()
            {
                // Report the release once so consumers end jump height / cancel charge logic.
                WasReleased = Pressed;
                WasPressed = false;
                Pressed = false;
                Held = 0f;
                Suppressed = true;
            }
        }

        [SerializeField] InputActionAsset actionsAsset;

        InputActionAsset runtimeAsset;
        InputAction move, jump, dash, attack;
        ButtonState jumpState, dashState, attackState;
        Vector2 moveValue;

        public Vector2 Move => moveValue;
        public InputButton Jump => jumpState.ToButton();
        public InputButton Dash => dashState.ToButton();
        public InputButton Attack => attackState.ToButton();

        // Lets tests and code-built heroes supply the asset without a serialized reference.
        public void Initialize(InputActionAsset asset)
        {
            actionsAsset = asset;
            if (isActiveAndEnabled) Bind();
        }

        void OnEnable() => Bind();
        void OnDisable() => Unbind();

        void Bind()
        {
            Unbind();
            if (actionsAsset == null) return;

            // A clone, so enabling/disabling here never affects another user of the shared asset.
            runtimeAsset = Instantiate(actionsAsset);
            var map = runtimeAsset.FindActionMap("Hero", true);
            move = map.FindAction("Move", true);
            jump = map.FindAction("Jump", true);
            dash = map.FindAction("Dash", true);
            attack = map.FindAction("Attack", true);
            map.Enable();
        }

        void Unbind()
        {
            if (runtimeAsset == null) return;
            runtimeAsset.Disable();
            Destroy(runtimeAsset);
            runtimeAsset = null;
            ReleaseAll();
        }

        void Update()
        {
            if (runtimeAsset == null) return;

            // Unscaled: hold time measures the player's finger, not hit-stop or slow motion.
            float dt = Time.unscaledDeltaTime;
            var raw = move.ReadValue<Vector2>();
            moveValue = raw.sqrMagnitude > 1f ? raw.normalized : raw;
            jumpState.Advance(jump.IsPressed(), dt);
            dashState.Advance(dash.IsPressed(), dt);
            attackState.Advance(attack.IsPressed(), dt);
        }

        // Clears every button and the stick, e.g. when the window loses focus and the release
        // event would never arrive.
        public void ReleaseAll()
        {
            moveValue = Vector2.zero;
            jumpState.ForceRelease();
            dashState.ForceRelease();
            attackState.ForceRelease();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) ReleaseAll();
        }
    }
}
