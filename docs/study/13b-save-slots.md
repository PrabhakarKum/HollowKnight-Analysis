# Save slots (Milestone 3, task 2)

Code: `Assets/Silksong/Data/SaveSlots.cs`, plus two small additions to `SaveSystem.cs` (`Peek`, `DeleteSlot`).
Not wired into the hero, the rooms or any scene yet (task 5 does that).

## What it does
- Four slots (0 to 3). `SaveSlots.SlotCount`.
- `Summary(slot)`: Empty, Ok, Corrupt or NewerVersion. For Ok it gives the map zone and max health, which is what Silksong shows. It only reads: it never renames, quarantines or writes anything (`SaveSystem.Peek`).
- `StartNew(slot)`: writes a fresh game to the slot at once and returns it. Empty and Ok slots are accepted (starting over an Ok slot overwrites it, so the menu must ask first). Corrupt, newer-version or unreadable slots are refused with a message; the player must delete first.
- `Load(slot)`: returns the saved data with silk set to 0 (the original does this on load). Health stays as saved. Empty, corrupt or newer slots return a failure.
- `Delete(slot)`: good saves are deleted. Corrupt files are moved aside as `*.corrupt`, newer-version files as `*.newer`, so nothing is silently destroyed. Deleting the current slot clears the selection.
- `CurrentSlot` (-1 = none), `Select(slot)`, `Autosave(data)`. Autosave with no current slot does nothing and returns false. `ResetForTests()` clears the selection.
- Results are `SlotResult` (Success, Message, Data). Expected problems never throw.

## Stubbed
- Play time: `PlayTimeText(slot)` reports `Stubs.NotImplemented("Save slot play time")` once and returns an empty string.
- A locked/unreadable file shows as Corrupt in the summary (file untouched).