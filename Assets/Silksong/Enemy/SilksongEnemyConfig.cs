using UnityEngine;

namespace Hollowknight.Silksong
{
    // What kind of hurt an enemy's touch is. The original's DamageHero has a hazard type; 1 is "enemy".
    // The hero's TakeHit has no damage-type parameter (spikes and enemies both go through it), so for now the
    // type is only data on the config.
    public enum EnemyDamageType
    {
        Enemy = 1
    }

    // The numbers of one enemy kind: the Moss Creep ("MossBone Crawler") for the first enemy. Every value is
    // from docs/study/14-enemies.md section 7 unless marked INFERRED. The walker (Task 3) takes its own plain
    // struct; the integration task copies the walk/probe numbers from here into it.
    // Create instances with ScriptableObject.CreateInstance; the asset itself is made in a later task.
    [CreateAssetMenu(fileName = "EnemyConfig", menuName = "Silksong/Enemy Config")]
    public class SilksongEnemyConfig : ScriptableObject
    {
        [Header("Health and contact")]
        // INFERRED (study row 4): original 10 hp / nail 5 = 2 hits; our nail does 1.
        public int maxHealth = SilksongEnemyConfigDefaults.MaxHealth;
        public int contactDamage = SilksongEnemyConfigDefaults.ContactDamage;
        public EnemyDamageType damageType = SilksongEnemyConfigDefaults.DamageType;

        [Header("Hit flash (seconds, strength 0..1)")]
        public float flashHoldSeconds = SilksongEnemyConfigDefaults.FlashHoldSeconds;
        public float flashFallSeconds = SilksongEnemyConfigDefaults.FlashFallSeconds;
        public float flashStrength = SilksongEnemyConfigDefaults.FlashStrength;

        [Header("Shove when hit")]
        public float shoveSpeed = SilksongEnemyConfigDefaults.ShoveSpeed;
        public float shoveSeconds = SilksongEnemyConfigDefaults.ShoveSeconds;

        [Header("Walking (Task 3 numbers)")]
        public float walkSpeed = SilksongEnemyConfigDefaults.WalkSpeed;
        // Ray down from the back edge: starts this far above the box bottom, this long. No ground = in the air, never turn.
        public float groundBackRayHeight = SilksongEnemyConfigDefaults.GroundBackRayHeight;
        public float groundBackRayLength = SilksongEnemyConfigDefaults.GroundBackRayLength;
        // Ray forward from the front edge: max(this, speed x dt) + skin. A hit means a wall: turn.
        public float wallRayDistance = SilksongEnemyConfigDefaults.WallRayDistance;
        public float wallRaySkin = SilksongEnemyConfigDefaults.WallRaySkin;
        // Ray down from this far ahead of the front edge, this long. No ground = a ledge: turn.
        public float ledgeRayForwardPadding = SilksongEnemyConfigDefaults.LedgeRayForwardPadding;
        public float ledgeRayLength = SilksongEnemyConfigDefaults.LedgeRayLength;

        [Header("Body")]
        public Vector2 colliderSize = SilksongEnemyConfigDefaults.ColliderSize;
        public Vector2 colliderOffset = SilksongEnemyConfigDefaults.ColliderOffset;
        public float gravityScale = SilksongEnemyConfigDefaults.GravityScale;
        public float mass = SilksongEnemyConfigDefaults.Mass;

        [Header("Persistence")]
        // The original Moss Creep is NOT persistent; the plan chooses stay-dead for the first enemy.
        public bool staysDead = SilksongEnemyConfigDefaults.StaysDead;
        // Full key = prefix + "-" + a per-placement id chosen by the room (PersistentBool wants an explicit id).
        public string persistenceIdPrefix = SilksongEnemyConfigDefaults.PersistenceIdPrefix;

        void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            contactDamage = Mathf.Max(0, contactDamage);
            flashHoldSeconds = Mathf.Max(0f, flashHoldSeconds);
            flashFallSeconds = Mathf.Max(0f, flashFallSeconds);
            flashStrength = Mathf.Clamp01(flashStrength);
            shoveSpeed = Mathf.Max(0f, shoveSpeed);
            shoveSeconds = Mathf.Max(0f, shoveSeconds);
        }
    }

    // The same numbers as constants, so tests can compare against the study without a config instance.
    public static class SilksongEnemyConfigDefaults
    {
        public const int MaxHealth = 2;
        public const int ContactDamage = 1;
        public const EnemyDamageType DamageType = EnemyDamageType.Enemy;

        public const float FlashHoldSeconds = 0.07f;
        public const float FlashFallSeconds = 0.05f;
        public const float FlashStrength = 0.85f;

        public const float ShoveSpeed = 15f;
        public const float ShoveSeconds = 0.15f;

        public const float WalkSpeed = 3f;
        public const float GroundBackRayHeight = 0.6f;
        public const float GroundBackRayLength = 1.1f;
        public const float WallRayDistance = 0.2f;
        public const float WallRaySkin = 0.1f;
        public const float LedgeRayForwardPadding = 0.2f;
        public const float LedgeRayLength = 1.1f;

        public static readonly Vector2 ColliderSize = new Vector2(1.64f, 1.11f);
        public static readonly Vector2 ColliderOffset = new Vector2(-0.03f, -0.20f);
        public const float GravityScale = 1f;
        public const float Mass = 1f;

        public const bool StaysDead = true;
        public const string PersistenceIdPrefix = "enemy";
    }
}
