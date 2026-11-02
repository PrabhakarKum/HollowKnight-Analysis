using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hollowknight.Silksong.Tests
{
    // Source: GameManager.FindTransitionPoint (GameManager.cs line 3170): first gate whose name equals the
    // stored name; when none matches, the first gate in the scene is used and a warning is logged.
    public class GateRegistryTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [SetUp]
        public void SetUp() => GateRegistry.ClearForTests();

        [TearDown]
        public void TearDown()
        {
            GateRegistry.ClearForTests();
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            foreach (var s in scenes)
                if (s.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(s);
            scenes.Clear();
        }

        // EditMode does not run OnEnable on plain MonoBehaviours, so tests register gates explicitly.
        TransitionPoint MakeGate(string name, UnityEngine.SceneManagement.Scene? scene = null)
        {
            var go = new GameObject(name);
            created.Add(go);
            // TransitionPoint requires a Collider2D, which is abstract, so Unity cannot add it itself.
            go.AddComponent<BoxCollider2D>().isTrigger = true;
            var gate = go.AddComponent<TransitionPoint>();
            if (scene.HasValue) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene.Value);
            GateRegistry.Register(gate);
            return gate;
        }

        readonly List<UnityEngine.SceneManagement.Scene> scenes = new List<UnityEngine.SceneManagement.Scene>();

        UnityEngine.SceneManagement.Scene NewScene()
        {
            // Preview scenes work even when the active scene is untitled (batch runs), unlike additive NewScene.
            var s = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            scenes.Add(s);
            return s;
        }

        [Test]
        public void ScopedFind_SameNameInTwoScenes_ResolvesToTheRequestedScene()
        {
            var a = NewScene();
            var b = NewScene();
            var inA = MakeGate("left1", a);
            var inB = MakeGate("left1", b);
            Assert.AreSame(inA, GateRegistry.Find("left1", a));
            Assert.AreSame(inB, GateRegistry.Find("left1", b));
        }

        [Test]
        public void ScopedFind_MissingName_FallsBackToTheFirstGateOfThatScene()
        {
            var a = NewScene();
            var b = NewScene();
            MakeGate("left1", a);
            var firstInB = MakeGate("right9", b);
            MakeGate("right1", b);
            LogAssert.Expect(LogType.Warning, new Regex("nope.*right9"));
            Assert.AreSame(firstInB, GateRegistry.Find("nope", b));
            Assert.AreSame(firstInB, GateRegistry.FirstGate(b));
        }

        [Test]
        public void ScopedFind_SceneWithoutGates_ReturnsNullAndLogsAnError()
        {
            var a = NewScene();
            var empty = NewScene();
            MakeGate("left1", a);
            LogAssert.Expect(LogType.Error, new Regex("no gates"));
            Assert.IsNull(GateRegistry.Find("left1", empty));
            Assert.IsNull(GateRegistry.FirstGate(empty));
        }

        [Test]
        public void Find_ReturnsTheGateWithThatName()
        {
            MakeGate("left1");
            var right2 = MakeGate("right2");
            MakeGate("top1");
            Assert.AreSame(right2, GateRegistry.Find("right2"));
        }

        [Test]
        public void Find_PicksTheFirstOfTwoGatesWithTheSameName()
        {
            var first = MakeGate("left1");
            MakeGate("left1");
            Assert.AreSame(first, GateRegistry.Find("left1"));
        }

        [Test]
        public void Find_MissingGate_FallsBackToTheFirstGateAndWarns()
        {
            var first = MakeGate("left1");
            MakeGate("right1");
            LogAssert.Expect(LogType.Warning, new Regex("nope.*left1"));
            Assert.AreSame(first, GateRegistry.Find("nope"));
        }

        [Test]
        public void Find_EmptyName_FallsBackAndWarns()
        {
            var first = MakeGate("bot1");
            LogAssert.Expect(LogType.Warning, new Regex("bot1"));
            Assert.AreSame(first, GateRegistry.Find(""));
        }

        [Test]
        public void Find_NoGatesAtAll_ReturnsNullAndLogsAnError()
        {
            LogAssert.Expect(LogType.Error, new Regex("no gates"));
            Assert.IsNull(GateRegistry.Find("left1"));
        }

        [Test]
        public void Find_ExactNameMatch_DoesNotWarn()
        {
            MakeGate("left1");
            GateRegistry.Find("left1");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Find_NameIsCaseSensitive()
        {
            MakeGate("left1");
            LogAssert.Expect(LogType.Warning, new Regex("Left1"));
            GateRegistry.Find("Left1");
        }

        [Test]
        public void Unregister_RemovesTheGate()
        {
            var a = MakeGate("left1");
            var b = MakeGate("right1");
            GateRegistry.Unregister(a);
            LogAssert.Expect(LogType.Warning, new Regex("left1"));
            Assert.AreSame(b, GateRegistry.Find("left1"), "falls back to the only remaining gate");
        }

        [Test]
        public void Register_SameGateTwice_IsStoredOnce()
        {
            var a = MakeGate("left1");
            GateRegistry.Register(a);
            Assert.AreEqual(1, GateRegistry.Count);
        }

        [Test]
        public void Find_DestroyedGatesAreSkipped()
        {
            var dead = MakeGate("left1");
            var alive = MakeGate("left2");
            Object.DestroyImmediate(dead.gameObject);
            LogAssert.Expect(LogType.Warning, new Regex("left1"));
            Assert.AreSame(alive, GateRegistry.Find("left1"));
        }
    }
}
