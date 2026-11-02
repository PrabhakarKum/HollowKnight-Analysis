using System.Collections;

namespace Hollowknight.Silksong
{
    // Black-screen fade used around a room change. Implemented by Core/ScreenFader (Task 3) and a fake in tests.
    // Fade lengths are chosen by the implementation, tuned by eye (the original keeps them inside a PlayMaker FSM).
    public interface IScreenFader
    {
        // Fades to black; finishes when the screen is fully dark so the room can be swapped unseen.
        IEnumerator FadeOut();

        // Fades back to the game; finishes when the screen is fully visible.
        IEnumerator FadeIn();
    }
}
