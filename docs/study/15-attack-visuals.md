# 15 - Attack visuals (how Silksong shows the hero's swing)

Study for `docs/superpowers/plans/2026-11-04-attack-visuals.md`, Task 1. Read only. Nothing in the game changed.

Sources (all read only):

- Decompile, `D:\Asset HollowKnight\HollowKnightSilksong\Assets\Scripts\Assembly-CSharp`: `NailSlash.cs`, `NailAttackBase.cs`, `NailSlashTravel.cs`, `NailSlashRecoil.cs`, `HeroExtraNailSlash.cs`, `HeroDownAttack.cs`, `Downspike.cs`, `HeroSlashBounceConfig.cs`, `HeroControllerConfig.cs`, `HeroAnimationController.cs`, `HeroController.cs`.
- Export, `D:\Asset HollowKnight Bundles\ExportedProject\Assets`: `Prefabs\Heroes\Hero_Hornet.prefab` (the `Attacks` group), `Animations\Knight.prefab` (clips), `Collections\Knight Data\Knight.prefab` (sprite definitions), `Data Assets\HeroController Configs\*.asset`.
- Our code: `Assets/Silksong/Hero/HeroAttack.cs`, `Anim/ISpriteAnimator.cs`, `Anim/UnitySpriteAnimator.cs`, `Editor/HeroAnimationImporter.cs`, `docs/study/07-hero-animation-mapping.md`, `docs/study/05-combat.md`.

Line numbers below are for the decompile files named. Numbers from the export were read straight from the YAML.

"Default crest" below means the group `Hero_Hornet/Attacks/Default` and the config `Data Assets\HeroController Configs\Default.asset` (no animation override library, `downSlashType` 0 = downspike). That the default (Hunter) crest uses exactly this pair is my reading of the data, not stated anywhere (PROVISIONAL, but it matches the numbers in study 05).

## 0. The short version

1. The slash effect is **a separate child object per swing type** (`Slash`, `AltSlash`, `UpSlash`, `DownSlash` (the downspike), `WallSlash`) under `Hero_Hornet/Attacks/Default`. Each has its own sprite renderer, its own animator, its own polygon hitbox and an `NailSlash` (or `Downspike`) component. All five are always in the hierarchy; their renderer and collider are off until a swing starts (`NailSlash.cs:60-64`).
2. The effect object's position is **(0, 0) on the hero root** for side and up swings. The offset is baked into the sprite art (the art sits left of the origin by up to 3.3 units). Only the downspike and the wall slash have a position offset. All five are mirrored by the hero's own scale flip.
3. **The original does not stretch the body clip to fit the attack time.** The body clip plays at its own frame rate (multiplied by 1.5 only when the "quickening" buff is on). `Slash` is 0.25 s, `AttackDuration` is 0.35 s, cooldown 0.41 s. So the plan's "speed so the swing fits the attack time" is not what the original does (section 2). This needs the owner's call.
4. The original's damage window is **not** the whole 0.35 s. The effect clip itself switches the hitbox on and off with frame events: 0.1 s for a side swing, 0.083 s for an up swing. Ours is on for 0.35 s. The hitboxes stay as they are (frozen), but the owner should know.
5. Alternate swing: toggles on every sideways swing, and resets to the first swing when more than **0.65 s** (`AttackRecoveryTime` 0.15 + `ALT_ATTACK_RESET` 0.5) passed since the last swing started, or when the swing direction changed. The default crest has **no** alternate up swing and **no** alternate down attack.
6. All effect clips are in the Knight library we already import from, use the Knight sprite collection we already cut sprites from. The importer only needs new rows.

## 1. How a swing spawns and plays the slash effect

### What happens, in order

`HeroController.Attack` (`HeroController.cs:4052`) picks the slash object from the direction and the alternate flag, sets the damage direction (0 / 180 / 90 / 270) and calls `SlashComponent.StartSlash()` (`:4185`). Then, in `NailSlash.StartSlash` (`NailSlash.cs:76-100`):

1. `OnSlashStarting` (`NailAttackBase.cs:127-156`) sets the object's local scale to its serialized `scale` (`:154`; the Long Needle tool scales it up, skip).
2. The attack sound plays.
3. The polygon collider is switched **off** (`:91`). The animation turns it on later (see below).
4. `PlaySlash` (`NailSlash.cs:121-133`): the mesh renderer is turned on (`:125`), the clip named `animName` starts with `anim.Play(clip, Mathf.Epsilon, clip.fps * speedMult)` (`:131`; start time is a hair above zero, speed multiplier is 1 except quickening). Two callbacks are hooked: frame events and clip completed.
5. `OnPlaySlash` (`NailAttackBase.cs:211`) starts the damager (`damager.StartDamage()`), and spawns the imbued-nail effect if the nail is imbued (skip).

### The five effect objects of the default crest

All under `Hero_Hornet/Attacks/Default` (identity transform; `Attacks` is a child of the hero root at identity, so every number is relative to the hero root). All have z = -0.001 (drawn just in front of the body, which is at z 0 on the root), sorting layer 0, sorting order 0 (same as the body renderer). Component: `NailSlash` except the down one (`Downspike` + `HeroDownAttack`). Source: `Hero_Hornet.prefab`.

| Object | `animName` (Knight library clip) | Local position | Local rotation | Local scale (as authored, hero facing left = hero scale +1) |
|---|---|---|---|---|
| `Slash` | `SlashEffect` | (0, 0) | 0 | (0.9, 1) |
| `AltSlash` | `SlashEffectAlt` | (0, 0) | 0 | (0.9, 1) |
| `UpSlash` | `UpSlashEffect` | (0, 0) | 0 | (1, 0.9) |
| `DownSlash` (a `Downspike`) | `DownSlashEffect` | (0.69, -0.37) | 0 | (1.2, 1.2) (z 1.33, ignore) |
| `WallSlash` | `SlashEffect` | (0.61, 0) | 0 | (-0.9, 1) |

(`NailSlash.scale` equals the transform scale in every row, so the scale set at `StartSlash` is the authored one.)

### Facing

The art faces **left** natively and the hero root's scale flips it: `Downspike.cs:84` reads `heroCtrl.transform.localScale.x < 0` as "facing right"; `HeroController.cs:4216` uses scale.x > 0 as "facing left". Our project already treats the art as left-facing (`artFacesLeft` true in `HeroArtFlip`, `HeroSceneBootstrap`). The effect object is a child of the hero root, so it flips with the root: the effect on the other side is the same object mirrored, and a position offset flips sign. Proof from the data: the original polygon on `Slash` reaches x from +0.57 to -3.36 (to the left), and the `WallSlash` offset (+0.61) and negative x scale only make sense if "hero faces left toward a wall on the left, swing goes right". On a wall the hero faces the wall and the swing goes away from it (`HeroController.cs:4098-4103`: `facingRight` gives damage direction 180).

**Rule for our mirror** (hero art is not a parent with flipped scale; `HeroArtFlip` flips the body by `flipX`): for a sideways swing going to the right, set the effect `flipX = true` (or scale.x negative); for one going to the left, native. Offsets (wall 0.61, down 0.69) take the sign of the swing/facing side. `Up` and `Down` flip with the hero's facing, not a swing side (the art is not symmetric; the up sprites sit at x from -0.88 to +1.78).

### Where the sprites are drawn (so pivot and offset are not needed in code)

The tk2d sprite definitions already contain the offset: the `Slash` effect's sprites span x -3.27 .. 2.0, y -0.98 .. 1.28 around the origin (sprite `Hornet_slashes_effects0000`, Knight collection id 737). Sprites are imported with the pivot at the tk2d origin at 64 pixels per unit (study 07), so an effect object at the hero root with the position from the table above lands in the same place as in the original. With the 0.9 x scale the visible swipe reaches about 2.9 units out in front.

### Clips (all in `Animations\Knight.prefab`, the library we import from; sprites in `Collections\Knight Data\Knight.prefab`)

fps, frames and "events" are tk2d `triggerEvent` frames (zero-based). Wrap mode 2 = Once.

| Clip | Frames | fps | Length | Wrap | Frame events | Notes |
|---|---|---|---|---|---|---|
| `SlashEffect` (line 378) | 4 (ids 737, 829, 906, 733) | 20 | 0.200 s | Once | frame 0, frame 2 | frame 3 is a 1-pixel empty sprite |
| `SlashEffectAlt` (407) | 4 (731, 732, 853, 827) | 20 | 0.200 s | Once | frame 0, frame 2 | frame 3 empty |
| `UpSlashEffect` (784) | 6 (717, 717, 719, 721, 716, 722) | 24 | 0.250 s | Once | frame 1, frame 3 | frames 0, 1, 4, 5 are empty; only frames 2 and 3 are visible (0.083 s to 0.167 s) |
| `DownSlashEffect` (825) | 5 (1319, 1321, 1325, 1326, 1320) | 33.33 | 0.150 s | Once | none | frame 4 empty; `loopStart` 1 is stored but ignored by Once |
| `SlashEffect_Glow`, `SlashEffectAlt_Glow`, `UpSlashEffect_Glow`, `DownSlashEffect_Glow` | 4 to 6 | 20 to 33 | | Once | none | nail-imbuement glow, a different sprite collection (`Knight Slash Cln`). Skip. |

### Lifetime and when it is hidden

- Shown: the moment `StartSlash` runs (`NailSlash.cs:125`).
- Hidden when the clip completes: `OnAnimationCompleted` calls `CancelAttack()` which turns the mesh off (`:186-190`, `:102-119`). So lifetime = clip length: 0.2 s (side, alt, wall), 0.25 s (up).
- Hidden early when `CancelAttack(forceHide: true)` runs: hero flipped mid swing (`:192-198`, only possible if `CanTurnWhileSlashing`, which is off for the default crest), a travel/impact cancel (Shaman only).
- NOT hidden when the whole attack is cancelled by `HeroController.CancelAttack(...)` unless the bounce config says so: that path calls `CancelAttack(forceHide: false)` (`HeroController.cs:9838-9843`), and the mesh is only turned off if `bounceConfig.HideSlashOnBounceCancel` (`NailSlash.cs:111`; the field defaults to true in `HeroSlashBounceConfig.cs`; I did not check which config asset each default-crest object references, PROVISIONAL). For the default crest the side/up swipes are 0.2 s long anyway, so this only matters for the down bounce.
- The attack timer ending (`attack_time >= attackDuration`, `HeroController.cs:2034`) does not hide the effect. The effect clip (0.2 s) is already over by then (0.35 s).

### Relation to the hitbox (we do not change it)

The effect object **is** the hitbox: the polygon collider is on the same object, in the effect's local space (so it is also scaled 0.9 and mirrored). The frame events of the effect clip turn it on and off (`NailSlash.cs:135-172`): the first event enables the collider (`:151-159`), the second disables it and ends the damage (`:161-171`). A frame event named `Bounce` is the downspike pogo hook (`:137-148`), none of the default clips has one.

- Side / alt / wall: collider on from frame 0 (t = 0) to frame 2 (t = 0.1 s). 
- Up: on from frame 1 (t = 0.042 s) to frame 3 (t = 0.125 s).
- The polygon of the side `Slash` (`Hero_Hornet.prefab`, collider on `Slash`): points x from -3.36 to +0.57, y from -1.42 to +1.20 (hero-root space, facing left).

Compare ours (`HeroConfig`, hand-tuned, unchanged): side box 1.1 x 1.2 centred 0.8 in front and 0.15 up, so it reaches 1.35 in front; and it is active for `AttackActiveSeconds` 0.35 s. **The drawn swipe will reach about 2.9 units; the hit reaches 1.35.** And the original window (0.1 s) is a third of ours. This is a feel gap to show the owner, not something Task 3 should fix (see open question 2).

### Not relevant to the look

`NailSlashRecoil` is the hit recoil of the hero (study 14e covers it). `HeroExtraNailSlash` is the nail-imbuement tint (skip). `HeroDownAttack` is the bounce trigger glue for the Slash-type down attack. `NailSlashTravel` is only on the Shaman crest (the slash flies out 4.4 units over 0.25 s; `Hero_Hornet.prefab` `Attacks/Shaman/*`), so it is out of scope.

## 2. Attack clip speed versus the real durations

### What the original really does

`HeroAnimationController` (`:581-605`) plays the attack body clip with `speedMultiplier = IsUsingQuickening ? Config.QuickAttackSpeedMult : 1f` (`:589`). `Play` (`:1455-1470`) then calls `animator.Play(clip, 0f, clip.fps * speedMultiplier)` only if the multiplier is not 1. `NailSlash.PlaySlash` (`:129-131`) does the same for the effect clip. In `HeroController.Attack`, `attackDuration` is `Config.AttackDuration` and is divided by the same multiplier only while quickening (`:4073-4076`). So:

> **speed = 1** normally, **1.5** while the Quickening buff is on (`quickAttackSpeedMult` 1.5 in Default). Nothing computes a speed from clip length and attack time.

The attack flag, the cooldown and the pose are three separate clocks.

### The real numbers (Default crest)

| Clock | Value | Where |
|---|---|---|
| `attackDuration` (the `attacking` flag, `attack_time`) | 0.35 s | `Default.asset`; `HeroController.cs:2030-2036` |
| `attackRecoveryTime` (until other actions are allowed again: dash, cast, bind, turn) | 0.15 s | `Default.asset`; checks at `HeroController.cs:3374`, `:7724` and others |
| `attackCooldownTime` (next swing allowed; never below `attackDuration`) | 0.41 s | `Default.asset`; `DidAttack` `:4284-4291` |
| Body clip `Slash` / `SlashAlt` | 5 frames, 20 fps = 0.250 s | Knight library lines 343 / 243 |
| Body clip `UpSlash` | 7 frames, 24 fps = 0.292 s | line 436 |
| Body clip `Wall Slash` | 7 frames, 24 fps = 0.292 s | line 6122 |
| Effect clip | 0.2 s (side), 0.25 s (up) | section 1 |
| Hitbox open | 0.1 s (side), 0.083 s (up) | section 1 |

Our `HeroConfig`: `AttackActiveSeconds` 0.35 (= `attackDuration`; also used by us as the hitbox time), `AttackCooldownSeconds` 0.41 (= `attackCooldownTime`). So our two timers already match the original. What does not match is only the **pose**: it ends at 0.25 s while our "attacking" runs to 0.35 s. In the original it is the same: when the body clip completes (`AnimationCompleteDelegate` cases `Slash`, `SlashAlt`, `UpSlash`, `HeroAnimationController.cs:1039-1054`) the controller sets `playSlashEnd` and plays `Slash To Run` (3 frames at 15 fps = 0.2 s, line 21924) or goes to idle/run, while `attacking` stays true until 0.35 s. So the last 0.1 s of "attacking" is already back in normal locomotion poses. That is faithful already: `HeroMovement` resumes Idle/Run once `IsPlaying("Slash")` is false.

### What the plan assumed, and the conflict

The plan and study 07 item 4 say "the original scales the attack clip so it fits the attack time". The code shows it does not. If Task 2 implements "speed = clipLength / AttackActiveSeconds" (0.25 / 0.35 = 0.714) the swing would look about 40 percent slower than Silksong's. The faithful choice is **speed 1.0 for the swing clips** and the speed parameter exists for the quickening buff later (stub). See open question 1.

### Inferred, not readable

The PlayMaker FSMs on the hero (`sprintFSM`, `crestAttacksFSM`, `slashFsm`) might touch animator speed; they cannot be read. The C# code never does.

## 3. Alternate swing

### Reset timer and toggle (`HeroController.Attack`, `:4052-4188`)

- `ALT_ATTACK_RESET` = 0.5 s (`Hero_Hornet.prefab` line 13607; field `HeroController.cs:319`).
- At the start of every `Attack`: `if (Time.timeSinceLevelLoad - altAttackTime > Config.AttackRecoveryTime + ALT_ATTACK_RESET || attackDir != prevAttackDir) cState.altAttack = false;` (`:4066-4069`). So the reset time is 0.15 + 0.5 = **0.65 s** since the last slash **started** (`altAttackTime` is set when a slash starts, `:4184`), or any change of direction from the previous attack (`prevAttackDir`, set at `:4188`).
- Sideways (`:4108-4126`): if `altAttack` is true, use the alternate slash and set it false; else use the normal slash and, **if an alternate slash object exists**, set it true. So swings go Slash, SlashAlt, Slash, SlashAlt...
- Up (`:4128-4145`): same pattern but with `altUpSlash`.
- Down downspike (`:4222-4247`): same pattern with `altDownSpike`. Down slash type Slash (`:4256-4271`): same with `altDownSlash`.
- Wall slash (`:4087-4097`): does not look at or change `altAttack`. It does set `altAttackTime`.
- The downspike does not set `altAttackTime` (`isSlashing` false), but it does set `prevAttackDir` to downward.

### Which clips

Body: `UpSlashAlt` / `DownSlashAlt` / `SlashAlt` / `Slash` (`HeroAnimationController.cs:590-605`, branch order: up, down, wall sliding, then alt-or-not sideways). Effect: the slash object chosen above.

| Direction | First swing | Alternate | Exists for the default crest? |
|---|---|---|---|
| Sideways | body `Slash`, effect `SlashEffect` on `Slash` | body `SlashAlt`, effect `SlashEffectAlt` on `AltSlash` | yes, both |
| Up | `UpSlash` + `UpSlashEffect` | `UpSlashAlt` | **no**. `Attacks/Default` has no `AltUpSlash` object, and `UpSlashAlt` is only in the Wanderer/Dagger override library (`Animations\Hornet CrestWeapon Dagger Anim.prefab`), not in `Knight.prefab`. `altAttack` is never set on an up swing for this crest. |
| Down (downspike) | `DownSpike Antic` then `DownSpike` | none | **no** alternate. `DownSlashAlt` exists only in the Dagger library (a different crest). |
| Wall | `Wall Slash` + `SlashEffect` on `WallSlash` | none | one version only |

The alternate pair for sideways is the same size and place (both objects at (0,0), scale (0.9, 1)). The two clips differ in art: `SlashEffect` first and last visible frames sit left of the origin; the Alt arc is slightly higher (sprite 731 spans y -1.31 .. 0.36, 732 y -1.27 .. 0.91).

### Edge cases to copy

- A press after more than 0.65 s: back to `Slash`.
- A different direction in between (an up swing or a downspike) resets to `Slash`.
- A wall slash in between does not reset the toggle by itself, but because a wall slash is direction `normal` and sets the timer, a stale alternate can survive it. Cheap to copy exactly: do not touch the flag in the wall path.

## 4. Downspike (the default crest's down attack)

Default config: `downSlashType` 0 (downspike), `downspikeAnticTime` 0.1, `downspikeTime` 0.1, `downspikeSpeed` 26, `downspikeRecoveryTime` 0.25, `downspikeBurstEffect` true, `downspikeThrusts` true (`Default.asset`).

### Sequence

1. Press with down held in the air: `Attack(downward)` -> `DownAttack` (`HeroController.cs:4206-4250`). No slash is started (`isSlashing = false`); `cState.attacking` is **not** set (`:4070`). It sets `downSpikeAntic = true`, zeroes the upward speed (clamped), switches gravity off, and takes control away (thrusts). Cooldown 0.41 s (`DidAttack`).
2. Antic, 0.1 s (`:2325-2342`, `downSpikeTimer >= DownSpikeAnticTime`). The hero decelerates. Body clip `DownSpike Antic`: 3 frames at 42.857 fps = 0.07 s (line 10319; last frame holds for the remaining 0.03 s) (`HeroAnimationController.cs:554-557`).
3. Thrust, `DownSpikeTime` 0.1 s (`Downspike()`, `:4346-4358`): `downSpiking = true`, velocity = (`downSpikeHorizontalSpeed`, -26) every step. `currentDownspike.StartSlash()` runs (`:2337`) -> `Downspike.StartSlash` (`Downspike.cs:82-102`): sets the extra up direction (45 if the hero faces right, 135 if left), plays the audio, plays the effect clip from frame 0 (`anim.PlayFromFrame(animName, 0)`), shows the mesh, **enables the polygon at once** (`:101`), and shrinks the hero box. The burst prefab is switched on (`downspikeBurstPrefab.SetActive(true)`, `:2331`) - a separate 3-frame effect (`DownSpike Burst`, 20 fps).
   Body clip `DownSpike` (`:576-579`): 4 frames at 25 fps, wrap LoopSection with loop from frame 2 (line 483), so it plays frames 0-1 once and then loops frames 2-3 until the thrust ends.
4. End: after 0.1 s `FinishDownspike` (`:10190-10215`) -> `currentDownspike.CancelAttack()` hides the mesh and turns the polygon off (`Downspike.cs:104-114`), sets recovery (`downspikeRecoveryTime` 0.25 s), gravity back. Body: `Downspike Recovery` (4 frames at 15 fps = 0.267 s, line 6169) or `Downspike Recovery Land` (3 frames at 30 fps = 0.1 s) on the floor (`HeroAnimationController.cs:663-665`).
5. A hit during the thrust: `HeroDownAttack` queues a bounce; `Downspike.QueueBounce` -> `DownspikeBounce` (`HeroController.cs:6264-6285`), `FinishDownspike`, and then the body plays `DownSpikeBounce 1` or `DownSpikeBounce 2` (lines 9157, 10842).

### Effect

`DownSlash` (the `Downspike`): clip `DownSlashEffect`, 5 frames at 33.3 fps = 0.15 s, no frame events. Position (0.69, -0.37), scale (1.2, 1.2) in the hero's local space, facing left native. It is shown for the length of the thrust (0.1 s) and hidden by `CancelAttack`, so the player sees about frames 0-2 (the art spans y from -2.2 to +1.8 around the object, x -2.0 .. +1.9). On a hit it is hidden sooner by the bounce.

### Inferred (PlayMaker)

- How long the last frame of a Once clip holds on the body (`DownSpike Antic` 0.07 s inside a 0.1 s antic) is tk2d's usual "hold last frame". Not checked against an FSM.
- Whether the burst prefab and the antic are driven partly by an FSM (`crestAttacksFSM` is only used for the `Custom` type): none of that is readable. The C# above is complete for the default crest.

### What stays out of scope for our Task 5

The 0.1 s antic freeze, the thrust itself (velocity 26 for 0.1 s) and the recovery state, the burst effect, the knock-back damagers (45 / 135 degrees), the hero box shrink, `DownSpikeBounce 1/2` and `Downspike Recovery*` clips, clash tink, audio, and the existing bounce behaviour. Our hero does a down swing as a plain swing with the existing pogo (study 05), and the stub "Hero downspike thrust" stays.

## 5. Wall slash

- Chosen when `(wallSliding || wallScrambling) && direction == normal && !Toolmaster crest` (`HeroController.cs:4084-4097`). With `wallSlashSlowdown` true (Default) the fall speed is halved, never faster than -5 (already in `HeroAttack.StartSwing`, `WallSlashMaxFall`). Sets `wallSlashing = true` and uses the `WallSlash` object. Up and down swings on a wall flip the hero away and use the up / downspike objects (`AttackCancelWallSlide`, `:4190-4197`).
- Body clip: `Wall Slash` when `cState.wallSliding` and not an up/down swing (`HeroAnimationController.cs:598-600`). 7 frames at 24 fps = 0.292 s, wrap Once, no events (Knight library line 6122; sprites `Hornet_wall_slash0000..0002`, then `Hornet_sprint_wall0011..0014`).
- Effect: `WallSlash` object, clip `SlashEffect`, position (0.61, 0), scale (-0.9, 1) (section 1), so it is the normal swipe, shifted 0.61 to the swing side and mirrored relative to the hero's facing. Same 0.2 s lifetime, same 0.1 s hit window.
- The wall slash is cancelled the step the hero is no longer on the wall (`HeroController.cs:2273-2276`).
- In our code: `HeroAttack.StartSwing` already handles the slowdown and the swing side (`sideRight = clingSide < 0`). It plays `Slash` today; `HeroWall.PlayClingClip` runs every step (`HeroWall.cs:126`) and would replace any wall swing clip with `WallSlide` or `WallCling` on the next step, so Task 4 must make `HeroWall` leave the clip alone while a swing runs.

## 6. What exists in the export, and what an importer needs

### Clips (Knight library, `Animations\Knight.prefab`, same library the hero importer reads)

Body (present): `Slash`, `SlashAlt`, `UpSlash`, `Wall Slash`, `DownSpike`, `DownSpike Antic`, `Downspike Recovery`, `Downspike Recovery Land`, `DownSpikeBounce 1`, `DownSpikeBounce 2`, `DownSpike Burst` (effect), `Slash Land`, `Slash Land Run`, `Slash Land Run Alt`, `Slash To Run`.
Body (absent in this library): `UpSlashAlt`, `DownSlashAlt`, `DownSlash` (only in the Dagger/Wanderer override library).
Effect (present): `SlashEffect`, `SlashEffectAlt`, `UpSlashEffect`, `DownSlashEffect` and the four `*_Glow` clips (skip).
Not needed: `DashStabEffect`, `Slash_Charged`, `Slash_Charged Effect`, `Dash Attack*` (other attacks).

### Sprites

- Body and effect clips both point at collection `4eb1fd6b...` = `Collections\Knight Data\Knight.prefab` (`texelSize` 0.015625 = 64 ppu, same as the hero). The importer already cuts sprites from that collection. Effect sprite names: `Hornet_slashes_effects0000..0008`, `Hornet_slash_up_effect0000..0004`, `downstab_effect0003..0007`.
- Several effect frames are `flipped` 90 degrees in the atlas (`flipped: 1`, e.g. 906, 719, 1319); study 07 already says the cutter turns them upright. The trailing "empty" frames are real 1 pixel sprites (about 0.016 x 0.016 units), not missing ones: keep them so the frame timing stays right.
- The glow clips use another collection (`Knight Slash Cln Data`, guid `afba5a93`); not needed.

### Importer changes needed (Task 3 to 5; `HeroAnimationImporter.cs`)

`HeroAnimationMap.Entries` is a list of (hero name, source clip, played-by-hero flag). It needs these rows:

| Hero name (proposed) | Source clip | Used by |
|---|---|---|
| `SlashEffect` | `SlashEffect` | Task 3 |
| `SlashAltEffect` (or keep `SlashEffectAlt`) | `SlashEffectAlt` | Task 4 |
| `SlashUpEffect` | `UpSlashEffect` | Task 3 |
| `SlashDownEffect` | `DownSlashEffect` | Task 5 |
| `WallSlash` | `Wall Slash` | Task 4 |
| `SlashAlt` | `SlashAlt` (already there, flag `PlayedByHero` to become true) | Task 4 |

Effect clips carry frame events (tk2d `triggerEvent`): the converter turns them into `OnFrameEvent` animation events. For effect clips they are harmless but would need a target on the same GameObject as the Animator: `UnitySpriteAnimator.OnFrameEvent` already is one, so a normal `UnitySpriteAnimator` on the effect object is enough (the events just raise `FrameEvent` with no listener). The importer also has to keep working with `HeroClipNames` lists (`PlayedByHeroCode`, `ImportedForLater` in `SilksongHeroAssets.cs`, checked by `FindProblems` and the scene builders) so the new names must be added to one of them, otherwise the builders will not load the clips.

Re-running `Silksong > Import Hero Animations` and rebuilding scenes is needed after Task 3 (the plan's known risk).

## 7. What has to change in our code

- `ISpriteAnimator`: add a speed. Smallest additive way: `void Play(string clip, float speed)` with a C# default interface body that calls `Play(clip)`, so the three `FakeAnimator` classes in `Tests/PlayMode` (`HeroLandAnimationTests`, `HeroTurnAnimationTests`, `HeroWallAnimationTests`) still compile. (Unity 6000.0, C# 9: default interface methods are fine.) PROVISIONAL: the plan said "additive"; this keeps it so.
- `UnitySpriteAnimator`: `StartClip` needs a speed. `playable.SetSpeed(speed)` on the `AnimationClipPlayable` after `Create`; `Update` already advances with `graph.Evaluate(Time.deltaTime)` and the end test is `playable.GetTime() >= current.length`, which still works with a scaled playable. A loop-part continuation (`StartClip(next, 0)`) must carry the speed over. `Play(name)` must keep speed 1.
- `HeroAttack.StartSwing`: today it plays `Slash` / `SlashUp` / `SlashDown` by direction (line ~`owner.Animator?.Play(...)`). It needs: the alternate decision (a tiny pure rule, section 3), the clip name (`Slash`, `SlashAlt`, `SlashUp`, `SlashDown`, `WallSlash`), spawning/restarting the effect (Task 3), and hiding the effect on `EndSwing`, `CancelForDamage`, `OnDisable`, a pogo cancel. It must not touch `PlaceHitbox` or the timers.
- `HeroMovement.PlayAnimation` guard (line 226-229): lists `Slash`, `SlashUp`, `SlashDown`. It must also cover `SlashAlt` and `WallSlash`, or the new clips are replaced on the first step.
- `HeroWall.PlayClingClip` (line 126): see section 5.
- Hero art is assembled in several places (`RoomWorldFactory.AddHeroArt`, `HeroSceneBootstrap`, `HeroSceneBuilder`, `HeroTestSceneBuilder`, `TransitionRoomsBuilder`). The cleanest way to avoid editing all of them is for `HeroAttack` to build the effect child itself when it gets its clips; the heroClips array has to include the effect clips, which `SilksongHeroAssets` loads by the lists above.
- `HeroConfig` and `HeroConfig.asset`: no change needed. The 0.65 s reset is the sum of two original constants; keep it as a code constant in the new rule class rather than a config field (open question 6).

## 8. Copy / simplify / skip

| Thing | Decision | Why |
|---|---|---|
| Separate effect object per swing type, sprite renderer in front of the body | **Copy** | This is the whole look; one object reused, clip chosen per swing |
| Effect position (0,0) side/up, (0.69,-0.37) down, (0.61,0) wall; scales 0.9/1, 1/0.9, 1.2, -0.9 | **Copy** | Exact values from the prefab |
| Mirror by swing side / facing | **Copy** (flipX plus offset sign instead of parent scale) | Our art flip is `flipX`, not scale |
| Effect shown at swing start, hidden when the clip completes or the swing is cancelled | **Copy** | `NailSlash.cs:125`, `:186-190` |
| Body clip speed = 1 for swings | **Copy** | The original never fits the clip to the duration |
| Speed parameter on the animator | **Copy (add it now)** | Needed for quickening later and for the effect; default 1 |
| Stretch the swing to 0.35 s | **Skip** (owner decision) | Not what the original does; looks slow |
| Hitbox timing from effect frame events (0.1 s) | **Skip** | Hitboxes are frozen. Report the gap |
| Alternate sideways swing (clips + toggle + 0.65 s reset + direction change reset) | **Copy** | Pure rule, easy to test |
| Alternate up and alternate down | **Skip** (does not exist for the default crest) | No `AltUpSlash` / alt downspike object |
| Wall slash body clip + effect | **Copy** | One extra clip and object offset |
| `DownSlashEffect` look for the down attack | **Simplify** | Show it at the swing start, hide it at 0.1 s or on a bounce / swing end |
| `DownSpike` body clip (loop from frame 2) | **Copy** (already imported as `SlashDown`) | Fits the existing hold-the-pose behaviour |
| `DownSpike Antic` clip and the 0.1 s antic | **Skip** | We have no antic phase; showing an antic pose while the hitbox is already live would be wrong. Stub line |
| Downspike burst effect, `Downspike Recovery*`, `DownSpikeBounce 1/2` | **Skip** | Needs the thrust state; stubs |
| Downspike thrust, hero box shrink, knock-back dampers | **Skip** | Behaviour, not look; out of scope |
| Glow (`*_Glow`) clips, nail imbuement tint, Long Needle scale | **Skip** | Tool/charm features we do not have |
| Quickening speed (x1.5 clip, /1.5 duration) | **Skip** (stub) | Buff not built |
| `Slash To Run`, `Slash Land*` clips | **Skip** | Locomotion transitions; study 07 already lists them |
| `NailSlashTravel`, other crests' objects | **Skip** | Shaman only; one crest built |
| Attack sound / vibration | **Skip** | Not a visual |
| Recovery time (0.15 s no dash/turn after a swing) | **Skip** | Behaviour, not look |

## 9. Proposed exact numbers for Tasks 2 to 5

Task 2 (animator speed and timing)

- `Play(string clip, float speed)`; speed 1 default. Swing clips are played at **speed 1**. Unit test: speed 2 finishes a 0.25 s clip in 0.125 s; speed 1 unchanged; the loop-part continuation keeps the speed.
- Expected clip lengths to assert from the import: `Slash` 0.25 s, `SlashAlt` 0.25 s, `SlashUp` 0.2917 s (7 / 24), `WallSlash` 0.2917 s.

Task 3 (nail swipe effect)

| Swing | Clip | Local position when the swipe goes left / hero faces left | Local scale | Mirror |
|---|---|---|---|---|
| Sideways | `SlashEffect` | (0, 0) | (0.9, 1) | flipX when the swing goes right |
| Up | `UpSlashEffect` | (0, 0) | (1, 0.9) | flipX when the hero faces right |
| Down | `DownSlashEffect` | (0.69, -0.37) | (1.2, 1.2) | flipX and x = -0.69 when the hero faces right |
| Wall | `SlashEffect` | swing side x 0.61 (so +0.61 right, -0.61 left), y 0 | (0.9, 1) | flipX when the swing goes right |

- Child of the hero root, sorting order = the body renderer's order + 1 (builders use 5 for the body, so 6), pivot on the tk2d origin (importer default). Hidden at start; shown at swing start from frame 0.
- Hidden when: the effect clip completes (`Completed`), the swing ends early (hurt, dead, `CancelForDamage`, respawn, disable), the pogo bounce starts (down). Lifetimes: 0.20 s, 0.25 s (up), 0.15 s (down, clipped to 0.10 s by the downspike end).
- Never changes the hitbox, `AttackActiveSeconds` or the cooldown.

Task 4 (alternate swing and wall slash)

- Rule: `alt = (now - lastSwingStart) <= 0.65 s && direction == previousDirection && previousSwingWasFirst`. Equivalent to the original: reset when `> 0.15 + 0.5` s or the direction differs; sideways swing uses alt if the flag is set (and clears it), otherwise first (and sets it). Up, down and wall swings never set it. Constant `0.65`.
- Clips: sideways `Slash` / `SlashAlt`, effect `SlashEffect` / `SlashEffectAlt`; wall: body `WallSlash` (0.2917 s), effect `SlashEffect`.
- `HeroMovement` guard and `HeroWall` guard widened as in section 7.

Task 5 (down attack look)

- Body: `SlashDown` (already `DownSpike`), plays as today. Effect `DownSlashEffect` from frame 0 at swing start, position / scale as the table, hidden at **0.10 s** (`downspikeTime`) or on a bounce or when the swing ends. No antic, no thrust, no change to the bounce.

## 10. Open questions for the owner

1. **Clip speed.** The original plays the swing at natural speed (0.25 s pose inside a 0.35 s attack). The plan says to fit it to the attack time. Stretching to 0.35 s is not what Silksong does and will feel slow. Recommend: speed 1, keep the speed API for later. Agree?
2. **Visual reach versus hit reach.** The drawn swipe is about 2.9 units out; our (hand-tuned, frozen) hit box reaches 1.35. The original hit polygon reaches 3.36. The effect will look bigger than what hits. Options: leave it; shrink the effect scale (departs from the original); or a later task widens the hitbox. Your call.
3. **Hit window.** The original opens the hitbox for 0.1 s (0.083 s for up); ours is 0.35 s. Not part of this plan. Do you want it studied in a later pass?
4. **Is the Default config really Hunter / the crest you want?** I matched `Default.asset` and `Attacks/Default` by data; a different crest would change every number here.
5. **Imbued/glow and the `Long Needle` tool:** all skipped. OK?
6. **The 0.65 s alternate reset:** a code constant (my proposal) or a `HeroConfig` field? Adding a field might count as touching the frozen config.
7. **Alternate up and alternate down:** do not exist for this crest, so skipped. OK?
8. **Effect on the hero without art** (the plain-box fallback and the capsule test scene): proposal is no effect at all in that case, with a stub line. OK?
9. **Wall slash clip:** the first 3 frames are a dedicated art; the last 4 are borrowed from the `sprint_wall` sprites. Fine to play as is?
10. **Audio** (swing sound, the original pitches it up with quickening) is untouched. Do you want a swing sound in this plan, or leave it?
