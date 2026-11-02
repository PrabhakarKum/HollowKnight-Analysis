using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Hollowknight.Silksong
{
    // Lets a room scene start the game by itself: press Play with Tut_01 (or Tut_01b, Tut_02) open and this creates
    // the persistent world (hero, camera, manager, fader, loader) through RoomWorldBootstrap, takes the open scene
    // as the current room and puts the hero on the room's start point. It holds only references (the HeroConfig
    // asset, the input actions asset, the start point, optional hero art), never a hero number.
    //
    // When the room is loaded by a running world (a room change) a world already exists and this does nothing, so
    // every room can carry one. It is NOT the world: the hero, camera and manager are never part of a room scene.
    //
    // Where other rooms come from: rooms listed in Build Settings load by name; in the Editor, rooms that are not
    // listed are loaded by asset path from Assets/Scenes/Rooms (see FallbackRoomSceneSource).
    [DefaultExecutionOrder(-300)]
    public class RoomWorldStarter : MonoBehaviour
    {
        public const string RoomsFolder = "Assets/Scenes/Rooms";

        [SerializeField] HeroConfig config;
        [SerializeField] InputActionAsset actionsAsset;
        // Where the hero stands at the start (a RespawnMarker in this room).
        [SerializeField] RespawnMarker startPoint;
        // Optional hero art: the imported clips (registered by name) and the sprite shown before one plays. With no
        // clips the hero is drawn as a plain box the size of its body.
        [SerializeField] AnimationClip[] heroClips;
        [SerializeField] Sprite heroSprite;
        // Art direction, as on HeroSceneBootstrap: which way the source sprites look.
        [SerializeField] bool artFacesLeft = true;

        public RespawnMarker StartPoint => startPoint;

        // For rooms built from code (the generated grey-box rooms).
        public void Configure(HeroConfig heroConfig, InputActionAsset inputActions, RespawnMarker start, Sprite sprite, AnimationClip[] clips)
        {
            config = heroConfig;
            actionsAsset = inputActions;
            startPoint = start;
            heroSprite = sprite;
            heroClips = clips;
        }

        // The asset path of a room scene made by the grey-box builder.
        public static string ScenePath(string sceneName) => RoomsFolder + "/" + sceneName + ".unity";

        IEnumerator Start()
        {
            if (RoomWorldBootstrap.Instance != null) yield break;   // this room was loaded by a running world

            var room = gameObject.scene;
            while (!room.isLoaded) yield return null;
            if (RoomWorldBootstrap.Instance != null) yield break;

            if (config == null || actionsAsset == null)
            {
                Debug.LogError("RoomWorldStarter: hero config or input actions are not assigned. Rebuild with Silksong > Build Transition Rooms.", this);
                yield break;
            }

            Stubs.NotImplemented("RoomWorldStarter: start menu and save slots (a room scene starts the world directly)");
            var world = CreateWorld();
            AddHeroArt(world.Hero);

            Vector2 spot;
            if (startPoint != null) spot = startPoint.transform.position;
            else
            {
                var gate = GateRegistry.FirstGate(room);
                spot = gate != null ? (Vector2)gate.transform.position : Vector2.zero;
                Debug.LogWarning("[World] Room '" + room.name + "' has no start point; the hero starts at its first gate.", this);
            }
            world.PlaceHero(spot);
            world.AdoptRoom(room);
            if (world.RoomCamera.GetComponent<AudioListener>() == null) world.RoomCamera.gameObject.AddComponent<AudioListener>();
            Debug.Log("[World] Started in room '" + room.name + "'.");
        }

        RoomWorldBootstrap CreateWorld()
        {
            // The bootstrap protects the scene it is created in (that scene is never unloaded). That must not be the
            // room the hero starts in, so it is created while a throw-away host scene is active; the bootstrap then
            // moves itself out of that scene and the empty host stays unused.
            var previous = SceneManager.GetActiveScene();
            var host = SceneManager.CreateScene("RoomWorldHost");
            SceneManager.SetActiveScene(host);

            var inputGo = new GameObject("RoomWorldInput");
            var actions = inputGo.AddComponent<HeroActions>();
            actions.Initialize(actionsAsset);

            var world = RoomWorldBootstrap.Create(config, actions, null, MakeSource());
            // The input lives and dies with the world.
            inputGo.transform.SetParent(world.transform, false);

            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            return world;
        }

        static IRoomSceneSource MakeSource()
        {
#if UNITY_EDITOR
            Debug.Log("[World] Editor: rooms in Build Settings load by name; the others load from " + RoomsFolder + " by path.");
            return new FallbackRoomSceneSource(new BuildSettingsSceneSource(), new EditorPathSceneSource(ScenePath));
#else
            return new BuildSettingsSceneSource();
#endif
        }

        void AddHeroArt(HeroController hero)
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

    // Asks the first source, and the second when the first does not have the scene. Lets the Editor use Build
    // Settings when a room is listed there and the asset path when it is not.
    public sealed class FallbackRoomSceneSource : IRoomSceneSource
    {
        readonly IRoomSceneSource primary;
        readonly IRoomSceneSource secondary;

        public FallbackRoomSceneSource(IRoomSceneSource first, IRoomSceneSource second)
        {
            primary = first;
            secondary = second;
        }

        public bool Exists(string sceneName) => primary.Exists(sceneName) || secondary.Exists(sceneName);

        public AsyncOperation BeginLoad(string sceneName) =>
            primary.Exists(sceneName) ? primary.BeginLoad(sceneName) : secondary.BeginLoad(sceneName);
    }
}
