# Milestone 3 checklist (owner acceptance): saving and loading

You do not need to read code. Do each line, watch what happens, and write pass or fail in the table at the end.
Saves go to `Application.persistentDataPath/Saves` (files `user0.json` .. `user3.json`; on Windows that is under
`%USERPROFILE%\AppData\LocalLow\<company>\<product>\Saves`). Slot 1 on the screen is `user0.json`, slot 4 is `user3.json`.
To start from a clean machine delete that `Saves` folder first.

## Setup

1. Open the project in Unity and wait for it to compile.
2. Menu **Silksong > Import Hero Animations** (once; skip it and the hero is an orange box, everything still works).
3. Menu **Silksong > Build Transition Rooms** (makes `Tut_01`, `Tut_01b`, `Tut_02`; build again after pulling any change,
   the wall, pickup and bench only exist in rooms built after Milestone 3).
4. Menu **Silksong > Build Start Screen**. It writes `Assets/Scenes/Rooms/StartScreen.unity` and opens it.
5. Press **Play** with `StartScreen` open. You should see "SILKSONG (grey-box)" and four slots, all "Empty" on a clean machine.

Keys on the start screen: Up/Down (or W/S), Enter or Space = start or continue, N = new game over a save (asks first),
X or Delete = delete (asks twice), Escape = cancel a question. Gamepad: D-pad or left stick, A, X, Y, B (full table in
`13f-start-screen.md`).
Keys in the game: A/D or arrows move, Z or Space jump, C dash, X attack (the nail), Up (or W) at the bench rests.
Escape (gamepad Start) saves and returns to the start screen; there is no pause menu.

Where things are in `Tut_01` (the hub room, 40 wide): the hero starts on the green strip at x 14 with a brown seat beside it;
that is the bench. The pickup is a small yellow square on the low left step (x 10). The breakable wall is the lighter
box at the right end of a roofed alcove on the high left shelf (x 6 to 7, height 12.5 to 15.5): climb the right-hand
platforms as for the platform at x 12 to 17, walk left along the shelf and hit the wall from the right with X.
(`13c-rooms-that-remember.md` has the details.)

## The walk

1. Slot 1 selected, press Enter: a new game starts in `Tut_01`, the hero on the bench strip, full health (5 masks).
   Slot 1 is already written on disk.
2. Walk left, jump onto the low step and touch the yellow pickup: it disappears.
3. Climb to the shelf and break the wall with the nail: it vanishes (a one-frame flash is possible, see below).
4. Walk through the gate `right1` (high ledge, right side) into `Tut_01b` and look around (the rooms change as in Milestone 2).
   Touch the spikes or let something hurt you once so you are below full health.
5. Come back to `Tut_01`, stand on the bench strip and rest (press Up). The screen fades out and in, health is full.
6. Press Escape: the game ends and the start screen returns. Slot 1 now shows a zone and max health.
7. Select slot 1 and press Enter (Continue): you start in `Tut_01` at the bench with full health, not where you stood.
8. Check the wall is still broken and the pickup is still gone.
9. Escape again, choose slot 2, Enter: a new game; the wall is whole and the pickup is back. Slot 1 is unaffected
   (Escape, Continue slot 1 once more: still broken).
10. Escape, select slot 2, press N: it asks before starting over. Press Escape: nothing changes.
11. Select slot 2, press X: "Delete this save?"; Enter: "Really delete? This cannot be undone."; Escape: the save is
    still there. Repeat and confirm twice: the slot is "Empty".
12. Die once in `Tut_01b` (spikes, with the bench rested at before): you come back at the `Tut_01` bench. Escape and
    Continue: you are again at the bench. (A save never holds zero health.)
13. Only one hero and one camera exist after every start (look at the Hierarchy during play).
14. Open `Tut_01` directly (not the start screen) and press Play: it still works as in Milestone 2, and resting or leaving
    the room writes nothing to the `Saves` folder.

## Odd saves (optional)

- Put garbage text in `user3.json`: the slot shows the damaged message, Enter only repeats it, N does the same, X then twice
  Enter deletes it (the old file is kept beside it with a changed name, ending `.corrupt`).
- Edit a good save and raise `version` above the current one: the newer-version message shows and the same rules apply
  (kept aside as `.newer`).

## What the tests cannot judge

The PlayMode fixture `M3AcceptanceTests` checks the round trip on the real rooms. These are left to you:

- How the start screen looks and reads: whether the text is readable at your resolution, the layout and colours (it is
  plain on purpose); whether the keys and the gamepad stick feel right on the slot list.
- Space or Enter pressed on the start screen also reaching the hero: the screen and the hero read the keyboard separately,
  so the key that starts a game may also make the hero jump in its first frames. Watch the first second after Enter and Space.
- The one-frame flash of a broken wall: a wall that was broken may be visible for one frame when its room loads, before it
  hides itself. The tests only check the end state.
- Reach of the `Tut_01` shelf, wall and pickup with the nail: the tests break the wall by calling its hit method and place the
  hero on the pickup, so they cannot tell whether the shelf can be reached by jumping or whether the nail's range reaches
  the wall from where you can stand.
- How the bench rest fade feels (length, whether it is too abrupt), and that there is no rest pose or sound yet.
- The edge case of holding Up while arriving at a bench: does it count as a rest or not (it should not, only a new press does).
- Whether the Continue position at the bench looks right (hero standing on the seat, camera framing).
- Whether Escape-to-menu feels abrupt (no fade, no pause menu), and the abrupt start (no load fade, the hero just appears).
- Hero art and the overall look in the editor.

## Known stubs (added in Milestone 3, each logs through `Stubs.NotImplemented`)

- Game start: load fade and the bench wake-up animation (the hero simply appears).
- Bench rest pose and animation; bench rest sound.
- Breakable wall: break effect and sound (any hit breaks it, no damage threshold).
- Pickup effect: the item is not granted, only the flag is stored.
- Start screen: art, sound and the options/settings menu (plain text list only).
- Pause menu (Escape quits to the start screen).
- Save slot play time (shows nothing).
- Save at the moment of death (this port saves after the respawn refill, so a slot never holds health 0).

`STUBS.md` is filled automatically when a stub first runs in the editor, so these appear there only after you have played.

## Results

| # | Step | Pass / fail | Notes |
|---|------|-------------|-------|
| 1 | walk step 1 | | |
| 2 | walk step 2 | | |
| 3 | walk step 3 | | |
| 4 | walk step 4 | | |
| 5 | walk step 5 | | |
| 6 | walk step 6 | | |
| 7 | walk step 7 | | |
| 8 | walk step 8 | | |
| 9 | walk step 9 | | |
| 10 | walk step 10 | | |
| 11 | walk step 11 | | |
| 12 | walk step 12 | | |
| 13 | walk step 13 | | |
| 14 | walk step 14 | | |
