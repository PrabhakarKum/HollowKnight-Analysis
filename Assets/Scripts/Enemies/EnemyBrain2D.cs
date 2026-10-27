using System;
using UnityEngine;
using Hollowknight.Player;

namespace Hollowknight.Enemies
{
    public enum EnemyState { Patrol, Pause, Reposition, Telegraph, Attack, Recover, Hurt, Dead }

    /// <summary>
    /// The enemy's decisions: patrol its home range, notice the hero, then hunt it - the melee kind chases and strikes, the ranged kinds keep
    /// a distance band and shoot (a telegraphed wind-up, the shot(s), a recovery). It jumps onto obstacles and platforms in its way, jumps
    /// after a hero standing above it, and steps off ledges after a hero below it. A hit staggers it and cancels an attack in progress.
    /// </summary>
    [RequireComponent(typeof(EnemyMotor2D))]
    public class EnemyBrain2D : MonoBehaviour
    {
        public EnemyState State { get; private set; } = EnemyState.Patrol;
        public bool Aggro { get; private set; }
        public int AttacksStarted { get; private set; }
        public int ShotsFired { get; private set; }
        public int JumpsMade { get; private set; }
        public EnemyConfig Config => cfg;
        public bool InAir => !motor.IsGrounded;
        public int AttackIndex { get; private set; }
        /// <summary>Seconds since the current state began.</summary>
        public float StateTime => Time.time - stateStart;
        /// <summary>The blow being wound up / thrown (melee kinds), or null.</summary>
        public MeleeMove CurrentMove { get; private set; }
        /// <summary>The blow it will throw next: they take turns.</summary>
        public MeleeMove NextMove => cfg != null && cfg.moves != null && cfg.moves.Length > 0 ? cfg.moves[AttackIndex % cfg.moves.Length] : null;
        public event Action<EnemyState> StateChanged;

        [SerializeField] EnemyConfig config;
        [SerializeField] DamageSource2D melee, contact;
        EnemyConfig cfg;
        EnemyMotor2D motor;
        EnemyArtSet art;
        PlayerController2D hero;
        PlayerHealth2D heroHealth;
        BoxCollider2D meleeBox;

        float homeX;
        int patrolDir = 1;
        float pauseUntil;
        float stateEnd;
        float stateStart;
        float hitStart, hitEnd;
        int shotCount;
        float shotGap;
        float loseAggroAt;
        float nextAttackAt;
        float nextJumpAt;
        int shotsLeft;
        float nextShotAt;
        bool exclaimed, aimLineShown;

        void Awake()
        {
            motor = GetComponent<EnemyMotor2D>();
            art = GetComponent<EnemyArtSet>();
            if (config != null) Apply();
        }

        /// <summary>Stores the config and the two damage sources (saved with the scene) and applies them.</summary>
        public void Configure(EnemyConfig c, DamageSource2D meleeSource, DamageSource2D contactSource)
        {
            config = c; melee = meleeSource; contact = contactSource;
            if (motor == null) motor = GetComponent<EnemyMotor2D>();
            if (art == null) art = GetComponent<EnemyArtSet>();
            Apply();
        }

        void Apply()
        {
            cfg = config;
            if (melee != null)
            {
                meleeBox = melee.GetComponent<BoxCollider2D>();
                if (cfg.moves != null && cfg.moves.Length > 0) meleeBox.size = cfg.moves[0].hitSize;
                melee.damage = cfg.damage;
                melee.active = false;
                melee.knockOrigin = transform;          // pushed away from the enemy's body, not from the swing box in front of it
            }
            if (contact != null) { contact.damage = cfg.damage; contact.active = cfg.contactDamage; contact.knockOrigin = transform; }
        }

        void Start()
        {
            homeX = transform.position.x;
            patrolDir = UnityEngine.Random.value < 0.5f ? -1 : 1;
            motor.Face(patrolDir);
        }

        // ------------------------------------------------------------------ helpers

        void EnsureHero()
        {
            if (hero == null) { hero = FindFirstObjectByType<PlayerController2D>(); if (hero != null) heroHealth = hero.GetComponent<PlayerHealth2D>(); }
        }

        Vector2 Chest => (Vector2)motor.BodyCollider.bounds.center + new Vector2(0f, 0.2f);
        Vector2 HeroChest => hero != null ? (Vector2)hero.GetComponent<Collider2D>().bounds.center : Vector2.zero;
        Vector2 Muzzle => (Vector2)transform.position + new Vector2(cfg.muzzleOffset.x * motor.Facing, cfg.muzzleOffset.y);

        bool CanSeeHero()
        {
            if (hero == null || (heroHealth != null && (heroHealth.IsDead || heroHealth.IsRespawning))) return false;
            Vector2 d = HeroChest - Chest;
            if (Mathf.Abs(d.x) > cfg.sightRange || Mathf.Abs(d.y) > cfg.sightHeight) return false;
            return !motor.LineBlocked(Chest, HeroChest);
        }

        void SetState(EnemyState s)
        {
            if (State == s) return;
            State = s;
            stateStart = Time.time;
            StateChanged?.Invoke(s);
        }

        // ------------------------------------------------------------------ main loop

        void FixedUpdate()
        {
            if (cfg == null || State == EnemyState.Dead) return;
            EnsureHero();

            if (State == EnemyState.Hurt)
            {
                if (Time.time >= stateEnd) SetState(Aggro ? EnemyState.Reposition : EnemyState.Patrol);
                return;
            }

            bool sees = CanSeeHero();
            if (sees) { Aggro = true; loseAggroAt = Time.time + cfg.loseSightTime; }
            else if (Aggro && Time.time > loseAggroAt) Aggro = false;

            switch (State)
            {
                case EnemyState.Patrol:
                case EnemyState.Pause:
                    if (Aggro) { SetState(EnemyState.Reposition); goto case EnemyState.Reposition; }
                    DoPatrol();
                    break;
                case EnemyState.Reposition:
                    if (!Aggro) { SetState(EnemyState.Patrol); break; }
                    DoReposition();
                    break;
                case EnemyState.Telegraph:
                    FaceHero();
                    motor.Stop();
                    TelegraphTells();
                    if (Time.time >= stateEnd) StartAttack();
                    break;
                case EnemyState.Attack:
                    motor.Stop();
                    UpdateAttack();
                    break;
                case EnemyState.Recover:
                    motor.Stop();
                    if (Time.time >= stateEnd) { nextAttackAt = Time.time + cfg.cooldown; SetState(Aggro ? EnemyState.Reposition : EnemyState.Patrol); }
                    break;
            }
        }

        void FaceHero()
        {
            if (hero == null) return;
            motor.Face(hero.transform.position.x >= transform.position.x ? 1 : -1);
        }

        // ------------------------------------------------------------------ patrol

        void DoPatrol()
        {
            if (State == EnemyState.Pause)
            {
                motor.Stop();
                if (Time.time >= pauseUntil) SetState(EnemyState.Patrol);
                return;
            }
            if (!motor.IsGrounded) return;
            bool atEdge = motor.LedgeAhead(patrolDir) && !cfg.canDropLedges;
            bool tooFar = (transform.position.x - homeX) * patrolDir > cfg.patrolRange;
            if (motor.WallAhead(patrolDir) || atEdge || tooFar)
            {
                patrolDir = -patrolDir;
                motor.Stop();
                pauseUntil = Time.time + cfg.patrolPause;
                SetState(EnemyState.Pause);
                motor.Face(patrolDir);
                return;
            }
            motor.Face(patrolDir);
            motor.MoveX(patrolDir * cfg.walkSpeed);
        }

        // ------------------------------------------------------------------ hunting

        void DoReposition()
        {
            if (hero == null) return;
            if (!motor.IsGrounded) return;                                   // committed to a jump: keep the air speed, decide on landing

            Vector2 hc = HeroChest, c = Chest;
            float dx = hc.x - c.x, adx = Mathf.Abs(dx), dy = hc.y - c.y;
            int dir = dx >= 0f ? 1 : -1;
            motor.Face(dir);
            bool heroBelow = dy < -1.2f;
            bool attackReady = Time.time >= nextAttackAt && Mathf.Abs(dy) <= cfg.sightHeight;

            if (cfg.kind == EnemyKind.Brawler)
            {
                var next = NextMove;
                // it starts braking early enough to come to rest at the move's range (it slides to a stop while the wind-up begins,
                // instead of freezing on the spot from a full run)
                float vx = Mathf.Abs(motor.Velocity.x), brake = 1.25f * vx * vx / (2f * Mathf.Max(1f, cfg.acceleration));   // (x1.25: measured, it travels a little more than v^2/2a)
                if (next != null && adx <= next.startRange + brake && Mathf.Abs(dy) < 1.6f && attackReady) { StartTelegraph(); return; }
                if (!MoveToward(dir, cfg.chaseSpeed, heroBelow)) motor.Stop();
            }
            else
            {
                bool tooClose = adx < cfg.rangedMin, tooFar = adx > cfg.rangedMax;
                if (tooFar) { if (!MoveToward(dir, cfg.walkSpeed * 1.4f, heroBelow)) motor.Stop(); }
                else if (tooClose)
                {
                    // back away, unless a wall or a ledge is behind
                    int away = -dir;
                    if (!motor.WallAhead(away) && !(motor.LedgeAhead(away) && !cfg.canDropLedges)) motor.MoveX(away * cfg.walkSpeed * 1.4f);
                    else motor.Stop();
                }
                else motor.Stop();
                if (!tooFar && adx > cfg.rangedMin * 0.6f && attackReady && !motor.LineBlocked(Muzzle, hc)) { StartTelegraph(); return; }
            }

            TryJump(dir, adx, dy);
        }

        /// <summary>Walk toward the hero; stops at walls it cannot jump and at ledges unless the hero is below. Returns false if it did not move.</summary>
        bool MoveToward(int dir, float speed, bool heroBelow)
        {
            if (motor.WallAhead(dir) && !(cfg.canJump && motor.ObstacleHeight(dir, cfg.maxJumpHeight) > 0f)) return false;
            if (motor.LedgeAhead(dir) && !cfg.canDropLedges && !heroBelow && !cfg.canJump) return false;
            if (motor.LedgeAhead(dir) && !cfg.canDropLedges && !heroBelow && !CanJumpGap(dir)) return false;
            motor.MoveX(dir * speed);
            return true;
        }

        bool CanJumpGap(int dir) => cfg.canJump && Time.time >= nextJumpAt;

        void TryJump(int dir, float adx, float dy)
        {
            if (!cfg.canJump || Time.time < nextJumpAt || !motor.IsGrounded) return;

            // 1. an obstacle or platform edge in the way
            if (motor.WallAhead(dir))
            {
                float h = motor.ObstacleHeight(dir, cfg.maxJumpHeight);
                if (h > 0.2f) { JumpTo(h + 0.5f, dir * cfg.jumpForwardSpeed); return; }
            }
            // 2. the hero is above and close: jump up after it
            if (dy > 1.2f && adx < 4f)
            {
                float need = Mathf.Min(dy + 0.7f, cfg.maxJumpHeight);
                if (need > 1.0f) { JumpTo(need, dir * cfg.jumpForwardSpeed * 0.8f); return; }
            }
            // 3. a gap in front and the hero beyond it (not below)
            if (motor.LedgeAhead(dir) && dy > -1.2f && adx > 1.5f) JumpTo(1.2f, dir * cfg.jumpForwardSpeed * 1.2f);
        }

        void JumpTo(float height, float vx)
        {
            motor.Jump(motor.JumpSpeedFor(height), vx);
            nextJumpAt = Time.time + cfg.jumpCooldown;
            JumpsMade++;
        }

        // ------------------------------------------------------------------ attacks

        void StartTelegraph()
        {
            AttacksStarted++;
            exclaimed = false; aimLineShown = false;
            CurrentMove = cfg.kind == EnemyKind.Brawler ? NextMove : null;
            SetState(EnemyState.Telegraph);
            stateEnd = Time.time + cfg.telegraphTime;
            if (CurrentMove != null && Mathf.Abs(motor.Velocity.x) > 2.5f)
                EnemyFx.Dust(new Vector2(transform.position.x, motor.FeetY + 0.03f));      // a skid puff as it brakes out of the run
            motor.Stop();
        }

        void TelegraphTells()
        {
            if (!exclaimed)
            {
                exclaimed = true;
                EnemyFx.Exclaim((Vector2)transform.position + new Vector2(0f, 1.15f), cfg);
            }
            bool ranged = cfg.kind != EnemyKind.Brawler;
            if (ranged && !aimLineShown && Time.time >= stateEnd - cfg.telegraphTime * 0.5f && hero != null)
            {
                aimLineShown = true;
                EnemyFx.AimLine(Muzzle, HeroChest, cfg);
            }
        }

        void StartAttack()
        {
            SetState(EnemyState.Attack);
            motor.Stop();
            switch (cfg.kind)
            {
                case EnemyKind.Brawler:
                {
                    var mv = CurrentMove ?? NextMove;
                    CurrentMove = mv;
                    stateEnd = Time.time + mv.FollowThrough;
                    hitStart = Time.time + mv.hitDelay;                      // the limb is seen reaching out first, then it connects
                    hitEnd = hitStart + mv.hitWindow;
                    if (melee != null)
                    {
                        meleeBox.size = mv.hitSize;
                        melee.transform.localPosition = new Vector3(mv.hitOffset.x * motor.Facing, mv.hitOffset.y, 0f);
                        melee.active = mv.hitDelay <= 0f;
                    }
                    break;
                }
                default:                                                    // gunner (a burst), archer, blade thrower (one shot)
                {
                    shotCount = cfg.kind == EnemyKind.Gunner ? Mathf.Max(1, cfg.burstCount) : 1;
                    shotsLeft = shotCount;
                    var sm = cfg.shot;
                    if (sm != null && sm.Timed)
                    {
                        // each shot plays its own animation, and the projectile leaves when the clip reaches its release frame
                        shotGap = Mathf.Max(cfg.burstGap, sm.ReleaseDelay + 0.05f);
                        nextShotAt = Time.time + sm.ReleaseDelay;
                        stateEnd = Time.time + (shotCount - 1) * shotGap + sm.Span;
                    }
                    else                                                    // no timing data: the old behaviour, the projectile leaves at once
                    {
                        shotGap = cfg.burstGap;
                        nextShotAt = Time.time;
                        stateEnd = Time.time + (cfg.kind == EnemyKind.Gunner ? cfg.burstGap * shotsLeft + cfg.activeTime : cfg.activeTime);
                    }
                    break;
                }
            }
        }

        void UpdateAttack()
        {
            if (cfg.kind != EnemyKind.Brawler && shotsLeft > 0 && Time.time >= nextShotAt)
            {
                Fire();
                shotsLeft--;
                nextShotAt += shotGap;
            }
            if (melee != null)
            {
                if (!melee.active && Time.time >= hitStart && Time.time < hitEnd) melee.active = true;
                else if (melee.active && Time.time >= hitEnd) melee.active = false;              // the limb is only dangerous while it is out
            }
            if (Time.time >= stateEnd) EndAttack();
        }

        // ------------------------------------------------------------------ animation pose (wind-up and swing / shot are played by hand)

        /// <summary>One frame to show: which clip, how long it is, where in it to be, and how far to lean the body back (world units, unsigned).</summary>
        public struct AttackPose { public string clip; public float length, time, lean; }

        static float EaseOut(float x) => 1f - (1f - x) * (1f - x);
        static float EaseIn(float x) => x * x;
        /// <summary>0 to 1 over the first 70 % of the wind-up, then back to 0 over the last 30 %: lean back, then snap forward into the blow.</summary>
        static float LeanCurve(float u) => u < 0.7f ? EaseOut(u / 0.7f) : 1f - EaseIn((u - 0.7f) / 0.3f);

        /// <summary>
        /// The animation pose during a wind-up (Telegraph) or a blow / shot (Attack), computed from the state's own clock so the animation and
        /// the damage / projectile come from the same time and can never drift apart. False = play the normal clip for the state.
        /// </summary>
        public bool TryGetAttackPose(out AttackPose pose)
        {
            pose = default;
            if (cfg == null || (State != EnemyState.Telegraph && State != EnemyState.Attack)) return false;
            float e = StateTime;
            float u = Mathf.Clamp01(e / Mathf.Max(0.05f, cfg.telegraphTime));

            var m = CurrentMove;
            if (m != null)                                                   // melee
            {
                pose.clip = m.clip; pose.length = m.clipLength;
                if (State == EnemyState.Telegraph)
                {
                    // the wind-up frames are stretched evenly over the telegraph (a held pose looked like the game stopping), and it never reaches the strike frame itself
                    pose.time = Mathf.Max(0f, m.strikeTime - 0.005f) * u;
                    pose.lean = LeanCurve(u) * m.pullBack;
                }
                else
                {
                    float slow = m.SlowSpan;
                    pose.time = m.strikeTime + (e <= slow ? e * m.hitSpeed : slow * m.hitSpeed + (e - slow) * m.swingSpeed);
                }
                pose.time = Mathf.Clamp(pose.time, 0f, pose.length - 0.001f);
                return true;
            }

            var sm = cfg.shot;
            if (sm == null) return false;
            if (State == EnemyState.Telegraph)
            {
                if (string.IsNullOrEmpty(sm.windupClip)) return false;
                pose.clip = sm.windupClip; pose.length = sm.windupClipLength;
                pose.time = Mathf.Clamp(sm.windupTo * u, 0f, pose.length - 0.001f);
                pose.lean = LeanCurve(u) * sm.windupLean;
                return true;
            }
            if (!sm.Timed) return false;
            int i = Mathf.Clamp(Mathf.FloorToInt(e / Mathf.Max(0.01f, shotGap)), 0, Mathf.Max(0, shotCount - 1));
            pose.clip = sm.clip; pose.length = sm.clipLength;
            pose.time = Mathf.Clamp(sm.StartTime + (e - i * shotGap) * sm.speed, 0f, pose.length - 0.001f);
            return true;
        }

        void EndAttack()
        {
            if (melee != null) melee.active = false;
            AttackIndex++;
            SetState(EnemyState.Recover);
            stateEnd = Time.time + cfg.recoverTime;
        }

        void Fire()
        {
            if (hero == null) return;
            ShotsFired++;
            Vector2 from = Muzzle, to = HeroChest;
            bool right = motor.Facing > 0;
            switch (cfg.kind)
            {
                case EnemyKind.Gunner:
                {
                    Vector2 d = (to - from);
                    d.x = Mathf.Abs(d.x) < 0.5f ? motor.Facing * 0.5f : d.x;
                    // aim at the hero but never steeper than ~35 degrees from horizontal
                    float ang = Mathf.Clamp(Mathf.Atan2(d.y, Mathf.Abs(d.x)) * Mathf.Rad2Deg, -35f, 35f) * Mathf.Deg2Rad;
                    var vel = new Vector2(Mathf.Cos(ang) * motor.Facing, Mathf.Sin(ang)) * cfg.projectileSpeed;
                    EnemyProjectile2D.Spawn(EnemyProjectile2D.Mode.Straight, from, vel, cfg, art, transform, right);
                    if (art != null) SpriteFlipbook2D.PlayOnce(from + new Vector2(motor.Facing * 0.15f, 0f), art.muzzleFlash, art.effectFps, !right, 1f);
                    break;
                }
                case EnemyKind.Archer:
                {
                    Vector2 d = to - from;
                    float dist = Mathf.Max(1f, d.magnitude);
                    float T = dist / cfg.projectileSpeed;                  // flight time at the arrow's speed
                    float g = cfg.projectileGravity;
                    var vel = new Vector2(d.x / T, d.y / T + 0.5f * g * T);  // lobbed so that it reaches the hero's chest
                    EnemyProjectile2D.Spawn(EnemyProjectile2D.Mode.Ballistic, from, vel, cfg, art, transform, right, g);
                    break;
                }
                case EnemyKind.BladeThrower:
                {
                    Vector2 d = to - from;
                    var vel = d.normalized * cfg.projectileSpeed;
                    EnemyProjectile2D.Spawn(EnemyProjectile2D.Mode.Boomerang, from, vel, cfg, art, transform, right);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ being hit / dying

        public void OnHurt(int awayX, AttackDir dir)
        {
            if (State == EnemyState.Dead) return;
            if (melee != null) melee.active = false;
            Aggro = true; loseAggroAt = Time.time + cfg.loseSightTime;
            SetState(EnemyState.Hurt);
            stateEnd = Time.time + cfg.hurtTime;
            nextAttackAt = Mathf.Max(nextAttackAt, Time.time + cfg.hurtTime + 0.2f);
            var kb = cfg.hitKnockback;
            if (dir == AttackDir.Up) motor.Knock(new Vector2(awayX * kb.x * 0.3f, kb.y * 1.6f), cfg.hurtTime);
            else if (dir == AttackDir.Down) motor.Knock(new Vector2(awayX * kb.x * 0.3f, 0f), cfg.hurtTime);
            else motor.Knock(new Vector2(awayX * kb.x, kb.y), cfg.hurtTime);
        }

        /// <summary>Test helper: skips to the next melee move in the rotation (the brawler's punch to its kick).</summary>
        public void DebugSkipMove() { AttackIndex++; }

        public void OnDeath(int awayX)
        {
            SetState(EnemyState.Dead);
            Aggro = false;
            if (melee != null) melee.active = false;
            if (contact != null) contact.active = false;
            motor.Stop();
            motor.Knock(new Vector2(awayX * cfg.hitKnockback.x * 1.2f, cfg.hitKnockback.y * 1.4f), 0.4f);
        }
    }
}
