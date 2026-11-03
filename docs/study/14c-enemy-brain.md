# 14c. The walking brain (Milestone 4, task 3)

Summary in my own words, built from `docs/study/14-enemies.md` sections 6 and 7. Unity was not opened; nothing here has been run.

## What it is

The Moss Creep has no sight, chase or attack. Its whole behaviour is: walk, turn, get shoved, die. `EnemyBrain` is that as a pure state machine (no physics); `EnemyWalker` is the physics shell around it.

## States

| State | Meaning |
|---|---|
| Walking | horizontal velocity = facing x 3 u/s |
| Turning | one physics step: velocity 0, facing already flipped; next step walks the other way |
| Dead | velocity 0, ignores probes and shoves |

## The three checks (taken before each step)

In this order, from `Crawler.ShouldTurn`:

1. Ground under the back foot (ray down from the back edge, starting 0.6 above the box bottom, 1.1 long). **Nothing found: no turn at all** (the enemy is in the air or falling).
2. Wall ahead (ray forward from the front edge, length max(0.2, speed x dt) + 0.1): turn.
3. No ground ahead (ray down from 0.2 beyond the front edge, 1.1 long): turn (a ledge).

Rays ignore triggers and the enemy's own collider; the layer mask is passed to `Configure`.

## Turn

Decided in one step: facing flips at once, the sprite flips (root local x scale; the art faces left, so facing right means negative scale), velocity is 0 for that step, the next step walks. The original `Turn` clip (3 frames at 12 fps) is only the look and is not waited for. `TurnSteps` (default 1) can lengthen it.

## Shove

`Shove(vx, seconds)` adds `vx` to the walking velocity for `seconds` (15 u/s for 0.15 s from the study; at 0.02 s steps that is 7 or 8 steps, so about 2.25 to 2.4 units). The enemy keeps walking underneath and does not turn while shoved (the original does not turn while recoiling). A stronger shove replaces one in progress; a weaker one is ignored. The original moves the body with a terrain-clipped sweep; we use velocity, so the physics engine does the clipping.

## Numbers

All in `EnemyWalkParams` (defaults = study table): walk 3, back ray 0.6 / 1.1, wall 0.2 + 0.1, ledge padding 0.2 / 1.1, shove 15 / 0.15, turn 1 step, gravity 1, mass 1.

INFERRED (not in the data): the wall ray's height (0.2 above the box bottom), the ledge ray's start height (same 0.6 as the back ray), a 0.01 inset of the wall ray's origin, and "no turn while shoved" being applied as a hard rule.

## Not built / stubbed

Roof and wall crawling, forced turns from messages ("GO LEFT/RIGHT" of the noise graph), the Turn clip, the enemy-detector layer (use the terrain mask for now), gravity direction changes. The config (`SilksongEnemyConfig`) is not read here; a later integration task fills `EnemyWalkParams` from it and wires `EnemyHealth.Shoved/Died` to `IEnemyMotor`. `EnemyPlaceholderView.Build` makes a 1.4 x 0.8 green box with a dark "eye" on the art-facing (left) side.
