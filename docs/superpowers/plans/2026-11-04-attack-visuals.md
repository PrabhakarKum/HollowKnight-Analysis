# Attack visuals (hero swing animation and nail swipe)

Status: draft for the owner's approval. Nothing here is built yet. Source of truth for how Silksong does it: a new study page written in Task 1 (`docs/study/15-attack-visuals.md`).

## Goal

When the hero attacks, it looks like Silksong: the body pose plays at the right speed, the nail swipe arc is drawn in the right place and direction, the second swing alternates, and the down attack looks like the downspike. The attack hitboxes and all hand-tuned values stay exactly as they are.

## What already exists

- Body clips imported and played: `Slash`, `SlashUp`, `SlashDown` (+ `SlashDown Loop`); `SlashAlt` imported but never played.
- `HeroAttack` plays the clip when a swing starts; `HeroMovement` does not overwrite it during a swing.
- Stubs: alternate slash, downspike thrust, `Wall Slash` clip, attack clip speed (the `Slash` clip is 0.25 s, `AttackActiveSeconds` is 0.35 s, `ISpriteAnimator` has no speed).

## Missing

1. The nail swipe effect (a separate object in the original: `NailSlash` and related classes). Not built, sprites not imported.
2. Alternate swing, down-attack thrust look, wall slash clip, clip speed.

## Tasks

Each task: failing tests, run, implement, run, checkpoint. Same standing rules as before (private branches, builders never open Unity, QA is the only Unity runner, no co-author lines, no edits to `HeroConfig.asset` values or the nail hitboxes).

1. **Study** (read-only). How `NailSlash`, the slash effect animations, `SlashAlt` timing and the downspike are driven in the decompile; which effect sprites and clips exist in the export; numbers for effect position, scale and lifetime. Output `docs/study/15-attack-visuals.md` with a copy / simplify / skip table.
2. **Animator speed and clip timing.** Add a speed setting to the animator (additive) so the swing clip fits the attack time as in the original. Pure rules in EditMode.
3. **Nail swipe effect.** Importer for the effect clips, an effect object spawned per swing in the right direction and facing, removed when finished. It never touches the hitbox.
4. **Alternate swing and wall slash.** Alternate between `Slash` and `SlashAlt` using the original's reset timer; play `Wall Slash` while clinging.
5. **Down attack look.** The downspike clip and effect, without changing the existing bounce behaviour.
6. **Acceptance.** `docs/study/Attack-checklist.md`, a PlayMode run on the real hero asserting the right clips and effects, a full test run, then the owner plays it.

Order: 1, then 2, then 3, then 4 and 5, then 6.

## Known risks

- The effect art may need its own importer step; rooms or hero scene may need rebuilding after pulling.
- `HeroAttack` is touched in several tasks, so one builder at a time.
- Some timings are inferred from PlayMaker data that cannot be read in the decompile; the study page will say which.

## Attack visuals status

Tasks 1-6 are built. Task 6 adds `docs/study/Attack-checklist.md` (the owner's hands-on list with a result table, a "What the tests cannot judge" section and the stub list for this milestone) and the PlayMode fixture `AttackVisualsAcceptanceTests`. It steps a test hero by hand in open air (no generated rooms, no `RoomWorldFactory`, no `GameSession`) with the imported clips when they exist and same-named clips built in code otherwise, and asserts: side swing plays `Slash` with the swipe flipped by facing; a second quick swing plays `SlashAlt` with the alt swipe; after 0.70 s of step clock the next swing is `Slash` again; up plays `SlashUp` with the up swipe; down plays `SlashDown` with the down swipe on the facing side, hidden after about 0.10 s while the swing stays on; the hitbox size and offset equal `HeroConfig` for every direction; nothing overwrites the swing clip during the swing; the swipe is gone when the swing ends. Still open: QA has not yet run the new fixture or the full suite (the fixtures that use the real rooms currently hang on the QA machine for an unknown reason, so they are not part of this run), the owner has not played the checklist, and the owner decision on the drawn swipe (about 2.9 units) against the hand-tuned hitbox (1.35) and hit window (original 0.1 s, ours 0.35 s) is pending; nothing was changed for it. By hand only: art alignment, feel, and whether the real `SlashDown` loop ends cleanly.
