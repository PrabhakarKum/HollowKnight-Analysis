# 07 - Hero animation mapping (Silksong Knight library to hero code)

Source library: `ExportedProject\Assets\Animations\Knight.prefab` (the hero's tk2d animation library, 500 named clips), sprites from `Collections\Knight Data\Knight.prefab` (1,828 sprite definitions, atlases `atlas0..3.png`, 4096 x 4096). Clip names below were read from the export, and the "how the original uses it" column from the original's `HeroAnimationController`.

The importer is `Silksong/Import Hero Animations` (menu) or, in batch mode,
`Unity -batchmode -projectPath <project> -executeMethod Hollowknight.Silksong.Editor.HeroAnimationImporter.ImportBatch -quit`.
It reads the library from `D:\Asset HollowKnight Bundles\ExportedProject\Assets\Animations\Knight.prefab` (override with the environment variable `SILKSONG_KNIGHT_ANIMATION_LIBRARY`) and writes clips and sprites to `Assets/Silksong/Imported/HeroAnimations` (gitignored). Each clip is saved under the **hero code's name**, because the hero asks `ISpriteAnimator` for names like `Jump` that the original does not have. The table is in code as `HeroAnimationMap.Entries`, and a test checks it against the names hero code plays.

## The mapping

"Hero plays" means the current hero code calls `Animator.Play(name)` for it. fps and frame counts are from the export.

| Hero name | Hero plays | Knight clip | Frames | fps | tk2d wrap mode | Unity result | Frame events |
|---|---|---|---|---|---|---|---|
| Idle | yes | `Idle` | 6 | 12 | Loop | loops | none |
| Run | yes | `Run` | 10 | 15 | Loop | loops | `Footstep` on frames 3 and 8 |
| Jump | yes | `Airborne` | 15 | 16 | LoopSection, loop from frame 12 | `Jump` once, then chains into `Jump Loop` (frames 12 to 14 looping) | none |
| Fall | yes | `Fall` | 7 | 15 | LoopSection, loop from frame 4 | `Fall` once, `Fall Loop` frames 4 to 6 | none |
| Dash | yes | `Dash` | 9 | 16 | Once | plays once | `Footstep` on frame 3 |
| WallCling | yes | `Wall Cling` | 8 | 14 | LoopSection, loop from frame 3 | once + `WallCling Loop` | none |
| Slash | yes | `Slash` | 5 | 20 | Once | once | none |
| SlashUp | yes | `UpSlash` | 7 | 24 | Once | once | none |
| SlashDown | yes | `DownSpike` | 4 | 25 | LoopSection, loop from frame 2 | once + `SlashDown Loop` | none |
| Hurt | yes | `Recoil` | 9 | 24.2424 | Once | once | none |
| Death | yes | `Death` | 16 | 12 | LoopSection, loop from frame 5 | once, holds the last frame + `Death Loop` | none |
| Land | yes (on touchdown, standing still) | `Land` | 14 | 24 | Once | once | none |
| Turn | yes (grounded facing reversal) | `Turn` | 6 | 18 | Once | once | one on frame 1 (no name), `Footstep` on frame 5 |
| WallSlide | no (later) | `Wall Slide` | 9 | 12 | LoopSection, loop from frame 6 | once + `WallSlide Loop` | none |
| WallJump | no (later) | `Walljump` | 14 | 15 | Once | once | none |
| SlashAlt | no (later) | `SlashAlt` | 5 | 20 | Once | once | none |

## Needed clips with no clip of that name

Every clip the plan lists exists in the library under a different name. None is missing. The ones to know about:

- **Jump** has no clip. `Airborne` is the original's jump-and-air clip: it starts at frame 0 on takeoff and the original jumps to frames 3, 7, 8 or 9 for falling states.
- **Hurt** has no clip. The original plays `Recoil` when the hero is hit (`Idle Hurt` is the wounded idle, not the hit reaction).
- **SlashDown** has no clip of that name. The default crest's downward attack plays `DownSpike`. A separate `DownSlash` and `DownSlashAlt` exist in the original for other crests and are not in this library as hero clips (`DownSlashEffect` is the slash effect sprite, not the hero body).
- **WallCling** and **WallJump** are `Wall Cling` and `Walljump`.
- **Death** is `Death`; `Death Final`, `Acid Death` and `Spike Death` are variants not used yet.
- The original has a second swing for each direction (`SlashAlt`, `UpSlashAlt`, `DownSlashAlt`). Only `SlashAlt` is imported (for later); the up and down alternates are not in this library under those names.
- `Wall Slash` is the attack clip while clinging. It is not imported yet (the current wall code plays `Slash`).

## Differences and things to know (PROVISIONAL where marked)

1. **LoopSection is chained by `UnitySpriteAnimator`.** Unity has one loop flag per clip, so the converter writes the whole sequence as a non-looping intro clip (`<name>`) and the loop part as a looping clip `<name> Loop`. When the intro ends the animator continues straight into `<name> Loop` and `Completed` is NOT raised for the intro; `Completed` is raised only when a non-looping clip ends with no `<name> Loop` registered. While either part is current, `IsPlaying(name)` is true and repeated `Play(name)` is ignored (hero code calls it every step); `Play` of a clip without a loop part still restarts as before. `Death` and `SlashDown` follow their source: the loop part keeps looping. No stub is involved.
2. **Frame events.** `AnimationEvent` is `OnFrameEvent`, `intParameter` = frame index, `stringParameter` = the tk2d `eventInfo`, `floatParameter` = `eventFloat`. The tk2d `eventInt` is dropped (one int per event, and it carries the frame index).
3. **Wrap modes.** Loop, Once, Single map directly. PingPong is unrolled forward then back without repeating the end frames and loops (event indexes are timeline positions). RandomFrame and RandomLoop (4, 5) have no Unity equivalent: they play as a plain loop and call `Stubs.NotImplemented("tk2d RandomFrame/RandomLoop wrap modes (played as a plain loop)")`. None of the hero clips above uses them.
4. **Attack animation speed.** The original scales the attack clip by a speed multiplier so it fits the attack time. `ISpriteAnimator` has no speed setting. The `Slash` clip is 0.25 s long and `HeroConfig.AttackActiveSeconds` is 0.35 s. Not changed here (hero code and `HeroConfig` are frozen for this task).
5. **Dash length.** The `Dash` clip is 0.5625 s; the dash itself is 0.1 s (`DashSeconds`). The clip keeps playing after the dash ends. The original hands off to `Dash To Idle`; that clip is not imported. PROVISIONAL.
6. **Falling.** The original does not use `Fall` for an ordinary fall; it plays `Airborne` from frame 7. This hero code plays `Fall`, so `Fall` is the nearest named clip. A more faithful route is `PlayFromFrame("Jump", 7)` in hero code. PROVISIONAL, for the owner to decide.
7. **Sprite scale.** Sprites are imported at 64 pixels per unit with the pivot on the tk2d origin, and used at scale 1. See `08-camera.md`, "Units and scale".

## Sprites

Each distinct sprite is cut from its atlas once into `Imported/HeroAnimations/Sprites/<sprite name>.png` and re-used by every clip that points at it. Flipped sprites (stored turned 90 degrees) are turned back upright when cut. The pure rules (rectangle, rotation, pivot, pixels per unit, frame timeline and event planning) are in `Editor/Tk2dAnimationData.cs` and are unit tested with fixtures taken from the spike's Run data; the asset-writing part is `Editor/Tk2dAnimationConverter.cs`.

`Tk2dAnimationConverter.Convert(path, outputFolder)` takes the library file with an optional clip name after `#` (for example `...\Knight.prefab#Run`). A library with more than one clip needs the name.

## Duplicate clip names

The Knight library holds 534 clips but only 501 distinct names. When a name appears more than once, the **first clip in file order** wins (`Tk2dAnimationData.FindClip`, tested). None of the 15 mapped names is duplicated (checked), so the rule does not change this table.

## Landing, and the other state changes (task 13)

**What the hero does now.** When `Landed` is raised and the hero is standing still, `Land` plays once. The per-step Idle request is held back while `Land` is playing (`ISpriteAnimator.IsPlaying`), and Idle follows when it finishes. No clip length is hard-coded. Running, jumping or falling, dashing, a swing, being hurt or dying cut it at once; `Run` is never blocked. Landing while a direction is held goes straight to `Run`. A hero that starts on the floor raises no `Landed` (the existing rule), and a respawn is a fresh start too: respawning resets that rule, so a respawn onto the floor raises no `Landed` and plays no `Land`, however many falls came before. A respawn in mid-air is a real fall and lands normally.

**What the original does (read from its `HeroAnimationController`).** It also plays `Land` on airborne-to-idle (`Land Q`, a quieter version, when hurt or enraged) and holds Idle back while `Land` or any of a list of "settle" clips runs; when the clip ends it plays Idle. It differs in four ways: (1) landing while running plays `Land To Run`, not `Run` straight away; (2) a landing during a swing plays `Slash Land` / `Slash Land Run`; (3) a hard landing after a long fall is a separate state and clip, `HardLand`; (4) `Land To Run` is also used when the player starts running during `Land`. None of these clips is imported; the hero uses `Run` for (1) and (4), and has no hard-landing state, so (3) is not built. PROVISIONAL.

**Other abrupt cuts that exist today (listed, not built).** The original has a transition clip for each: Idle to Run = `Idle To Run` (and `Idle To Run Short`, `Idle To Run Weak`); Run to Idle = `Run To Idle` (`Walk To Idle`); Dash to Idle = `Dash To Idle`, Dash to Run = `Dash To Run`; Sprint to Run = `Sprint To Run`; Slash to Run = `Slash To Run`; Fall = `Airborne` from frame 7, not a separate `Fall`; Fall to Land = `Land`. The hero code switches straight between the plain clips for all of them (item 5 above for dash). Also, the hero's movement step used to replace a swing's clip with Idle/Run/Fall on the step the swing started; it now leaves the `Slash*` clips alone while a swing is active.

## Turn (reversing direction on the ground)

**What it does.** When the hero is standing or running on the floor and its facing reverses, the `Turn` clip plays once (6 frames at 18 fps). It is only decoration: the facing, the hitbox and the movement change on the very same step, and nothing waits for the clip. Run speed is unchanged.

**When it plays.** On a grounded reversal, unless the hero was clinging to a wall on the step before, is under a scripted move (room-transition walk), or is hurt or dead. A reversal made in the air, during a dash, a swing or a wall jump plays nothing, and a facing change made during one of those is not "remembered" and played later on the ground. Spawning or respawning facing left is not a turn either. The original also refuses while its get-up-from-the-ground and hazard-respawn clips play; those are not built here.

**While it runs.** The per-step `Idle` and `Run` requests are held back while our own `Turn` is playing (the original does the same through `IsPlayingTurn`). Anything else cuts it at once: jumping or falling, a dash, a swing, a hit, death. When it ends the normal `Idle` or `Run` follows. If an animator has no `Turn` clip (art imported before this change, or a test animator) nothing special happens: the request is ignored and `Idle`/`Run` carry on as before.

**The flip.** In the original, a facing change flips the sprite's scale immediately (`FlipSprite`) and then asks for the clip, so the clip is drawn on the already-flipped object and its first frames are the old pose seen from the new side. We do the same: `HeroArtFlip` / `HeroSceneBootstrap.LateUpdate` mirror the sprite on the step facing changes, and `Turn` just plays. I could not look at the art itself, so whether the first frame reads correctly after the mirror is for the owner to judge in play. PROVISIONAL.

**Skipped on purpose.** `TurnWalk` is the same turn inside a "walk zone" (slow-walk areas); we have none, so it is not imported and never requested. `TurnToIdle`, a settle clip after the turn, is also skipped. The first `Turn` request reports both through `Stubs.NotImplemented("Hero TurnWalk and TurnToIdle ...")`, so they appear in `STUBS.md`.

**What the owner must do.** Run `Silksong > Import Hero Animations` once. `Turn` is now on the "played by hero code" list, so the scene builders treat a missing `Turn.anim` the same as any other missing clip until it is imported.

Code: `Hero/HeroTurnRules.cs` (the pure decision, unit tested), `Hero/HeroMovement.cs` (tracks facing and holds Idle/Run), tests in `HeroTurnRulesTests` and `HeroTurnAnimationTests`.
