# 11 - The grey-box transition rooms (owner steps)

Three small rooms joined by Silksong's gate names, so you can walk the hero from one room to the next: `Tut_01`
(the hub), `Tut_01b` (to its right) and `Tut_02` (to its left). They are plain boxes; real room art comes later.
Every hero number still comes from the one file `Assets/Silksong/Hero/HeroConfig.asset`; camera numbers stay in
`GameCameraSettings`; the fade and walk-in timings are in `RoomTransitionSettings`. The rooms hold none of them.

## Steps

1. Open the project in Unity and let it compile.
2. Run **Silksong > Import Hero Animations** once (see `09-hero-scene.md`). This only gives the hero its Silksong
   art. If you skip it the rooms still build and the hero is an orange box.
3. Run **Silksong > Build Transition Rooms**. It makes `Tut_01.unity`, `Tut_01b.unity` and `Tut_02.unity` in
   `Assets/Scenes/Rooms/` and opens `Tut_01`. (You do not need *Build Hero Scene* for this.)
4. Press Play. The hero appears at the green strip on the floor of `Tut_01`.

You can also open `Tut_01b` or `Tut_02` from the Project window and press Play there: every room can start the game.

## What you should see

- Each room has its own colour: `Tut_01` blue-grey, `Tut_01b` green, `Tut_02` brown. A big label names the room and
  small labels sit next to each gate ("right1 -> Tut_01b / left1").
- Gates are the cyan see-through boxes at the room edges. The side wall has an opening at each one and a short floor
  carries the hero out past the edge.
- Walk into a gate and keep going: the hero keeps walking out, the screen fades to black, the next room appears
  with the hero at the matching gate, the screen fades back in, the hero walks a few steps inward, and then you have
  control again.
- Only one room is loaded at a time, and it is the same hero all the way through.

## The gates

| Room | Gate | Floor height | Leads to | Arrives at |
|---|---|---|---|---|
| Tut_01 | right1 | high ledge (15) | Tut_01b | left1 |
| Tut_01 | right2 | middle ledge (7.5) | Tut_01b | left2 |
| Tut_01 | left2 | small ledge (5) | Tut_02 | right1 |
| Tut_01 | left3 | floor (0) | Tut_02 | right2 |
| Tut_01b | left1 | high ledge (15) | Tut_01 | right1 |
| Tut_01b | left2 | middle ledge (7.5) | Tut_01 | right2 |
| Tut_02 | right1 | small ledge (5) | Tut_01 | left2 |
| Tut_02 | right2 | floor (0) | Tut_01 | left3 |

The names and links are exactly those of the real rooms (study `10-room-transitions.md`, section 6). Heights are
the real gate heights scaled down: stairs of 2.5 units get you to each ledge with ordinary jumps.

## Getting to each gate

- **Tut_01**: left3 is at the bottom of the left wall. For left2, jump up the two small steps at the left to the ledge
  above left3. For right2, climb the staircase on the right (three steps up) and walk right. For right1, go up
  from the staircase over the two floating platforms to the high ledge, then walk right.
- **Tut_01b**: left2 is at the top of a staircase of three steps on the left. For left1 go on over the two floating
  platforms to the high ledge above it. The right wall is closed (the real room has a gate to `Weave_04` there).
- **Tut_02**: right2 is at the floor on the right. For right1, jump up the step and onto the ledge at the right end.

A good test is to leave through one gate and come back through the same pair, then try the other pair.

## Not built

The real rooms also have `Tut_01` left1 (to `Tut_03`), `Tut_01` top1 (to `Bonetown`) and `Tut_01b` right1 (to
`Weave_04`). Those rooms are not part of this milestone, so the exits are left out (notes in the rooms say so, and
the stub list `STUBS.md` has a line). The hero cannot die or respawn across rooms yet (Task 8).

## Why the scenes are not committed

The scenes can point at the imported hero art by id, and that art is not in the repository. So the scene files and
the folder `.meta` are in `.gitignore` and you build them on your machine. After a change to the builder, build
again (the PlayMode test says so if a scene is older than the builder).

## How Play works in the Editor

A room scene is not the whole game. Each room carries one small `RoomWorldStarter` object. When you press Play it
creates the persistent world (hero, camera, fader, transition manager, room loader) with `RoomWorldBootstrap`,
takes the open room as the current one and puts the hero on the room's `Start` marker. When a room is loaded by the
running world, the world already exists and the starter does nothing. The hero, camera and manager are never saved
in a room scene.

Scenes loaded by the world come from **Build Settings** when they are listed there. The three rooms are not listed
(`ProjectSettings` is yours to edit), so in the Editor they are loaded straight from the file, by the path
`Assets/Scenes/Rooms/<name>.unity`; the Console says so when the world starts.

## Real builds

To use the rooms in a player build, add `Tut_01`, `Tut_01b` and `Tut_02` to **File > Build Profiles > Scene List**
(or Build Settings). Scene names must stay as they are; the gates look rooms up by name. The starter picks Build
Settings first, so this works without any other change. Opening a room scene is also still how the game starts: there
is no menu yet (stubbed).

## For the QA run

- Build: `-executeMethod Hollowknight.Silksong.Editor.TransitionRoomsBuilder.BuildBatch` (without `-quit`; it
  exits by itself).
- EditMode tests: `TransitionRoomsBuilderTests` (builds into a temp folder, checks gates, links, floors, reachability).
- PlayMode tests: `TransitionRoomsWalkTests` (needs the built rooms; walks all eight gate crossings of the table).

Owner acceptance checklist for Milestone 2: `docs/study/M2-checklist.md`.
