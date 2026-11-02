using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hollowknight.Silksong.Tests
{
    // Drives RoomTransitionManager entirely with fakes. "Rooms" are empty scenes created in code.
    public class RoomTransitionManagerTests
    {
        class Log { public List<string> Events = new List<string>(); }

        class FakeFader : IScreenFader
        {
            readonly Log log; public FakeFader(Log l) { log = l; }
            public IEnumerator FadeOut() { log.Events.Add("fadeOut"); yield return null; log.Events.Add("fadeOutDone"); }
            public IEnumerator FadeIn() { log.Events.Add("fadeIn"); yield return null; log.Events.Add("fadeInDone"); }
        }

        class FakeHero : ITransitionHero, ITransitionHeroRecovery
        {
            readonly Log log; public FakeHero(Log l) { log = l; }
            public bool ControlLocked { get; set; }
            public Rigidbody2D Body => null;
            public HeroTransitionState GetTransitionState() => new HeroTransitionState { InPosition = true };
            public void LeaveRoom(GatePosition gate) { ControlLocked = true; log.Events.Add("leave:" + gate); }
            public IEnumerator EnterRoom(TransitionPoint gate, float delay)
            {
                log.Events.Add("enter:" + gate.name);
                yield return null;
                ControlLocked = false;
            }
            public void RestoreControl() { ControlLocked = false; log.Events.Add("restore"); }
        }

        class FakeLoader : IRoomLoader
        {
            readonly Log log;
            public bool Throw;
            public string[] Gates = { "left1", "right1" };
            public FakeLoader(Log l) { log = l; }

            public IEnumerator LoadHidden(string sceneName)
            {
                log.Events.Add("load");
                yield return null;
                if (Throw) throw new InvalidOperationException("boom");
                var scene = SceneManager.CreateScene(sceneName);
                foreach (var g in Gates)
                {
                    var go = new GameObject(g);
                    go.AddComponent<BoxCollider2D>();
                    SceneManager.MoveGameObjectToScene(go, scene);
                    go.AddComponent<TransitionPoint>();
                }
            }

            public IEnumerator ActivateAndUnloadPrevious()
            {
                log.Events.Add("activate");
                yield return null;
            }
        }

        Log log;
        FakeFader fader;
        FakeHero hero;
        FakeLoader loader;
        RoomTransitionManager manager;
        string sceneName;
        readonly List<GameObject> spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            Stubs.StubsDocPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rtm-stubs-" + Guid.NewGuid() + ".md");
            Stubs.ResetForTests();
            log = new Log();
            fader = new FakeFader(log);
            hero = new FakeHero(log);
            loader = new FakeLoader(log);
            sceneName = "RtmRoom_" + Guid.NewGuid().ToString("N");

            var go = new GameObject("Manager");
            spawned.Add(go);
            manager = go.AddComponent<RoomTransitionManager>();
            manager.Settings = new RoomTransitionSettings { fadeWaitSeconds = 0f, heroWaitSeconds = 0f };
            manager.Configure(loader, fader, hero);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) if (go != null) UnityEngine.Object.Destroy(go);
            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
        }

        TransitionRequest Request(string entry = "left1") => new TransitionRequest
        {
            TargetScene = sceneName, EntryGate = entry, LeaveDirection = GatePosition.Right
        };

        IEnumerator WaitUntilPlaying(float timeout = 3f)
        {
            float t = 0f;
            while ((manager.IsTransitionRunning || manager.State != GameState.Playing) && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        [Test]
        public void Enable_SetsHandler_DestroyAndDisable_ClearIt()
        {
            Assert.AreSame(manager, TransitionPoint.Handler);
            manager.enabled = false;
            Assert.IsNull(TransitionPoint.Handler);
            manager.enabled = true;
            Assert.AreSame(manager, TransitionPoint.Handler);
            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            Assert.IsNull(TransitionPoint.Handler);
        }

        [UnityTest]
        public IEnumerator FullSequence_RunsInOrder_AndEndsPlaying()
        {
            var states = new List<GameState>();
            manager.RequestTransition(Request());
            Assert.AreEqual(GameState.ExitingLevel, manager.State);
            Assert.IsTrue(manager.IsTransitionRunning);
            Assert.IsTrue(hero.ControlLocked);

            while (manager.State != GameState.Playing)
            {
                if (states.Count == 0 || states[states.Count - 1] != manager.State) states.Add(manager.State);
                yield return null;
            }

            CollectionAssert.AreEqual(new[] { GameState.ExitingLevel, GameState.EnteringLevel }, states);
            Assert.IsFalse(hero.ControlLocked);
            Assert.IsFalse(manager.IsTransitionRunning);

            int iLeave = log.Events.IndexOf("leave:Right");
            int iLoad = log.Events.IndexOf("load");
            int iActivate = log.Events.IndexOf("activate");
            int iEnter = log.Events.IndexOf("enter:left1");
            Assert.That(iLeave, Is.GreaterThanOrEqualTo(0));
            Assert.Less(iLeave, iLoad);
            Assert.Less(iLoad, iActivate);
            Assert.Less(iActivate, iEnter);
            Assert.Less(log.Events.IndexOf("fadeOutDone"), iLoad, "load starts only after the screen is dark");
        }

        [UnityTest]
        public IEnumerator FadeWait_HoldsBackTheLoad()
        {
            manager.Settings.fadeWaitSeconds = 0.34f;
            float start = Time.realtimeSinceStartup;
            manager.RequestTransition(Request());
            while (!log.Events.Contains("load")) yield return null;
            Assert.GreaterOrEqual(Time.realtimeSinceStartup - start, 0.33f);
            yield return WaitUntilPlaying();
        }

        [Test]
        public void Defaults_MatchStudyValues()
        {
            var s = new RoomTransitionSettings();
            Assert.AreEqual(0.34f, s.fadeWaitSeconds);
            Assert.AreEqual(0.165f, s.heroWaitSeconds);
        }

        [UnityTest]
        public IEnumerator SecondRequestWhileRunning_IsIgnored()
        {
            manager.RequestTransition(Request());
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[Transition\] Request .* rejected"));
            manager.RequestTransition(Request("right1"));
            yield return WaitUntilPlaying();
            Assert.AreEqual(1, log.Events.FindAll(e => e == "load").Count);
            Assert.AreEqual(1, log.Events.FindAll(e => e.StartsWith("leave")).Count);
        }

        [UnityTest]
        public IEnumerator RejectedRequest_ResetsTheTouchedGate()
        {
            var go = new GameObject("right1");
            spawned.Add(go);
            go.AddComponent<BoxCollider2D>();
            var gate = go.AddComponent<TransitionPoint>();
            gate.Configure(sceneName, "left1", Vector2.zero);

            var decision = gate.Touch(manager, new HeroTransitionState { FacingRight = true, InPosition = true });
            Assert.AreEqual(TransitionDecision.Leave, decision);
            yield return WaitUntilPlaying();

            // Gate is activated now. Force a rejection by sending the same request while paused-like state.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"rejected"));
            manager.RequestTransition(new TransitionRequest { TargetScene = sceneName, EntryGate = "", LeaveDirection = GatePosition.Right });
            // Invalid request has no matching gate; the gate with a real target stays activated, as designed.
            // Now an actual match: same request fields as the gate, but manager has no loader.
            manager.Configure(null, fader, hero);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"rejected"));
            manager.RequestTransition(gate.BuildRequest());
            var again = gate.Touch(manager, new HeroTransitionState { FacingRight = true, InPosition = true });
            Assert.AreEqual(TransitionDecision.Leave, again, "gate fires again after the rejection reset it");
        }

        [UnityTest]
        public IEnumerator MissingGate_FallsBackWithWarning()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"Gate 'nope' not found"));
            manager.RequestTransition(Request("nope"));
            yield return WaitUntilPlaying();
            Assert.AreEqual(GameState.Playing, manager.State);
            Assert.Contains("enter:left1", log.Events);
        }

        [UnityTest]
        public IEnumerator LoaderThrows_RestoresPlayAndControl()
        {
            loader.Throw = true;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[Transition\] Failed"));
            manager.RequestTransition(Request());
            yield return WaitUntilPlaying();
            Assert.AreEqual(GameState.Playing, manager.State);
            Assert.IsFalse(manager.IsTransitionRunning);
            Assert.IsFalse(hero.ControlLocked);
        }

        [UnityTest]
        public IEnumerator TargetSceneWithoutGates_RestoresPlayAndControl()
        {
            loader.Gates = new string[0];
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"no gates in the searched room"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[Transition\] Failed"));
            manager.RequestTransition(Request());
            yield return WaitUntilPlaying();
            Assert.AreEqual(GameState.Playing, manager.State);
            Assert.IsFalse(hero.ControlLocked);
        }

        [UnityTest]
        public IEnumerator AfterFinishing_ANewTransitionIsAccepted()
        {
            manager.RequestTransition(Request());
            yield return WaitUntilPlaying();
            sceneName = "RtmRoom_" + Guid.NewGuid().ToString("N");
            manager.RequestTransition(Request());
            Assert.IsTrue(manager.IsTransitionRunning);
            yield return WaitUntilPlaying();
            Assert.AreEqual(2, log.Events.FindAll(e => e == "load").Count);
        }

        [UnityTest]
        public IEnumerator ScreenFader_FadesAlpha()
        {
            var go = new GameObject("Fader");
            spawned.Add(go);
            var f = go.AddComponent<ScreenFader>();
            f.Settings = new RoomTransitionSettings { fadeOutSeconds = 0.05f, fadeInSeconds = 0.05f };
            Assert.AreEqual(0f, f.Alpha);
            yield return f.FadeOut();
            Assert.AreEqual(1f, f.Alpha);
            yield return f.FadeIn();
            Assert.AreEqual(0f, f.Alpha);
        }
    }
}
