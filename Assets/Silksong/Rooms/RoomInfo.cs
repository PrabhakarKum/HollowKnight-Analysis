using UnityEngine;

namespace Hollowknight.Silksong
{
    // Per-room facts, one component on each room's root. The original keeps these on the room's scene manager
    // (CustomSceneManager: mapZone, scene type) and reads the room size from the tile map. Here the size comes from
    // a RoomBounds placed by hand, and everything that is only look and feel is left out (see the stub below).
    // Camera numbers are NOT per room: they stay in GameCameraSettings on the camera.
    public class RoomInfo : MonoBehaviour
    {
        [SerializeField] string sceneName;
        // Silksong's Tut_* rooms use map zone 13 ("Moss Cave"). Kept as a plain id plus a display name so nothing
        // depends on a zone enum that does not exist yet.
        [SerializeField] int mapZoneId = 13;
        [SerializeField] string mapZoneName = "Moss Cave";
        [SerializeField] RoomBounds bounds;

        // The scene name when none was set, so a room built from code still has a usable key.
        public string SceneName => string.IsNullOrEmpty(sceneName) ? gameObject.scene.name : sceneName;
        public int MapZoneId => mapZoneId;
        public string MapZoneName => mapZoneName;
        public RoomBounds Bounds => bounds;

        void Awake()
        {
            Stubs.NotImplemented("RoomInfo: scene manager look and feel (colours, light, particles, audio, wind, darkness, border walls)");
        }

        // For rooms built from code (tests, generated scenes).
        public void Configure(string scene, int zoneId, string zoneName, RoomBounds roomBounds)
        {
            sceneName = scene;
            mapZoneId = zoneId;
            mapZoneName = zoneName;
            bounds = roomBounds;
        }
    }
}
