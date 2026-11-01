using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowknight.Silksong
{
    // Wires the hero in the Silksong room scene. HeroController.Config / Input / Animator are code-injected
    // properties (not serialized, so tests need no assets), so a scene needs one object to inject them. This is it.
    //
    // The ONLY hero numbers that reach the hero come from the single project asset Assets/Silksong/Hero/HeroConfig.asset,
    // which the scene builder stores here as a reference (never a copy). This component holds no hero values of its own.
    // Runs before the hero's own scripts.
    [DefaultExecutionOrder(-200)]
    public class HeroSceneBootstrap : MonoBehaviour
    {
        [SerializeField] HeroController hero;
        [SerializeField] HeroActions actions;
        [SerializeField] HeroConfig config;
        [SerializeField] UnitySpriteAnimator animator;
        [SerializeField] SpriteRenderer visual;
        [SerializeField] RespawnMarker spawn;
        // Art direction, not a hero value: which way the source sprites look. Hero code only knows FacingRight.
        // If the hero looks backwards in the scene, flip this tick box on the bootstrap object.
        [SerializeField] bool artFacesLeft = true;

        public HeroController Hero => hero;
        public HeroConfig Config => config;
        public RespawnMarker Spawn => spawn;

        void Awake()
        {
            Stubs.NotImplemented("Hero room: real Silksong room art and geometry (the room is plain blocks)");
            if (hero == null || config == null || actions == null)
            {
                Debug.LogError("HeroSceneBootstrap: hero, actions or config is not assigned; the hero will not move. Rebuild with Silksong > Build Hero Scene.", this);
                return;
            }
            hero.Config = config;
            hero.Input = actions;
            if (animator != null) hero.Animator = animator;
        }

        void Start()
        {
            // The scene runs physics at 50 Hz but renders (and moves the camera in LateUpdate) every frame, so a body
            // that only changes position on physics steps is drawn in uneven jumps against the smoothly moving
            // camera: the hero looks jittery when walking. Interpolation draws it between its last two physics
            // positions. It is set here, in Start, because HeroController.Awake sets None (kept so tests that step
            // physics by hand stay exact) and this component's Awake runs before it. The camera reads the interpolated
            // transform.position in LateUpdate, which is what we want.
            if (hero != null)
            {
                var body = hero.GetComponent<Rigidbody2D>();
                if (body != null) body.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
            if (hero == null || spawn == null || hero.Health == null) return;
            // Puts the hero on the marker, facing the marker's way, and makes it the marker a death returns to.
            hero.Health.RespawnAt(spawn);
        }

        void Update()
        {
            // R: back to the respawn marker (test convenience).
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame && hero != null && spawn != null && hero.Health != null)
                hero.Health.RespawnAt(spawn);

            // H and K are also test-room only. H: a hit for 1 from the front (the hero is pushed backwards).
            // K: a hit for the hero's whole current health, so it dies. Both go through the normal TakeHit, so they
            // are ignored during invulnerability, like any hit. The source is null: the knockback then points away
            // from the way the hero faces.
            if (kb != null && hero != null && hero.Health != null)
            {
                if (kb.hKey.wasPressedThisFrame)
                    hero.Health.TakeHit(new HitInstance { Damage = 1, Direction = AttackDirection.Normal });
                if (kb.kKey.wasPressedThisFrame)
                    hero.Health.TakeHit(new HitInstance { Damage = Mathf.Max(1, hero.Health.Health), Direction = AttackDirection.Normal });
            }
        }

        void LateUpdate()
        {
            if (hero == null || visual == null) return;
            visual.flipX = artFacesLeft ? hero.FacingRight : !hero.FacingRight;
        }
    }
}
