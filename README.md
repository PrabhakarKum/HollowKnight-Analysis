# Hollowknight-Analysis

A **learning project**: study how Hollow Knight: Silksong-style player mechanics work, then re-implement them in Unity with our own code and our own values, one mechanic at a time.

- Unity **6000.0.84f1**, URP 17, Input System 1.20 (new input only), 2D.
- Test scene: `Assets/Scenes/SampleScene.unity`.
- The game's own code, art and audio are **not** in this repository. The mechanics were studied by reading the compiled logic of a legally owned copy, then written from scratch. Numbers that are not readable from the game are our own guesses, tuned by feel (the notes say which is which).

## What is built

Run, variable-height jump, double jump, dash, sticky wall slide / wall jump, attacks (forward / up / down, combo, recoil, pogo), hard landing, camera (look-ahead, look-down, lock zones, shake), hit-stop, health + damage + invulnerability + hazards + death/respawn, silk + bind (heal), sprint + sprint jump. Effects (bind, sprint dust) are original pixel art drawn in code.

## Where to read more

- `SESSION_HANDOFF.md` - start here: project map, rules agreed, how to test, what is next.
- `HOLLOWKNIGHT_ANALYSIS.md` - per-mechanic notes, what the real code does, and what is our own guess.

## Code layout

```
Assets/Scripts/Player/   controller, tuning (ScriptableObject), combat, health, silk/bind, sprint, effects, visuals
Assets/Scripts/Camera/   follow camera + lock zones
Assets/Scripts/Feel/     hit-stop, screen shake, pixel-effect helpers
Assets/Scripts/Dev/      PlaytestBot - editor-only automated play-mode tests
Assets/Editor/           setup menus (Hollowknight > ...)
Backups/                 a full copy of the test bot from before the suite was shortened
```

## Third-party assets that are NOT in this repository

These are licensed packs and are excluded on purpose (see `.gitignore`). The project opens without them but the hero is invisible/missing until the sprites are re-imported:

| Asset | Folder it goes in |
|---|---|
| DeadRevolver "Pixel Prototype Player Sprites" | `Assets/DeadRevolver/PixelPrototypePlayerSprites/` |
| vHierarchy, vInspector, vFolders (editor tools, optional) | `Assets/Plugins/` |

After re-importing the sprites run `Hollowknight > Use DeadRevolver Sprites On Player` and `Hollowknight > Bake Sprite Edges`.

## Testing

`PlaytestBot` injects keyboard input in play mode and logs `[PLAYTEST] PASS/FAIL` lines to the editor log. See `SESSION_HANDOFF.md` section 5 for how to start a run.
