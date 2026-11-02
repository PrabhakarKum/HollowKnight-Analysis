namespace Hollowknight.Silksong
{
    // The pure decisions of hazard and death respawn, kept apart from physics and scenes so they can be tested alone.
    public static class RespawnRules
    {
        // The room and marker used when the player data names none (the new-game bench, the start room's Start marker).
        public const string DefaultBenchScene = "Tut_01";
        public const string StartMarkerName = "Start";

        public enum HazardSpotSource { None, Saved, RoomStart }

        // A saved hazard spot counts only in the room it was saved in. An empty scene (old save) never counts.
        public static bool IsHazardSpotUsable(HazardRespawn spot, string room)
        {
            return spot != null && spot.IsSet && !string.IsNullOrEmpty(room) && spot.scene == room;
        }

        // A spot that is set but belongs to another room (or to no known room): it should be ignored and cleared.
        public static bool IsHazardSpotStale(HazardRespawn spot, string room)
        {
            return spot != null && spot.IsSet && !IsHazardSpotUsable(spot, room);
        }

        // Where a hazard hit in `room` puts the hero: the saved spot, else the room's Start marker, else nowhere
        // (then the hit is a plain hurt-and-knockback, as in the first milestone).
        public static HazardSpotSource ChooseHazardSpot(HazardRespawn saved, string room, bool roomHasStartMarker)
        {
            if (IsHazardSpotUsable(saved, room)) return HazardSpotSource.Saved;
            return roomHasStartMarker ? HazardSpotSource.RoomStart : HazardSpotSource.None;
        }

        public struct DeathTarget
        {
            public bool InPlace;       // true: the bench room is the room the hero died in
            public string Scene;
            public string Marker;
        }

        // Where a death sends the hero. Empty respawnScene / respawnMarker fall back to the start room's Start marker.
        public static DeathTarget ChooseDeathTarget(string deathRoom, string respawnScene, string respawnMarker)
        {
            string scene = string.IsNullOrEmpty(respawnScene) ? DefaultBenchScene : respawnScene;
            string marker = string.IsNullOrEmpty(respawnScene) || string.IsNullOrEmpty(respawnMarker) ? StartMarkerName : respawnMarker;
            return new DeathTarget
            {
                InPlace = !string.IsNullOrEmpty(deathRoom) && deathRoom == scene,
                Scene = scene,
                Marker = marker,
            };
        }
    }
}
