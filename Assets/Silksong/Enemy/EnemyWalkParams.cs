namespace Hollowknight.Silksong
{
    // Plain data for the walking brain. Numbers are the Moss Creep's (docs/study/14-enemies.md section 7).
    // A later integration task fills this from SilksongEnemyConfig. "S" = read from the original,
    // "INFERRED" = our proposal, not in the data.
    [System.Serializable]
    public struct EnemyWalkParams
    {
        public float WalkSpeed;            // S: 3 u/s
        public float BackFootHeight;       // S: back ray starts 0.6 above the box bottom
        public float BackFootDown;         // S: back ray length 1.1
        public float WallRayFrontDistance; // S: 0.2 (the ray is max(this, speed*dt) + WallRaySkin long)
        public float WallRaySkin;          // S: 0.1
        public float WallRayHeight;        // INFERRED: 0.2 above the box bottom ("just above the floor")
        public float LedgeAheadPadding;    // S: 0.2 beyond the front edge
        public float LedgeAheadDown;       // S: 1.1 long (its start height, BackFootHeight, is INFERRED)
        public float ShoveSpeed;           // S: 15 u/s (used by Task 6 when it asks for a shove)
        public float ShoveSeconds;         // S: 0.15 s
        public int TurnSteps;              // S: 1 physics step
        public float GravityScale;         // S: 1
        public float Mass;                 // S: 1
        public bool SpriteFacesRight;      // S: false (the original art faces left)

        public static EnemyWalkParams Default => new EnemyWalkParams
        {
            WalkSpeed = 3f,
            BackFootHeight = 0.6f,
            BackFootDown = 1.1f,
            WallRayFrontDistance = 0.2f,
            WallRaySkin = 0.1f,
            WallRayHeight = 0.2f,
            LedgeAheadPadding = 0.2f,
            LedgeAheadDown = 1.1f,
            ShoveSpeed = 15f,
            ShoveSeconds = 0.15f,
            TurnSteps = 1,
            GravityScale = 1f,
            Mass = 1f,
            SpriteFacesRight = false,
        };
    }
}
