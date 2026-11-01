using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowknight.Silksong
{
    // Reads HeroInputActions and exposes it as IHeroInput. Input is sampled once per Update.
    // The hero reads it in FixedUpdate, and Update and FixedUpdate do not run 1:1, so press and
    // release edges are latched in Update and cleared only after a fixed step has run
    // (EndFixedStep). A press in a frame with no fixed step is therefore seen by the next
    // step, and a frame with two fixed steps reports it once.
    // Runs before default-order scripts so Update readers see this frame's input.
    [DefaultExecutionOrder(-100)]
    public class HeroActions : MonoBehaviour, IHeroInput
    {
        // State for one button, advanced once per Update.
        struct ButtonState
        {
            public bool Pressed, LatchedPressed, LatchedReleased, Suppressed;
            public float Held;

            public InputButton ToButton() => new InputButton(Pressed, LatchedPressed, LatchedReleased, Held);

            public void Advance(bool down, bool pressedThisFrame, bool releasedThisFrame, float dt)
            {
                // After ReleaseAll a still-held key must not look like a fresh press: wait for
                // the physical release before the button can come alive again.
                if (Suppressed)
                {
                    if (!down) Suppressed = false;
                    down = pressedThisFrame = releasedThisFrame = false;
                }

                // The ThisFrame signals catch taps that start and end inside one input update,
                // where the polled level never changes.
                bool pressEdge = (down && !Pressed) || pressedThisFrame;
                bool releaseEdge = (!down && Pressed) || releasedThisFrame;

                LatchedPressed |= pressEdge;
                LatchedReleased |= releaseEdge;
                Held = !down ? 0f : pressEdge ? 0f : Held + dt;
                Pressed = down;
            }

            public void EndFixedStep()
            {
                LatchedPressed = false;
                LatchedReleased = false;
            }

            public void ForceRelease(bool stillPhysicallyDown)
            {
                // Report the release so consumers end jump height / cancel charge logic.
                LatchedReleased |= Pressed;
                LatchedPressed = false;
                Pressed = false;
                Held = 0f;
                // Only a key that is really down needs waiting for; arming it for a key that
                // was up would swallow the player's next genuine press.
                Suppressed = stillPhysicallyDown;
            }

            public void Clear() => this = default;
        }

        [SerializeField] InputActionAsset actionsAsset;

        // Test hook: when true no automatic end-of-fixed-step loop runs and the test calls
        // EndFixedStep itself, so it can drive "fixed steps" without real time. Set before
        // Initialize/enable.
        public bool ManualFixedStepping { get; set; }

        InputActionAsset runtimeAsset;
        InputAction move, jump, dash, attack;
        ButtonState jumpState, dashState, attackState;
        Vector2 moveValue;
        Coroutine fixedStepLoop;

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
            if (!ManualFixedStepping) fixedStepLoop = StartCoroutine(FixedStepLoop());
        }

        void Unbind()
        {
            if (fixedStepLoop != null) StopCoroutine(fixedStepLoop);
            fixedStepLoop = null;
            if (runtimeAsset != null)
            {
                runtimeAsset.Disable();
                Destroy(runtimeAsset);
                runtimeAsset = null;
            }
            move = jump = dash = attack = null;
            // Plain reset, not ReleaseAll: nothing is being released by the player here.
            moveValue = Vector2.zero;
            jumpState.Clear();
            dashState.Clear();
            attackState.Clear();
        }

        // WaitForFixedUpdate resumes after every FixedUpdate callback of the step, so edges
        // survive for all readers in that step and are gone for the next one.
        IEnumerator FixedStepLoop()
        {
            var wait = new WaitForFixedUpdate();
            while (true)
            {
                yield return wait;
                EndFixedStep();
            }
        }

        // Called once after each fixed step has run all its FixedUpdates.
        public void EndFixedStep()
        {
            jumpState.EndFixedStep();
            dashState.EndFixedStep();
            attackState.EndFixedStep();
        }

        void Update()
        {
            if (runtimeAsset == null) return;

            // Unscaled: hold time measures the player's finger, not hit-stop or slow motion.
            float dt = Time.unscaledDeltaTime;
            var raw = move.ReadValue<Vector2>();
            moveValue = raw.sqrMagnitude > 1f ? raw.normalized : raw;
            Advance(ref jumpState, jump, dt);
            Advance(ref dashState, dash, dt);
            Advance(ref attackState, attack, dt);
        }

        static void Advance(ref ButtonState state, InputAction action, float dt) =>
            state.Advance(action.IsPressed(), action.WasPressedThisFrame(), action.WasReleasedThisFrame(), dt);

        // Clears every button and reports the releases, e.g. when the window loses focus and
        // the release event would never arrive. Move is level input: it reads zero until the
        // next Update and then follows whatever stick or key is physically held.
        public void ReleaseAll()
        {
            moveValue = Vector2.zero;
            jumpState.ForceRelease(jump != null && jump.IsPressed());
            dashState.ForceRelease(dash != null && dash.IsPressed());
            attackState.ForceRelease(attack != null && attack.IsPressed());
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) ReleaseAll();
        }
    }
}
