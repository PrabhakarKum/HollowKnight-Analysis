# Milestone 4: the first enemy

Status: draft for the owner's approval. Nothing here is built yet. Source of truth for how Silksong does it: a new study page written in Task 1 (`docs/study/14-enemies.md`).

## Goal

A Silksong-style enemy stands in one of the grey-box rooms. It patrols, notices the hero, chases or attacks according to its Silksong behaviour, and hurts the hero on contact. The hero can hit it with the nail: it flashes, is knocked back, and dies after enough hits. A dead enemy stays dead after leaving the room and after save and load. The hero feels the hit (nail recoil, silk gain), which are stubs today.

## What already exists

- `HealthManager` and `HitInstance` (the hero's nail already damages anything that takes a hit), hero damage and respawn (`HeroHealth`), hazards, rooms that remember (`PersistentBool`), saving and loading.
- Stubs waiting for this milestone, from `Stubs.NotImplemented` in `HeroAttack`: silk gain on hit, nail recoil (horizontal and up), downspike bounce.
- Separately, your own earlier prototype has Archer, Brawler, BladeThrower and Gunner enemies with hand-tuned configs under `Assets/ScriptableObjects/Enemies` and code under `Assets/Scripts`. They are not Silksong code.

## Owner decisions (2026-11-03)

| Question | Decision |
|---|---|
| Scope | The first enemy in a Silksong room, not a boss, not tools and map. A boss is a later milestone. |
| Your old enemies | Leave them alone. The Silksong-style enemy is new, separate code in `Assets/Silksong`. Your four enemy configs, `Brawler.asset` and their tuned values are not touched or reused. |

Decisions after the Task 1 study (`docs/study/14-enemies.md`):

| Question | Decision |
|---|---|
| First enemy | The Moss Creep (MossBone Crawler): 3 in Tut_01b, 8 in Tut_02. It only walks at 3 u/s, turns at walls and ledges, and hurts on touch (damage 1). The "notice, chase, attack" brain in Tasks 2 and 3 shrinks to Walking, Turning and Dead. A chasing or shooting enemy is a later milestone. |
| Stay dead | A per-enemy "stays dead" setting, on for the first enemy so the saving can be seen. The original lets ordinary enemies return on every room load. |
| Hits to kill | 2 nail hits (health 2 with our nail damage of 1). |
| Hit reaction | White flash (0.07 s hold, 0.05 s fall, 0.85 strength), a 15 u/s x 0.15 s shove while it keeps walking, no stun and no invulnerability window. A dying enemy stops dealing damage. |
| Hero feedback | Silk +1 per damaging nail hit (capped). After a downspike hit the hero ignores enemy contact for 2 steps; because that rule lives in `HeroHealth`, Task 5 may also make a minimal additive edit there. |
| Art | Placeholder shape first; the real `Walk` / `Turn` / death clips can follow, since the existing converter is generic. |

Standing rules apply: private branches `agent/builder/*` in worktrees; builders never open Unity; QA is the only Unity runner (quote paths with spaces; scratch copies only); merge into `main` only after the owner says yes; no co-author lines; do not touch `SampleScene`, `Brawler.asset`, `HeroInputActions.inputactions` or `HeroConfig.asset` values; keep third-party vendor code out; every "not implemented yet" feature logs through `Stubs.NotImplemented`; test code can only use public members of the product assembly.

## Tasks

Each task: failing tests, run, implement, run, checkpoint.

1. **Study.** Read-only. How Silksong enemies work: `HealthManager`, hit and hurt effects, knockback and stun, `DamageHero` contact damage and its rules, death and corpse, how an enemy is made persistent, and the patrol / chase / attack logic of the simplest enemy that appears in Tut_01, Tut_01b or Tut_02. Picks the first enemy (a simple walker with one attack) and says why. Output `docs/study/14-enemies.md` with a copy / simplify / skip table and the exact numbers.
2. **Enemy core** (needs 1). `Enemy/SilksongEnemyConfig` (a new ScriptableObject, not the old one), `Enemy/EnemyHealth` (hits, knockback, hit flash, stun, death), contact damage to the hero through the existing hero damage path, death event. Tests with fakes in PlayMode, pure rules in EditMode.
3. **Enemy brain** (needs 1; parallel with 2). `Enemy/EnemyBrain`: a small state machine for the chosen enemy (patrol, turn at ledges and walls, notice the hero, chase or attack, recover). Pure and EditMode-testable, plus a PlayMode movement shell. Numbers from the study.
4. **Enemies that stay dead** (needs 2). Death written to scene data under an explicit id; the room applies it on entry; the rule for what a bench rest or a respawn does to dead enemies follows the study. Saved and loaded with the slot.
5. **Hit feel on the hero** (needs 2). The only task that edits `HeroAttack`: nail recoil (horizontal and up), downspike bounce on enemies, silk gain on hit, additive config fields only. Existing values do not change; nail hitboxes stay as hand-tuned.
6. **Place and acceptance** (needs 3, 4, 5). The grey-box builder gets one enemy in the room the study picks (additive; no existing coordinate changes), `docs/study/M4-checklist.md`, a PlayMode walk: hero meets the enemy, is hurt, kills it, leaves and returns, saves, loads, and it is still dead. Full test run, then the owner plays it.

Order: 1, then 2 and 3 in parallel, then 4 and 5, then 6.

## Known risks

1. Silksong enemy logic is mostly PlayMaker state machines, which cannot be read in the decompile. Behaviour and some numbers will be inferred from the C# actions they call, and the study page will say so, as it did for the bench.
2. Enemy art: the hero art comes from the exported Knight animation library. The enemy's art and animations may need their own importer; if so it is a separate small task and the grey-box version uses a placeholder shape first.
3. `HeroAttack` is touched only by Task 5, so there is a single writer.
4. Rooms must be rebuilt by the owner after pulling changes (the builders are menu items).
5. Fixture leaks (scenes, statics) have bitten every milestone; every new PlayMode fixture reuses its empty scene by name and resets statics.

## Left open for later milestones

Bosses (health bar, phases, arena lock), enemy drops and money, more enemy types, hit-stop and screen shake, enemy audio, real art and animation polish, tools, map and UI.
