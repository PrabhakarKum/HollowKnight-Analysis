using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace Hollowknight.Silksong.Tests
{
    // Task 11. The camera test runs now with a hero built from code. The three scene and asset tests are written
    // against what Task 11b produces (the imported hero animations and Assets/Scenes/SilksongHero.unity) and are
    // ignored until then; their assertions are final, so 11b only has to remove the [Ignore] lines.
    public class RoomSceneTests
    {
        // 11b builds the real room scene here (HeroTestSceneBuilder.DefaultScenePath is the same path).
        public const string ScenePath = "Assets/Scenes/SilksongHero.unity";

        readonly List<Object> spawned = new List<Object>();
        SimulationMode2D savedMode;
        string stubsPath;

        [SetUp]
        public void SetUp()
        {
            savedMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            stubsPath = Path.Combine(Path.GetTempPath(), "room-tests-stubs-" + System.Guid.NewGuid() + ".md");
            Stubs.StubsDocPath = stubsPath;
            Stubs.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in spawned) if (o != null) Object.DestroyImmediate(o);
            spawned.Clear();
            Physics2D.simulationMode = savedMode;
            Stubs.StubsDocPath = null;
            if (File.Exists(stubsPath)) File.Delete(stubsPath);
        }

        T Track<T>(T o) where T : Object { spawned.Add(o); return o; }

        // ---------- 1. camera (runs now) ----------

        [Test]
        public void Camera_FollowsHero_AndStaysInsideRoomBounds()
        {
            const float dt = 0.02f;
            // The real project config, so the hero here runs at the same 8.25 u/s as in the scene.
            HeroConfig config = null;
#if UNITY_EDITOR
            config = HeroConfigConsistency.LoadProjectAsset();
#endif
            if (config == null) config = Track(ScriptableObject.CreateInstance<HeroConfig>());

            var ground = Track(new GameObject("Ground"));
            ground.AddComponent<BoxCollider2D>().size = new Vector2(400f, 1f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);

            var heroGo = Track(new GameObject("Hero"));
            heroGo.transform.position = new Vector3(5f, 0.51f, 0f);
            var box = heroGo.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.5f, 1f);
            var hero = heroGo.AddComponent<HeroController>();
            var input = new FakeHeroInput();
            hero.ManualStepping = true;
            hero.Config = config;
            hero.Input = input;
            Physics2D.SyncTransforms();

            // Room: x 0..60, y -10..20, so the camera's centre may sit in x 14.6..45.4 and y -1.7..11.7.
            var room = RoomBounds.Create(new Rect(0f, -10f, 60f, 30f));
            Track(room.gameObject);
            var camGo = Track(new GameObject("Main Camera"));
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = SilksongUnits.CameraOrthographicSize;
            cam.aspect = 16f / 9f;
            var gameCam = camGo.AddComponent<GameCamera>();
            gameCam.ManualStepping = true;
            gameCam.Bounds = room;
            gameCam.Target = heroGo.transform;
            Assert.AreSame(room, gameCam.Bounds);
            Assert.AreSame(heroGo.transform, gameCam.Target);

            const float minX = 14.6f, maxX = 60f - 14.6f, minY = -10f + 8.3f, maxY = 20f - 8.3f;
            void Step()
            {
                hero.StepFixed(dt);
                Physics2D.Simulate(dt);
                input.EndFixedStep(dt);
                gameCam.Step(dt);
                var p = camGo.transform.position;
                Assert.GreaterOrEqual(p.x, minX - 1e-3f, "camera left of its room limit");
                Assert.LessOrEqual(p.x, maxX + 1e-3f, "camera right of its room limit");
                Assert.GreaterOrEqual(p.y, minY - 1e-3f, "camera below its room limit");
                Assert.LessOrEqual(p.y, maxY + 1e-3f, "camera above its room limit");
                Assert.AreEqual(-10f, p.z, 1e-6f, "camera keeps its depth");
            }

            // Settle on the floor; the first step snaps the camera onto the hero (clamped into the room).
            for (int i = 0; i < 15; i++) Step();
            Assert.IsTrue(hero.IsGrounded, "test setup: hero should stand on the floor");
            Assert.AreEqual(minX, camGo.transform.position.x, 0.5f, "hero starts near the left edge: camera is held at the room limit");
            Assert.AreEqual(heroGo.transform.position.y, camGo.transform.position.y, 0.2f, "hero height is inside the camera range: camera is level with the hero");

            // Run right for 8 s (about 66 units), far past the right edge of the camera range.
            input.Move = new Vector2(1f, 0f);
            float startHeroX = heroGo.transform.position.x;
            float maxLag = 0f;
            for (int i = 0; i < 400; i++)
            {
                Step();
                float heroX = heroGo.transform.position.x, camX = camGo.transform.position.x;
                if (heroX > minX + 4f && heroX < maxX - 4f) maxLag = Mathf.Max(maxLag, Mathf.Abs(heroX - camX));
            }
            float finalHeroX = heroGo.transform.position.x;
            Assert.Greater(finalHeroX - startHeroX, 30f, "test setup: hero should have run a long way");
            Assert.Greater(finalHeroX, maxX + 5f, "hero ran past the camera's range, so the clamp was exercised");
            Assert.AreEqual(maxX, camGo.transform.position.x, 0.05f, "camera stopped at the right room limit while the hero ran on");
            Assert.Less(maxLag, 3f, "while inside the range the camera stays with the hero");
            Assert.LessOrEqual(gameCam.LookAheadOffset, 1f, "look-ahead never exceeds one unit (it is pulled in at the room edge)");
        }

        // ---------- 2. scene (11b) ----------

        [UnityTest]
        [Ignore("Task 11b: needs imported assets and scene")]
        public IEnumerator SilksongHeroScene_Loads_WithNoMissingScripts()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = SceneManager.GetActiveScene();
            Assert.AreEqual(ScenePath, scene.path);

            int missing = 0;
            var offenders = new List<string>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (n == 0) continue;
                    missing += n;
                    offenders.Add(t.name + " (" + n + ")");
                }
            }
            Assert.AreEqual(0, missing, "Missing scripts on: " + string.Join(", ", offenders));

            // The hero in the scene is configured only by the project HeroConfig asset.
            HeroConfigConsistency.AssertSceneUsesProjectConfig();
#else
            yield break;
#endif
        }

        [UnityTest]
        [Ignore("Task 11b: needs imported assets and scene")]
        public IEnumerator Hero_SpawnsAtRespawnMarker()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var hero = Object.FindFirstObjectByType<HeroController>();
            Assert.IsNotNull(hero, "scene has a hero");
            var marker = Object.FindFirstObjectByType<RespawnMarker>();
            Assert.IsNotNull(marker, "scene has a respawn marker");
            Assert.AreSame(marker, RespawnMarker.Find(marker.MarkerName), "the marker registers itself under its name");

            // The hero root starts at the marker (RespawnAt places the root there), before gravity has moved it far.
            Vector2 heroPos = hero.transform.position, markerPos = marker.transform.position;
            Assert.Less(Vector2.Distance(heroPos, markerPos), 0.05f, "hero starts at the respawn marker");
            Assert.AreEqual(marker.FacingRight, hero.FacingRight, "hero faces the way the marker says");
#else
            yield break;
#endif
        }

        // ---------- 3. animations (11b) ----------

        [UnityTest]
        [Ignore("Task 11b: needs imported assets and scene")]
        public IEnumerator HeroAnimations_AllNeededClipsPresent_ForRunJumpDashWallAttackHurtDeath()
        {
#if UNITY_EDITOR
            var names = new List<string>(HeroClipNames.PlayedByHeroCode);
            names.AddRange(HeroClipNames.ImportedForLater);
            Assert.GreaterOrEqual(names.Count, 15);

            var clips = new List<AnimationClip>();
            foreach (var name in names)
            {
                string path = HeroClipNames.ImportFolder + "/" + name + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                Assert.IsNotNull(clip, "missing hero clip " + path);
                Assert.AreEqual(name, clip.name, "clip is named after the animation hero code plays");
                Assert.Greater(clip.frameRate, 0f, name);

                var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                Assert.AreEqual(1, bindings.Length, name + " animates exactly the sprite");
                Assert.AreEqual(typeof(SpriteRenderer), bindings[0].type, name);
                Assert.AreEqual("m_Sprite", bindings[0].propertyName, name);
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
                Assert.GreaterOrEqual(keys.Length, 1, name);
                Assert.AreEqual(keys.Length / clip.frameRate, clip.length, 1e-3f, name + ": length is frames / fps (no closing key)");
                foreach (var k in keys)
                {
                    var sprite = k.value as Sprite;
                    Assert.IsNotNull(sprite, name + ": every key holds a sprite");
                    Assert.AreEqual(SilksongUnits.PixelsPerUnit, sprite.pixelsPerUnit, 1e-3f, name + ": art is at 64 pixels per unit");
                }
                clips.Add(clip);
            }

            // The loops and one-shots the hero code relies on.
            Assert.IsTrue(Clip(clips, "Idle").isLooping);
            Assert.IsTrue(Clip(clips, "Run").isLooping);
            foreach (var once in new[] { "Dash", "Slash", "SlashUp", "Hurt", "Land" })
                Assert.IsFalse(Clip(clips, once).isLooping, once + " plays once");
            // Footsteps: Run carries two frame events for UnitySpriteAnimator.
            var run = Clip(clips, "Run");
            Assert.AreEqual(2, run.events.Length);
            foreach (var e in run.events) Assert.AreEqual("OnFrameEvent", e.functionName);
            Assert.AreEqual(15f, run.frameRate);
            Assert.AreEqual(10f / 15f, run.length, 1e-3f);

            // Every clip registers and plays by the name hero code uses. LoopSection clips come as an intro
            // ("Jump") plus a looping "Jump Loop"; register both so the animator can chain them.
            var go = Track(new GameObject("AnimCheck", typeof(SpriteRenderer), typeof(Animator), typeof(UnitySpriteAnimator)));
            var animator = go.GetComponent<UnitySpriteAnimator>();
            foreach (var c in clips) animator.Register(c);
            var loopParts = new List<string>();
            foreach (var name in names)
            {
                var loop = AssetDatabase.LoadAssetAtPath<AnimationClip>(HeroClipNames.ImportFolder + "/" + name + UnitySpriteAnimator.LoopSuffix + ".anim");
                if (loop == null) continue;
                Assert.IsTrue(loop.isLooping, name + " loop part loops");
                Assert.IsFalse(Clip(clips, name).isLooping, name + " intro plays once");
                animator.Register(loop);
                loopParts.Add(name);
            }
            foreach (var expected in new[] { "Jump", "Fall", "WallCling", "SlashDown", "Death" })
                CollectionAssert.Contains(loopParts, expected, expected + " is a LoopSection clip in the source");
            foreach (var name in names)
            {
                animator.Play(name);
                Assert.IsTrue(animator.IsPlaying(name), name);
            }

            // Jump and Fall chain into their loop and are not restarted by hero code calling Play every step.
            foreach (var name in new[] { "Jump", "Fall" })
            {
                animator.Play(name);
                yield return null;
                yield return new WaitForSeconds(Clip(clips, name).length + 0.1f);
                Assert.IsTrue(animator.IsPlaying(name + UnitySpriteAnimator.LoopSuffix), name + " chained into its loop part");
                for (int i = 0; i < 5; i++) animator.Play(name);
                Assert.IsTrue(animator.IsPlaying(name + UnitySpriteAnimator.LoopSuffix), name + " not restarted by repeated Play");
            }
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        static AnimationClip Clip(List<AnimationClip> clips, string name) => clips.Find(c => c.name == name);
#endif
    }
}
