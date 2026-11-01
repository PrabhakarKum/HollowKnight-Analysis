using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowknight.Silksong.Editor
{
    // Builds the Silksong hero room scene: a simple room that exercises run, jump, dash, wall cling / wall jump and
    // pogo, with the real hero art. The hero is configured only by the project HeroConfig asset: the scene holds a
    // reference to it (HeroSceneBootstrap) and no copied value. Camera numbers live in GameCameraSettings.
    //
    // Needs the imported hero animations (menu Silksong/Import Hero Animations, run once). If they are missing the
    // build fails with a message saying so; it never falls back to placeholder art.
    public static class HeroSceneBuilder
    {
        // Not Assets/Scenes/SilksongHero.unity: that path holds the committed placeholder-art scene from
        // HeroTestSceneBuilder. This scene references gitignored imported sprites, so it is generated locally.
        public const string ScenePath = "Assets/Scenes/SilksongHeroRoom.unity";
        public const string ImportFirstMessage =
            "Hero animations are not imported. Run the menu 'Silksong/Import Hero Animations' first, then run 'Silksong/Build Hero Scene' again.";

        // Room layout in world units (1 unit = 1 original unit). Floor top is y = 0.
        const float RoomMinX = 0f, RoomMaxX = 90f, RoomMinY = -4f, RoomMaxY = 30f;
        const float FloorBottom = -3f;
        const float PitMinX = 24f, PitMaxX = 29f, PitBottom = -2f;

        static Sprite whiteSprite;

        [MenuItem("Silksong/Build Hero Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                Build(ScenePath, true);
                EditorSceneManager.OpenScene(ScenePath);
                Debug.Log("[HeroSceneBuilder] Built and opened " + ScenePath);
            }
            catch (Exception e)
            {
                Debug.LogError("[HeroSceneBuilder] " + e.Message);
            }
        }

        // -executeMethod Hollowknight.Silksong.Editor.HeroSceneBuilder.BuildBatch
        public static void BuildBatch()
        {
            try
            {
                Debug.Log("[HeroSceneBuilder] Built " + Build(ScenePath, true));
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[HeroSceneBuilder] FAILED: " + e.Message);
                EditorApplication.Exit(1);
            }
        }

        // Builds the scene only when the file is absent (tests call this before loading it). Returns the path.
        public static string EnsureBuilt()
        {
            if (File.Exists(ScenePath)) return ScenePath;
            return Build(ScenePath, true);
        }

        // Every hero clip name the scene needs, intro and loop part where a loop part exists.
        public static List<string> MissingClips()
        {
            var missing = new List<string>();
            foreach (var group in new[] { HeroClipNames.PlayedByHeroCode, HeroClipNames.ImportedForLater })
                foreach (var name in group)
                    if (AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(name)) == null) missing.Add(name);
            return missing;
        }

        static string ClipPath(string name) => HeroClipNames.ClipPath(name);

        public static string Build(string scenePath, bool discardOpenScenes)
        {
            if (!discardOpenScenes)
                throw new InvalidOperationException("Build replaces all open scenes without saving; pass discardOpenScenes: true to confirm.");

            // Fail before touching any scene, with the instruction the owner needs.
            var missing = MissingClips();
            if (missing.Count > 0)
                throw new InvalidOperationException(ImportFirstMessage + " (missing clips: " + string.Join(", ", missing) + ")");
            RequireAsset<HeroConfig>(HeroTestSceneBuilder.ConfigPath);
            RequireAsset<InputActionAsset>(HeroTestSceneBuilder.InputActionsPath);
            RequireAsset<Sprite>(HeroTestSceneBuilder.SpritePath);

            EnsureFolder(Path.GetDirectoryName(scenePath).Replace('\\', '/'));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                // Loaded after the scene switch: switching can unload assets fetched earlier.
                var config = RequireAsset<HeroConfig>(HeroTestSceneBuilder.ConfigPath);
                var actionsAsset = RequireAsset<InputActionAsset>(HeroTestSceneBuilder.InputActionsPath);
                whiteSprite = RequireAsset<Sprite>(HeroTestSceneBuilder.SpritePath);

                BuildRoom();
                var marker = BuildRespawnMarker();
                var bounds = RoomBounds.Create(new Rect(RoomMinX, RoomMinY, RoomMaxX - RoomMinX, RoomMaxY - RoomMinY));
                var hero = BuildHero(marker, actionsAsset, config, out var bootstrapParts);
                BuildDummies();
                BuildCamera(hero.transform, bounds);
                BuildBootstrap(hero, config, marker, bootstrapParts);

                if (!EditorSceneManager.SaveScene(scene, scenePath))
                    throw new InvalidOperationException("Could not save " + scenePath);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            AssetDatabase.Refresh();
            return scenePath;
        }

        // ---------- helpers ----------

        static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Required asset missing or not imported: " + path);
            return asset;
        }

        static void SetReference(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            if (value == null) throw new InvalidOperationException("Cannot assign null to " + target.GetType().Name + "." + property);
            var so = new SerializedObject(target);
            var prop = so.FindProperty(property);
            if (prop == null) throw new InvalidOperationException("Missing serialized field " + property + " on " + target.GetType().Name);
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (prop.objectReferenceValue == null)
                throw new InvalidOperationException(target.GetType().Name + "." + property + " did not keep its reference");
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // ---------- room ----------

        static void BuildRoom()
        {
            var world = new GameObject("World").transform;
            var ground = new Color(0.35f, 0.35f, 0.4f);
            var platform = new Color(0.3f, 0.55f, 0.85f);
            var wall = new Color(0.55f, 0.4f, 0.6f);

            // Floor with a shallow pit (dash and jump across it, or fall in and wall-jump out).
            Block(world, "FloorLeft", RoomMinX, PitMinX, 0f, FloorBottom, ground);
            Block(world, "PitBottom", PitMinX, PitMaxX, PitBottom, FloorBottom, ground);
            Block(world, "FloorRight", PitMaxX, RoomMaxX, 0f, FloorBottom, ground);
            // Room walls, tall enough that the hero can never leave.
            Block(world, "WallLeft", RoomMinX - 1f, RoomMinX, RoomMaxY, FloorBottom, ground);
            Block(world, "WallRight", RoomMaxX, RoomMaxX + 1f, RoomMaxY, FloorBottom, ground);
            Block(world, "Ceiling", RoomMinX, RoomMaxX, RoomMaxY + 1f, RoomMaxY, ground);

            // Jump practice: a low step and a higher platform.
            Block(world, "PlatformLow", 10f, 14f, 1.5f, 1.0f, platform);
            Block(world, "PlatformHigh", 16f, 20f, 3.0f, 2.5f, platform);

            // Wall jump shaft: two walls 4 units apart; zig-zag up and step onto the ledge on the right.
            Block(world, "ShaftWallLeft", 38f, 40f, 16f, FloorBottom, wall);
            Block(world, "ShaftWallRight", 44f, 54f, 16f, FloorBottom, wall);

            // Damage practice: red spikes, one on the floor and one in the pit. Touch them to be hurt (the damage,
            // knockback and invulnerability all come from HazardZone / HeroHealth / HeroConfig, nothing set here).
            Spikes(world, "SpikesFloor", new Vector2(33f, 0.25f), new Vector2(2f, 0.5f));
            Spikes(world, "SpikesPit", new Vector2((PitMinX + PitMaxX) * 0.5f, PitBottom + 0.25f), new Vector2(2f, 0.5f));
        }

        static void Spikes(Transform parent, string name, Vector2 center, Vector2 size)
        {
            var sr = Visual(parent, name, center, size, new Color(0.9f, 0.1f, 0.1f));
            sr.sortingOrder = 1;
            var col = sr.gameObject.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            col.isTrigger = true;
            sr.gameObject.AddComponent<HazardZone>();
        }

        static SpriteRenderer Visual(Transform parent, string name, Vector2 center, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = whiteSprite;
            sr.color = color;
            return sr;
        }

        static void Block(Transform parent, string name, float minX, float maxX, float top, float bottom, Color color)
        {
            var center = new Vector2((minX + maxX) * 0.5f, (top + bottom) * 0.5f);
            var sr = Visual(parent, name, center, new Vector2(maxX - minX, top - bottom), color);
            // Unit size: the object's scale already carries the real dimensions.
            sr.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;
        }

        // ---------- hero ----------

        // Height of the hero root above the feet, from the original collider.
        static float RootAboveFeet => SilksongUnits.HeroColliderSize.y * 0.5f - SilksongUnits.HeroColliderOffset.y;

        static RespawnMarker BuildRespawnMarker()
        {
            var go = new GameObject("RespawnMarker");
            // Root a hair above where the feet touch the floor, so the hero settles instead of starting inside it.
            go.transform.position = new Vector3(4f, 0f + RootAboveFeet + 0.01f, 0f);
            var marker = go.AddComponent<RespawnMarker>();
            marker.Configure("Start", true);
            return marker;
        }

        struct HeroParts
        {
            public HeroActions Actions;
            public UnitySpriteAnimator Animator;
            public SpriteRenderer Visual;
        }

        static HeroController BuildHero(RespawnMarker marker, InputActionAsset actionsAsset, HeroConfig config, out HeroParts parts)
        {
            var go = new GameObject("Hero");
            go.transform.position = marker.transform.position;
            go.transform.localScale = Vector3.one * SilksongUnits.HeroRootScale;

            go.AddComponent<Rigidbody2D>();
            var box = go.AddComponent<BoxCollider2D>();
            box.size = SilksongUnits.HeroColliderSize;
            box.offset = SilksongUnits.HeroColliderOffset;
            var hero = go.AddComponent<HeroController>();
            go.AddComponent<HeroHealth>();
            var actions = go.AddComponent<HeroActions>();
            SetReference(actions, "actionsAsset", actionsAsset);

            // Art on a child so flipping it never touches the collider. Sprite origin = hero root (tk2d origin).
            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(go.transform, false);
            var sr = visualGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 5;
            var animator = visualGo.AddComponent<UnitySpriteAnimator>(); // brings the Animator it needs

            var clips = new List<AnimationClip>();
            foreach (var group in new[] { HeroClipNames.PlayedByHeroCode, HeroClipNames.ImportedForLater })
                foreach (var name in group)
                {
                    clips.Add(RequireAsset<AnimationClip>(ClipPath(name)));
                    var loopPart = AssetDatabase.LoadAssetAtPath<AnimationClip>(HeroClipNames.LoopClipPath(name));
                    if (loopPart != null) clips.Add(loopPart);
                }
            var so = new SerializedObject(animator);
            var list = so.FindProperty("clips");
            list.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // The first Idle frame, so the hero is visible in the editor before play.
            var idle = RequireAsset<AnimationClip>(ClipPath("Idle"));
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(idle);
            if (bindings.Length > 0)
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(idle, bindings[0]);
                if (keys.Length > 0) sr.sprite = keys[0].value as Sprite;
            }

            parts = new HeroParts { Actions = actions, Animator = animator, Visual = sr };
            return hero;
        }

        static void BuildDummies()
        {
            var parent = new GameObject("Dummies").transform;
            // Standing on the floor, for side and up swings.
            Dummy(parent, "TrainingDummyGround", new Vector2(60f, 1.25f));
            // Floating high over the floor, to pogo on: hit it downward from above, again and again.
            Dummy(parent, "TrainingDummyAir", new Vector2(70f, 5f));
        }

        static void Dummy(Transform parent, string name, Vector2 center)
        {
            var sr = Visual(parent, name, center, new Vector2(1.5f, 2.5f), new Color(0.85f, 0.3f, 0.3f));
            sr.sortingOrder = 1;
            // A trigger, as in the prototype, so the hero can pass through it.
            var col = sr.gameObject.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            col.isTrigger = true;
            sr.gameObject.AddComponent<HealthManager>();
            sr.gameObject.AddComponent<TrainingDummy>();
        }

        // ---------- camera and bootstrap ----------

        static void BuildCamera(Transform target, RoomBounds bounds)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(target.position.x, target.position.y, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = SilksongUnits.CameraOrthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            go.AddComponent<AudioListener>();
            var gameCam = go.AddComponent<GameCamera>();
            gameCam.Bounds = bounds;
            gameCam.Target = target;
            // Properties are not serialized fields; write the private ones through the serializer.
            SetReference(gameCam, "target", target);
            SetReference(gameCam, "bounds", bounds);
        }

        static void BuildBootstrap(HeroController hero, HeroConfig config, RespawnMarker marker, HeroParts parts)
        {
            var go = new GameObject("HeroSceneBootstrap");
            var boot = go.AddComponent<HeroSceneBootstrap>();
            SetReference(boot, "hero", hero);
            SetReference(boot, "actions", parts.Actions);
            SetReference(boot, "config", config);          // the one project asset, by reference
            SetReference(boot, "animator", parts.Animator);
            SetReference(boot, "visual", parts.Visual);
            SetReference(boot, "spawn", marker);
        }
    }
}
