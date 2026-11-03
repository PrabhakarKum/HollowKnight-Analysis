using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Hollowknight.Silksong
{
    // The one recipe for building the persistent world (hero with art, camera, manager, fader, loader, input,
    // audio listener). Shared by RoomWorldStarter (a room played directly) and the start screen (a saved game), so
    // both build exactly the same world.
    public static class RoomWorldFactory
    {
        public const string HostSceneName = "RoomWorldHost";

        // data: the PlayerData the world must use (a loaded or fresh save), or null for a fresh in-memory one.
        public static RoomWorldBootstrap Create(HeroConfig config, InputActionAsset actionsAsset, Sprite heroSprite,
            AnimationClip[] heroClips, bool artFacesLeft, PlayerData data)
        {
            // The bootstrap protects the scene it is created in (that scene is never unloaded). That must not be the
            // room the hero starts in (or the start screen), so it is created while a throw-away host scene is
            // active; the bootstrap then moves itself out of that scene and the empty host stays unused.
            var previous = SceneManager.GetActiveScene();
            // A host left over from an earlier game is reused: CreateScene throws when the name already exists.
            var host = SceneManager.GetSceneByName(HostSceneName);
            if (!host.IsValid() || !host.isLoaded) host = SceneManager.CreateScene(HostSceneName);
            SceneManager.SetActiveScene(host);

            var inputGo = new GameObject("RoomWorldInput");
            var actions = inputGo.AddComponent<HeroActions>();
            actions.Initialize(actionsAsset);

            var world = RoomWorldBootstrap.Create(config, actions, null, MakeSource(), data);
            // The input lives and dies with the world.
            inputGo.transform.SetParent(world.transform, false);

            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);

            AddHeroArt(world.Hero, heroSprite, heroClips, artFacesLeft);
            world.Hero.gameObject.AddComponent<HeroInterpolation>();
            if (world.RoomCamera.GetComponent<AudioListener>() == null) world.RoomCamera.gameObject.AddComponent<AudioListener>();
            return world;
        }

        public static IRoomSceneSource MakeSource()
        {
#if UNITY_EDITOR
            Debug.Log("[World] Editor: rooms in Build Settings load by name; the others load from " + RoomWorldStarter.RoomsFolder + " by path.");
            return new FallbackRoomSceneSource(new BuildSettingsSceneSource(), new EditorPathSceneSource(RoomWorldStarter.ScenePath));
#else
            return new BuildSettingsSceneSource();
#endif
        }

        public static void AddHeroArt(HeroController hero, Sprite heroSprite, AnimationClip[] heroClips, bool artFacesLeft)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(hero.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 5;
            sr.sprite = heroSprite;

            bool hasClips = heroClips != null && heroClips.Length > 0;
            if (hasClips)
            {
                // Sprite origin = hero root, as in HeroSceneBuilder.
                var animator = go.AddComponent<UnitySpriteAnimator>();
                foreach (var clip in heroClips) animator.Register(clip);
                hero.Animator = animator;
            }
            else
            {
                // No imported art: a box the size of the body so the hero can be seen.
                go.transform.localPosition = SilksongUnits.HeroColliderOffset;
                go.transform.localScale = new Vector3(SilksongUnits.HeroColliderSize.x, SilksongUnits.HeroColliderSize.y, 1f);
                sr.color = new Color(0.95f, 0.6f, 0.2f);
                Stubs.NotImplemented("RoomWorldStarter: hero art needs Silksong/Import Hero Animations (a plain box is drawn)");
            }
            go.AddComponent<HeroArtFlip>().Configure(hero, sr, artFacesLeft && hasClips);
        }
    }
}
