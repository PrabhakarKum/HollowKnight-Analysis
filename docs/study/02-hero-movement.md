# 02 Hero movement

Summary in my own words of how the original hero runs, jumps and falls, and which numbers this project uses.
Sources, all read only:

- Code: `D:\Asset HollowKnight\HollowKnightSilksong\Assets\Scripts\Assembly-CSharp\HeroController.cs` (hero movement,
  jump, ledge grace and queue logic) and `HeroControllerConfig.cs`.
- Serialized values: `D:\Asset HollowKnight Bundles\ExportedProject\Assets\Prefabs\Heroes\Hero_Hornet.prefab`
  (the hero controller component and its Rigidbody2D) and
  `...\Assets\Data Assets\HeroController Configs\Default.asset` (the starting crest's attack config).
- Project settings: `D:\Asset HollowKnight\HollowKnightSilksong\ProjectSettings\Physics2DSettings.asset` and
  `TimeManager.asset`.

## Time base

The original counts most movement timing in fixed steps, and the project's fixed step is 0.02 s (50 Hz). Every
step count below is converted to seconds so that this project's behaviour does not depend on the physics rate
(Review Focus 3). "1 step" = 0.02 s.

## Behaviour

**Run.** Horizontal speed is set directly each step to the run speed times the direction (-1, 0, +1). There is no
acceleration or deceleration, and the same speed is used in the air. Direction comes from the left/right presses,
which use the 0.3 threshold from `04-input.md`; a tilted stick is therefore full speed, not scaled.

**Jump, variable height.** Pressing jump while standing starts a rise. For up to 9 steps (0.18 s) while the button
stays down, the upward speed is forced to the jump speed every step, so the early rise is a constant-speed climb.
After that, gravity takes over. Letting go early cuts the rise: the upward speed is halved and the sustained rise
ends. The cut is not allowed in the first 2 steps (0.04 s), so a quick tap still produces a small hop. The
original also keeps a very short queue for the release itself, which this project does not need because it reads
the button level.

**Coyote time.** After walking off a ledge the hero may still start a ground jump for 4 steps (0.08 s). The grace
is cleared the moment a jump starts, and is not granted when the hero left the ground by jumping. In this project the
window is inclusive: a jump is allowed while the time since leaving the floor (0 on the step it left) is at most
CoyoteSeconds, so 0.08 s allows five steps at 50 Hz. I did not trace the original counter order, so it may allow one
step fewer; the window equals the config value by definition.

**Jump buffering.** A jump press that cannot act yet is remembered for 2 steps (0.04 s) and fires when the hero
can jump. The memory is dropped if the button is released in a later step than the press; a press and release inside
one fixed step still counts (a tap), and then holds the jump for the minimum time. The window is inclusive: age 0 is
the press step, usable while the age is at most JumpBufferSeconds (three steps at 50 Hz). The original tests its
queue counter with a less-or-equal against 2 (read from the code, unverified at run time), which would be the same
inclusive counting; for coyote I did not trace the
counter order, so it may differ by one step.

**Gravity and fall cap.** The hero's body uses the engine's gravity. The project gravity is 60 downward and the
hero's gravity scale is 1, so the effective pull is 60 units per second squared. Falling speed is clamped to a
maximum of 30 (38 with the weighted anklet tool; not built). There is also a brief low-gravity "air hang" and a
double jump, used by other abilities; neither is built here.

## HeroConfig fields and where each value came from

"Hero prefab" = `Prefabs/Heroes/Hero_Hornet.prefab`, hero controller component. "Derived" = steps times 0.02 s.

| Field | Value | Source |
|---|---|---|
| RunSpeed | 8.25 | Hero prefab, `RUN_SPEED` |
| JumpSpeed | 18.6 | Hero prefab, `JUMP_SPEED` |
| JumpHoldMaxSeconds | 0.18 | Derived: Hero prefab `JUMP_STEPS` = 8, and the code sustains the rise while the step counter is 8 or less (9 steps) |
| JumpHoldMinSeconds (extra field) | 0.04 | Derived: Hero prefab `JUMP_STEPS_MIN` = 2 |
| JumpReleaseMultiplier (extra field) | 0.5 | `HeroController.cs`, jump-release handling (rise speed halved) |
| Gravity | 60 | Derived: `Physics2DSettings.asset` gravity y = -60 times hero Rigidbody2D gravity scale 1 (Hero prefab, `DEFAULT_GRAVITY` = 1 and Rigidbody2D `m_GravityScale` = 1) |
| FallSpeedMax | 30 | Hero prefab, `MAX_FALL_VELOCITY` |
| CoyoteSeconds | 0.08 | Derived: `HeroController.cs` ledge buffer constant of 4 steps (not serialized, a code default) |
| JumpBufferSeconds | 0.04 | Derived: `HeroController.cs` jump queue constant of 2 steps (code default) |
| HorizontalPressThreshold (extra) | 0.3 | `04-input.md` (`HeroActions.cs`) |
| VerticalPressThreshold (extra) | 0.5 | `04-input.md` (`HeroActions.cs`) |
| DashSpeed | 28 | Hero prefab, `DASH_SPEED` |
| DashSeconds | 0.1 | Hero prefab, `DASH_TIME` |
| DashCooldownSeconds | 0.425 | Hero prefab, `DASH_COOLDOWN` |
| WallSlideSpeed | 6 | **PROVISIONAL.** The original has no fixed slide speed: it adds an acceleration (Hero prefab `WALLSLIDE_ACCEL` = -24 per second) after a 0.15 s stick time. A fixed speed is a design choice for the wall task |
| WallJumpSpeedX | 25 | Hero prefab, `WJ_KICKOFF_SPEED` (starts at this and decays toward run speed over the lock) |
| WallJumpSpeedY | 18.6 | **PROVISIONAL.** Assumed equal to the jump speed (`JUMP_SPEED`); the original wall jump was not traced to confirm it |
| WallJumpLockSeconds | 0.3 | Derived: Hero prefab `WJLOCK_STEPS_LONG` = 15 (short lock is 5 steps = 0.1 s) |
| AttackActiveSeconds | 0.35 | `Default.asset`, `attackDuration` |
| AttackCooldownSeconds | 0.41 | `Default.asset`, `attackCooldownTime` |
| PogoBounceSpeed | 12 | **PROVISIONAL.** Best-guess match to Hero prefab `BOUNCE_VELOCITY`; not confirmed to be the pogo bounce (down-spike rebound speed 20 is sideways) |
| HurtInvulnerableSeconds | 1 | Hero prefab, `INVUL_TIME` |
| HurtKnockbackX | 15 | **PROVISIONAL.** Guess that Hero prefab `RECOIL_VELOCITY` is the damage recoil speed; not confirmed |
| HurtKnockbackY | 0 | **PROVISIONAL.** No separate vertical damage knockback value found; the original recoil is horizontal |
| HurtStunSeconds | 0.2 | Hero prefab, `RECOIL_DURATION` |

Dash, wall, attack and hurt fields are declared now and unused until their tasks.

## PROVISIONAL values (owner to confirm)

1. WallSlideSpeed = 6.
2. HurtKnockbackY = 0.
3. WallJumpSpeedY = 18.6 (assumed equal to jump speed).
4. PogoBounceSpeed = 12 (guessed mapping).
5. HurtKnockbackX = 15 (guessed mapping).

## Deliberate deviations

- The original sets the rise speed and relies on the engine to apply gravity afterwards; this project applies
  gravity itself (body gravity scale 0) so the config value is the only source of truth.
- Step counts are stored as seconds. At 50 Hz they match the original exactly; at other rates the durations stay the
  same and the number of steps changes.
- Hero starts and stops with instant velocity changes, as the original does; no smoothing was added.
- Not built: air hang, double jump, brolly float, walk zones, slopes, conveyors, updrafts, mantle.

## Dash

Summary in my own words. Sources, all read only: the dash start, per-step dash, finish and cancel code in
`HeroController.cs` (the decompiled export), the serialized numbers in `Hero_Hornet.prefab`
(`DASH_SPEED`, `DASH_TIME`, `AIR_DASH_TIME`, `DOWN_DASH_TIME`, `DASH_QUEUE_STEPS`, `DASH_COOLDOWN`), and the owner's
prototype (`PlayerController2D.cs`, `PlayerTuning.cs`, `PlayerTuning.asset`; prototype step counts are converted at
0.02 s per step). The prototype was only read, never changed.

How it plays: a press starts a straight horizontal burst at a fixed speed with no vertical speed and no gravity.
When its time runs out gravity returns and normal movement takes over. The cooldown starts when the dash starts, not
when it ends. One dash is allowed per airtime; touching the floor (or sliding on a wall) gives it back. A press that
arrives while a dash is not yet possible is remembered briefly and fires when it becomes possible.

### Comparison: prototype vs Silksong vs the new hero

"New hero" follows Silksong's measured value unless noted. Rows marked DIFFERENT need the owner's decision: say
"keep prototype" for any row to put the prototype value into `HeroConfig` instead.

| # | Parameter or rule | Prototype | Silksong (source) | New hero uses | |
|---|---|---|---|---|---|
| 1 | Dash speed | 22 | 28 (prefab `DASH_SPEED`) | 28 | DIFFERENT |
| 2 | Ground dash time | 0.18 s (9 steps) | 0.1 s (prefab `DASH_TIME`) | 0.1 s | DIFFERENT |
| 3 | Air dash time | 0.16 s (8 steps) | 0.02 s in the prefab (`AIR_DASH_TIME`), but the code uses it as the air dash length and it is far too short to match the known feel; the rest may live in the sprint state machine, which cannot be read | 0.1 s (same as ground) | DIFFERENT, **PROVISIONAL** |
| 4 | Ground distance | 3.96 | 2.8 (28 x 0.1) | 2.8 | DIFFERENT (follows 1 and 2) |
| 5 | Cooldown | 0.46 s (23 steps), from dash start | 0.425 s (prefab `DASH_COOLDOWN`), from dash start | 0.425 s from dash start | DIFFERENT |
| 6 | Cooldown shortened by jumping | cut to 3 steps (0.06 s) when a jump starts | set to 0.05 s when a jump starts | not built (would need a hook in `HeroMovement`, outside this task) | DIFFERENT, deferred |
| 7 | Press buffer | 6 steps (0.12 s) | 10 steps (0.2 s, prefab `DASH_QUEUE_STEPS`), only while the button stays held | not built (no config field; recommended follow-up) | DIFFERENT, deferred |
| 8 | Direction | held left/right, else facing | held left/right (not both), else facing | same | same |
| 9 | Vertical speed and gravity | speed forced to 0, gravity skipped | speed 0, body gravity switched off, restored at the end | speed 0 and gravity skipped; gravity resumes the step after the dash ends | same |
| 10 | Air dashes per airtime | 1 | 1 (a flag set on an airborne dash) | 1 | same |
| 11 | Refresh of the air dash | landing, wall slide, wall jump | landing, wall slide, jumping from floor or wall, several move resets | landing now; wall refresh when the wall task exists (stub) | same |
| 12 | Can start | cooldown over and (grounded or air dash left or wall sliding) | same, plus not in hard landing, not already dashing, not hurt/dead, and the ability must be unlocked | cooldown over, grounded or air dash left, not hurt or dead | same (unlock check not built) |
| 13 | Exit speed | run speed in the held direction, else zero | not set by the dash; the sprint machine takes over | movement resumes the same step (run speed if held, else stop) | **PROVISIONAL** |
| 14 | Sprint after dash | rolls into a sprint if the dash button is still held | asks the sprint machine to try a sprint | not built (stub) | DIFFERENT, deferred |
| 15 | Wall contact while dashing | ends the dash and starts a wall slide | the per-step dash code starts a wall slide if one is possible | not built (wall task) | same |
| 16 | Down dash (in the air, down held, no sideways) | none | yes, straight down at fall speed, 0.25 s | not built; logs a stub and does a normal sideways dash | DIFFERENT, deferred |
| 17 | Gamepad/keys buttons | shift, x, east, right trigger | the Dash action | the `IHeroInput.Dash` edge | n/a |

Count: 7 differences that change feel (rows 1, 2, 3, 5, 6, 7, 14) plus 2 more (row 4 follows from 1 and 2; row 16 is a
whole extra move). Time base: all new-hero timers are seconds reduced by the step length, so 50 and 100 Hz agree.

### PROVISIONAL (owner to confirm)

1. Air dash time = 0.1 s (row 3). The prefab number 0.02 s could not be reconciled with how the dash is used.
2. Exit speed after the dash (row 13). The dash code does not set it and the sprint state machine could not be read.

### Not built here (stubs and follow-ups)

Dash press buffer (row 7), jump-shortened cooldown (row 6), sprint after dash (row 14), down dash (row 16, logs a
stub), wall interactions (row 15), and the "dash unlocked" check.
