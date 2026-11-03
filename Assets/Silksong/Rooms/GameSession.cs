using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowknight.Silksong
{
    // Answer of GameSession.StartGame (a coroutine cannot return a value). Done turns true when the coroutine ends.
    public sealed class GameStartResult
    {
        public bool Done;
        public bool Success;
        public string Message = "";
        public RoomWorldBootstrap World;
    }

    // The entry point that starts and ends a real game in a save slot (the start screen calls it; rooms played
    // directly in the editor do NOT, so they never write a save).
    //
    //  - New game: the slot gets a fresh PlayerData (SaveSlots.StartNew), the world starts in Tut_01 on its Start marker.
    //  - Continue: the slot is read (SaveSlots.Load: silk 0, health as saved) and the world starts in
    //    data.respawnScene on the marker data.respawnMarker, as the original's GetRespawnInfo / ReadyForRespawn do:
    //    at the last bench, never mid-room. A missing marker falls back to the room's first bench, then its Start
    //    marker, then its first gate, with a warning.
    //  - The loaded PlayerData is handed to RoomWorldBootstrap.Create, so there is only ever one copy: HeroHealth, the
    //    bench, the hazard respawn and every PersistentBool read it.
    //  - HeroHealth.SaveSlot follows the selected slot, so a respawn saves to it.
    //  - A slot that is empty (continue), corrupt or from a newer version is refused: nothing is created or selected.
    //
    // Quit tears the world down so another game can start (no second hero, camera or manager).
    // Rules that are tested without a scene live in GameSessionRules.
    public static class GameSession
    {
        public const string NewGameRoom = "Tut_01";

        // createWorld builds the persistent world around the given PlayerData, normally
        // d => RoomWorldBootstrap.Create(config, input, settings, source, d). Hero art, input and the host scene are
        // the caller's business (see RoomWorldStarter.CreateWorld).
        public static IEnumerator StartGame(int slot, bool continueSave, Func<PlayerData, RoomWorldBootstrap> createWorld, GameStartResult result)
        {
            if (result == null) result = new GameStartResult();
            result.Done = false;
            result.Success = false;
            result.World = null;

            if (RoomWorldBootstrap.Instance != null)
            {
                Finish(result, false, "A game is already running; quit it first.");
                yield break;
            }
            if (createWorld == null)
            {
                Finish(result, false, "No world factory was given.");
                yield break;
            }

            var slotResult = continueSave ? SaveSlots.Load(slot) : SaveSlots.StartNew(slot);
            if (!slotResult.Success)
            {
                Finish(result, false, slotResult.Message);
                yield break;
            }
            var data = slotResult.Data;
            GameSessionRules.SanitizeHealth(data);

            Stubs.NotImplemented("Game start: load fade and the bench wake-up animation (the hero simply appears)");
            SaveSlots.Select(slot);
            var world = createWorld(data);
            if (world == null || world.Hero == null)
            {
                SaveSlots.Deselect();
                Finish(result, false, "The world could not be created.");
                yield break;
            }
            // The factory may have returned a world that was not given our data; make sure there is only this copy.
            if (!ReferenceEquals(world.PlayerData, data)) world.PlayerData = data;
            world.Hero.Health.SaveSlot = slot;

            string room = continueSave ? GameSessionRules.ContinueRoom(data) : NewGameRoom;
            string markerName = continueSave ? data.respawnMarker : RespawnRules.StartMarkerName;

            yield return world.StartInRoom(room);
            if (!world.Loader.CurrentRoom.IsValid() || !world.Loader.CurrentRoom.isLoaded || world.Loader.CurrentRoom.name != room)
            {
                yield return AbortStart(world);
                Finish(result, false, "Could not load the room '" + room + "'.");
                yield break;
            }

            var scene = world.Loader.CurrentRoom;
            var marker = FindStartMarker(scene, markerName, warn: continueSave);
            if (marker != null)
            {
                world.PlaceHeroAt(marker.transform.position, marker.FacingRight);
                // The hero stands here, so this is also the hazard spot (a stale one from another room is replaced).
                data.SetHazardRespawn(scene.name, marker.MarkerName, marker.transform.position, marker.FacingRight);
                if (continueSave)
                {
                    if (marker.MarkerName != data.respawnMarker || scene.name != data.respawnScene)
                    {
                        // A fallback bench becomes the respawn; any other fallback leaves the saved respawn alone.
                        if (GameSessionRules.IsBenchLike(marker.SetsBenchRespawn, marker.GetComponent<Bench>() != null)) world.SetBenchRespawn(marker);
                    }
                }
                else if (marker.SetsBenchRespawn) world.SetBenchRespawn(marker);
            }
            else
            {
                Debug.LogWarning("[Session] Room '" + scene.name + "' has no respawn marker; the hero stands at its first gate.");
            }

            // Objects of the loaded room read the world's data when they start; this makes sure of it for any that
            // already started with an older answer.
            ReapplyRoomMemory(scene);

            if (!continueSave) SaveSlots.Autosave(data);   // the new file now knows the room and the bench

            Debug.Log("[Session] " + (continueSave ? "Continued" : "Started a new game") + " in slot " + slot + ", room '" + scene.name + "'.");
            result.World = world;
            Finish(result, true, "");
        }

        // The marker named name in the scene, else the first bench-like marker, else the Start marker, else null.
        // warn: say so when it is not the named one.
        public static RespawnMarker FindStartMarker(Scene scene, string name, bool warn)
        {
            var exact = RespawnMarker.Find(name, scene);
            if (exact != null) return exact;

            RespawnMarker bench = null, start = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var m in root.GetComponentsInChildren<RespawnMarker>(false))
                {
                    if (bench == null && GameSessionRules.IsBenchLike(m.SetsBenchRespawn, m.GetComponent<Bench>() != null)) bench = m;
                    if (start == null && m.MarkerName == RespawnRules.StartMarkerName) start = m;
                }
            }
            var found = bench ?? start;
            if (warn)
                Debug.LogWarning("[Session] Marker '" + name + "' was not found in room '" + scene.name + "'; "
                    + (found != null ? "using '" + found.MarkerName + "'." : "no bench or Start marker either."));
            return found;
        }

        static void ReapplyRoomMemory(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var wall in root.GetComponentsInChildren<BreakableWall>(true)) wall.Reapply();
                foreach (var pickup in root.GetComponentsInChildren<Pickup>(true)) pickup.Reapply();
                foreach (var enemy in root.GetComponentsInChildren<EnemyPersistence>(true)) enemy.Reapply();
            }
        }

        static IEnumerator AbortStart(RoomWorldBootstrap world)
        {
            SaveSlots.Deselect();
            yield return DestroyWorld(world);
        }

        static void Finish(GameStartResult result, bool success, string message)
        {
            result.Success = success;
            result.Message = message;
            result.Done = true;
            if (!success) Debug.LogWarning("[Session] Could not start: " + message);
        }

        // Leaves the game: saves the selected slot (unless the hero is dead), destroys the persistent world (hero,
        // camera, manager, fader, loader), unloads the room scenes and forgets the selected slot, so StartGame can run
        // again with no leftovers. Safe to call with no game running.
        public static IEnumerator Quit(bool save = true)
        {
            var world = RoomWorldBootstrap.Instance;
            if (world != null && save)
            {
                var health = world.Hero != null ? world.Hero.Health : null;
                bool dead = health != null && (health.IsDead || health.Health <= 0);
                if (!dead) SaveSlots.Autosave(world.PlayerData);
            }
            SaveSlots.Deselect();
            yield return DestroyWorld(world);
            PersistentBool.ResetStatics();
        }

        static IEnumerator DestroyWorld(RoomWorldBootstrap world)
        {
            if (world == null)
            {
                yield return UnloadLeftoverHost();
                yield break;
            }
            var loader = world.Loader;
            // Destroying the world also stops its coroutines (a transition in progress) and destroys hero and camera.
            UnityEngine.Object.Destroy(world.gameObject);
            yield return null;

            if (loader != null)
            {
                yield return loader.FinishAbandon();
                var room = loader.CurrentRoom;
                if (room.IsValid() && room.isLoaded && !loader.IsProtected(room) && SceneManager.sceneCount > 1)
                {
                    var op = SceneManager.UnloadSceneAsync(room);
                    if (op != null) yield return op;
                }
            }
            yield return UnloadLeftoverHost();
            yield return null;
        }

        // The empty scene RoomWorldFactory created to host the bootstrap. No world is alive when this runs, so the
        // scene protects nothing; it is only unloaded when empty and not the active scene, and never when a live
        // bootstrap exists.
        static IEnumerator UnloadLeftoverHost()
        {
            if (RoomWorldBootstrap.Instance != null) yield break;
            var host = SceneManager.GetSceneByName(RoomWorldFactory.HostSceneName);
            if (!host.IsValid() || !host.isLoaded) yield break;
            if (host.rootCount != 0 || SceneManager.sceneCount < 2) yield break;
            if (SceneManager.GetActiveScene() == host) yield break;
            var op = SceneManager.UnloadSceneAsync(host);
            if (op != null) yield return op;
        }
    }

    // The decisions of GameSession that need no scene.
    public static class GameSessionRules
    {
        // The room Continue loads: the saved respawn room; Tut_01 when it is unknown (as the original's fallback).
        public static string ContinueRoom(PlayerData data)
        {
            if (data == null || string.IsNullOrEmpty(data.respawnScene)) return GameSession.NewGameRoom;
            return data.respawnScene;
        }

        // A saved health of 0 (or less) would load a hero that dies from the next touch; a hero never rests dead, so
        // this only repairs a broken file. Health above the maximum is cut to it. Returns whether anything changed.
        public static bool SanitizeHealth(PlayerData data)
        {
            if (data == null) return false;
            int before = data.health;
            if (data.maxHealth < 1) data.maxHealth = 1;
            if (data.health < 1) data.health = data.maxHealth;
            if (data.health > data.maxHealth) data.health = data.maxHealth;
            return before != data.health;
        }

        // A fallback marker that can stand in for the saved bench.
        public static bool IsBenchLike(bool setsBenchRespawn, bool hasBench) => setsBenchRespawn || hasBench;
    }
}
