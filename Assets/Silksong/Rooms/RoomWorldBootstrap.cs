using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowknight.Silksong
{
    // The persistent part of the game: hero, GameCamera, RoomTransitionManager, ScreenFader and the room loader.
    // They live outside any room scene (DontDestroyOnLoad), so a room change only swaps the room around them and the
    // hero is the same object before and after. Rooms hold gates, RoomInfo and RoomBounds and nothing else that
    // persists.
    //
    // Hero values: the hero gets ALL of them from the one HeroConfig.asset reference given here; the bootstrap holds
    // no hero number. Injection goes through HeroWiring, the same code HeroSceneBootstrap uses.
    // Timings: ONE RoomTransitionSettings object is shared by the manager and the ScreenFader.
    //
    // One world only: a second bootstrap (a room scene that carries a copy of this object being loaded again) removes
    // itself and what it brought, and the first stays in charge.
    [DefaultExecutionOrder(-200)]
    public class RoomWorldBootstrap : MonoBehaviour
    {
        // Scene setup. Optional when built with Create (tests, generated worlds).
        [SerializeField] HeroController hero;
        [SerializeField] HeroActions actions;
        [SerializeField] HeroConfig config;
        [SerializeField] UnitySpriteAnimator animator;
        [SerializeField] GameCamera gameCamera;
        [SerializeField] RoomTransitionSettings settings = new RoomTransitionSettings();
        // Room to load when the world starts. Empty: start in no room (the caller uses StartInRoom / AdoptRoom).
        [SerializeField] string startRoom;
        [SerializeField] string startGate;
        // True when the scene holding this bootstrap IS the first room (a room scene opened in the editor). Then that
        // scene is adopted as the current room and may be unloaded later; otherwise it is protected.
        [SerializeField] bool homeSceneIsRoom;

        public static RoomWorldBootstrap Instance { get; private set; }

        public HeroController Hero => hero;
        public GameCamera RoomCamera => gameCamera;
        public RoomTransitionManager Manager { get; private set; }
        public ScreenFader Fader { get; private set; }
        public AdditiveRoomLoader Loader { get; private set; }
        public RoomTransitionSettings Settings => settings;
        // Written to the selected slot after a room change (see Autosave); nothing is written without a slot.
        // The hero's HeroHealth uses this same object (task 8), so hazard and bench respawn data is one record.
        public PlayerData PlayerData
        {
            get => playerData;
            set
            {
                playerData = value ?? PlayerData.CreateNew();
                if (hero != null && hero.Health != null) hero.Health.Data = playerData;
            }
        }
        PlayerData playerData = PlayerData.CreateNew();

        // Code-injected pieces (not serialized), set by Create before Awake.
        IHeroInput injectedInput;
        IRoomSceneSource sceneSource;
        bool duplicate;
        DeathRespawnController deathRespawn;

        // The bench: where the hero comes back after dying (PlayerData respawnScene + respawnMarker). The grey-box
        // rooms set it from the marker flagged setsBenchRespawn when the world starts in that room.
        public void SetBenchRespawn(RespawnMarker marker)
        {
            if (marker == null) return;
            PlayerData.respawnScene = marker.gameObject.scene.name;
            PlayerData.respawnMarker = marker.MarkerName;
        }

        // Builds a whole world in code. Returns the running one if a world already exists (nothing is duplicated).
        public static RoomWorldBootstrap Create(HeroConfig heroConfig, IHeroInput input, RoomTransitionSettings transitionSettings = null,
            IRoomSceneSource source = null, PlayerData data = null)
        {
            if (Instance != null) return Instance;

            // Inactive while fields are set, so Awake sees them.
            var go = new GameObject("RoomWorld");
            go.SetActive(false);
            var bootstrap = go.AddComponent<RoomWorldBootstrap>();
            bootstrap.config = heroConfig;
            bootstrap.injectedInput = input;
            bootstrap.sceneSource = source;
            if (transitionSettings != null) bootstrap.settings = transitionSettings;
            if (data != null) bootstrap.PlayerData = data;
            go.SetActive(true);
            return bootstrap;
        }

        // With domain reload disabled the static would survive between play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                RemoveDuplicate();
                return;
            }
            Instance = this;

            var home = gameObject.scene;
            if (settings == null) settings = new RoomTransitionSettings();

            BuildHero();
            BuildCamera();
            if (hero.Health != null) hero.Health.Data = playerData;

            Fader = GetComponent<ScreenFader>();
            if (Fader == null) Fader = gameObject.AddComponent<ScreenFader>();
            Manager = GetComponent<RoomTransitionManager>();
            if (Manager == null) Manager = gameObject.AddComponent<RoomTransitionManager>();

            Loader = new AdditiveRoomLoader(sceneSource, settings);
            if (homeSceneIsRoom) Loader.CurrentRoom = home;
            else Loader.Protect(home);   // never unload the scene that carries the bootstrap

            // Configure shares the one settings object with the fader.
            Manager.Configure(Loader, Fader, hero.Transition, settings);
            Manager.TransitionStarted += OnTransitionStarted;
            Manager.RoomActivated += OnRoomActivated;
            Manager.HeroPlaced += OnHeroPlaced;
            Manager.TransitionFinished += OnTransitionFinished;

            // Death: the hero's death delay calls the controller, which respawns in place or through the manager.
            Manager.RespawnPlacer = (scene, markerName) => DeathRespawnController.PlaceAtMarker(hero.Health, scene, markerName);
            deathRespawn = new DeathRespawnController(hero.Health, Manager, () => Loader.CurrentRoom);

            // Out of the room scene so unloading rooms never takes the world with it.
            MakePersistent(gameObject);
            MakePersistent(hero.gameObject);
            MakePersistent(gameCamera.gameObject);
        }

        static void MakePersistent(GameObject go)
        {
            var root = go.transform.root.gameObject;
            // Not in play mode (edit-mode tests building the object): DontDestroyOnLoad would log an error.
            if (Application.isPlaying) DontDestroyOnLoad(root);
        }

        void BuildHero()
        {
            if (hero == null)
            {
                // A bare hero for worlds built from code: collider and body only, no art.
                var go = new GameObject("Hero");
                var box = go.AddComponent<BoxCollider2D>();
                box.size = SilksongUnits.HeroColliderSize;
                box.offset = SilksongUnits.HeroColliderOffset;
                hero = go.AddComponent<HeroController>();
                Stubs.NotImplemented("RoomWorldBootstrap: hero art when the hero is built from code (collider only)");
            }

            IHeroInput input = injectedInput ?? actions;
            if (config == null || input == null)
                Debug.LogError("RoomWorldBootstrap: hero config or input is missing; the hero will not move.", this);
            else
                HeroWiring.Inject(hero, config, input, animator);
        }

        void BuildCamera()
        {
            if (gameCamera == null)
            {
                var go = new GameObject("GameCamera");
                go.tag = "MainCamera";
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = SilksongUnits.CameraOrthographicSize;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                go.transform.position = new Vector3(0f, 0f, -10f);
                gameCamera = go.AddComponent<GameCamera>();
            }
            gameCamera.Target = hero.transform;
        }

        // The second bootstrap and the hero / camera it carried must not stay: that would be two heroes.
        void RemoveDuplicate()
        {
            duplicate = true;
            if (hero != null && hero.gameObject != gameObject) Destroy(hero.transform.root.gameObject);
            if (gameCamera != null && gameCamera.gameObject != gameObject) Destroy(gameCamera.transform.root.gameObject);
            Destroy(gameObject);
        }

        IEnumerator Start()
        {
            if (duplicate || string.IsNullOrEmpty(startRoom)) yield break;
            yield return StartInRoom(startRoom, startGate);
        }

        // Loads the first room (nothing is unloaded), makes it current and puts the hero at a gate of it.
        // An empty gate name uses the room's first gate. Errors are logged; the world stays as it was.
        public IEnumerator StartInRoom(string sceneName, string gateName = null)
        {
            Exception err = null;
            yield return Run(Loader.LoadHidden(sceneName), e => err = e);
            if (err == null) yield return Run(Loader.ActivateAndUnloadPrevious(), e => err = e);
            if (err != null)
            {
                Debug.LogException(err);
                Debug.LogError("[World] Could not start in room '" + sceneName + "'.");
                yield break;
            }

            var scene = Loader.CurrentRoom;
            var gate = string.IsNullOrEmpty(gateName) ? GateRegistry.FirstGate(scene) : GateRegistry.Find(gateName, scene);
            if (gate != null) PlaceHero((Vector2)gate.transform.position + gate.EntryOffset);
            EnterRoomView(scene);
            RecordRoom(scene);
        }

        // A room that is already loaded (a scene opened in the editor): becomes the current room, nothing is loaded.
        public void AdoptRoom(Scene scene)
        {
            Loader.CurrentRoom = scene;
            SceneManager.SetActiveScene(scene);
            EnterRoomView(scene);
            RecordRoom(scene);
        }

        // Puts the hero at a world position with no walk-in (start of the game, tests).
        public void PlaceHero(Vector2 position)
        {
            var t = hero.transform;
            t.position = new Vector3(position.x, position.y, t.position.z);
            var body = hero.GetComponent<Rigidbody2D>();
            body.position = position;
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        // Puts the hero at a spot of the current room facing a way, and jumps the camera to it (GameSession uses it to
        // stand the hero on a respawn marker after the room was loaded).
        public void PlaceHeroAt(Vector2 position, bool facingRight)
        {
            PlaceHero(position);
            hero.FacingRight = facingRight;
            if (Loader != null && Loader.CurrentRoom.IsValid()) EnterRoomView(Loader.CurrentRoom);
        }

        // Bounds of the room and an instant snap to the hero; used where no transition ran.
        void EnterRoomView(Scene scene)
        {
            gameCamera.EnterRoom(FindRoomInfo(scene));
            gameCamera.SnapTo(hero.transform.position, hero.FacingRight);
        }

        // Steps an enumerator by hand so an exception becomes a value, as the manager does.
        static IEnumerator Run(IEnumerator inner, Action<Exception> onError)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!inner.MoveNext()) yield break;
                    current = inner.Current;
                }
                catch (Exception ex)
                {
                    onError(ex);
                    yield break;
                }
                yield return current;
            }
        }

        // Leaving: the camera stays where it is (the original's FreezeInPlace) until the hero is in the new room.
        void OnTransitionStarted(TransitionRequest request)
        {
            if (gameCamera != null) gameCamera.Freeze();
        }

        // The new room is live: its bounds apply now, still frozen, so the snap below lands inside them.
        void OnRoomActivated(Scene scene, TransitionPoint arrival)
        {
            if (gameCamera != null) gameCamera.EnterRoom(FindRoomInfo(scene));
        }

        // The hero stands at the arrival gate: jump the camera to it (also unfreezes and starts the 0.1 s hold).
        void OnHeroPlaced(Scene scene, TransitionPoint arrival)
        {
            if (gameCamera != null && hero != null) gameCamera.SnapTo(hero.transform.position, hero.FacingRight);
            RecordHazardSpot(scene, arrival);
        }

        // Arrival sets the hazard respawn spot (the original's FinishedEnteringScene). arrival is null after a death
        // respawn, which has no gate: the bench spot where the hero now stands is used.
        void RecordHazardSpot(Scene scene, TransitionPoint arrival)
        {
            if (hero == null) return;
            Vector2 pos = hero.transform.position;
            if (arrival != null)
                RespawnRules.ApplyGateArrival(PlayerData, scene.name, arrival.NonHazardGate, arrival.HazardMarker, pos, hero.FacingRight);
            else
                PlayerData.SetHazardRespawn(scene.name, "Respawn", pos, hero.FacingRight);
        }

        void OnTransitionFinished(bool success)
        {
            // A failed change must not leave the camera frozen.
            if (gameCamera != null) gameCamera.Unfreeze();
            if (Loader == null || !Loader.CurrentRoom.IsValid()) return;

            var live = Loader.CurrentRoom;
            if (success)
            {
                RecordRoom(live);
                Autosave();
                return;
            }
            // Failed after the swap (new room without gates): the old room is gone, so the camera must follow the room
            // that is actually live. Before the swap the live room is still the old one and its bounds already apply.
            var info = FindRoomInfo(live);
            if (gameCamera != null && info != null && info.Bounds != gameCamera.Bounds)
            {
                EnterRoomView(live);
                RecordRoom(live);
            }
        }

        // Times an autosave after a room change wrote a file (tests count it; stays 0 with no selected slot).
        public int AutosaveCount { get; private set; }

        // Save after a completed room change, never while dead. SaveSlots.Autosave does nothing without a selected
        // slot. The original saves at death, bench and quit-to-menu; saving on a room change is this port's addition.
        void Autosave()
        {
            if (hero != null && hero.Health != null && hero.Health.IsDead) return;
            if (SaveSlots.Autosave(PlayerData)) AutosaveCount++;
        }

        // Remembers which room the player is in, in PlayerData (Autosave writes it to disk).
        // respawnScene is NOT touched: as in the original it only changes at benches / respawn markers (Task 8).
        // currentScene is the plain "where am I" record; mapZone gets the stable key, never the display name.
        void RecordRoom(Scene scene)
        {
            var info = FindRoomInfo(scene);
            if (info == null)
            {
                Debug.LogWarning("[World] Room '" + scene.name + "' has no RoomInfo; player data keeps its old room.");
                return;
            }
            PlayerData.currentScene = info.SceneName;
            if (!string.IsNullOrEmpty(info.MapZoneKey)) PlayerData.mapZone = info.MapZoneKey;
        }

        public static RoomInfo FindRoomInfo(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var info = root.GetComponentInChildren<RoomInfo>(true);
                if (info != null) return info;
            }
            return null;
        }

        void OnDestroy()
        {
            if (duplicate) return;
            if (deathRespawn != null) deathRespawn.Dispose();
            if (Manager != null)
            {
                Manager.RespawnPlacer = null;
                Manager.TransitionStarted -= OnTransitionStarted;
                Manager.RoomActivated -= OnRoomActivated;
                Manager.HeroPlaced -= OnHeroPlaced;
                Manager.TransitionFinished -= OnTransitionFinished;
            }
            // The manager clears TransitionPoint.Handler itself when it is destroyed with this object.
            if (Instance == this) Instance = null;
            // The persistent objects were moved out of the scene, so they would outlive this object.
            if (hero != null && hero.gameObject != gameObject) Destroy(hero.transform.root.gameObject);
            if (gameCamera != null && gameCamera.gameObject != gameObject) Destroy(gameCamera.transform.root.gameObject);
        }
    }
}
