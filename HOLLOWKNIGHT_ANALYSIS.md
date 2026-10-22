# Hollowknight-Analysis (learning project)

Goal: recreate and then refine Silksong/Hollow Knight-style player mechanics in Unity, with our own code
and our own values. Reference notes: `E:\Git Hub\HK-analysis\Silksong-player-mechanics.md`
(structure and field names only, no numbers).
Rules: learn the design, write original code. Do not copy their code or assets.

## Project
Unity 6000.0.84f1, URP 17, Input System 1.20 (new input only). Namespace `Hollowknight.*`.
Scripts compile into the default Assembly-CSharp (no asmdefs yet).

## Status
- [x] Movement + jump: run, variable-height jump, coyote time, jump buffer, custom gravity, apex hang
- [x] Camera follow: facing look-ahead, X/Y damping, grounded-only vertical follow (air dead zone), fall/rise offset, lock zones
      (per-state look-ahead for dash/sprint/etc. comes with those mechanics)
- [x] Dash (ground + air, buffered, cooldown, air-dash reset on ground/wall); camera dash look-ahead
- [x] Wall slide / wall jump (wall coyote, input lock after kick-off, chainable)
- [x] Double jump (1 air jump, hold for height) and float (hold jump while falling once air jumps are spent)
- [x] Attacks: forward/up/down slashes, 2-swing combo, cooldown + buffer, recoil, down-slash pogo, wall-slash rules
- [x] Health (masks), damage + knock-back, invulnerability + flicker, hazard (spikes) respawn, death + checkpoint respawn
- [x] Hit-stop (15-row freeze table from the DLL, stacking time controls) + screen shake (shake / rumble / sway, fall rumble, hard-landing
      shake, first-hit freeze on nail hits, enemy-death freeze) - see "Hit-stop ... and screen shake" below
- [ ] Skills / silk resource, tools
- [ ] Parry

## Files
- `Assets/Scripts/Player/PlayerTuning.cs` - ScriptableObject with every tunable (steps = fixed-timestep steps, 50 Hz default)
- `Assets/Scripts/Player/PlayerController2D.cs` - Rigidbody2D controller (gravityScale forced to 0, custom gravity)
- `Assets/Editor/PlayerSetupMenu.cs` - menu `Hollowknight > Create Movement Test Setup` builds a player + test platforms in the open scene
- `Assets/Scripts/Camera/CameraFollow2D.cs` - on the Main Camera; all camera tunables are serialized fields on it
- `Assets/Scripts/Camera/CameraLockZone.cs` - trigger box; while the player is inside, the view is clamped to it (room bounds)
- `Assets/Editor/CameraSetupMenu.cs` - menu `Hollowknight > Add Camera Follow + Lock Zone`
- `Assets/ScriptableObjects/PlayerTuning.asset` - created by the menu; tweak values here

## How to test
1. Open `SampleScene`, run menu `Hollowknight > Create Movement Test Setup`, press Play.
2. A/D, arrows or stick = run. Space / Z / gamepad South = jump (hold for higher).
3. Tap = short hop (min steps), hold = full jump. Walk off a ledge and jump late (coyote).
   Press jump just before landing (buffer).

## Controls (attack)
Attack = J / C / left mouse / gamepad West (Dash on gamepad is now East or Right Trigger). Hold Up = up slash, hold Down in the
air = down slash (pogo off any `HitTarget`). Menu `Hollowknight > Add Combat + Hit Dummies` adds the component and two dummies.

## Controls (added)
Dash = Left Shift / X / gamepad West or Right Trigger. Wall slide = airborne, hold toward a wall.
Wall jump = Jump while sliding or within wall-coyote steps. Dash while sliding goes away from the wall.
Test shaft: "Wall" (x=12) and "Wall 2" (x=15.5) in SampleScene for chain wall jumps.

## Current feel numbers (computed, not play-tested)
Full jump rise 4.2u (15 speed x 14 steps), min hop 1.5u, double jump +2.16u, dash 3.96u, float fall 3.5u/s.
Player is 1.4u tall, so a full jump is ~3 body heights. "Platform High" (top y=3.75, 4.25u above ground)
needs a double jump; "Platform Low" is a single jump. Float: after the double jump, keep holding Jump while falling.
Air jump and air dash refresh on ground and on wall slide / wall jump.

## Automated playtest (measured in the editor)
`Assets/Scripts/Dev/PlaytestBot.cs` (editor-only) injects keyboard input in play mode and logs `[PLAYTEST]` lines.
Run: set `EditorPrefs` bool `HK_PLAYTEST` = true and enter play mode (via Unity MCP RunCommand); read Editor.log.
Measured: full jump 4.20u, tap hop 1.49u, run 8.30, dash 22.0 u/s, wall-slide fall -3.50, wall-jump kick -9.00,
float fall cap 3.50. Not yet covered by the bot: coyote time, jump buffering, camera behaviour, chain wall jumps.
### Extended bot results (coyote / buffer / camera)
- Coyote (air jumps switched off for the test): ground jump still fires up to 5 steps after leaving a ledge
  (6+ steps: no jump). `coyoteSteps` raised 5 -> 8 because input lag (~1-2 steps) ate the window.
- Jump buffer: a press up to ~6 steps (0.12 s) before landing jumps on landing; 17 steps before does not.
  Bug found + fixed: with an air jump available, pressing jump just before landing spent the air jump in mid-air.
  Now `airJumpMinGroundGap` / fall-speed x buffer-time decides: near the ground the press is kept as a buffered
  ground jump. Trade-off: no double jump in the last ~2 u of a fast fall.
- Double jump: total rise 6.30 u from a 4.2 u jump (expected ~6.2), float then holds fall at 3.5 u/s.
- Camera: look-ahead +-2.2 u while running (target 3.0 minus damp lag), air dead zone: 4.2 u jump moves camera
  only 1.9 u, lock zone clamps to the room edge exactly (6.33), settles back to +1.2 after landing.
  Fixes made: dampTimeX 0.12 -> 0.07, lookAhead 2.5 -> 3.0, downward air dead zone 0.5 (was cancelling the fall
  offset), faster fall offset (start 6 u/s, smooth 0.12, dampTimeYFalling 0.05). Fall offset only reaches ~0 rel.
  to the player on a 9 u drop (needs a longer fall to look fully down); not yet eyeballed in the Game view.

### Player-feel bug hunt (22 scenarios, all passing)
Run: `EditorPrefs` string `HK_MODE`="hunt" + bool `HK_PLAYTEST`=true, enter play mode; results are `PASS`/`FAIL` lines in Editor.log.
Bugs found by the hunt and fixed:
1. Jump locked out after a head-bump under a low platform (reported by user): `jumpedSinceGrounded` only reset on a
   landing transition, so a jump that never left the ground blocked every later jump. Now it resets whenever the
   player is grounded and not mid-rise.
2. Head clipping a platform corner by a hair cancelled the whole jump: added corner correction (`cornerCorrection`, 0.3u).
3. A jump pressed during a dash was lost (buffer expired before the dash ended): the buffer now waits for the dash to end.
4. Falling onto a ledge with the feet a hair below its top blocked you: added ledge step-up (`ledgeStepUp`, 0.25u, airborne only).
5. (Earlier) Air jump was spent mid-air when pressing jump just before landing: now kept as a buffered ground jump.
Measured feel: input latency jump 20-26 ms / run 17 ms / dash 27-34 ms; reversing at full speed 0.12 s; stop distance 0.12 u;
only 1 air dash per airtime; air jump returns after a wall jump; chain wall jumps work; mashing jump 8/8.
Test-layout gotchas: platforms under the test spot cause accidental landings, and the near-ground rule turns a jump press
into a landing jump (by design).

### What the real game code does (read from Assembly-CSharp.dll IL; our code is our own)
Tool: `scratchpad/ildump/ildump.cs` (dotnet 10 file-based app) lists a method's IL with opcodes/constants/field names.
Numeric tunables (RUN_SPEED, JUMP_SPEED...) are serialized in assets, NOT in the DLL, so our numbers stay our own.
Verified behaviours, now mirrored in `PlayerController2D`:
- Jump: while the jump state is on, each fixed step sets vy = JUMP_SPEED, up to JUMP_STEPS; reaching the limit just
  cancels the state (velocity kept -> coasts over the top). Jump also clears the ledge buffer.
- Early release (after JUMP_STEPS_MIN, only if moving up): vy x 0.5, then cancel. (We used to zero it.)
- Air hang: gravity is set LOW (AIR_HANG_GRAVITY) after the rise and ramps up by AIR_HANG_ACCEL per second back to
  DEFAULT_GRAVITY while jump is held; if not held it snaps to default. Gravity is a rigidbody gravityScale.
- Double jump: a fixed-length routine (RISE/FALL steps), not button-dependent, speed JUMP_SPEED x 1.1, with its own
  max-fall-speed (MAX_FALL_VELOCITY_DJUMP).
- Float: while floating, vy is set to the constant FLOAT_SPEED each step.
- Input read each frame, jump/attack/dash queues counted in fixed steps (what we already did).
Dash (read + matched):
- Timed in seconds, separate ground (DASH_TIME) and air (AIR_DASH_TIME) lengths; vx = +-DASH_SPEED each frame, vy = 0,
  gravity off. Starting a dash cancels the current jump and clears the wall lock.
- The cooldown (DASH_COOLDOWN) is set at dash START. A jump sets the cooldown to 0.05 s (so jumping almost resets it).
- CanDash refuses: during another dash, cooldown running, attacking within recovery, or already air-dashed (unless wall
  sliding, which also refreshes it). Facing: wall sliding -> dash away from the wall; else the held direction; else facing.
- Running into a wall mid-dash (CanWallSlide) ends the dash and starts a wall slide. After the dash velocity snaps back to run.
Wall slide / wall jump (read + matched):
- Slide starts from a fall pressing into a wall (not on ground, not recoiling, not in the double jump). It is STICKY: you
  stay on the wall until you hold away for WALL_STICKY_STEPS or stop touching it. Sideways movement is held at 0 meanwhile.
- Wall jump: face away, refresh air dash + double jump, kick-off speed WJ_KICKOFF_SPEED that decays linearly to run
  speed over WJLOCK_STEPS_LONG (input ignored meanwhile), then a short chain window (WJLOCK_CHAIN_STEPS) where chaining
  needs no held direction. The jump counts as past its minimum steps (can be cut short at once).
Our values for these (cooldown 23 steps, air dash 8, sticky 8, kick-off 12, lock 10, jump-cooldown 3) are our own guesses.
Attacks (read + matched; `PlayerCombat2D`, `HitTarget`, tunables in PlayerTuning "Attack"):
- CanAttack: attack cooldown <= 0, not already attacking, not dashing (a down-dash excepted), not in a downspike
  bounce/recovery. Direction: Up held -> up; Down held while airborne -> down; down on the ground is a normal swing.
  Hit angles: right 0, left 180, up 90, down 270. A held horizontal direction turns you before the swing.
- Timers in seconds: attack lasts AttackDuration; cooldown = AttackCooldownTime but never less than the duration.
- Two-swing combo: normal then alternate; restarts after (duration + ALT_ATTACK_RESET) of pause or a direction change.
- Wall: a forward slash on a wall halves the slide speed (clamped at -5); an up slash moves the hero 0.8 u away from the
  wall and drops the slide; a down slash just drops it. An attack cancels a float.
- Recoil: a sideways hit forces +-RECOIL_HOR_VELOCITY for RECOIL_HOR_STEPS steps, away from the target.
- Pogo (down slash hit): a short uncuttable jump that starts bounce.JumpSteps into the normal rise, refreshes the air
  jump and air dash (ResetAirMoves), and ends the downspike.
Our additions/choices: buffer holds during a dash (swing fires when the dash ends), debug slash box, hit dummies.
Hard landing (read + matched; PlayerController2D, tunables "Hard landing"):
- A fall timer counts seconds only while falling at terminal speed (reset otherwise, and while wall sliding / floating). Past
  BIG_FALL_TIME the next landing is a hard one. On touchdown: cancel dash/attack, clear input buffers, enter a locked hero
  state (hero_state 5) for HARD_LANDING_TIME during which input is not even read; then back to idle.
  Ours: bigFallTime 1.1 s, hardLandingTime 0.5 s (guesses). Presses made during the lock are thrown away.
Camera (read + matched; CameraFollow2D):
- CameraTarget look-ahead: a world-unit offset slides at a constant 6 u/s toward the facing side (+-xLookAhead); follows facing,
  not movement, and holds when idle. A dash adds its own offset (dashLookAhead) only while dashing fast (|vx| > 5).
- Looking down: while falling at near-terminal speed a normalized offset ramps 4/s down to -1; after floating > 1.25 s it ramps
  1/s down to -1.5; otherwise it snaps to 0 (the controller's damping smooths it). Our unit for 1.0 is 2 world units.
- CameraController: SmoothDamp with separate dampTimeX / Y / Rising / Falling (we kept that structure). Lock zones/bounds exist.
Ours vs real: removed our own "rise offset" (the original has none for normal jumps). The air dead zone is still our own idea.
Not read/matched: hit-stop/screen shake, up-hit recoil (RECOIL_DOWN_VELOCITY), crest variants (downspike thrust type, quick attack,
rage mode), nail charge, tool throws, damage numbers. Our swing duration/cooldown/hitbox sizes/recoil are our own guesses.
Not yet read/matched: landing camera shake, per-state camera look-ahead for sprint/harpoon/super jump, wall slide fall speed/accel, sprint,
damage/health/silk, tools. (Hard landing and the camera look-ahead/look-down are done - see above.)
Rule followed: we study logic and re-implement it; no game code, art or audio is copied into the project.

### Wall-slide gap (user report) - cause and fix
Not the pivot (bottom-centre, correct) and not a collider offset (0). The DeadRevolver WallSlide art reaches only
0.25-0.31 u from the pivot while the collider half-width is 0.4, so ~0.15 u of empty space sat at the wall.
Fix: `PlayerVisuals2D.LateUpdate` places the art's wall-side edge on the collider edge each frame while sliding.
Gotcha: the pack's Animator controller resets the Visual's position every frame, so the offset must be assigned directly
(smoothing made it frame-time dependent and jittery). Test bot gotcha: `GetComponentInChildren<SpriteRenderer>()` on the
Player returns the disabled root placeholder - use the Animator's renderer. S24 now measures the gap per frame (0.00 u).

Follow-up (second report, "still a little gap"): Sprite.vertices is a LOOSER outline than the visible art (mesh x
[-0.313,0.250] vs opaque pixels [-0.250,0.188], 2 px each side). Fix: `Hollowknight > Bake Sprite Edges` reads the PNG pixels
of the WallSlide/WallJump frames and stores the exact opaque left/right edge per sprite on PlayerVisuals2D; the wall-slide
placement uses those (+0.015 for the physics contact skin). Re-bake if the sprites change. S24 now measures the gap from the
PNG pixels (independent code) to the wall surface: 0.005 u.

### Float (user report: "floats while coming down") - cause and fix
Cause: float started automatically whenever jump was merely still HELD after the double jump (no new press), and kept
floating right down to the ground (2.25 s). The real game (CanFloat / LookForQueueInput, read from the DLL) floats only from a
FRESH jump press in the air when the double jump is not available, and refuses it when about to land (IsApproachingSolidGround),
during the ledge buffer, wall slide, dash or an attack.
Fix: float is press-triggered (buffered like jumps), needs ground >= floatMinGroundGap (1.0 u) below, not coyote/wall/dash/attack;
release / land / wall / dash / attack ends it. A deliberate fresh press after the double jump DOES still float - that is the
float ability itself. To turn it off: PlayerTuning.floatEnabled = false.
Tests S40-S43 cover: holding after the double jump falls normally, a fresh press floats, near-ground press does not, attack ends it.

Float, second report ("double jump, then A+D+Space together floats"): probe showed the press floats when made in the last
~0.12 s before the top of the double jump (the 6-step buffer carries it to the apex) or any time after. In the real input
code a jump press that cannot act is queued and replayed later (jump queue + double-jump queue), so carrying a press is
authentic, but it makes the hero hover right at the top after a double jump, which reads as a bug.
Rule now (PlayerTuning.floatFromPressDuringRise = false): a float only starts from a press made while already falling.
Set it true for the queued behaviour. S44 checks presses at 0..0.2 s (rising) do not float and 0.45/0.6 s (falling) do.

Wall-slide facing (user report: after the slide the hero still looks at the wall): in the real code CancelWallsliding does not
flip, but its callers do: landing, losing contact with the wall (Update) and holding away past WALL_STICKY_STEPS (FixedUpdate) all
call FlipSprite first, so the hero ends facing AWAY from the wall. Ours never flipped. Fixed in CheckWall (the slide-ending
branches call FaceDirection(-side)); a wall jump and an attack/dash cancel keep their own facing. If you keep holding toward the
wall at that moment, input turns you back to it (same as the original). Tests S45a-d.
Note: one full run showed a single H1 failure (a swing counted during the hard-landing lock); two later full runs and the
isolated group were clean (60/60), so it looks like a timing flake in the test, not reproduced.

Wall-jump look (user report: "looks at the wall first, then turns away midway"): the facing logic was right (flips away on the
first step); the VISUAL was wrong. The wall jump starts inside a physics step that still reports "wall sliding", and
PlayerVisuals2D treated that stale flag as an interruption, so the WallJump one-shot was cancelled and the WallSlide (clinging) pose
stayed on screen for the whole 0.25 s before switching to a fall pose. Fix: a wall-slide state only interrupts a one-shot if it began
>0.1 s after the one-shot started (chain wall jumps still work). The pack's WallJump clip is a tucked somersault; if you would rather
have a plain jump pose, set PlayerVisuals2D.wallJump to "JumpRise". Test S45e checks the animation + facing on both sides.

FLOAT IS NOW OFF (user request: "I don't want the player to float at all"): PlayerTuning.floatEnabled defaults to false and the
asset is saved with false. The float code (press-triggered, ground gap, rise rule) is kept, so it can come back by ticking
floatEnabled. Tests: S46a (off by default), S46b (A+D+Space held after the double jump at 3 delays: never floats, falls at 20 u/s),
S46c (3 s of random mashing of every key: never floats). The older float tests (S40-S44, K4) now switch it on temporarily to prove
the code still works, then restore it.

Observation: after a full jump the fall reaches ~-19 u/s within 0.3 s (gravity 70, hard stop at apex) - snappy;
lower `gravity` or raise `jumpEndVelocityKeep` / apex hang if it should feel floatier.

### Damage / health / invulnerability (read from the DLL, then written ourselves)
Read: `HeroController.TakeDamage`, `CanTakeDamage`, `StartInvulnerable`, `HeroDamaged`, `DamageSelf`, `CancelDamageRecoil`, and the
compiler-generated coroutines `<StartRecoil>d__1088` and `<Invulnerable>d__1092` (ildump accepts those names as the type).
What the real code does (structure only; the values INVUL_TIME / RECOIL_VELOCITY / RECOIL_DURATION / DAMAGE_FREEZE_* are in assets):
- `CanTakeDamage` is false when: damage mode is "no damage", in a scene transition, `cState.Invulnerable`, `cState.recoiling`,
  `playerData.isInvincible`, parry invulnerability running, cheat invincibility, dead, hazard-dying, boss transition.
- `TakeDamage(go, side, amount, type)`: if allowed -> `PlayerData.TakeHealth`, `HeroDamaged` (cancel attack + fall effects), cancel wall
  slide / recoil / bounce, zero horizontal speed, cancel nail charge; health <= 0 -> `Die`; hazard types -> `DieFromHazard` / hazard
  respawn; otherwise `StartRecoil`. (Lots of Silksong extras - tools, crests, lifeblood, bell bind, parry - were not mirrored.)
- `StartRecoil`: reset motion, gravity off, `recoilVector` = +-RECOIL_VELOCITY x 0.5 depending on the impact side, turn to face the
  source, start invulnerability, then a "freeze moment" (DAMAGE_FREEZE_DOWN / WAIT / UP / SPEED), then `recoiling` = true.
  `Update` counts `recoilTimer` up to RECOIL_DURATION and then `CancelDamageRecoil` (motion reset = stop dead, gravity back).
- `Invulnerable` coroutine: first counts down the freeze duration, then INVUL_TIME in game time (deltaTime), flashing a pulse, then
  clears `invulnerable` and `recoiling`.
Ours (files `PlayerHealth2D`, `DamageSource2D`, `HealthHud2D`, `SimplePatrol2D`, hooks in `PlayerController2D`, tunables under
"Damage / health" in PlayerTuning):
- Health 5 masks (own guess). `PlayerHealth2D.TakeDamage(DamageInfo)` follows the order above: `CanTakeDamage` -> health -> cancel
  swing (`PlayerCombat2D.CancelAttack`) -> die, or invulnerable + hit freeze + knock-back (`PlayerController2D.BeginDamageRecoil`:
  drops dash/jump/float/wall slide/pogo/hard landing/buffered presses, forced velocity (9, 5) for 0.2 s with gravity off and input
  ignored, hero turns toward the source, then stops dead). Invulnerable 1.3 s (game time), alpha flicker, hit freeze (first a 0.1 s `Time.timeScale`
  guess; now the `FreezeMoment` HeroDamage row, see the hit-stop section; the original's four DAMAGE_FREEZE values are asset data).
- Hazards (`DamageKind.Hazard`, spikes): -1 health, no knock-back, hero hidden and held still, moved to the last safe ground after
  0.5 s, shown again facing back toward the hazard, held 0.25 s more, then invulnerable for 2 x invulnerableTime + 0.3 s.
  "Safe ground" = a grounded position the hero was already >= 0.25 s clear of (a lagging trail), frozen while respawning.
  Read later with ilspycmd (C# of `HeroController`, installed in the session scratchpad via `dotnet tool install ilspycmd`):
  the original takes the health, then `DieFromHazard` hides the hero, spawns a hazard effect, fades the screen and
  `HazardRespawn` puts it on a ground point under a designed respawn marker (not a trailing position), facing the marker's
  direction or back toward where it came from, invulnerable INVUL_TIME x 2 + 0.3. We copied the hide / facing / longer
  invulnerability; the marker-based respawn point, screen fade and "Hazard Respawn" animation are NOT done (ours is the auto trail).
  Also read: the knock-back vector's y is half its x; the recoil timer starts after the hit freeze; `recoiling` stays set until
  invulnerability ends (it is a second "cannot be hit" flag); death hides the hero and stops it in place (it does not fall) while
  a death effect plays - ours lets the hero fall and plays the pack's `Die` clip.
- Death: Health 0 -> `Dead` state (falls, no input, no sideways drift), 1.6 s later back at the checkpoint (default = start
  position, `SetCheckpoint`) with full health, a moment of invulnerability (our own addition) and a 0.25 s hold.
- `DamageSource2D` polls its collider each FixedUpdate instead of using `OnTriggerStay2D`: a resting Rigidbody2D sleeps and Stay
  callbacks stop, so a hero standing still in an enemy was never hurt again once invulnerability ended (found by the bot, S55).
- Menu `Hollowknight > Add Health + Damage Test Objects`: adds PlayerHealth2D + a HUD (IMGUI masks) and a root "Damage Test Objects"
  with a patrolling enemy (x -14..-11, also a pogo target) and spikes (x 9.5). The bot deactivates that root so old scenarios are
  not disturbed; the damage scenarios spawn their own throw-away damagers.
- Tests S50-S67 (bot group `HK_ONLY=damage`): full health, 1 health per hit, hit-freeze length, knock-back speed/duration/direction
  while holding the opposite key, stop at the end, invulnerable window (1.32 s measured vs 1.3), both sides + facing, dash cancel,
  swing cancel + discarded press, mid-jump hit, god mode, spikes respawn, 5 hits die, no input while dead, checkpoint respawn,
  timeScale restored.
Not done / not read: damage types beyond enemy + hazard (acid, lava, steam...), per-damage-amount effects and sounds, lifeblood,
extra masks, tools that change damage, hero "taunt"/parry interactions, the real hazard respawn fade, boss-scene damage scaling,
a death animation timing check (uses the pack's `Die` clip and `HitDamage` for the hurt pose, unchecked by eye).

### Hit-stop (freeze moments) and screen shake (read from the DLL, then written ourselves)
Read: `GameManager.FreezeMoment(FreezeMomentTypes)` and its coroutine `<FreezeMoment>d__464`, `GameManager.SetTimeScale` (both overloads),
`TimeManager` (all of it), `CameraShakeManager`, `CameraShakeProfile`, `CameraShakeTarget`, `HeroController.StartFallRumble /
CancelFallEffects / the fall-timer update`, `DamageEnemies.firstHitFreeze`, `HealthManager` and `EnemyDeathEffects` call sites.
What the real code does:
- A freeze moment = (rampDown, wait, rampUp, targetSpeed) from a 15-row `switch` in `GameManager.FreezeMoment`: it creates its OWN
  time control (`TimeManager.CreateTimeControl(1, type 0)`), ramps that control to `targetSpeed` over `rampDown` real seconds, waits
  `waitTime` of unscaled time, ramps back to 1 over `rampUp`, then releases the control. `GameManager.timeSlowedCount` counts the
  running ones. The table (all read, not guessed): HeroDamage .01/.28/.1/0, EnemyDeath .04/.024/.04/0, BossDeathStrike 0/.35/.1/0,
  NailClashEffect .01/.25/.1/0, BossStun 0/.25/.1/0, EnemyDeathShort .04/.015/.04/0, QuickFreeze .0001/.02/.0001/.0001, ZapFreeze 0/.1/0/0,
  WitchBindHit .04/.03/.04/0, HeroDamageShort .001/.15/.05/0, BossDeathSlow .1/1.15/.1/.05, RaceWinSlow .5/3/.3/.1,
  EnemyBattleEndSlow .1/1/.75/.25, BigEnemyDeathSlow .04/.06/.04/0, BindBreak .01/.4/.1/0.
- `TimeManager.UpdateTimeScale`: `Time.timeScale = max(0, TimeScale x CameraShakeTimeScale x DebugTimeScale x PlatformBackgroundTimeScale)`,
  then every control multiplies it (type 0) or caps it with `min` (type 1). So two overlapping freezes multiply, and a hit-stop that
  ends while a boss slow-down is still running goes back to the slow-down, not to 1.
- Camera shake profile (`CameraShakeProfile.GetOffset`): `Random.insideUnitCircle` picked once per frame, then
  type 0 "shake" = random x magnitude x decayCurve(elapsed/duration); type 1 "rumble" = random x magnitude, forever; type 2 "sway" =
  (swayX(t), swayY(t)) x magnitude, or the random vector scaled per axis by the curves when `useRandomShake`. Only shake (and sway with
  `endAfterDuration`) can finish. `freezeFrames` (default 1) is ignored for sway.
- `CameraShakeManager`: keeps a list of running shakes; each frame adds `dt` to each one's elapsed, SUMS the offsets, and if the sum is
  longer than the strongest running shake's magnitude it is clamped to that length, then x `ShakeMultiplier` (On 1, Reduced = a config
  value, Off 0). Playing a shake that can't finish while the same one runs is ignored. `DoShake(..., tryFreezeFrames)` may start a
  camera freeze: `CameraShakeTimeScale` = 0 for `frameCount x 0.016667` real seconds (the whole game stops). `CancelAllShakes` also
  blocks new finishable shakes for 0.5 s. The offset is added to the camera's local position in `OnPreCull` and taken off again in
  `OnPostRender`; it is skipped when `Time.timeScale` is 0 (non-realtime cameras) or while freeze-frames run.
- Triggers found in the DLL: `DamageEnemies.firstHitFreeze` (an int per attack, default -1 = none; applied to the first thing the
  attack hits), enemy death effects (`deathCameraShake`, "AverageShake"), blocked hits, `HealthManager` freeze type 9 for a crest crit,
  and the fall: when `fallTimer > BIG_FALL_TIME` the hero sets `willHardLand` and calls `StartFallRumble` (sets the camera FSM's
  "RumblingFall" bool, plus a looping sound); `CancelFallEffects` clears it on landing, damage, swimming, etc.
NOT in the DLL (PlayMaker FSMs / asset files): the magnitudes / durations / decay curves of every shake profile, the camera FSM that
turns "RumblingFall" into a rumble, and the hard-landing shake. The hero's own hit freeze is NOT a `FreezeMoment` table row either:
`HeroController.StartRecoil` uses its own `DAMAGE_FREEZE_DOWN / WAIT / UP / SPEED` fields (the same four-number shape, values live in
the prefab), so which table row stands in for it is our choice (see below).
Ours (`Assets/Scripts/Feel/`: `GameTime`, `FreezeMoment`, `ShakeProfile`, `ShakePresets`, `CameraShake2D`; hooks in `PlayerCombat2D`,
`PlayerHealth2D`, `PlayerController2D`, `HitTarget`, `CameraFollow2D`; tunables under "Hit-stop & screen shake" in PlayerTuning):
- `GameTime` is the only writer of `Time.timeScale` (product of independent controls, as above). `FreezeMoment.Start(type)` runs the
  table; the 15 rows are the DLL's numbers. `FreezeMoment.IsFrozen` replaces the old `PlayerHealth2D` coroutine
  (`PlayerHealth2D.IsFrozen` now forwards to it); the old `damageFreezeTime` (0.1 s, our guess) is gone: hero damage now uses the
  `HeroDamage` row (0.28 s hold) by default - OUR choice of row, change `heroDamageFreeze` (`HeroDamageShort` = 0.15 s).
- `CameraShake2D` is on the camera: same three profile types, per-frame random point, summed and clamped to the strongest, accessibility
  setting, camera freeze-frames, the 0.5 s block, no offset while the game is fully stopped. It only computes the offset;
  `CameraFollow2D` keeps its own base position (`BasePosition`) and writes `transform.position = base + shake` at the end of
  LateUpdate, so the shake is never fed back into the damping. (The original shakes at render time instead; same look, simpler here
  and safe under URP. Anything that reads the camera's transform sees the shaken position; use `BasePosition` for the steady one.)
- Hooks: first thing a swing hits -> `nailHitFreeze` (default QuickFreeze, ~0.02 s) + `nailHitShake` (Tiny) once per swing
  (`PlayerCombat2D.FirstHitFeedbackCount`); a `HitTarget` with `hitsToKill > 0` dies on that hit with `deathFreeze` (EnemyDeath) +
  `deathShake` (EnemyKill); hurt -> `heroDamageFreeze` + `heroDamageShake` (Average); death -> same freeze + `heroDeathShake` (Big);
  a fall past `bigFallTime` -> the RumblingFall rumble until landing / damage / death / wall slide / no longer falling;
  hard landing -> `hardLandShake` (Average).
- ALL shake sizes (magnitude 0.04 .. 0.30 world units, durations 0.12 .. 0.45 s, freeze-frame counts) and the choice of which
  freeze/shake goes with which event are our own guesses: they live in `ShakePresets.cs` and PlayerTuning, tune by feel.
- Tests FS1-FS30 (bot group `HK_ONLY=feel`, also part of the full run): table rows, HeroDamage hold ~0.29 s measured vs 0.28 table,
  QuickFreeze blink, stacking (slow-down x hit-stop returns to the slow-down), camera freeze-frames, shake peak <= magnitude and fading,
  base position undisturbed (no feedback), stacked shakes clamped, rumble not doubled, Off/Reduced setting, shake paused while the game
  is stopped, sway curves, the 0.5 s block, one freeze + one shake per swing even with two targets, killable target
  (first-hit + death feedback, dead targets can't be hit), hurt/death feedback, short fall silent / long fall rumbles from 1.1 s and the
  hard landing shakes, getting hurt mid-fall cancels the rumble, everything back to normal at the end. Full run: 106 PASS / 0 FAIL.
  Side effects of the longer hit-stop on older tests: S52 now checks the table's HeroDamage hold, S53 counts knock-back steps from
  the hit with a FixedUpdate counter (the ramp-up after the hold lets ~3 knock-back steps run before the old counting started).
- Not verified: how it FEELS by hand (nobody has played it); the shake is applied to the camera transform in LateUpdate, not at render time.

### Silk and bind (read with ilspycmd; effects are our own pixel art)
Read (decompiled C#, `HeroController`): `AddSilk`, `TakeSilk`, `SilkGain` (1 silk per nail hit; per-hit "full / first hit / none"), silk regen
(delay then duration per chunk, reset by any silk change; values are asset data), `CanBind`, `BindCompleted`, `BindInterrupted`, `AddHealth`.
- `CanBind` is false when: input blocked / cutscene / paused, dashing or back-dashing, attacking before the attack's recovery time,
  recoiling or recoil-frozen, transitioning, hazard dying/respawning, dead, hard landing, or a charge effect is running. The threshold
  where the "can bind" flash plays is 9 silk. `BindInterrupted` only clears moss chunks and silk parts.
- The bind itself (how long, how much it heals, when silk drains) is a PlayMaker FSM in the original and is NOT readable as code. Ours
  is a guess: hold F / gamepad North for 1.0 s, costs 9 silk, heals 3 masks, silk is only spent when the bind finishes.
- Ours: `PlayerSilk2D` (silk 0-9, +1 per nail hit on a `HitTarget` with `givesSilk`, bind input + rules, empties on death),
  `PlayerController2D.SetBinding` (all actions dropped, rooted sideways, gravity still applies, input ignored, new `MotionState.Binding`,
  shown with the pack's `Crouch` pose), health via `PlayerHealth2D.Heal`. Binding needs full silk AND missing health, a fresh press (not
  just a held button), and is broken by letting go, a hit, death or a hazard. Not done: silk regeneration, silk hearts / max-silk upgrades,
  silk parts (3 parts = 1 silk), moss chunks, tool/spell silk use, in-air bind rules (ours allows it; the hero falls rooted sideways).
- Effects (not Silksong's - drawn in code to match the 32-pixels-per-unit hero sprites; `Feel/PixelFx.cs`, `Player/BindEffect2D.cs`):
  a 64x64 dithered radial glow (ordered 4x4 dither, 4 brightness steps) behind the hero that swells and pulses in steps while binding;
  square pixel motes in cream / amber / teal spiral in and are absorbed (denser and faster with progress); on completion a flash, two
  expanding pixel rings (cream, teal) and a spark spray; breaking the bind drops a few ember pixels; gaining silk flies motes from the
  hit target into the hero; the HUD shows 9 amber silk pips that pulse when a bind is possible. All positions snap to the pixel grid.
  Looked at in rendered frames (the bot's `bindshots` mode writes PNGs to `HK_SHOTDIR`); two bugs only visible there were fixed (glow
  centred 0.7 u too high because the pivot is the body centre; motes 1/32 of their size from a double scale).
- Tests S70-S82 (`HK_ONLY=silk`): no bind without silk / at full health / one silk short, +1 silk per hit (and none for `givesSilk=false`),
  cap 9, bind rooted while D+Space are held, glow + motes + 93-pixel burst, finish at 1.05 s healing 2 -> 5 and spending 9, release at
  40 % breaks it with nothing spent, a hit breaks it (held F does not restart it), no bind during a dash, death empties silk.
  One capture run had a bind break early (hero standing, F apparently released) that did not repeat in the next runs or in the tests;
  not explained, probably input state dropped when the editor window lost focus.

### Sprint and sprint jump (read with ilspycmd; the speeds are not readable, effects are our own pixel art)
Read (decompiled C#): `CanSprint`, `FinishedDashing`, `HeroJump(checkSprint)`, `OnShuttleCockJump`, `ShuttleCockCancel`, the shuttlecock block in
`FixedUpdate`, `LeftGround` / `AllowShuttleCock` / `sprintBufferSteps`, `CanJump`, `BackOnGround` hooks, and `CameraTarget` (sprint look-ahead).
The sprint itself is a PlayMaker FSM (`sprintFSM`: events TRY SPRINT, CANCEL SPRINT, DASHED, HARD LANDING, DO SPRINT SKID), so its speed and exact
transitions are NOT in the code. What the code does show:
- The dash button is the sprint button. When a dash finishes (ground or air, not a down-dash) and input is not blocked, the game sends TRY SPRINT;
  the FSM then sprints while the button stays held. `CanSprint`: not in no-input / hard landing / dash landing, not dashing, not within attack
  recovery, no `preventDash`, control not relinquished. A hard landing cancels it.
- Sprint jump ("shuttlecock"): a jump while dashing / sprinting, or within the ledge-buffer steps after leaving the ground while sprinting/dashing
  (`sprintBufferSteps`), sets `jumped_steps = -5` (the rise lasts 5 steps longer), forces horizontal speed SHUTTLECOCK_SPEED in the facing direction
  every fixed step, and sets a 0.1 s dash cooldown. It is cancelled by an attack, by the fall starting (falling && !jumping), a downspike, a double
  jump, a wall jump, losing control. A cancel in the air leaves a decaying extra velocity (decay 5, cancelled by turning). When the fall starts or the
  hero lands with the dash button still held the game sends TRY SPRINT again (so the sprint carries on); without it a landing is a soft landing.
- Camera: while sprinting `CameraTarget` uses its own `sprintLookAhead` (asset value) in the facing direction instead of the dash look-ahead.
Ours (`PlayerController2D` + tuning "Sprint", `SprintFx2D`, `CameraFollow2D.sprintLookAheadBonus`; all numbers are guesses):
- Holding dash through a dash that runs its course (and not steering the opposite way) rolls into a sprint: dash speed 22 eases to sprintSpeed 14
  at 70 u/s^2; state `MotionState.Sprinting` on the ground (pack's `DashLoop`); a skid (`DashEnd`) when it ends at speed on the ground.
- Sprint ends on: dash released, steering the other way, a swing starting, wall slide / wall ahead, a hit, bind, death, hard landing. A dash tap
  (button released early) behaves exactly like the old dash. The sprint persists when running off a ledge and in the air.
- Sprint jump: shuttlecockSpeed 14 forced while rising, 5 extra rise steps (measured 5.62 u rise vs 4.12 u for a normal jump), ends when the fall
  starts; dash still held -> the sprint resumes in the air and on landing; dash released -> the speed fades (7 u/s^2) and turning cancels it.
  A jump within `coyoteSteps` of running off a ledge while sprinting is also a sprint jump; later it is a double jump. Dash during sprint restarts it.
- Not done: the attack-while-sprinting rules (ours: a swing ends the sprint - a guess), sprint into a wall launch (`startWithWallsprintLaunch`),
  back-sprint / scuttle (`isBackSprinting`, `isBackScuttling`), Sprintmaster / Quickening tool effects, conveyors doubling sprint speed, the
  terminal-velocity clamp while sprinting.
- Effects (original pixel art, `Player/SprintFx2D.cs` on `Feel/PixelFx.cs`): warm-grey square dust puffs kicked up behind the feet, short two-pixel
  cream / teal speed dashes streaming back past the body, a dust burst when the sprint starts, a forward dust spray on a skid, side dust + trailing
  speed pixels on a sprint jump. Checked in rendered frames (bot mode `sprintshots` -> PNGs in `HK_SHOTDIR`); dust was made chunkier after looking.
- Tests S90-S106 (`HK_ONLY=sprint`): dash tap does not sprint, hold dash sprints (0.21 s after the press, 14.0 u/s, state), camera look-ahead 2.2,
  dust count, release ends it + skid + run speed, release everything stops in 0.41 u, turning ends it, wall ends it at x 11.09, swing ends it, hit
  ends it, sprint jump speed/rise, fall start keeps sprinting, landing keeps sprinting, dash released in the air fades the speed and does not sprint,
  ledge window 3 steps yes / 14 steps no, `sprintEnabled=false` restores the old behaviour.
  A first capture run right after a recompile did not sprint (input state lost); the same capture run again was fine (repeats the note about editor
  stalls right after a script reload).

## Design notes (HK field names -> ours)
JUMP_STEPS / JUMP_STEPS_MIN -> jumpSteps / jumpStepsMin
LEDGE_BUFFER_STEPS -> coyoteSteps
JUMP_QUEUE_STEPS -> jumpBufferSteps
JUMP_RELEASE_QUEUE_STEPS -> jumpReleaseBufferSteps
DEFAULT_GRAVITY -> gravity; MAX_FALL_VELOCITY -> maxFallSpeed; AIR_HANG_* -> apexGravityMultiplier
Numeric values are our own starting guesses; tune by feel.

## Refinement ideas
Corner correction on head bumps, ledge step-up, slope handling, run-start/skid animation hooks,
per-state gravity, acceleration curves.
