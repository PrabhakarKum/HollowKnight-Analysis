# Silksong architecture map

A plain-language map of how the original game's main pieces connect, and which seams this project replaces for the playable-hero milestone.

## The pieces

**Game manager.** A single long-lived object that owns the big state of a play session: which scene is loaded, whether the game is paused, saving and loading, and starting scene transitions. Almost everything else reaches it through a global "instance" handle. It is the conductor, so it has a very large surface.

**Player data.** One big bag of named values (health, silk, collected items, which doors are open, which bosses are beaten). The game manager holds the live copy; saving serialises it to a slot file and loading replaces it. Other systems read and write it directly, which is why it is easy to reach but hard to test.

**Hero controller.** The player character. It is one large class that reads input each frame and runs the movement, dash, wall, attack and damage behaviours, tracking them in a set of state flags (on ground, dashing, wall sliding, attacking, invulnerable and so on). Tuning numbers live in a separate config asset per hero kind. It pulls player data, the game manager, the cameras and the input handler from their global handles.

**Input.** An input handler wraps a third-party input library and exposes a set of named actions (move, jump, attack, dash and so on) that the hero polls. Keyboard and gamepad bindings are both stored there.

**Cameras.** A camera rig object holds the main camera plus effect cameras. A camera controller follows a target that tracks the hero, clamps to the current room's bounds, and handles shake and screen flashes. The hero triggers shakes and flashes by reaching into the rig.

**Scene transitions.** Each room is a scene. Doors and gates are transition points: the hero touches one, the game manager fades out, loads the target scene, finds the matching named entry gate, places the hero there, then fades in. A per-scene manager supplies room settings such as bounds and lighting.

**Health and hits.** Enemies carry a health component. An attack builds a hit description (damage, direction, attacker) and hands it to the target's health component, which applies damage, knockback and death. The attack direction (normal, up, down) decides things like pogo bounces.

## How they connect

Input feeds the hero. The hero reads and writes player data, asks the game manager to change scenes, and tells the camera rig about shakes. The game manager owns player data and saving. The camera follows the hero and reads room bounds from the scene manager. Enemies receive hit descriptions from the hero's attack.

## Seams this plan replaces

- Global instance handles for the game manager and player data: replaced by plain objects passed in, so the pieces can be tested without a running game.
- Third-party input library: replaced by Unity's Input System behind a small hero-input interface, so tests can feed fake input.
- Visual-scripting state machines that drive animation and some hero behaviour: replaced by hand-written C# and a small animator interface.
- The proprietary 2D sprite animation library: replaced by Unity sprite animation, converted from exported data in the editor.
- Platform services (store achievements, cloud saves): replaced by one small platform class that does nothing yet and logs stubs.
- Whole-game scene transitions and the full manager: out of scope for this milestone; a single room with respawn markers stands in, and unfinished features call the stub helper so they are listed in `STUBS.md`.
