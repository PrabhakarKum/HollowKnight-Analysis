using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // Code-built ground (BoxCollider2D floors and walls), Script-mode physics, explicit Tick + Simulate pairs
    // (the walker's own FixedUpdate does not run under ManualStepping), like HazardZoneTests.
    public class EnemyWalkerTests
    {
        const float dt = 0.02f;
        readonly List<Object> spawned = new List<Object>();
        SimulationMode2D savedMode;
        string stubsPath;
        EnemyWalker walker;
        Rigidbody2D body;

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            stubsPath = Path.Combine(Path.GetTempPath(), "enemy-walker-tests-stubs-" + System.Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in spawned) if (o != null) Object.DestroyImmediate(o);
            spawned.Clear();
            walker = null; body = null;
            Physics2D.simulationMode = savedMode;
            Stubs.StubsDocPath = null;
            Stubs.ResetForTests();
            if (File.Exists(stubsPath)) File.Delete(stubsPath);
        }

        T Track<T>(T o) where T : Object { spawned.Add(o); return o; }

        // A floor whose top surface is y = 0, spanning [left, right].
        void Floor(float left, float right)
        {
            var g = Track(new GameObject("Floor"));
            g.AddComponent<BoxCollider2D>().size = new Vector2(right - left, 1f);
            g.transform.position = new Vector3((left + right) * 0.5f, -0.5f, 0f);
        }

        void Wall(float x)
        {
            var g = Track(new GameObject("Wall"));
            g.AddComponent<BoxCollider2D>().size = new Vector2(1f, 8f);
            g.transform.position = new Vector3(x + 0.5f, 4f, 0f); // its left face is at x
        }

        void SpawnEnemy(float x, int facing = 1)
        {
            var go = Track(EnemyPlaceholderView.Build(null));
            go.transform.position = new Vector3(x, 0.42f, 0f);
            body = go.AddComponent<Rigidbody2D>();
            walker = go.AddComponent<EnemyWalker>();
            walker.ManualStepping = true;
            walker.Configure(EnemyWalkParams.Default, ~0, facing);
            Physics2D.SyncTransforms();
        }

        void Step(int count)
        {
            for (int i = 0; i < count; i++)
            {
                walker.Tick(dt);
                Physics2D.Simulate(dt);
            }
        }

        static int Steps(float seconds) => Mathf.RoundToInt(seconds / dt);
        float X => walker.transform.position.x;
        float Y => walker.transform.position.y;

        [Test]
        public void WalksRight_AtAboutThreeUnitsPerSecond()
        {
            Floor(-50f, 50f);
            SpawnEnemy(0f);
            Step(Steps(0.2f)); // settle onto the floor
            float x0 = X;
            Step(Steps(1f));
            Assert.AreEqual(3f, X - x0, 0.3f);
            Assert.AreEqual(1, walker.Facing);
        }

        [Test]
        public void TurnsAtLedge_InsteadOfFalling()
        {
            Floor(-5f, 5f);
            SpawnEnemy(0f);
            Step(Steps(3f));
            Assert.AreEqual(-1, walker.Facing, "turned at the right ledge");
            Assert.Less(X, 5f);
            Assert.Greater(Y, 0.2f, "still on the platform");
        }

        [Test]
        public void TurnsAtWall()
        {
            Floor(-50f, 50f);
            Wall(4f);
            SpawnEnemy(0f);
            float maxFront = float.MinValue;
            for (int i = 0; i < Steps(3f); i++)
            {
                Step(1);
                maxFront = Mathf.Max(maxFront, X + 0.7f);
            }
            Assert.AreEqual(-1, walker.Facing);
            Assert.LessOrEqual(maxFront, 4.05f, "never pushed into the wall");
        }

        [Test]
        public void WalksBackAndForth_FromEndToEnd_NeverLeavingThePlatform()
        {
            Floor(-5f, 5f);
            SpawnEnemy(0f);
            int turns = 0;
            int last = walker.Facing;
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < Steps(12f); i++)
            {
                Step(1);
                Assert.Greater(Y, 0.2f, "fell off at step " + i);
                minX = Mathf.Min(minX, X); maxX = Mathf.Max(maxX, X);
                if (walker.Facing != last) { turns++; last = walker.Facing; }
            }
            Assert.GreaterOrEqual(turns, 3);
            Assert.Greater(maxX, 3f, "reached the right end");
            Assert.Less(minX, -3f, "reached the left end");
            Assert.Less(maxX, 5f);
            Assert.Greater(minX, -5f);
        }

        [Test]
        public void FlipsTheTransformScale_OnTurn()
        {
            Floor(-5f, 5f);
            SpawnEnemy(0f, 1);
            // The art faces left, so facing right means a negative x scale.
            Assert.Less(walker.transform.localScale.x, 0f);
            Step(Steps(3f));
            Assert.AreEqual(-1, walker.Facing);
            Assert.Greater(walker.transform.localScale.x, 0f);
        }

        [Test]
        public void Shove_MovesItFast_ThenItResumesWalking()
        {
            Floor(-50f, 50f);
            SpawnEnemy(0f);
            Step(Steps(0.2f));
            float x0 = X;
            walker.Shove(15f, 0.15f);
            Step(Steps(0.16f));
            Assert.Greater(X - x0, 2f, "shove carries it about 2.25 units on top of the walk");
            Step(Steps(0.1f));
            float x1 = X;
            Step(Steps(0.5f));
            Assert.AreEqual(1.5f, X - x1, 0.3f, "back to walking at 3 u/s");
            Assert.AreEqual(1, walker.Facing);
            Assert.AreEqual(EnemyState.Walking, walker.State);
        }

        [Test]
        public void Stop_HaltsIt()
        {
            Floor(-50f, 50f);
            SpawnEnemy(0f);
            Step(Steps(0.3f));
            walker.Stop();
            Step(2);
            float x = X;
            Step(Steps(1f));
            Assert.AreEqual(x, X, 0.05f);
            Assert.AreEqual(EnemyState.Dead, walker.State);
        }

        [Test]
        public void Airborne_DoesNotTurn_AndFallsStraightToTheFloor()
        {
            Floor(-50f, 50f);
            SpawnEnemy(0f);
            walker.transform.position = new Vector3(0f, 6f, 0f);
            Physics2D.SyncTransforms();
            int before = walker.Facing;
            Step(Steps(0.3f)); // still falling
            Assert.AreEqual(before, walker.Facing);
        }
    }
}
