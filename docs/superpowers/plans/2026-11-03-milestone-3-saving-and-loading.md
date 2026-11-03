# Milestone 3: saving and loading

Status: draft for the owner's approval. Nothing here is built yet. Source of truth for how Silksong does it: `docs/study/03-data-and-saving.md` (M1 summary) plus a new study page written in Task 1.

## Goal

The game starts on a start screen with four save slots. You pick a slot, play, rest at a bench or change room, quit, come back, pick the slot again and continue where you were: same room, same health, broken walls still broken, collected pickups still gone.

## What already exists (from Milestones 1 and 2)

- `SaveSystem` (JSON slot files, atomic write, corrupt files set aside, newer-version files never overwritten) and `PlayerData` (health, silk, respawn scene and marker, map zone, hazard respawn spot, current room, `SceneData`).
- `SceneData` and its bool/int storage, with a round-trip test. Nothing uses it yet.
- `HeroHealth` already saves when a slot is set (`SaveSlot`). Nothing sets it today.
- `RoomWorldBootstrap` keeps `PlayerData` in memory and records the room on every change; writing it to disk is a known stub.

## Owner decisions (2026-11-03)

| Question | Decision |
|---|---|
| Choosing a slot | A simple grey-box start screen with 4 slots (empty, or room and health shown). New game, continue, delete with two confirms like the original. Real art comes later. |
| When it saves | At a bench rest, and quietly after every completed room change. |
| Proof that rooms remember | A breakable wall and a pickup in Tut_01 that stay broken and collected. |
| Where Continue puts the hero (after the Task 1 study) | At the last bench or respawn marker, like Silksong; never mid-room. The room-change autosave still runs, so broken walls, pickups and the room record survive, but the hero's position does not. |
| What a slot shows (after the Task 1 study) | Map zone (area) and max health, like Silksong. Silk, money and play time are stubs. |

Findings from `docs/study/13-saving-and-loading.md` that shape the tasks below: Silksong writes the slot at death, at a bench and on quit to menu, never at a room change (our room-change autosave is our own addition); loading forces silk to 0; persistent objects are keyed by scene name plus id and the id should be explicit, not the object name; New game over a newer-version or unreadable slot needs an explicit delete first; delete asks twice; persistent objects must read the bootstrap's current `PlayerData.sceneData` at use time because loading replaces it.

Standing rules apply: private branches `agent/builder/*` in worktrees; builders never open Unity; QA is the only Unity runner; merge into `main` only after the owner says yes; no co-author lines; do not touch `SampleScene`, the owner's tuned values or `HeroConfig.asset` values; do not edit `HeroInputActions.inputactions` (the owner has uncommitted edits in it); every "not implemented yet" feature logs through `Stubs.NotImplemented`. New `PlayerData` fields are additive (no version bump while there are no shipped saves, as in Milestone 2).

## Tasks

Each task: failing tests, run, implement, run, checkpoint. Logic is tested in EditMode against a temp folder (never the real save folder); the rest in PlayMode with fakes.

1. **Study the original and write the save page.** Read-only study of `GameManager` save/load, `DesktopSaveRestoreHandler`, `SaveGameData`, `SceneData`, `PersistentBoolItem` and the bench rest sequence. Output `docs/study/13-saving-and-loading.md`: what is saved when, what a slot shows, how scene data is applied to a room, what rest does. Lists what we copy and what we skip (encryption, backups, cloud saves).
2. **Slot manager** (needs 1). `Data/SaveSlots`: current slot, list four slot summaries (empty / ok with map zone and max health, play time stub / corrupt / from a newer version), load a slot into a `PlayerData`, start a new game in a slot, delete a slot, autosave the current data. Wraps `SaveSystem`; no new file format. Tests use a temp root folder.
3. **Rooms that remember** (parallel with 2). `Rooms/PersistentBool` (an id that reads and writes `SceneData` for its room), `BreakableWall` (nail hit breaks it once, stays broken), `Pickup` (touch collects once). Applied when the room becomes active, so a room left and re-entered, or loaded from a save, shows the right state. The builder adds one wall and one pickup to Tut_01.
4. **Bench rest and autosave** (needs 2). `Rooms/Bench`: stand in the bench trigger and press up (the existing Move input, no new input binding) to rest: set the bench respawn, heal to full, save, short fade. Autosave after every completed room change. Resting animation stays a stub.
5. **Load into the world** (needs 2, 3, 4). `RoomWorldBootstrap` starts from a loaded `PlayerData`: Continue puts the hero at the saved bench or respawn marker (`respawnScene` / `respawnMarker`), with the saved health and silk reset to 0 as the original does; a new game starts in Tut_01 as today. Death and hazard respawn keep working from loaded data. Playing a room directly in the editor does not write saves (so tuning never overwrites a slot).
6. **Start screen** (needs 2, 5). A generated grey-box scene (builder in the same style as the room builder, menu `Silksong/Build Start Screen`, gitignored): four slot rows (map zone and max health), keyboard and gamepad navigation, New/Continue/Delete with confirm, corrupt and newer-version slots shown plainly and never overwritten without a choice. Starts the world in the chosen slot.
7. **Milestone 3 acceptance.** `docs/study/M3-checklist.md`, PlayMode round trip: new game in a slot, break the wall, take the pickup, change room, rest, rebuild the world from disk, continue, and everything is still the same. Full test run, then the owner plays it.

Order: 1, then 2 and 3 in parallel, then 4, then 5, then 6, then 7.

## Known risks

1. Game state lives in several places (hero, camera, manager, bootstrap). Loading must rebuild them without duplicating the persistent world, as in Milestone 2.
2. Saving during a room change or a death must not write a half-state. Task 4 writes only when a transition has finished or a bench is used.
3. A start screen that starts the world needs the persistent world to be created from a menu scene. Tests must not leak scenes (the Milestone 2 lesson).
4. Rooms must be rebuilt by the owner after pulling changes (the builders are menu items).
5. Play time is not tracked yet; the slot shows room and health only (stub).

## Left open for later milestones

Silk and money in slots, tools and items, journal and quests, map markers, cloud saves, save backups, real start-screen art, bench animation, audio.
