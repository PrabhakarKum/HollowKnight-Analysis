# Bench rest and autosave (Milestone 3, task 4)

Code: `Assets/Silksong/Rooms/Bench.cs`, `Rooms/BenchRules.cs`, a small change in `Rooms/RoomWorldBootstrap.cs`, and the Tut_01 bench in `Editor/TransitionRoomsBuilder.cs`.
Tests: `Tests/EditMode/BenchRulesTests.cs`, `Tests/PlayMode/BenchRestTests.cs`.

## What resting does
A bench is a trigger on a respawn marker. The hero rests when all of this is true: it is inside the trigger, standing on the ground and almost still, not dashing, attacking, hurt, dead or clinging, its controls are not locked, no room change is running, and the player presses up. Up must be a new press: holding up through a rest, or pressing it in the air and landing, does not rest. After a rest there is a half-second cooldown as well.

The steps, in order:
1. The controls are taken (the same lock a room change uses) and the hero cannot be hurt.
2. The bench becomes the death respawn (`respawnScene`, `respawnMarker`).
3. Health is set to maximum.
4. The game is saved into the selected slot. This happens after steps 2 and 3, so the file holds the new respawn and full health. With no slot selected (a room played directly in the editor) nothing is written; the rest still heals.
5. The screen fades out and back in, using the same fader and fade lengths as a room change (0.25 s out, 0.3 s in, tuned by eye).
6. Controls come back and the `Rested` event is raised.

## What is stubbed
- Sitting pose and animation (`Bench rest pose and animation`).
- Sound (`Bench rest sound`).
- Silk: left alone. Nothing refills it and nothing is logged, because we do not know what the original does.

## What is unknown about the original
The bench is a PlayMaker state machine that could not be read (see page 13, section 5). We know from the C# around it that it sets the death respawn, probably heals through `MaxHealth()`, saves with the `SaveGame` action, and takes control while `atBench` is set. The order of the steps, the fade lengths, the up-press rule, whether silk refills and whether the save comes before or after the fade are all inferred or invented here.

## Autosave on a room change (our addition)
The original saves at death, at a bench and on quit to menu. This port also saves once after a room change has finished successfully (`RoomWorldBootstrap`, after the room is recorded). It does not save after a failed change, and not while the hero is dead. With no selected slot nothing is written. `RoomWorldBootstrap.AutosaveCount` and `Bench.SavesWritten` count real writes so tests can check them.

## Known edge
If the player holds up while the controls are locked by a room change and then control returns, that counts as a fresh press, because the locked hero reads no input. This can start a rest right after arriving at a bench; it is harmless.

## Editor note
The Tut_01 Start marker (the only bench-flagged marker) gets a trigger box and a `Bench` component. No coordinates changed. Rebuild the rooms with the menu `Silksong/Build Transition Rooms` to get it into the scene files.
