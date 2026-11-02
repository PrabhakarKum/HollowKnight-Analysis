# Milestone 2 checklist (owner acceptance): room transitions

You do not need to read code. Do each line, watch what happens, and write pass or fail in the table at the end.
Hero numbers still come from `Assets/Silksong/Hero/HeroConfig.asset`; the fade and walk-in timings are in
`RoomTransitionSettings` (on the world object, see "Tuned by eye" below).

## Setup

1. Open the project in Unity and wait for it to compile.
2. Menu **Silksong > Import Hero Animations** (once). Skip it and the hero is an orange box; everything still works.
3. You do NOT need *Build Hero Scene*.
4. Menu **Silksong > Build Transition Rooms**. It makes `Tut_01`, `Tut_01b` and `Tut_02` in `Assets/Scenes/Rooms/`
   and opens `Tut_01`. Build again after any change to the builder code.
5. Open `Tut_01` if it is not already open and press **Play**. The hero appears on the green strip on the floor of
   `Tut_01`, next to a small brown seat (the bench).

Keys: A/D or arrows = move, Z or Space = jump, C = dash. Room names are written big in each room; each gate has a
small label ("right1 -> Tut_01b / left1"). Gates are the cyan see-through boxes at the room edges.
How to reach each gate is in `11-transition-rooms.md` ("Getting to each gate").

## What a good crossing looks like (check this every time)

1. You walk into the gate facing outward and keep going: the hero keeps walking out by itself and you cannot steer.
2. The screen fades to black (about a quarter of a second).
3. The next room appears, with the hero standing at the matching gate. The old room is gone.
4. The screen fades back in (about 0.3 s). The hero walks a few steps inward by itself (about a third of a second).
5. The camera is already on the hero (it jumps, it does not slide across the room) and stays inside the room.
6. Control comes back. No jitter, no flash of the old room, no second hero, no health lost.
7. The hero cannot be hurt while leaving and entering (spikes touched in that window do nothing).

## Gate pairs (walk each one there and back)

For each row go through the first gate, then turn round and walk straight out of the gate you arrived at.

| # | Go from | Gate | You arrive in | At gate | Then back out of | Arrive again at |
|---|---------|------|---------------|---------|------------------|-----------------|
| 1 | Tut_01 | right1 (high ledge) | Tut_01b | left1 | left1 | Tut_01 right1 |
| 2 | Tut_01 | right2 (middle ledge) | Tut_01b | left2 | left2 | Tut_01 right2 |
| 3 | Tut_01 | left2 (small ledge) | Tut_02 | right1 | right1 | Tut_01 left2 |
| 4 | Tut_01 | left3 (floor) | Tut_02 | right2 | right2 | Tut_01 left3 |

Each of the eight crossings must match "a good crossing" above, and the room name you see must be the one in the table.

## Rules and stray cases

| # | Try | Expect |
|---|-----|--------|
| A | Stand just inside a right gate, press left (face inward) so the hero faces away, then get into the gate box without turning (for example let a jump or dash carry you in backwards, or step back and forth on its edge). | The hero is pushed back out of the gate box and nothing starts. Turn to face outward and walk in: it leaves normally. |
| B | Jump into a gate. | If you are facing outward it leaves; otherwise you are pushed back. No crash, no stuck hero. |
| C | Dash into a gate. | Same as B. A dash that is still running does not break the crossing. |
| D | Arrive in a room and immediately walk back out of the same gate, as soon as control returns. | Works, and the hero is not hurt or stuck. Walking into it during the walk-in does nothing (you have no control yet). |
| E | Press move keys the whole time while crossing. | You cannot steer during the crossing; after it you keep walking if you still hold the key. |
| F | Look at the exits that are not built: `Tut_01` left1 and top1, `Tut_01b` right1. | There is no gate there (a wall, and a note in the room). Nothing happens, nothing to walk into. The Console has a "[Stub] Not implemented" line about them. The real destinations Tut_03, Bonetown and Weave_04 do not exist yet. |

## Spikes (hazard respawn stays in the same room)

| # | Room | Do | Expect |
|---|------|----|--------|
| 1 | Tut_01 | On the middle ledge, step on the pale yellow box then into the red spikes. | Lose 1 health and reappear on the yellow strip in the same room. |
| 2 | Tut_01b | Go in through right1 then touch the spikes straight away. Then try the yellow boxes and spikes on the floor. | You reappear a few units inside the gate (or at the last yellow strip), always in `Tut_01b`. |
| 3 | Tut_02 | Same on the floor. | Reappear at the yellow strip in `Tut_02`. |

## Death and the bench

| # | Do | Expect |
|---|----|--------|
| 1 | In `Tut_01b` or `Tut_02`, kill the hero (spikes until health is gone). | About 1.6 s later the screen fades, `Tut_01` loads and you stand at the bench (the green strip with the brown seat) with full health and control. |
| 2 | Die a second time the same way. | It works again. No second hero, no stuck black screen. |
| 3 | Die in `Tut_01` itself. | You come back in place, with no fade and no room change. |
| 4 | After deaths, cross a few gates again. | Everything still works as above. |

## Tuned by eye (PROVISIONAL, the values are guesses)

The real game keeps these inside parts we cannot read, so they were chosen to feel right. Tell me which feel wrong.

| What | Now | Where it is |
|------|-----|-------------|
| Fade out | 0.25 s | `fadeOutSeconds` in `RoomTransitionSettings` |
| Fade in | 0.3 s | `fadeInSeconds` |
| Wait before the old room goes | 0.34 s | `fadeWaitSeconds` (a real value) |
| Wait before the hero is placed | 0.165 s | `heroWaitSeconds` (a real value) |
| Walk-in time | 0.33 s | `TransitionWalkInSeconds` in `HeroConfig` (a real code-side value) |
| Where you stand when entering from a top or bottom gate, and the throw out of a bottom gate | guessed numbers | `HeroTransitionDriver` / `HeroConfig` entry fields (no top or bottom gate exists in the three rooms, so this cannot be seen yet) |
| Hero cannot be hurt during a change | the whole change | guess |

## Known limitations (not bugs)

- No room art: plain coloured boxes. Real rooms come later.
- Nothing is saved to disk: player data (current room, bench) lives in memory only.
- The three rooms are not in Build Settings, so they only load inside the Editor (by file path). For a real build:
  open **File > Build Profiles > Scene List** and add `Assets/Scenes/Rooms/Tut_01.unity`, `Tut_01b.unity` and
  `Tut_02.unity` (use "Add Open Scenes" after opening each, or drag them in). Keep the scene names.
- The scene files are not in git (they point at imported hero art); everyone builds them locally.
- Doors, benches you can sit on, a death screen, silk loss, sound, pickups and breakables staying broken are not built.
- Camera lock areas are stubbed.

## Result sheet

| # | Item | Pass / Fail / Blocked | Comments |
|---|------|-----------------------|----------|
| 1 | Tut_01 right1 -> Tut_01b left1 | | |
| 2 | Tut_01b left1 -> Tut_01 right1 | | |
| 3 | Tut_01 right2 -> Tut_01b left2 | | |
| 4 | Tut_01b left2 -> Tut_01 right2 | | |
| 5 | Tut_01 left2 -> Tut_02 right1 | | |
| 6 | Tut_02 right1 -> Tut_01 left2 | | |
| 7 | Tut_01 left3 -> Tut_02 right2 | | |
| 8 | Tut_02 right2 -> Tut_01 left3 | | |
| 9 | Facing rule (A) | | |
| 10 | Jump / dash into a gate (B, C) | | |
| 11 | Leaving again straight away (D, E) | | |
| 12 | Unbuilt exits do nothing (F) | | |
| 13 | Spikes in Tut_01 | | |
| 14 | Spikes in Tut_01b | | |
| 15 | Spikes in Tut_02 | | |
| 16 | Death in Tut_01b or Tut_02 returns to the Tut_01 bench | | |
| 17 | Second death | | |
| 18 | Fade, walk-in and camera feel | | |

Owner sign-off: ____________________  Date: ____________

## What to tell me if something is off

Please send:

1. The row number and what you did (which gate, which direction, walking, jumping or dashing).
2. What you expected and what happened instead, in plain words ("screen stayed black", "hero appeared in the wall").
3. Whether it happens every time or only sometimes.
4. Any red line in the Console (copy it as it is). Yellow "[Stub]" lines are normal.
5. For feel problems: too fast, too slow, too long, too abrupt, and roughly by how much.

## For the QA run

Rebuild the rooms first (`-executeMethod Hollowknight.Silksong.Editor.TransitionRoomsBuilder.BuildBatch`), then run the
PlayMode fixture `M2AcceptanceTests` (assembly `Hollowknight.Silksong.Tests.PlayMode`); it walks the gates through the real
triggers with scripted input. A missing or older scene fails at once with the rebuild instruction.
