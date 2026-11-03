# Milestone 3 checklist (owner acceptance): saving and loading

Do each line, watch what happens, and write pass or fail in the table at the end. Saves go to
`Application.persistentDataPath/Saves` (files `user0.json` .. `user3.json`).

## Setup

1. Open the project in Unity and wait for it to compile.
2. Menu **Silksong > Import Hero Animations** (once; skip it and the hero is an orange box).
3. Menu **Silksong > Build Transition Rooms**.
4. Menu **Silksong > Build Start Screen**. It opens `StartScreen`.
5. Press **Play**. You should see "SILKSONG (grey-box)" and four slots, all "Empty" on a clean machine.

Keys on the screen: Up/Down, Enter or Space, N, X or Delete, Escape (full table in `13f-start-screen.md`).
In game: A/D move, Z or Space jump, C dash, X attack, Up at the bench rests. Escape quits to the screen.

## The walk

1. Slot 1 selected, press Enter: a new game starts in `Tut_01`, the hero on the bench strip.
2. Break the `Tut_01` breakable wall with the nail (see `13c-rooms-that-remember.md` for where it is).
3. Walk over the pickup: it disappears.
4. Walk through a gate to `Tut_01b` and look around (the rooms still change as in Milestone 2).
5. Come back to `Tut_01` and rest at the bench (stand on it, press Up). Health is full.
6. Press Escape: the game ends and the start screen returns. Slot 1 now shows a zone and max health.
7. Select slot 1 and press Enter (Continue): you start in `Tut_01` at the bench with full health.
8. Check the wall is still broken and the pickup is still gone.
9. Escape again, choose slot 2, Enter: a new game; the wall is whole and the pickup is back. Slot 1 is unaffected.
10. Escape, select slot 2, press N: it asks before starting over. Press Escape: nothing changes.
11. Select slot 2, press X: "Delete this save?"; Enter: "Really delete? This cannot be undone."; Escape: the save is
    still there. Repeat and confirm twice: the slot is "Empty".
12. Only one hero and one camera exist after every start (look at the Hierarchy during play).

## Odd saves (optional)

- Put garbage text in `user3.json`: the slot shows the damaged message, Enter only repeats it, X then twice Enter deletes it
  (the old file is kept beside it with a changed name).
- Edit a good save and raise `version` above the current one: the newer-version message shows and the same rules apply.

## What the tests cannot judge

- Whether the text is readable at your resolution and the screen looks acceptable (it is plain on purpose).
- Whether the keys feel right, especially a gamepad stick on the slot list.
- Whether the Continue position at the bench looks right (hero standing on the seat, camera framing).
- Whether Escape-to-menu feels abrupt (no fade, no pause menu).
- Hero art and the overall look in the editor.

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
