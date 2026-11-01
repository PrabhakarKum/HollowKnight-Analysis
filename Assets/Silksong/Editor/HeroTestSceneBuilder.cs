using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Hollowknight.Silksong.Editor
{
    // Builds a scratch scene so the hero can be played and judged by hand. Everything is generated from
    // code (no hand-placed objects) so the scene YAML stays reviewable and a rebuild is deterministic.
    public static class HeroTestSceneBuilder
    {
        public const string DefaultScenePath = "Assets/Scenes/SilksongHero.unity";
        public const string ConfigPath = "Assets/Silksong/Hero/HeroConfig.asset";
        public const string InputActionsPath = "Assets/Silksong/Input/HeroInputActions.inputactions";
        // A plain white square (placeholder art, not any Silksong sprite); tinted per object.
        public const string SpritePath = "Assets/Silksong/Hero/Testing/WhiteSquare.png";

        // Feet-to-head size of the placeholder hero.
        static readonly Vector2 HeroSize = new Vector2(0.8f, 1.8f);
        const float UpperFloorTop = 0f;
        // A 5 unit drop is below the full-jump height, so the hero can climb back up the cliff.
        const float LowerFloorTop = -5f;
        const float WorldBottom = -12f;

        static Sprite whiteSprite;
        static Font labelFont;

        [MenuItem("Hollowknight/Silksong/Build Hero Test Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            EditorSceneManager.OpenScene(DefaultScenePath);
        }

        // Batch entry point: -executeMethod Hollowknight.Silksong.Editor.HeroTestSceneBuilder.BuildBatch
        public static void BuildBatch()
        {
            try
            {
                string path = Build();
                Debug.Log("[HeroTestSceneBuilder] Built " + path);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[HeroTestSceneBuilder] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        // Builds into scenePath (overwriting it). The HeroConfig asset is shared and only created if
        // absent, so tests that build into a temp folder never produce a second config.
        public static string Build(string scenePath = DefaultScenePath)
        {
            EnsureFolder(Path.GetDirectoryName(scenePath).Replace('\\', '/'));
            var config = EnsureConfig();
            whiteSprite = EnsureSprite();
            labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (labelFont == null) throw new InvalidOperationException("Built-in label font LegacyRuntime.ttf not found");
            var actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actionsAsset == null) throw new InvalidOperationException("Missing " + InputActionsPath);

            // Additive so the scene the user (or test runner) has open is never replaced; closed again below.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                BuildWorld();
                var hero = BuildHero(actionsAsset, out var actions);
                BuildCamera(hero.transform);
                BuildHud(hero, actions, config);
                if (!EditorSceneManager.SaveScene(scene, scenePath))
                    throw new InvalidOperationException("Could not save " + scenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.Refresh();
            return scenePath;
        }

        // ---------- assets ----------

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        static HeroConfig EnsureConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<HeroConfig>(ConfigPath);
            if (existing != null) return existing;
            var config = ScriptableObject.CreateInstance<HeroConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        static Sprite EnsureSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(SpritePath).Replace('\\', '/'));
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);

            // 4 pixels at 4 pixels-per-unit = exactly one world unit, so object scale is its size in units.
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 4f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        // ---------- world ----------

        static void BuildWorld()
        {
            var world = new GameObject("World").transform;
            var labels = new GameObject("Labels").transform;
            var ground = new Color(0.35f, 0.35f, 0.4f);
            var slopeColor = new Color(0.8f, 0.5f, 0.2f);
            var platformColor = new Color(0.3f, 0.55f, 0.85f);

            // Upper floor (start area) and lower floor; the cliff between them is the ledge to walk off.
            Block(world, "UpperFloor", -20f, 40f, UpperFloorTop, WorldBottom, ground);
            Block(world, "LowerFloor", 40f, 140f, LowerFloorTop, WorldBottom, ground);
            // Tall end walls so the hero can never leave the test area.
            Block(world, "WallLeft", -21f, -20f, 40f, WorldBottom, ground);
            Block(world, "WallRight", 140f, 141f, 40f, WorldBottom, ground);

            // Start marker: a collider-less pole so it never affects movement.
            var pole = Visual(world, "StartMarker", new Vector2(0f, 1.5f), new Vector2(0.2f, 3f), new Color(0.2f, 0.85f, 0.3f));
            pole.sortingOrder = -1;
            Label(labels, "START\n(R returns here)", new Vector3(0f, 4.2f));
            Label(labels, "Wide flat ground: run both ways", new Vector3(-10f, 2f));

            Label(labels, "LEDGE: walk off, then jump late\n(coyote time 0.08 s)", new Vector3(37f, 3f));
            Label(labels, "LOWER FLOOR\n(5 u drop; jump back up the cliff)", new Vector3(46f, -1.5f));

            // Slopes rise 5 units so their plateau is level with the upper floor.
            Slope(world, "Slope30", 55f, LowerFloorTop, 30f, 5f, slopeColor, 6f, ground);
            Label(labels, "30 DEG SLOPE\n(idle on it: no creeping)", new Vector3(58f, 2.5f));
            Slope(world, "Slope45", 80f, LowerFloorTop, 45f, 5f, slopeColor, 6f, ground);
            Label(labels, "45 DEG SLOPE\n(steeper ramp)", new Vector3(83f, 2.5f));

            // Reference jump heights, measured above the lower floor.
            Platform(world, labels, "PlatformLowHop", 100f, 1.5f, platformColor, "LOW HOP\n1.5 u: a tap reaches it");
            Platform(world, labels, "PlatformFullJump", 110f, 5f, platformColor, "FULL JUMP\n5 u: needs a held jump");
            Platform(world, labels, "PlatformTooHigh", 120f, 8f, platformColor, "TOO HIGH\n8 u: should be unreachable");
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

        // Solid axis-aligned block described by its edges, which is easier to reason about than centres.
        static void Block(Transform parent, string name, float minX, float maxX, float top, float bottom, Color color)
        {
            var center = new Vector2((minX + maxX) * 0.5f, (top + bottom) * 0.5f);
            var sr = Visual(parent, name, center, new Vector2(maxX - minX, top - bottom), color);
            AddBoxCollider(sr.gameObject);
        }

        static void AddBoxCollider(GameObject go)
        {
            // Unit size: the object's scale already carries the real dimensions.
            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
        }

        // A thin ramp whose top surface starts exactly at (x0, baseY) and climbs to baseY + rise, then
        // a plateau block so the ramp's far end meets solid ground with no gap.
        static void Slope(Transform parent, string name, float x0, float baseY, float angleDeg, float rise,
                          Color slopeColor, float plateauWidth, Color plateauColor)
        {
            const float thickness = 1f;
            float rad = angleDeg * Mathf.Deg2Rad;
            float length = rise / Mathf.Sin(rad);
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            var normal = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
            // Centre chosen so the TOP surface (not the box centre) starts at the base point.
            Vector2 center = new Vector2(x0, baseY) + dir * (length * 0.5f) - normal * (thickness * 0.5f);

            var sr = Visual(parent, name, center, new Vector2(length, thickness), slopeColor);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            AddBoxCollider(sr.gameObject);

            float endX = x0 + rise / Mathf.Tan(rad);
            Block(parent, name + "Plateau", endX, endX + plateauWidth, baseY + rise, WorldBottom, plateauColor);
        }

        static void Platform(Transform world, Transform labels, string name, float centerX, float heightAboveLowerFloor,
                             Color color, string text)
        {
            float top = LowerFloorTop + heightAboveLowerFloor;
            Block(world, name, centerX - 2.5f, centerX + 2.5f, top, top - 0.5f, color);
            Label(labels, text, new Vector3(centerX, top + 1.5f));
        }

        static void Label(Transform parent, string text, Vector3 position)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = labelFont;
            mesh.fontSize = 48;
            mesh.characterSize = 0.1f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
            var renderer = go.GetComponent<MeshRenderer>();
            // A TextMesh draws nothing without the font's own material.
            renderer.sharedMaterial = labelFont.material;
            renderer.sortingOrder = 10;
        }

        // ---------- hero, camera, hud ----------

        static HeroController BuildHero(InputActionAsset actionsAsset, out HeroActions actions)
        {
            var go = new GameObject("Hero");
            // Spawn with the capsule's feet a hair above the upper floor so it settles instead of starting inside it.
            go.transform.position = new Vector3(0f, UpperFloorTop + HeroSize.y * 0.5f + 0.05f, 0f);

            go.AddComponent<Rigidbody2D>();
            var capsule = go.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = HeroSize;
            var hero = go.AddComponent<HeroController>();
            actions = go.AddComponent<HeroActions>();

            // The actions asset field is private on purpose; SerializedObject sets it without a code change.
            var so = new SerializedObject(actions);
            so.FindProperty("actionsAsset").objectReferenceValue = actionsAsset;
            so.ApplyModifiedPropertiesWithoutUndo();

            // The body sprite is a child so scaling it to the hero's size never scales the collider.
            var sr = Visual(go.transform, "Visual", go.transform.position, HeroSize, new Color(1f, 0.85f, 0.25f));
            sr.sortingOrder = 5;
            return hero;
        }

        static void BuildCamera(Transform target)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 2f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            go.AddComponent<AudioListener>();
            var follow = go.AddComponent<HeroTestCameraFollow>();
            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildHud(HeroController hero, HeroActions actions, HeroConfig config)
        {
            var go = new GameObject("HeroDebugHud");
            var hud = go.AddComponent<HeroDebugHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("hero").objectReferenceValue = hero;
            so.FindProperty("actions").objectReferenceValue = actions;
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
