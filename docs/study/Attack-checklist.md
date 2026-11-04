# Attack visuals checklist (owner acceptance): the nail swipe, alternate swing, wall slash, down swing look

You do not need to read code. Do each line, watch what happens, and write pass or fail in the table at the end.
Background is in `15-attack-visuals.md`.

## Setup (rebuild first)

1. Open the project in Unity and wait for it to compile.
2. Menu **Silksong > Import Hero Animations**. This is required this time: the nail swipe clips, `SlashAlt` and `Wall Slash`
   are only imported by the newer importer. Without it the hero is an orange box and has no swipe at all.
3. Rebuild the hero scene and the rooms: menu **Silksong > Build Transition Rooms**, then **Silksong > Build Start Screen**.
4. Open `StartScreen` (it opens by itself) and press **Play**. Start a new game; the first room is `Tut_01`.

Keys: A/D or arrows move, Z or Space jump, C dash, X attack (the nail), Up/Down with X swing up/down. A wall to cling to is
the right wall at the end of the first room or any wall in `Tut_01b`.

## Things to check

1. **Side swing.** Stand still, face right, press X. A white nail swipe appears in front of the hero on the right. Turn left,
   press X: the swipe is on the left, mirrored. Turning the hero must never leave a swipe on the wrong side.
2. **Alternate swing.** Press X, then press X again as soon as the first swing is over (about half a second). The body shows a
   different slash pose and a different swipe the second time. A third quick swing goes back to the first.
3. **Reset time.** Swing once, wait a bit more than a second, swing again: it is the first pose again (the original resets
   after 0.65 s since the last swing started). Swinging again within about 0.6 s gives the alternate.
4. **Up swing.** Hold Up and press X: the swipe is above the hero, and no alternate pose appears however fast you repeat.
5. **Down swing in the air.** Jump, hold Down and press X. The down swipe shows for a short moment (about a tenth of a second)
   on the side the hero faces, and the down-slash pose stays until the swing ends. Hitting an enemy below
   still bounces you as before.
6. **Wall.** Cling to a wall and press X (stick toward the wall or neutral): the wall slash pose plays and the swipe is on the
   side away from the wall, with a small gap from the body. The hero must not flip back to the wall-slide pose in the middle
   of the swing.
7. **Cut short.** Swing and get hurt (touch the moss creep or a spike) in the middle: the swipe vanishes at once and does not
   stay on screen. Same after a hit on an enemy with a down swing (the swipe ends with the bounce).
8. **Length.** Look at how far the swipe is drawn compared to how far the nail really hits. The drawn swipe is about 2.9 units
   long, the hand-tuned hitbox side box is 1.35 from the hero (size 1.1 at offset 0.8), and the original hit window was 0.1 s
   where ours is 0.35 s. So the nail may look as if it reaches farther than it does. This is known and left as is: decide
   whether it matters (owner decision pending; do not change anything yet). Note your impression in the table.
9. **Smoothness.** During all of the above there is no jitter, no one-frame flash of a swipe in the wrong place, no swipe left
   behind after the swing ends, and no body pose flicker when running, jumping or landing right after a swing.

## What the tests cannot judge

The PlayMode fixture `AttackVisualsAcceptanceTests` checks clip names, swipe visibility and flip, and that the hitbox numbers
equal `HeroConfig` for every direction, by stepping a test hero by hand. These are left to you:

- How the swing feels and whether the timing looks right (swipe length against pose length, 0.25 s clip against 0.35 s swing).
- Whether the swipe art lines up with the body art (position, size, sorting in front of the hero) for each direction.
- Whether the real `SlashDown` pose, which has a looping part, ends cleanly when the swing ends. The test only checks the clip
  name; it never plays the real clip to the end.
- Whether the drawn swipe length against the hitbox length is acceptable (item 8).

## Known stubs (this milestone, each logs through `Stubs.NotImplemented`)

- Downspike choreography: the antic, thrust and recovery phases of the down swing are not built (the pose is one clip).
- Downspike burst effect on a hit, and the hero box shrink during the down swing.
- Imbue glow on the swipe and the Long Needle swipe variants (the default needle only).
- A plain-box hero (no imported art) gets no swipe: a stub line is logged on the first swing.
- Up and down alternates: the default crest has none, so up/down swings always use one pose.
- The 0.65 s alternate reset time is a code constant, not a setting in `HeroConfig`.

`STUBS.md` is filled automatically when a stub first runs in the editor.

## Results

| # | Step | Pass / fail | Notes |
|---|------|-------------|-------|
| 1 | side swing, swipe on the right side, flips with facing | | |
| 2 | two quick swings alternate the pose and the swipe | | |
| 3 | waiting past 0.65 s starts again with the first | | |
| 4 | up swing: swipe above, no alternate | | |
| 5 | down swing in the air: short swipe, pose holds | | |
| 6 | wall cling + attack: wall slash, swipe on the open side | | |
| 7 | swipe does not linger after hurt or bounce | | |
| 8 | drawn swipe against nail reach (decision) | | |
| 9 | no jitter or flicker | | |
