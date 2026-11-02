# 10 - Room transitions (Milestone 2 study)

Plain-language summary of how Silksong moves the hero from one room to the next, written so a plan for
"Milestone 2: room transitions between a few rooms" can be made. Everything below is described in my own words
from reading the decompiled source; line numbers point into
`D:\Asset HollowKnight\HollowKnightSilksong\Assets\Scripts\Assembly-CSharp` (written `src/`) and the exported
scenes in `D:\Asset HollowKnight Bundles\ExportedProject\Assets\Scenes\Hornet` (written `scenes/`).

## The big picture in one paragraph

Each room is its own Unity scene. The hero, the camera, the game manager and the screen fader live outside the rooms
and survive every change. When the hero walks into an invisible box at a room edge (a **transition point**, also
called a gate), the game: freezes the hero's input and makes the hero keep walking out of the room, fades the
screen to black, loads the next room's scene in the background, switches to it, throws away the old room, finds
the matching gate in the new room by name, puts the hero there, fades back in, makes the hero walk a few steps
inwards, and only then gives the controls back. A gate is just a named object: the sender stores "go to scene X,
arrive at gate Y". Gate Y is simply an object in room X with that name.

## Words used here

| Word | Meaning |
|---|---|
| Gate / transition point | A trigger box at a room edge (or a door). Component `TransitionPoint`. |
| Gate position | top, right, left, bottom, door. Taken from the gate **object's name**, not a setting. |
| Entry gate | The gate in the new room where the hero appears. Stored on the sender as `entryPoint` (the task text calls it `entryGate`). |
| Hazard respawn | Where the hero reappears after touching spikes (not a death). |
| Bench respawn | Where the hero reappears after dying. Saved as scene + marker name. |

## 1. TransitionPoint (the door/gate trigger)

`src/TransitionPoint.cs` (611 lines). **What it does:** sits on a trigger collider; when the hero touches it,
starts a scene change; also is the arrival spot for the other side.

Key settings (lines 43-89):

- `targetScene` (line 65): name of the room to load. `entryPoint` (line 67): name of the gate object in that
  room to appear at. Both must be filled or nothing happens (line ~283).
- `entryOffset` (line 72): shift added to the arrival spot. Example from `Tut_01b`: its `right1` gate has offset
  (-1, 0) so the hero lands one unit inside the next room.
- `entryDelay` (line 51): extra seconds to wait **before walking in at this gate** (when this gate is the arrival
  gate, not the sender). All Tut gates use 0.
- `alwaysEnterRight` / `alwaysEnterLeft` (lines 53-55): force the hero's facing when arriving at a bottom gate or a
  door. For left/right/top gates the hero entry code ignores them (HeroController 6625-6830), so they are inert
  there. (`Tut_01 right2` has `alwaysEnterLeft` set; it does nothing.)
- `isADoor`, `dontWalkOutOfDoor`, `IsOverHero`, `hardLandOnExit`: door behaviour (press up to enter, a door
  animation, walk out of the door) and "land hard when arriving from a top gate".
- `nonHazardGate` + `respawnMarker` (lines 87-89): which hazard-respawn spot is set when the hero arrives here.
  If not set, `Start()` (line ~176) looks for a `HazardRespawnMarker` child of the gate.
- Audio snapshots, cosmetics, `customFadeFSM`, `additionalFadeFSM`, `cutsceneFsmHolder`, `customEntryFSM`: fade
  and music mood hooks. Optional.

How it triggers (lines 194-292, `TryDoTransition`):

1. Only objects on layer 9 (the hero) count (lines 198, 206). The check runs on enter and on stay.
2. Ignored while `IsTransitionBlocked`, or unless the game state is PLAYING. In ENTERING_LEVEL it is allowed only
   for a bottom gate when the hero is moving down (so you can leave straight back out of a bottom gate).
3. For left/right gates the hero must be **facing outward** (a hero walking backwards while sprinting counts as
   the reverse). Otherwise the hero is pushed back outside the box and nothing happens (lines ~250-270). Same
   idea for top/bottom with vertical push-back.
4. Hero binding, recoiling, or in a cutscene move is also pushed back.
5. Otherwise: mark activated, run the fade-out hooks, and `DoSceneTransition` (line 307).

`DoSceneTransition` builds a `SceneLoadInfo` (scene name, entry gate name, leave direction = this gate's position,
entry delay, whether to fade), tells the hero `IgnoreInput`, then calls `GameManager.BeginSceneTransition`.
It also checks the target gate exists in a build-time list (`SceneTeleportMap`, lines 294-345); if not it logs an
error and sends the hero back to the **same** scene/gate. We should copy that safety idea (see Open questions).

Gate position from name (line 422): name contains "top", "right", "left", "bot", or "door" (or `isADoor`).
So gate names such as `left1`, `right2`, `top1`, `bot1`, `door1` matter. Numbers are just to tell gates apart.

Door-type gates (`EnterDoorSequence`, line 524): hero interacts, loses control, plays an enter animation, screen
fades, 0.5 s pause, then the same scene change. **Not needed for M2** (stub).

Dependencies: base class `InteractableBase` (vendor-free but part of the interact system), PlayMaker
(`customFadeFSM`, `customEntryFSM`, `cutsceneFsmHolder`, and `OnSceneLintUpgrade` still migrates an old
"Door Control" FSM), Addressables (to pre-fetch the target scene), audio mixer snapshots, `tk2dSpriteAnimator` for
the door animation, `SceneTeleportMap` (editor-built list of gates).

**Rebuild by hand** (`Hollowknight.Silksong`, `Rooms/`): `TransitionPoint` (trigger box + the fields
`targetScene`, `entryGate`, `entryOffset`, `entryDelay`, `alwaysEnterRight/Left`, `nonHazardGate`,
`respawnMarker`, `hardLandOnExit`) with `GatePosition GetGatePosition()` from the name, and a pure helper
`TransitionRules.ShouldLeave(gatePos, heroFacingRight, gameState, ...)` so the facing-outward rule is unit tested.
Add `Core/GatePosition.cs`, `Core/GameState.cs` (the originals are in `src/GlobalEnums/GatePosition.cs`,
`GameState.cs`: top/right/left/bottom/door/unknown; INACTIVE, MAIN_MENU, LOADING, ENTERING_LEVEL, PLAYING,
PAUSED, EXITING_LEVEL, CUTSCENE, PRIMER). A static `GateRegistry` replaces `TransitionPoint.TransitionPoints`
(line 133, a static list the game manager searches by name).
**Stub:** doors (`Stubs.NotImplemented("TransitionPoint.Door")`), audio snapshots, custom fade/entry/cutscene
hooks, Addressables pre-fetch, `hardLandOnExit` (hero has no hard landing), `SceneTeleportMap` (replace by a simple
"does the target scene/gate exist" check at build time or on load).

## 2. GameManager scene-change flow

`src/GameManager.cs` is 5393 lines; the part we need is small.

`BeginSceneTransition(info)` (line 615) → coroutine `BeginSceneTransitionRoutine` (line 626). In order:

1. Ignore the request if one is already running (line 629).
2. Reset "camera positioned at hero" (633), set `IsInSceneTransition` (634).
3. Create a `SceneLoad` (637) - the object that loads the scene.
4. Tell the hero it is leaving: `LeavingScene()` (646) and un-parent it (647). The hero is marked
   `DontDestroyOnLoad` inside `HeroController.LeaveScene` (line 6926-6930), which is why it survives.
5. `SaveLevelState()` (line 3793, fires an event so persistent items write their state into `SceneData`).
6. Set state EXITING_LEVEL (660). Remember `entryGateName` and `targetScene` (662-663).
7. `hero_ctrl.LeaveScene(info.HeroLeaveDirection)` (666): hero walks out (see section 3).
8. Unless `PreventCameraFadeOut`: freeze the camera in place and send the Screen Fader the event
   "SCENE FADE OUT" (670-673). The fade is a PlayMaker FSM called "Screen Fader" (found in `SetupGameRefs`, line
   ~3314) - **we cannot reuse it**.
9. A timer of **0.34 s** (`cameraFadeTimer`, line ~700) must run out before the new scene may activate, so the
   screen is dark first. Platform "fetch before fade" lets the load start earlier.
10. `sceneLoad.Begin()` starts loading (`src/SceneLoad.cs`): load the target scene **additive** (not replacing)
    with `activateOnLoad: false` (line 183/187), wait until allowed, activate (235). Then the old scene is
    unloaded (`ActivationCompleteAction`, line ~756), garbage collected, tile map info refreshed
    (`RefreshTilemapInfo`, line 3749).
11. `CompleteAction` → `SetupSceneRefs` + `BeginScene` (line 3460: initialises input, UI, hero scene state), and if
    the new room is in another map zone `EnteredNewMapZone` fires.
12. `FinishAction` → `OnNextLevelReady` (line 2979). That sets state ENTERING_LEVEL, calls
    `heroController.RegainControl(false)` and `EnterHero()` (3189).

`EnterHero` (3189-3260): if the hero is respawning after death use the bench marker; hazard respawn uses the saved
location; else (normal door/gate entry) `FindTransitionPoint(entryGateName)` (3170) - first gate whose **name
equals** the stored name, falling back to the first gate in the scene with a warning - then starts the coroutine
`hero_ctrl.EnterScene(gate, entryDelay, ...)`. After the hero finishes entering, `FinishedEnteringScene()` (3282)
sets state PLAYING and sends "hero entered scene".

Screen fade in: `FadeSceneIn()` (line 2301) sends "SCENE FADE IN" to the Screen Fader FSM; the hero's entry code
calls it once the camera is in place.

**Hero input lock while moving:** `TransitionPoint.DoSceneTransition` calls `hero.IgnoreInput()` (HeroController
8009: sets `acceptingInput=false` and zeroes held inputs). `AcceptInput()` (8026) only works when the hero is in
position and not mid-transition. Pause is disabled while entering (`playerData.disablePause`).

**Respawn data:** `PlayerData.respawnScene` + `respawnMarkerName` + `respawnType` (PlayerData.cs lines 175-185)
are the death/bench spot; `SetBenchRespawn(marker, sceneName, type)` (3748) also stores `mapZone`.
`hazardRespawnLocation` (206) is a plain world position that is only valid in the current room.
`GameManager.SetCurrentMapZoneAsRespawn` (2488) copies the room's `mapZone` into `PlayerData.mapZone`.
`mapZone` itself is a setting on the room's `CustomSceneManager` (section 4); `Tut_*` rooms use value 13
(`MOSS_CAVE`).

Dependencies: PlayMaker (Screen Fader, inventory FSM, many `FSMUtility` calls), Addressables, Platform,
audio, UI, achievements, map. **Almost all of the 5393 lines are unrelated** - do not port the class.

**Rebuild by hand:** `Rooms/RoomTransitionManager` (a small slice of GameManager): holds `GameState`, the current
request (`TransitionRequest { targetScene, entryGate, leaveDirection, entryDelay }`), runs the sequence in a
coroutine against three small interfaces so it can be tested without loading scenes:
`IRoomLoader` (load target hidden, activate, unload old), `ITransitionHero` (leave / enter / lock), `IScreenFader`
(fade out / in). Add `Core/ScreenFader` (a black full-screen UI image, fade by alpha).
**Stub:** achievements, inventory cancel, audio fades, map update, map-zone events, memory/GC phases, loading
visualisations, `SceneLoadInfo` extras (`EntrySkip`, `ForceWaitFetch`, ...), first-level/new-game path.

## 3. Hero side: HeroController leave and enter

`src/HeroController.cs`.

**Leaving** - `LeaveScene(gate)` (6926): marks the hero not in position, damage mode `NO_DAMAGE` (hero cannot be
hurt while leaving), state `no_input`, transition state `EXITING_SCENE`, and sets a walking-out velocity:
right gate → +run speed, left gate → -run speed (if already faster, keeps the faster speed), top → upward
`MIN_JUMP_SPEED`, bottom → zero. In `FixedUpdate` (lines ~2993-3025) the velocity is applied each step while the
state is `no_input` and `cState.transitioning`. Also `RecordLeaveSceneCState` (11798) remembers if the hero was
dashing/sprinting/super-dashing so it can re-enter in that state (we will skip that).

**Entering** - `EnterScene(gate, delay, ...)` (6625-6790), a coroutine:

1. Stand-still setup: cancel effects, reset motion, no gravity, state `no_input`, transition state
   `WAITING_TO_ENTER_LEVEL`, `cState.transitioning = true`. Gate position is read from the gate's name.
2. **Left/right gates** (`EnterHeroSubHorizontal`, 6796-6886): hero is placed at gate.x + entryOffset.x, with y found
   by looking for ground near the gate (`FindGroundPointY`), facing away from the gate (arriving at a left gate →
   faces right). The sprite is hidden until the fade starts. Then `EnterHeroSubFadeUp` (6888):
   wait **0.165 s**, wait while the camera is still being positioned, call `FadeSceneIn`, then wait
   `delayBeforeEnter` (the sender's `entryDelay`) and the arrival gate's own `entryDelay`. Next the hero walks
   inward at **run speed for 0.33 s** (`ENTERING_SCENE`) and `FinishedEnteringScene`.
3. **Top gates** (6649-6699): positioned at the gate, fade up, then forced downward velocity
   `SPEED_TO_ENTER_SCENE_DOWN` (-12, from the Hero_Hornet prefab) with gravity on for 0.33 s; optional hard
   landing.
4. **Bottom gates** (6700-6740): positioned 3 units **above** the gate, thrown sideways/up
   (`SPEED_TO_ENTER_SCENE_HOR` 8, `SPEED_TO_ENTER_SCENE_UP` 9.4) for `TIME_TO_ENTER_SCENE_BOT` 0.1 s, then falls.
5. **Doors**: stand on the ground at the door, play "Exit Door To Idle", then finish.

`FinishedEnteringScene` (9109-9175) hands control back: `cState.transitioning=false`, transition state
`WAITING_TO_TRANSITION`, gravity on, **hazard respawn set** to the gate's `respawnMarker` unless `nonHazardGate`
(or to the hero's own position if the game started in this room), damage mode `FULL_DAMAGE`, `AcceptInput()`,
tells GameManager, 0.5 s delay before interacting again.

Invulnerability: it is not a timed buff; the hero is simply in `NO_DAMAGE` damage mode from `LeaveScene` until
`FinishedEnteringScene` sets `FULL_DAMAGE`; the damage functions (11035-11071) also refuse hits unless the
transition state is `WAITING_TO_TRANSITION`.

Dependencies: tk2d animator (`animCtrl`), PlayMaker (`proxyFSM`, `sprintFSM`, `vignetteFSM`, "ROAR EXIT"),
audio, many status systems. **Rebuild by hand:** `Hero/HeroTransitionDriver` (or a partial of `HeroController`),
implementing `ITransitionHero`: `LeaveRoom(GatePosition)`, `EnterRoom(TransitionPoint gate, float delay)`
coroutine, `ControlLocked`, an `IsInvulnerable` hook into `HeroHealth`, facing, scripted velocity for
`EXITING_SCENE` / `ENTERING_SCENE`, ground snap (raycast down, same as the existing respawn code). The existing
`HeroController` has no input-lock or scripted-move API; M2 must add a minimal one (see risks).
New constants go in `HeroConfig` (frozen contract? - **open question**: new fields must be added by whoever owns
`HeroConfig`; the plan must schedule this): `EnterSceneWait 0.165`, `EnterWalkSeconds 0.33`, `EnterBottomSeconds 0.1`,
`EnterHorSpeed 8`, `EnterUpSpeed 9.4`, `EnterDownSpeed -12`.
**Stub:** door animations/walking sound, dash/quake/sprint re-entry, `hardLandOnExit`, "dream gate", respawn
cinematics.

## 4. Scene-level data (what the room itself carries)

### 4a. CustomSceneManager (the "SceneManager" component, `src/CustomSceneManager.cs`, 808 lines)

One per room. Holds: scene type (gameplay or not), `mapZone`, `environmentType`, darkness, wind, colour grading
curves, ambient light, hero light, particles, audio snapshots, music cue, world rumble, border walls. In
`Start` (line 302) it applies those settings. Most is look-and-feel. Of use to M2: `mapZone` and `sceneType`
(the Tut rooms use mapZone 13, sceneType 0 = gameplay, borders mask -1 = all four sides). The border walls
(`SceneBorder` objects; code at lines 680-704) are solid boxes placed around the room using
`gm.sceneWidth/Height` so the hero cannot leave except via a gate.
Dependencies: PlayMaker ("Darkness Control"), audio mixer, URP grading, tk2d tilemap size.
**Rebuild:** `Rooms/RoomInfo` (MonoBehaviour per room: `SceneName` (from the scene), `MapZone` enum or string,
`RoomBounds` reference). **Stub:** colours, light, particles, audio, wind, darkness, border walls (rooms are
walled by geometry we build).

### 4b. SceneData persistent bools and ints (`src/SceneData.cs`, 294 lines; `PersistentBoolItem.cs`)

A dictionary `scene name → item id → value` (breakable already broken, lever pulled, pickup taken). A
`PersistentBoolItem` on an object stores its state when `SaveLevelState` fires at the start of a transition and
restores it when the room loads again. Some entries are "semi-persistent" and reset on bench rest. Saved with
the game.
**Rebuild (small):** `Rooms/SceneData` with `GetBool/SetBool/GetInt/SetInt(scene, id)`, in memory, plus
`Rooms/PersistentBool` component; keep save-file wiring as a stub (`Stubs.NotImplemented("SceneData.Save")`),
unless the owner wants room state to survive quitting (open question). Not required to pass M2 if we have no
breakables in the rooms.

### 4c. Hazard respawn objects

`HazardRespawnMarker` (`src/HazardRespawnMarker.cs`, 50 lines): an empty marker; on Awake it raycasts down to
find ground. `HazardRespawnTrigger` (`src/HazardRespawnTrigger.cs`, 55 lines): a trigger box; when the hero (layer 9)
touches it, `PlayerData.SetHazardRespawn(marker)` (PlayerData 3778) stores the position + facing. A gate's
hazard marker is stored when the hero arrives (section 3). Rooms use many of these (10 markers, 7 triggers in
`Tut_01b`). In M2 these let a spike hit put the hero back **inside the same room**. The existing
`Rooms/HazardZone.cs` and `Rooms/RespawnMarker.cs` (M1) cover spikes and bench respawn; we add
`HazardRespawnMarker` + `HazardRespawnTrigger` next to them. Death respawn across rooms (hero dies in room B,
bench was in room A) needs the transition manager to load `respawnScene` and use the marker name instead of a gate
(section 2 `EnterHero`, `RespawningHero` branch).

## 5. Camera on entry

`src/CameraController.cs` (1207 lines), `src/GameCameras.cs` (363), `src/CameraLockArea.cs` (332).

- On leaving: `FreezeInPlace` (line 421) holds the camera while fading out.
- When the hero is placed in the new room, `SendHeroInPosition` (HeroController 1774) raises the `heroInPosition`
  event; the camera listens (line 224) and runs `PositionToHero` (837-980): waits one physics step, reads the new
  room size, moves the camera **instantly** to the hero (clamped to the room bounds and any camera lock area),
  zeroes its velocity, stays frozen 0.1 s, then resumes following. If the hero stands at a horizontal room edge
  and faces **inward**, the camera is shifted one unit toward the room (lines ~895-925) so the view is not
  jammed against the wall.
- The fade-in waits for the camera to be positioned (EnterHeroSubFadeUp loop on `IsPositioningCamera`).
- Room bounds: the original reads the size of the room's `tk2dTileMap` (`GameManager.RefreshTilemapInfo`, line
  3749), origin at (0,0), and keeps the camera centre 14.6 units from left/right and 8.3 from bottom/top
  (`KeepWithinSceneBounds`, line 677; `xLimit/yLimit`, 822-823). Real Tut rooms: `Tut_01` 120x120,
  `Tut_01b` 160x99, `Tut_02` 150x59 tiles (note gates are not at the tile map edges: `Tut_01b` gates are at
  x=58 and x=150.5, so the playable part is a sub-rectangle).
- Lock areas: `CameraLockArea` is a trigger box with `cameraXMin/YMin/XMax/YMax` (-1 = unset), priority,
  look-up/down limits. While the hero is inside, the camera may not leave those limits; when the hero enters from
  a side it records which side. Real rooms have 13-23 of them each. Needs the tilemap to exist first (line ~110).

Already in the repo: `Rooms/GameCamera.cs` (follow, look-ahead, `SnapToTarget`, `Bounds`) and `Rooms/RoomBounds.cs`
(a rectangle placed by hand, with the same 14.6/8.3 insets). M2 needs: `GameCamera.Bounds` swapped per room,
`SnapToTarget` called when the hero is in position, a hold (freeze) while fading. (The original also has an "inward edge nudge", but reading the source shows it only runs with lock-area offsets, so it was not built; see 08-camera.md.)
**Stub:** camera lock areas in the first pass (`Rooms/CameraLockZone` later), shake, FOV offsets, forced aspect.
Note the owner prototype has `Assets/Scripts/Camera/CameraLockZone.cs`; do not touch it.

## 6. How rooms are authored in the exported scenes

Findings from `scenes/Tut_01.unity`, `Tut_01b.unity`, `Tut_02.unity` (the first three rooms of the game;
`mapZone 13`, "Moss Cave"):

| Scene | File size | GameObjects | tk2d tile map | Camera lock areas | PlayMaker FSMs | Gates |
|---|---|---|---|---|---|---|
| Tut_01 | 21,165 KB | 3,552 | 120 x 120 | 18 | 30 | 6 |
| Tut_01b | 13,938 KB | 2,137 | 160 x 99 | 13 | 15 | 3 |
| Tut_02 | 22,368 KB | 3,051 | 150 x 59 | 23 | 39 | 2 |

Gates (name at position → leads to `targetScene` / `entryPoint`, from the TransitionPoint component):

| Scene | Gate (position x,y) | Goes to | Arrives at |
|---|---|---|---|
| Tut_01 | right1 (80.5, 91) | Tut_01b | left1 |
| Tut_01 | right2 (80.5, 38) | Tut_01b | left2 |
| Tut_01 | left2 (-0.5, 33) | Tut_02 | right1 |
| Tut_01 | left3 (-0.5, 14) | Tut_02 | right2 |
| Tut_01 | left1 (child object) | Tut_03 | right1 |
| Tut_01 | top1 (49, 120.5, offset 0,2) | Bonetown | bot2 |
| Tut_01b | left1 (58, 91) | Tut_01 | right1 |
| Tut_01b | left2 (58, 38) | Tut_01 | right2 |
| Tut_01b | right1 (150.5, 63, offset -1,0) | Weave_04 | left1 |
| Tut_02 | right1 (150.5, 33) | Tut_01 | left2 |
| Tut_02 | right2 (150.5, 12) | Tut_01 | left3 |

So **Tut_01 ↔ Tut_01b** is joined by two gate pairs (right1/left1 and right2/left2) and **Tut_01 ↔ Tut_02** by
two pairs (left2/right1 and left3/right2). Gate objects are on layer 13; every link goes both ways, which makes
a good test: leave through one, return through the other.
Exits that would lead outside the M2 set (`Tut_03`, `Bonetown`, `Weave_04`) should be disabled or stubbed
(`Stubs.NotImplemented("Transition to Tut_03")`) rather than imported.

How a room is built: one root object per art piece (about 540 root objects in `Tut_01b`); a `tk2dTileMap` object
(size only used for bounds); ground/wall colliders as plain 2D colliders on separate objects (e.g. "Roof Collider",
"Spike Collider"); hundreds of decorative sprites (moss, grass, fog, black fades); gates, camera lock areas,
hazard respawn markers and triggers, enemies with PlayMaker, breakables, `SceneBorder` walls, a `_SceneManager`
object with `CustomSceneManager`. Most weight is art and moss/grass scripts.

Dependencies to import, if the real rooms are used: `tk2dSprite`, `tk2dSpriteAnimator`, `tk2dTileMap`
(vendor tk2d, the highest counts: 34-81 sprites per room), `PlayMakerFSM` and the PlayMaker trigger helpers,
`CameraLockArea`, `MossClump`, `GrassCut/GrassBehaviour`, `Breakable`, `HealthManager` and enemy effect scripts, `SetZ*`,
`NonBouncer`, `DamageHero`, audio components, shaders/materials, plus 20+ MB of YAML each. This is why a first M2
should use **grey-box rooms** with the same gate layout (and optionally copy only the ground colliders by an
editor script) instead of importing the real scenes. Real rooms are a later "dress the rooms" step.

Smaller real rooms exist (Bone_17 1.6 MB, Bone_East_21 1.1 MB, ...), but they are dead ends with one gate each, so they
cannot form an adjacent trio; `Tut_01 / Tut_01b / Tut_02` is the smallest connected trio with several gates.
Alternative smaller chain: `Weave_13` (2.9 MB, 508 objects; left1→Weave_02/right2, right1→Weave_08/left1), but its
neighbours were not checked for size.

## 7. Summary: rebuild / stub / open questions

| Piece | Rebuild (namespace `Hollowknight.Silksong`) | Stub | Open question for the owner |
|---|---|---|---|
| TransitionPoint | `Rooms/TransitionPoint`, `Rooms/GateRegistry`, `Core/GatePosition` | doors, audio, FSM hooks, hard landing | Keep the original field name `entryPoint` or use `entryGate` (my pick: `entryGate`)? |
| Scene change | `Rooms/RoomTransitionManager`, `Rooms/IRoomLoader`, `Rooms/AdditiveRoomLoader`, `Core/GameState`, `Core/ScreenFader` | achievements, GC phases, visualisations | Rooms as separate Unity scenes (like the original) or as prefabs in one scene (simpler, no ProjectSettings change)? |
| Hero enter/leave | `Hero/HeroTransitionDriver`, `HeroController` input lock hook | dash/sprint re-entry, door anims | OK to touch `HeroController` and add fields to `HeroConfig` in M2? |
| Room data | `Rooms/RoomInfo` (map zone, bounds), `Rooms/SceneData` | colours, light, audio, particles | Do pickups/breakables need to stay broken after leaving a room in M2? |
| Hazard respawn | `Rooms/HazardRespawnMarker`, `Rooms/HazardRespawnTrigger`, `PlayerData` fields | respawn cinematics | Is the hazard spot kept in the save file? (original: yes, `hazardRespawnLocation`) |
| Camera | `GameCamera` per-room `Bounds`, snap-on-entry, freeze while fading | lock areas (later `Rooms/CameraLockZone`), shake | Are lock areas needed for the grey-box rooms or only when real rooms arrive? |
| Rooms | grey-box builder for Tut_01, Tut_01b, Tut_02 with the gate names above | everything outside those three rooms | Grey-box rooms, or import real geometry? Which rooms (my pick: Tut_01, Tut_01b, Tut_02)? |

Other open questions: fade durations (the original's fade lengths live inside the PlayMaker Screen Fader FSM; I
only know the code-side 0.34 s wait, 0.165 s hero wait, 0.33 s walk-in; the fade itself should be tuned by eye);
whether a missing target gate should fall back like the original (first gate in the room, with a warning) or be a
hard test failure; where scenes are registered (Unity needs rooms in Build Settings, a ProjectSettings file the
builder agent may not edit - tests must load scenes by path with the editor loader, and the owner adds them for
real builds).

## 8. Recommended task breakdown (TDD, like Tasks 7-12 of the M1 plan)

Each task: write failing tests → run → implement → run → checkpoint. Pure logic is tested in EditMode, the rest in
PlayMode with a fake loader, so tests need no real room scenes until Task 7.

1. **Gate data and rules** (`Core/GatePosition.cs`, `Core/GameState.cs`, `Rooms/TransitionPoint.cs`,
   `Rooms/GateRegistry.cs`, `Rooms/TransitionRules.cs`). Tests: gate position from name (`left1`, `bot2`,
   `door1`), must face outward to trigger, pushed back otherwise, ignored unless PLAYING, lookup by name with fallback
   to first gate + warning. Define the three interfaces (`IRoomLoader`, `ITransitionHero`, `IScreenFader`).
   *Must go first.*
2. **Persistent scene data and respawn data** (`Rooms/SceneData.cs`, `Rooms/PersistentBool.cs`, `PlayerData` fields
   for map zone and hazard respawn, save round trip). *Parallel with 3, 4, 5 after Task 1.* Touches `PlayerData` and
   `SaveSystem` (version bump decision).
3. **Transition manager** (`Rooms/RoomTransitionManager.cs`, `Core/ScreenFader.cs`) driven by fakes: state sequence
   PLAYING → EXITING_LEVEL → ENTERING_LEVEL → PLAYING, 0.34 s fade wait, ignores a second request, input locked
   from first touch until finished, missing gate handled. *Parallel with 2, 4, 5.*
4. **Hero leave and enter** (`Hero/HeroTransitionDriver.cs`, small input-lock hook in `HeroController`, new
   `HeroConfig` fields). Tests: leave right → hero velocity +run speed and cannot be hurt; enter at a left gate →
   placed at gate + offset on the ground, faces right, walks 0.33 s, control returns after, invulnerable until then;
   top and bottom variants. *Parallel with 2, 3, 5; the only task that edits `HeroController`.*
5. **Camera on entry and room bounds** (`Rooms/GameCamera` snap-and-hold, `Rooms/RoomInfo.cs`, bounds swap).
   Tests: camera jumps to the hero inside new bounds, stays frozen 0.1 s. (The original edge nudge is dead without lock areas; see 08-camera.md.)
   *Parallel with 2, 3, 4.*
6. **Real room loading** (`Rooms/AdditiveRoomLoader.cs`, persistent bootstrap holding hero, camera, manager and
   fader). PlayMode test with two tiny test scenes: leave A, arrive in B at the named gate, A is gone, hero object is
   the same instance. *Needs 1, 3, 4, 5.*
7. **Grey-box rooms** (`Editor/TransitionRoomsBuilder.cs`, menu "Silksong > Build Transition Rooms", gitignored
   scenes for Tut_01, Tut_01b, Tut_02 using the gate table above, `docs/study/` owner steps page). The gate data
   table and room sizes can be written early; the builder needs Task 1. *Can start in parallel with 2-5 after Task 1,
   finishes after 6.*
8. **Hazard and death respawn across rooms** (`Rooms/HazardRespawnMarker`, `HazardRespawnTrigger`, manager loads
   `respawnScene` and the saved marker). *Needs 2, 3, 6.*
9. **Milestone 2 acceptance** (`docs/study/M2-checklist.md`, a PlayMode walk through all gate pairs
   Tut_01 ↔ Tut_01b ↔ Tut_01 ↔ Tut_02, owner checkpoint). *Last.*

Parallel groups after Task 1: {2, 3, 4, 5, 7-data}. Then 6, then 8, then 9.

## 9. Top risks

1. The hero has no input-lock or scripted-move API; adding one edits `HeroController` (shared with other hero
   work) and the new constants need `HeroConfig` fields (frozen contract question).
2. Real scenes are 11-22 MB and tk2d/PlayMaker-heavy; importing them would blow up M2. Grey-box first.
3. Scenes must be in Build Settings for loading by name; that file is in ProjectSettings, which agents may not edit.
4. Fade timing and hero walk-in feel depend on a PlayMaker Screen Fader we cannot copy; tune by eye with the owner.
5. Persistent objects (hero, camera, manager) surviving scene changes can leak or duplicate across tests and
   on re-entering a scene; needs careful bootstrap and teardown.
