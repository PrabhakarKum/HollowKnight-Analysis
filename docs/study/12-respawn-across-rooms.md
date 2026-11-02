# 12 - Respawn across rooms (Milestone 2, task 8)

Plain-language summary of what was built, in my own words.

## Two kinds of "come back"

- **Hazard respawn** (spikes): the hero is hurt for 1 and put back on safe ground in the SAME room.
- **Death respawn**: after the death delay the hero comes back at the bench, which may be in ANOTHER room.

## What is built

- `HazardRespawnMarker`: an empty marker with a facing. It looks straight down for ground (no ground is fine: it then uses its own spot).
- `HazardRespawnTrigger`: a trigger box. When the hero touches it, the marker's spot and the room name go into `PlayerData.hazardRespawn`.
- A spot is only valid in the room it was saved in. In another room it is ignored and cleared; then the room's `Start` marker is used.
- Gate arrival: the gate's hazard marker (field `respawnMarker`, or a marker child) becomes the spot. A gate with `nonHazardGate` sets nothing (and clears a spot from the old room). No marker: the hero's arrival position is used.
- `HazardZone` has a switch "respawns hero" (off by default so the first milestone room is unchanged). On: a hit that does not kill puts the hero at the spot.
- Death: `DeathRespawnController` chooses the bench from `respawnScene` and `respawnMarker`. Same room: respawn in place. Other room: `RoomTransitionManager.RequestRespawn` fades out, loads the bench room, places the SAME hero at the marker with full health, fades in. The camera snaps to the new room. If the marker name is missing the room's `Start` marker is used; empty data means Tut_01 `Start`.
- Bench decision: a `RespawnMarker` flagged "sets bench respawn" becomes the bench when the world starts in its room. Only the Tut_01 `Start` marker has the flag (drawn with a brown seat).

## Try it by hand (rebuild the rooms first: Silksong > Build Transition Rooms)

1. Open Tut_01 and press Play. Walk right along the ledge: step on the pale yellow box, then into the red spikes. You lose 1 health and reappear at the yellow strip.
2. Go through right1 into Tut_01b. Touch the spikes straight away: you reappear at the strip a few units inside the gate. Walk to a yellow box, then the spikes: you reappear there.
3. Kill yourself in Tut_01b (K key or spikes until dead): after about 1.6 s the screen fades, Tut_01 loads, and you stand at the bench with full health and control.
4. In Tut_01 die again: you come back in place. Repeat in Tut_02.

## Stubbed

Fade and animation on hazard respawn, benches you can sit on, death screen, silk loss and cocoon, saving the spot to disk.
