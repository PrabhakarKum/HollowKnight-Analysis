# Stubs

Features that call `Stubs.NotImplemented`, added automatically in the Editor.

- GameCamera: sprint, wall-sprint, slide, harpoon, updraft, super-jump and rising look-ahead/offsets
- GameCamera: umbrella-float look-down
- GameCamera: lock areas (CameraLockArea) and camera offset areas
- GameCamera: detach / short-damp after hits and respawns
- GameCamera: screen shake and post effects (bloom, colour correction, noise) as URP volume
- tk2d RandomFrame/RandomLoop wrap modes (played as a plain loop)
- Hero room: real Silksong room art and geometry (the room is plain blocks)
- Hero hazard respawn
- TransitionPoint.Door
- GameCamera: entry snap ignores lock areas (no CameraLockArea clamp or lock-area entry offset, the only case where the original's inward edge nudge can run) and does not wait one physics step
- RoomInfo: scene manager look and feel (colours, light, particles, audio, wind, darkness, border walls)
- PersistentBool.SemiPersistent
- RoomTransitionManager.LevelStateAndGc
- Hero leaving through a door or unknown gate
- Hero entering through a door or unknown gate
- Hero dash and sprint re-entry after a room change (only when the hero was dashing at leave)
- Gate hardLandOnExit (hard landing after a top-gate arrival)
- Gate alwaysEnterLeft/alwaysEnterRight facing at bottom gates
