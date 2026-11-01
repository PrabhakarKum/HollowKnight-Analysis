# Combat: needle attack, pogo and training dummy

Summary in my own words. Sources, all read only: the attack code in `HeroController.cs` (`DoAttack`, `Attack`,
`DownAttack`, `DidAttack`, `CanAttack` and its helper, the bounce routines `Bounce`, `DownspikeBounce`, the recoil
routines), `NailAttackBase.cs` / `NailSlash.cs` (how a swing starts and is cancelled), `NailSlashRecoil.cs` (what a
hit does to the hero), `DamageEnemies.cs` (how a hit instance is built and handed to a target), `HitInstance.cs`,
`HealthManager.cs`, `HeroControllerConfig.cs`, `HeroSlashBounceConfig.cs`; the numbers in `Hero_Hornet.prefab`
(`BOUNCE_TIME` 0.25, `BOUNCE_VELOCITY` 12, `RECOIL_HOR_VELOCITY` 3.75, `RECOIL_HOR_STEPS` 8, `RECOIL_DOWN_VELOCITY` 0,
`ALT_ATTACK_RESET` 0.5, `DOWNSPIKE_REBOUND_SPEED` 20, `DOWNSPIKE_REBOUND_STEPS` 6) and the default hero config asset
(`attackDuration` 0.35, `attackCooldownTime` 0.41, `attackRecoveryTime` 0.15, `wallSlashSlowdown` on, `downSlashType`
0 = downspike); and the owner's prototype (`PlayerCombat2D.cs`, `PlayerController2D.cs`, `PlayerTuning.cs`,
`PlayerTuning.asset`, `HitTarget.cs`). The prototype was only read, never changed, and its hitbox numbers are copied
unchanged.

## How the original swings the needle

- A press starts a swing if no swing is running, the attack cooldown is over, the hero is not dashing (except a
  downward dash), not dead, not in a hard landing and has control. The direction is up if up is held, otherwise
  down if down is held and the hero is in the air, otherwise "normal" (the way the hero faces). The hero first turns
  toward a held sideways direction.
- A swing sets an "attacking" flag for `AttackDuration` (0.35 s) and a separate cooldown of `AttackCooldownTime`
  (0.41 s, never shorter than the duration) that both start with the swing. Attacking is a flag, not a state: the
  hero keeps running and falling during it.
- Alternating swings: a second slash prefab is used every other swing, the alternation resets after a pause.
- The slash prefab holds the damaging collider; the animation switches it on and off (not a fixed time in code).
  Each target is hit once per swing unless the damager is a "multi hitter".
- A hit is a hit instance (damage, direction, source) given to the target's health manager. The target decides
  whether it lands (invulnerable and dead targets refuse). Afterwards the hero reacts through a "recoil" helper: a
  sideways hit on an enemy knocks the hero back; an upward hit stops the hero rising (`RECOIL_DOWN_VELOCITY` 0).
  Terrain only does this if it carries a "Recoiler" tag (spikes), and an enemy carrying an active "non bouncer"
  marker never does it.
- Down swing: the default crest does not slash down, it does a short anticipation, a downward thrust (the
  "downspike") and on a hit a rebound jump (4 of the normal jump's steps already done, normal jump speed 18.6) that
  also gives back the air dash and double jump. Other crests slash down instead; their hits use the generic
  `Bounce()`: vertical speed forced to `BOUNCE_VELOCITY` (12) every step for `BOUNCE_TIME` (0.25 s), then set to zero.
  `Bounce()` gives back the air dash and double jump and is cancelled by landing, a ceiling, a wall slide and dashes.
- On a wall: a sideways swing halves the slide speed once (never faster than -5) and slashes away from the wall; an
  up swing steps the hero 0.8 off the wall and lets go; a down swing lets go.

## What the new hero does

`HeroAttack` is a component added by `HeroController.Awake` (like `HeroDash` and `HeroWall`). It owns a child object
`NailHitbox` with a trigger `BoxCollider2D`, disabled except during a swing.

Order inside `HeroController.StepFixed`: dash, then `attack.BeginStep` (before movement and wall), then wall and
movement as before, then `attack.EndStep` (after them). Why: the swing must start before the wall runs because a swing
on a wall changes the cling and the wall has to see the result in the same step; hit detection and the pogo run
afterwards because movement writes the vertical velocity every step (gravity), so the bounce has to overwrite it, in
the same way the wall cling overwrites it. A dash step refuses the swing.

- Press: swing starts if not swinging, the cooldown is over, not dashing, not hurt or dead. A refused press is logged
  by the stub `Hero attack buffer` (the original buffers it).
- Direction: up held, else down held and not on the floor, else sideways; down on the floor is an ordinary sideways
  swing, so it can never pogo. Sideways turns the hero toward a held direction.
- Hitbox: box from `HeroConfig` placed on the facing side (mirrored), enabled for `AttackActiveSeconds` (0.35 s),
  checked every fixed step with an overlap query on the trigger collider (so it is deterministic under scripted
  physics). The cooldown (`AttackCooldownSeconds`, never below the active time) runs from the start of the swing.
  Timers are seconds reduced by the step length, so 50 and 100 Hz agree within one 50 Hz step.
- Hit: anything with an `IHittable` on the collider or a parent. Each target is tried once per swing whether or not it
  landed. A hit instance (`Damage`, `Direction`, `Source`) is handed over; `HealthManager` subtracts it and raises `Hit`
  and `Died`. `TrainingDummy` counts hits and refills its health, so it never dies.
- Pogo: a landed hit from a down swing on a target without an active `NonBouncer` starts a bounce: vertical velocity is
  forced to `PogoBounceSpeed` (12) every step for 0.25 s after movement runs, then set to zero. Gives back the air dash
  (not the dash cooldown). Cancelled by a dash, wall cling or wall jump, landing, a head bump, hurt or dead. Plain
  ground and walls have no `IHittable`, so they never pogo.
- Wall: sideways swing keeps the cling, slashes away from the wall and halves the slide speed once (cap -5); up swing
  steps 0.8 off the wall and lets go; down swing lets go. After an up or down swing the wall may not grab again until
  the swing ends (`HeroAttack.BlocksWallCling`), otherwise a hero pressing into the wall would re-latch in the same step.
- Hurt or dead (the state set by another system) ends the swing and the bounce and switches the hitbox off; nothing
  starts while it lasts. Disabling the component resets swing, cooldown and bounce.
- `HeroState.Attacking` is not used: the swing is an overlay flag (`HeroAttack.IsAttacking`), like the original.
- Animation: `ISpriteAnimator.Play("Slash" / "SlashUp" / "SlashDown")` when an animator is assigned (placeholder names).

`HeroConfig` gained seven fields because it had no hitbox sizes or damage (everything else it already had):
`AttackSideSize/Offset`, `AttackUpSize/Offset`, `AttackDownSize/Offset` and `AttackDamage`. Defaults are the owner's
prototype values.

## Comparison: prototype vs Silksong vs the new hero

Say "keep prototype" for a DIFFERENT row to put the prototype value into `HeroConfig` / the code instead.

| # | Parameter or rule | Prototype | Silksong (source) | New hero uses | |
|---|---|---|---|---|---|
| 1 | Swing length (attacking flag) | 0.30 s | 0.35 s (`attackDuration`) | 0.35 s (`AttackActiveSeconds`) | DIFFERENT |
| 2 | Attack cooldown | 0.36 s, never below the swing | 0.41 s (`attackCooldownTime`), never below the swing | 0.41 s from swing start | DIFFERENT |
| 3 | Hitbox live window | from a strike keyframe (0.06 to 0.14 s in) for 0.14 s | animation-driven, no number in code | live from the first step for the whole 0.35 s (no wind-up) | DIFFERENT, **PROVISIONAL** |
| 4 | Hitbox shape and size | boxes: side 1.1 x 1.2 at (0.8, 0.15), up 1.4 x 0.8 at (0, 1.15), down 1.4 x 0.5 at (0, 0) | polygon colliders in the slash prefabs, not measurable here | the prototype boxes, now `HeroConfig` fields, unchanged | DIFFERENT, **PROVISIONAL** |
| 5 | How a hit is found | overlap query on a box each step | trigger collider on the slash, enabled by the animation | trigger child collider, overlap query each fixed step | DIFFERENT (mechanism only) |
| 6 | Direction choice | any nonzero vertical input; up wins; down only in the air | up pressed, else down pressed and (in air), else normal | vertical threshold 0.5; up wins; down only in the air | DIFFERENT (threshold) |
| 7 | Down held on the floor | ordinary forward swing | ordinary normal swing | ordinary sideways swing, no pogo | same |
| 8 | Turn toward held direction before swinging | yes | yes (forced facing) | yes (not while clinging) | same |
| 9 | One hit per target per swing | set of targets per swing | non-multi-hit damager hits once per swing | set of targets per swing; also tried only once if it refused | same |
| 10 | Multi-hit attacks | none | `multiHitter` option | not built (no such target) | DIFFERENT, deferred (no stub) |
| 11 | Alternating swing and its reset | two swings, reset after 0.4 s pause | second prefab, reset after recovery 0.15 + 0.5 s | not built (art only), stub `Hero alternate slash` | DIFFERENT, deferred |
| 12 | Attack press buffer | 6 steps (0.12 s) | queue of steps while the button is held (value not read) | not built, stub `Hero attack buffer` (logs a refused press) | DIFFERENT, deferred |
| 13 | Press during a dash | waits for the dash to end | not allowed (except downward dash) | refused | DIFFERENT |
| 14 | Damage per hit | 1 (`nailDamage`) | grows with needle upgrades (not read) | 1 (`AttackDamage`) | DIFFERENT, **PROVISIONAL** |
| 15 | Damage hand-over | hit target gets the direction only | hit instance with damage, direction, source; target decides | `HitInstance`, `IHittable.TakeHit` returns whether it landed | DIFFERENT |
| 16 | Which targets bounce the hero | `pogo` flag on the target | enemy layer without an active non-bouncer, or terrain tagged "Recoiler" | any landed hit on an `IHittable` without an active `NonBouncer` | DIFFERENT |
| 17 | Pogo off tagged terrain (spikes) | a target with the flag | yes (`Recoiler` tag) | not built; no tag system yet, so no stub | DIFFERENT, deferred |
| 18 | Pogo motion | a short jump, starts 3 steps in, cannot be cut short | default crest: downspike rebound jump (4 steps in, jump speed 18.6); other crests: `Bounce()` | the `Bounce()` model: 12 u/s for 0.25 s | DIFFERENT, **PROVISIONAL** |
| 19 | `PogoBounceSpeed` 12 and its time | n/a | 12 (`BOUNCE_VELOCITY`) for 0.25 s (`BOUNCE_TIME`), confirmed as the `Bounce()` numbers | 12 from `HeroConfig`, 0.25 s as a named constant (no config field) | same, whether it is the default crest's pogo is **PROVISIONAL** |
| 20 | End of the bounce | rise ends by itself | vertical speed set to 0 | vertical speed set to 0 | same (prototype differs) |
| 21 | Bounce cancelled by | landing, ceiling | landing, ceiling, wall slide, dashes | landing, head bump, dash, cling, wall jump, hurt, dead | same |
| 22 | Pogo gives back | air jump and air dash | air dash and double jump | air dash (cooldown untouched); double jump stub `Hero bounce refreshes double jump` | same, double jump deferred |
| 23 | Swing ends on pogo | yes | no, the swing runs on | no (the hit set stays, so no second hit) | DIFFERENT |
| 24 | Down swing is a thrust (downspike) | none | default crest: 0.1 s antic, speed 26 for 0.1 s, 0.25 s recovery | not built, slash only, stub `Hero downspike thrust` | DIFFERENT, deferred |
| 25 | Sideways recoil on hit | 5 steps at 8 u/s | 8 steps (0.16 s) at 3.75 u/s | not built, stub `Hero nail horizontal recoil` | DIFFERENT, deferred |
| 26 | Upward hit stops rising | none | vertical speed capped to 0 | not built, stub `Hero nail up recoil` | DIFFERENT, deferred |
| 27 | Hit-stop and camera shake | a short freeze and shake on the first hit | optional per attack | none (owner setting: hero hit-stop off) | DIFFERENT, by owner decision |
| 28 | Silk gained per hit | yes | yes | not built (no silk), stub `Hero silk gain on hit` | DIFFERENT, deferred |
| 29 | Wall: sideways swing | halves slide speed (cap 5) for the swing length | halves it once (cap -5), slashes away from the wall | halves it once (cap 5); movement and the wall cap then bring it back to full slide speed | DIFFERENT |
| 30 | Wall: up swing | pushes off 0.8 and cancels the slide | pushes off 0.8 and cancels the slide | same | same |
| 31 | Wall: down swing | cancels the slide | cancels the slide | cancels, and no new cling until the swing ends | DIFFERENT, **PROVISIONAL** (re-latch rule not read) |
| 32 | Hurt or dead | swing dropped | attacks reset | swing, hitbox and bounce dropped | same |
| 33 | Hard landing cancels a swing | yes | hard landing blocks attacks | no hard landing in the new hero yet | n/a |
| 34 | Crest and tool variants (charged, drill, quickening, anklet) | none | many | not built, no stub (no crest or tool system) | DIFFERENT, deferred |
| 35 | Hero state during a swing | flag | flag | flag (`HeroState.Attacking` stays unused) | same |

Count: 24 DIFFERENT rows (1, 2, 3, 4, 5, 6, 10, 11, 12, 13, 14, 15, 16, 17, 18, 23, 24, 25, 26, 27, 28, 29, 31, 34);
11 same or n/a (7, 8, 9, 19, 20, 21, 22, 30, 32, 33, 35). Row 5 is only a mechanism difference, rows 10, 17 and 34 have no
stub (no system to hang one on), row 27 is the owner's decision.
Time base: every new-hero timer is seconds reduced by the step length, so 50 and 100 Hz agree within one 50 Hz step.

### PROVISIONAL (owner to confirm)

1. Hitbox live window (row 3): the original's is animation-driven; the whole 0.35 s swing is used.
2. Hitbox sizes (row 4): the prototype's hand-tuned boxes, not the original's polygons. The down box (1.4 x 0.5,
   centred on the hero) lies inside a 0.5 x 1 hero collider, so it only reaches targets that overlap the hero; check it
   against the real hero collider size when the art arrives.
3. Damage (row 14): 1 per hit.
4. Pogo (rows 18, 19): the default crest's pogo is the downspike rebound (about 18.6 u/s for 0.1 s), not `Bounce()`
   (12 u/s for 0.25 s). `PogoBounceSpeed` 12 is the real `Bounce()` number, so it is kept; which feel is wanted is
   the owner's call.
5. Wall down swing re-latch block (row 31).

### Not built here (stubs and follow-ups)

Each logs once through `Stubs.NotImplemented`: `Hero attack buffer` (row 12, also a press during a dash),
`Hero alternate slash` (row 11), `Hero downspike thrust` (row 24), `Hero nail horizontal recoil` (row 25),
`Hero nail up recoil` (row 26), `Hero silk gain on hit` (row 28), `Hero bounce refreshes double jump` (row 22).
Unbuilt without a stub because nothing exists to hang them on: multi-hit attacks (row 10), pogo off tagged terrain
(row 17), crest and tool variants (row 34), hard-landing cancel (row 33).

The wall interaction stub from Task 8 ("attack interaction while latched", row 22 of the Wall table) is gone: it is
built, see rows 29 to 31.
