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
            var world = RoomWorldFactory.Create(config, actionsAsset, heroSprite, heroClips, artFacesLeft, null);

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
            if (startPoint != null && startPoint.SetsBenchRespawn) world.SetBenchRespawn(startPoint);
            Debug.Log("[World] Started in room '" + room.name + "'.");
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
