# 14. Enemies (Milestone 4, task 1: study only)

Summary in my own words. Nothing was built and Unity was never opened. Sources, all read only:

- `src/` = `D:\Asset HollowKnight\HollowKnightSilksong\Assets\Scripts\Assembly-CSharp`: `HealthManager.cs`, `DamageHero.cs`, `HeroBox.cs`, `HeroController.cs` (`TakeDamage`, `CanTakeDamage`, `NailHitEnemy`, `SilkGain`, `AddSilk`, `RecoilLeft/Right/Down`, `DownspikeBounce`, `StartDownspikeInvulnerability`), `NailSlashRecoil.cs`, `HeroDownAttack.cs`, `Downspike.cs`, `NonBouncer.cs`, `Recoil.cs`, `Crawler.cs`, `EnemyHitEffectsRegular.cs`, `EnemyDeathEffects.cs`, `SpriteFlash.cs`, `DamageEnemies.cs`, `PersistentItem.cs`, `PersistentBoolItem.cs`, `SceneData.cs`, `GameManager.cs`, `PlayerData.cs`, `GlobalEnums/PhysLayers.cs`.
- `scenes/` = `D:\Asset HollowKnight Bundles\ExportedProject\Assets\Scenes\Hornet\Tut_01.unity`, `Tut_01b.unity`, `Tut_02.unity` (the enemies placed there and their saved component values), `Prefabs\Heroes\Hero_Hornet.prefab` (slash multipliers), `Animations\Hornet Enemies\MossBone Crawler Anim.prefab` and `Collections\Hornet Enemies\MossBone Crawler Cln Data\` (art).
- Our code: `HealthManager`, `HitInstance`, `HeroAttack`, `HeroHealth`, `HazardZone`, `PersistentBool`, `BreakableWall`, `HeroConfig`, `PlayerData`.

The owner's old prototype enemies were not read and are not used.

## 1. What an enemy is in Silksong

An enemy is one game object with a small stack of ordinary components, not a class hierarchy:

| Part | Job |
|---|---|
| `HealthManager` | hit points, takes hits, dies |
| `DamageHero` | the "I hurt the hero when touched" data (damage 1, type "enemy") |
| `Recoil` | the little shove an enemy gets when hit (speed and duration are numbers on the component) |
| `EnemyHitEffectsRegular` + `SpriteFlash` | white flash and blood puff on a hit |
| `EnemyDeathEffects` | what is left behind when it dies (corpse, sound, journal) |
| a movement script | for the Moss Creep this is `Crawler.cs`, plain C# (not a PlayMaker graph) |
| `Rigidbody2D` + `BoxCollider2D` | on the physics layer "Enemies" (11) |
| optionally `PersistentBoolItem` | only if the enemy must stay dead (see section 5) |
| optionally one or more PlayMaker graphs | the brain of every "real" enemy; unreadable here |

The enemy's solid box is also what hurts the hero: the hero carries a trigger box (`HeroBox`) and any enemy box that overlaps it is looked at for a `DamageHero`. So there is no separate "attack hitbox" for contact damage.

## 2. Hit and hurt rules (hero hits enemy)

Read from `HealthManager.Hit` / `TakeDamage` (src `HealthManager.cs:763`, `926`).

1. A dead enemy ignores the hit. A hit with zero damage does nothing (unless it is a "weak hit").
2. "Invincible" enemies (the `invincible` flag, with an optional protected direction) turn the hit into a clink: spark, sound, and the hero is pushed back sideways, but no damage. The Moss Creep is not invincible (`invincible: 0`). Not built in M4.
3. Otherwise the damage is multiplied by a per-needle-level multiplier (all 1 on the Moss Creep), then `hp -= damage`. There is no "invulnerable after being hit" window on the enemy (`NonFatalHit`, `:1404`, only handles an optional alternate hit animation). The only repeat protection is on the attacker: a nail swing hits each enemy once.
4. In the same call, for a nail hit: the enemy's `Recoil` is told to shove (`:1072`), the hero is told it hit something (`NailHitEnemy`, `:1086`), the hero gains silk (`:1093`), the white flash and blood effects play (`:1259`), and an event "TOOK DAMAGE" goes to the enemy's graphs.
5. If hp is still above 0 the enemy carries on (event `TookDamage`). If it is 0 or below, `Die` runs.
6. Direction: the hit carries a direction angle; the enemy turns it into one of four (0 right, 1 up, 2 left, 3 down) and recoils that way. The nail's direction comes from the swing, not from where the hero stands.
7. Damage numbers on screen: none exist in `HealthManager` or the effects classes. Skip.

Nail damage: `PlayerData.nailDamage` is 5 at needle level 0, then 9, 13, 17, 21 (src `PlayerData.cs:2886`). The normal slash objects in `Hero_Hornet.prefab` use a damage multiplier of 1 and a recoil magnitude of 1 (one secondary damager at line 39096 has 0.5 and 0; not examined). So one starting-needle hit = 5. **Enemy hp in the original is therefore about 5 times what our hero deals** (our `HeroConfig.AttackDamage` is 1).

### Flash and shove (the numbers)

- Hit flash: white, strength 0.85, rise 0 s, hold 0.07 s, fall 0.05 s, so about 0.12 s in total (`SpriteFlash.FlashEnemyHit`, `:681`).
- Shove (`Recoil`, src `Recoil.cs:117`): distance = `recoilSpeedBase x magnitude` per second for `recoilDuration`. Moss Creep: 15 u/s for 0.15 s, so 2.25 units along the hit direction, stopped early by terrain. A stronger shove replaces a weaker one in progress; a weaker one is ignored. There is no stun state on the Moss Creep: it has no "Stun" graph and it does not stop walking (see 7).
- Stun: other enemies may carry a "Stun Control" graph that takes `stunDamage` from hits (`:1423`); that is graph data and not needed for the first enemy.

### Death (`Die`, `:1536`)

In order, ignoring the parts that are only about drops and journals:

1. Refuses if already dead (also if the hero is dead and the enemy is set to not die after the hero).
2. Sends "ZERO HP" to its graphs; some enemies have a special death in the graph.
3. Sets `isDead`, **sets its `DamageHero.damageDealt` to 0** (`:1643`: a dying enemy can no longer hurt the hero), tells a battle scene, fires `OnDeath`.
4. `EnemyDeathEffects.ReceiveDeathEvent` spawns a corpse prefab flung at 22 u/s (Moss Creep), then, unless told to recycle, **destroys the enemy**. It also saves the persistent flag at that moment if there is a `PersistentBoolItem`.
5. A below-the-world check in `Update` (`y < -400`) kills the enemy.

Irrelevant for the first enemy: money and shell shard drops (the Moss Creep drops 2 shards), item drops, thief charm, reaper crest silk orbs, black thread, journal kills, lag hits, tag damage (poison, zap), moss extraction, critical hits, hunter combo, stun, alternate hit animation, battle scenes, steal hits.

## 3. Contact damage (enemy hurts hero)

Chain: hero trigger box (`HeroBox.OnTriggerEnter2D / Stay`, src `HeroBox.cs:41`) -> the touching object's `DamageHero` -> `HeroController.TakeDamage` (src `HeroController.cs:5211`).

- Moss Creep values: `damageDealt` 1, `hazardType` 1 (= enemy), no special flags (scene `Tut_01b.unity`, `DamageHero` &7400).
- If `damageDealt` is 0 or the component is disabled, nothing happens.
- Which side: the hero's own x compared to the enemy's x (right if the enemy is to the right). The hit is applied at the end of the same physics step (late update), so two overlapping damagers give the larger one.
- Refused while the hero: is invulnerable (1 s after a hit), is in the hurt recoil (0.2 s), is dead, is in a scene transition, is evading (scuttle), is shadow-dashing, is parrying or just parried, is in a "hazard only" mode, or **just landed a downspike on something** (2 physics steps, 10 for a long one; `DOWNSPIKE_INVULNERABILITY_STEPS`, `HeroController.cs:333`, `:11540`). Enemy hits are in addition refused if the hero's "bell bind" absorbs them (tools, skip).
- Enemy hits do NOT use the hazard override table (spikes 1, lava 2, ...). They use the damage on the component.
- On a landed hit with damage: mask lost, attack, wall slide, bounce and nail charge cancelled, sound, effects, hero recoil (15 u/s for 0.2 s, hero faces the hit, 1 s invulnerability; those numbers are already in `docs/study/06-damage-and-respawn.md` and in `HeroConfig`), and the damager gets its `HeroDamaged` event (the Moss Creep points it at its "Noise Reaction" graph, event WAKE).
- No contact cooldown on ordinary enemies: the hero's invulnerability is the cooldown. `DamageHero.SetCooldown` exists and is used by one case: a downspike that pogoes off a hazard that is not an enemy (no `HealthManager`) sets 0.5 s on that hazard so the bounce does not hurt (`HeroDownAttack.cs:68`).
- The "clash" parts of `DamageHero` (`canClashTink`, nail parry) are for armed enemies and are off on the Moss Creep. Skip.

What our hero already does (`HeroHealth.TakeHit`): refuses on invulnerable, stunned (the hurt recoil), dead and during a transition; knocks away from `HitInstance.Source`; same numbers. `HazardZone` is the model for "hit the hero through `TakeHit` from an overlap". An enemy needs the same: overlap the hero's collider, build `HitInstance{Damage 1, Source = enemy}`, call `TakeHit`. Missing compared to the original: the 2-step downspike grace (see section 4).

## 4. Hero feedback when it hits something

All from `NailSlashRecoil.HitResponse` (src `NailSlashRecoil.cs:42`) and `HealthManager.TakeDamage`.

**When it counts.** The hit must come from a damager whose own direction matches the hit direction. The target must be on the Enemies layer and have no active `NonBouncer` (src `NonBouncer.cs`: a component with an `active` flag, default true). Terrain only counts if it carries the "Recoiler" tag (spikes). The `NonBouncer` check is the whole definition of "a bouncer": every ordinary enemy is one.

**Sideways hit (hit direction right or left).** `RecoilLeft` / `RecoilRight`: the hero is pushed away from the enemy at `RECOIL_HOR_VELOCITY` 3.75 u/s for `RECOIL_HOR_STEPS` 8 physics steps (0.16 s, so about 0.6 units). It is refused if the hero is already recoiling sideways or does not have control (`CanRecoil`, `HeroController.cs:6423`). These numbers are already in `docs/study/05-combat.md` row 25. Stronger "long" recoil exists only for bounce mushrooms.

**Upward hit.** `RecoilDown`: if the hero is rising faster than `RECOIL_DOWN_VELOCITY` (0) the vertical speed is capped to 0 (`:6545`). Same as row 26.

**Downward hit (pogo).** The default crest uses the downspike: a thrust that on hitting any non-terrain, non-`NonBouncer` target starts the rebound jump (`HeroDownAttack.OnHitResponded`, `:108`; `Downspike.TryDownBounce`; `HeroController.DownspikeBounce`, `:6264`). The same hit starts 2 physics steps of "downspike invulnerability" so an enemy under the hero does not hurt it during the bounce. The generic `Bounce()` (12 u/s for 0.25 s) is for crests that slash down. Our `HeroAttack` uses the `Bounce()` model (PROVISIONAL in 05-combat); the 2-step grace is not built.

**Silk.** For a nail hit that did damage (weak, zero-damage hits give none): `HeroController.SilkGain(hitInstance)` adds **1** silk (`HeroController.cs:5766`) when the swing's silk setting is "Full", or only on the first hit of a swing for "FirstHit" (0 for "None"). Normal slashes are "Full" (the enum default is Full). Enemies of type Shade or Armoured, or with `doNotGiveSilk`, give none (`HealthManager.cs:1089`). `PlayerData.AddSilk` adds then clamps at the silk maximum (`PlayerData.cs:3706`); our `PlayerData` already has `silk` and `maxSilk` (9). The Moss Creep gives silk (`doNotGiveSilk: 0`, type 0). There is also silk-spool "moss" bookkeeping in `AddSilk` (`mossCreep1Hits`); it is only about the HUD and is skipped.

**Other hero reactions on a hit**: a freeze-frame ("hit stop") on critical hits only here; the owner switched hero hit-stop off. Camera shake and sound: skipped.

## 5. Persistence and respawn

Key finding: **ordinary enemies are not persistent.** They come back every time the room loads.

- An enemy is persistent only if it has a `PersistentBoolItem`. `HealthManager.OnAwake` hooks it (`:636`): the saved value is `isDead`; if the loaded value is true the enemy marks itself dead, fires `StartedDead` and deactivates itself. The flag is saved when the enemy dies (`EnemyDeathEffects`, `:379`) and with all persistent objects when the scene is left (`GameManager.SaveLevelState`).
- The key is scene name + item id; if the id is empty it falls back to the object's name (`PersistentItem.EnsureSetup`). Our `PersistentBool` insists on an explicit id; that is stricter and fine.
- `IsSemiPersistent` on the item decides what resets it. Semi-persistent items are reset by `GameManager.ResetSemiPersistentItems()` (`GameManager.cs:3798`). Its callers in C# are only the hero's death routine (`PlayerDead`, `:937`, and again after the respawn, `:989`). So: **dead hero = semi-persistent enemies return; leaving and re-entering the room does not bring them back.** I found no C# call from a bench rest, so whether a bench rest also resets them is not visible (the bench is a PlayMaker graph; see `docs/study/13d-bench-rest.md`). I am not going to claim it. The existing `PersistentBool` already carries a `semiPersistent` flag that only records the request.
- `HealthManager.ignorePersistence` turns persistence off (it saves `false`); the field `semiPersistent` on `HealthManager` is marked deprecated and unused.

What is placed in the first three rooms (counted from the scene files; hp is the saved `hp`; "persist" = has a `PersistentBoolItem`):

| Room | Enemy | Count | hp | Persist | Readable behaviour |
|---|---|---|---|---|---|
| Tut_01 | Moss Fly ("MossBone Fly") | 2 | 12 | no | flyer, sight detector + graph (graph unreadable) |
| Tut_01 | Bone Thumper | 1 | 45 | yes, fully (stays dead for good) | graph only |
| Tut_01b | Moss Fly | 3 | 12 | no | as above |
| Tut_01b | **Moss Creep ("MossBone Crawler")** | 3 | 10 | no | **all in `Crawler.cs`** |
| Tut_01b | Bone Roller | 5 | 15 | no | graph only |
| Tut_01b | Bone Thumper | 1 | 50 | yes, semi (returns after the hero dies) | graph only |
| Tut_02 | Moss Fly | 4 | 12 | no | as above |
| Tut_02 | **Moss Creep** | 8 | 10 | no | `Crawler.cs` |
| Tut_02 | Moss Cocoon ("MossBone Cocoon") | 4 | 1 | yes, semi | not examined |
| Tut_02 | Mossbone Mother Ambient | 1 | 350 | yes (non-semi) | a boss-sized thing; out of scope |

## 6. The first enemy: the Moss Creep

**Pick: the Moss Creep (`MossBone Crawler`)**, found 3 times in Tut_01b and 8 times in Tut_02.

Why:
- It is the only walking enemy in those rooms whose whole behaviour is visible in plain C# (`Crawler.cs`). Its one graph is "Noise Reaction", which only handles the needolin song and moss extraction (states Idle, Type, Wake Directional, Active, Sing, Sing End, Return Control, Extract), not combat. The Bone Roller and Bone Thumper are graph-only, so their numbers would all be INFERRED. The Moss Fly is a flyer.
- Walking, turning at walls and ledges, contact damage, 2 hits to kill with the starting needle. A clean test of everything M4 asks for.
- Its art is small (see 8).

**Honest difference from the plan:** the Moss Creep has **no sight, chase or attack**. It walks and turns, nothing else; its attack is touching it. The plan's brain (patrol, notice, chase or attack, recover) is more than this enemy does. The readable choice is: walk, turn, get shoved, die. If the owner wants a second behaviour that really exists in the game, that is a different enemy (Bone Roller or Thumper) and every number would be guessed.

### Behaviour, exactly (`Crawler.cs`, values from the Tut_01b instance, `Crawler` &6923)

- Each physics step the Creep sets its horizontal velocity to `speed` (3 u/s) in the direction it faces. It keeps walking while it is being shoved; the shove is a position push on top (`Recoil` moves the body by a sweep clipped by terrain), and when the shove ends the walk velocity is restored (`StartCrawling` hook on `OnCancelRecoil`, `:409`).
- Each step it checks `ShouldTurn` (`:336`), in this order:
  1. A ray straight down from the back edge of its box (0.6 units above the box bottom, 1.1 long). If that finds no ground (it is in the air, falling) it does not turn at all.
  2. A ray forward from near the front edge, just above the floor, length `max(rayFrontDistance 0.2, speed x dt) + 0.1` (about 0.3). Hits terrain: turn (a wall).
  3. A ray down from just beyond the front edge (`rayDownFrontPadding` 0.2 ahead), 1.1 long. Finds no ground: turn (a ledge).
  The rays hit layers 8 (Terrain) and 15 (`ENEMY_DETECTOR`), ignoring triggers (mask 33024). Layer 15 is a layer for invisible "keep enemies in this area" walls.
- A turn: stops horizontal speed for that step, flips the sprite (scale x), plays the `Turn` clip over the walk (it does not wait for it because `moveWhileTurning` is on; `flipBeforeTurn` on), negates the velocity, and resumes walking next step. So a turn is instant in gameplay terms; the `Turn` clip is 3 frames at 12 fps (0.25 s) and is only the look.
- Does not turn while recoiling, and a turn can be forced from outside by the messages "GO LEFT / GO RIGHT" (used by the noise graph). Roof and wall variants exist (the script supports a crawler rotated to walk on roofs and walls; Tut_02 uses some). Skip: floor only.
- It starts walking when enabled (`startInactive: 0`, `ambientIdle: 0`), so it never idles.
- Contact damage 1, enemy type; collider box 1.64 x 1.11, offset (-0.03, -0.20); body: dynamic `Rigidbody2D`, gravity scale 1, mass 1, rotation frozen; layer Enemies (11); solid, not a trigger.

## 7. All the numbers

Source column: S = read from source or scene data (exact). INFERRED = my proposal, not in the data.

| # | Item | Value | Source or INFERRED |
|---|---|---|---|
| 1 | Hit points (original units) | 10 | S: `HealthManager.hp`, Tut_01b/Tut_02 scene data |
| 2 | Starting needle damage (original units) | 5 | S: `PlayerData.nailDamage`, `:2886` |
| 3 | Hits to kill with the starting needle | 2 | S: 10 / 5 |
| 4 | Our enemy hp with `AttackDamage` = 1 | 2 | INFERRED (keeps "2 hits"; the owner may want more) |
| 5 | Damage scaling per needle level | 1, 1, 1, 1, 1 | S: `damageScaling`, scene |
| 6 | Walk speed | 3 u/s | S: `Crawler.speed` |
| 7 | Front wall ray length | 0.2 (+0.1 skin, or speed x dt if larger) | S: `rayFrontDistance`, `Crawler.cs:350` |
| 8 | Ledge ray: forward padding / length | 0.2 / 1.1 | S: `rayDownFrontPadding`, `Crawler.cs:344` |
| 9 | Ground (back foot) ray length | 1.1 | S: `Crawler.cs:355` |
| 10 | Turn time in gameplay | one physics step; look = 3-frame `Turn` clip, 12 fps | S: `moveWhileTurning`, `flipBeforeTurn`, clip data |
| 11 | Sight, chase, attack, notice range | none | S: not in `Crawler.cs`; graph is noise only |
| 12 | Contact damage | 1 | S: `DamageHero.damageDealt` |
| 13 | Contact hazard type | enemy (1) | S: `DamageHero.hazardType` |
| 14 | Hero hurt: invulnerability, recoil time, recoil speed | 1 s, 0.2 s, 15 u/s | S: `Hero_Hornet.prefab` `INVUL_TIME`, `RECOIL_DURATION`, `RECOIL_VELOCITY`; already in `HeroConfig` |
| 15 | Hero downspike contact grace | 2 steps (10 long) | S: `DOWNSPIKE_INVULNERABILITY_STEPS` |
| 16 | Enemy shove speed x time | 15 u/s x 0.15 s = 2.25 u | S: `Recoil.recoilSpeedBase`, `recoilDuration` |
| 17 | Shove multiplier (nail) | 1 | S: slash `magnitudeMult` in `Hero_Hornet.prefab` (one secondary damager differs) |
| 18 | Shove direction | along the swing's direction; up/down shoves are clipped by terrain, ignore | S for horizontal; up/down INFERRED to skip |
| 19 | Stun, hurt animation | none | S: no stun graph, `hasAlternateHitAnimation: 0` |
| 20 | Post-hit invulnerability of the enemy | none | S: `NonFatalHit` |
| 21 | Hit flash | white 0.85, up 0, hold 0.07 s, down 0.05 s | S: `SpriteFlash.cs:683` |
| 22 | Corpse fling speed | 22 u/s | S: `EnemyDeathEffects.corpseFlingSpeed` (corpse prefab not read) |
| 23 | Death handover | `damageDealt` -> 0, then destroyed | S: `HealthManager.cs:1643`, `EnemyDeathEffects.cs:398` |
| 24 | Drops | 2 shell shards, nothing else | S: scene; SKIP (no money yet) |
| 25 | Silk per damaging nail hit | 1, capped at max (9 in our data) | S: `HeroController.SilkGain` |
| 26 | Hero horizontal recoil on hit | 3.75 u/s for 8 steps (0.16 s) | S: `RECOIL_HOR_VELOCITY`, `RECOIL_HOR_STEPS` |
| 27 | Hero up recoil | vertical speed capped to 0 | S: `RECOIL_DOWN_VELOCITY` 0 |
| 28 | Pogo | `Bounce()` 12 u/s for 0.25 s is what we have; the crest's real one is the downspike rebound | S (05-combat) |
| 29 | Collider box / offset | 1.64 x 1.11 / (-0.03, -0.20) | S: scene |
| 30 | Rigidbody | dynamic, gravity scale 1, mass 1, rotation frozen | S: scene |
| 31 | Sprite size | about 2.0 x 1.5 units (walk frame), 64 px per unit | S: collection `texelSize` 0.015625 |
| 32 | Walk clip | `Walk`, 6 frames, 12 fps, loop | S: library |
| 33 | Turn clip | `Turn`, 3 frames, 12 fps, once | S: library |
| 34 | Death clips | `Death Air` (5 frames), `Death Land` (3 frames), 12 fps | S: library; used by the corpse, which I did not read |
| 35 | Persistence of this enemy in the original | none | S: no `PersistentBoolItem` on it |
| 36 | Room placement for M4 | Tut_01b, on a flat floor stretch with a wall or ledge at both ends | INFERRED (original has 3 in Tut_01b at about x = 94, y = 62 in the real room; the grey-box room is 56 x 24) |

Everything that would be INFERRED if we had picked another enemy (ranges, wind-ups, attack damage, speeds of a chase) is simply not needed for the Moss Creep.

## 8. Art

- Sprites: `Collections\Hornet Enemies\MossBone Crawler Cln Data\atlas0.png` (512 x 1024) with its collection `MossBone Crawler Cln.prefab`. Clips: `Animations\Hornet Enemies\MossBone Crawler Anim.prefab` (14 clips; the ones we need: `Walk`, `Turn`, `Death Land`, `Death Air`).
- It is the same tk2d format as the hero's `Knight.prefab` library, with the same 1/64 unit texel size. `Tk2dAnimationConverter.ConvertFrom(library, libraryFile, clipName, outputFolder, outputName)` is generic (it finds the sprite collection by guid under the export's `Collections` folder), so **no new importer is needed**; only a small caller like `HeroAnimationImporter` that lists the four clips and writes them to a gitignored folder under `Assets/Silksong/Imported/`. I did not run it (no Unity). Open: the converter's handling of the `Death Air` wrap mode (1 = loop section) follows the same chain-into-loop rule as the hero's `Death`.
- Cheapest safe route: **placeholder first** (a coloured box with an arrow for facing, like the grey-box rooms), then a follow-up small task for the real clips. This keeps Tasks 2 to 6 independent of the art. A flip needs `localScale.x` negation as in `Crawler.FlipScaleX`; the sprite is drawn facing left by default (`spriteFacesRight: 0`).
- The enemy renders through tk2d materials in the original; we use plain Unity sprites like the hero.

## 9. Copy / simplify / skip, mapped to the plan

| Plan task | Copy as is | Simplify | Skip |
|---|---|---|---|
| **2. Enemy core** (config, `EnemyHealth`, contact damage, death event) | `hp -= damage`, dead refuses hits, damage zeroed on death, contact = `HitInstance{Damage 1, Source}` into `HeroHealth.TakeHit`, enemy layer, solid box, flash 0.07 s hold + 0.05 s fall at 0.85 | Keep using the existing `HealthManager`/`IHittable` (it already raises `Hit` and `Died`); shove as a short position push along the swing direction; death = disable and event (no corpse flight) | direction-blocking/invincible tinks, stun, damage scaling by needle level (single fixed value), drops, journal, hit effects prefabs, sound, lag hits, tag damage, silk orbs |
| **3. Brain** (patrol, turn, notice, chase, attack, recover) | walk at 3 u/s; turn from the three ray rules (back-foot ground, front wall, front ledge); turn is a one-step stop; keep walking during a shove; no turn checks while shoved | states reduce to Walking / Turning(1 step) / Dead; use 2D `Physics2D` queries through a small interface so the rules stay pure | **notice, chase, attack, recover** (this enemy has none); wall and roof crawling; needolin "Noise Reaction"; enemy-only barrier layer (use terrain for now) |
| **4. Stay dead** | scene + explicit id key (`PersistentBool`); save at death; apply on room entry by not spawning / deactivating; semi-persistent = back after the hero dies | a flag on the config "persistent" (default off in the original; the plan wants it on for this enemy, see differences) | bench-rest reset (not visible in C#; stub it), full vs semi in the same milestone (build semi only if cheap), `ignorePersistence` |
| **5. Hit feel on the hero** | silk +1 per damaging nail hit (clamp at max, none for non-silk enemies); sideways recoil 3.75 u/s for 8 steps; up hit caps rising speed at 0; pogo on any hittable without an active `NonBouncer` | hit direction from the swing; keep the existing `Bounce()` pogo; downspike 2-step contact grace as a tiny additive field if the owner wants it | long recoil, drill pull, bounce mushrooms, "Recoiler" terrain, hit-stop, shake |
| **6. Place and acceptance** | place in Tut_01b (the original's room for 3 Moss Creeps); spec-based, additive | one enemy on flat floor | the other 10 creeps, flies, rollers, thumper, cocoons, mother |

### Differences from what the plan says

1. **"Notice the hero, chase or attack"**: the Moss Creep does neither. The brain is smaller. (If the owner wants a chasing enemy, the study gives no readable one in those rooms.)
2. **"Enemies that stay dead"**: the original's Moss Creep is not persistent; it returns every visit. The plan's behaviour is a deliberate choice and matches the original's *Bone Thumper* (persistent) and *Moss Cocoon* (semi-persistent). Recommend: the config has a flag, the first enemy sets it on, and the rule for a hero death is "semi-persistent enemies return", copied from `GameManager.PlayerDead`. The bench rule is unknown (not in C#).
3. **"Dies after enough hits"**: in the original it is 2 hits with the starting needle (hp 10, nail 5). Our nail does 1, so hp should be 2 (or raise nail damage; `HeroConfig` is frozen).
4. **"Hit flash, knocked back, stun"**: the Moss Creep has a flash and a 2.25-unit shove but **no stun** and no invulnerability window.
5. **Contact damage of a dying enemy**: the original zeroes it at death; the plan does not say. Add it.
6. **Downspike grace**: the hero ignores enemy contact for 2 steps after a downspike hit. It is in `HeroHealth` territory, not in the plan's file list.
7. **Pogo / "what counts as a bouncer"**: confirmed as the plan assumed (`NonBouncer`), plus the original also treats a "bounce balloon" as non-bouncing (skip).

### Open questions (for the owner or QA)

1. Hp in hits: keep the faithful 2 (fast), or choose more? The numbers allow either.
2. Should the Moss Creep stay dead (as the plan says, unlike the original), and should a hero death bring it back (semi-persistent)?
3. Bench rest and enemies: not visible in the C#; keep as a stub?
4. Does the hero physically collide with enemies, or do they pass through each other? That is the Unity layer matrix (`ProjectSettings`), which I did not read. Our hero trigger overlap for damage works either way.
5. Gravity, mass and friction of the crawler body on our rooms; the original values are 1 and 1 but our physics scale may differ. QA should check it walks without sliding.
6. Hit freeze on hero hits, camera shake, sounds: skipped on purpose.
7. What the Creep's four child objects are (probably audio and effect holders); the root carries all the logic.

### Not determined

- The graph "Noise Reaction" details beyond state names; nothing in it affects combat as far as the state list shows.
- The corpse prefab and the death animation timing (they live in a prefab I did not open).
- The bench rest's effect on enemies.
- Behaviour of Bone Roller, Bone Thumper, Moss Fly and Moss Cocoon (graph data).
- The project's physics layer collision matrix.
