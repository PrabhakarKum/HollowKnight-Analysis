# 13c. Rooms that remember

Milestone 3, task 3. A room used to start fresh every time it loaded. Now a few objects keep their state in the save data, so a wall you broke stays broken and a pickup you took stays gone, whether you left the room and came back or loaded a save.

## The idea in one paragraph

Every room object that must remember has an **id** written by hand. The save data (`PlayerData.sceneData`) stores a true or false per **scene name + id**. When the object comes alive it asks the save data "was I already done?" and starts broken or collected if the answer is yes. When the state changes it writes the answer straight away, so nothing is lost if the player quits a moment later.

## The three pieces (all in `Assets/Silksong/Rooms/`)

**PersistentBool.** The memory. It has a serialized `Id`, takes its scene name from the scene the object lives in, and reads and writes the true or false in the save data. It works out *which* save data on every use, never once at start: if a load replaces the save data while the game runs, the object follows the new one. With a running world it uses the world's save data (`RoomWorldBootstrap.Instance.PlayerData`). With no world (a room opened alone, tests) it uses a static fallback `PersistentBool.Fallback`; tests can set it and must call `PersistentBool.ResetStatics()` in teardown. It logs a warning once, when the object starts, if the id is empty, and a warning if two objects in one scene share an id (they would overwrite each other). The decision "starts done only if stored and true" is a plain function (`PersistentRules.StartsDone`) so it is tested without a scene. The older explicit `Configure` / `Save` / `Restore` use is unchanged.

**BreakableWall.** A solid wall. The hero's nail finds things to hit through `IHittable`, and the wall is one, so any nail hit breaks it (no damage threshold). Breaking writes broken = true, then switches off the wall's colliders and renderers. If the stored value is already true when the wall starts, it starts broken. A broken wall refuses further hits, so the nail does not pogo off empty space.

**Pickup.** A trigger. The first time the hero touches it, it writes collected = true and disappears (collider and renderer off). If the stored value is already true at start it is already gone. It only reacts to the hero (anything with a `HeroController` above the touching collider).

Both objects also have `Reapply()`, which re-reads the current save data and updates the object. A load into a room that is already on screen should call it.

## The id rule

- The id is typed by hand and stays the same forever. Renaming the object does not change it.
- The save key is scene name + id, so the same id in two different rooms is fine. Two objects with the same id in one room are not (warning).
- Do not leave it empty. An empty id falls back to the object's name, which works but warns, because renaming the object would lose the saved state.
- Naming style used so far: `<Room>_<Kind>_<Number>`, for example `Tut_01_BreakableWall_1`, `Tut_01_Pickup_1`.

## Adding one to a room

By hand in a scene: put `BreakableWall` (needs a solid 2D collider) or `Pickup` (needs a trigger 2D collider) on the object; the `PersistentBool` is added with it. Type the Id on the `PersistentBool`.

In the room builder (`TransitionRoomsBuilder`): add a `BreakableWallSpec` or `PickupSpec` to the room's spec. The builder sets the id.

## What Tut_01 has now

- `Tut_01_BreakableWall_1`: a 1 x 3 wall that seals a small alcove at the left end of a new shelf. The shelf (`ShelfLeft`) continues the existing `PlatformB` to the left wall, and a new `NookRoof` closes the top. The hero reaches the shelf the way it reaches `PlatformB` (up the right-hand platforms), walks left and hits the wall with the nail. The alcove is empty for now.
- `Tut_01_Pickup_1`: a yellow square on `StepLeft`, which the hero walks over on the way to the left gates.

No existing coordinate changed. The new blocks stay clear of the gates, the bench and the hazard patches (an EditMode test checks this).

**The grey-box rooms are generated and gitignored. Rebuild them** (menu Silksong / Build Transition Rooms, or `-executeMethod Hollowknight.Silksong.Editor.TransitionRoomsBuilder.BuildBatch`) to see the wall and pickup. The M2 acceptance tests already insist the scenes are newer than the builder source.

## Not built (logged through `Stubs.NotImplemented`)

- Break effect and sound for the wall.
- Pickup effect: the pickup grants no item, it only records that it was taken.
- Semi-persistent items (reset when resting at a bench): the flag exists, the behaviour does not.
- Saving to disk is not part of this task; the flags live in `PlayerData` and travel with it.

## Tests

- EditMode `RoomMemoryRulesTests`: the pure rules, storage lookup and replaced-data behaviour, and the Tut_01 additions.
- PlayMode `RoomMemoryTests`: wall and pickup in a code-built room that is unloaded and rebuilt; replaced save data with and without the flags; the same with a running world.
