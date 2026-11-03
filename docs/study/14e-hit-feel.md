# 14e. Hit feel on the hero (Milestone 4, task 5)

Numbers come from `docs/study/14-enemies.md` (sections "hero feedback on hit" and "downspike grace") and the rows in `docs/study/05-combat.md`. Nothing here changes nail damage, hitboxes, swing timings or any value already in `HeroConfig`.

## What the hero now feels when the needle lands

A "landed" hit is one the target accepted (`TakeHit` returned true). What follows depends on the target.

The target says what it is through a tiny interface, `IHitFeedbackTarget` (`Combat/IHitFeedbackTarget.cs`): `GivesSilk` and `Bounces`. `EnemyHealth` implements it (both true, so the Moss Creep gives silk and bounces). `BreakableWall`, `HealthManager`/`TrainingDummy` and any other target do not implement it. The hero never checks for enemy types.

| Hit | Effect | Number | Config field |
|---|---|---|---|
| Any direction, target gives silk | +1 silk, clamped at `PlayerData.maxSilk` (9). Written to the hero's own player data (`HeroHealth.Data`, the same object that holds health) | 1 | `SilkPerNailHit` |
| Sideways | Hero is pushed away from the swing (a right swing pushes left), forcing the horizontal speed even if the player holds a direction. The hit step counts as step 1; afterwards run input is back | 3.75 u/s for 8 steps (0.16 s, about 0.6 units) | `NailRecoilSpeed`, `NailRecoilSteps` |
| Up | A rise faster than the limit is cut to the limit; falling is untouched | limit 0 | `UpRecoilMaxRiseSpeed` |
| Down, target bounces and has no active `NonBouncer` | The existing pogo (12 u/s for 0.25 s, gives back the air dash), and the enemy-contact grace below | 2 steps | `DownspikeGraceSteps` |

Rules that stop the sideways push (it neither starts nor continues): already recoiling, dashing, clinging to a wall, wall-jump locked, hurt or dead, or control locked by a room transition. A hit on the hero (`CancelForDamage`), a respawn or disabling the hero clear it. The push is applied at the end of the controller step, after movement, the same way the pogo overrides gravity, so the run input cannot overwrite it. No new velocity-owning mechanism was added.

Up hit does the same exclusions for dashing and clinging (dash owns the velocity; an up swing already lets go of a wall).

Silk needs `AttackDamage > 0` and a target with `GivesSilk`. A swing hits a given target once, so one swing gives at most one silk per target. Dead enemies refuse hits, so they give nothing. Not built: the original's silk-spool "moss" bookkeeping (HUD only) and the Shade/Armoured exclusion beyond the `GivesSilk` flag (no such enemy yet).

## Downspike contact grace

After a down hit that bounces, the hero ignores enemy contact for 2 steps (10 for the original's long downspike, which we do not have; the field is configurable).

- `HeroHealth` got a small counter: `BeginContactGrace(steps)`, `IsInContactGrace`, `ContactGraceStepsRemaining`. It ticks down at the start of each controller step (`FixedStep`) and is cleared when the component is disabled.
- It is deliberately NOT part of `CanTakeDamage`. Only `EnemyContactDamage.Hit` asks `hero.IsInContactGrace` and skips. Spikes and other hazards (`HazardZone`) call `TakeHit` directly and still hurt.
- Counting: set to 2 at the hit (end of the controller step). The contact checks in that physics step and the next one are ignored; the check after the second following controller step hurts again.

## Files changed

- New: `Combat/IHitFeedbackTarget.cs`, `Hero/HeroHitFeelRules.cs` (pure decisions: silk clamp, recoil gate, direction, rise cap, bounce gate).
- `Hero/HeroAttack.cs`: replaced the three hit stubs with `ApplyHitFeedback`, plus the recoil state and its per-step application.
- `Hero/HeroConfig.cs`: five additive fields (defaults above). `HeroConfig.asset` was not edited; Unity gives missing fields their C# defaults.
- `Hero/HeroHealth.cs`: the contact-grace counter (additive).
- `Enemy/EnemyHealth.cs`: implements `IHitFeedbackTarget`. `Enemy/EnemyContactDamage.cs`: one early-return line.
- Tests: `Tests/EditMode/HeroHitFeelRulesTests.cs`, `Tests/PlayMode/HeroHitFeelTests.cs`; `HeroConfigConsistency.cs` documents the new fields; `HeroAttackTests` lost its assertion of the removed recoil stub.

## Stubs

Removed (really implemented): `Hero silk gain on hit`, `Hero nail horizontal recoil`, `Hero nail up recoil`.

Kept: `Hero bounce refreshes double jump` (the hero has no double jump at all yet, so there is nothing to refresh; the air dash refresh already existed), `Hero downspike thrust`, `Hero alternate slash`, `Hero attack buffer`.

Not built, no stub: long recoil, drill pull, bounce mushrooms, "Recoiler" terrain, hit-stop, shake, long downspike.

## Known difference from the brief

Targets without `IHitFeedbackTarget` still pogo on a down hit unless they carry an active `NonBouncer` (the old rule, which existing tests pin: `Pogo_OffAnyHittable_NotJustDummies`). They get no silk, no sideways recoil and no up cap. A target that wants no bounce can implement the interface with `Bounces = false` or carry a `NonBouncer`. Giving `BreakableWall` a non-bounce is a one-line change in a file outside this task.
