using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    public class TransitionPointTests
    {
        GameObject go;

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        TransitionPoint Make(string name)
        {
            go = new GameObject(name);
            return go.AddComponent<TransitionPoint>();
        }

        static HeroTransitionState OutwardRightHero() => new HeroTransitionState { FacingRight = true, InPosition = true };

        [TestCase("left2", GatePosition.Left)]
        [TestCase("right1", GatePosition.Right)]
        [TestCase("top1", GatePosition.Top)]
        [TestCase("bot2", GatePosition.Bottom)]
        [TestCase("door1", GatePosition.Door)]
        [TestCase("whatever", GatePosition.Unknown)]
        public void Position_ComesFromTheObjectName(string name, GatePosition expected)
        {
            Assert.AreEqual(expected, Make(name).Position);
        }

        [Test]
        public void Position_FollowsARename()
        {
            var gate = Make("left1");
            go.name = "right1";
            Assert.AreEqual(GatePosition.Right, gate.Position);
        }

        [Test]
        public void Configure_SetsTargetSceneEntryGateAndOffset()
        {
            var gate = Make("right1");
            gate.Configure("Tut_01b", "left1", new Vector2(-1f, 0f));
            Assert.AreEqual("Tut_01b", gate.TargetScene);
            Assert.AreEqual("left1", gate.EntryGate);
            Assert.AreEqual(new Vector2(-1f, 0f), gate.EntryOffset);
            Assert.IsTrue(gate.HasTarget);
        }

        [Test]
        public void HasTarget_NeedsBothSceneAndGate()
        {
            var gate = Make("right1");
            Assert.IsFalse(gate.HasTarget);
            gate.Configure("Tut_01b", "", Vector2.zero);
            Assert.IsFalse(gate.HasTarget);
            gate.Configure("", "left1", Vector2.zero);
            Assert.IsFalse(gate.HasTarget);
        }

        [Test]
        public void BuildRequest_CarriesTheLeaveDirectionAndEntryData()
        {
            var gate = Make("right2");
            gate.Configure("Tut_01b", "left2", Vector2.zero);
            var request = gate.BuildRequest();
            Assert.AreEqual("Tut_01b", request.TargetScene);
            Assert.AreEqual("left2", request.EntryGate);
            Assert.AreEqual(GatePosition.Right, request.LeaveDirection);
        }

        [Test]
        public void Touch_OutwardHeroWhilePlaying_SendsOneRequest()
        {
            var gate = Make("right1");
            gate.Configure("Tut_01b", "left1", Vector2.zero);
            var handler = new FakeHandler { State = GameState.Playing };

            var decision = gate.Touch(handler, OutwardRightHero());

            Assert.AreEqual(TransitionDecision.Leave, decision);
            Assert.AreEqual(1, handler.Requests.Count);
            Assert.AreEqual("left1", handler.Requests[0].EntryGate);
        }

        [Test]
        public void Touch_WrongWayHero_SendsNothingAndReportsPushBack()
        {
            var gate = Make("right1");
            gate.Configure("Tut_01b", "left1", Vector2.zero);
            var handler = new FakeHandler { State = GameState.Playing };

            var decision = gate.Touch(handler, new HeroTransitionState { FacingRight = false, InPosition = true });

            Assert.AreEqual(TransitionDecision.PushBack, decision);
            Assert.AreEqual(0, handler.Requests.Count);
        }

        [Test]
        public void Touch_WhenNotPlaying_IsIgnored()
        {
            var gate = Make("right1");
            gate.Configure("Tut_01b", "left1", Vector2.zero);
            var handler = new FakeHandler { State = GameState.Paused };
            Assert.AreEqual(TransitionDecision.Ignore, gate.Touch(handler, OutwardRightHero()));
            Assert.AreEqual(0, handler.Requests.Count);
        }

        [Test]
        public void Touch_WhileHandlerIsRunning_IsIgnored()
        {
            var gate = Make("right1");
            gate.Configure("Tut_01b", "left1", Vector2.zero);
            var handler = new FakeHandler { State = GameState.Playing, Running = true };
            Assert.AreEqual(TransitionDecision.Ignore, gate.Touch(handler, OutwardRightHero()));
            Assert.AreEqual(0, handler.Requests.Count);
        }

        [Test]
        public void Touch_TwiceOnTheSameGate_SendsOnlyOneRequest()
        {
            // OnTriggerStay keeps firing every physics step while the hero overlaps; the original guards
            // this with its `activated` flag. This covers a handler that has not flipped to running yet.
            var gate = Make("right1");
            gate.Configure("Tut_01b", "left1", Vector2.zero);
            var handler = new FakeHandler { State = GameState.Playing };

            gate.Touch(handler, OutwardRightHero());
            var second = gate.Touch(handler, OutwardRightHero());

            Assert.AreEqual(TransitionDecision.Ignore, second);
            Assert.AreEqual(1, handler.Requests.Count);
        }

        [Test]
        public void ResetActivation_AllowsTheGateToFireAgain()
        {
            var gate = Make("right1");
            gate.Configure("Tut_01b", "left1", Vector2.zero);
            var handler = new FakeHandler { State = GameState.Playing };
            gate.Touch(handler, OutwardRightHero());
            gate.ResetActivation();
            gate.Touch(handler, OutwardRightHero());
            Assert.AreEqual(2, handler.Requests.Count);
        }

        class FakeHandler : ITransitionRequestHandler
        {
            public GameState State { get; set; }
            public bool Running;
            public bool IsTransitionRunning => Running;
            public readonly List<TransitionRequest> Requests = new List<TransitionRequest>();
            public void RequestTransition(TransitionRequest request) => Requests.Add(request);
        }
    }
}
