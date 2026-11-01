# Unity multi-agent workflow (parallel, branch-per-agent)

Status: draft for the owner's review. Nothing here has been run yet.

## Why this exists

Several agents can build different parts of the game at the same time, but Unity punishes naive parallelism: one Editor per project folder, scenes and prefabs that are single text files, one compile error that stops everything, and `.meta` GUIDs that clash. This workflow keeps agents apart by giving each its own git branch and worktree, its own folders, and its own assembly, and by sending every Unity run through one lane.

## The owner's rules (they apply to every agent)

- Agents commit **only** to their own private branch `agent/<name>/<task>`. Never to `main`. Never push.
- Merging into `main` happens only after review, and only after the owner says yes in chat.
- No Claude co-author line in commit messages.
- Do not overwrite the owner's tuned values: nail hitboxes, Brawler maxHealth 5000, hero hit-stop off. Float stays off. Do not run "Reset Enemy Configs" or "Build Enemy Test Arena".
- Third-party vendor code (PlayMaker, InControl, tk2d, Steam, GOG, Xbox, SharpDX) never enters the project.
- Report test results honestly, including failures.

## Roles

| Role | Does | Owns | Never does |
|---|---|---|---|
| Controller (the main session) | Plans, dispatches, merges after approval, keeps the ledger | the ledger, `main` merges | write feature code |
| **builder** | Writes code, tests and the study page for one task, in its own worktree | its folders and its assembly | open Unity; edit scenes, prefabs or project settings; touch frozen contracts |
| **qa** | The only role that runs Unity: tests, console, scene checks | the Editor and scratch project copies | change feature code |
| **integrator** | Edits scenes, prefabs and project settings, one queue item at a time | scenes, prefabs, `ProjectSettings` | write feature logic |
| **reviewer** | Checks a branch against the plan, spec and these rules | nothing (read-only) | change any file |

## Setup (once)

1. **Base branch.** The plan, spec and spike are not committed yet, so a worktree from `main` would not contain them. Make a private branch `silksong/base` in its own worktree, add `docs/` and `Assets/Silksong/` (without `Imported/`), commit, and have agents branch from it. `main` and the owner's uncommitted edits stay untouched.
2. **Ignore rules.** `.gitignore` on `silksong/base` adds `/Assets/Silksong/Imported/` and `/.superpowers/`.
3. **Assemblies.** Each area gets its own assembly definition (Task 2 of the plan creates the first), so one agent's compile error stays inside its assembly.
4. **Contracts frozen first.** Shared types (`IHeroInput`, `InputButton`, `HeroConfig`, `HitInstance`, `IHittable`, `AttackDirection`, `ISpriteAnimator`, `Stubs`) are written by one builder before any parallel work, committed on `silksong/base`, and then treated as read-only. A change needs the controller's ruling and a new base commit.

## Running a parallel batch

1. The controller picks tasks that touch **disjoint folders** and need only frozen contracts.
2. For each task: create a worktree at `..\Hollowknight-Analysis-wt\<name>` on branch `agent/<name>/<task>` from `silksong/base`. Code-only builders never open Unity, so they do not need a `Library` folder.
3. Builders work in parallel, commit to their branch, and write a report listing every file they touched.
4. The controller checks for overlap: if two branches touched the same file, they are not merged in parallel.
5. The **qa** agent takes each branch in turn, copies it into a scratch project, runs EditMode and PlayMode tests, and reports. Unity runs are serialized; they never run at the same time.
6. The **reviewer** checks each passing branch.
7. The **integrator** applies any scene or prefab work for the batch, one item at a time.
8. The controller shows the owner the results, and merges into `main` only after the owner agrees.

## What can run in parallel in Milestone 1

- Tasks 3 (data and saving), 4 (input) and 5 (platform stub and animation adapter) are independent once Task 2 has landed.
- Tasks 6 to 10 all edit `HeroController.cs`, so they run one after another.
- Task 11 (real room, art, camera) mixes script work with scene work: scripts to a builder, scene assembly to the integrator, after Task 10.

## Failure rules

- A builder whose tests fail twice stops and reports. It does not widen its folders to fix a neighbour.
- A merge conflict is never resolved by guessing. It goes back to the owning builder.
- If two branches both need a contract change, the controller serializes them.
- Anything touching `Library`, `ProjectSettings` or package manifests goes through the integrator.
