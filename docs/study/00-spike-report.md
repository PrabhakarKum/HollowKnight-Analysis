# 00 - Feasibility spike report

Date: 2026-11-01. Unity 6000.0.84f1 project; export is 6000.0.50f1 (AssetRipper-style).
Verdict: all three checks PASS. Importing real Silksong room and hero art is feasible. GO.

## Check A - sprite links: PASS (200/200 = 100%)

- Sampled 200 of 20,735 `Sprite\*.asset` files (random, fixed seed 12345).
- Each one's `m_RD.texture` GUID was resolved against a GUID index built from the export's texture `.meta` files (34,925 entries) and the target file was confirmed to exist.
- Result: 200 of 200 resolved, all to `.png` files. Pass bar was 95%.
- Sprite assets are plain Unity YAML, so they can be copied with their `.meta` and the texture they point to. Nothing needs re-slicing.

## Check B - script GUID reuse: PASS (0 missing for the reused GUID)

- Scene: `Scenes\Hornet\Room_Caravan_Interior.unity` (364 KB). Smallest scene containing a `TransitionPoint` (hero entry gate) and a `HazardRespawnMarker` (respawn spawn point). No prefab instances, so no prefab dependencies to copy.
- Stub: `_Spike/HazardRespawnMarker.cs`, hand-written, with a `.meta` carrying the original GUID `81d4c55b17aa35891ca0d0bb228876a6`.
- Opened the scene in Unity 6000.0.84f1 batchmode and counted components.
  - The GUID resolved to the stub path. The marker GameObject loaded with the stub bound (1 of 1).
  - Its serialized position (20, 7, 0) came through intact.
  - 43 other MonoBehaviours show as missing. That is expected, because only one GUID was reused. The scene has 44 MonoBehaviours in total.
  - Missing for the reused GUID: 0.
- Finding that matters for later tasks: every script in the export's `Scripts\` folder (1,249 of 1,249) is an empty AssetRipper dummy class. The decompiled game code (`HollowKnightSilksong\Assets\Scripts`) carries the same GUIDs in its `.meta` files, so it is the code source and the GUID source. Reusing GUIDs works for every script we port.
- Scene content is mostly tk2d sprites, tk2d tilemap, and PlayMaker FSMs. All of those scripts will be missing until stubbed or replaced, so a room needs a rendering route for tk2d content. That is a Task 11 concern.

## Check C - tk2d animation to AnimationClip: PASS

- Source: `Animations\Knight.prefab` (the hero's tk2d animation library; `Knight` is the internal name), clip `Run`.
- Source data: 10 frames, 15 fps, wrapMode 0 (Loop), footstep events on frames 3 and 8.
- Sprites come from `Collections\Knight Data\Knight.prefab` (1,828 sprite definitions), sprite IDs 1162, 1164, 1171, 1170, 1166, 1168, 1167, 1165, 1163, 1169. These are named `Hornet_run_new0000` to `0009`.
- Recipe, which becomes the `Tk2dAnimationConverter` in Task 6:
  1. Read `spriteDefinitions[spriteId]` and take `uvs`, `positions`, `flipped`, `materialId` and `texelSize`.
  2. `materialId` selects `atlasN.png` (verified through the material to texture GUIDs). The atlases are 4096x4096.
  3. The pixel rect is the UV bounding box times the atlas size.
  4. If `flipped == 1` the sprite is stored rotated 90 degrees: sprite x runs along atlas v, and sprite y runs along atlas u. Un-rotate when cutting. Otherwise x is along u and y is along v.
  5. Pixels per unit is `1 / texelSize` (64 here). The pivot is the tk2d origin: `(-minX / texel / width, -minY / texel / height)` using the `positions` extents. The cut size matched the `positions` extents exactly (max deviation 0.000000 units).
  6. Build `ObjectReferenceKeyframe`s at `i / fps` on `SpriteRenderer.m_Sprite`, with no extra closing key. Unity gives the last key one frame of duration, so the length is exactly `frames / fps`. Set `loopTime` from `wrapMode` (0 = Loop). Add events from `triggerEvent` and `eventInfo`.
- Verified in Unity: clip frameRate 15, 10 keys, length 0.6667 s (10/15), loopTime on, `SpriteRenderer.m_Sprite` curve. Sampling the clip at mid-frame gave 10 of 10 expected sprites. Playing through a PlayableGraph on an Animator over two loops gave 20 of 20 correct samples, in order 0..9 then wrapping to 0 with no repeated frame.
- Visual check: frames 0000, 0004 and 0007 were inspected. Flipped and unflipped frames both come out upright and look right.
- Limits: `loopStart` is ignored when wrapMode is Loop. The other wrap modes (Once = 2, PingPong = 3, LoopSection = 1, and so on) were not exercised. Hand-checked only: the frame order in the clip follows the source `spriteId` order, which is not the numeric sprite order.

## Fallbacks
None needed. All three checks passed.

## Files created (all under `Assets/Silksong/Imported/_Spike/`, plus this report)
- `Room_Caravan_Interior.unity` (+ .meta), a copy of the export scene
- `HazardRespawnMarker.cs` (+ .meta with the reused GUID)
- `RunFrames/Hornet_run_new0000..0009.png` (+ .meta)
- `Hornet_Run.anim`, `Hornet_Run.controller`, `Hornet_Run_Spike.prefab` (+ .meta)
- `Editor/SpikeRunner.cs`, `Editor/run_clip_data.txt`

## Notes
- The owner's Editor was open on the project during the spike (`Temp/UnityLockfile` appeared at 10:26). The Unity runs were done in a scratch copy of the project (Assets/Silksong, Packages, ProjectSettings) so the owner's session was not disturbed. The results were then copied back into `_Spike`.
- The scene still shows 43 missing-script components when opened. That is expected for the spike.
