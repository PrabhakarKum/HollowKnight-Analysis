# Session handoff - Hollowknight-Analysis (read this first in a new chat)

Paste or point Claude at this file at the start of a new session. It says what the project is, what is already
built, how to test it, the rules we agreed on, and what comes next. Also read `HOLLOWKNIGHT_ANALYSIS.md`
(same folder) for the per-mechanic details and the exact numbers.

## 1. What this project is
A **learning project**: recreate Hollow Knight: Silksong-style player mechanics in Unity, with **our own code and our own
values**, then refine and extend them. The owner studies how the real game works by reading its compiled C# logic,
then re-implements it. The owner owns a purchased copy of the game.

- Unity project: `E:\Unity Projects\Unity 2D Projects\Hollowknight-Analysis` (Unity 6000.0.84f1, URP 17, Input System 1.20, new input only)
- Scene: `Assets/Scenes/SampleScene.unity` (test level: ground, Platform Low/High, two walls, hit dummies, lock zone)
- Notes file with details: `HOLLOWKNIGHT_ANALYSIS.md` (project root). Keep updating it.
- Game install (read-only reference): `D:\Games\Hollow Knight - Silksong\` ; managed code at `...\Hollow Knight Silksong_Data\Managed\Assembly-CSharp.dll`
- Copy of the DLL used for analysis: `E:\Git Hub\HK-analysis\Assembly-CSharp.dll` (+ PlayMaker.dll, TeamCherry.*.dll)
- Earlier structure summary: `E:\Git Hub\HK-analysis\Silksong-player-mechanics.md`
- Sprites in use: DeadRevolver pack at `Assets/DeadRevolver/PixelPrototypePlayerSprites` (the owner installed it)

## 2. Rules agreed with the owner (important)
1. **UPDATED (owner, in chat, after the damage work): using the game's code and assets in the project is now allowed.** The owner
   owns a purchased copy and this is a personal learning project. Earlier sessions did not copy anything; that was the old rule.
   Keep it local: do not publish, share or redistribute the project with game assets/code in it, and note where anything came from.
   Downloading extraction tools (e.g. an asset ripper) still needs the owner's explicit OK for each download.
2. The game's **numeric values are not in the DLL** (they live in Unity asset files). Until they are read from the asset files,
   every number in the controller is our own guess, tuned by feel. Say so honestly when reporting which numbers are real vs guessed.
3. **Always report test results honestly** (what passed, what is a guess, what was not verified). The owner playtests by hand
   and reports bugs; reproduce them in the editor first, find the cause, fix, add a test.
4. Think about how the player *feels*, not only whether code is correct. The owner found most bugs by playing.
5. **Float is OFF** (owner does not want the player to float at all). `PlayerTuning.floatEnabled = false`. Do not re-enable.
6. The owner asked to work one mechanic at a time: read the real logic from the DLL, match it, test, update notes, ask what next.
7. **Git** (repo: github.com/PrabhakarKum/HollowKnight-Analysis, public, only the owner): do NOT add Claude as co-author / collaborator (no
   `Co-Authored-By` trailer, no "Generated with Claude" text). Do NOT commit or push on your own: leave changes uncommitted so the owner sees the
   diff in GitHub Desktop and commits them. Licensed assets (DeadRevolver, vHierarchy/vInspector/vFolders) stay out of the repo (see README).

## 3. Folder / file map
```
Assets/Scripts/Player/
  PlayerController2D.cs   movement, jump, dash, wall, hard landing, recoil/pogo hooks (Rigidbody2D, gravityScale forced 0)
  PlayerTuning.cs         ScriptableObject with every tunable (asset: Assets/ScriptableObjects/PlayerTuning.asset)
  PlayerCombat2D.cs       attacks (forward/up/down), combo, cooldown, buffer, recoil, pogo, wall rules
  HitTarget.cs            anything the slash can hit (test dummies)
  PlayerVisuals2D.cs      picks animation states from controller state; wall-slide art fitting (baked sprite edges)
  PlayerHealth2D.cs       health (masks), TakeDamage, knock-back, hit freeze, invulnerability + flicker, hazard respawn, death/checkpoint
  DamageSource2D.cs       enemy body / spikes: hurts the hero while overlapping (polls; kind = Enemy or Hazard)
  SimplePatrol2D.cs       kinematic test enemy that walks between two x positions
  HealthHud2D.cs          IMGUI masks + silk pips top-left (placeholder UI)
Assets/Scripts/Enemies/   EnemyConfig / Motor / Health / Brain / Visuals / Projectile / Fx / ArtSet / SpriteFlipbook2D, EnemyBuilder (editor-only)
Assets/Editor/            EnemyArtGenerator.cs (recolours the pack into Assets/EnemyArt, gitignored), EnemyArenaBuilder.cs (menu items)
  PlayerSilk2D.cs         silk (0-9, +1 per nail hit) and bind (hold F / gamepad North: 1 s, costs 9, heals 3 - all guesses)
  BindEffect2D.cs         our own pixel-art bind / silk effects (dithered glow + square motes); uses Feel/PixelFx.cs
  SprintFx2D.cs           our own pixel-art sprint dust / speed dashes / skid / sprint-jump effects
Assets/Scripts/Feel/PixelFx.cs   pixel-grid helpers + pooled particle layer (32 px per unit, same as the hero sprites)
Assets/Scripts/Camera/
  CameraFollow2D.cs       look-ahead, damping, look-down, lock-zone clamp; keeps BasePosition and adds the screen shake on top
  CameraLockZone.cs       room bounds trigger
Assets/Scripts/Feel/      (namespace Hollowknight.Feel) hit-stop + screen shake
  GameTime.cs             the only writer of Time.timeScale: product of independent time controls (like the original TimeManager)
  FreezeMoment.cs         FreezeType enum + the 15-row (rampDown, wait, rampUp, target) table read from the DLL; Start(type)
  ShakeProfile.cs         shake / rumble / sway profile maths (CameraShakeProfile.GetOffset)
  ShakePresets.cs         named presets (Tiny, Small, Average, AverageQuick, Big, BigQuick, EnemyKill, RumblingFall) - sizes are OUR guesses
  CameraShake2D.cs        on the camera: running shakes, sum clamped to the strongest, accessibility setting, camera freeze-frames
Assets/Scripts/Dev/
  PlaytestBot.cs          EDITOR-ONLY automated playtest (see section 5)
Assets/Editor/
  PlayerSetupMenu.cs, CameraSetupMenu.cs, VisualsSetupMenu.cs, CombatSetupMenu.cs, SpriteEdgeBaker.cs, DamageSetupMenu.cs
Assets/ScriptableObjects/PlayerTuning.asset, NoFriction.physicsMaterial2D
Menu items: Hollowknight > Create Movement Test Setup / Add Camera Follow + Lock Zone / Use DeadRevolver Sprites On Player /
            Add Combat + Hit Dummies / Bake Sprite Edges / Add Health + Damage Test Objects
Tool outside the project: E:\Git Hub\HK-analysis\tools\ildump.cs   (IL reader, see section 6)
Readable C#: `dotnet tool install ilspycmd --tool-path <dir>` was done in a session scratchpad (may be gone); to redo:
  ilspycmd -t HeroController -o <out> "E:\Git Hub\HK-analysis\Assembly-CSharp.dll" -r "E:\Git Hub\HK-analysis"
  REA (MCP) cannot decompile this managed DLL (its providers need Ghidra / do not support .NET) - use ilspycmd or ildump.
```
Namespaces: `Hollowknight.Player`, `Hollowknight.CameraSystem`, `Hollowknight.Feel`, `Hollowknight.Dev`, `Hollowknight.EditorTools`.

## 4. What is built and how it behaves (all matched to the real logic where it could be read)
Controls: A/D or arrows or stick = move; Space / Z / gamepad South = jump; Left Shift / X / gamepad East or Right Trigger = dash;
J / C / left mouse / gamepad West = attack; hold W/Up = up-slash; hold S/Down in the air = down-slash.

- **Jump**: hold = constant upward speed for `jumpSteps` fixed steps (7). Natural end keeps speed and coasts; releasing early after
  `jumpStepsMin` (4) halves upward speed. Air hang (low gravity ramping back while held). Coyote 8 steps, jump buffer 6 steps,
  release buffer. Fixed step 0.02 s. Pressing jump near the ground while falling stays buffered as a landing jump.
- **Double jump**: one air jump, fixed length (6 steps), refreshed by ground, wall slide, wall jump, pogo.
- **Dash**: ground 9 steps / air 8, speed 22, cooldown 23 steps from the dash START, a jump cuts the cooldown to 3 steps, one air
  dash per airtime, dashing into a wall ends in a wall slide, buffered while dashing.
- **Wall**: sticky wall slide (stay until you hold away 8 steps, lose the wall or land), fall speed 3.5, wall jump with kick-off
  speed 12 decaying to run speed over 10 steps, chainable. After a slide ends the hero faces AWAY from the wall. The wall-jump
  animation must not be cancelled by the stale "sliding" flag (fixed).
- **Forgiveness**: corner correction (0.3 u), ledge step-up (0.25 u, airborne only), head-bump no longer locks jumping.
- **Attack**: duration 0.30 s, cooldown 0.36 s, slash window 0.14 s, 2-swing combo that restarts after a pause or direction change,
  buffer 6 steps (held while dashing), sideways hit recoil (5 steps at 8), down-slash pogo (short uncuttable hop, refreshes air
  jump + dash), forward slash on a wall halves slide speed, up-slash on a wall pushes off 0.8 u.
- **Hard landing**: fall at terminal speed > 1.1 s -> next landing locks the hero 0.5 s, input ignored, cancels swing.
- **Camera**: look-ahead slides at 6 u/s toward facing (holds when idle), dash offset, look-down ramps 4/s at near-terminal fall
  speed, lock zones clamp the view, vertical air dead zone (our own idea).
- **Visuals**: DeadRevolver animations driven by `MotionState`; wall-slide art placed on the real opaque pixel edge (baked).
- **Float**: code exists, press-triggered, but **disabled** (`floatEnabled=false`).
- **Health / damage** (added this session; read from `TakeDamage`, `CanTakeDamage`, `<StartRecoil>d__1088`, `<Invulnerable>d__1092`):
  5 masks. A hit is ignored while invulnerable, knocked back, dead or respawning. Otherwise: -health, swing cancelled, then either
  death or invulnerable 1.3 s (flicker) + hit-stop (`FreezeMoment`, see Hit-stop below; was a 0.1 s guess) + knock-back (9,5) for 0.2 s with input ignored,
  gravity off, hero turns toward the source, then stops dead. Spikes (`DamageKind.Hazard`): -1, no knock-back, moved back to the last
  safe ground (a trail lagging 0.25 s) after 0.5 s. Death: falls, 1.6 s later back at the checkpoint with full health (+ brief
  invulnerability, our addition). All values are our guesses. Details in HOLLOWKNIGHT_ANALYSIS.md.
  `Hollowknight > Add Health + Damage Test Objects` is already applied to SampleScene (enemy at x -14..-11, spikes at x 9.5, under
  the root "Damage Test Objects"; the bot switches that root off). Not hand-played yet: the owner should try it and report feel.

- **Hit-stop + screen shake** (added after reading `GameManager.FreezeMoment`, `TimeManager`, `CameraShakeManager/Profile`,
  `HeroController.StartFallRumble`): freeze timings are the DLL's 15-row table; shake maths follows the DLL; sizes/durations and which
  freeze/shake goes with which event are OUR choices (PlayerTuning "Hit-stop & screen shake", `ShakePresets.cs`). Events: first thing
  a swing hits (QuickFreeze + Tiny shake, once per swing), `HitTarget.hitsToKill` death (EnemyDeath freeze + EnemyKill shake), hero hurt
  (HeroDamage freeze 0.28 s + Average shake; replaced the old 0.1 s `damageFreezeTime`), hero death (Big shake), long fall rumble from
  `bigFallTime` until landing/damage, hard landing (Average shake). The camera's `transform.position` now includes the shake; use
  `CameraFollow2D.BasePosition` for the steady position. Not hand-played yet: the owner should feel it and say which sizes to change.

- **Silk + bind** (added after damage; menu `Hollowknight > Add Silk + Bind`, already applied to the Player): nail hits give silk, hold F
  to bind when hurt and the pips pulse. Effects are ORIGINAL pixel art (owner's instruction: do not use Silksong's effect assets, make
  something new in the hero's pixel style). Details and what is guessed: HOLLOWKNIGHT_ANALYSIS.md "Silk and bind".

- **Sprint + sprint jump** (added after silk/bind): HOLD the dash button through a dash (Left Shift / X / gamepad East or Right Trigger) and the
  hero sprints at 14 u/s; jumping while sprinting is a longer sprint jump with forced forward speed; letting go skids. Speeds are guesses (the real
  sprint is a PlayMaker FSM we cannot read); the structure comes from the DLL hooks. `PlayerTuning.sprintEnabled=false` restores the old dash.
  Details: HOLLOWKNIGHT_ANALYSIS.md "Sprint and sprint jump".

- **Ledge mantle** (added after sprint): jump toward a ledge with your head above its top and the hero pulls itself up onto it (0.4 s, jump
  cancels it). The ledge test is the original's (read from the DLL, probes adapted to our wider collider); the climb timing is a guess.
  `PlayerTuning.mantleEnabled=false` restores the old behaviour. Details: HOLLOWKNIGHT_ANALYSIS.md "Ledge mantle / clamber".

- **Enemies** (added after mantle): Gunner, Archer, Brawler, Blade Thrower - recoloured COPIES of the DeadRevolver pack's player animations with its
  weapon sets (owner's idea; no sword enemy because the hero uses it), chasing + jumping, health / nail damage / death, projectiles, original pixel
  effects. The generated art is NOT in the repo (`Assets/EnemyArt/` is gitignored): after cloning run `Hollowknight > Generate Enemy Variants`, then
  `Hollowknight > Build Enemy Test Arena` (SampleScene must be open - the builder refuses a scene without the hero). `HK_ONLY=enemy` /
  `enemyshots`. Details + what is guessed: HOLLOWKNIGHT_ANALYSIS.md "Enemies".
  Environment lesson: the editor once had the pack's DemoScene open and the first arena build went into it; always check the active scene before
  building objects or calling SaveOpenScenes from a command.
  **The owner keeps changing values between turns** (seen so far: nail boxes, Brawler maxHealth 5000, hero `maxHealth` 100, `downSlashStartTime` 0.071). The bot therefore pins what its
tests assume on its own tuning copy (maxHealth 5, hit-stop ON) and derives expected animation frames from the tuning values instead of hard-coding them. A test failing after the
owner played usually means "their value differs from what the test assumed" - check the tuning first, don't revert their number.
**OWNER'S OWN EDITS - NEVER OVERWRITE:** the owner hand-tunes values in the Inspector (nail hitboxes in `PlayerTuning`: forward 1.1x1.2 at (0.8, 0.15), up 1.4x0.8, down 1.4x0.5 at (0, 0);
Brawler `maxHealth` 5000 for testing; maybe more). Do NOT run `Reset Enemy Configs To Defaults` / `GetConfig(reset:true)` / `Build Enemy Test Arena` without asking - they overwrite the
configs and scene enemies. Before touching assets, check for unsaved edits (`EditorUtility.IsDirty`, compare `SerializedObject` with `InternalEditorUtility.LoadSerializedFileAndForget`).
The bot now works on a COPY of the tuning (swapped into the player at start), so a run can never leave tuning edited.
Hero hit-stop is OFF at the owner's request (`heroDamageFreeze = None`); the nail's hit counts on the swing animation's keyframe (see HOLLOWKNIGHT_ANALYSIS.md "Nail hit timing").
**Polishing them one by one from the owner's hand-play reports.** Brawler (round 1, done): the owner saw "no proper contact" and "sudden time stop,
  then the hit". Fixed: hitbox/timing now come from the measured art (`MeleeMove` in `EnemyConfig`: punch fist reaches 1.06 u, kick 0.84 u; the swing is
  stepped by hand so the strike frame and the hitbox go live together), contact box matches the art, knock-back pushes away from the body; the hero now shows
  its hurt pose + a white flash + our own pixel impact burst at the freeze instant (the freeze itself is unchanged, the original's 0.28 s). New: `Assets/Shaders/SpriteFlash.shader`,
  `Feel/SpriteFlash2D.cs`, `Player/HeroHitFx.cs`; bot `HK_ONLY=brawlershots` (PNG frames) and tests S152-S158. Details: HOLLOWKNIGHT_ANALYSIS.md "Brawler polish".
  NEXT: wait for the owner's feedback on the brawler by hand, then do the gunner, archer and blade thrower the same way (measure the art, align the shot with the
  firing frame, check rendered frames). After editing `EnemyConfig`/`EnemyBuilder.Defaults` run `Hollowknight > Reset Enemy Configs To Defaults` (or `GetConfig(kind, reset:true)`)
  and rebuild the arena - the saved config assets and scene enemies do not pick up new defaults by themselves.

Known gaps / not done: enemy armour / stun / drops, more enemy types, mantle hang / flip variants, silk regen / upgrades, back-sprint / wall-sprint launch, per-state camera look-ahead for sprint/harpoon, wall-slide acceleration detail, tools,
silk/resources, real enemies (only dummies, a patrolling test enemy and spikes exist), other damage types, parry, hurt/death
animation check by eye (uses the pack's `HitDamage` and `Die` clips).

## 5. How to test (automated play mode bot)
Unity MCP (`mcp__unity-mcp__Unity_RunCommand`, `Unity_GetConsoleLogs`, `Unity_Camera_Capture`) is connected to the editor.
The bot injects keyboard input via the Input System and prints `[PLAYTEST] PASS|FAIL ...` lines into
`C:\Users\moste\AppData\Local\Unity\Editor\Editor.log`.

Start a run with a RunCommand like:
```csharp
AssetDatabase.Refresh();
if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }   // then call again
EditorPrefs.SetString("HK_MODE", "hunt");
EditorPrefs.SetString("HK_ONLY", "");          // "" = everything (~6 min, 87 verdict lines). Others: damage, combat, float, flooff,
                                               // landcam, wallface, wjface, probe, wall, feel (hit-stop + shake, FS1-FS30)
EditorPrefs.SetBool("HK_PLAYTEST", true);
EditorApplication.isPlaying = true;
```
Read results with bash: find the last line containing `[PLAYTEST] keyboard`, then grep `[PLAYTEST] PASS|FAIL` after it, wait for
`[PLAYTEST] DONE`. TEST SUITE SIZE (owner asked to shrink it): the DEFAULT run (`HK_ONLY=""`) now runs only the newer mechanics - damage S50-S67, hit-stop/shake FS*,
silk + bind S70-S82, sprint S90-S106, mantle S110-S119, enemies S130-S172 = ~125 checks (~6 min). Latest `all` run (old + new, with enemies and the brawler polish): **195 PASS / 0 FAIL**.
The bot sets `Application.runInBackground = true` at start (runtime only): without it the editor stalls the play loop as soon as its window is not in
front (the Claude app on top), which looks like a hung run (frame counter stuck, no `[PLAYTEST]` lines) and probably explains the old "editor stall" notes.
Script compilation also stalls while the editor window is not in front ("COMPILATION_IN_PROGRESS" from every RunCommand, no progress in the log): bring Unity to the
front (PowerShell `(New-Object -ComObject WScript.Shell).AppActivate(<Unity pid>)`) and it finishes at once.
A run only compiles if the scripts compile: a `RunCommand` that reports "compiled" may be using the OLD assembly - after editing scripts call
`AssetDatabase.Refresh()` and check `Unity_GetConsoleLogs` for errors before starting a run. `HK_ONLY=brawlershots` writes PNG frames of a brawler hit.
The bot switches the mantle OFF for every group except `mantle` / `mantleshots` (it would pull the hero onto the scene's platforms and wall tops
and change what the old wall/ledge scenarios measure) and restores it in `Finish()`. `HK_ONLY=mantleshots` writes PNGs of the pull-up. The old scenarios (run, jump, double jump, dash, wall slide/jump, combat, float,
hard landing, camera - all had passed repeatedly) are kept, not deleted, behind `HK_ONLY="legacy"`; `HK_ONLY="all"` runs both (~7 min).
Full copy of the bot before the split: `Backups/PlaytestBot.full-suite.cs.txt`. When a new mechanic touches old movement (jump, dash, wall, landing),
run `legacy` once before calling it done.
Before the split: **136 PASS / 0 FAIL** (119 earlier + 17 sprint lines S90-S106; groups `HK_ONLY=sprint`,
`silk`; `HK_ONLY=bindshots` / `sprintshots` write PNGs of the effects to the folder in EditorPrefs `HK_SHOTDIR`). NOTE: the first play run right
after a script recompile sometimes loses input (a capture run did not sprint / bind once); re-running fixes it - if a test group fails only
right after editing scripts, run it again before suspecting the code.
Earlier: 119 PASS / 0 FAIL (106 earlier incl. hit-stop/shake FS1-FS30, + 13 silk/bind lines S70-S82).
The one capture run where a bind broke early probably hit the same editor-stall / "Asset Pipeline Refresh" problem described next.
A damage-only run once failed S61/S62 (spikes) while an "Asset Pipeline Refresh" was logged in the middle of the test and the hero
had moved only ~0.34 s of game time in the test's 4 s real-time timeout, i.e. an editor stall; it passed on the next two runs. Do not
edit scripts or assets while a run is going (it triggers refreshes). Earlier note on the 87-line state: The FIRST full run after adding damage showed 84/3 (S18a animation sample, S33 down-slash-in-air, S41 float press);
all three are older timing-sensitive scenarios that run before any damage code is active, and they passed on the rerun. The
cause is not identified (not reproduced); treat as an intermittent flake but watch for them. Tip: to wait for a run, count
`[PLAYTEST] keyboard` lines before starting and wait for the count to grow, then for DONE (the previous run's DONE is still in the log).

Test-writing gotchas learned the hard way:
- Teleport (`Tp`) does not reset controller state (air jumps, dash, flags); start from the ground (`Place`) when that matters.
- After pressing a key, wait until the effect really started (input takes 1-3 frames) before reading state.
- Platforms near the test spot cause accidental landings; use open space (x around 1..2 for jumps, shaft at x 12.5..15 for walls).
- `GetComponentInChildren<SpriteRenderer>()` on the Player returns the disabled root placeholder; use the Animator's renderer.
- A RunCommand that enters play mode while already playing does nothing; stop first, then start.
- Wait for compilation before starting a run (a few seconds after editing scripts).
- Existing serialized scene/asset values override new code defaults: set new values through a RunCommand and save
  (`EditorSceneManager.SaveOpenScenes()`, `AssetDatabase.SaveAssets()`).
- Do not leave the camera/player moved after taking screenshots in edit mode (restore before saving the scene).

## 6. How to read the real game logic (method used)
`E:\Git Hub\HK-analysis\tools\ildump.cs` lists the IL of any method (opcodes, constants, field/method names). Run from a scratch
folder with the .NET 10 SDK:
```
dotnet run ildump.cs "E:\Git Hub\HK-analysis\Assembly-CSharp.dll" HeroController Jump 2
dotnet run ildump.cs "E:\Git Hub\HK-analysis\Assembly-CSharp.dll" CameraTarget Update 2
```
(args: dll, type name, method name or `*`, max methods). Useful types: `HeroController`, `CameraController`, `CameraTarget`.
Key `HeroController` methods already studied: Jump, JumpReleased, HeroJump, DoubleJump, Dash, HeroDash, DoWallJump, FixedUpdate,
Update, CanFloat, Attack, DoAttack, DownAttack, Recoil, DoRecoilMovement, FallCheck, DoHardLanding, BackOnGround.
Next to read for damage: `TakeDamage`, `CanTakeDamage`, `StartInvulnerable`, `Invulnerable`, `DamageSelf`, `StartRecoil`,
`CancelDamageRecoil`, `HazardRespawn`, `Die`, fields `INVUL_TIME`, `RECOIL_DURATION`, `RECOIL_VELOCITY`, `DAMAGE_FREEZE_*`.
The REA MCP server (reverse-engineering plugin) was installed but is not needed and was disconnected; ildump works without it.
Constants like `JUMP_SPEED` are serialized fields (not readable from the DLL).

## 7. Suggested next steps
1. (Done) Damage + health + invulnerability - see section 4. Ask the owner how it FEELS by hand (knock-back size, invulnerable
   length, freeze length, flicker, spikes respawn, death delay) and tune `PlayerTuning` "Damage / health".
2. (Done) Hit-stop + screen shake - see section 4. Ask the owner how it FEELS (nail-hit freeze length, shake sizes, fall rumble) and tune.
   Still open: a real death/respawn presentation, shake on blocked hits / clashes (no such mechanic yet).
3. Silk resource + Bind (heal), then sprint, remaining camera states, tools, parry, more damage types.
4. After each mechanic: update `HOLLOWKNIGHT_ANALYSIS.md`, run the full bot, report honestly, ask what next.

## 8. Environment notes
Windows 11; shells: Bash (Git Bash) and PowerShell. The GitHub MCP connector fails to connect (auth header) and is not needed.
Also installed on this machine by request: gstack skills (`E:\Git Hub\gstack`, registered under `~/.claude/skills`), REA
(`E:\Git Hub\REA`, MCP + skill registered in Claude Code), Bun (npm global). The owner's other Unity project
(`E:\Unity Projects\FOG\flip-n-win-unity`) is unrelated; do not touch it.
