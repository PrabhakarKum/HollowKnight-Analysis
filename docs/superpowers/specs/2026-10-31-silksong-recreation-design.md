# Silksong-structure recreation: design

Date: 2026-10-31. Status: draft for the owner's review. Nothing in this document has been built yet.

## Intent

The owner wants to learn how a high-end 2D action-platformer is put together by studying *Hollow Knight: Silksong* in depth, then rebuild its systems in their own Unity project (`Hollowknight-Analysis`, Unity 6000.0.84f1, URP 17, Input System 1.20). The point is understanding as much as the finished game: each system gets a plain-language write-up of how Silksong does it, then a working version the owner can run, tweak and test.

The owner told us they are on the Team Cherry team and have permission to use the game's code and assets. We took that on their word. Even so, this design writes each system by hand as an adaptation (see "Code approach"), because the original code cannot run in this project without being reworked anyway.

## Goals

- Recreate every Silksong system, delivered as playable milestones, one system at a time.
- For each system: a study page in `docs/` explaining the design and why it was built that way, then an implementation with "why" comments, then a check by the owner in Unity before moving on.
- Keep Silksong's structure and behaviour recognisable so the study carries over.
- Keep the owner's existing prototype player working and untouched while the new one is built.

## Non-goals

- No console, Steam, GOG or Xbox features (achievements, cloud saves, platform services).
- No PlayMaker, InControl or tk2d (2D Toolkit) dependencies in the owner's project.
- No importing the whole 13 GB export into Unity.
- Not a byte-for-byte reproduction of Team Cherry's files.

## Anti-goals (a failure even if it technically works)

- Silent gaps: a feature that quietly does nothing. Anything not yet implemented must log "not implemented yet".
- Overwriting the owner's tuned prototype values or breaking the prototype's test bot.
- A big dump of unexplained code the owner cannot follow.

## Constraints and the owner's standing rules

- Private repo; it will not go public.
- Do not overwrite hand-tuned values (nail hitboxes, Brawler maxHealth 5000, hero hit-stop off). Float stays off.
- Do not run "Reset Enemy Configs" or "Build Enemy Test Arena" without asking.
- Never commit or push unless asked. No Claude co-author line in commits.
- Third-party vendor libraries (PlayMaker, InControl, tk2d, Steamworks, GOG Galaxy, Xbox runtime, SharpDX) stay out of the project. Team Cherry's permission cannot waive those vendors' licenses.
- Report test results honestly.

## What exists today (facts found during discovery)

**Reference material**
- Decompiled Silksong scripts: `D:\Asset HollowKnight\HollowKnightSilksong\Assets\Scripts\Assembly-CSharp` (5,030 files; about 1,400 have no external dependencies). Game assemblies: `D:\Asset HollowKnight\AuxiliaryFiles\GameAssemblies`.
- Editable asset export: `D:\Asset HollowKnight Bundles\ExportedProject` (13.2 GB, Unity 6000.0.50f1): about 590 scenes, over 1,400 prefabs, 2,449 PNGs, 1,499 animation clips, 810 animator controllers, 9,360 audio clips. The agent that reported this could not confirm the tool that produced it, and the 41k `Sprite\*.asset` files need checking to see whether they still point at their textures.
- Silksong content ships as Addressables bundles; running those bundles directly is fragile, so the export is the working source.

**Dependency picture of the Silksong code (agent report, not independently verified)**
- 2,205 of the files are auto-generated PlayMaker action classes; about 288 other game files also use PlayMaker. Biggest cost: about 27 call sites in the hero controller and about 29 in the health manager.
- The game does not use Unity's Input System. It uses InControl (source included, commercial, modified).
- tk2d exists only as a compiled fork, no source. 234 files use it.
- Steam, GOG and Xbox code is confined to a handful of files.
- Core classes (`GameManager`, `HeroController`, `HealthManager`) each reach roughly 650 to 830 other classes within three hops of references, so nothing can be lifted out cleanly.

**Owner's prototype (`Hollowknight-Analysis`, about 9.8k lines, namespaces `Hollowknight.*`)**
- Player: motion-state controller, ScriptableObject tuning, combat, health/silk, hit feedback, a camera with look-ahead and room-lock zones, a test bot reporting 195 pass / 0 fail.
- Four enemies driven by `EnemyConfig` ScriptableObjects and an enum state machine.
- One scene, `SampleScene`. No audio, saves, real room transitions or bosses.

## Decisions made with the owner

| Topic | Decision |
|---|---|
| Scope | Full recreation of every system, built as playable milestones. |
| Workflow | One system at a time. Study page, then implementation, then the owner tests and approves, then move on. |
| Who writes code | Claude writes it with comments explaining why. The owner reads, tweaks, tests. For pieces they want to master, we switch to guided writing. |
| PlayMaker | Replace with plain C#. Real C# for behaviour the current milestone needs. A placeholder that logs "not implemented yet" for everything else. |
| Animation / sprites | tk2d content converted once to Unity sprites and animation clips, accessed through a small adapter over SpriteRenderer + Animator. |
| Input | Rewritten on the Input System package using the game's own action names (jump, attack, dash and so on). |
| Platform services | Replaced with a do-nothing stub. |
| Material from the export | Copy in only what each milestone needs. The export stays read-only beside the project. |
| Existing prototype | Stays untouched. The new hero lives in its own folder and test scene. Retire the prototype only once the new hero feels right. |
| Repo | Private. |

## Code approach (Claude's call, and a small change from the original Q13 answer)

Each class is studied in the Silksong source, explained in `docs/`, then implemented here by hand. The goal is the same structure, the same responsibilities and the same behaviour, with Silksong's naming kept where it helps the owner cross-reference. It is not a mechanical file copy, because every large class must be rewired around PlayMaker, tk2d and InControl anyway. Small dependency-free helpers can be ported nearly as they are.

## Architecture: the seams

Silksong's code is a web with a few hubs. We rebuild the hubs behind thin seams so the rest can be ported against them:

1. **Utilities**: object pool and poolable interface, enum flag helpers, global enums, extension methods.
2. **Data and saving**: a slim `PlayerData`, scene-level persistent data, a plain JSON file save (no BinaryFormatter, no platform save handlers).
3. **Input**: an Input System actions asset plus a wrapper exposing the hero's action properties.
4. **Platform**: a stub `Platform.Current`.
5. **Game manager facade**: scene transitions, game state, pause, respawn. Scene loading through `SceneManager`, with Addressables optional.
6. **FSM seam**: C# events/interfaces for the places the original calls into PlayMaker, plus a logging placeholder for the rest.
7. **Animation adapter**: one interface over SpriteRenderer + Animator in place of tk2d's sprite and animator types.
8. **Hero**, then **camera**, then **health and damage**, then **room transitions**.

## Milestones

1. Hero walking and fighting in one real room.
2. Room transitions between a few rooms.
3. Saving and loading.
4. Enemies and a boss.
5. Everything else: tools and crests, map, quests, UI, audio, cutscenes.

Milestone 1 order: the feasibility spike first (see below), then study pages on how the hero, game manager and data fit together, helpers, slim player data, input layer, and the hero controller ported in pieces so each piece can be tested as it grows. Milestone 1 is done when the exit-bar checklist passes in a real room with real hero art.

## Layout (Claude's calls, change freely)

- Code: `Assets/Silksong/` with subfolders per seam (`Core`, `Data`, `Input`, `Platform`, `Hero`, `Camera`, `Combat`, `Rooms`, `Anim`) and its own assembly definition `Hollowknight.Silksong`, so it compiles separately and cannot clash with the prototype in the default assembly.
- Namespace: `Hollowknight.Silksong`.
- Study pages: `docs/study/NN-<system>.md`. This design: `docs/superpowers/specs/`.
- Imported art, animation and scenes: `Assets/Silksong/Imported/`, added a piece at a time.
- Test scene for the new hero: `Assets/Scenes/SilksongHero.unity`, separate from `SampleScene`.
- Art: Milestone 1 uses the real room and hero art from the export (owner's choice). The owner's placeholder sprites are the fallback. Later milestones import more as needed.

## Known risks and unknowns

- **Prefab and scene links.** Prefabs and scenes in the export reference scripts by Unity GUID. Our hand-written scripts get new GUIDs, so imported rooms would show missing scripts. Candidate fix: reuse the original script's `.meta` GUID on our port so references hold. To be tested on one small room before we depend on it.
- **Unity version gap.** Export is 6000.0.50f1, the project is 6000.0.84f1. Expect shader and import fix-ups.
- **Sprite assets.** Whether the export's `Sprite\*.asset` files still reference their textures is unverified.
- **Pipeline.** Silksong's built-in-pipeline image effects do not work under URP; replace with URP volume effects.
- **Scale.** The hero controller alone is about 12k lines; it pulls in a global gameplay-settings object and many helpers. It will be ported in pieces.
- **Authorisation.** We are relying on the owner's statement about permission. We keep third-party vendor code out regardless.

## Calls made after the builder check (Claude's calls; the owner can change any)

- **Feasibility spike first.** Before any plan depends on the export, run one spike: check that the sprite assets resolve to their textures, try GUID reuse on one small room, and convert one tk2d animation. Fallback if the sprite assets are broken: re-slice the PNG atlases by hand or extract from the bundles.
- **Stubs are surfaced.** Each "not implemented yet" placeholder logs once per feature per session, and a running list of stubs is kept in `docs/study/STUBS.md`.
- **Testing.** For each system Claude writes a short manual checklist for the owner. The play-mode bot is extended only where it is cheap. Work is batched one system at a time, so the owner's review pace sets the pace.
- **Sequencing.** One study page, then its code, then the owner's review, per system. No large batch of study pages up front.
- **Input.** Keyboard and gamepad both work from the start, with default bindings taken from the original game. A rebinding UI is out of scope for Milestone 1.
- **Platform.** Windows desktop, 60 fps, pixel-perfect rendering to match the prototype.
- **Committing.** The owner decides what is committed. Because of size, `Assets/Silksong/Imported/` is gitignored by default; the owner can change that.

## Owner answers: Milestone 1 definition

- **Exit bar (accepted):** the hero can run; jump with variable height; dash; cling to a wall and wall-jump; swing the needle left, right, up and down with a pogo bounce off enemies hit from above; take damage, die and respawn. The room has one training dummy that reacts to hits. Silk skills, tools and crests are out of Milestone 1. Test: a manual checklist where the owner performs each action in the room and confirms it.
- **Art (owner chose Option B):** Milestone 1 uses a real Silksong room and the real hero art and animations from the export, not placeholders. The import is done at file level from the owner's own export, and nothing is redrawn. The owner's existing placeholder sprites remain available as a fallback if the import route fails.
- **Fidelity (accepted):** match Silksong's logic and timings as closely as they can be read from the source and the export's data, and judge "feels right" by playing next to the real game.

### Consequence of choosing real art for Milestone 1

The feasibility spike (sprite links, script GUID reuse on one room, tk2d-to-Unity animation conversion) is now the first task and sits on the critical path of Milestone 1. Nothing in the hero port is scheduled before the spike reports. If the spike fails, the fallback is the placeholder art in a hand-built room, and the owner is told before we switch.

## Left to the builder

- Exact folder and file naming within the layout above.
- Which simple room to use for milestone 1.
- How to slim `PlayerData` (which fields to keep first).
- Test approach for each piece (editor tests, extending the play-mode bot, or manual checklists).
- Order of pieces inside the hero port.
