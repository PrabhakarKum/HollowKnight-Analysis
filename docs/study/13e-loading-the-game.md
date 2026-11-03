# 13e. Loading the game (new game, continue, quit)

Code: `Assets/Silksong/Rooms/GameSession.cs`. Tests: `Tests/PlayMode/LoadGameTests.cs`, `Tests/EditMode/GameSessionRulesTests.cs`.

## What it is

`GameSession.StartGame(slot, continueSave, createWorld, result)` is the one way to begin a real game in a save slot. The
start screen (Task 6) calls it. A room played directly in the editor does not: it has no slot, so it never writes a save.

`createWorld` is a small function the caller gives: it builds the persistent world (hero, camera, manager, fader) around the
`PlayerData` it is handed, normally `d => RoomWorldBootstrap.Create(config, input, settings, source, d)`. Hero art, input
and the host scene stay the caller's job, exactly as `RoomWorldStarter` does them today. The answer comes back in a
`GameStartResult` (`Done`, `Success`, `Message`, `World`), because a coroutine cannot return a value.

## New game

1. The slot gets a fresh save (`SaveSlots.StartNew`). A corrupt or newer-version slot is refused: delete it first.
2. The slot is selected, the world is built with that data, and the hero starts in `Tut_01` on its `Start` marker.
3. If that marker is the bench marker, it becomes the death respawn. The file is written once more so it knows the room.

## Continue

1. The slot is read (`SaveSlots.Load`): silk is set to 0, health stays as saved (a saved health of 0 or less is repaired to
   full, since a hero is never saved dead). Empty, corrupt and newer-version slots are refused.
2. The loaded object is handed to `RoomWorldBootstrap.Create`, so the bootstrap, the hero's health, the bench and every
   `PersistentBool` read the same `PlayerData` (no stale copy). Walls and pickups of the loaded room are told to re-read it too.
3. The world starts in `respawnScene` on the marker named `respawnMarker`. This is the last bench, never the place the
   player quit (as in the original: `GetRespawnInfo` / `ReadyForRespawn`).
4. If the marker is missing, in this order: the room's first bench marker (a `Bench` or a marker flagged as bench), then its
   `Start` marker, then the first gate. A warning says which. A fallback bench becomes the saved respawn.
5. The hazard respawn spot is replaced by the marker the hero stands on, so an old spot from another room cannot be used.

## While playing

- `HeroHealth.SaveSlot` is the selected slot, so a hazard or death respawn saves there. Direct room play keeps -1.
- A death respawn, in the same room or through another room, still goes to the bench from the loaded data.
- Bench rest and the room-change autosave write to the selected slot as before.
- Death save: the original writes the slot at the moment of death, before health is refilled, so its file can hold health 0.
  This port does not copy that. The respawn saves after the refill, and the room-change autosave skips a dead hero, so a
  slot never holds health 0. Logged as a stub: "Save at the moment of death".

## Quit to the menu

`GameSession.Quit(save = true)` (a coroutine): saves the selected slot unless the hero is dead, destroys the world (which also
destroys hero, camera, manager, fader and stops a running transition), unloads the room scene and abandoned loads, forgets
the selected slot and clears the persistent-object statics. After it, `StartGame` can run again with exactly one hero, one
camera and one manager. It is safe to call with no game running. A scene that holds the bootstrap (the menu scene) is
protected and stays loaded.

## Stubbed

- Load fade and the bench wake-up animation (the hero just appears): `Game start: load fade and the bench wake-up animation`.
- Save at the moment of death (see above).
- Play time, silk, money in the slot (earlier stubs).
- Continue never resumes mid-room (owner decision).

## Things Task 6 needs to know

- Build the world in the factory the way `RoomWorldStarter.CreateWorld` does (host scene, `HeroActions`, hero art,
  `AudioListener`), and pass the loaded data to `RoomWorldBootstrap.Create`.
- Call `yield return GameSession.Quit()` before starting another slot; `StartGame` refuses while a world exists.
