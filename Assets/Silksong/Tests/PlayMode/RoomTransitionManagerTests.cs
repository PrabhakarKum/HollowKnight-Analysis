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
    // Failure tests log errors on purpose, so failing log messages are ignored and the tests check the captured
    // log text instead (order independent).
    public class RoomTransitionManagerTests
    {
        class Log { public List<string> Events = new List<string>(); }

        class FakeFader : IScreenFader
        {
            readonly Log log;
            public int FadeInFrames = 1;
            public FakeFader(Log l) { log = l; }
            public IEnumerator FadeOut() { log.Events.Add("fadeOut"); yield return null; log.Events.Add("fadeOutDone"); }
            public IEnumerator FadeIn()
            {
                log.Events.Add("fadeIn");
                for (int i = 0; i < FadeInFrames; i++) yield return null;
                log.Events.Add("fadeInDone");
            }
        }

        // A hero WITHOUT the recovery hook.
        class FakePlainHero : ITransitionHero
        {
            protected readonly Log log;
            public bool ThrowOnLeave, ThrowOnEnter;
            public int EnterFrames = 1;
            public FakePlainHero(Log l) { log = l; }
            public bool ControlLocked { get; set; }
            public Rigidbody2D Body => null;
            public HeroTransitionState GetTransitionState() => new HeroTransitionState { InPosition = true };
            public void LeaveRoom(GatePosition gate)
            {
                ControlLocked = true;
                log.Events.Add("leave:" + gate);
                if (ThrowOnLeave) throw new InvalidOperationException("leave-boom");
            }
            public IEnumerator EnterRoom(TransitionPoint gate, float delay)
            {
                log.Events.Add("enter:" + gate.name);
                for (int i = 0; i < EnterFrames; i++) yield return null;
                if (ThrowOnEnter) throw new InvalidOperationException("enter-boom");
                ControlLocked = false;
                log.Events.Add("enterDone");
            }
        }

        class FakeHero : FakePlainHero, ITransitionHeroRecovery
        {
            public FakeHero(Log l) : base(l) { }
            public void RestoreControl() { ControlLocked = false; log.Events.Add("restore"); }
        }

        class FakeLoader : IRoomLoader
        {
            readonly Log log;
            public bool ThrowOnLoad, ThrowOnActivate, CreateScene = true;
            public string[] Gates = { "left1", "right1" };
            public readonly List<Scene> Created = new List<Scene>();
            public FakeLoader(Log l) { log = l; }

            public IEnumerator LoadHidden(string sceneName)
            {
                log.Events.Add("load");
                yield return null;
                if (ThrowOnLoad) throw new InvalidOperationException("load-boom");
                if (!CreateScene) yield break;
                var scene = SceneManager.CreateScene(sceneName);
                Created.Add(scene);
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
                if (ThrowOnActivate) throw new InvalidOperationException("activate-boom");
            }
        }

        Log log;
        FakeFader fader;
        FakePlainHero hero;
        FakeLoader loader;
        RoomTransitionManager manager;
        string sceneName;
        readonly List<GameObject> spawned = new List<GameObject>();
        readonly List<string> captured = new List<string>();

        void OnLog(string message, string stack, LogType type) => captured.Add(type + ": " + message);

        bool Logged(string fragment) => captured.Exists(c => c.Contains(fragment));

        int Count(string ev) => log.Events.FindAll(e => e == ev).Count;

        // Declares expected log messages in the order the manager emits them (Unity needs every error and
        // exception to be expected).
        static void ExpectLogs(params (LogType type, string regex)[] logs)
        {
            foreach (var l in logs) LogAssert.Expect(l.type, new System.Text.RegularExpressions.Regex(l.regex));
        }

        const string FailedOld = @"\[Transition\] Failed: .*Returning to the old room";
        const string FailedGone = @"\[Transition\] Failed: .*old room is already gone";

        static string NewSceneName() => "RtmRoom_" + Guid.NewGuid().ToString("N");

        [SetUp]
        public void SetUp()
        {
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            Stubs.StubsDocPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rtm-stubs-" + Guid.NewGuid() + ".md");
            Stubs.ResetForTests();
            captured.Clear();
            Application.logMessageReceived += OnLog;
            // Failure tests declare their expected messages with ExpectLogs.

            log = new Log();
            fader = new FakeFader(log);
            hero = new FakeHero(log);
            loader = new FakeLoader(log);
            sceneName = NewSceneName();
            MakeManager();
        }

        void MakeManager(ITransitionHero useHero = null)
        {
            var go = new GameObject("Manager");
            spawned.Add(go);
            manager = go.AddComponent<RoomTransitionManager>();
            manager.Configure(loader, fader, useHero ?? hero,
                new RoomTransitionSettings { fadeWaitSeconds = 0f, heroWaitSeconds = 0f, fadeOutSeconds = 0f, fadeInSeconds = 0f });
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Application.logMessageReceived -= OnLog;
            // (captured log list is cleared in SetUp)
            foreach (var go in spawned) if (go != null) UnityEngine.Object.Destroy(go);
            foreach (var scene in loader.Created)
                if (scene.IsValid() && scene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(scene);
            TransitionPoint.ResetHandlerForTests();
            GateRegistry.ClearForTests();
            Stubs.StubsDocPath = null;
            Stubs.ResetForTests();
        }

        TransitionRequest Request(string entry = "left1") => new TransitionRequest
        {
            TargetScene = sceneName, EntryGate = entry, LeaveDirection = GatePosition.Right
        };

        // Waits for a condition, failing the test on timeout instead of hanging.
        IEnumerator Until(Func<bool> condition, string what, float timeout = 3f)
        {
            float start = Time.unscaledTime;
            while (!condition())
            {
                if (Time.unscaledTime - start > timeout) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        IEnumerator UntilIdle() =>
            Until(() => !manager.IsTransitionRunning && manager.State == GameState.Playing, "transition to finish");

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

            float start = Time.unscaledTime;
            while (manager.State != GameState.Playing)
            {
                Assert.Less(Time.unscaledTime - start, 3f, "timeout");
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
            float start = Time.unscaledTime;
            manager.RequestTransition(Request());
            yield return Until(() => log.Events.Contains("load"), "load");
            // Frame granularity: allow one frame of slack.
            Assert.GreaterOrEqual(Time.unscaledTime - start, 0.34f - 0.05f);
            yield return UntilIdle();
        }

        [Test]
        public void Defaults_MatchStudyValues()
        {
            var s = new RoomTransitionSettings();
            Assert.AreEqual(0.34f, s.fadeWaitSeconds);
            Assert.AreEqual(0.165f, s.heroWaitSeconds);
            Assert.IsNull(s.ValidationWarning());
            s.fadeOutSeconds = s.fadeWaitSeconds;
            Assert.IsNotNull(s.ValidationWarning());
        }

        [UnityTest]
        public IEnumerator SecondRequestWhileRunning_IsIgnored()
        {
            manager.RequestTransition(Request());
            manager.RequestTransition(Request("right1"));
            Assert.IsTrue(Logged("rejected"));
            yield return UntilIdle();
            Assert.AreEqual(1, Count("load"));
            Assert.AreEqual(1, log.Events.FindAll(e => e.StartsWith("leave")).Count);
        }

        [Test]
        public void RejectedRequest_ResetsTheRealGate_SoItCanFireAgain()
        {
            var go = new GameObject("right1");
            spawned.Add(go);
            go.AddComponent<BoxCollider2D>();
            var gate = go.AddComponent<TransitionPoint>();
            gate.Configure(sceneName, "left1", Vector2.zero);
            manager.Configure(null, null, null);   // makes the manager reject every request
            var heroState = new HeroTransitionState { FacingRight = true, InPosition = true };

            Assert.AreEqual(TransitionDecision.Leave, gate.Touch(manager, heroState));
            // Without the reset the gate would stay activated and ignore this second touch.
            Assert.AreEqual(TransitionDecision.Leave, gate.Touch(manager, heroState), "gate fired again after rejection");
        }

        [Test]
        public void MissingCollaborators_LogOnlyOnceUntilReconfigured()
        {
            manager.Configure(null, null, null);
            manager.RequestTransition(Request());
            manager.RequestTransition(Request());
            Assert.AreEqual(1, captured.FindAll(c => c.Contains("not configured")).Count);
            manager.Configure(null, null, null);
            manager.RequestTransition(Request());
            Assert.AreEqual(2, captured.FindAll(c => c.Contains("not configured")).Count);
        }

        [UnityTest]
        public IEnumerator MissingGate_FallsBackWithWarning()
        {
            manager.RequestTransition(Request("nope"));
            yield return UntilIdle();
            Assert.IsTrue(Logged("Gate 'nope' not found"));
            Assert.Contains("enter:left1", log.Events);
        }

        [UnityTest]
        public IEnumerator LoaderThrowsInLoad_RecoversIntoOldRoom()
        {
            ExpectLogs((LogType.Exception, "load-boom"), (LogType.Error, FailedOld));
            loader.ThrowOnLoad = true;
            manager.RequestTransition(Request());
            yield return UntilIdle();
            Assert.IsTrue(Logged("load-boom"), "exception logged with details");
            Assert.IsTrue(Logged("Returning to the old room"));
            Assert.IsFalse(hero.ControlLocked);
            Assert.Contains("fadeIn", log.Events);
            Assert.Contains("restore", log.Events);
        }

        [UnityTest]
        public IEnumerator LoaderThrowsInActivate_RecoversIntoOldRoom()
        {
            ExpectLogs((LogType.Exception, "activate-boom"), (LogType.Error, FailedOld));
            loader.ThrowOnActivate = true;
            manager.RequestTransition(Request());
            yield return UntilIdle();
            Assert.IsTrue(Logged("activate-boom"));
            Assert.IsTrue(Logged("Returning to the old room"));
            Assert.IsFalse(hero.ControlLocked);
        }

        [UnityTest]
        public IEnumerator EnterRoomThrows_EndsPlayingAndUnlocked()
        {
            ExpectLogs((LogType.Exception, "enter-boom"), (LogType.Error, FailedGone));
            ((FakePlainHero)hero).ThrowOnEnter = true;
            manager.RequestTransition(Request());
            yield return UntilIdle();
            Assert.IsTrue(Logged("enter-boom"));
            Assert.IsFalse(hero.ControlLocked);
        }

        [UnityTest]
        public IEnumerator LeaveRoomThrows_EndsPlayingAndUnlocked()
        {
            ExpectLogs((LogType.Exception, "leave-boom"), (LogType.Error, FailedOld));
            ((FakePlainHero)hero).ThrowOnLeave = true;
            manager.RequestTransition(Request());
            yield return UntilIdle();
            Assert.IsTrue(Logged("leave-boom"));
            Assert.IsFalse(hero.ControlLocked);
            Assert.AreEqual(0, Count("load"), "no load after a failed leave");
        }

        [UnityTest]
        public IEnumerator AfterSwap_SceneWithoutGates_IsFatalButSafe()
        {
            ExpectLogs((LogType.Error, "no gates in the searched room"), (LogType.Error, FailedGone));
            loader.Gates = new string[0];
            manager.RequestTransition(Request());
            yield return UntilIdle();
            Assert.IsTrue(Logged("old room is already gone"));
            Assert.IsFalse(hero.ControlLocked);
            Assert.Contains("fadeInDone", log.Events);
        }

        [UnityTest]
        public IEnumerator AfterSwap_SceneNotLoaded_IsFatalButSafe()
        {
            ExpectLogs((LogType.Error, FailedGone));
            loader.CreateScene = false;
            manager.RequestTransition(Request());
            yield return UntilIdle();
            Assert.IsTrue(Logged("old room is already gone"));
            Assert.IsFalse(hero.ControlLocked);
            Assert.Contains("fadeInDone", log.Events);
        }

        [UnityTest]
        public IEnumerator HeroWithoutRecovery_NoSendingGate_LogsErrorAndStaysLockedButPlaying()
        {
            var plain = new FakePlainHero(log);
            MakeManager(plain);
            ExpectLogs((LogType.Exception, "load-boom"), (LogType.Error, FailedOld), (LogType.Error, "no recovery hook"));
            loader.ThrowOnLoad = true;
            manager.RequestTransition(Request());
            yield return UntilIdle();
            Assert.IsTrue(Logged("no recovery hook"));
            Assert.AreEqual(GameState.Playing, manager.State);
            Assert.IsTrue(plain.ControlLocked, "documented limit: no recovery hook, no gate");
        }

        [UnityTest]
        public IEnumerator HeroWithoutRecovery_WithSendingGate_IsPlacedBackAtTheGate()
        {
            var plain = new FakePlainHero(log);
            MakeManager(plain);
            var go = new GameObject("right1");
            spawned.Add(go);
            go.AddComponent<BoxCollider2D>();
            var gate = go.AddComponent<TransitionPoint>();
            gate.Configure(sceneName, "left1", Vector2.zero);
            ExpectLogs((LogType.Exception, "load-boom"), (LogType.Error, FailedOld));
            loader.ThrowOnLoad = true;

            manager.RequestTransition(Request());
            yield return UntilIdle();
            Assert.Contains("enter:right1", log.Events);
            Assert.IsFalse(plain.ControlLocked);
        }

        [UnityTest]
        public IEnumerator DisableMidTransition_RestoresEverything_AndReEnableAcceptsRequests()
        {
            var fgo = new GameObject("Fader");
            spawned.Add(fgo);
            var sf = fgo.AddComponent<ScreenFader>();
            var settings = new RoomTransitionSettings { fadeWaitSeconds = 0.4f, fadeOutSeconds = 0.1f, heroWaitSeconds = 0f, fadeInSeconds = 0f };
            manager.Configure(loader, sf, hero, settings);
            Assert.AreSame(settings, sf.Settings, "one shared settings object");

            manager.RequestTransition(Request());
            yield return Until(() => sf.Alpha > 0.2f, "fade to start");
            manager.enabled = false;

            Assert.IsNull(TransitionPoint.Handler);
            Assert.AreEqual(0f, sf.Alpha);
            Assert.AreEqual(GameState.Playing, manager.State);
            Assert.IsFalse(manager.IsTransitionRunning);
            Assert.IsFalse(hero.ControlLocked);
            Assert.Contains("restore", log.Events);

            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(0, Count("load"), "the aborted transition never loads");

            manager.enabled = true;
            Assert.AreSame(manager, TransitionPoint.Handler);
            settings.fadeWaitSeconds = 0.2f;
            manager.RequestTransition(Request());
            Assert.IsTrue(manager.IsTransitionRunning);
            yield return UntilIdle();
            Assert.AreEqual(1, Count("load"));
        }

        [UnityTest]
        public IEnumerator RequestDuringEnteringLevel_IsAccepted_AndAbandonsTheWalkInAndFade()
        {
            ((FakePlainHero)hero).EnterFrames = 20;
            fader.FadeInFrames = 20;
            manager.RequestTransition(Request());
            yield return Until(() => log.Events.Contains("enter:left1"), "entering");
            Assert.AreEqual(GameState.EnteringLevel, manager.State);
            Assert.IsFalse(manager.IsTransitionRunning, "running ends when entering starts");

            sceneName = NewSceneName();
            manager.RequestTransition(Request());
            Assert.IsTrue(manager.IsTransitionRunning, "bottom-gate style request accepted");
            Assert.AreEqual(GameState.ExitingLevel, manager.State);

            yield return UntilIdle();
            Assert.AreEqual(2, Count("load"));
            Assert.AreEqual(1, Count("enterDone"), "the abandoned walk-in never completed");
            Assert.AreEqual(1, Count("fadeInDone"), "the abandoned fade-in was stopped, only the new one finished");
        }

        [UnityTest]
        public IEnumerator AfterFinishing_ANewTransitionIsAccepted()
        {
            manager.RequestTransition(Request());
            yield return UntilIdle();
            sceneName = NewSceneName();
            manager.RequestTransition(Request());
            Assert.IsTrue(manager.IsTransitionRunning);
            yield return UntilIdle();
            Assert.AreEqual(2, Count("load"));
        }

        [UnityTest]
        public IEnumerator ScreenFader_FadesAlpha_AndNewestFadeWins()
        {
            var go = new GameObject("Fader");
            spawned.Add(go);
            var f = go.AddComponent<ScreenFader>();
            f.Configure(new RoomTransitionSettings { fadeOutSeconds = 0.15f, fadeInSeconds = 0.05f });
            Assert.AreEqual(0f, f.Alpha);
            Assert.IsFalse(f.enabled, "OnGUI stays off while nothing is drawn");

            go.GetComponent<ScreenFader>().StartCoroutine(f.FadeOut());
            yield return null;
            Assert.IsTrue(f.enabled);
            go.GetComponent<ScreenFader>().StartCoroutine(f.FadeIn());   // stale fade-out must not win
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(0f, f.Alpha);
            Assert.IsFalse(f.enabled);

            yield return f.FadeOut();
            Assert.AreEqual(1f, f.Alpha);
            yield return f.FadeIn();
            Assert.AreEqual(0f, f.Alpha);
        }
    }
}
