# 14b. Enemy core (Milestone 4, task 2)

What was built for the first enemy (the Moss Creep) before it walks anywhere. All code is in `Assets/Silksong/Enemy/`, namespace `Hollowknight.Silksong`. The numbers come from `docs/study/14-enemies.md` section 7. Written without opening Unity: nothing here has been compiled or run yet.

## The pieces

**SilksongEnemyConfig** (a ScriptableObject, menu Silksong/Enemy Config) holds every number of the enemy in one place. `SilksongEnemyConfigDefaults` has the same numbers as constants so tests can check them. No asset file exists yet; a later task makes it.

| Number | Value |
|---|---|
| Hit points | 2 (the original has 10 with a nail of 5; ours deals 1) |
| Contact damage / type | 1 / enemy |
| Flash | white, strength 0.85, hold 0.07 s, fall 0.05 s |
| Shove when hit | 15 u/s for 0.15 s (2.25 units) |
| Walk speed | 3 u/s |
| Back-foot ground ray | starts 0.6 above the box bottom, 1.1 long |
| Wall ray | 0.2 (+0.1 skin) |
| Ledge ray | 0.2 ahead of the front edge, 1.1 long |
| Body | box 1.64 x 1.11, offset (-0.03, -0.20), gravity 1, mass 1 |
| Stays dead | yes (a choice of the plan; the original creep is not persistent) |
| Persistence id prefix | "enemy" |

The walker (Task 3) has its own small struct; the integration task copies the walk and ray numbers from here.

**EnemyHealth** is what the hero's needle hits (it is an `IHittable`, found by `HeroAttack` the same way as any target). Rules:
- A hit with damage above zero on a living enemy takes that damage off the health. There is no "just hit" invulnerability window and no stun, so two quick hits both land.
- Events, in this order for one hit: `Hit(HitInstance)`, then `Shoved(velocityX, seconds)`, then `Died` if it was the killing blow.
- The shove goes away from the hit source along x at 15 u/s for 0.15 s. Only sideways swings shove; up and down hits do not (study row 18). The event only announces the shove; the walker applies it.
- Death: the contact damage is set to zero first, then renderers and colliders are switched off, then `Died` is raised once, then the object is disabled (or destroyed if `DestroyOnDeath`) after `RemoveDelaySeconds` (default 0). Hits after death are refused.

**EnemyRules** is the pure maths (damage, shove direction and speed, flash curve, contact damage when dead), tested in EditMode.

**EnemyHitFlash** blends the sprite colour toward white by the flash strength: full strength for 0.07 s, then a straight fall to zero over 0.05 s, then the colour is put back. With no SpriteRenderer it does nothing. `ManualStepping` and `Advance(dt)` let tests drive time.

**EnemyContactDamage** hurts the hero on touch. It copies how `HazardZone` works: the enemy's collider reports the hero, and each physics step it re-checks the heroes it knows about. A touch calls `HeroHealth.TakeHit` with damage 1 and the enemy as the source, so the hero's own invulnerability (1 s), stun and knockback do the rest. The hero's invulnerability is the only cooldown, as in the original. Only the hero's body counts: the needle's hitbox is a trigger on a child object and is ignored, otherwise swinging at an enemy would hurt the hero.

## Hero side
No hero code was changed. `HeroHealth.TakeHit` has no damage-type parameter, so the "enemy" type exists only as data on the config.

## Stubbed (`Stubs.NotImplemented`)
- Enemy hit sound and blood effects.
- Enemy corpse, death sound and drops.
- Enemy hit flash white-fill shader (a plain sprite tint cannot go brighter than white, so a real sprite needs a flash shader; the placeholder box is fine).

## Not built, on purpose
Invincible/tink hits, stun, damage scaling by needle level, the hero's 2-step downspike grace (lives in `HeroHealth`), silk gain (it is in the hero's attack code and already a stub there).

## Tests
- EditMode `Tests/EditMode/EnemyRulesTests.cs`: config defaults equal the study numbers, damage, shove, flash curve.
- PlayMode `Tests/PlayMode/EnemyCoreTests.cs`: a real nail swing hits and does not hurt the hero, 2 hits kill and `Died` fires once, shove direction both ways, no invulnerability, dead enemy refuses hits, contact costs 1 and not again inside the window, a dying enemy deals nothing, no contact after death, flash runs and ends.
