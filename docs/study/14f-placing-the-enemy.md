# 14f: putting the Moss Creep together and placing it

Plain-language summary of Milestone 4, Task 6. The rules and numbers of the enemy are in `14-enemies.md` to `14e-hit-feel.md`;
this page is about how the pieces were joined and where the enemy stands.

## One component joins the pieces

`Enemy/MossCreep` reads one `SilksongEnemyConfig` and builds a whole enemy from it, so the config is the only place the numbers live.
From the config it sets: health and damage, the hit flash timings, the shove speed and time, the walker (speed, the three probe rays,
gravity, mass), the body size, and whether the enemy stays dead and the prefix of its save id.

What it puts on the object:

- a solid box collider, so the enemy stands on the floor;
- a second box of the same size that is a trigger, which the hero's body overlaps for contact damage;
- a Rigidbody2D, `EnemyHealth`, `EnemyContactDamage`, `EnemyHitFlash`, `EnemyWalker`, `EnemyPersistence` (with its saved flag);
- the green placeholder box, fitted to the config's body size.

It also connects "got shoved" to the walker's shove and "died" to the walker's stop, and disconnects them when the enemy is destroyed.

Two ways to make one: `MossCreep.Create(config, id, position, facingRight, parent)` builds a complete enemy in code (tests use it), and
the room builder puts a bare `MossCreep` (config, save id, facing) in the room file; it builds the rest when the room loads. So a changed
number in the config shows up on the next Play without rebuilding rooms.

## The save id

The room gives the explicit id `Tut_01b_MossCreep_1`. The save key is the room's scene name plus that id, the same rule as the Tut_01 wall
and pickup. An enemy made with no id uses the config prefix plus its object name, but the rooms never rely on that.

## What the walker may stand on

All terrain, the hero and the enemies sit on the Default physics layer in the generated rooms, so a layer mask cannot tell them apart. The
walker's rays use every layer except Ignore Raycast, ignore triggers, and now skip the hero and any enemy (a small fix in `EnemyWalker`:
before, the hero counted as a wall and the enemy turned away from you). Terrain is everything else.

## The hero is not blocked

The enemy's solid collider and the hero's body ignore each other (`Physics2D.IgnoreCollision`, set as soon as a hero exists, retried every
few physics steps). The hero walks through the enemy and the trigger box reports the overlap, so contact damage works and nothing shoves
the hero around. The nail finds both boxes (it asks for triggers too); each swing hits one enemy once.

## Where it stands, and why there is a step

Tut_01b has a 2-high block (the Bump, x 40 to 46) on the floor, away from the gates (at least 40 units), the spikes, the hazard triggers and
the Start marker. The creep walks on top of it; the drop at each end turns it round (the ledge probe), and the block is 6 wide while the creep
is 1.64, so there is room for a shove of 2.25 units without falling off.

On flat ground the creep would almost be out of reach. The hero's side swing is a box 1.2 high centred 0.15 above the hero's root, and the
root is 1.55 above the feet, so the swing covers only 1.1 to 2.3 above the feet. The creep is 1.11 high: on the hero's floor the two overlap
by 0.03. This is a property of the hand-tuned swing box (Milestone 1, not changed here), not of the enemy. So a 1-high step (`MossStep`,
x 37 to 40, against the Bump's left wall) was added: standing on it, the swing covers the whole creep when it is near the step end of its
stretch. Nothing existing moved.

Recommendation for a later task: lower the swing's vertical offset (`AttackSideOffset.y`) or make the box taller so a standing hero
can hit a floor-level enemy, then the step can go. The owner's call; the values were left alone.

## Where the config asset lives

`Assets/ScriptableObjects/SilksongEnemies/MossCreep.asset`, a new folder (the old prototype's `Assets/ScriptableObjects/Enemies` is not used).
It is generated and gitignored like the rooms: building the transition rooms, or the menu **Silksong > Build Moss Creep Config**, creates it
from the study numbers if missing and never overwrites an existing one.

## Tests

- EditMode: `MossCreepTests` (config to walker mapping, id rule, floor placement), `TransitionRoomsBuilderTests` (one enemy, Tut_01b only,
  explicit id, stands on solid ground with a ledge at both ends, clear of gates, hazards, spikes and Start, the step puts the enemy inside the
  swing, the config asset is created on demand and gitignored).
- PlayMode: `MossCreepAssemblyTests` (every piece from a config, the hero not blocked and not a wall, shove and death reach the walker),
  `M4AcceptanceTests` (the real rooms: alive and walking, touch hurts by 1, two real swings kill with silk and recoil, stays dead after a gate
  trip, after rest, quit and continue, alive in another slot, stays-dead off brings it back).
