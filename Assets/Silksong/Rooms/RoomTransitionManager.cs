using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowknight.Silksong
{
    // Runs a room change: PLAYING -> EXITING_LEVEL -> (load hidden) -> ENTERING_LEVEL -> PLAYING.
    // A small slice of the original GameManager.BeginSceneTransitionRoutine. All work with the outside world goes
    // through IRoomLoader, IScreenFader and ITransitionHero so it is tested with fakes.
    //
    // Failure policy (the player must never end up locked or looking at a black screen):
    //  - A failure BEFORE ActivateAndUnloadPrevious completes recovers into the OLD room: fade in, hero control
    //    back (ITransitionHeroRecovery.RestoreControl, or EnterRoom on the sending gate if the hero has no
    //    recovery hook), State Playing.
    //  - A failure AFTER the swap (new scene not loaded, no gates in it) is "fatal but safe": the old room is
    //    already gone and cannot come back, so the manager logs that, unlocks the hero, fades in and goes to
    //    Playing in whatever the new room is. A hero WITHOUT ITransitionHeroRecovery stays locked then (there is
    //    no gate to enter through); this is logged as an error.
    //  - Any exception anywhere in the sequence ends in the same "restore everything" path.
    public class RoomTransitionManager : MonoBehaviour, ITransitionRequestHandler
    {
        public RoomTransitionSettings Settings = new RoomTransitionSettings();

        IRoomLoader loader;
        IScreenFader fader;
        ITransitionHero hero;
        Coroutine routine;
        Coroutine activeFade;
        bool running;
        bool loggedMissingCollaborators;

        public GameState State { get; private set; } = GameState.Playing;
        public bool IsTransitionRunning => running;

        // Observation points for the persistent bootstrap (camera freeze, bounds swap, snap, player data). They only
        // report; they never change the sequence, and a throwing listener is logged and skipped.
        // Order for a successful change: TransitionStarted, RoomActivated, HeroPlaced, TransitionFinished(true).
        // A failure ends with TransitionFinished(false) instead.
        // The request is accepted and the hero has been told to leave (State is ExitingLevel).
        public event Action<TransitionRequest> TransitionStarted;
        // The new room is live and its arrival gate was found; the hero has not been placed yet.
        public event Action<Scene, TransitionPoint> RoomActivated;
        // The hero was put at the arrival gate (its EnterRoom has run its first step); it may still walk in.
        public event Action<Scene, TransitionPoint> HeroPlaced;
        // The sequence is over and the hero has control again (true), or it failed / was aborted (false).
        public event Action<bool> TransitionFinished;

        // Every listener is called even if an earlier one throws.
        static void Notify<T>(Action<T> e, T arg)
        {
            if (e == null) return;
            foreach (Delegate d in e.GetInvocationList())
            {
                try { ((Action<T>)d)(arg); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        static void Notify<T1, T2>(Action<T1, T2> e, T1 a, T2 b)
        {
            if (e == null) return;
            foreach (Delegate d in e.GetInvocationList())
            {
                try { ((Action<T1, T2>)d)(a, b); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        // Wires the collaborators. Called by the bootstrap (Task 6) or a test. Passing settings makes the manager
        // and a ScreenFader share ONE settings object.
        public void Configure(IRoomLoader roomLoader, IScreenFader screenFader, ITransitionHero transitionHero,
            RoomTransitionSettings settings = null)
        {
            loader = roomLoader;
            fader = screenFader;
            hero = transitionHero;
            if (settings != null) Settings = settings;
            if (fader is ScreenFader sf && sf != null) sf.Configure(Settings);
            loggedMissingCollaborators = false;

            string warning = Settings.ValidationWarning();
            if (warning != null) Debug.LogWarning("[Transition] " + warning);
        }

        void OnEnable() => TransitionPoint.Handler = this;

        // Both, because a destroyed manager must never stay referenced by the static gate handler.
        void OnDisable()
        {
            AbortIfBusy();
            ClearHandler();
        }

        void OnDestroy()
        {
            AbortIfBusy();
            ClearHandler();
        }

        void ClearHandler()
        {
            if (ReferenceEquals(TransitionPoint.Handler, this)) TransitionPoint.Handler = null;
        }

        void AbortIfBusy()
        {
            if (running || routine != null || State != GameState.Playing) Abort();
        }

        // Synchronous "put everything back": used when the manager is switched off mid-transition and when the
        // sequence itself throws. Best effort, never throws.
        void Abort()
        {
            StopAllCoroutines();
            activeFade = null;
            routine = null;
            running = false;
            State = GameState.Playing;
            try
            {
                if (fader is ScreenFader sf && sf != null) sf.SetAlpha(0f);
                if (hero is ITransitionHeroRecovery recovery) recovery.RestoreControl();
                // A room loaded hidden but never activated must not stay behind.
                if (loader is IRoomLoaderCleanup cleanup) cleanup.AbandonPending();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            Notify(TransitionFinished, false);
        }

        public void RequestTransition(TransitionRequest request)
        {
            if (loader == null || fader == null || hero == null)
            {
                // Logged once until Configure is called again: a gate in a stay-loop would flood the console.
                if (!loggedMissingCollaborators)
                {
                    loggedMissingCollaborators = true;
                    Debug.LogWarning("[Transition] Request rejected: loader, fader or hero is not configured.");
                }
                ReleaseGates(request);
                return;
            }

            // EnteringLevel is accepted: a bottom gate may be left straight after arriving (TransitionRules).
            bool stateOk = State == GameState.Playing || State == GameState.EnteringLevel;
            bool valid = !string.IsNullOrEmpty(request.TargetScene) && !string.IsNullOrEmpty(request.EntryGate);
            // Two scenes of one name cannot be told apart (scene lookup is by name), so a room cannot lead to itself.
            bool sameRoom = valid && loader is IRoomLoaderCleanup info && info.CurrentRoomName == request.TargetScene;
            if (running || !stateOk || !valid || sameRoom)
            {
                Debug.LogWarning($"[Transition] Request to '{request.TargetScene}' rejected (running={running}, state={State}, valid={valid}, targetIsCurrentRoom={sameRoom}).");
                ReleaseGates(request);
                return;
            }

            // A walk-in still in progress is abandoned: the new request takes over the hero. Its fade-in must
            // stop too, or it would fight the next fade-out. The abandoned run is reported as finished (false) so
            // TransitionStarted / TransitionFinished stay paired.
            if (routine != null)
            {
                StopCoroutine(routine);
                Notify(TransitionFinished, false);
            }
            if (activeFade != null) StopCoroutine(activeFade);
            activeFade = null;
            routine = StartCoroutine(Guard(Run(request)));
        }

        // The gate marked itself activated when it sent this; let it fire again.
        static void ReleaseGates(TransitionRequest request)
        {
            foreach (var gate in FindGates(request)) gate.ResetActivation();
        }

        // The request does not say which gate sent it, so find gates that would build this same request.
        static List<TransitionPoint> FindGates(TransitionRequest request)
        {
            var result = new List<TransitionPoint>();
            foreach (var gate in FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
            {
                var r = gate.BuildRequest();
                if (r.TargetScene == request.TargetScene && r.EntryGate == request.EntryGate && r.LeaveDirection == request.LeaveDirection)
                    result.Add(gate);
            }
            return result;
        }

        IEnumerator Run(TransitionRequest request)
        {
            running = true;
            State = GameState.ExitingLevel;
            var senders = FindGates(request);   // looked up now: the old room is gone after the swap
            Exception err = null;
            Notify(TransitionStarted, request);

            try { hero.LeaveRoom(request.LeaveDirection); }   // locks control from here on
            catch (Exception ex) { err = ex; }
            if (err != null) { yield return Fail(err, "hero.LeaveRoom threw", senders, false); yield break; }

            // Fade and the fixed wait run side by side: the new room may only go live when both are done.
            var fade = StartFade(fader.FadeOut(), ex => err = ex);
            yield return new WaitForSecondsRealtime(Settings.fadeWaitSeconds);
            yield return fade;
            if (err != null) { yield return Fail(err, "fade out failed", senders, false); yield break; }

            yield return Stage(loader.LoadHidden(request.TargetScene), ex => err = ex);
            if (err != null) { yield return Fail(err, "loading the room failed", senders, false); yield break; }

            yield return Stage(loader.ActivateAndUnloadPrevious(), ex => err = ex);
            if (err != null) { yield return Fail(err, "activating the room failed", senders, false); yield break; }

            // From here the old room is gone.
            var scene = SceneManager.GetSceneByName(request.TargetScene);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                yield return Fail(null, $"scene '{request.TargetScene}' is not loaded after activation", senders, true);
                yield break;
            }

            // Scoped to the NEW room: both rooms use names like left1 / right1.
            var arrival = GateRegistry.Find(request.EntryGate, scene);
            if (arrival == null)
            {
                yield return Fail(null, $"scene '{request.TargetScene}' has no gates", senders, true);
                yield break;
            }

            // The original's SaveLevelState, map-zone events and GC pass would run here.
            Stubs.NotImplemented("RoomTransitionManager.LevelStateAndGc");

            Notify(RoomActivated, scene, arrival);

            State = GameState.EnteringLevel;
            running = false;   // gates may fire again now (bottom-gate exception)

            yield return new WaitForSecondsRealtime(Settings.heroWaitSeconds);
            var fadeIn = StartFade(fader.FadeIn(), ex => err = ex);
            yield return Stage(PlaceNotice(hero.EnterRoom(arrival, request.EntryDelay + arrival.EntryDelay), scene, arrival), ex => err = ex);
            yield return fadeIn;
            if (err != null) { yield return Fail(err, "entering the room failed", senders, true); yield break; }

            State = GameState.Playing;
            routine = null;
            Notify(TransitionFinished, true);
        }

        // Passes the hero's EnterRoom through unchanged, but raises HeroPlaced after its first step: that is when the
        // hero stands at the arrival gate (the real hero places itself before its first wait). Raised even when
        // EnterRoom ends in that first step.
        IEnumerator PlaceNotice(IEnumerator inner, Scene scene, TransitionPoint arrival)
        {
            bool more = inner.MoveNext();
            // A hero that refused (no input / config) never moved; announcing a placement would snap the camera to
            // the wrong spot. A placed hero is always locked at this point.
            if (hero.ControlLocked) Notify(HeroPlaced, scene, arrival);
            else Debug.LogError("[Transition] The hero was not placed at the arrival gate (EnterRoom refused); HeroPlaced is skipped.");
            while (more)
            {
                yield return inner.Current;
                more = inner.MoveNext();
            }
        }

        IEnumerator Fail(Exception error, string message, List<TransitionPoint> senders, bool oldRoomGone)
        {
            if (error != null) Debug.LogException(error);
            Debug.LogError(oldRoomGone
                ? $"[Transition] Failed: {message}. The old room is already gone; unlocking the hero in the current room."
                : $"[Transition] Failed: {message}. Returning to the old room.");

            // Drop a room that was loaded hidden but never made live (and finish any interrupted swap).
            if (loader is IRoomLoaderCleanup cleanup)
            {
                cleanup.AbandonPending();
                yield return Stage(cleanup.FinishAbandon(), Debug.LogException);
            }

            Exception fadeError = null;
            yield return StartFade(fader.FadeIn(), ex => fadeError = ex);
            if (fadeError != null) Debug.LogException(fadeError);

            if (hero is ITransitionHeroRecovery recovery)
            {
                try { recovery.RestoreControl(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            else
            {
                TransitionPoint back = null;
                if (!oldRoomGone)
                    foreach (var g in senders) if (g != null) { back = g; break; }

                if (back != null)
                    yield return Stage(hero.EnterRoom(back, 0f), Debug.LogException);
                else
                    Debug.LogError("[Transition] The hero has no recovery hook and no sending gate exists: it stays locked.");
            }

            foreach (var g in senders) if (g != null) g.ResetActivation();
            State = GameState.Playing;
            running = false;
            routine = null;
            Notify(TransitionFinished, false);
        }

        // Starts a fade and remembers it, cancelling the previous one so a stale fade-in cannot undo a fade-out.
        Coroutine StartFade(IEnumerator fade, Action<Exception> onError)
        {
            if (activeFade != null) StopCoroutine(activeFade);
            activeFade = StartCoroutine(Stage(fade, onError));
            return activeFade;
        }

        // Last line of defence: anything not caught by a stage ends in Abort, never in a frozen state.
        IEnumerator Guard(IEnumerator inner)
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
                    Debug.LogException(ex);
                    Abort();
                    yield break;
                }
                yield return current;
            }
        }

        // Steps an enumerator by hand so an exception from a collaborator is caught and reported, not thrown
        // out of the manager's coroutine (which would freeze the game in EXITING_LEVEL).
        static IEnumerator Stage(IEnumerator inner, Action<Exception> onError)
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
    }
}
