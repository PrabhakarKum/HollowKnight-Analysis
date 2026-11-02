using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowknight.Silksong
{
    // Decides where a dead hero comes back and drives it. After HeroHealth's own death delay it picks the bench from
    // PlayerData.respawnScene / respawnMarker (RespawnRules.ChooseDeathTarget):
    //  - the bench is in the room the hero died in: the hero is put back at the marker at once (HeroHealth.RespawnAt);
    //  - the bench is in another room: RoomTransitionManager.RequestRespawn loads that room (fade, no walk-in) and the
    //    manager's placer puts the SAME hero at the marker with full health.
    // If anything fails, it falls back to the Start marker of the room the hero is in, so a dead hero is never stuck.
    // Plain class owned by RoomWorldBootstrap; it takes HeroHealth.DeathRespawnHandler in place of the built-in lookup.
    public sealed class DeathRespawnController : IDisposable
    {
        readonly HeroHealth health;
        readonly RoomTransitionManager manager;
        readonly Func<Scene> currentRoom;
        bool pending;

        public DeathRespawnController(HeroHealth heroHealth, RoomTransitionManager transitionManager, Func<Scene> liveRoom)
        {
            health = heroHealth;
            manager = transitionManager;
            currentRoom = liveRoom;
            health.DeathRespawnHandler = OnDeathRespawnDue;
            manager.TransitionFinished += OnFinished;
        }

        public void Dispose()
        {
            if (health != null) health.DeathRespawnHandler = null;
            if (manager != null) manager.TransitionFinished -= OnFinished;
        }

        // The marker by name in this scene, else the scene's Start marker. Respawns the hero there. Used for the
        // in-place case and as the manager's placer.
        public static bool PlaceAtMarker(HeroHealth hero, Scene scene, string markerName)
        {
            var marker = RespawnMarker.Find(markerName, scene) ?? RespawnMarker.Find(RespawnRules.StartMarkerName, scene);
            if (marker == null) return false;
            hero.RespawnAt(marker);
            return true;
        }

        void OnDeathRespawnDue()
        {
            var room = currentRoom();
            string roomName = room.IsValid() ? room.name : "";
            var data = health.Data;
            var target = RespawnRules.ChooseDeathTarget(roomName, data.respawnScene, data.respawnMarker);

            if (target.InPlace)
            {
                if (!PlaceAtMarker(health, room, target.Marker)) Debug.LogError("[Respawn] No respawn marker in room '" + roomName + "'; the hero stays dead.");
                return;
            }

            pending = manager.RequestRespawn(target.Scene, target.Marker);
            if (!pending) FallBackInPlace("the respawn request was refused");
        }

        void OnFinished(bool success)
        {
            if (!pending) return;
            pending = false;
            if (!success && health.IsDead) FallBackInPlace("the room change failed");
        }

        void FallBackInPlace(string why)
        {
            var room = currentRoom();
            Debug.LogWarning("[Respawn] " + why + "; respawning in the current room.");
            if (!room.IsValid() || !PlaceAtMarker(health, room, RespawnRules.StartMarkerName))
                Debug.LogError("[Respawn] No Start marker in the current room; the hero stays dead.");
        }
    }
}
