using UnityEngine;

namespace Hollowknight.Enemies
{
    /// <summary>Picks the animation state (one per generated clip) from what the brain and motor are doing, and mirrors the sprite to the facing.</summary>
    public class EnemyVisuals2D : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] float crossFade = 0.04f;
        [SerializeField] EnemyConfig config;

        public string CurrentClip { get; private set; }

        EnemyConfig cfg;
        EnemyMotor2D motor;
        EnemyBrain2D brain;
        bool scrubbing;

        void Awake()
        {
            motor = GetComponent<EnemyMotor2D>();
            brain = GetComponent<EnemyBrain2D>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (sprite == null && animator != null) sprite = animator.GetComponent<SpriteRenderer>();
            cfg = config;
        }

        public void Configure(EnemyConfig c, Animator anim, SpriteRenderer sr)
        {
            config = c; cfg = c; animator = anim; sprite = sr;
        }

        void OnEnable()
        {
            if (motor == null) motor = GetComponent<EnemyMotor2D>();
            if (motor != null) motor.Landed += OnLanded;
        }

        void OnDisable() { if (motor != null) motor.Landed -= OnLanded; }

        void OnLanded() => EnemyFx.Dust(new Vector2(transform.position.x, motor.FeetY + 0.03f));

        void Update()
        {
            if (cfg == null || animator == null || sprite == null) return;
            sprite.flipX = motor.Facing < 0;

            // a wind-up / blow / shot is stepped through by hand, from the brain's own clock: the damage or projectile and the frame
            // that shows it are the same moment (a separate animator clock ran behind and the damage landed before the animation)
            if (brain.TryGetAttackPose(out var pose))
            {
                ScrubPose(pose);
                return;
            }
            if (scrubbing) { scrubbing = false; animator.speed = 1f; CurrentClip = null; }
            WindupOffset = 0f;
            ScrubClipTime = -1f;

            string target = Choose();
            if (target != CurrentClip && !string.IsNullOrEmpty(target))
            {
                CurrentClip = target;
                animator.CrossFadeInFixedTime(target, crossFade, 0, 0f);
            }
        }

        /// <summary>How far the body is leaned back from where it stands (world x, signed), during a melee wind-up. 0 otherwise.</summary>
        public float WindupOffset { get; private set; }

        /// <summary>Where in the clip the hand-played pose currently is, in clip seconds (-1 when the normal animation is playing).</summary>
        public float ScrubClipTime { get; private set; } = -1f;

        void ScrubPose(EnemyBrain2D.AttackPose pose)
        {
            WindupOffset = -motor.Facing * Mathf.Round(pose.lean * 32f) / 32f;                        // whole art pixels
            scrubbing = true;
            CurrentClip = pose.clip;
            ScrubClipTime = pose.time;
            animator.speed = 0f;                                                                      // we set the time ourselves every frame
            animator.Play(pose.clip, 0, pose.time / pose.length);
            animator.Update(0f);                                                                      // show it now, even while the game is frozen
        }

        // after the animator has chosen the frame: lean the body (the pack's controller may reset the visual's position, so it is set every frame)
        void LateUpdate()
        {
            if (animator == null) return;
            var tr = animator.transform;
            var lp = tr.localPosition;
            lp.x = WindupOffset;
            tr.localPosition = lp;
        }

        string Choose()
        {
            switch (brain.State)
            {
                case EnemyState.Dead: return cfg.dieClip;
                case EnemyState.Hurt: return cfg.hurtClip;
                case EnemyState.Telegraph: return cfg.telegraphClip;
                case EnemyState.Attack: return cfg.attackClip;
                case EnemyState.Recover: return cfg.recoverClip;
            }
            var v = motor.Velocity;
            if (!motor.IsGrounded) return v.y > 1.5f ? cfg.jumpRiseClip : (v.y < -1.5f ? cfg.jumpFallClip : cfg.jumpMidClip);
            float speed = Mathf.Abs(v.x);
            if (speed > cfg.walkSpeed + 0.8f) return cfg.runClip;
            if (speed > 0.4f) return brain.Aggro ? cfg.runClip : cfg.walkClip;
            return brain.Aggro ? cfg.aggroIdleClip : cfg.idleClip;
        }
    }
}
