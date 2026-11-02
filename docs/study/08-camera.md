# 08 - Camera, room bounds and hero scale

Files: `Rooms/GameCamera.cs`, `Rooms/RoomBounds.cs`, `Rooms/SilksongHeroAssets.cs`. Tests: `Tests/PlayMode/GameCameraTests.cs`, `Tests/PlayMode/RoomSceneTests.cs`, `Tests/EditMode/RoomBoundsTests.cs`.

Sources read for this page:

- Original camera code: `CameraTarget.cs` (the follow target that leads the hero), `CameraController.cs` (the camera that eases toward the target, look up/down, room limits), `HeroController.cs` (`LOOK_DELAY`).
- Original values: `Prefabs\Camera\_GameCameras.prefab` in the export (camera ortho size, damp times, look-ahead numbers).
- Prototype: `Assets/Scripts/Camera/CameraFollow2D.cs` and `CameraLockZone.cs`, with the values set in `SampleScene.unity` (they equal the code defaults).

## The owner's rule and where camera numbers live

Everything the hero does is configured only by `Assets/Silksong/Hero/HeroConfig.asset`. The camera is not the hero, so its numbers are in a separate small serialized class, `GameCameraSettings`, on the `GameCamera` component. Nothing in it repeats a HeroConfig value, and HeroConfig was not changed. `HeroConfigConsistency` (tests assembly) enforces the rule for scenes:

- `FindValueMismatches` / `AssertAssetMatchesSilksong`: the asset still holds the documented numbers (RunSpeed, JumpSpeed, Gravity, Dash*, Wall*, Attack*, Hurt*, and the rest, every field). A new HeroConfig field with no documented value is reported until one is added.
- `FindWiringProblems` / `AssertSceneUsesProjectConfig`: in a loaded scene every `HeroController.Config` is the asset itself (same reference, not a copy), no second `HeroConfig` instance exists, and no component in the scene serializes a different `HeroConfig`. `RoomSceneTests` calls it; Task 11b reuses it on the real scene.
- The helper's own behaviour is tested in `HeroConfigConsistencyTests`.

## Units and scale (so HeroConfig distances mean the same as in Silksong)

| Quantity | Value | Source |
|---|---|---|
| tk2d texel size | 0.015625 = 1/64 unit per pixel | every sprite definition in `Collections\Knight Data` |
| Pixels per unit for imported hero sprites | **64** | 1 / texelSize (spike Check C) |
| Hero root scale in the original | (1, 1, 1) | `Hero_Hornet.prefab` |
| Sprite scale in the scene | 1 | same |
| Hero body collider (BoxCollider2D) | size 0.5 x 2.078899, offset (0, -0.51323676) from the hero root | `Hero_Hornet.prefab` |
| Run frame 0 size | 2.47 x 3.06 units (158 x 196 px) | sprite definition `Hornet_run_new0000` |
| Main camera ortho half-height | 8.710664 (view about 30.97 x 17.42 at 16:9) | `_GameCameras.prefab` |

With sprites at 64 PPU and scale 1, the hero is the same size in world units as the original, so `RunSpeed` 8.25 u/s, the jump apex (about 6 units from `JumpSpeed` 18.6 and `Gravity` 60), the dash (28 u/s for 0.1 s = 2.8 units) and the attack boxes all mean what they mean in Silksong. The constants are in code as `SilksongUnits`.

Points for Task 11b:

- The scratch scene built by `HeroTestSceneBuilder` uses a placeholder 0.8 x 1.8 capsule (white squares, not Silksong art). The real scene should give the hero a box collider of `SilksongUnits.HeroColliderSize` at `SilksongUnits.HeroColliderOffset`, sprite scale 1, sprite renderer origin at the hero root. The controller does not depend on the collider size (it casts from whatever collider is there).
- `HeroConfig` attack boxes are centred on the hero **root**, and the root sits about 1.04 units above the feet in the original (the collider centre is 0.51 below the root). The attack boxes are the owner's hand-tuned prototype numbers (marked PROVISIONAL in HeroConfig) and were not touched.
- `HeroController.Config` is not serialized. In the scratch scene `HeroDebugHud` injects it from its own serialized reference to the asset. The real scene needs some bootstrap that does the same from the same single asset (not a copy), and `AssertSceneUsesProjectConfig` will check it.

## How GameCamera works

Two stages, as in the original.

1. **Follow target** (`CameraTarget`). Destination = hero position + look-ahead + dash lead + fall offset. The look-ahead slides toward the facing side at 6 units/s and is clamped at +-1. While the hero moves toward the destination faster than the target eases, the target is carried along by the hero's own movement (never past the destination). The target eases to the destination with a 0.2 s critically damped ease, then is kept inside the room.
2. **Camera** (`CameraController`). Eases toward the target with 0.075 s, with the vertical ease changed by two rules: while holding up or down (0.35 s timer) it uses 0.15 s, and while falling fast it starts at 0.075 and shrinks by 0.1 per second down to 0.03. The result is kept inside the room.

`Step(dt)` does one update; `LateUpdate` calls it with `Time.deltaTime` (and does nothing at time scale 0), and `ManualStepping = true` lets a test call it itself. The smoothing is a closed-form critically damped ease (`GameCamera.Smooth`), the same curve as Unity's `SmoothDamp` but exact, so for a target that stays still 50 Hz and 100 Hz stepping end in the same place (tested to 0.001), and for a running hero they differ by less than 0.15 units (tested). Every time and speed is in seconds and units per second.

URP only: the component has no image effects and needs none; the original camera's bloom, colour correction and noise are a stub (see below).

`RoomBounds` holds the room rectangle. The camera centre is kept at least 14.6 units from the left and right edges and 8.3 from the bottom and top (the original's `KeepWithinSceneBounds`: `[14.6, width - 14.6]` by `[8.3, height - 8.3]` of the room tilemap, origin 0,0). Near an edge the look-ahead is pulled in so it cannot push the target past the limit.

## Comparison table: prototype vs Silksong

"Differs" compares the prototype column with the Silksong column. "GameCamera" says what this task built.

| # | Rule | Prototype | Silksong (source) | GameCamera | Differs |
|---|---|---|---|---|---|
| 1 | View size (ortho half-height) | 6 (scene) | 8.710664 (`_GameCameras.prefab`) | 8.710664 | yes |
| 2 | Vertical anchor above hero | +1.2 | 0 (camera centres on hero root) | 0 | yes |
| 3 | Look-ahead distance | 3 | 1 (`xLookAhead` 1, code clamps to +-1) | 1 | yes |
| 4 | Look-ahead ramp speed | 6 u/s | 6 u/s (literal in `CameraTarget.Update`) | 6 | no |
| 5 | Look-ahead follows facing even when still | yes | yes | yes | no |
| 6 | Look-ahead at a snap/respawn | 0 | +-1 toward the facing side (`PositionToStart`) | +-1 | yes |
| 7 | Look-ahead pulled in at the room edge | no | yes (offset set to stay inside) | yes | yes |
| 8 | Dash lead | 1.5, when dashing and fast | 1 (`dashLookAhead`), when dashing and \|vx\| > 5 | 1 | yes |
| 9 | Sprint lead | 2.2 (a guess) | 1 (`sprintLookAhead`) | stub | yes |
| 10 | Smoothing structure | one ease | two eases: target, then camera | two | yes |
| 11 | Horizontal ease time | 0.07 | 0.2 (target, `dampTimeNormal`) then 0.075 (camera, `dampTime`) | same | yes |
| 12 | Vertical ease time, normal | 0.18 | 0.2 then 0.075 | same | yes |
| 13 | Vertical ease time, rising | 0.25 | no special case for a normal jump (only super-jump / rising look-ahead) | none | yes |
| 14 | Vertical ease time, falling | 0.05 | starts at 0.075, shrinks 0.1 per second to 0.03 while falling fast | same | yes |
| 15 | Target carried by hero movement (catch-up) | none | yes | yes | yes |
| 16 | Vertical follow rule | grounded only; air dead zone 2.5 up, 0.5 down | always follows the hero's height | always | yes |
| 17 | Vertical speed cap | 60 | none | none | yes |
| 18 | Fall look-down starts above | 17 u/s | 22 u/s | 22 | yes |
| 19 | Fall look-down amount | 2 units (1 x offset unit 2) | 1 unit | 1 | yes |
| 20 | Fall look-down ramp | 4/s of 2 units = 8 u/s | 4 u/s | 4 | yes |
| 21 | Long float (umbrella) look-down | after 1.25 s, 1/s, max 3 units | after 1.25 s, 1 u/s, max 1.5 units | stub | yes |
| 22 | Hold up/down to look | none | after 0.85 s still: 6 units above/below, vertical ease 0.15 s for 0.35 s | yes, PROVISIONAL conditions | yes |
| 23 | Room limit rule | view fully inside the lock zone box | centre kept 14.6 / 8.3 from the room edge | same (smaller of that and the view's half-size), PROVISIONAL | yes |
| 24 | Room smaller than the view | centre on the room | limits invert and pin to one edge | centre on the room, PROVISIONAL | yes |
| 25 | Lock zones inside a room | `CameraLockZone` enter/exit | `CameraLockArea` and offset areas | stub | no |
| 26 | Screen shake | `CameraShake2D` | camera shake rig | stub | no |
| 27 | Post effects | none | bloom, colour correction, noise (built-in image effects) | stub (URP volume later) | yes |
| 28 | Update timing | LateUpdate, `deltaTime` | per frame, `deltaTime`, frozen at time scale 0 | LateUpdate, `deltaTime`, frozen at time scale 0 | no |
| 29 | Smoothing maths | `SmoothDamp` | `SmoothDamp` | closed-form equivalent (exact at any step rate) | no |
| 30 | Snap to the hero on entering a room | `SnapToTarget` on start or new target, no wait | `PositionToHero`: wait one physics step, then move instantly, clamped to the new room | instant (`SnapTo`), no physics-step wait (caller waits) | no |
| 31 | Hold after the snap | none | frozen 0.1 s, velocity zeroed, then follows | 0.1 s (`EntryHoldSeconds`), then follows | yes |
| 32 | Edge nudge on entry | none | at a left/right room edge facing inward: camera x = hero x +-1 (not clamped) | same (`EntryNudgeUnits`) | yes |
| 33 | Freeze while leaving a room | none | `FreezeInPlace` while fading out | `Freeze` / `Unfreeze` | yes |

**Differences between prototype and Silksong: 26 of 33 rows** (rows 1, 2, 3, 6 to 24, 27 and 31 to 33). The 7 rows that match are 4, 5, 25, 26, 28, 29, 30. GameCamera follows the Silksong column everywhere it is implemented; the prototype was not edited.

## PROVISIONAL

- **Look up/down conditions (row 22).** The 0.85 s delay, the 6 units and the 0.15 s / 0.35 s ease are from the original. "Standing still" is my reading: hero state `Grounded`, on the ground, \|vx\| < 0.01, and exactly one of up/down held. The original uses its own state flags and an alternative stick shortcut that were not traced.
- **Falling and dashing flags.** `HeroState.Falling` stands for the original's `cState.falling` and `HeroState.Dashing` for `cState.dashing`.
- **Room limit on small views (row 23).** The original's 14.6 / 8.3 are fixed numbers that fit a 16:9 view of 8.71 half-height. For a smaller camera the limit is the view's half-size if that is smaller, so a test camera can still reach the edges. For a view at least that big the original numbers apply unchanged (the view can show up to 0.9 units past the room edge, as in the original).
- **Room smaller than the view (row 24).** Centred; the original's behaviour there (inverted limits) was judged a bug, not copied.
- **Room rectangle.** `RoomBounds` is placed by hand or from code. The original derives it from the room tilemap with the origin at 0,0; whether Task 11b's room uses the same origin is for 11b to confirm.

## Stubs (each calls `Stubs.NotImplemented` once, from `GameCamera.Awake`)

- `GameCamera: sprint, wall-sprint, slide, harpoon, updraft, super-jump and rising look-ahead/offsets`
- `GameCamera: umbrella-float look-down`
- `GameCamera: lock areas (CameraLockArea) and camera offset areas`
- `GameCamera: detach / short-damp after hits and respawns, scene-transition positioning`
- `GameCamera: screen shake and post effects (bloom, colour correction, noise) as URP volume`

The animation converter's only stub is the RandomFrame/RandomLoop wrap mode (see 07-hero-animation-mapping.md).

## Replaces

`HeroTestCameraFollow` (the placeholder, still in `Hero/Testing`) is replaced by `GameCamera` when Task 11b builds the real scene. It was not edited or removed here.

## Note for Task 11b

`HeroController.Config` is not serialized and is only injected by `HeroDebugHud` in the scratch scene. The real scene needs a bootstrap that sets it from the one `HeroConfig.asset` (same reference). `HeroConfigConsistency.AssertSceneUsesProjectConfig` always hard-fails on a copy or a second config; value drift on measured fields hard-fails and on PROVISIONAL / owner-tuned fields only logs a warning.

## Entry snap (Milestone 2, Task 5)

Files: `Rooms/GameCamera.cs` (additions), `Rooms/RoomInfo.cs`. Tests: `Tests/PlayMode/GameCameraEntryTests.cs`.

In my own words, what the original does when the hero arrives in a new room (`CameraController.PositionToHero`): it waits one physics step, learns the new room size, puts the camera on the hero at once (clamped to the room), zeroes its speed, stays frozen for 0.1 s, then goes back to following. If that clamped position sits on a left or right room edge and the hero faces inward, the camera is put at the hero's x plus or minus 1 instead, so the hero is not jammed against the wall. On leaving, `FreezeInPlace` holds the camera while the screen fades.

What `GameCamera` has now:

- `Bounds` (setter) or `EnterRoom(RoomInfo)` swaps the room. Old follow speed is dropped and the follow target is re-clamped, so nothing overshoots into the new room. A null or destroyed room means no limits.
- `SnapTo(position[, facingRight])` jumps to that hero position inside the current bounds with the look-ahead already facing the hero's way, no smoothing, then holds for `GameCameraSettings.EntryHoldSeconds` (0.1). `IsHolding` / `IsPositioning` are true meanwhile; `PositionedAtHero` fires when the hold ends. The position is passed in because the hero object may not be moved yet.
- Edge nudge: `EntryNudgeUnits` (1) when at a side edge facing inward. The ordinary room clamp pulls the camera back inside as soon as following resumes (the original clamps in follow mode too).
- `Freeze()` / `Unfreeze()` hold the camera in place while leaving.
- Both numbers are in `GameCameraSettings`; HeroConfig is untouched and a test checks it has no such fields.

`RoomInfo` is a small per-room component: scene name, map zone id and name (Tut rooms: 13, "Moss Cave") and the `RoomBounds` reference. Camera numbers are not per room.

PROVISIONAL: the original decides "at the edge" with its own tests on the target position and an unread `targetDeltaX`; I use "the clamped camera x is on a side limit". Stubs: entry snap ignores lock areas and does not wait a physics step; RoomInfo look-and-feel (colours, light, audio, wind, darkness, border walls).
