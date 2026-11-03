using System.Collections.Generic;

namespace Hollowknight.Silksong
{
    // What the screen should do after a key press (the model itself only deletes files).
    public enum StartScreenAction { None, StartNew, Continue }

    // Where the slot menu is: browsing, or answering a question.
    public enum StartScreenPhase { Browsing, ConfirmNew, ConfirmDelete1, ConfirmDelete2 }

    // One line of the slot list.
    public sealed class SlotRow
    {
        public int Slot;
        public SlotState State;
        public string Title = "";
        public string Detail = "";
        public bool CanContinue;
        public bool CanStartNew;
        public bool CanDelete;
    }

    // The logic of the start screen with no scene and no UI, so it is tested in EditMode: four slot rows from
    // SaveSlots.Summary, the selection, the New / Continue / Delete rules and the two-step delete question.
    //
    //  - Empty slot: Submit starts a new game. Delete does nothing ("Nothing to delete").
    //  - Ok slot: Submit continues. N (RequestNew) asks before starting a new game over the save. Delete asks twice.
    //  - Corrupt or newer-version slot: Submit and N only explain; the single available action is Delete, which moves
    //    the file aside (SaveSlots.Delete). A bad file is never overwritten without that explicit choice.
    //  - Cancel backs out of any question. Moving the selection is ignored during a question.
    public sealed class StartScreenModel
    {
        public const string DamagedMessage = "This save is damaged. Delete it to use the slot.";
        public const string NewerMessage = "This save is from a newer version. Delete it to use the slot.";
        public const string ConfirmNewMessage = "Start a new game over this save? Enter again to confirm, Esc to cancel.";
        public const string ConfirmDelete1Message = "Delete this save?";
        public const string ConfirmDelete2Message = "Really delete? This cannot be undone.";

        readonly List<SlotRow> rows = new List<SlotRow>();

        public IReadOnlyList<SlotRow> Rows => rows;
        public int Selected { get; private set; }
        public StartScreenPhase Phase { get; private set; } = StartScreenPhase.Browsing;
        // The status / question line under the list.
        public string Message { get; private set; } = "";

        public StartScreenModel()
        {
            Refresh();
        }

        public SlotRow SelectedRow => rows[Selected];

        // Re-reads all slots. A question that is open stays open.
        public void Refresh()
        {
            rows.Clear();
            for (int i = 0; i < SaveSlots.SlotCount; i++) rows.Add(MakeRow(i));
        }

        public static SlotRow MakeRow(int slot)
        {
            var summary = SaveSlots.Summary(slot);
            var row = new SlotRow { Slot = slot, State = summary.State, Title = "Slot " + (slot + 1) };
            switch (summary.State)
            {
                case SlotState.Ok:
                    row.Detail = "Zone: " + summary.MapZone + "   Max health: " + summary.MaxHealth;
                    string play = SaveSlots.PlayTimeText(slot);
                    if (!string.IsNullOrEmpty(play)) row.Detail += "   " + play;
                    row.CanContinue = true;
                    row.CanStartNew = true;
                    row.CanDelete = true;
                    break;
                case SlotState.Empty:
                    row.Detail = "Empty";
                    row.CanStartNew = true;
                    break;
                case SlotState.NewerVersion:
                    row.Detail = NewerMessage;
                    row.CanDelete = true;
                    break;
                default:
                    row.Detail = DamagedMessage;
                    row.CanDelete = true;
                    break;
            }
            return row;
        }

        public void Select(int index)
        {
            if (Phase != StartScreenPhase.Browsing) return;
            if (index >= 0 && index < rows.Count) Selected = index;
        }

        public void MoveDown() { Move(1); }
        public void MoveUp() { Move(-1); }

        void Move(int step)
        {
            if (Phase != StartScreenPhase.Browsing) return;
            Selected = ((Selected + step) % rows.Count + rows.Count) % rows.Count;
            Message = "";
        }

        // Enter / Space / gamepad A.
        public StartScreenAction Submit()
        {
            var row = SelectedRow;
            switch (Phase)
            {
                case StartScreenPhase.ConfirmNew:
                    Phase = StartScreenPhase.Browsing;
                    Message = "";
                    return StartScreenAction.StartNew;
                case StartScreenPhase.ConfirmDelete1:
                    Phase = StartScreenPhase.ConfirmDelete2;
                    Message = ConfirmDelete2Message;
                    return StartScreenAction.None;
                case StartScreenPhase.ConfirmDelete2:
                    PerformDelete();
                    return StartScreenAction.None;
            }

            if (row.CanContinue) { Message = ""; return StartScreenAction.Continue; }
            if (row.State == SlotState.Empty) { Message = ""; return StartScreenAction.StartNew; }
            Message = row.State == SlotState.NewerVersion ? NewerMessage : DamagedMessage;
            return StartScreenAction.None;
        }

        // N / gamepad X: a new game in the selected slot. Over a good save it asks first.
        public StartScreenAction RequestNew()
        {
            if (Phase != StartScreenPhase.Browsing) return StartScreenAction.None;
            var row = SelectedRow;
            if (row.State == SlotState.Empty) { Message = ""; return StartScreenAction.StartNew; }
            if (row.State == SlotState.Ok)
            {
                Phase = StartScreenPhase.ConfirmNew;
                Message = ConfirmNewMessage;
                return StartScreenAction.None;
            }
            Message = row.State == SlotState.NewerVersion ? NewerMessage : DamagedMessage;
            return StartScreenAction.None;
        }

        // X / Delete key / gamepad Y: the first of the two questions.
        public void RequestDelete()
        {
            if (Phase != StartScreenPhase.Browsing) return;
            if (!SelectedRow.CanDelete)
            {
                Message = "Nothing to delete.";
                return;
            }
            Phase = StartScreenPhase.ConfirmDelete1;
            Message = ConfirmDelete1Message;
        }

        // Escape / gamepad B: leave any question. Returns whether there was one to leave.
        public bool Cancel()
        {
            if (Phase == StartScreenPhase.Browsing) return false;
            Phase = StartScreenPhase.Browsing;
            Message = "Cancelled.";
            return true;
        }

        void PerformDelete()
        {
            int slot = SelectedRow.Slot;
            Phase = StartScreenPhase.Browsing;
            var result = SaveSlots.Delete(slot);
            Message = result.Success ? "Slot " + (slot + 1) + " deleted." : result.Message;
            Refresh();
        }

        // For the screen: a start that failed, or any other plain line.
        public void ShowMessage(string message)
        {
            Message = message ?? "";
        }
    }
}
