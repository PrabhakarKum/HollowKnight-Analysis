# 13f. The start screen (Milestone 3, task 6)

Code: `Assets/Silksong/Rooms/StartScreen.cs` (the screen), `Rooms/StartScreenModel.cs` (the rules, no scene needed),
`Rooms/RoomWorldFactory.cs` (builds the world; shared with `RoomWorldStarter`), `Editor/StartScreenBuilder.cs`.
Tests: `Tests/EditMode/StartScreenModelTests.cs`, `StartScreenBuilderTests.cs`, `Tests/PlayMode/StartScreenTests.cs`.

## What it is

A plain text screen with four save slots. It is the way to begin a real game: it calls `GameSession.StartGame` (page 13e),
so a game started here saves into the chosen slot. Rooms played directly (open `Tut_01`, press Play) still work as before
and never write a save.

The screen is drawn with uGUI built in code (no prefab, no art). It reads the keyboard and gamepad directly, and does not
touch `HeroInputActions.inputactions`.

## Controls

| Action | Keyboard | Gamepad |
|---|---|---|
| Move between slots (wraps) | Up / Down (or W / S) | D-pad up / down, left stick |
| Continue (good save) or New game (empty slot) | Enter or Space | A (south) |
| New game over a good save (asks first) | N | X (west) |
| Delete (asks twice) | X or Delete | Y (north) |
| Cancel a question | Escape | B (east) |
| While a game runs: back to this screen | Escape | Start |

## What each slot shows

- **Slot N, then the map zone and max health** for a good save. Play time is a stub and shows nothing yet.
- **Empty** for a free slot. Enter starts a new game.
- **Damaged save** (unreadable file): "This save is damaged. Delete it to use the slot." Enter and N only show this message.
  The only action is Delete, which moves the file aside (it is not destroyed), as `SaveSlots.Delete` does.
- **Newer version**: "This save is from a newer version. Delete it to use the slot." Same rules.

A damaged or newer save is never overwritten without that explicit delete.

## Questions the screen asks

- New game over a good save: "Start a new game over this save?" Enter again confirms, Escape cancels.
- Delete: "Delete this save?" then, after another Enter, "Really delete? This cannot be undone." Escape cancels at either step.
  Nothing is deleted until the second confirm. While a question is open the selection cannot move.

## Starting and leaving a game

1. Enter on a slot hides the screen and runs `GameSession.StartGame` with the shared world factory. If it fails (for
   example the room scene is missing) the screen comes back with the reason on the message line.
2. While the game runs, Escape saves the game (unless the hero is dead) and quits to the screen through `GameSession.Quit`,
   with the rows read again. This is a developer shortcut: there is no pause menu.
3. Another slot can then be started; there is exactly one hero, camera and manager at any time.

## Shared world factory

`RoomWorldFactory.Create(config, actionsAsset, heroSprite, heroClips, artFacesLeft, data)` builds what `RoomWorldStarter`
used to build inline: the throw-away host scene `RoomWorldHost`, the hero input from the actions asset, the world (with the
given save data), the hero art, `HeroInterpolation` and the `AudioListener`. Both the starter and the screen call it. The
starter's behaviour is unchanged.

## How to build and open it

1. Menu **Silksong > Import Hero Animations** (once; optional, otherwise the hero is a box).
2. Menu **Silksong > Build Transition Rooms** (the rooms the game loads).
3. Menu **Silksong > Build Start Screen**. It writes `Assets/Scenes/Rooms/StartScreen.unity` (gitignored like the rooms) and
   opens it. Or run `-executeMethod Hollowknight.Silksong.Editor.StartScreenBuilder.BuildBatch`.
4. Press Play with `StartScreen` open. Build Settings are not changed: in the Editor the rooms load from
   `Assets/Scenes/Rooms/` by path when they are not listed there.

## Not done (stubs)

Art and sound for the screen; the options and settings menu; play time on the slot rows; a real pause menu (`Pause menu`
is logged as a stub); the load fade and bench wake-up animation (from page 13e).

## Notes for builders

The product assembly now references `UnityEngine.UI` (the `com.unity.ugui` package was already installed). The screen has no
event system, so mouse clicks do nothing; it is keyboard and gamepad only.
