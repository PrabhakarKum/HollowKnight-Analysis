using System.Collections;

namespace Hollowknight.Silksong
{
    // Loads and swaps room scenes. Implemented by AdditiveRoomLoader (Task 6) and by a fake in tests.
    // Three steps, because the screen must be dark before the old room disappears (original: load the
    // target additively without activating it, wait for the fade, activate, then unload the old scene).
    public interface IRoomLoader
    {
        // Starts loading the room with this name but keeps it hidden. Finishes when it is ready to activate.
        IEnumerator LoadHidden(string sceneName);

        // Makes the hidden room live and unloads the previous one. The new room's gates register
        // themselves, so GateRegistry.Find works once this finishes.
        IEnumerator ActivateAndUnloadPrevious();
    }
}
