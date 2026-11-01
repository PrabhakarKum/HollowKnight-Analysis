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
is cleared the moment a jump starts, and is not granted when the hero left the ground by jumping.

**Jump buffering.** A jump press that cannot act yet is remembered for 2 steps (0.04 s) and fires when the hero
can jump. The memory is dropped if the button is released in a later step than the press; a press and release inside
one fixed step still counts (a tap), and then holds the jump for the minimum time. The window is inclusive: age 0 is
the press step, usable while the age is at most JumpBufferSeconds (three steps at 50 Hz). The original tests its
queue counter with a less-or-equal against 2, which is the same inclusive counting; for coyote I did not trace the
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
