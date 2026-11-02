using System;

namespace Hollowknight.Silksong
{
    // The slim slice of the original's PlayerData that Milestone 1 needs. Public fields (not
    // properties) because JsonUtility only serialises fields.
    [Serializable]
    public class PlayerData
    {
        // Bump when the layout changes in a way old code could not read. SaveSystem refuses
        // files newer than this rather than guessing at them.
        public const int CurrentVersion = 1;

        // Defaults to 0 on purpose: a file with no version key must read as invalid, not be stamped current.
        public int version;
        public int health;
        public int maxHealth;
        public int silk;
        public int maxSilk;
        public string respawnScene;
        public string respawnMarker;

        // Added after version 1 shipped. JsonUtility leaves keys missing from an old file at these initialisers,
        // so old saves load unchanged and no version bump is needed.
        public string mapZone = "TUT_01";
        public HazardRespawn hazardRespawn = new HazardRespawn();
        public SceneData sceneData = new SceneData();

        // New-game values mirror the original's field defaults (see docs/study/03-data-and-saving.md).
        // Silk starts at 0 in the original: it is earned by fighting, not given.
        public static PlayerData CreateNew()
        {
            return new PlayerData
            {
                version = CurrentVersion,
                health = 5,
                maxHealth = 5,
                silk = 0,
                maxSilk = 9,
                respawnScene = "Tut_01",
                respawnMarker = "Death Respawn Marker Init",
                mapZone = "TUT_01",
                hazardRespawn = new HazardRespawn(),
                sceneData = new SceneData(),
            };
        }
    }
}
