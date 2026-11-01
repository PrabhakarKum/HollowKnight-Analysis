# 04 Input

Summary in my own words of how the original reads input and what this project copies. Sources are in
`D:\Asset HollowKnight\HollowKnightSilksong\Assets\Scripts\Assembly-CSharp` (read only, InControl based).

## Default bindings in the original

| Action | Keyboard | Gamepad | Where |
|---|---|---|---|
| Move left / right / up / down | arrow keys | d-pad and left stick | `InputHandler.cs` (keyboard layout method, `MapDefaultKeyboardLayout`; stick and d-pad in the default-bindings block near line 1180) |
| Jump | Z | south face button (Action1) | `InputHandler.cs` (`ResetDefaultKeyBindings`), `ControllerMapping.cs` |
| Attack | X | west face button (Action3) | same |
| Dash | C | right trigger | same |

Other defaults seen, not built in Milestone 1: cast A / Action2, super dash S / left trigger, dream nail D / Action4,
quick map Tab / left bumper, quick cast F / right bumper, taunt V / right stick click.

The gamepad face-button names are position based: Action1 is the south button, Action3 the west one.

## Stick and direction handling

- `HeroActions.cs` defines left, right, up, down as four separate actions, joined into a two-axis `MoveVector`.
- Horizontal press threshold is 0.3; vertical press threshold is 0.5 (so a slightly tilted stick still runs, but
  up/down needs a firmer push).
- The two-axis vector uses a lower dead zone of 0.15 and an upper dead zone of 0.95.
- A right stick vector also exists. `InputHandler.GetSticksInput` returns whichever stick is pushed further, and
  clamps its length to 1.

## Button timing rules

- The hero reads each button as "is down", "went down this frame" and "went up this frame" (`WasPressed`,
  `WasReleased`, `IsPressed`), once per frame.
- A press that cannot act yet is remembered in a counter and retried. In `HeroController.cs`: jump queue is 2
  steps, jump-release queue 2 steps, double jump queue 10 steps, attack queue 8 steps; dash has a configurable
  queue. A queue only keeps going while the button is still held; letting go cancels a queued dash or attack.
- Releasing jump is acted on (also through a short release queue), which is what cuts the jump short. Variable
  jump height therefore needs `WasReleased` and `IsPressed`.
- "Steps" are fixed-update steps, not frames, so the queues belong to the hero tasks (Task 7+), not to this layer.

## What this project does

- `HeroInputActions.inputactions` holds the bindings above on the Input System.
- `HeroActions` samples once per `Update` and turns the result into `InputButton` snapshots (`IsPressed`,
  `WasPressed`, `WasReleased`, `HeldSeconds`) behind `IHeroInput`. Queue timers are not here; hero code owns them.
- Opposite keys held give 0 on that axis (the 2D vector composite cancels them).
- `OnApplicationFocus(false)` calls `ReleaseAll()`: all buttons and the stick are cleared, a release is reported
  once, and a key still physically down stays off until it is let go and pressed again.

### Deliberate deviations

- Extra keyboard alternates: WASD for movement and Space for jump, added alongside the original defaults. The
  original has no such keys (A and D belong to cast and dream nail there), but the Task 4 tests use them and they
  are comfortable. Cast and dream nail are not built, so no conflict yet.
- The dead zone values are applied to the left stick only; the d-pad and keys are digital.
- Trigger press point uses the Input System default (0.5); the original's value was not found.
- No rebinding and no controller-type specific layouts (the original has per-pad-type mapping).
