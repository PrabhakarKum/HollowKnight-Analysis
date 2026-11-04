# Milestone 4 checklist (owner acceptance): the first enemy

You do not need to read code. Do each line, watch what happens, and write pass or fail in the table at the end.
Saves go to `Application.persistentDataPath/Saves` (`user0.json` is slot 1; on Windows under
`%USERPROFILE%\AppData\LocalLow\<company>\<product>\Saves`). To start clean, delete that `Saves` folder first.

## Setup

1. Open the project in Unity and wait for it to compile.
2. Menu **Silksong > Import Hero Animations** (once; skip it and the hero is an orange box, everything still works).
3. Menu **Silksong > Build Transition Rooms**. You MUST build again after pulling Milestone 4: the enemy and its step are only in
   rooms built now. This also creates the enemy's settings asset `Assets/ScriptableObjects/SilksongEnemies/MossCreep.asset`
   (generated and not committed, like the rooms; it holds the numbers: 2 health, 1 damage, walk speed 3, and so on).
4. Menu **Silksong > Build Start Screen**, then open `StartScreen` (it opens by itself) and press **Play**.

Keys in the game: A/D or arrows move, Z or Space jump, C dash, X attack (the nail), Up at the bench rests, Escape saves and
returns to the start screen.

## Where the enemy is

In `Tut_01b` (the room to the right of the hub, reached through the gate `right1` on the high right ledge of `Tut_01`, or `right2`).
Walk east along the floor, past the red spikes and the yellow hazard patches. At about x 37 there is a 1-high step, and
right behind it a 2-high block (the Bump, x 40 to 46). The green box walking back and forth on top of the Bump is the Moss Creep.
It is called `Tut_01b_MossCreep_1` in the Hierarchy under `Room/Enemies` while you play.

The step is there on purpose: your side swing only covers the height from 1.1 to 2.3 above your feet, so a green box standing
on your own floor is touched by a sliver of the swing. Stand on the step and the swing covers the whole creep
(`14f-placing-the-enemy.md`).

## The walk

1. New game (slot 1) in `Tut_01`. Walk through `right1` into `Tut_01b`. Nothing hurts you on arrival (the enemy is far away).
2. Find the green box on the Bump. It walks at an even pace, turns round at both ends of the Bump (it does not walk off) and
   keeps doing so; the dark "eye" square is on the side it faces.
3. Climb the step and the Bump (jump) and touch the creep: you lose exactly one mask, are pushed away, and for about a second
   touching it again does nothing.
4. Climb the step (x 37 to 40, face right). Wait until the creep is at the left end of the Bump, close to you, and press X.
   Watch: it flashes whiter for a moment, is shoved to the right, and you are pushed back a little (recoil).
5. Hit it a second time the same way: it vanishes and does not come back. It does not hurt you if you stand where it died.
6. Silk: there is no silk display yet. Each landed hit should give +1 silk (2 after two hits). To see it: after the two hits press
   Escape (this saves and goes to the start screen), then open `user0.json` in the Saves folder and look for `"silk": 2`.
   (Loading a save sets silk back to 0 by design, so look at the file, not at the game after Continue.)
7. Leave `Tut_01b` through `left1` (the high left gate) and come back through `right1`: the creep is still gone.
8. Walk back to `Tut_01`, stand on the bench strip and rest (Up). Press Escape, Continue slot 1: you stand at the bench;
   go to `Tut_01b` again: the creep is still gone.
9. Escape, slot 2, Enter (new game), go to `Tut_01b`: the creep is alive and walking in slot 2. Slot 1 is still without it.
10. Optional, the stays-dead switch: select the asset `Assets/ScriptableObjects/SilksongEnemies/MossCreep.asset`, untick
    "Stays Dead", start a new game (a new slot), kill the creep, leave `Tut_01b` and come back: it is there again.
    Tick it again afterwards.
11. Optional, tuning: change "Walk Speed" or "Max Health" in the same asset; the next time you press Play the creep follows it
    (no room rebuild needed for those).

## What the tests cannot judge

The PlayMode fixture `M4AcceptanceTests` runs steps 1 to 9 on the real rooms with real swings. These are left to you:

- The look of the placeholder: a green box with a dark eye square, no art, no animation.
- Whether the hit flash can be seen on a green box. The flash blends the colour toward white for 0.07 s then fades over 0.05 s; a
  proper white fill needs a shader that is not built (stub). It may be too faint or too brief to notice.
- The feel of the recoil on you and the shove on the creep (speed 3.75 for 8 steps, and 15 for 0.15 s, from the original's numbers
  but not tuned by feel), including the creep being shoved around the Bump.
- Whether the placement is fair: one creep on a raised block, away from every gate, with a step beside it.
- Whether the nail reaches the creep comfortably. The side swing is the old hand-tuned box (hero centre, 1.2 high), which
  reaches a floor-level creep by only 0.03 units; that is why the step exists. It may still feel fiddly (you must wait for the
  creep to come to the near end of the Bump).
- Whether touching the creep from below (standing on the floor beside the Bump) feels right; there is no contact there.
- The one-frame look when you re-enter `Tut_01b` after killing it (the creep should never be visible, but a flicker would be a bug).

## Known stubs (added in Milestone 4, each logs through `Stubs.NotImplemented`)

- Enemy art and animations (a plain green box is drawn).
- Enemy hit flash as a real white fill (blended colour only), hit sound and blood effects.
- Enemy corpse, death sound and drops (it just disappears).
- Enemy walker: turn animation, roof and wall crawling, forced turns from noise (floor walking only).
- Hero: attack buffer, alternate slash, downspike thrust, and the bounce refreshing the double jump.
- Hero damage effects, hit-stop and invulnerability flicker (from Milestone 2, they still apply to an enemy hit).

`STUBS.md` is filled automatically when a stub first runs in the editor.

## Results

| # | Step | Pass / fail | Notes |
|---|------|-------------|-------|
| 1 | arrive in Tut_01b safely | | |
| 2 | it walks and turns at both ends | | |
| 3 | touch costs one mask, then a grace second | | |
| 4 | first hit: flash, shove, recoil | | |
| 5 | second hit: it dies, harmless | | |
| 6 | silk +1 per hit (in the slot file) | | |
| 7 | still dead after leaving and returning | | |
| 8 | still dead after rest, quit, continue | | |
| 9 | alive in a second slot | | |
| 10 | stays-dead off brings it back (optional) | | |
| 11 | config edit changes it (optional) | | |
