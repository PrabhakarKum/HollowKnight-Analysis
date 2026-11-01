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
| Jump | yes | `Airborne` | 15 | 16 | LoopSection, loop from frame 12 | `Jump` plays once and holds frame 14; `Jump Loop` holds frames 12 to 14 looping | none |
| Fall | yes | `Fall` | 7 | 15 | LoopSection, loop from frame 4 | `Fall` once, `Fall Loop` frames 4 to 6 | none |
| Dash | yes | `Dash` | 9 | 16 | Once | plays once | `Footstep` on frame 3 |
| WallCling | yes | `Wall Cling` | 8 | 14 | LoopSection, loop from frame 3 | once + `WallCling Loop` | none |
| Slash | yes | `Slash` | 5 | 20 | Once | once | none |
| SlashUp | yes | `UpSlash` | 7 | 24 | Once | once | none |
| SlashDown | yes | `DownSpike` | 4 | 25 | LoopSection, loop from frame 2 | once + `SlashDown Loop` | none |
| Hurt | yes | `Recoil` | 9 | 24.2424 | Once | once | none |
| Death | yes | `Death` | 16 | 12 | LoopSection, loop from frame 5 | once, holds the last frame + `Death Loop` | none |
| Land | no (later) | `Land` | 14 | 24 | Once | once | none |
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

1. **LoopSection is not chained.** Unity has one loop flag per clip and no loop section. The converter writes the whole sequence as a non-looping clip and the loop part as a second looping clip named `<name> Loop`. `UnitySpriteAnimator` does not switch from one to the other, so these clips play once and hold the last frame (visually fine for `Jump`, `Fall`, `WallCling`, `Death`, `SlashDown`). It also raises `Completed` for them when the intro ends. Stub: `tk2d LoopSection chaining (intro clip, then the 'Loop' clip)`. PROVISIONAL: nothing in hero code listens for `Completed` of these clips yet.
2. **Frame events.** `AnimationEvent` is `OnFrameEvent`, `intParameter` = frame index, `stringParameter` = the tk2d `eventInfo`, `floatParameter` = `eventFloat`. The tk2d `eventInt` is dropped (one int per event, and it carries the frame index).
3. **Wrap modes.** Loop, Once, Single map directly. PingPong is unrolled forward then back without repeating the end frames and loops (event indexes are timeline positions). RandomFrame and RandomLoop (4, 5) have no Unity equivalent: they play as a plain loop and call `Stubs.NotImplemented("tk2d RandomFrame/RandomLoop wrap modes (played as a plain loop)")`. None of the hero clips above uses them.
4. **Attack animation speed.** The original scales the attack clip by a speed multiplier so it fits the attack time. `ISpriteAnimator` has no speed setting. The `Slash` clip is 0.25 s long and `HeroConfig.AttackActiveSeconds` is 0.35 s. Not changed here (hero code and `HeroConfig` are frozen for this task).
5. **Dash length.** The `Dash` clip is 0.5625 s; the dash itself is 0.1 s (`DashSeconds`). The clip keeps playing after the dash ends. The original hands off to `Dash To Idle`; that clip is not imported. PROVISIONAL.
6. **Falling.** The original does not use `Fall` for an ordinary fall; it plays `Airborne` from frame 7. This hero code plays `Fall`, so `Fall` is the nearest named clip. A more faithful route is `PlayFromFrame("Jump", 7)` in hero code. PROVISIONAL, for the owner to decide.
7. **Sprite scale.** Sprites are imported at 64 pixels per unit with the pivot on the tk2d origin, and used at scale 1. See `08-camera.md`, "Units and scale".

## Sprites

Each distinct sprite is cut from its atlas once into `Imported/HeroAnimations/Sprites/<sprite name>.png` and re-used by every clip that points at it. Flipped sprites (stored turned 90 degrees) are turned back upright when cut. The pure rules (rectangle, rotation, pivot, pixels per unit, frame timeline and event planning) are in `Editor/Tk2dAnimationData.cs` and are unit tested with fixtures taken from the spike's Run data; the asset-writing part is `Editor/Tk2dAnimationConverter.cs`.

`Tk2dAnimationConverter.Convert(path, outputFolder)` takes the library file with an optional clip name after `#` (for example `...\Knight.prefab#Run`). A library with more than one clip needs the name.
