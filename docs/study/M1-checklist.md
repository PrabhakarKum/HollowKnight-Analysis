# Milestone 1 checklist (owner acceptance)

You do not need to read code. Do each line, watch what happens, and write pass or fail in the table at the end.
All numbers below come from `Assets/Silksong/Hero/HeroConfig.asset`. Distances are in Unity units (the hero body is
0.5 wide and 2.08 tall).

## Setup

1. Open the project in Unity and wait for it to compile.
2. Menu **Silksong > Import Hero Animations** (once). The Console should say how many clips were imported. This needs
   your exported game files (default `D:\Asset HollowKnight Bundles\ExportedProject`).
3. Menu **Silksong > Build Hero Scene**. A red Console message means step 2 was skipped.
4. Unity opens `Assets/Scenes/SilksongHeroRoom.unity`. Press **Play**.
5. You should see: a flat grey room, a small pit, two blue platforms, two purple walls 4 units apart (the wall shaft),
   the hero at the green start point on the left, a red dummy on the floor and a red dummy floating high on the right.

Keys: A/D or left/right arrows = move. W/S or up/down arrows = look, and aim the needle up/down. Z or Space = jump.
C = dash. X = attack. R = put the hero back at the start (test room only).
If the hero faces the wrong way when you turn, tick or untick "Art Faces Left" on the `HeroSceneBootstrap` object.

## The checks (one line each)

| # | Item | How to do it | Pass if |
|---|------|--------------|---------|
| 1 | Run | Hold D, then A, on flat floor. | Hero moves at a steady 8.25 units/s (about 11 s to cross the 90-unit room), starts and stops instantly with no sliding, turns to face the way you press, run animation plays. |
| 2 | Variable-height jump | Tap Z quickly, then hold Z the whole way up. | A quick tap gives a short hop; holding gives a clearly higher jump (hold counts for up to 0.18 s). Releasing early cuts the rise (upward speed halved). Rise starts at 18.6 u/s, gravity 60. Pressing jump just after walking off a ledge (within 0.08 s) still jumps. |
| 3 | Dash on the ground | Stand still, press C, once facing each way. | Hero shoots forward about 2.8 units (28 u/s for 0.1 s), then stops or resumes running. A second press straight away does nothing; it works again after 0.425 s. |
| 4 | Dash in the air | Jump, press C at the top. Then press C again before landing. | The first air dash works. A second one in the same jump does not. After you touch the ground (or cling to a wall) you can air dash again. Same cooldown, 0.425 s. |
| 5 | Wall cling | In the wall shaft, jump toward a purple wall and keep holding toward it. | Hero sticks to the wall and slides down slowly (capped at 6 u/s), much slower than a free fall (30 u/s max). Letting go of the direction drops the hero off. |
| 6 | Wall jump | While clinging, press Z. | Hero is thrown away from the wall (25 u/s sideways, 18.6 up) and cannot steer back for 0.3 s. Jumping at one wall, then the other, lets you climb the shaft. |
| 7 | Needle left | Face left, press X. | A slash appears on the left. A swing lasts 0.35 s and you cannot swing again for 0.41 s. |
| 8 | Needle right | Face right, press X. | Same, on the right. |
| 9 | Needle up | Hold W or Up arrow, press X. | Slash goes above the head. |
| 10 | Needle down | In the air, hold S or Down arrow, press X. | Slash goes below the feet. |
| 11 | Pogo | Jump over the floating dummy (right side of the room), hold Down and press X while above it. | Hero bounces upward off it (12 u/s for 0.25 s) and can do it again, so you can keep bouncing on it. |
| 12 | Dummy reacts | Hit either red dummy with a side swing, then from above. | The dummy visibly reacts on every hit and never dies (it refills after each hit). Every hit registers, however many you land. |
| 13 | Take damage | See the warning below. | Lose 1 health (of 5). Hero is pushed away from the source (15 u/s) and stunned for 0.2 s, then cannot be hurt again for 1 s. |
| 14 | Die | See the warning below. | After the 5th hit the hero loses control and dies. |
| 15 | Respawn | After dying, wait; or press R at any time. | After 1.6 s the hero reappears on the green start marker with control and full health. R does the same instantly. |

**Warning on 13 and 14.** The built room has no spike, enemy or damage zone, and no key that hurts the hero. The
damage rules are covered by automated tests (`HeroHealthTests`), but you cannot trigger them by playing this room yet.
Mark these two as "Blocked: no damage source in the room" until one is added (the builder has reported this).

## Side by side with the real game: what to compare

Run the real Silksong and this room next to each other and compare by feel:

- Run: how quickly the hero starts and stops; run animation speed.
- Jump: height of a tap versus a full hold; how floaty the top of the jump is; how fast you fall.
- Dash: distance, and what the hero does right after (here it just resumes running).
- Wall: slide speed, how long the hero sticks before sliding, wall-jump distance, how easily you chain up a shaft.
- Needle: swing speed, how far the slash reaches, how long before you can swing again.
- Pogo: bounce height, and whether you can keep it going over a long gap.
- Camera: look-ahead as you run, how it stops at the room edges, how far it moves when you hold up/down (after
  standing still 0.85 s it should move 6 units).
- Hurt: knockback distance and stun time (once a damage source exists).

Write what differs in the Comments column and say which way (too fast, too slow, too high, and so on).

## Known provisional values the owner should judge

These could not be confirmed from the game's data. They are best guesses. If one feels wrong, change the field in
`Assets/Silksong/Hero/HeroConfig.asset` (unless noted).

| What | Field to change | Now | Why it is a guess |
|------|-----------------|-----|-------------------|
| Wall slide speed | `WallSlideSpeed` | 6 | The real game has no fixed speed: it speeds the slide up after a 0.15 s stick. Here it is a fixed cap. |
| Wall jump height | `WallJumpSpeedY` | 18.6 | Assumed equal to the normal jump. How long the rise is held after a wall jump was not found (0.18 s assumed). |
| Pogo bounce | `PogoBounceSpeed` | 12 | The real default needle may pogo with a jump of about 18.6 u/s for 0.1 s instead. The 0.25 s time is not a config field. |
| Hurt knockback sideways | `HurtKnockbackX` | 15 | Probably right, not confirmed. |
| Hurt knockback up | `HurtKnockbackY` | 0 | The real game pushes up about 7.5. The old prototype used 5. |
| Needle damage | `AttackDamage` | 1 | The real value grows with needle upgrades. |
| Needle hit boxes | `AttackSideSize`, `AttackSideOffset`, `AttackUpSize`, `AttackUpOffset`, `AttackDownSize`, `AttackDownOffset` | old prototype boxes | The real slash shapes could not be measured. The down box is small, so check that pogo feels reachable. |
| Needle hit window | `AttackActiveSeconds` | 0.35 | The real hit is live for part of the swing; here it is the whole swing. |
| Air dash length | `DashSeconds` | 0.1 | The real number could not be reconciled; the ground dash time is used. |
| Speed after a dash | none (code) | n/a | The real game decides this elsewhere; here you resume running if a direction is held. |
| Dash direction while clinging | none (code) | n/a | Not read from the real game. |
| Auto-respawn delay | `deathRespawnDelaySeconds` on the hero's `HeroHealth` component | 1.6 s | Stands in for the real death screen. Negative turns it off. |
| Look up/down camera rule | `GameCameraSettings` | 0.85 s, 6 units | The numbers are real; the "standing still" test is a reading of it. |
| Falling animation | none (animation mapping) | `Fall` | The real game uses a later frame of the jump clip. |
| Dash animation | none (animation mapping) | `Dash`, 0.56 s | The clip keeps playing a little after the 0.1 s dash ends. |

## Not built yet (so it is not a bug)

Each of these logs a "not implemented" line once in the Console.

- Camera: extra look-ahead for sprint, wall-sprint, slide, harpoon, updraft, super-jump and rising; umbrella-float
  look-down; lock areas and offset areas; short-damp after hits and respawns and scene-transition positioning; screen
  shake and glow/colour effects.
- Animation: random-frame and random-loop clips play as a plain loop.
- Room: plain blocks, not a real Silksong room (real room art and shape come later).
- Dash: dash buffer, shorter cooldown after a short jump, sprint after a dash, dash ending at a wall, down dash,
  "dash unlocked" check.
- Wall: stick delay before sliding, slide speed-up, chained wall jumps, short lock after a wall jump, wall cling tool,
  surfaces that cannot be clung to.
- Needle: attack buffer, alternating slashes, down-thrust animation, small push-back when the needle hits.
- Damage: hit-stop freeze, hit effects and sound, flicker while invulnerable, death animation, death screen, silk loss
  and cocoon, respawn fade and bench pose, hazards and hazard respawn, saving on death.
- Silk skills, tools and crests are out of Milestone 1 on purpose.

## Result sheet

| # | Item | Pass / Fail / Blocked | Comments |
|---|------|-----------------------|----------|
| 1 | Run | | |
| 2 | Variable-height jump | | |
| 3 | Dash on the ground | | |
| 4 | Dash in the air | | |
| 5 | Wall cling | | |
| 6 | Wall jump | | |
| 7 | Needle left | | |
| 8 | Needle right | | |
| 9 | Needle up | | |
| 10 | Needle down | | |
| 11 | Pogo off the floating dummy | | |
| 12 | Dummy reacts to hits | | |
| 13 | Take damage | | |
| 14 | Die | | |
| 15 | Respawn | | |
| - | Side-by-side feel notes | | |

Owner sign-off: ____________________  Date: ____________
