using UnityEngine;

namespace Hollowknight.Silksong
{
    // The only thing hero code may know about input. Tests supply a fake, so no hero test needs
    // a keyboard or gamepad.
    public interface IHeroInput
    {
        // Left stick / d-pad / arrows, already dead-zoned and clamped to length 1.
        Vector2 Move { get; }
        InputButton Jump { get; }
        InputButton Dash { get; }
        InputButton Attack { get; }
    }
}
