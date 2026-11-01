using UnityEngine;

namespace Hollowknight.Silksong
{
    // The only thing hero code may know about input. Tests supply a fake, so no hero test needs
    // a keyboard or gamepad.
    //
    // Edge contract: WasPressed / WasReleased are latched in Update and cleared after the
    // FixedUpdate that could consume them, so hero code must read input in FixedUpdate and sees each
    // press exactly once. A fake used in tests must behave the same way: edges set by the test persist
    // until the test advances a fixed step.
    public interface IHeroInput
    {
        // Left stick / d-pad / arrows, already dead-zoned and clamped to length 1.
        Vector2 Move { get; }
        InputButton Jump { get; }
        InputButton Dash { get; }
        InputButton Attack { get; }
    }
}
