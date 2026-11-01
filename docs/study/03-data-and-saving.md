# 03 - Player data and saving

Sources (read-only decompile, `HollowKnightSilksong/Assets/Scripts/Assembly-CSharp/`):
`PlayerData.cs`, `SaveGameData.cs`, `DesktopSaveRestoreHandler.cs`, `GameManager.cs` (save/load section).

## What the original holds

`PlayerData` is one very large class (thousands of lines) holding everything the game remembers:
build version, play time, health and silk, money, respawn location, map zone, items and tools,
quest and journal progress, visited-scene sets, and hundreds of story flags. A save slot is a
`SaveGameData` wrapper holding that `PlayerData` plus a separate `SceneData` (per-room state such as
opened doors and collected pickups).

Defaults are declared on the fields themselves with a default-value attribute; a field with no
attribute starts at zero/false/null. `SetupNewPlayerData` only builds the collections and the
starting tool crest; it does not set numbers.

## New-game values (recorded for `PlayerData.CreateNew()`)

| Field here | Original field | New-game value | Where |
|---|---|---|---|
| `health` | `health` | 5 | `PlayerData.cs`, field declaration near top |
| `maxHealth` | `maxHealth` (also `maxHealthBase`) | 5 | same |
| `silk` | `silk` | 0 (no default declared, so zero) | same |
| `maxSilk` | `silkMax` | 9 | same |
| `respawnScene` | `respawnScene` | `Tut_01` | same, in the respawn block |
| `respawnMarker` | `respawnMarkerName` | `Death Respawn Marker Init` | same |

Note: `silk` is current silk and `silkMax` the spool size; a separate `silkRegenMax` (default 0)
controls passive regeneration and is not needed yet.

## How the original saves

- One file per slot, in a data folder; slot files carry a numeric slot in the name and there are
  versioned backups of previous saves kept alongside (`DesktopSaveRestoreHandler`).
- The payload is JSON text, optionally encrypted and wrapped with a legacy binary serializer
  (`GameManager` save/load). Encryption is skipped on platforms with a protected file system.
- `PlayerData` carries a `version` string and a `RevisionBreak` number so loading code can detect
  saves from incompatible builds.

## What Milestone 1 keeps and why

Kept: `version`, `health`, `maxHealth`, `silk`, `maxSilk`, `respawnScene`, `respawnMarker`.
These are exactly what the hero, damage and death/respawn tasks read or write.

Deliberately dropped: money, items, tools, quests, journal, story flags, scene data, play time,
encryption, backups. Nothing in M1 uses them. `Stubs` is not called because no feature is
pretending to exist; these are simply out of scope.

Differences by design:
- `version` is a plain integer (`PlayerData.CurrentVersion = 1`), easier to compare than the
  original's string. A file with a higher number is never loaded or overwritten.
- No encryption and no binary serializer (security risk and unnecessary here). Plain JSON via
  `JsonUtility`.
- Atomic write: the new data goes to a temp file, then replaces the old one, so a crash mid-save
  keeps the previous save.
- A corrupt file is renamed to `*.corrupt` (kept for recovery) and defaults are returned.
- A missing file returns new-game defaults and writes nothing.

## Open questions

- Whether `Tut_01` is a real room in the exported scenes; Task 10 decides the actual respawn
  scene for the M1 test room.
