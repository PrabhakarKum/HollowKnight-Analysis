namespace Hollowknight.Silksong
{
    // Direction of a nail swing relative to the hero. Kept as its own enum (not a Vector2)
    // because hit rules branch on it: only Down can pogo, and only on a valid target.
    public enum AttackDirection
    {
        Normal = 0,
        Up = 1,
        Down = 2
    }
}
