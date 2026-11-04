using System;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // The first enemy, put together: reads ONE SilksongEnemyConfig and wires every enemy piece from it, so the config
    // is the single home of the numbers (docs/study/14f-placing-the-enemy.md).
    //
    // Two ways in, same result:
    //  - Create(...) builds a whole enemy in code (tests, anything that spawns one).
    //  - A MossCreep component placed by the room builder holds only the config, the save id and the facing (these
    //    serialize); the pieces below are assembled in Awake when the room loads. Nothing but this component is stored
    //    in the room file, so a number changed in the config asset applies without rebuilding rooms.
    //
    // Pieces (all on this object): a solid BoxCollider2D (stands on the floor), a second trigger BoxCollider2D of the
    // same size (the hero's body overlaps it for contact damage; the nail finds both), Rigidbody2D, EnemyHealth,
    // EnemyContactDamage, EnemyHitFlash, EnemyWalker, EnemyPersistence (+ its PersistentBool) and the placeholder box.
    // EnemyHealth.Shoved -> walker.Shove and EnemyHealth.Died -> walker.Stop are connected here and disconnected on destroy.
    //
    // The hero must not be blocked by the enemy: the solid collider ignores the hero's body collider
    // (Physics2D.IgnoreCollision, set as soon as a hero exists). Contact still happens through the trigger.
    //
    // Ground for the walker's rays: every layer except Ignore Raycast. The rooms put terrain, the hero and enemies on
    // the Default layer, so the walker itself skips the hero and any enemy (EnemyWalker.Cast), and trigger colliders
    // are never ground.
    public class MossCreep : MonoBehaviour
    {
        // How far above the floor the enemy is dropped so it settles instead of starting inside it.
        public const float SpawnClearance = 0.02f;

        [SerializeField] SilksongEnemyConfig config;
        [SerializeField] string placementId;
        [SerializeField] bool startFacingRight = true;

        bool assembled;
        BoxCollider2D solid;
        BoxCollider2D contactTrigger;
        EnemyHealth health;
        EnemyWalker walker;
        EnemyContactDamage contact;
        EnemyPersistence persistence;
        EnemyHitFlash flash;
        Rigidbody2D body;
        Action<float, float> shoveHandler;
        Action diedHandler;
        HeroHealth ignoredHero;
        int scanCountdown;

        public SilksongEnemyConfig Config => config;
        public string PlacementId => placementId;
        public bool StartFacingRight => startFacingRight;
        public bool IsAssembled => assembled;
        public EnemyHealth Health => health;
        public EnemyWalker Walker => walker;
        public EnemyContactDamage Contact => contact;
        public EnemyPersistence Persistence => persistence;
        public EnemyHitFlash Flash => flash;
        public Rigidbody2D Body => body;
        public BoxCollider2D SolidCollider => solid;
        public BoxCollider2D ContactTrigger => contactTrigger;

        // ---------- pure helpers (EditMode-testable) ----------

        // What the walker's rays hit: all layers but Ignore Raycast. Documented above.
        public static LayerMask GroundMask => Physics2D.DefaultRaycastLayers;

        // Every config field that the walker uses, mapped onto its plain struct. WallRayHeight is not in the config
        // (the study has no value; the walker's inferred default stands) and TurnSteps/SpriteFacesRight are fixed.
        public static EnemyWalkParams WalkParamsFrom(SilksongEnemyConfig c)
        {
            var p = EnemyWalkParams.Default;
            if (c == null) return p;
            p.WalkSpeed = c.walkSpeed;
            p.BackFootHeight = c.groundBackRayHeight;
            p.BackFootDown = c.groundBackRayLength;
            p.WallRayFrontDistance = c.wallRayDistance;
            p.WallRaySkin = c.wallRaySkin;
            p.LedgeAheadPadding = c.ledgeRayForwardPadding;
            p.LedgeAheadDown = c.ledgeRayLength;
            p.ShoveSpeed = c.shoveSpeed;
            p.ShoveSeconds = c.shoveSeconds;
            p.GravityScale = c.gravityScale;
            p.Mass = c.mass;
            return p;
        }

        // The save id: the explicit placement id when there is one, else the config's prefix + "-" + the object's name.
        public static string PersistenceIdFor(SilksongEnemyConfig c, string explicitId, string objectName)
        {
            if (!string.IsNullOrEmpty(explicitId)) return explicitId;
            string prefix = c != null ? c.persistenceIdPrefix : SilksongEnemyConfigDefaults.PersistenceIdPrefix;
            return prefix + "-" + objectName;
        }

        // Where the root goes so the collider's bottom edge is SpawnClearance above a floor at floorTop.
        public static float RootYOnFloor(SilksongEnemyConfig c, float floorTop)
        {
            Vector2 size = c != null ? c.colliderSize : SilksongEnemyConfigDefaults.ColliderSize;
            Vector2 offset = c != null ? c.colliderOffset : SilksongEnemyConfigDefaults.ColliderOffset;
            return floorTop + size.y * 0.5f - offset.y + SpawnClearance;
        }

        // ---------- building ----------

        // Gives a bare MossCreep its three serialized values (the room builder, Create). Call before Awake runs.
        public void Configure(SilksongEnemyConfig value, string id, bool facingRight)
        {
            config = value;
            placementId = id;
            startFacingRight = facingRight;
        }

        // One call, one complete enemy. rootPosition is the world position of the enemy's root (see RootYOnFloor);
        // parent may be null (the active scene). The object is built before it is activated, so Awake sees the config.
        public static MossCreep Create(SilksongEnemyConfig config, string placementId, Vector2 rootPosition, bool facingRight, Transform parent = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var go = new GameObject("MossCreep");
            go.SetActive(false);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(rootPosition.x, rootPosition.y, 0f);
            var creep = go.AddComponent<MossCreep>();
            creep.Configure(config, placementId, facingRight);
            go.SetActive(true);
            return creep;
        }

        void Awake()
        {
            if (config == null)
            {
                Debug.LogWarning("[MossCreep] '" + name + "' has no config; it stays an empty object.", this);
                return;
            }
            Assemble();
        }

        void Assemble()
        {
            if (assembled) return;
            assembled = true;

            Vector2 size = config.colliderSize;
            Vector2 offset = config.colliderOffset;

            // Visible stand-in, fitted to the config's collider (the placeholder's own collider is not used).
            var view = EnemyPlaceholderView.Build(transform);
            var viewCollider = view.GetComponent<BoxCollider2D>();
            if (viewCollider != null) DestroyImmediate(viewCollider);
            var bodyView = view.transform.Find("Body");
            if (bodyView != null)
            {
                bodyView.localPosition = new Vector3(offset.x, offset.y, 0f);
                bodyView.localScale = new Vector3(size.x, size.y, 1f);
                bodyView.GetComponent<SpriteRenderer>().sortingOrder = 3;
            }
            var eye = view.transform.Find("Eye");
            if (eye != null)
            {
                eye.localPosition = new Vector3(offset.x - size.x * 0.35f, offset.y + size.y * 0.15f, 0f);
                eye.GetComponent<SpriteRenderer>().sortingOrder = 4;
            }

            // Colliders first: the walker and the contact component look for a Collider2D on this object (the first
            // one is the solid one).
            solid = gameObject.AddComponent<BoxCollider2D>();
            solid.size = size;
            solid.offset = offset;
            contactTrigger = gameObject.AddComponent<BoxCollider2D>();
            contactTrigger.size = size;
            contactTrigger.offset = offset;
            contactTrigger.isTrigger = true;

            body = gameObject.GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();

            health = gameObject.AddComponent<EnemyHealth>();
            health.Configure(config);
            contact = gameObject.AddComponent<EnemyContactDamage>();
            contact.Configure(config);
            flash = gameObject.AddComponent<EnemyHitFlash>();

            walker = gameObject.AddComponent<EnemyWalker>();
            walker.Configure(WalkParamsFrom(config), GroundMask, startFacingRight ? 1 : -1);

            persistence = gameObject.AddComponent<EnemyPersistence>();
            persistence.Configure(PersistenceIdFor(config, placementId, gameObject.name), config.staysDead);

            shoveHandler = walker.Shove;
            diedHandler = walker.Stop;
            health.Shoved += shoveHandler;
            health.Died += diedHandler;

            Stubs.NotImplemented("Moss Creep art, animations and sounds (placeholder box)");
            TryIgnoreHero();
        }

        void OnDestroy()
        {
            if (health != null)
            {
                if (shoveHandler != null) health.Shoved -= shoveHandler;
                if (diedHandler != null) health.Died -= diedHandler;
            }
        }

        void FixedUpdate()
        {
            if (!assembled || ignoredHero != null) return;
            // A hero made after this enemy (or replaced) is found within a few steps.
            if (--scanCountdown > 0) return;
            scanCountdown = 10;
            TryIgnoreHero();
        }

        // The solid collider and the hero's body never collide, so the hero walks through the enemy; the trigger
        // still reports the overlap for contact damage.
        void TryIgnoreHero()
        {
            if (solid == null) return;
            var hero = FindFirstObjectByType<HeroHealth>();
            if (hero == null) return;
            var heroBody = hero.GetComponent<Collider2D>();
            if (heroBody == null) return;
            Physics2D.IgnoreCollision(heroBody, solid, true);
            ignoredHero = hero;
        }

        void OnDrawGizmos()
        {
            // Shows where the enemy will stand in the Scene view of a generated room (the pieces exist only at run time).
            if (assembled || config == null) return;
            Gizmos.color = new Color(0.35f, 0.65f, 0.3f, 1f);
            Vector2 c = (Vector2)transform.position + config.colliderOffset;
            Gizmos.DrawWireCube(c, config.colliderSize);
        }
    }
}
