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
        }

        // EditMode does not run OnEnable on plain MonoBehaviours, so tests register gates explicitly.
        TransitionPoint MakeGate(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            var gate = go.AddComponent<TransitionPoint>();
            GateRegistry.Register(gate);
            return gate;
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
