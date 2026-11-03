# 14d. Enemies that stay dead (Milestone 4, task 4)

Plain-language summary. Unity was not opened; nothing here has been run.

## What stays dead

An enemy with the `EnemyPersistence` component, with `StaysDead` on, stays dead once killed:

- through leaving and re-entering its room,
- through a bench rest,
- through the hero dying and respawning,
- through quit and continue (the flag is part of the save file).

It never comes back, because nothing in this project clears the flag. Only loading a different save (different `PlayerData`) changes it.

## How it works

- The flag is one "done" bool in the current `PlayerData.sceneData`, keyed by scene name + the enemy's id. A `PersistentBool` on the same object does the storage, so the id rule, the warnings and the "read the current data at the moment of use" rule are exactly those of breakable walls and pickups (see `13c-rooms-that-remember.md`).
- When `EnemyHealth` raises `Died`, the flag is written at once (not when the room is left).
- On `Start`, and again when `GameSession` re-applies memory to a freshly loaded room (`EnemyPersistence.Reapply`, called beside the wall and pickup calls), an enemy whose flag is true is hidden: contact damage zeroed, walker stopped, object deactivated. It can neither hurt nor be hit.
- `StaysDead` off: nothing is read or written; the enemy returns on every visit.
- The decisions are in the pure static class `EnemyPersistenceRules`.

## The id rule

The id must be written by hand, unique inside its scene, and never changed afterwards (it is part of the save key). An empty id or two enemies sharing an id in one scene log the same warnings as other persistent objects. Two enemies with different ids are independent.

## Adding it to an enemy

Put `EnemyPersistence` next to `EnemyHealth`. It adds a `PersistentBool` itself. Set `Id` (for example `Tut_01_Creep_1`). The integration task sets `StaysDead` from `SilksongEnemyConfig.staysDead` through `Configure(id, staysDead)`; the default on the component is true.

## What the original does differently

Ordinary enemies in the original are not persistent: they return every time the room loads. Only enemies with a `PersistentBoolItem` stay dead, and "semi-persistent" ones come back after the hero dies (study 14, section 5). Whether a bench rest resets them is not visible in the C# code. Here the first enemy stays dead as a deliberate choice, so none of those resets are built.

## What is not done

- Semi-persistent enemies (return after hero death) and any bench-rest reset. No stub was added: nothing is half-built, the flag simply is never cleared. If a respawn rule is wanted later it is a new feature, not a missing piece of this one.
- `Reapply` only hides. If new data says "not dead" for an enemy already hidden by this component, it is not revived (its walker is stopped for good); a fresh room load does start it alive.
- No corpse that stays, no `ignorePersistence` equivalent.
