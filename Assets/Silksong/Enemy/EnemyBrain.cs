namespace Hollowknight.Silksong
{
    public enum EnemyState { Walking, Turning, Dead }

    // One snapshot of the three ground/wall checks, taken by the shell before each step.
    public struct EnemyProbes
    {
        public bool GroundUnderBackFoot; // ray down from the back edge found ground
        public bool WallAhead;           // ray forward from the front edge hit terrain
        public bool GroundAhead;         // ray down from just past the front edge found ground
    }

    public struct EnemyBrainOutput
    {
        public float VelocityX;  // desired horizontal velocity (walk + shove)
        public int Facing;       // +1 right, -1 left
        public EnemyState State;
        public bool Turned;      // true on the step the turn was decided
    }

    // The Moss Creep's whole behaviour as a pure state machine (docs/study/14c-enemy-brain.md).
    // No UnityEngine physics; deterministic.
    public class EnemyBrain
    {
        const float TimeEpsilon = 1e-5f;

        readonly float walkSpeed;
        readonly int turnSteps;
        int facing;
        EnemyState state = EnemyState.Walking;
        int turnStepsLeft;
        float shoveVelocity;
        float shoveSecondsLeft;

        public EnemyBrain(float walkSpeed, int startFacing, int turnSteps = 1)
        {
            this.walkSpeed = walkSpeed;
            this.turnSteps = turnSteps < 1 ? 1 : turnSteps;
            facing = startFacing < 0 ? -1 : 1;
        }

        public EnemyState State => state;
        public int Facing => facing;
        public bool IsShoved => shoveSecondsLeft > TimeEpsilon;

        // A stronger shove replaces one in progress; a weaker one is ignored (study section 2).
        public void Shove(float velocityX, float seconds)
        {
            if (state == EnemyState.Dead || seconds <= 0f) return;
            if (IsShoved && System.Math.Abs(velocityX) < System.Math.Abs(shoveVelocity)) return;
            shoveVelocity = velocityX;
            shoveSecondsLeft = seconds;
        }

        public void Kill()
        {
            state = EnemyState.Dead;
            shoveVelocity = 0f;
            shoveSecondsLeft = 0f;
        }

        public EnemyBrainOutput Step(float dt, EnemyProbes probes)
        {
            var o = new EnemyBrainOutput();
            if (state == EnemyState.Dead)
            {
                o.Facing = facing; o.State = EnemyState.Dead; return o;
            }

            float shove = 0f;
            bool shoved = IsShoved;
            if (shoved)
            {
                shove = shoveVelocity;
                shoveSecondsLeft -= dt;
            }

            float walk = 0f;
            if (state == EnemyState.Turning)
            {
                // The step that decided the turn already flipped facing and held still (step 1 of turnSteps);
                // walking resumes on the step after the last one.
                turnStepsLeft--;
                if (turnStepsLeft <= 0)
                {
                    state = EnemyState.Walking;
                    walk = facing * walkSpeed;
                }
            }
            else if (!shoved && ShouldTurn(probes))
            {
                facing = -facing;
                state = EnemyState.Turning;
                turnStepsLeft = turnSteps;
                o.Turned = true;
            }
            else
            {
                walk = facing * walkSpeed;
            }

            // A turn step stops the walk but the shove still pushes.
            o.VelocityX = walk + shove;
            o.Facing = facing;
            o.State = state;
            return o;
        }

        // The study's order: no ground under the back foot means no turn at all (in the air);
        // then a wall ahead; then no ground ahead (ledge).
        static bool ShouldTurn(EnemyProbes p)
        {
            if (!p.GroundUnderBackFoot) return false;
            if (p.WallAhead) return true;
            if (!p.GroundAhead) return true;
            return false;
        }
    }
}
