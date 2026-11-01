namespace Hollowknight.Silksong
{
    // Snapshot of one button for the current frame. A struct so a consumer cannot mutate the
    // shared state, and so reading it allocates nothing.
    public readonly struct InputButton
    {
        // Held right now.
        public bool IsPressed { get; }
        // True for exactly one Update: the frame the button went down.
        public bool WasPressed { get; }
        // True for exactly one Update: the frame the button went up (or was forced up).
        public bool WasReleased { get; }
        // Seconds the button has been continuously held; 0 when not held.
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
