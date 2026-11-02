using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowknight.Silksong
{
    // Optional extra for the hero side: give control back after a transition failed. ITransitionHero (frozen)
    // has no such call, and EnterRoom needs a gate, which may not exist after a failed load.
    public interface ITransitionHeroRecovery
    {
        void RestoreControl();
    }

    // Runs a room change: PLAYING -> EXITING_LEVEL -> (load hidden) -> ENTERING_LEVEL -> PLAYING.
    // A small slice of the original GameManager.BeginSceneTransitionRoutine. All work with the outside world goes
    // through IRoomLoader, IScreenFader and ITransitionHero so it is tested with fakes.
    public class RoomTransitionManager : MonoBehaviour, ITransitionRequestHandler
    {
        public RoomTransitionSettings Settings = new RoomTransitionSettings();

        IRoomLoader loader;
        IScreenFader fader;
        ITransitionHero hero;
        Coroutine routine;
        bool running;

        public GameState State { get; private set; } = GameState.Playing;
        public bool IsTransitionRunning => running;

        // Wires the collaborators. Called by the bootstrap (Task 6) or a test.
        public void Configure(IRoomLoader roomLoader, IScreenFader screenFader, ITransitionHero transitionHero)
        {
            loader = roomLoader;
            fader = screenFader;
            hero = transitionHero;
        }

        void OnEnable() => TransitionPoint.Handler = this;

        // Both, because a destroyed manager must never stay referenced by the static gate handler.
        void OnDisable() => ClearHandler();
        void OnDestroy() => ClearHandler();

        void ClearHandler()
        {
            if (ReferenceEquals(TransitionPoint.Handler, this)) TransitionPoint.Handler = null;
        }

        public void RequestTransition(TransitionRequest request)
        {
            // EnteringLevel is accepted: a bottom gate may be left straight after arriving (TransitionRules).
            bool stateOk = State == GameState.Playing || State == GameState.EnteringLevel;
            bool valid = !string.IsNullOrEmpty(request.TargetScene) && !string.IsNullOrEmpty(request.EntryGate);
            if (running || !stateOk || !valid || loader == null || fader == null || hero == null)
            {
                Debug.LogWarning($"[Transition] Request to '{request.TargetScene}' rejected (running={running}, state={State}, valid={valid}).");
                // The gate marked itself activated when it sent this; let it fire again.
                foreach (var gate in FindGates(request)) gate.ResetActivation();
                return;
            }

            // A walk-in still in progress is abandoned: the new request takes over the hero.
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Run(request));
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

            hero.LeaveRoom(request.LeaveDirection);   // locks control from here on

            // Fade and the fixed wait run side by side: the new room may only go live when both are done.
            var fade = StartCoroutine(fader.FadeOut());
            yield return new WaitForSecondsRealtime(Settings.fadeWaitSeconds);
            yield return fade;

            string error = null;
            yield return RunGuarded(loader.LoadHidden(request.TargetScene), e => error = e);
            if (error == null)
                yield return RunGuarded(loader.ActivateAndUnloadPrevious(), e => error = e);

            TransitionPoint arrival = null;
            if (error == null)
            {
                var scene = SceneManager.GetSceneByName(request.TargetScene);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    error = $"scene '{request.TargetScene}' is not loaded after activation";
                }
                else
                {
                    // Scoped to the NEW room: both rooms use names like left1 / right1.
                    arrival = GateRegistry.Find(request.EntryGate, scene);
                    if (arrival == null) error = $"scene '{request.TargetScene}' has no gates";
                }
            }

            if (error != null)
            {
                yield return Recover(senders, error);
                yield break;
            }

            // The original's SaveLevelState, map-zone events and GC pass would run here.
            Stubs.NotImplemented("RoomTransitionManager.LevelStateAndGc");

            State = GameState.EnteringLevel;
            running = false;   // gates may fire again now (bottom-gate exception)

            yield return new WaitForSecondsRealtime(Settings.heroWaitSeconds);
            var fadeIn = StartCoroutine(fader.FadeIn());
            yield return hero.EnterRoom(arrival, request.EntryDelay + arrival.EntryDelay);
            yield return fadeIn;

            State = GameState.Playing;
            routine = null;
        }

        IEnumerator Recover(List<TransitionPoint> senders, string error)
        {
            Debug.LogError($"[Transition] Failed: {error}. Restoring play.");
            yield return fader.FadeIn();

            if (hero is ITransitionHeroRecovery recovery)
            {
                recovery.RestoreControl();
            }
            else
            {
                TransitionPoint back = null;
                foreach (var g in senders) if (g != null) { back = g; break; }
                if (back != null) yield return hero.EnterRoom(back, 0f);
                else Debug.LogError("[Transition] No way to give the hero control back (no recovery hook and no sending gate).");
            }

            foreach (var g in senders) if (g != null) g.ResetActivation();
            State = GameState.Playing;
            running = false;
            routine = null;
        }

        // Steps an enumerator by hand so an exception from the loader is caught and reported, not thrown
        // out of the manager's coroutine (which would freeze the game in EXITING_LEVEL).
        static IEnumerator RunGuarded(IEnumerator inner, Action<string> onError)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!inner.MoveNext()) yield break;
                    current = inner.Current;
                }
                catch (Exception e)
                {
                    onError(e.Message);
                    yield break;
                }
                yield return current;
            }
        }
    }
}
