# Milestone 2: room transitions

Status: draft for the owner's approval. Nothing here is built yet. Source of truth for how Silksong does it: `docs/study/10-room-transitions.md`.

## Goal

The hero walks out of one room and arrives in the next at the matching gate, with a fade, the hero walking in, the camera snapping to the new room, and control returning afterwards. Three connected rooms: Tut_01, Tut_01b, Tut_02 (gate names and links as in study page 10).

## Owner decisions (2026-11-01)

| Question | Decision |
|---|---|
| Room look | Grey-box rooms with Silksong's gate names and links. Real room art comes later. |
| Room loading | Separate Unity scenes, loaded additively like Silksong. Tests load them by path; the owner adds them to Build Settings once for real builds. |
| Hero changes | Additive only: new `HeroConfig` fields (walk-in timing and the like) and one input-lock hook in `HeroController`. Existing values and behaviour do not change. |

Standing rules apply: private branches `agent/builder/task-N-*` in worktrees; builders never open Unity; QA is the only Unity runner; reviewer is read-only; merge into `main` only after the owner says yes; no co-author lines; do not touch `SampleScene` or the owner's tuned values; every "not implemented yet" feature logs through `Stubs.NotImplemented`.

## Tasks

Each task: failing tests, run, implement, run, checkpoint. Pure logic is tested in EditMode; the rest in PlayMode with fakes, so tests need no real room scenes until Task 6.

1. **Gate data and rules** (must go first). `Core/GatePosition`, `Core/GameState`, `Rooms/TransitionPoint`, `Rooms/GateRegistry`, `Rooms/TransitionRules`; interfaces `IRoomLoader`, `ITransitionHero`, `IScreenFader`. Tests: gate position from name (`left1`, `bot2`, `door1`); the hero must face outward to trigger, otherwise it is pushed back; ignored unless state is PLAYING; lookup by name, missing gate falls back to the first gate with a warning.
2. **Scene data and respawn data** (parallel with 3, 4, 5). `Rooms/SceneData`, `Rooms/PersistentBool`, `PlayerData` fields for map zone and hazard respawn, save round trip. Decide the save-format version bump with the owner if one is needed.
3. **Transition manager** (parallel). `Rooms/RoomTransitionManager`, `Core/ScreenFader`, driven by fakes. State sequence PLAYING, EXITING_LEVEL, ENTERING_LEVEL, PLAYING; 0.34 s fade wait; a second request is ignored; input locked from first touch until finished.
4. **Hero leave and enter** (parallel; the only task that edits `HeroController` and `HeroConfig`, additively). `Hero/HeroTransitionDriver` plus the input-lock hook. Tests: leaving right gives velocity at run speed and the hero cannot be hurt; entering at a left gate places the hero at the gate plus offset on the ground, facing right, walking 0.33 s, control returns after, invulnerable until then; top and bottom variants.
5. **Camera on entry and room bounds** (parallel). `GameCamera` snap-and-hold, `Rooms/RoomInfo`, bounds swap. Tests: camera jumps to the hero inside the new bounds, stays frozen 0.1 s. The original edge nudge only runs with lock-area offsets, so it is not built. Camera values stay in `GameCameraSettings`, not `HeroConfig`.
6. **Real room loading** (needs 1, 3, 4, 5). `Rooms/AdditiveRoomLoader` and a persistent bootstrap holding hero, camera, manager and fader. PlayMode test with two tiny test scenes: leave A, arrive in B at the named gate, A is gone, the hero object is the same instance, nothing is duplicated after re-entering.
7. **Grey-box rooms** (data part parallel with 2-5; builder needs Task 1; finishes after 6). `Editor/TransitionRoomsBuilder`, menu `Silksong/Build Transition Rooms`, generated and gitignored scenes for Tut_01, Tut_01b, Tut_02 using the study page's gate table, plus an owner-steps page. All hero values still come from the one `HeroConfig.asset`. Built: each room holds gates, RoomInfo, RoomBounds and a `RoomWorldStarter` (creates the world when a room is played directly); in the Editor rooms load by path through `FallbackRoomSceneSource`; owner steps in `docs/study/11-transition-rooms.md`.
8. **Hazard and death respawn across rooms** (needs 2, 3, 6). `Rooms/HazardRespawnMarker`, `HazardRespawnTrigger`; death loads `respawnScene` and the saved marker. Built: markers and triggers store the spot with the room name (a spot from another room is ignored), gate arrival sets it (`nonHazardGate` / `respawnMarker` on `TransitionPoint`), spikes (`HazardZone` with respawn on) put the hero back inside the room, and a death with the bench in another room goes through `RoomTransitionManager.RequestRespawn` driven by `DeathRespawnController`. Details in `docs/study/12-respawn-across-rooms.md`.
9. **Milestone 2 acceptance**. `docs/study/M2-checklist.md`, a PlayMode walk through every gate pair Tut_01 to Tut_01b to Tut_01 to Tut_02, full test run, owner plays it.

Parallel groups after Task 1: 2, 3, 4, 5 and the data part of 7. Then 6, then 8, then 9. `HeroController` is edited only by Task 4.

## Known risks

1. No input-lock or scripted-move API on the hero yet; Task 4 adds it. Only one task may edit `HeroController`.
2. Fade length lives inside Silksong's PlayMaker Screen Fader, which we cannot reuse. Code-side timings are known (0.34 s, 0.165 s, 0.33 s); the fade is tuned by eye with the owner.
3. Rooms must be in Build Settings to load by name in real builds. That file is in ProjectSettings, so the owner adds them; tests load by path.
4. Hero, camera and manager persist across scene changes. Bootstrap and teardown must not leak or duplicate them, in tests or on re-entry.
5. Silksong's field is called `entryPoint`; this plan uses `entryGate` for clarity and notes the rename in code comments.

## Left open for later milestones

Pickups and breakables staying broken after leaving a room (the data classes exist after Task 2, nothing uses them yet), camera lock areas (stubbed), doors, audio, real room art.

## Milestone 2 status

Tasks 1-9 are built. Task 9 adds `docs/study/M2-checklist.md` (the owner's hands-on acceptance list with a result table) and the PlayMode fixture `M2AcceptanceTests`, which walks every gate pair of the real generated rooms through the real gate triggers with scripted input, checks the facing rule, a spike hit in each room, death in Tut_01b back to the Tut_01 bench, a second death and a second full round trip with one hero, camera, manager and fader throughout. Still open: QA has not yet run the new fixture (rebuild the rooms with `TransitionRoomsBuilder.BuildBatch` first), the owner has not played the checklist, fade and walk-in timings are tuned by eye, and jump or dash into a gate is covered by hand only.
