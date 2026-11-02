using System.Collections;

namespace Hollowknight.Silksong
{
    // Optional extras for a room loader, used by RoomTransitionManager when a change fails or is cut short.
    // IRoomLoader (frozen) cannot express them; a loader without this interface is simply not cleaned up after.
    public interface IRoomLoaderCleanup
    {
        // Name of the room that is live now, or null when none was loaded by the loader. A request for this same
        // room is refused (two scenes of one name cannot be told apart).
        string CurrentRoomName { get; }

        // Synchronous, never throws: forget a room that was loaded hidden but will not be activated. Safe to call
        // from Abort / OnDisable. Anything that needs frames to finish is completed by FinishAbandon.
        void AbandonPending();

        // Completes the clean-up: waits for abandoned loads and unloads leftover scenes.
        IEnumerator FinishAbandon();
    }
}
