using UnityEngine;

namespace Hollowknight.Silksong
{
    // Smooths the hero in the transition rooms. Physics runs at 50 Hz while the camera moves in LateUpdate every frame,
    // so a body that only changes position on physics steps is drawn in uneven jumps against the camera (visible
    // jitter when walking or jumping). Interpolation draws it between its last two physics positions.
    // Same idea as HeroSceneBootstrap, which does it for the single hero room. Added by RoomWorldStarter, so tests
    // that step physics by hand are not affected. HeroController.Awake sets None; this component starts later.
    public class HeroInterpolation : MonoBehaviour
    {
        HeroController hero;
        Rigidbody2D body;
        RoomTransitionManager manager;

        void Start()
        {
            hero = GetComponent<HeroController>();
            body = GetComponent<Rigidbody2D>();
            if (body == null) return;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            // A teleport (room arrival, death respawn) must not be drawn as a slide from the old spot.
            if (hero != null && hero.Health != null) hero.Health.Respawned += Restart;
            var world = RoomWorldBootstrap.Instance;
            if (world != null && world.Manager != null)
            {
                manager = world.Manager;
                manager.HeroPlaced += OnHeroPlaced;
            }
        }

        void OnHeroPlaced(UnityEngine.SceneManagement.Scene scene, TransitionPoint arrival) => Restart();

        void Restart()
        {
            if (body == null) return;
            body.interpolation = RigidbodyInterpolation2D.None;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        void OnDestroy()
        {
            if (hero != null && hero.Health != null) hero.Health.Respawned -= Restart;
            if (manager != null) manager.HeroPlaced -= OnHeroPlaced;
        }
    }
}
