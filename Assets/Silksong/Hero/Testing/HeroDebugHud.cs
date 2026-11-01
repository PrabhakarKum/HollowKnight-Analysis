using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowknight.Silksong
{
    // Test-scene helper: wires the hero and draws a debug overlay (F1 hides it, R returns the hero to
    // the start). HeroController's Config and Input are code-injected properties (not serialized, so
    // tests need no assets), which means a scene needs something to inject them; this object does it
    // so no gameplay class had to change. Runs before the hero's own scripts.
    [DefaultExecutionOrder(-200)]
    public class HeroDebugHud : MonoBehaviour
    {
        [SerializeField] HeroController hero;
        [SerializeField] HeroActions actions;
        [SerializeField] HeroConfig config;

        public HeroController Hero => hero;
        public HeroConfig Config => config;

        bool visible = true;
        Vector3 startPosition;
        float takeoffY;
        float apexAboveTakeoff;
        bool wasGrounded = true;
        GUIStyle style;

        void Awake() => Wire();

        // Public so an EditMode test can check the wiring without entering play mode.
        public void Wire()
        {
            if (hero == null || config == null || actions == null)
            {
                Debug.LogError("HeroDebugHud: hero, actions or config is not assigned; the hero will not move. Rebuild the scene with Hollowknight > Silksong > Build Hero Test Scene.", this);
                return;
            }
            hero.Config = config;
            hero.Input = actions;
            startPosition = hero.transform.position;
        }

        void Start()
        {
            if (hero == null) return;
            // The hero turns interpolation off in Awake; smoothing is cosmetic only (physics is
            // unchanged) and stops the 50 Hz fixed step from looking juddery at a higher frame rate.
            var body = hero.GetComponent<Rigidbody2D>();
            if (body != null) body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f1Key.wasPressedThisFrame) visible = !visible;
                if (kb.rKey.wasPressedThisFrame) ResetHero();
            }

            if (hero == null) return;
            // Apex is measured from where the hero last stood, which is the number to compare with the
            // platform heights in the scene.
            float y = hero.transform.position.y;
            if (hero.IsGrounded) takeoffY = y;
            else
            {
                // Reset on leaving the floor; the last apex then stays readable after landing.
                if (wasGrounded) apexAboveTakeoff = 0f;
                apexAboveTakeoff = Mathf.Max(apexAboveTakeoff, y - takeoffY);
            }
            wasGrounded = hero.IsGrounded;
        }

        void ResetHero()
        {
            var body = hero.GetComponent<Rigidbody2D>();
            hero.transform.position = startPosition;
            if (body != null) { body.position = startPosition; body.linearVelocity = Vector2.zero; }
        }

        void OnGUI()
        {
            if (!visible || hero == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };

            var v = hero.Velocity;
            string text =
                "<b>HERO</b>\n" +
                $"State: {hero.State}\n" +
                $"Velocity: ({v.x:F2}, {v.y:F2})\n" +
                $"IsGrounded: {hero.IsGrounded}\n" +
                $"Facing right: {hero.FacingRight}\n" +
                $"Last jump apex above takeoff: {apexAboveTakeoff:F2} u\n\n" +
                "<b>KEYS</b>\n" +
                "Arrows / WASD: move    Z / Space: jump (hold = higher)\n" +
                "X: attack (stub)    C: dash (stub)    Gamepad A: jump\n" +
                "R: back to start    F1: hide this panel\n";
            if (config != null)
            {
                text += "\n<b>CONFIG</b>\n" +
                    $"RunSpeed {config.RunSpeed}  JumpSpeed {config.JumpSpeed}  Gravity {config.Gravity}  FallMax {config.FallSpeedMax}\n" +
                    $"JumpHold {config.JumpHoldMinSeconds}-{config.JumpHoldMaxSeconds}s  ReleaseMult {config.JumpReleaseMultiplier}\n" +
                    $"Coyote {config.CoyoteSeconds}s  JumpBuffer {config.JumpBufferSeconds}s";
            }

            // Clamped so the panel never spills out of a small game window.
            float w = Mathf.Min(520f, Screen.width - 16f);
            float h = Mathf.Min(250f, Screen.height - 16f);
            GUI.Box(new Rect(8, 8, w, h), GUIContent.none);
            GUI.Label(new Rect(16, 12, w - 16, h - 12), text, style);
        }
    }
}
