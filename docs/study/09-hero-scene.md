# 09 - The Silksong hero scene (owner steps)

This scene lets you play the hero with the real Silksong art and body. Every hero number (speed, jump, dash,
wall, attack, hurt) comes from the one file `Assets/Silksong/Hero/HeroConfig.asset`. The scene stores a link to
that file, never a copy. Camera numbers stay in the camera's own settings (`GameCameraSettings`).

## Steps

1. Open the project in Unity and let it compile.
2. Run the menu **Silksong > Import Hero Animations** once. This needs the exported game files on disk
   (default `D:\Asset HollowKnight Bundles\ExportedProject`). It fills the gitignored folder
   `Assets/Silksong/Imported/HeroAnimations`. The Console should say how many clips were imported.
3. Run the menu **Silksong > Build Hero Scene**. If step 2 was skipped, the Console shows a red message telling you
   to run the import first, and no scene is made.
4. Unity opens `Assets/Scenes/SilksongHeroRoom.unity`. Press Play.

## Why the scene is not committed

The scene points at imported sprites by id, and those sprites are not in the repository (game art is never
committed). A committed copy would show missing sprites on any other computer. So the scene file and its `.meta`
are in `.gitignore` and you build it on your machine. The scene is named `SilksongHeroRoom.unity` because
`SilksongHero.unity` is the older committed scene with white-square placeholder art; building over it would
change a committed file and break it for everyone else.

## What you should see

- A flat grey room 90 units wide with a small 5-unit pit, two blue jump platforms, and two purple walls 4 units
  apart (the wall-jump shaft).
- The hero (Silksong sprites, 0.5 x 2.08 unit body) standing at the green start point on the left, idle animation playing.
- Two red training dummies on the right: one on the floor and one floating high (pogo target).
- The camera follows the hero with look-ahead and stops at the room edges.

## What to try

- Arrows / WASD to run, Z or Space to jump (hold for higher), C to dash, X to attack (up and down with the arrows).
- Dash across the pit, or fall in and cling to its wall.
- In the shaft, jump at one wall, hold toward it to cling, then jump away and catch the other wall to climb.
- Jump over the floating dummy and swing down on it to bounce.
- R puts the hero back at the start.

If the hero faces the wrong way when you turn, tick or untick "Art Faces Left" on the `HeroSceneBootstrap` object.
That box is only about which way the sprites look; it does not change any hero value.

## Tests

- EditMode: `Hollowknight.Silksong.Tests.EditMode` -> `HeroSceneBuilderTests`.
- PlayMode: `Hollowknight.Silksong.Tests.PlayMode` -> `RoomSceneTests`. They do NOT build the scene (building needs edit
  mode and hangs a batch run that goes on to play mode). Build it first: menu, or in batch
  `-executeMethod Hollowknight.Silksong.Editor.HeroSceneBuilder.BuildBatch` before `-runTests`. Rebuild after ANY change to
  the builder or bootstrap (BuildBatch always rebuilds); the tests fail with "scene is older than ... rebuild" when stale, and
  with the import-and-build instruction when the scene is missing.
