using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowknight.Silksong
{
    // Swaps room scenes additively: the persistent bootstrap (hero, camera, manager, fader) stays, one room scene is
    // live at a time. Mirrors the original GameManager: load the target without activating it, wait for the screen to
    // be dark (the manager does that between the two calls), activate, then unload the old room.
    //
    // "Hidden" means:
    //  - Engine loads: the load is held at 90% with allowSceneActivation = false, so the scene is not integrated yet.
    //    Nothing in it has run Awake / OnEnable, so its gates are not registered and nothing is visible. The
    //    activation step lets it integrate and waits until that is done.
    //  - Scenes that are already live when the source returns (code-built test rooms, or an engine that ignores
    //    allowSceneActivation): their root objects are switched off and switched back on at activation.
    // Either way a room's gates register in OnEnable at the moment the room goes live, and ActivateAndUnloadPrevious
    // only finishes after that, so GateRegistry.Find(name, newScene) works as soon as the manager resumes.
    //
    // TESTING: the unit tests only exercise the "already live" path (code-built scenes). The engine-deferred path
    // (allowSceneActivation) is exercised by the grey-box rooms of Task 7 through EditorPathSceneSource.
    //
    // Abandoning a deferred room: Unity cannot cancel a load held at 90%, and a held load blocks every later scene
    // load. So an abandoned deferred room is allowed to finish and is then hidden and unloaded (FinishAbandon). Its
    // Awake / OnEnable run once in that moment; its gates are registered briefly and removed again when it unloads.
    //
    // Only the room this loader activated last (CurrentRoom) is ever unloaded, and never a protected scene (the
    // bootstrap's own scene).
    public class AdditiveRoomLoader : IRoomLoader, IRoomLoaderCleanup
    {
        const float ReadyProgress = 0.9f;   // engine progress value at which an op with allowSceneActivation=false stalls

        readonly IRoomSceneSource source;
        readonly RoomTransitionSettings settings;
        readonly HashSet<int> protectedHandles = new HashSet<int>();

        // Pending (hidden) room.
        string pendingName;
        AsyncOperation pendingOp;
        bool pendingDeferred;                       // true: waiting for allowSceneActivation
        List<GameObject> hiddenRoots;               // live scenes only
        Scene pendingScene;                         // live scenes only

        // Loads that were released but are still integrating, to be hidden and unloaded.
        readonly List<KeyValuePair<AsyncOperation, string>> orphans = new List<KeyValuePair<AsyncOperation, string>>();
        // The previous room, between "new room is current" and "old room is unloaded". Kept so a coroutine that is
        // stopped in between does not leak the old scene: the next clean-up unloads it.
        Scene retiring;

        public AdditiveRoomLoader(IRoomSceneSource sceneSource = null, RoomTransitionSettings sharedSettings = null)
        {
            source = sceneSource ?? new BuildSettingsSceneSource();
            settings = sharedSettings ?? new RoomTransitionSettings();
        }

        // The room that was made live last; the next activation unloads it. Set it for a room that was already
        // loaded before the loader started (a scene opened in the editor).
        public Scene CurrentRoom { get; set; }

        public string CurrentRoomName => CurrentRoom.IsValid() && CurrentRoom.isLoaded ? CurrentRoom.name : null;

        public bool HasPending => pendingName != null;

        // A scene the loader must never unload (the one holding the bootstrap).
        public void Protect(Scene scene)
        {
            if (scene.IsValid()) protectedHandles.Add(scene.handle);
        }

        // Whether this scene was protected (the bootstrap's own scene); GameSession.Quit never unloads those.
        public bool IsProtected(Scene scene) => scene.IsValid() && protectedHandles.Contains(scene.handle);

        bool TimedOut(float start) => Time.unscaledTime - start >= settings.loadTimeoutSeconds;

        public IEnumerator LoadHidden(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) throw new ArgumentException("room scene name is empty");
            if (sceneName == CurrentRoomName)
                throw new InvalidOperationException("room scene '" + sceneName + "' is the room that is already live");

            // Leftovers of an earlier change go first: a hidden room that was never activated, loads still
            // integrating, the old room of an interrupted swap. A deferred load would block every later load.
            yield return FinishAbandon();

            if (!source.Exists(sceneName))
                throw new InvalidOperationException("room scene '" + sceneName + "' cannot be loaded (not in Build Settings / not found)");

            var op = source.BeginLoad(sceneName);
            pendingName = sceneName;
            pendingOp = op;
            hiddenRoots = null;
            pendingDeferred = false;

            if (op != null)
            {
                op.allowSceneActivation = false;
                float start = Time.unscaledTime;
                // Done already means the engine ignored the hold: treat it as live and hide it by hand below.
                while (!op.isDone && op.progress < ReadyProgress)
                {
                    if (TimedOut(start))
                    {
                        AbandonPending();
                        throw new TimeoutException("room scene '" + sceneName + "' did not finish loading in " + settings.loadTimeoutSeconds + " s");
                    }
                    yield return null;
                }
                pendingDeferred = !op.isDone;
                if (pendingDeferred) yield break;
                yield return null;   // let the engine finish integrating the scene before it is looked up
            }

            pendingScene = SceneManager.GetSceneByName(sceneName);
            if (!pendingScene.IsValid() || !pendingScene.isLoaded)
            {
                ClearPending();
                throw new InvalidOperationException("room scene '" + sceneName + "' did not load");
            }
            HideRoots(pendingScene);
        }

        public IEnumerator ActivateAndUnloadPrevious()
        {
            if (!HasPending) throw new InvalidOperationException("ActivateAndUnloadPrevious called without LoadHidden");

            Scene scene;
            if (pendingDeferred)
            {
                var op = pendingOp;
                op.allowSceneActivation = true;
                float start = Time.unscaledTime;
                while (!op.isDone)   // Awake / OnEnable of the room (gate registration) run here
                {
                    if (TimedOut(start))
                    {
                        // Already released, so it finishes by itself; FinishAbandon removes it.
                        orphans.Add(new KeyValuePair<AsyncOperation, string>(op, pendingName));
                        string slow = pendingName;
                        ClearPending();
                        throw new TimeoutException("room scene '" + slow + "' did not activate in " + settings.loadTimeoutSeconds + " s");
                    }
                    yield return null;
                }
                scene = SceneManager.GetSceneByName(pendingName);
            }
            else
            {
                scene = pendingScene;
                ShowRoots();   // OnEnable: gates register now
            }

            string activated = pendingName;
            ClearPending();
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("room scene '" + activated + "' is not loaded after activation");

            SceneManager.SetActiveScene(scene);

            var previous = CurrentRoom;
            CurrentRoom = scene;
            if (previous.IsValid() && previous.isLoaded && previous != scene && !protectedHandles.Contains(previous.handle))
            {
                // Switched off first: for the frames the unload takes, the old room's colliders and gates must not
                // overlap the new room's (rooms share world coordinates).
                foreach (var root in previous.GetRootGameObjects()) root.SetActive(false);
                retiring = previous;
                yield return UnloadRetiring();
            }
        }

        IEnumerator UnloadRetiring()
        {
            var old = retiring;
            if (old.IsValid() && old.isLoaded && old != CurrentRoom)
            {
                var unload = SceneManager.UnloadSceneAsync(old);
                if (unload != null) yield return unload;
            }
            retiring = default;
        }

        public void AbandonPending()
        {
            if (!HasPending) return;
            try
            {
                if (pendingDeferred && pendingOp != null)
                {
                    // Cannot be cancelled; must be released or later loads queue behind it.
                    pendingOp.allowSceneActivation = true;
                    orphans.Add(new KeyValuePair<AsyncOperation, string>(pendingOp, pendingName));
                }
                else if (pendingScene.IsValid() && pendingScene.isLoaded && pendingScene != CurrentRoom)
                {
                    SceneManager.UnloadSceneAsync(pendingScene);
                }
                else if (pendingOp != null && !pendingOp.isDone)
                {
                    pendingOp.allowSceneActivation = true;
                    orphans.Add(new KeyValuePair<AsyncOperation, string>(pendingOp, pendingName));
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            ClearPending();
        }

        public IEnumerator FinishAbandon()
        {
            AbandonPending();

            foreach (var orphan in orphans.ToArray())
            {
                float start = Time.unscaledTime;
                while (!orphan.Key.isDone && !TimedOut(start)) yield return null;
                orphans.Remove(orphan);
                var scene = SceneManager.GetSceneByName(orphan.Value);
                if (!orphan.Key.isDone || !scene.IsValid() || !scene.isLoaded || scene == CurrentRoom) continue;
                foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null) yield return unload;
            }

            if (retiring.IsValid()) yield return UnloadRetiring();
        }

        void ClearPending()
        {
            pendingName = null;
            pendingOp = null;
            pendingDeferred = false;
            hiddenRoots = null;
            pendingScene = default;
        }

        void HideRoots(Scene scene)
        {
            hiddenRoots = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.activeSelf) continue;
                root.SetActive(false);
                hiddenRoots.Add(root);
            }
        }

        void ShowRoots()
        {
            if (hiddenRoots == null) return;
            foreach (var root in hiddenRoots) if (root != null) root.SetActive(true);
            hiddenRoots = null;
        }
    }
}
