namespace Hollowknight.Silksong
{
    // Snapshot of one button. A struct so a consumer cannot mutate the shared state, and so
    // reading it allocates nothing.
    public readonly struct InputButton
    {
        // Held right now (level value).
        public bool IsPressed { get; }
        // Latched edge: true from the Update that saw the button go down until the end of the
        // next fixed step, so a FixedUpdate reader sees each press exactly once even when a
        // frame has zero or several fixed steps. An Update-only reader may see it on more than
        // one frame if no fixed step runs in between.
        public bool WasPressed { get; }
        // Latched edge for the button going up (or being forced up), same lifetime as WasPressed.
        public bool WasReleased { get; }
        // Seconds the button has been continuously held; 0 when not held (level value).
        public float HeldSeconds { get; }

        public InputButton(bool isPressed, bool wasPressed, bool wasReleased, float heldSeconds)
        {
            IsPressed = isPressed;
            WasPressed = wasPressed;
            WasReleased = wasReleased;
            HeldSeconds = heldSeconds;
        }
    }
}
