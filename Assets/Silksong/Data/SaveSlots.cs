using UnityEngine;

namespace Hollowknight.Silksong
{
    public enum SlotState { Empty, Ok, Corrupt, NewerVersion }

    // What a slot menu shows for one slot. MapZone and MaxHealth are only meaningful when State is Ok.
    public struct SlotSummary
    {
        public SlotState State;
        public string MapZone;
        public int MaxHealth;
    }

    // Outcome of a slot operation. Expected problems (wrong slot state) come back here, never as exceptions.
    public struct SlotResult
    {
        public bool Success;
        public string Message;
        public PlayerData Data;

        public static SlotResult Ok(PlayerData data) => new SlotResult { Success = true, Message = "", Data = data };
        public static SlotResult Fail(string message) => new SlotResult { Success = false, Message = message };
    }

    // Four save slots on top of SaveSystem, plus "which slot is the current game in".
    // Static on purpose (one game session); call ResetForTests() to clear the current slot.
    public static class SaveSlots
    {
        public const int SlotCount = 4;

        // -1 means no game is running in a slot, so autosave does nothing.
        public static int CurrentSlot { get; private set; } = -1;

        public static void ResetForTests()
        {
            CurrentSlot = -1;
        }

        public static bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;

        // Returns false (and leaves the current slot alone) for an out-of-range slot.
        public static bool Select(int slot)
        {
            if (!IsValidSlot(slot)) return false;
            CurrentSlot = slot;
            return true;
        }

        public static void Deselect()
        {
            CurrentSlot = -1;
        }

        // Read-only: never writes, renames or quarantines. An unreadable (locked) file reports as Corrupt
        // here so the menu can show something, but the file itself is not touched.
        public static SlotSummary Summary(int slot)
        {
            if (!IsValidSlot(slot)) return new SlotSummary { State = SlotState.Empty };
            PlayerData data;
            switch (SaveSystem.Peek(slot, out data))
            {
                case SaveSystem.SlotFileState.Missing:
                    return new SlotSummary { State = SlotState.Empty };
                case SaveSystem.SlotFileState.Valid:
                    return new SlotSummary { State = SlotState.Ok, MapZone = data.mapZone, MaxHealth = data.maxHealth };
                case SaveSystem.SlotFileState.Newer:
                    return new SlotSummary { State = SlotState.NewerVersion };
                default:
                    return new SlotSummary { State = SlotState.Corrupt };
            }
        }

        // Play time is not tracked yet. Slot menus may call this; it reports the stub once and returns "".
        public static string PlayTimeText(int slot)
        {
            Stubs.NotImplemented("Save slot play time");
            return "";
        }

        // Writes a fresh game into the slot straight away. Empty and Ok slots are accepted (an Ok slot is
        // overwritten; the UI should confirm first). Corrupt, newer-version or unreadable slots are refused:
        // the caller must Delete first, which keeps the old bytes aside.
        public static SlotResult StartNew(int slot)
        {
            if (!IsValidSlot(slot)) return SlotResult.Fail("No such save slot.");
            SlotState state = Summary(slot).State;
            if (state == SlotState.Corrupt) return SlotResult.Fail("This slot holds an unreadable save. Delete it first.");
            if (state == SlotState.NewerVersion) return SlotResult.Fail("This slot was saved by a newer version of the game. Delete it first.");

            PlayerData fresh = PlayerData.CreateNew();
            if (!SaveSystem.TrySave(fresh, slot)) return SlotResult.Fail("Could not write the save file.");
            return SlotResult.Ok(fresh);
        }

        // Load rule from the original: silk always starts at 0 on load; health stays as saved.
        public static SlotResult Load(int slot)
        {
            if (!IsValidSlot(slot)) return SlotResult.Fail("No such save slot.");
            PlayerData data;
            switch (SaveSystem.Peek(slot, out data))
            {
                case SaveSystem.SlotFileState.Valid:
                    data.silk = 0;
                    return SlotResult.Ok(data);
                case SaveSystem.SlotFileState.Missing:
                    return SlotResult.Fail("This slot is empty.");
                case SaveSystem.SlotFileState.Newer:
                    return SlotResult.Fail("This slot was saved by a newer version of the game.");
                default:
                    return SlotResult.Fail("This slot holds an unreadable save.");
            }
        }

        // Player-requested delete. Good saves are removed; corrupt or newer files are moved aside, not destroyed.
        public static SlotResult Delete(int slot)
        {
            if (!IsValidSlot(slot)) return SlotResult.Fail("No such save slot.");
            if (!SaveSystem.DeleteSlot(slot)) return SlotResult.Fail("Could not delete the save file.");
            if (CurrentSlot == slot) CurrentSlot = -1;
            return SlotResult.Ok(null);
        }

        // Saves into the current slot. No current slot: does nothing and returns false.
        public static bool Autosave(PlayerData data)
        {
            if (!IsValidSlot(CurrentSlot) || data == null) return false;
            return SaveSystem.TrySave(data, CurrentSlot);
        }
    }
}