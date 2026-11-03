using UnityEngine;

namespace Hollowknight.Silksong
{
    // The physics shell around EnemyBrain: takes the three probes with 2D raycasts, feeds the brain,
    // applies the velocity, flips the sprite by negating the root's local x scale (like Crawler.FlipScaleX;
    // the art faces left unless SpriteFacesRight). Needs a Collider2D on the same object.
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyWalker : MonoBehaviour, IEnemyMotor
    {
        // Tests set this and call Tick themselves (same pattern as HeroController.ManualStepping).
        public bool ManualStepping { get; set; }

        EnemyWalkParams walkParams = EnemyWalkParams.Default;
        LayerMask groundMask = ~0;
        EnemyBrain brain;
        Rigidbody2D body;
        Collider2D bodyCollider;
        int startFacing = 1;
        readonly RaycastHit2D[] hits = new RaycastHit2D[8];

        public EnemyBrain Brain => EnsureBrain();
        public EnemyState State => EnsureBrain().State;
        public int Facing => EnsureBrain().Facing;
        public EnemyWalkParams Params => walkParams;

        public void Configure(EnemyWalkParams p, LayerMask ground, int facing = 1)
        {
            walkParams = p;
            groundMask = ground;
            startFacing = facing < 0 ? -1 : 1;
            brain = new EnemyBrain(p.WalkSpeed, startFacing, p.TurnSteps);
            Setup();
            ApplyFacing(brain.Facing);
            Stubs.NotImplemented("Enemy walker: Turn animation clip, roof/wall crawling, forced turns from noise (floor walking only)");
        }

        void Awake()
        {
            EnsureBrain();
        }

        EnemyBrain EnsureBrain()
        {
            if (brain == null)
            {
                brain = new EnemyBrain(walkParams.WalkSpeed, startFacing, walkParams.TurnSteps);
                Setup();
            }
            return brain;
        }

        void Setup()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = walkParams.GravityScale;
            body.mass = walkParams.Mass;
            body.freezeRotation = true;
        }

        void FixedUpdate()
        {
            if (!ManualStepping) Tick(Time.fixedDeltaTime);
        }

        // Exactly what one FixedUpdate does. Public so tests can run a known number of steps.
        public void Tick(float dt)
        {
            var b = EnsureBrain();
            var probes = b.State == EnemyState.Dead ? default(EnemyProbes) : TakeProbes(dt, b.Facing);
            int before = b.Facing;
            var o = b.Step(dt, probes);
            var v = body.linearVelocity;
            v.x = o.VelocityX;
            body.linearVelocity = v;
            if (o.Facing != before) ApplyFacing(o.Facing);
        }

        public void Shove(float velocityX, float seconds) => EnsureBrain().Shove(velocityX, seconds);

        public void Stop()
        {
            EnsureBrain().Kill();
            if (body != null)
            {
                var v = body.linearVelocity;
                v.x = 0f;
                body.linearVelocity = v;
            }
        }

        void ApplyFacing(int facing)
        {
            var s = transform.localScale;
            float mag = Mathf.Abs(s.x);
            s.x = mag * (walkParams.SpriteFacesRight ? facing : -facing);
            transform.localScale = s;
        }

        EnemyProbes TakeProbes(float dt, int facing)
        {
            var p = walkParams;
            Bounds bd = bodyCollider.bounds;
            float bottom = bd.min.y;
            float front = bd.center.x + facing * bd.extents.x;
            float back = bd.center.x - facing * bd.extents.x;

            float wallLen = Mathf.Max(p.WallRayFrontDistance, Mathf.Abs(p.WalkSpeed) * dt) + p.WallRaySkin;
            return new EnemyProbes
            {
                GroundUnderBackFoot = Cast(new Vector2(back, bottom + p.BackFootHeight), Vector2.down, p.BackFootDown),
                WallAhead = Cast(new Vector2(front - facing * 0.01f, bottom + p.WallRayHeight), new Vector2(facing, 0f), wallLen),
                GroundAhead = Cast(new Vector2(front + facing * p.LedgeAheadPadding, bottom + p.BackFootHeight), Vector2.down, p.LedgeAheadDown),
            };
        }

        // True if the ray touches a solid (non-trigger) collider on the ground mask that is not this enemy.
        bool Cast(Vector2 origin, Vector2 dir, float length)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(groundMask);
            int n = Physics2D.Raycast(origin, dir, filter, hits, length);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || c == bodyCollider) continue;
                if (c.attachedRigidbody == body) continue;
                return true;
            }
            return false;
        }
    }
}
