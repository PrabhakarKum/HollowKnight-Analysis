# Damage, death and respawn

Summary in my own words. Sources, all read only: `HeroController.cs` (`TakeDamage`, `CanTakeDamage`, `StartRecoil`,
`StartInvulnerable` / `Invulnerable`, `CancelDamageRecoil`, the recoil block of the update loop, `Die`, `Respawn`,
`HazardRespawn`), `PlayerData.cs` (`TakeHealth`), the numbers in `Hero_Hornet.prefab` (`INVUL_TIME` 1,
`RECOIL_DURATION` 0.2, `RECOIL_VELOCITY` 15, `DAMAGE_FREEZE_DOWN` 0, `DAMAGE_FREEZE_WAIT` 0.1, `DAMAGE_FREEZE_UP` 0.1,
`DAMAGE_FREEZE_SPEED` 0), and the owner's prototype (`PlayerHealth2D.cs`, `PlayerController2D.cs`, `PlayerTuning.cs`,
`PlayerTuning.asset`). The prototype was only read, never changed.

## How the original handles a hit

- A hit is refused while the hero is invulnerable, still in the recoil, dead, or has a damage mode of "none". Enemy
  and explosion hits are also refused by several special states (parry, evade, a downspike just landed, shadow dash,
  hazard-only mode). A hit that deals zero damage does nothing except tell the attacker.
- Damage amount is taken from the hit, except hazards, which override it by type (spikes 1, acid 1, lava 2, steam 2,
  coal 1, zap 1, respawn pit 0); flame adds up to 2; boss-scene levels scale it. Tools can change it (barbed wire,
  lucky dice shield, bell bind absorbs a hit, lava bell halves lava damage).
- Health is taken from `PlayerData`: lifeblood masks first, then normal health, floor at 0.
- On a landed hit that deals damage: sound and effects, the thief charm's coin loss, the reaper / warrior crest
  states reset, the attack is cancelled, a wall slide is dropped (hero box back to normal), a wall touch is cleared, a
  sideways recoil and any bounce are cancelled (a bounce also zeroes the vertical speed), nail charging is cancelled.
- Health 0: the death routine runs. Otherwise a hazard hit starts the hazard death (below); an enemy hit starts the
  recoil.
- Recoil (`StartRecoil`): all motion is reset, gravity off, the hero cannot be steered (the "no input" state). The
  recoil speed is `RECOIL_VELOCITY` (15) horizontally away from the damage side and half of that (7.5) upward. The hero
  turns to face the side the hit came from. Invulnerability (`INVUL_TIME`, 1 s) starts at the same moment. A short
  freeze-frame (`FreezeMoment`: 0 s down, 0.1 s hold, 0.1 s back up) plays first, and the velocity is only applied
  after it. The recoil then lasts `RECOIL_DURATION` (0.2 s) of game time; at its end the hero is stopped dead, gravity
  returns and the state goes back to airborne or the previous one.
- Invulnerability counts down in game time (the freeze does not eat into it) while the sprite pulses. Extra sources
  (parry, quake, silk dash, cross stitch) use separate shorter timers.
- Hazard hit (spikes, acid, lava, pit, ...): the hero is hidden, put back on the last safe ground (found by a ground
  probe from the saved hazard respawn point), faces back toward the hazard (or the marker's facing), and gets
  `INVUL_TIME * 2 + 0.3` invulnerability. A second hazard hit within 0.5 s of a hazard respawn moves the respawn point
  to the room entrance instead.
- Death (`Die`): once only (it returns if already dead); velocity zero, gravity off, body kinematic, hero hidden and
  moved to an ignore layer, a death animation prefab is spawned, the corpse / cocoon marker is stored (silk and coins
  are left there, hence the "silk loss"), the death screen runs, then the scene reloads at the respawn marker.
  Non-lethal deaths (memory scenes, some bosses) use a temporary marker instead.
- Respawn (`Respawn`): hero visible and on its normal layer, hero box restored, dead / hazard / recoil flags cleared,
  air dash and double jump reset, motion reset, attacks reset, input reset, max health restored after a real death,
  then the entry animation (bench sit or walk-in).

## What the new hero does

`HeroHealth` is a component added by `HeroController.Awake` (like the dash, wall and attack). It implements
`IHittable`, so an enemy's damage code hands the hero a `HitInstance` (`Damage`, `Source`) exactly as the hero's needle
hands one to enemies. `RespawnMarker` is a plain marker component with a name and a facing.

Order inside `HeroController.StepFixed`: `health.FixedStep` first, then dash, attack-begin, then wall / movement as
before, then attack-end. Why:

- `health.FixedStep` ticks the invulnerability and death timers. While the hero is stunned (or dead) it owns the
  velocity: it forces the knockback speed every step and returns true, and the controller then skips `HeroMovement` and
  the wall's second half. This is the same bypass the dash and the pogo use, because movement writes the vertical
  velocity every step (gravity) and would otherwise undo it. Gravity is therefore off during the stun, like the original.
- `HeroState.Hurt` / `Dead` are set by health and held: `HeroMovement` no longer writes the state when it is Hurt or
  Dead. When the stun ends the knockback stops dead, the state goes to Falling and movement runs in that same step
  (the dash uses the same hand-over).
- A landed hit cancels, at once and not a step later: a dash in progress, the wall cling and wall-jump push, the
  swing and its hitbox, the pogo bounce, and a jump in progress with its buffers (`HeroMovement.ResetForDamage`).
  Cooldowns (dash, swing) keep running. Dash, wall and attack also still end themselves if something else sets the
  state to Hurt or Dead, so a state set from outside is handled too.
- Hit rules: no damage for a hit of 0 or less; ignored while invulnerable, stunned or dead; ignored when the hit's
  source is the hero itself or one of its children (the hero's own swing never hurts it; `HeroAttack` also never offers
  the hero as a target).
- A landed hit: health in `PlayerData` goes down by the damage (floor 0). Away from the source along x (the hero is
  pushed right when it is at or to the right of the source), speed `HurtKnockbackX`, vertical `HurtKnockbackY`, forced
  for `HurtStunSeconds`; the hero faces the source. Invulnerability `HurtInvulnerableSeconds` starts at the hit and
  includes the stun. A hit without a source pushes away from the way the hero faces. `Damaged` is raised.
- Health 0: state Dead, velocity zero, all actions cancelled, `Died` raised once (a dead hero refuses further hits).
  Nothing moves it while dead. If a marker is known (the last `RespawnAt`, or a registered marker named in
  `PlayerData`) and `DeathRespawnDelaySeconds` is not negative, the hero respawns by itself after that delay; with no
  marker it stays dead.
- `RespawnAt(marker)`: all actions cancelled and also the dash and swing cooldowns and the spent air dash; stun,
  invulnerability and death cleared; health back to `maxHealth`; position, zero velocity, facing from the marker;
  state Falling (movement works out the rest next step); the marker name (and scene, when the marker's scene has a
  name) goes into `PlayerData`, and is saved with `SaveSystem` only when `SaveSlot` is set (default: no file is
  written); `Respawned` raised. A null marker logs a warning and changes nothing.
- Disabling the component clears dead, hurt and invulnerability and hands a Hurt / Dead state back to Falling (since
  movement no longer rewrites those states, they must not outlive their owner).
- Hit-stop stays OFF (owner decision). The original's freeze is not built and is a one-shot stub (`Hero damage hit-stop`),
  not a config flag, so `HeroConfig` did not need a new field. Nothing touches `Time.timeScale`.
- All timers are seconds reduced by the step length, so 50 and 100 Hz agree within one step.

No `HeroConfig` field was added: all four `Hurt*` fields already existed. The auto-respawn delay is a serialized field on
`HeroHealth` (not the frozen config).

## Comparison: prototype vs Silksong vs the new hero

Say "keep prototype" for a DIFFERENT row to put the prototype value into `HeroConfig` / the code instead.

| # | Parameter or rule | Prototype | Silksong (source) | New hero uses | |
|---|---|---|---|---|---|
| 1 | Invulnerability after a hit | 1.3 s (`invulnerableTime`) | 1 s (`INVUL_TIME`), starts with the recoil | 1 s (`HurtInvulnerableSeconds`), starts at the hit | DIFFERENT |
| 2 | Knockback horizontal speed | 9 (`damageRecoilVelocity.x`) | 15 (`RECOIL_VELOCITY`) | 15 (`HurtKnockbackX`) | DIFFERENT, **PROVISIONAL** (config value is flagged in the ledger; the 15 itself matches the prefab) |
| 3 | Knockback vertical speed | 5 | half of the horizontal speed = 7.5 | 0 (`HurtKnockbackY`) | DIFFERENT, **PROVISIONAL** (the ledger default 0 disagrees with the original's 7.5) |
| 4 | Knockback duration (stun) | 0.2 s (`damageRecoilTime`) | 0.2 s (`RECOIL_DURATION`) | 0.2 s (`HurtStunSeconds`) | same |
| 5 | Gravity during the knockback | off | off | off (movement is bypassed) | same |
| 6 | Steering during the knockback | ignored | ignored (no-input state) | ignored | same |
| 7 | Speed at the end of the knockback | stops dead | stops dead (`ResetMotion`) | stops dead | same |
| 8 | Knockback direction | away from the source x (at or right of it pushes right) | away from the damage side (left / right) | away from the source x, at or right of it pushes right; no source: away from facing | DIFFERENT (mechanism) |
| 9 | Hero turns to face the source | no (not read) | yes | yes | DIFFERENT |
| 10 | Hit refused while | invulnerable, in knockback, dead, respawning, god mode | invulnerable, recoiling, dead, damage mode none, many special states (parry, evade, shadow dash, ...) | invulnerable, stunned, dead; zero damage; own hit | DIFFERENT, special states deferred (no stub: those mechanics do not exist) |
| 11 | Zero-damage hit | refused | does nothing to the hero (may still notify the attacker) | refused | same |
| 12 | Hit-stop on damage | off in the asset (`heroDamageFreeze` None, code supports it) | yes: 0 s down, 0.1 s hold, 0.1 s up | OFF (owner decision); stub `Hero damage hit-stop` | DIFFERENT, by owner decision |
| 13 | Camera shake on damage | yes (`Average`) | yes (camera FSM) | none; stub `Hero damage effects and sound` | DIFFERENT, deferred |
| 14 | White flash, hit particles, sound, vibration | flash and burst | effects, sounds, controller vibration | none; same stub as row 13 | DIFFERENT, deferred |
| 15 | Invulnerability flicker | sprite alpha pulse | sprite pulse | none; stub `Hero invulnerability flicker` | DIFFERENT, deferred |
| 16 | Does invulnerability run during the freeze | yes (game time) | yes | n/a (no freeze) | n/a |
| 17 | Starting and maximum health | 5 in code, 100 in the saved tuning asset | 5 (new-game default) | `PlayerData` (5 by default) | DIFFERENT (prototype asset value) |
| 18 | Where health is stored | the health component | `PlayerData` | `PlayerData` (`Data`), saved through `SaveSystem` when a slot is set | DIFFERENT |
| 19 | Extra health types (lifeblood masks, fractured masks) | none | yes | not built, no stub (no such data field) | DIFFERENT, deferred |
| 20 | Damage by hazard type, boss-level scaling, tool modifiers | none (amount from the source) | yes | not built, no stub (no hazard or tool system); amount comes from the hit | DIFFERENT, deferred |
| 21 | Hit cancels the swing | yes | yes | yes (hitbox off, cooldown keeps running) | same |
| 22 | Hit cancels dash | yes | yes (dash ends with the state change) | yes, at once | same |
| 23 | Hit cancels wall slide | not read | yes | yes, at once | same |
| 24 | Hit cancels a pogo bounce | not read | yes (and zeroes vertical speed) | yes (vertical speed is set by the knockback) | same |
| 25 | Hit cancels a jump in progress | yes (motion reset) | yes (`ResetMotion`) | yes (`HeroMovement.ResetForDamage`) | same |
| 26 | Hit cancels nail charge, silk specials, binding | n/a | yes | n/a (not built) | n/a |
| 27 | Death trigger | health reaches 0 | health reaches 0 | health reaches 0 | same |
| 28 | Death raised once | yes (dead flag) | yes (returns if already dead) | yes (`Died` once; dead hero refuses hits) | same |
| 29 | Death presentation | camera shake, hero stays | hero hidden, death animation prefab, audio, vibration | state Dead, animator "Death"; stub `Hero death animation and corpse` | DIFFERENT, deferred |
| 30 | Silk / coins lost, cocoon corpse | none | yes | none; stub `Hero silk loss and cocoon on death` | DIFFERENT, deferred |
| 31 | Death screen, scene reload | none | yes | none; stub `Hero death screen` | DIFFERENT, deferred |
| 32 | Delay before coming back | 1.6 s (`deathRespawnDelay`) | set by the death screen and scene load (no number) | 1.6 s auto-respawn at the last known marker, serialized on `HeroHealth`; off if negative | DIFFERENT, **PROVISIONAL** |
| 33 | Where the hero comes back | the checkpoint position | the saved respawn marker in the saved scene (a bench) | `RespawnMarker` position and facing; name stored in `PlayerData` | DIFFERENT |
| 34 | Health on respawn | full | full after a real death | full | same |
| 35 | Invulnerability right after respawn | `invulnerableTime` (own addition) | none from a bench respawn | none | DIFFERENT |
| 36 | Hold still after respawn | 0.25 s (`respawnHoldTime`) | entry animation (bench sit / walk in) | none; stub `Hero respawn fade and bench pose` | DIFFERENT, deferred |
| 37 | Respawn clears actions | cancels attacks, holds | resets air dash, double jump, motion, attacks, input | resets dash (incl. cooldown and air dash), wall, swing (incl. cooldown), bounce, jump, stun, invulnerability, death | same in effect |
| 38 | Hazard respawn (spikes, acid, pits) | yes: hidden 0.5 s, back on the last safe ground (trail lagging 0.25 s), held 0.25 s, invulnerability `2 x time + 0.3` | yes: fade, last safe ground, invulnerability `INVUL_TIME * 2 + 0.3`, double hazard falls back to the room entrance | NOT BUILT: nothing in the scene is a hazard yet; documented here, no call site for a stub | DIFFERENT, deferred |
| 39 | Respawn marker data | checkpoint position only | marker name, scene, facing in `PlayerData` | marker name (and scene when the marker's scene has a name) in `PlayerData` | DIFFERENT |
| 40 | Saving on respawn | none | the death flow saves | only when `SaveSlot` is set; no save UI | DIFFERENT, deferred |
| 41 | Cheats / god mode | `godMode` flag | cheat manager invincibility | not built, no stub | DIFFERENT, deferred |
| 42 | Damage to a hit with no source | n/a | side from the hit's side | pushes away from the facing | DIFFERENT |
| 43 | Own attacks hurt the hero | no | no (attacks hit enemies only) | no: refused by source check; `HeroAttack` never targets its owner | same |
| 44 | State shown while hurt | `Damaged` | no-input actor state plus recoil flag | `HeroState.Hurt`, held by health | same in effect |

Count: 26 DIFFERENT rows (1, 2, 3, 8, 9, 10, 12, 13, 14, 15, 17, 18, 19, 20, 29, 30, 31, 32, 33, 35, 36, 38, 39, 40, 41,
42); 18 same or n/a (4, 5, 6, 7, 11, 16, 21 to 28, 34, 37, 43, 44; rows 16 and 26 are n/a). 44 rows in total.
Time base: every new-hero timer is seconds reduced by the step length, so 50 and 100 Hz agree within one step.

### PROVISIONAL (owner to confirm)

1. `HurtKnockbackX` 15 (row 2): the number is the original's `RECOIL_VELOCITY`; it is the damage recoil speed in the
   code I read, so it is probably right, but the ledger kept it flagged.
2. `HurtKnockbackY` 0 (row 3): the original pushes up at half the horizontal speed (7.5). `HeroConfig` is frozen, so
   the ledger value 0 is used. Changing the config default is the owner's call (the prototype uses 5).
3. Auto-respawn delay 1.6 s (row 32): the prototype's number, a stand-in for the original's death screen.
4. The knockback is a plain forced speed for 0.2 s; the original applies it after the freeze-frame, which is not built.

### Not built here (stubs and follow-ups)

Each logs once through `Stubs.NotImplemented`: `Hero damage hit-stop` (row 12), `Hero damage effects and sound`
(rows 13, 14), `Hero invulnerability flicker` (row 15), `Hero death animation and corpse` (row 29), `Hero death screen`
(row 31), `Hero silk loss and cocoon on death` (row 30), `Hero respawn fade and bench pose` (row 36).
Unbuilt without a stub because nothing exists to hang them on: hazard respawn and hazard damage amounts (rows 20, 38),
special invulnerability states (row 10), lifeblood masks (row 19), cheats (row 41), real save-on-death (row 40; the
`SaveSlot` switch exists but the real game scene must set it).

Open questions for the owner: is the original's upward knockback (7.5) wanted (set `HurtKnockbackY`)? Should the new
hero keep the short post-respawn invulnerability the prototype has (row 35)?
