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
    // Only the room this loader activated last (CurrentRoom) is ever unloaded, and never a protected scene (the
    // bootstrap's own scene).
    public class AdditiveRoomLoader : IRoomLoader
    {
        const float ReadyProgress = 0.9f;   // engine progress value at which an op with allowSceneActivation=false stalls

        readonly IRoomSceneSource source;
        readonly HashSet<int> protectedHandles = new HashSet<int>();

        // Pending (hidden) room.
        string pendingName;
        AsyncOperation pendingOp;
        bool pendingDeferred;                       // true: waiting for allowSceneActivation
        List<GameObject> hiddenRoots;               // live scenes only
        Scene pendingScene;                         // live scenes only

        public AdditiveRoomLoader(IRoomSceneSource sceneSource = null)
        {
            source = sceneSource ?? new BuildSettingsSceneSource();
        }

        // The room that was made live last; the next activation unloads it. Set it for a room that was already
        // loaded before the loader started (a scene opened in the editor).
        public Scene CurrentRoom { get; set; }

        public bool HasPending => pendingName != null;

        // A scene the loader must never unload (the one holding the bootstrap).
        public void Protect(Scene scene)
        {
            if (scene.IsValid()) protectedHandles.Add(scene.handle);
        }

        public IEnumerator LoadHidden(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) throw new ArgumentException("room scene name is empty");

            // A hidden room that was never activated (a transition that failed in between) has to go first: a
            // deferred load blocks every later scene load, and a hidden live scene would leak.
            if (HasPending) yield return Abandon();

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
                // Done already means the engine ignored the hold: treat it as live and hide it by hand below.
                while (!op.isDone && op.progress < ReadyProgress) yield return null;
                pendingDeferred = !op.isDone;
                if (pendingDeferred) yield break;
                yield return null;   // let the engine finish integrating the scene before it is looked up
            }

            pendingScene = SceneManager.GetSceneByName(sceneName);
            if (!pendingScene.IsValid() || !pendingScene.isLoaded)
            {
                string failed = pendingName;
                ClearPending();
                throw new InvalidOperationException("room scene '" + failed + "' did not load");
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
                while (!op.isDone) yield return null;   // Awake / OnEnable of the room (gate registration) run here
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
                var unload = SceneManager.UnloadSceneAsync(previous);
                if (unload != null) yield return unload;
            }
        }

        // Drops a room that was loaded hidden but will not be used.
        IEnumerator Abandon()
        {
            var scene = pendingDeferred ? default(Scene) : pendingScene;
            if (pendingDeferred && pendingOp != null)
            {
                // Must be allowed to finish, or later loads queue behind it forever.
                pendingOp.allowSceneActivation = true;
                while (!pendingOp.isDone) yield return null;
                scene = SceneManager.GetSceneByName(pendingName);
            }
            ClearPending();
            if (scene.IsValid() && scene.isLoaded && scene != CurrentRoom)
            {
                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null) yield return unload;
            }
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
