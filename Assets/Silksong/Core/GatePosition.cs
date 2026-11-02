namespace Hollowknight.Silksong
{
    // Which edge of the room a gate sits on. Decides which way the hero walks out and in.
    public enum GatePosition
    {
        Top,
        Right,
        Bottom,
        Left,
        Door,
        Unknown
    }

    public static class GatePositions
    {
        // Silksong derives the position from the gate OBJECT'S NAME, not from a setting, so level builders
        // only have to name objects "left1", "right2", ... Matching is a case-sensitive substring test in a
        // fixed order (top, right, left, bot, door), copied so names written for the original keep working.
        // A name that matches nothing is Unknown (the original returns it too, no exception).
        // The original also has an isADoor flag that forces Door; this port has no such field and always
        // ignores doors (stub), so the flag is not modelled. A "door" name still parses as Door.
        public static GatePosition FromName(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                if (name.Contains("top")) return GatePosition.Top;
                if (name.Contains("right")) return GatePosition.Right;
                if (name.Contains("left")) return GatePosition.Left;
                if (name.Contains("bot")) return GatePosition.Bottom;
                if (name.Contains("door")) return GatePosition.Door;
            }
            return GatePosition.Unknown;
        }
    }
}
