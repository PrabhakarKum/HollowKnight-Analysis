using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hollowknight.Silksong.Tests
{
    // Same deterministic setup as the dash and wall tests: Script-mode physics, explicit StepFixed + Simulate
    // pairs, input edges cleared after each step. The world has a floor (top at y = 0) and, for the wall
    // tests, a tall wall to the right of the hero whose left face is at x = WallFaceX. Targets are trigger
    // boxes, like the prototype's, so the hero passes through them.
    public class HeroAttackTests
    {
        const float HeroHalfWidth = 0.25f;
        const float HeroHalfHeight = 0.5f;
        const float WallFaceX = 1f;

        // A hittable that is not a dummy: counts hits, can refuse them.
        class PlainHittable : MonoBehaviour, IHittable
        {
            public int Hits;
            public bool Lands = true;

            public bool TakeHit(HitInstance hit)
            {
                if (!Lands) return false;
                Hits++;
                return true;
            }
        }

        readonly List<UnityEngine.Object> spawned = new List<UnityEngine.Object>();
        SimulationMode2D savedMode;
        float dt = 0.02f;
        HeroConfig config;
        FakeHeroInput input;
        HeroController hero;
        Collider2D groundCollider;
        string stubsPath;
        int attackedCount;

        static readonly Vector2 Right = new Vector2(1f, 0f);
        static readonly Vector2 Left = new Vector2(-1f, 0f);
        static readonly Vector2 Up = new Vector2(0f, 1f);
        static readonly Vector2 Down = new Vector2(0f, -1f);

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            dt = 0.02f;
            config = ScriptableObject.CreateInstance<HeroConfig>();
            stubsPath = Path.Combine(Path.GetTempPath(), "attack-tests-stubs-" + System.Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                ClearWorld();
                if (config != null) UnityEngine.Object.DestroyImmediate(config);
            }
            finally
            {
                Physics2D.simulationMode = savedMode;
                Stubs.StubsDocPath = null;
                if (File.Exists(stubsPath)) File.Delete(stubsPath);
            }
        }

        void ClearWorld()
        {
            foreach (var o in spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            spawned.Clear();
            hero = null;
            groundCollider = null;
        }

        // Floor, optionally a wall to the right, and the hero standing startHeight above the floor (touching the
        // wall face when withWall).
        void BuildWorld(float startHeight, bool withWall = false)
        {
            ClearWorld();
            input = new FakeHeroInput();
            attackedCount = 0;

            var ground = new GameObject("Ground");
            spawned.Add(ground);
            var gbox = ground.AddComponent<BoxCollider2D>();
            gbox.size = new Vector2(400f, 1f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            groundCollider = gbox;

            float x = 0f;
            if (withWall)
            {
                var wall = new GameObject("Wall");
                spawned.Add(wall);
                var wbox = wall.AddComponent<BoxCollider2D>();
                wbox.size = new Vector2(1f, 400f);
                wall.transform.position = new Vector3(WallFaceX + 0.5f, 190f, 0f);
                x = WallFaceX - HeroHalfWidth - 0.01f;
            }

            var go = new GameObject("Hero");
            spawned.Add(go);
            go.transform.position = new Vector3(x, HeroHalfHeight + 0.01f + startHeight, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(HeroHalfWidth * 2f, HeroHalfHeight * 2f);
            hero = go.AddComponent<HeroController>();
            hero.ManualStepping = true;
            hero.Config = config;
            hero.Input = input;
            hero.Attack.Attacked += _ => attackedCount++;
            Physics2D.SyncTransforms();
        }

        void Step(int count)
        {
            for (int i = 0; i < count; i++)
            {
                hero.StepFixed(dt);
                Physics2D.Simulate(dt);
                input.EndFixedStep(dt);
            }
        }

        int Steps(float seconds) => Mathf.RoundToInt(seconds / dt);

        void Settle()
        {
            Step(Steps(0.2f));
            Assert.IsTrue(hero.IsGrounded, "hero should be standing on the ground after settling");
        }

        void PressAttack()
        {
            input.AttackButton.Release();
            input.AttackButton.Press();
        }

        // Presses attack and runs one step (the step that starts the swing).
        void Swing(Vector2 move)
        {
            input.Move = move;
            PressAttack();
            Step(1);
        }

        GameObject MakeTarget(Vector2 center, Vector2 size, params System.Type[] components)
        {
            var go = new GameObject("Target");
            spawned.Add(go);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = size;
            box.isTrigger = true;
            foreach (var t in components) go.AddComponent(t);
            Physics2D.SyncTransforms();
            return go;
        }

        TrainingDummy MakeDummy(Vector2 center) =>
            MakeTarget(center, new Vector2(1f, 1f), typeof(HealthManager), typeof(TrainingDummy)).GetComponent<TrainingDummy>();

        // A dummy that overlaps the downward hitbox of a hero in the air (the box is centred on the hero).
        TrainingDummy MakeDummyUnder(Transform t) => MakeDummy((Vector2)t.position + new Vector2(0f, -0.2f));

        void SetState(HeroState state)
        {
            var setter = typeof(HeroController).GetProperty("State").GetSetMethod(true);
            setter.Invoke(hero, new object[] { state });
        }

        Bounds HitboxBounds()
        {
            Physics2D.SyncTransforms();
            return hero.Attack.Hitbox.bounds;
        }

        // ------------------------------------------------------------------ the plan's six tests

        [Test]
        public void Attack_FourDirections_PlaceHitboxOnMatchingSide()
        {
            BuildWorld(0f);
            Settle();
            Vector2 pos;

            Swing(Left);
            input.Move = Vector2.zero;   // stop running so the box is compared with where the hero is
            pos = hero.transform.position;
            Assert.IsTrue(hero.Attack.HitboxActive);
            Assert.AreEqual(AttackDirection.Normal, hero.Attack.CurrentDirection);
            Assert.IsFalse(hero.FacingRight, "turns toward the held direction");
            Assert.Less(HitboxBounds().center.x, pos.x - 0.3f, "left swing box is on the left");

            Step(Steps(0.5f));
            Assert.IsFalse(hero.Attack.HitboxActive);

            Swing(Right);
            input.Move = Vector2.zero;
            pos = hero.transform.position;
            Assert.IsTrue(hero.FacingRight);
            Assert.Greater(HitboxBounds().center.x, pos.x + 0.3f, "right swing box is on the right");

            Step(Steps(0.5f));
            Swing(Up);
            input.Move = Vector2.zero;
            pos = hero.transform.position;
            Assert.AreEqual(AttackDirection.Up, hero.Attack.CurrentDirection);
            Assert.Greater(HitboxBounds().center.y, pos.y + 0.5f, "up swing box is above");

            // Down only counts in the air.
            BuildWorld(5f);
            pos = hero.transform.position;
            Swing(Down);
            Assert.AreEqual(AttackDirection.Down, hero.Attack.CurrentDirection);
            Assert.AreEqual(config.AttackDownSize.x, HitboxBounds().size.x, 0.01f);
            Assert.AreEqual(config.AttackDownSize.y, HitboxBounds().size.y, 0.01f);
            Assert.AreEqual(pos.y + config.AttackDownOffset.y, HitboxBounds().center.y, 0.1f);
        }

        [Test]
        public void Attack_OneSwing_HitsDummyExactlyOnce()
        {
            BuildWorld(0f);
            Settle();
            var dummy = MakeDummy(new Vector2(1.0f, 0.5f));

            Swing(Vector2.zero);
            // The box stays live and overlapping for the whole swing; the dummy must still only be hit once.
            Step(Steps(config.AttackActiveSeconds) + 3);
            Assert.AreEqual(1, dummy.HitCount);

            // A new swing after the cooldown is a new swing: it hits again.
            Step(Steps(config.AttackCooldownSeconds));
            Swing(Vector2.zero);
            Step(Steps(0.1f));
            Assert.AreEqual(2, dummy.HitCount);
        }

        [Test]
        public void Attack_DuringCooldown_IsIgnored()
        {
            BuildWorld(0f);
            Settle();
            Swing(Vector2.zero);
            Assert.AreEqual(1, attackedCount);

            // The swing is over after AttackActiveSeconds but the cooldown runs a little longer.
            Step(Steps(config.AttackActiveSeconds));
            Assert.IsFalse(hero.Attack.IsAttacking, "test setup: swing finished");
            Assert.Greater(hero.Attack.CooldownRemaining, 0.01f, "test setup: still cooling down");
            Swing(Vector2.zero);
            Assert.AreEqual(1, attackedCount, "press during the cooldown does nothing");
            Assert.IsFalse(hero.Attack.IsAttacking);
            Assert.IsFalse(hero.Attack.HitboxActive);

            Step(Steps(0.1f));
            Swing(Vector2.zero);
            Assert.AreEqual(2, attackedCount, "accepted once the cooldown is over");
        }

        [Test]
        public void DownAttack_HittingDummy_SetsVelocityYToPogoBounceSpeed()
        {
            BuildWorld(20f);
            Step(3);
            var dummy = MakeDummyUnder(hero.transform);
            Assert.Less(hero.Velocity.y, 0f, "test setup: falling");

            Swing(Down);
            Assert.AreEqual(1, dummy.HitCount);
            Assert.AreEqual(config.PogoBounceSpeed, hero.Velocity.y, 0.001f);
            Assert.IsTrue(hero.Attack.IsBouncing);
            Assert.AreEqual(HeroState.Rising, hero.State);
        }

        [Test]
        public void DownAttack_HittingPlainGround_DoesNotPogo()
        {
            // A tall box so it really reaches the floor; the assertion below proves the overlap happened.
            config.AttackDownSize = new Vector2(1.4f, 4f);
            BuildWorld(1f);
            Step(2);
            int hits = 0;
            hero.Attack.HitLanded += (_, __) => hits++;

            Swing(Down);
            Assert.AreEqual(AttackDirection.Down, hero.Attack.CurrentDirection);
            Assert.IsTrue(HitboxBounds().Intersects(groundCollider.bounds), "test setup: the box overlaps the floor");
            Step(Steps(0.2f));
            Assert.AreEqual(0, hits);
            Assert.IsFalse(hero.Attack.IsBouncing);
            Assert.Less(hero.Velocity.y, config.PogoBounceSpeed - 1f, "no bounce speed");
        }

        [Test]
        public void DownAttack_HeldWhileStandingOnGround_IsAnOrdinarySwing()
        {
            BuildWorld(0f);
            Settle();
            Swing(Down);
            Assert.AreEqual(AttackDirection.Normal, hero.Attack.CurrentDirection);
            Assert.IsFalse(hero.Attack.IsBouncing);
        }

        [Test]
        public void TrainingDummy_Hit_IncrementsHitCountAndRaisesHit()
        {
            var go = new GameObject("Dummy");
            spawned.Add(go);
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<HealthManager>();
            var dummy = go.AddComponent<TrainingDummy>();
            var health = go.GetComponent<HealthManager>();
            var seen = new List<HitInstance>();
            dummy.Hit += seen.Add;
            int healthRaised = 0;
            health.Hit += _ => healthRaised++;

            var hit = new HitInstance { Damage = 2, Direction = AttackDirection.Up, Source = go };
            Assert.IsTrue(((IHittable)dummy.GetComponent<HealthManager>()).TakeHit(hit));
            Assert.IsTrue(health.TakeHit(hit));

            Assert.AreEqual(2, dummy.HitCount);
            Assert.AreEqual(2, seen.Count);
            Assert.AreEqual(2, healthRaised);
            Assert.AreEqual(2, seen[0].Damage);
            Assert.AreEqual(AttackDirection.Up, seen[0].Direction);
            Assert.AreEqual(health.StartHealth, health.Health, "a dummy never runs out of health");
            Assert.IsFalse(health.IsDead);
        }

        // ------------------------------------------------------------------ extras

        [Test]
        public void HealthManager_DamageToZero_RaisesDiedOnceAndRefusesFurtherHits()
        {
            var go = new GameObject("Target");
            spawned.Add(go);
            var health = go.AddComponent<HealthManager>();
            health.SetStartHealth(2);
            int died = 0;
            health.Died += () => died++;
            var hit = new HitInstance { Damage = 1, Direction = AttackDirection.Normal, Source = go };

            Assert.IsTrue(health.TakeHit(hit));
            Assert.AreEqual(1, health.Health);
            Assert.IsTrue(health.TakeHit(hit));
            Assert.AreEqual(1, died);
            Assert.IsFalse(health.TakeHit(hit), "a dead target refuses hits");
            Assert.AreEqual(1, died);
        }

        [Test]
        public void Attack_CooldownLength_IsTheSameAtFiftyAndOneHundredHertz()
        {
            float at50 = TimeToSecondSwing(0.02f);
            float at100 = TimeToSecondSwing(0.01f);
            Assert.GreaterOrEqual(at50, config.AttackCooldownSeconds - 0.011f);
            Assert.GreaterOrEqual(at100, config.AttackCooldownSeconds - 0.011f);
            Assert.AreEqual(at50, at100, 0.021f, "differs by at most one 50 Hz step");
        }

        float TimeToSecondSwing(float stepSeconds)
        {
            dt = stepSeconds;
            BuildWorld(0f);
            Settle();
            int steps = 0;
            // Mash attack every step; the second swing starts as soon as the cooldown allows.
            while (attackedCount < 2 && steps < 1000)
            {
                PressAttack();
                Step(1);
                steps++;
            }
            Assert.AreEqual(2, attackedCount, "second swing never started");
            return (steps - 1) * dt;
        }

        [Test]
        public void Attack_HitboxLiveTime_IsTheSameAtFiftyAndOneHundredHertz()
        {
            float at50 = LiveSeconds(0.02f);
            float at100 = LiveSeconds(0.01f);
            Assert.AreEqual(config.AttackActiveSeconds, at100, 0.011f);
            Assert.AreEqual(at50, at100, 0.021f);
        }

        float LiveSeconds(float stepSeconds)
        {
            dt = stepSeconds;
            BuildWorld(0f);
            Settle();
            int live = 0;
            PressAttack();
            for (int i = 0; i < 1000; i++)
            {
                Step(1);
                if (!hero.Attack.HitboxActive) break;
                live++;
            }
            return live * dt;
        }

        [TestCase(HeroState.Hurt)]
        [TestCase(HeroState.Dead)]
        public void Attack_SwingInProgress_EndsWhenHeroIsHurtOrDead(HeroState state)
        {
            BuildWorld(0f);
            Settle();
            var dummy = MakeDummy(new Vector2(1.0f, 0.5f));
            Swing(Vector2.zero);
            Assert.IsTrue(hero.Attack.HitboxActive);

            SetState(state);
            Step(1);
            Assert.IsFalse(hero.Attack.IsAttacking);
            Assert.IsFalse(hero.Attack.HitboxActive, "hitbox is switched off");

            // And nothing new can start while hurt or dead.
            Step(Steps(config.AttackCooldownSeconds));
            SetState(state);
            int before = attackedCount;
            Swing(Vector2.zero);
            Assert.AreEqual(before, attackedCount);
            Assert.IsFalse(hero.Attack.HitboxActive);
            Assert.AreEqual(1, dummy.HitCount, "no extra hit after the swing was cancelled");
        }

        [Test]
        public void Attack_DisablingTheHero_ResetsSwingCooldownAndBounce()
        {
            BuildWorld(20f);
            Step(3);
            MakeDummyUnder(hero.transform);
            Swing(Down);
            Assert.IsTrue(hero.Attack.IsAttacking);
            Assert.IsTrue(hero.Attack.IsBouncing);

            hero.Attack.enabled = false;
            hero.Attack.enabled = true;
            Assert.IsFalse(hero.Attack.IsAttacking);
            Assert.IsFalse(hero.Attack.HitboxActive);
            Assert.IsFalse(hero.Attack.IsBouncing);
            Assert.AreEqual(0f, hero.Attack.CooldownRemaining);
        }

        [Test]
        public void Pogo_OffAnyHittable_NotJustDummies()
        {
            BuildWorld(20f);
            Step(3);
            var go = MakeTarget((Vector2)hero.transform.position + new Vector2(0f, -0.2f), Vector2.one, typeof(PlainHittable));
            var target = go.GetComponent<PlainHittable>();

            Swing(Down);
            Assert.AreEqual(1, target.Hits);
            Assert.AreEqual(config.PogoBounceSpeed, hero.Velocity.y, 0.001f);
        }

        [Test]
        public void Pogo_HitThatDoesNotLand_DoesNotBounce()
        {
            BuildWorld(20f);
            Step(3);
            var go = MakeTarget((Vector2)hero.transform.position + new Vector2(0f, -0.2f), Vector2.one, typeof(PlainHittable));
            go.GetComponent<PlainHittable>().Lands = false;

            Swing(Down);
            Assert.IsFalse(hero.Attack.IsBouncing);
            Assert.Less(hero.Velocity.y, 0f);
        }

        [Test]
        public void Pogo_OffTargetWithActiveNonBouncer_TakesTheHitButDoesNotBounce()
        {
            BuildWorld(20f);
            Step(3);
            var go = MakeTarget((Vector2)hero.transform.position + new Vector2(0f, -0.2f), Vector2.one, typeof(PlainHittable), typeof(NonBouncer));
            var target = go.GetComponent<PlainHittable>();

            Swing(Down);
            Assert.AreEqual(1, target.Hits);
            Assert.IsFalse(hero.Attack.IsBouncing);
            Assert.Less(hero.Velocity.y, 0f);
        }

        [Test]
        public void Pogo_KeepsRisingForBounceTime_ThenGravityTakesOver()
        {
            BuildWorld(20f);
            Step(3);
            MakeDummyUnder(hero.transform);
            Swing(Down);
            Step(Steps(0.2f));
            Assert.AreEqual(config.PogoBounceSpeed, hero.Velocity.y, 0.001f, "still forced to the bounce speed");
            Step(Steps(0.25f));
            Assert.IsFalse(hero.Attack.IsBouncing);
            Assert.Less(hero.Velocity.y, config.PogoBounceSpeed - 1f, "bounce is over, gravity pulls it down");
        }

        [Test]
        public void Pogo_GivesBackTheAirDash_ButNotTheCooldown()
        {
            BuildWorld(30f);
            Step(2);
            input.DashButton.Press();
            Step(1);
            input.DashButton.Release();
            Step(Steps(config.DashSeconds) + 3);
            Assert.IsFalse(hero.Dash.IsDashing, "test setup: dash over");
            Assert.IsFalse(hero.Dash.CanAirDash, "test setup: air dash spent");

            MakeDummyUnder(hero.transform);
            Swing(Down);
            Assert.IsTrue(hero.Dash.CanAirDash, "pogo refreshed the air dash");
        }

        [Test]
        public void Pogo_EndsWhenHeroIsHurt()
        {
            BuildWorld(20f);
            Step(3);
            MakeDummyUnder(hero.transform);
            Swing(Down);
            Assert.IsTrue(hero.Attack.IsBouncing);

            SetState(HeroState.Hurt);
            Step(1);
            Assert.IsFalse(hero.Attack.IsBouncing);
        }

        [Test]
        public void Attack_SidewaysHit_OnTargetWithoutFeedbackHook_DoesNotMoveHero()
        {
            // The recoil is built (HeroHitFeelTests); a plain dummy has no IHitFeedbackTarget, so it gets none.
            BuildWorld(0f);
            Settle();
            MakeDummy(new Vector2(1.0f, 0.5f));
            Swing(Vector2.zero);
            CollectionAssert.DoesNotContain(new List<string>(Stubs.Reported), "Hero nail horizontal recoil");
            Assert.AreEqual(0f, hero.Velocity.x, 0.01f);
        }

        [Test]
        public void Attack_RefusedPress_LogsBufferStubOnce()
        {
            BuildWorld(0f);
            Settle();
            Swing(Vector2.zero);
            Swing(Vector2.zero);
            Swing(Vector2.zero);
            int count = 0;
            foreach (var f in Stubs.Reported) if (f == "Hero attack buffer") count++;
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Attack_WhileDashing_IsRefused()
        {
            BuildWorld(0f);
            Settle();
            input.DashButton.Press();
            Step(1);
            Assert.AreEqual(HeroState.Dashing, hero.State);
            Swing(Vector2.zero);
            Assert.AreEqual(0, attackedCount);
        }

        // ------------------------------------------------------------------ on a wall

        void ClingToWall()
        {
            BuildWorld(60f, withWall: true);
            input.Move = Right;
            Step(Steps(0.3f));
            Assert.AreEqual(HeroState.WallClinging, hero.State, "test setup: hero should be clinging");
            Step(Steps(0.2f));
            Assert.AreEqual(-config.WallSlideSpeed, hero.Velocity.y, 0.05f, "test setup: sliding at the cap");
        }

        [Test]
        public void WallCling_SidewaysSwing_HitsAwayFromWall_KeepsClingAndSlowsTheSlideOnce()
        {
            ClingToWall();
            Vector2 pos = hero.transform.position;

            Swing(Right);   // still pressing toward the wall
            Assert.IsTrue(hero.Attack.HitboxActive);
            Assert.AreEqual(AttackDirection.Normal, hero.Attack.CurrentDirection);
            Assert.Less(HitboxBounds().center.x, pos.x - 0.3f, "the box is on the open side, away from the wall");
            Assert.IsTrue(hero.Wall.IsClinging, "a sideways swing keeps the cling");
            Assert.IsTrue(hero.FacingRight, "still faces the wall");
            Assert.Greater(hero.Velocity.y, -config.WallSlideSpeed + 1f, "the slide was slowed by the swing");
            Assert.Less(hero.Velocity.y, 0f);
        }

        [Test]
        public void WallCling_UpSwing_StepsOffTheWallAndLetsGo()
        {
            ClingToWall();
            float xBefore = hero.transform.position.x;

            Swing(new Vector2(1f, 1f));
            Assert.AreEqual(AttackDirection.Up, hero.Attack.CurrentDirection);
            Assert.IsFalse(hero.Wall.IsClinging);
            Assert.Less(hero.transform.position.x, xBefore - 0.5f, "stepped away from the wall");
        }

        [Test]
        public void WallCling_DownSwing_LetsGoAndDoesNotRelatchUntilTheSwingEnds()
        {
            ClingToWall();

            Swing(new Vector2(1f, -1f));   // pressing into the wall the whole time
            Assert.AreEqual(AttackDirection.Down, hero.Attack.CurrentDirection);
            Assert.IsFalse(hero.Wall.IsClinging);
            Step(3);
            Assert.IsFalse(hero.Wall.IsClinging, "does not grab the wall again mid-swing");
            Assert.IsFalse(hero.IsGrounded);

            Step(Steps(config.AttackActiveSeconds));
            Assert.IsFalse(hero.Attack.IsAttacking);
            Step(3);
            Assert.IsTrue(hero.Wall.IsClinging, "clings again once the swing is over");
        }
    }
}
