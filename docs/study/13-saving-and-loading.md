# 13 - Saving and loading (Milestone 3, task 1)

Plain-language summary of how the original does it, in my own words, and what we copy, simplify or skip.
Sources (read-only decompile, `HollowKnightSilksong/Assets/Scripts/Assembly-CSharp/`): `GameManager.cs`,
`SaveGameData.cs`, `SceneData.cs`, `PersistentItem.cs`, `PersistentBoolItem.cs`, `PersistentItemData.cs`,
`RestBench.cs`, `RestBenchHelper.cs`, `SaveStats.cs`, `UnityEngine/UI/SaveSlotButton.cs`, `UIManager.cs`,
`PlayerData.cs`, `HeroController.cs`, `Breakable.cs`, `CollectableItemPickup.cs`, `RespawnTrigger.cs`.
Line numbers below are from that copy ("GameManager.cs:4123").

## Important gap: the bench rest sequence is not readable

The bench rest itself (sit down, heal, save, fade, stand up) is a PlayMaker state machine on the bench
object. The decompile contains only the C# actions it calls, not the machine's order. So section 5 is
built from those actions and from what the hero code checks while resting. The exact order, fade length
and which input starts the rest could NOT be read. Treat the order in section 5 as inferred.

## 1. What triggers a save (and what does not)

The one place that writes a slot is the private `SaveGame(int slot, ...)` (GameManager.cs:4123). Public
wrappers: `SaveGame(callback)` (GameManager.cs:3932), `SaveGameWithAutoSave` (3944) and the queued
variants (3953-3975). It does these steps: copy every persistent object's state into the in-memory scene
data (`SaveLevelState`, 3793, 4146), stamp play time / version / slot number / date on the player data
(`PreparePlayerDataForSave`, 4342), build a `SaveGameData` and turn it into JSON on a background queue,
write the slot file, then write a backup copy. If it fails the player sees a "save failed" message.

Callers I could find in C#:
- **Death**: `PlayerDead` (GameManager.cs:933) saves right away (953), then waits for the respawn delay.
  It also resets the "semi-persistent" items (see 4). So the game saves at the moment of death, not at
  respawn.
- **Return to main menu** with "willSave" (GameManager.cs:5218).
- **Play Maker actions** `SaveGame` and `SaveGameV2` (the bench, story beats, shrines). The bench uses
  one of them; I cannot see which. `CreateAutoSaveSilent` makes a restore point.
- **Queued saves**: quest events and dialogue end (`QueueSaveGame`, `DoQueuedSaveGame`; used by
  `FullQuestBase.cs:919`, `NPCControlBase.cs:413`, `UIMsgPopupBase.cs:170`).

What does NOT save:
- **Changing room does not write the slot file.** `ChangeToScene`, `BeginSceneTransition` and the old
  `TransitionScene` only call `SaveLevelState()` (GameManager.cs:657, 888, 908). That copies persistent
  objects into the in-memory scene data so the room remembers when you come back, but nothing goes to
  disk. I found no `SaveGame(` call on scene change.
- A hazard death (`PlayerDeadFromHazard`, 993) also only calls `SaveLevelState`.
- A new game does not write a file at start (`StartNewGame`, 5050, `RunStartNewGame`, 5066). The slot
  stays empty until the first real save.
- Quitting the game from the pause menu without the save-and-quit option writes nothing.

So the owner decision "quietly after every completed room change" is OUR addition, not the original's.
That is fine, but it has a consequence for Continue (section 3).

Not the same thing as autosave: `CreateRestorePoint` (3989, 4331) writes extra named restore-point files
(a rolling set of older saves) that the player can pick from a menu. Skip.

## 2. What a slot file and a slot button contain

A slot file is `SaveGameData` (SaveGameData.cs): exactly two members, `playerData` and `sceneData`.
- `playerData`: version string and a "revision break" number, slot id, date, play time, health, silk,
  respawn scene/marker/type, a temporary respawn (used for special rooms), hazard respawn position and
  facing, map zone, and thousands of story flags. See study page 03.
- `sceneData`: three collections of "scene name + id + value" entries (bools, ints, and the breakable
  rock hit counts). See 4.

On disk it is JSON text that is encrypted and base64'd; a backup copy is written beside it and restore
points go in a per-slot folder (`DesktopSaveRestoreHandler`, `.dat` files).

The slot button does not load the whole save. `GetSaveStatsForSlot` (GameManager.cs:4551) reads the file
and builds a `SaveStats` (SaveStats.cs:62) holding: version and revision break, max health (the base
value, not current health), max silk, money, shell shards, MAP ZONE (the area, not the room), play time,
completion percent, ending icons, crest. The button (`SaveSlotButton.cs`) shows area name and area
background picture, play time as "1h 05m", health masks (max), silk spool, money, shards, completion.
It never shows the room name or current health.

Slot states (SaveSlotButton.cs:14-22): not started, operation in progress, empty, loaded stats, corrupted,
incompatible. Decision (ProcessSaveStats, 522): unreadable file gives corrupted; "blank" gives empty;
a version check says incompatible; else loaded. The version check (`SaveDataUtility.IsVersionIncompatible`,
SaveDataUtility.cs:41) treats a save as incompatible if its revision break is higher than the game's, or
the file's major version is higher. Pressing a corrupted slot only shows a "save corrupted" message
(SaveSlotButton.cs:714). Pressing an incompatible one does not load it (and offers restore points).

Four slots (`SaveSlot` enum, SaveSlotButton.cs:24). Pressing an empty slot starts a new game
(`ui.StartNewGame`, SaveSlotButton.cs:692/710); pressing a loaded slot continues
(`ui.UIContinueGame(slot, saveStats.saveGameData)`, SaveSlotButton.cs:~700). The loaded data is reused, so
the file is not read twice. Delete is two prompts: "clear?" then "really clear?"
(`ClearSavePrompt`, `ClearSaveConfirmPrompt`, SaveSlotButton.cs:832-841), then `ClearSaveFile`
(GameManager.cs:4528) deletes the file and the restore points.

## 3. How Continue restores the world

1. The slot is chosen; `LoadGame` (GameManager.cs:4426) reads and decodes the file, or the button hands
   over the already-read data (4394-4419).
2. `SetLoadedGameData` (4507): the loaded player data and scene data become THE current ones
   (`PlayerData.instance`, `SceneData.instance`), the slot number is remembered, and:
   **silk is set to 0** (GameManager.cs:4517) and the spool parts to 0. So silk is not restored.
   Nothing touches health, so health comes back as saved.
3. `ContinueGame` / `RunContinueGame` (5090 / 5095): fade out, load the shared object pool and the hero
   prefab, then `ReadyForRespawn(isFirstLevelForPlayer: true)` (1055).
4. `ReadyForRespawn` asks `GetRespawnInfo` (1010) for a scene and a marker. It uses the temporary
   respawn if one is set, else the saved bench: `respawnScene` + `respawnMarkerName`. If the scene is
   unknown it falls back to `Tut_01` + `Death Respawn Marker Init` and clears the temp respawn.
5. The scene loads with `RespawningHero = true`; `EnterHero` (3189) finds the marker, plays the
   wake-up, and `HeroController.Respawn` puts the hero there.

Consequences:
- **Continue always lands at the last bench / respawn marker, not at the room you quit in.** There is no
  "current room" in the original save used for loading. Quit after walking five rooms from a bench and
  you wake at the bench.
- Health: whatever the file says. After a death the respawn code refills it (`HeroController.cs:7046`
  runs `MaxHealth()` if the hero was dead). The death save is written before that refill, so a save
  taken at the moment of death may hold health 0; I could not confirm how the original handles
  quitting inside that 1.6 s window.
- Hazard respawn position is saved but is only meaningful in the same room.

## 4. How persistent objects store and re-apply state

Class names: `PersistentItem<T>` (abstract base), `PersistentBoolItem`, `PersistentIntItem`,
`PersistentItemData<T>`, `SceneData`.

**Key**: the pair (scene name, id). `PersistentItemData` holds `ID`, `SceneName`, `IsSemiPersistent`,
`Value` and a mutator (PersistentItemData.cs). If the id is empty it becomes the GAME OBJECT NAME; if
the scene name is empty it becomes the object's scene name with any sub-scene suffix removed
(`GetBaseSceneName`, GameManager.cs:3705; `EnsureSetup`, PersistentItem.cs:212). So two objects in one
room with the same name collide.

**Storage**: `SceneData` keeps, per scene name, a dictionary of id to item data (SceneData.cs:28-155). On
write it flattens to a list of entries (serialization 92-111), because dictionaries do not serialize.
`SetValue` (113) inserts, `TryGetValue` (122) reads, missing gives default.

**Write path**: every persistent item subscribes to `GameManager.SavePersistentObjects` when it wakes
(PersistentItem.cs:59). `SaveLevelState()` fires that event, so every item in the room asks its owner
"what is your value?" (`OnGetSaveState`) and stores it in `SceneData` (`SaveStateNoCondition`, 170-190).
This happens when LEAVING a room (GameManager.cs:657/908) and just before a disk save (4146). The value
is not written the instant a wall breaks.

**Read path**: in `Start` (PersistentItem.cs:90-116) the item looks itself up. If found, it hands the
value to its owner (`OnSetSaveState`) so the owner can put itself in the final state. If not found it
reads the owner's current value instead. `PersistentBoolItem` also has "disable if activated", which
turns the object off when the stored value is true (PersistentBoolItem.cs:325-335).

**Examples**:
- `Breakable.cs:425-441`: get-state returns `isBroken`; set-state sets `isBroken` and, if true, calls
  `SetAlreadyBroken()` (shows it broken with no effects, no drops). Breakables with a rosary pickup get
  the persistent component added automatically (Breakable.cs:417-423).
- `CollectableItemPickup.cs:497-506`: get-state returns `activated`; set-state sets it and `CheckActivation`
  removes the pickup.

**Semi-persistent**: some items are flagged to forget on death (`ResetSemiPersistentItems`,
GameManager.cs:1000; SceneData.cs:270-275). They are not even written to disk
(`OnBeforeSerialize` skips them, SceneData.cs:92-111; loading drops them, 48-60). Used for enemies and
loot that come back after you die. We do not need it yet.

The real files contain one subtle thing: the data lives in memory in `SceneData.instance`, which is
replaced wholesale on load (GameManager.cs:4507). Anything holding an old reference sees stale data.

## 5. What resting at a bench does

C# pieces I can see:
- `RestBench.cs:5-22`: a trigger. When the hero enters, `NearBench(true)`; when it leaves,
  `NearBench(false)` (HeroController.cs:8049, sets a flag `cState.nearBench`). That is the full
  C# of the bench object. It does not rest by itself.
- The rest is started by the player interacting (an up press on an interactable) and driven by the bench
  PlayMaker machine. I could not read that machine.
- `RestBenchHelper.cs`: when the bench object is turned off while the hero is on it, it clears
  `atBench`, re-enables gravity, ends the interaction, gives control back and returns the animation to idle.
  So the machine sets `atBench = true` (PlayerData.cs:172), turns gravity off and takes control.
- `SetDeathRespawnMarker` action (HutongGames/PlayMaker/Actions/SetDeathRespawnMarker.cs:9): calls
  `HeroController.SetBenchRespawn` (HeroController.cs:4842-4850) which calls `PlayerData.SetBenchRespawn`
  (PlayerData.cs:3748). That stores the marker name, the CURRENT scene name, the respawn type (1 if the
  marker object has a `RestBench`, else 0) and the current map zone as the respawn zone. This is what a
  bench "sets".
- Healing: `HeroController.MaxHealth()` (HeroController.cs:6212) sets health to the maximum
  (`PlayerData.MaxHealth`, PlayerData.cs:3636), clears temporary bonus health and sends a "healed to max"
  event. I believe the bench machine calls it but cannot confirm.
- Saving: the `SaveGame` / `SaveGameV2` actions (above) call `GameManager.SaveGame`.
- While `atBench` is true: the hero's gravity shove, charge effects, companions and the pause menu behave
  differently (HeroController.cs:3311, 7897; GameManager.cs:4858; CameraTarget.cs:179).
- `RespawnTrigger.cs:45-65` is a different thing: walking through the trigger sets the bench respawn
  without resting (once if single use). Our `RespawnMarker` "sets bench respawn" flag is the equivalent.

So in order (inferred): player presses interact near bench, input is taken and the hero sits and
`atBench` is set; bench respawn is set to this marker; health goes to max; the game is saved; hero
stands up, `atBench` cleared, control returned. Whether silk is refilled is not visible.

## 6. Decisions: copy / simplify / skip, mapped to the plan

| Area | Original | Decision | Plan task |
|---|---|---|---|
| Slot file content | `SaveGameData` = player data + scene data | Copy. Ours is `PlayerData` with `sceneData` inside; same two parts, one JSON file. No new format. | 2 |
| Encryption, base64, legacy binary | Used | Skip (already decided in M1). | 2 |
| Backup copy per save, restore points, version backups | Used | Skip. Our atomic temp+replace and `.corrupt` quarantine cover the crash case. | 2 |
| Cloud / platform save APIs, save icon | Used | Skip. | 2 |
| Slot summary | area name, play time, max health, silk, money, shards, completion | Simplify: room (or area), health, play time placeholder (Stub). Original shows max health and AREA, not room and current health; the plan says "room and health". Decide which. | 2, 6 |
| Slot states | empty / loaded / corrupt / incompatible | Copy as empty / ok / corrupt / newer. Original blocks loading corrupt and incompatible slots; ours never overwrites them without a choice (stricter, good). | 2, 6 |
| Delete | two prompts then delete file and restore points | Copy: one confirm is acceptable (plan says "with a confirm"); original uses two. | 6 |
| Save triggers | death, bench/shrine, quit-to-menu, quests | Copy bench and death. Add the room-change autosave (owner decision; NOT in the original). Skip quest saves. | 4 |
| Save on death | Saves at death moment, before respawn refill (GameManager.cs:953) | Differs: our `HeroHealth.RespawnAt` saves AFTER refilling health (HeroHealth.cs:197). Ours is safer; keep. | 4, 5 |
| Persistent object key | scene name + id, id defaults to object name | Copy the key. Do NOT default the id to the object name silently: require an explicit id or warn on duplicates. | 3 |
| Persistent write time | copied into scene data when leaving the room and before a disk save | Simplify: write straight into `SceneData` when the wall breaks or the pickup is taken. Same result, no event wiring. | 3 |
| Persistent read time | on object `Start` | Copy: on room activation, each persistent object reads and applies state before the first frame is shown. | 3 |
| Semi-persistent items | reset on death, never written | Skip (Stub if any object asks). | 3 |
| Breakable wall, pickup | `Breakable`, `CollectableItemPickup` with persistent item | Simplify to two small classes; pickup has no inventory yet (Stub the item grant). | 3 |
| Bench rest | PlayMaker machine; sets respawn, heals, saves, fade | Copy the outcome: set bench respawn, full health, save, short fade. Resting pose/animation, camera zoom, tool refill stay Stubs. Rest input is "up" on the existing Move input (owner choice; original uses an interact press). | 4 |
| `atBench` flag | stops gravity shove, enables menus | Simplify: block hero control during rest; no flag needed until something reads it. | 4 |
| Continue destination | last bench / respawn marker, always | See open question 1. | 5 |
| Silk on load | forced to 0 (GameManager.cs:4517) | Copy (our new game silk is already 0, but a loaded save must also zero it, and tests should check this). | 5 |
| Health on load | as saved | Copy. | 5 |
| Loading sequence | fade, build pools and hero, then respawn through the normal scene transition | Copy the idea: load through the same respawn path as death (`RequestRespawn`) rather than a new placement path. | 5 |
| Start screen | menu scene with 4 buttons | Simplify per plan. | 6 |
| Last used slot memory | `lastProfileIndex` stored (SaveSlotButton.cs:673) | Optional nicety; skip or Stub. | 6 |
| Play time | accumulated into player data each save (GameManager.cs:4349) | Stub (plan). | 2, 6 |

### Where this differs from the plan, and what the plan missed

1. **Continue destination (needs an owner answer).** The goal says "continue where you were: same room".
   The original always resumes at the bench, never at the room you left. Task 5 says "at the saved
   bench (or saved hazard spot ...) in the saved room", which mixes the two. Because the room-change
   autosave already records `currentScene`, we could resume in that room at its `Start` marker. Options:
   (a) copy the original: resume at bench, the room-change autosave then only keeps broken walls and
   pickups; (b) resume in `currentScene`. Under (a) a player who changes rooms and quits finds themselves
   back at the bench with the walls still broken, which matches Silksong exactly but may surprise the
   owner. I recommend asking.
2. **Slot display**: original shows area and MAX health, not room and current health. Plan text says
   "room and health shown". Cheap either way; decide wording.
3. **Room-change autosave and half states.** The original never saves on a room change, so there is no
   original rule to copy. Plan risk 2 is right: save only after `TransitionFinished`, not in the
   `RoomActivated` or `HeroPlaced` events, and not while the hero is dead (the death delay is running).
   An autosave must never write a hero with health 0 and an old bench.
4. **Autosave with a death in progress.** `HeroHealth.RespawnAt` already saves when a slot is set
   (HeroHealth.cs:197). Room-change autosave and respawn can fire close together after a cross-room
   respawn (`RequestRespawn` also raises a transition). Two writes in a row are harmless (atomic), but
   tests should check the final file has full health.
5. **Silk reset on load** is easy to miss. Plan does not mention it.
6. **New game does not write a file.** In the original the slot stays empty until the first save. In ours
   "New game" on an empty slot should write immediately (a created-but-empty slot is easy to lose) or
   the start screen must say so. Decide; I suggest writing immediately because the slot summary needs a file.
7. **Overwrite protection on New game over a corrupt/newer slot.** `SaveSystem.TrySave` refuses a newer or
   unreadable file and quarantines a corrupt one. A "new game" on a newer-version slot must therefore be an
   explicit delete first; Task 2 should expose that rule and Task 6 show it.
8. **Persistent ids.** Using the object name as the id (original default) breaks if a builder renames an
   object. Task 3 should store an explicit id string and test duplicates in one room. The plan's "scene name
   + id" key matches the original and `SceneData.Key` is fine as is.
9. **Scene name key.** The original strips sub-scene suffixes (`GetBaseSceneName`). We use plain room
   scene names (`Tut_01`, `Tut_01b`); no stripping needed, but the persistent object must use the room's
   `RoomInfo.SceneName` or `gameObject.scene.name` consistently with `RecordRoom`.
10. **Replacing the in-memory data.** The original replaces `SceneData.instance` on load, leaving old
    references stale. Ours: `RoomWorldBootstrap.PlayerData` has a setter that re-points the hero's health.
    Persistent objects must read `bootstrap.PlayerData.sceneData` at use time, not cache it. This is plan
    risk 1 in concrete form; Task 3 and 5 need a test: load a different `PlayerData` while a room is
    active and check the objects follow after the room is re-activated.
11. **Bench respawn type and map zone.** Setting a bench in the original also stores the map zone as the
    respawn zone (PlayerData.cs:3748-3760). Ours has one `mapZone` field; Task 4 should keep that behaviour
    simple (no new field).
12. **Rest order, fade length and input** could not be read (PlayMaker). Use the owner's grey-box choices
    and keep the numbers in config, not in code.
13. **Editor play** (plan Task 5: playing a room directly must not write saves) matches `SaveSlot = -1`
    default in `HeroHealth`; keep that default.

## 7. Open questions

- Which Continue behaviour (item 1 above).
- Whether the bench restores silk (not visible in the C#; we have no silk use yet, so skipping is safe).
- Exact PlayMaker order of the bench rest.
- Whether the original handles quitting during the death delay (health 0 save).
- Play time units and format: stub as "--".
