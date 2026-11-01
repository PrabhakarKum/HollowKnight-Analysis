# Milestone 1: Playable Hero Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A hero built to Silksong's structure and timings that runs, jumps, dashes, wall-clings, attacks in four directions with a pogo bounce, takes damage, dies and respawns in a real Silksong room with real hero art, plus a training dummy.

**Architecture:** A new assembly `Hollowknight.Silksong` under `Assets/Silksong/`, separate from the owner's prototype. Small seams (input, data/save, stubs, animation adapter) are built first with tests. The hero is then built as focused components coordinated by `HeroController`, with every tuning value in a `HeroConfig` ScriptableObject filled from the original's values, which are recorded in study pages. A feasibility spike comes before everything and gates the real-art path.

**Tech Stack:** Unity 6000.0.84f1, URP 17.0.4, Input System 1.20, Unity Test Framework 1.6 (EditMode and PlayMode), C#.

**Spec:** `docs/superpowers/specs/2026-10-31-silksong-recreation-design.md`

## Global Constraints

- Namespace `Hollowknight.Silksong`; assembly definition `Hollowknight.Silksong`; code under `Assets/Silksong/`.
- No PlayMaker, InControl, tk2d, Steamworks, GOG Galaxy, Xbox runtime or SharpDX in the project.
- Classes are written by hand from understanding the original's behaviour, not transcribed line by line. Silksong's names are kept where they help cross-referencing.
- Every tuning number lives in `HeroConfig` and is taken from the original (source or export data), recorded in the matching study page. No unexplained magic numbers.
- Unfinished features call `Stubs.NotImplemented(feature)`: it logs once per feature per session and the feature is listed in `docs/study/STUBS.md`.
- Input: keyboard and gamepad both work, default bindings taken from the original game. No rebinding UI.
- Windows desktop, 60 fps, pixel-perfect rendering like the prototype.
- The owner's prototype (`Assets/Scripts/*`, `Assets/Scenes/SampleScene.unity`, `Assets/ScriptableObjects/Enemies/*`) is not edited. Do not overwrite the owner's tuned values: nail hitboxes, Brawler maxHealth 5000, hero hit-stop off. Float stays off. Do not run "Reset Enemy Configs" or "Build Enemy Test Arena" without asking.
- Imported Silksong material goes only under `Assets/Silksong/Imported/`, gitignored. The export at `D:\Asset HollowKnight Bundles\ExportedProject` is read-only.
- **Never commit or push unless the owner asks. No Claude co-author line.** Each task ends with a checkpoint that lists changed files and waits for the owner.
- Private repo.
- Report test results honestly, including failures.

## Review Focus

Failure modes the spec implies that a person will hit; each has a pinning test in the task that owns it.

1. Save file missing, corrupt or from a newer version must never crash and never overwrite the player's file (Task 3).
2. Opposite directions held, and focus loss with buttons held, must not leave stuck input (Task 4).
3. Movement must not change with physics rate or frame rate (Task 7).
4. Dying mid-dash, mid-attack or on a wall must reset all states on respawn: no stuck invulnerability or cling (Task 10).
5. One swing must hit a target once, and a down attack on plain ground must not pogo (Task 9).

## File Structure

```
Assets/Silksong/
  Hollowknight.Silksong.asmdef
  Core/        Stubs.cs, GlobalEnums.cs, EnumFlags.cs, ObjectPool.cs, IPoolable.cs
  Data/        PlayerData.cs, SaveSystem.cs
  Input/       InputButton.cs, IHeroInput.cs, HeroActions.cs, HeroInputActions.inputactions
  Platform/    Platform.cs
  Anim/        ISpriteAnimator.cs, UnitySpriteAnimator.cs
  Hero/        HeroConfig.cs, HeroController.cs, HeroMovement.cs, HeroDash.cs, HeroWall.cs,
               HeroAttack.cs, HeroHealth.cs
  Combat/      HitInstance.cs, IHittable.cs, HealthManager.cs, TrainingDummy.cs
  Rooms/       GameCamera.cs, RespawnMarker.cs, RoomBounds.cs
  Editor/      Tk2dAnimationConverter.cs
  Imported/    (gitignored: room scene, hero sprites/animations)
  Tests/EditMode/  Hollowknight.Silksong.Tests.EditMode.asmdef
  Tests/PlayMode/  Hollowknight.Silksong.Tests.PlayMode.asmdef, FakeHeroInput.cs
Assets/Scenes/SilksongHero.unity
docs/study/   00-spike-report.md, 01-architecture-map.md, 02-hero-movement.md, 03-data-and-saving.md,
              04-input.md, 05-combat.md, 06-damage-and-respawn.md, M1-checklist.md, STUBS.md
```

**Running tests.** Run EditMode tests with the Editor closed using
`"E:/Unity Engine/Unity 6/6000.0.84f1/Editor/Unity.exe" -batchmode -projectPath "E:\Unity Projects\Unity 2D Projects\Hollowknight-Analysis" -runTests -testPlatform EditMode -assemblyNames Hollowknight.Silksong.Tests.EditMode -testResults results.xml -logFile -`
(use `PlayMode` and `Hollowknight.Silksong.Tests.PlayMode` for PlayMode). If the Editor is open, use Window > General > Test Runner instead; the pass/fail expectations are identical.

---

### Task 1: Feasibility spike (gates everything else)

**Files:**
- Create: `docs/study/00-spike-report.md`
- Create: scratch scripts in the session scratchpad only; throwaway scene copy under `Assets/Silksong/Imported/_Spike/`

**Interfaces:**
- Produces: a go / no-go decision per check, and for check C the recipe Task 6 turns into `Tk2dAnimationConverter`.

- [ ] **Step 1: Check A, sprite links.** Sample 200 files from `ExportedProject\Assets\Sprite\*.asset`. For each, resolve the texture it references to a real file in the export. Record the resolve rate. Pass: at least 95%.
- [ ] **Step 2: Check B, script GUID reuse.** Pick the smallest scene under `ExportedProject\Assets\Scenes\` that contains a hero spawn point. Copy it and its prefab dependencies to `Imported/_Spike/`. Create one hand-written stub script whose `.meta` reuses the GUID of an original script the scene uses. Open the scene and count missing-script components for that script. Pass: zero for the reused GUID.
- [ ] **Step 3: Check C, tk2d animation conversion.** Take one hero animation (the run cycle) from the export's tk2d animation data. Build an `AnimationClip` with sprite keyframes at the source frame rate and play it on a `SpriteRenderer`. Pass: frame count and fps match the source and it loops visibly correctly.
- [ ] **Step 4: Write `docs/study/00-spike-report.md`.** One section per check with PASS/FAIL, numbers, and a go/no-go. For any FAIL, name the fallback (re-slice PNG atlases; or placeholder art in a hand-built room) and **stop for the owner's decision** before Task 11 plans around it.
- [ ] **Step 5: Checkpoint.** List changed files, show the report, wait for the owner. Do not start Task 2 until the owner has seen the report.

---

### Task 2: Scaffolding, utilities and architecture study page

**Files:**
- Create: `Assets/Silksong/Hollowknight.Silksong.asmdef`, `Assets/Silksong/Tests/EditMode/Hollowknight.Silksong.Tests.EditMode.asmdef`, `Assets/Silksong/Tests/PlayMode/Hollowknight.Silksong.Tests.PlayMode.asmdef`
- Create: `Core/GlobalEnums.cs`, `Core/EnumFlags.cs`, `Core/IPoolable.cs`, `Core/ObjectPool.cs`, `Core/Stubs.cs`
- Create: `docs/study/01-architecture-map.md`, `docs/study/STUBS.md`
- Modify: `.gitignore` (add `/Assets/Silksong/Imported/` and `/Assets/Silksong/Imported.meta`)
- Test: `Tests/EditMode/CoreTests.cs`

**Interfaces:**
- Produces:
  - `enum AttackDirection { Normal, Up, Down }`
  - `static class Stubs { static void NotImplemented(string feature); static IReadOnlyCollection<string> Reported { get; } static void ResetForTests(); }`
  - `interface IPoolable { void OnSpawn(); void OnDespawn(); }`
  - `static class ObjectPool { static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component; static void Despawn(GameObject instance); }`
  - `static class EnumFlags { static bool Has<T>(this T value, T flag) where T : Enum; }`

- [ ] **Step 1: Write `docs/study/01-architecture-map.md`.** Plain-language map of how hero, game manager, player data, input, camera and scene transitions connect in Silksong, with the seams this plan replaces. Summaries only, no source excerpts.
- [ ] **Step 2: Create the three assembly definitions.** Main assembly references `Unity.InputSystem`; the test assemblies reference the main one, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, and (PlayMode) `Unity.InputSystem.TestFramework`.
- [ ] **Step 3: Write failing tests in `CoreTests.cs`:** `Stubs_NotImplemented_LogsOncePerFeature` (call twice with "tools"; assert one log via `LogAssert` and `Reported` contains "tools" once), `ObjectPool_Despawn_ThenSpawn_ReusesInstance` (same instance returned, `OnSpawn` called twice), `EnumFlags_Has_ReturnsTrueOnlyForSetFlag`.
- [ ] **Step 4: Run EditMode tests.** Expected: FAIL, types undefined.
- [ ] **Step 5: Implement the Core types** with the signatures above. `Stubs` also appends new features to `docs/study/STUBS.md` only when run in the Editor.
- [ ] **Step 6: Run tests.** Expected: all pass.
- [ ] **Step 7: Checkpoint.** List changed files, wait for the owner.

---

### Task 3: Slim player data and saving

**Files:**
- Create: `Data/PlayerData.cs`, `Data/SaveSystem.cs`, `docs/study/03-data-and-saving.md`
- Test: `Tests/EditMode/SaveSystemTests.cs`

**Interfaces:**
- Consumes: `Stubs` (Task 2).
- Produces:
  - `[Serializable] class PlayerData { int version; int health; int maxHealth; int silk; int maxSilk; string respawnScene; string respawnMarker; static PlayerData CreateNew(); }` (new-game values come from the original and are recorded in the study page)
  - `static class SaveSystem { static string Root { get; set; } static bool TrySave(PlayerData data, int slot); static PlayerData LoadOrDefault(int slot); }`

- [ ] **Step 1: Write the study page** on what Silksong's player data holds and which fields Milestone 1 needs. Summaries only.
- [ ] **Step 2: Write failing tests** (each uses a temp `Root`):
  - `Save_ThenLoad_RoundTripsHealthAndRespawn`: set `health=3`, `respawnMarker="Bench_A"`, save slot 1, load slot 1, assert both equal.
  - `Load_MissingFile_ReturnsNewGameDefaults`: `LoadOrDefault(9).health == PlayerData.CreateNew().health`.
  - `Load_CorruptFile_ReturnsDefaultsAndKeepsBackup`: write `{not json`, load; assert defaults returned and the bad file is renamed to `*.corrupt`.
  - `Load_NewerVersion_ReturnsDefaultsWithoutOverwriting`: file with `version=999`; assert defaults returned and the original file bytes are unchanged.
- [ ] **Step 3: Run tests.** Expected: FAIL.
- [ ] **Step 4: Implement** with `JsonUtility`, one file per slot, write to a temp file then replace so a crash mid-save cannot corrupt the old save.
- [ ] **Step 5: Run tests.** Expected: pass.
- [ ] **Step 6: Checkpoint.** Wait for the owner.

---

### Task 4: Input layer

**Files:**
- Create: `Input/InputButton.cs`, `Input/IHeroInput.cs`, `Input/HeroActions.cs`, `Input/HeroInputActions.inputactions`, `docs/study/04-input.md`
- Test: `Tests/PlayMode/HeroActionsTests.cs`

**Interfaces:**
- Produces:
  - `struct InputButton { bool IsPressed; bool WasPressed; bool WasReleased; float HeldSeconds; }`
  - `interface IHeroInput { Vector2 Move { get; } InputButton Jump { get; } InputButton Dash { get; } InputButton Attack { get; } }`
  - `class HeroActions : MonoBehaviour, IHeroInput { void ReleaseAll(); }`; updates once per `Update`, and calls `ReleaseAll()` on `OnApplicationFocus(false)`.

- [ ] **Step 1: Write the study page:** the original's default keyboard and gamepad bindings for move, jump, dash and attack, and its input timing rules.
- [ ] **Step 2: Write failing tests** with `InputTestFixture`:
  - `Jump_WasPressed_TrueOnlyOnPressFrame`: press Space; after one update `WasPressed` is true; after the next it is false and `IsPressed` stays true; on release `WasReleased` is true.
  - `Move_OppositeKeysHeld_ReturnsZeroX`: hold A and D; `Move.x == 0`.
  - `Gamepad_LeftStickLeft_MovesLeft`: stick (-1,0); `Move.x < 0`.
  - `ReleaseAll_WhileJumpHeld_ClearsPressedState`: hold Space, call `ReleaseAll()`; `Jump.IsPressed == false`.
- [ ] **Step 3: Run tests.** Expected: FAIL.
- [ ] **Step 4: Implement** the actions asset with the recorded default bindings and `HeroActions` reading it.
- [ ] **Step 5: Run tests.** Expected: pass.
- [ ] **Step 6: Checkpoint.** Wait for the owner.

---

### Task 5: Platform stub and animation adapter

**Files:**
- Create: `Platform/Platform.cs`, `Anim/ISpriteAnimator.cs`, `Anim/UnitySpriteAnimator.cs`
- Test: `Tests/PlayMode/SpriteAnimatorTests.cs`

**Interfaces:**
- Produces:
  - `static class Platform { static IPlatform Current { get; } }` with `interface IPlatform { bool FetchScenesBeforeFade { get; } }` returning fixed, documented values.
  - `interface ISpriteAnimator { void Play(string clip); void PlayFromFrame(string clip, int frame); bool IsPlaying(string clip); event Action<string> Completed; event Action<string,int> FrameEvent; }`
  - `class UnitySpriteAnimator : MonoBehaviour, ISpriteAnimator` over `SpriteRenderer` + `Animator`.

- [ ] **Step 1: Write failing tests:** `Play_NonLoopingClip_RaisesCompletedOnce`, `PlayFromFrame_StartsAtGivenFrame` (assert current sprite equals the sprite of that frame), `FrameEvent_RaisedWithClipNameAndFrame`. Use a test clip generated in code.
- [ ] **Step 2: Run tests.** Expected: FAIL.
- [ ] **Step 3: Implement** both. Frame events come from `AnimationEvent`s added by the converter in Task 11, so the adapter only forwards them.
- [ ] **Step 4: Run tests.** Expected: pass.
- [ ] **Step 5: Checkpoint.** Wait for the owner.

---

### Task 6: HeroConfig, controller shell and ground movement with jump

**Files:**
- Create: `Hero/HeroConfig.cs`, `Hero/HeroController.cs`, `Hero/HeroMovement.cs`, `docs/study/02-hero-movement.md`
- Test: `Tests/PlayMode/FakeHeroInput.cs`, `Tests/PlayMode/HeroMovementTests.cs`

**Interfaces:**
- Consumes: `IHeroInput`, `InputButton` (Task 4), `ISpriteAnimator` (Task 5), `Stubs` (Task 2).
- Produces:
  - `ScriptableObject HeroConfig` with float fields `RunSpeed, JumpSpeed, JumpHoldMaxSeconds, Gravity, FallSpeedMax, CoyoteSeconds, JumpBufferSeconds, DashSpeed, DashSeconds, DashCooldownSeconds, WallSlideSpeed, WallJumpSpeedX, WallJumpSpeedY, WallJumpLockSeconds, AttackActiveSeconds, AttackCooldownSeconds, PogoBounceSpeed, HurtInvulnerableSeconds, HurtKnockbackX, HurtKnockbackY, HurtStunSeconds`.
  - `class HeroController : MonoBehaviour { IHeroInput Input {get;set;} HeroConfig Config {get;set;} HeroState State {get;} Vector2 Velocity {get;} bool IsGrounded {get;} bool FacingRight {get;} event Action Landed; }`
  - `enum HeroState { Grounded, Rising, Falling, Dashing, WallClinging, Attacking, Hurt, Dead }`
  - `FakeHeroInput : IHeroInput` with setters for tests.

- [ ] **Step 1: Write the study page** `02-hero-movement.md`: for run, jump (variable height), coyote time, jump buffering, gravity and fall cap, the original's behaviour and its values. List every `HeroConfig` field with its original value and where it came from (file or export asset). Nothing is left as a guess; unreadable values are flagged and the owner is asked.
- [ ] **Step 2: Create `HeroConfig` asset** at `Assets/Silksong/Hero/HeroConfig.asset` filled from the study page.
- [ ] **Step 3: Write failing tests** against a minimal ground-and-ledge test scene built in code:
  - `Run_HoldingRight_ReachesConfigRunSpeed`.
  - `Jump_HeldFullDuration_ReachesHigherApexThanTap`.
  - `Jump_Released_CutsRisingVelocity`.
  - `JumpBuffer_PressedWithinConfigWindowBeforeLanding_JumpsOnLanding` and `_JustOutsideWindow_DoesNotJump`.
  - `Coyote_JumpWithinConfigWindowAfterWalkingOffLedge_Jumps` and `_AfterWindow_DoesNotJump`.
  - `Apex_IsSameAcrossPhysicsRates` (Review Focus 3): run with `Time.fixedDeltaTime` 0.02 and 0.01; apex heights agree within 5%.
- [ ] **Step 4: Run tests.** Expected: FAIL.
- [ ] **Step 5: Implement `HeroController` (state, config, input injection) and `HeroMovement`** in `FixedUpdate`, reading only `HeroConfig` values.
- [ ] **Step 6: Run tests.** Expected: pass.
- [ ] **Step 7: Checkpoint.** The owner tries it in a scratch scene and judges the feel. Wait.

---

### Task 7: Dash

**Files:**
- Create: `Hero/HeroDash.cs`
- Modify: `Hero/HeroController.cs` (state hook), `docs/study/02-hero-movement.md` (dash section)
- Test: `Tests/PlayMode/HeroDashTests.cs`

**Interfaces:**
- Consumes: `HeroController.State`, `HeroConfig.DashSpeed/DashSeconds/DashCooldownSeconds`.
- Produces: `class HeroDash : MonoBehaviour { bool CanAirDash {get;} event Action Dashed; }`

- [ ] **Step 1: Extend the study page** with the original's dash rules (direction, air-dash allowance, cooldown, gravity during dash).
- [ ] **Step 2: Write failing tests:**
  - `Dash_Ground_TravelsDashSpeedTimesDashSecondsAndEntersDashingState`.
  - `Dash_DuringCooldown_IsIgnored`.
  - `AirDash_OnlyOncePerAirtime_RefreshedOnLanding`.
- [ ] **Step 3: Run tests.** Expected: FAIL.
- [ ] **Step 4: Implement `HeroDash`** and integrate with the controller's state machine.
- [ ] **Step 5: Run tests.** Expected: pass.
- [ ] **Step 6: Checkpoint.** Owner tests the feel. Wait.

---

### Task 8: Wall cling and wall jump

**Files:**
- Create: `Hero/HeroWall.cs`
- Modify: `Hero/HeroController.cs`, `docs/study/02-hero-movement.md` (wall section)
- Test: `Tests/PlayMode/HeroWallTests.cs`

**Interfaces:**
- Consumes: `HeroConfig.WallSlideSpeed/WallJumpSpeedX/WallJumpSpeedY/WallJumpLockSeconds`.
- Produces: `class HeroWall : MonoBehaviour { bool IsTouchingWall {get;} }`

- [ ] **Step 1: Extend the study page** with wall detection, cling conditions, and wall jump push and input lock.
- [ ] **Step 2: Write failing tests:**
  - `WallCling_PressingTowardWallWhileFalling_CapsFallSpeedAtWallSlideSpeed`.
  - `WallJump_PushesAwayFromWall_AndIgnoresHorizontalInputForLockSeconds`.
  - `WallCling_NotPressingTowardWall_DoesNotCling`.
- [ ] **Step 3: Run tests.** Expected: FAIL.
- [ ] **Step 4: Implement `HeroWall`.**
- [ ] **Step 5: Run tests.** Expected: pass.
- [ ] **Step 6: Checkpoint.** Owner tests the feel. Wait.

---

### Task 9: Combat, needle attack, pogo and training dummy

**Files:**
- Create: `Combat/HitInstance.cs`, `Combat/IHittable.cs`, `Combat/HealthManager.cs`, `Combat/TrainingDummy.cs`, `Hero/HeroAttack.cs`, `docs/study/05-combat.md`
- Test: `Tests/PlayMode/HeroAttackTests.cs`

**Interfaces:**
- Consumes: `AttackDirection` (Task 2), `HeroController`, `HeroConfig.AttackActiveSeconds/AttackCooldownSeconds/PogoBounceSpeed`.
- Produces:
  - `struct HitInstance { int Damage; AttackDirection Direction; GameObject Source; }`
  - `interface IHittable { bool TakeHit(HitInstance hit); }` (returns whether the hit landed)
  - `class HealthManager : MonoBehaviour, IHittable { int Health; event Action<HitInstance> Hit; event Action Died; }`
  - `class TrainingDummy : MonoBehaviour { int HitCount {get;} }`
  - `class HeroAttack : MonoBehaviour { event Action<AttackDirection> Attacked; }`

- [ ] **Step 1: Write the study page** on how Silksong swings the needle (directions, hitbox timing, cooldown, damage handoff via a hit instance, pogo rule). Summaries only. Do not touch the prototype's nail hitbox values.
- [ ] **Step 2: Write failing tests:**
  - `Attack_FourDirections_PlaceHitboxOnMatchingSide` (left, right, up, down).
  - `Attack_OneSwing_HitsDummyExactlyOnce` (Review Focus 5).
  - `Attack_DuringCooldown_IsIgnored`.
  - `DownAttack_HittingDummy_SetsVelocityYToPogoBounceSpeed`.
  - `DownAttack_HittingPlainGround_DoesNotPogo` (Review Focus 5).
  - `TrainingDummy_Hit_IncrementsHitCountAndRaisesHit`.
- [ ] **Step 3: Run tests.** Expected: FAIL.
- [ ] **Step 4: Implement** the types above. The slash hitbox is a trigger collider enabled for `AttackActiveSeconds`; track already-hit targets per swing.
- [ ] **Step 5: Run tests.** Expected: pass.
- [ ] **Step 6: Checkpoint.** Owner tests the feel. Wait.

---

### Task 10: Damage, death and respawn

**Files:**
- Create: `Hero/HeroHealth.cs`, `Rooms/RespawnMarker.cs`, `docs/study/06-damage-and-respawn.md`
- Modify: `Hero/HeroController.cs`
- Test: `Tests/PlayMode/HeroHealthTests.cs`

**Interfaces:**
- Consumes: `PlayerData`, `SaveSystem` (Task 3), `HitInstance`, `IHittable`, `HeroConfig.Hurt*`.
- Produces: `class HeroHealth : MonoBehaviour, IHittable { event Action Damaged; event Action Died; event Action Respawned; void RespawnAt(RespawnMarker marker); }` and `class RespawnMarker : MonoBehaviour { string MarkerName {get;} }`.

- [ ] **Step 1: Write the study page** on hurt knockback, invulnerability, death flow and respawn, with the original's values.
- [ ] **Step 2: Write failing tests:**
  - `Damage_ReducesHealthByAmount_AndStartsInvulnerability`.
  - `Damage_DuringInvulnerability_IsIgnored`.
  - `Damage_AppliesKnockbackAndStunsForConfigSeconds`.
  - `Death_WhileDashing_ClearsDashAndStateOnRespawn` (Review Focus 4).
  - `Death_WhileClingingToWall_ClearsClingOnRespawn` (Review Focus 4).
  - `Respawn_RestoresHealthAtMarker_AndClearsInvulnerability`.
  - `Respawn_AfterDeathMidAttack_AllowsAttackAgainImmediately` (Review Focus 4).
- [ ] **Step 3: Run tests.** Expected: FAIL.
- [ ] **Step 4: Implement `HeroHealth` and `RespawnMarker`;** store the marker name in `PlayerData` and save.
- [ ] **Step 5: Run tests.** Expected: pass.
- [ ] **Step 6: Checkpoint.** Owner tests the feel. Wait.

---

### Task 11: Real room, real hero art and camera

**Files:**
- Create: `Editor/Tk2dAnimationConverter.cs`, `Rooms/GameCamera.cs`, `Rooms/RoomBounds.cs`, `Assets/Scenes/SilksongHero.unity`
- Create under gitignored `Assets/Silksong/Imported/`: the chosen room scene and its dependencies, hero sprites and converted animation clips
- Test: `Tests/PlayMode/RoomSceneTests.cs`

**Interfaces:**
- Consumes: the spike report's recipes (Task 1), `ISpriteAnimator` (Task 5), `HeroController` and `HeroHealth`, `RespawnMarker`.
- Produces:
  - `static class Tk2dAnimationConverter { static AnimationClip Convert(string exportedAnimationPath, string outputFolder); }` (Editor-only)
  - `class GameCamera : MonoBehaviour { Transform Target {get;set;} RoomBounds Bounds {get;set;} }`

**This task is written for a passing spike.** If Task 1 recorded a FAIL, stop and replan with the owner's chosen fallback.

- [ ] **Step 1: Write failing tests:**
  - `SilksongHeroScene_Loads_WithNoMissingScripts` (use `GameObjectUtility.GetMonoBehavioursWithMissingScriptCount` across the scene).
  - `Hero_SpawnsAtRespawnMarker`.
  - `Camera_FollowsHero_AndStaysInsideRoomBounds`.
  - `HeroAnimations_AllNeededClipsPresent_ForRunJumpDashWallAttackHurtDeath`.
- [ ] **Step 2: Run tests.** Expected: FAIL.
- [ ] **Step 3: Implement `Tk2dAnimationConverter`** from the spike recipe, with fps, frame order and frame events kept from the source.
- [ ] **Step 4: Import the room and hero art** into `Imported/` a piece at a time (only what the room and hero need), map the ported components onto room objects, and build `SilksongHero.unity` with the hero, the training dummy and a respawn marker.
- [ ] **Step 5: Implement `GameCamera` and `RoomBounds`** with URP only (no built-in image effects).
- [ ] **Step 6: Run tests.** Expected: pass.
- [ ] **Step 7: Checkpoint.** The owner opens `SilksongHero.unity` and looks. Wait.

---

### Task 12: Milestone 1 acceptance

**Files:**
- Create: `docs/study/M1-checklist.md`

- [ ] **Step 1: Write `M1-checklist.md`:** one line per exit-bar item (run; variable jump; dash; wall cling and wall jump; needle left, right, up, down; pogo; take damage; die; respawn; dummy reacts), each with how to perform it and the result that counts as a pass.
- [ ] **Step 2: Run the whole test suite** (EditMode and PlayMode). Expected: all pass; report any failure as it is.
- [ ] **Step 3: Owner runs the checklist** in `SilksongHero.unity` and plays side by side with the real game for feel, noting anything that differs.
- [ ] **Step 4: Fix what the owner reports,** re-run tests, repeat until the owner signs off.
- [ ] **Step 5: Checkpoint.** Milestone 1 is done when the owner signs off. Ask whether to commit. Write the plan for Milestone 2 (room transitions) next.
