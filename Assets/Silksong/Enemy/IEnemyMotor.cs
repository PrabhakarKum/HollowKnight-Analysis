namespace Hollowknight.Silksong
{
    // What the enemy's health/death code needs from whatever moves the enemy (Task 6 wires
    // EnemyHealth.Shoved / Died to this).
    public interface IEnemyMotor
    {
        // Push the enemy sideways at velocityX (signed, +right) for seconds; walking carries on underneath.
        void Shove(float velocityX, float seconds);

        // Halt for good (the enemy died).
        void Stop();
    }
}
