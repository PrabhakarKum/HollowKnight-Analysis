#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Hollowknight.Player;

namespace Hollowknight.EditorTools
{
    public static class PlayerSetupMenu
    {
        const string TuningPath = "Assets/ScriptableObjects/PlayerTuning.asset";
        const string MaterialPath = "Assets/ScriptableObjects/NoFriction.physicsMaterial2D";

        [MenuItem("Hollowknight/Create Movement Test Setup")]
        public static void CreateTestSetup()
        {
            EnsureFolder("Assets/ScriptableObjects");

            var tuning = AssetDatabase.LoadAssetAtPath<PlayerTuning>(TuningPath);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<PlayerTuning>();
                AssetDatabase.CreateAsset(tuning, TuningPath);
            }

            var noFriction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath);
            if (noFriction == null)
            {
                noFriction = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
                AssetDatabase.CreateAsset(noFriction, MaterialPath);
            }
            AssetDatabase.SaveAssets();

            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var player = new GameObject("Player");
            player.transform.position = new Vector3(0, 1, 0);
            var sr = player.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(0.8f, 1.4f);
            sr.color = new Color(0.85f, 0.9f, 1f);
            player.AddComponent<Rigidbody2D>();
            var box = player.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.8f, 1.4f);
            box.sharedMaterial = noFriction;
            var ctrl = player.AddComponent<PlayerController2D>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("tuning").objectReferenceValue = tuning;
            so.FindProperty("spriteToFlip").objectReferenceValue = sr;
            so.ApplyModifiedPropertiesWithoutUndo();

            MakePlatform("Ground", new Vector2(0, -1), new Vector2(30, 1), sprite, noFriction);
            MakePlatform("Platform Low", new Vector2(6, 1.2f), new Vector2(4, 0.5f), sprite, noFriction);
            MakePlatform("Platform High", new Vector2(-6, 3.5f), new Vector2(4, 0.5f), sprite, noFriction);
            MakePlatform("Wall", new Vector2(12, 2), new Vector2(1, 6), sprite, noFriction);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 6;
                cam.transform.position = new Vector3(0, 2, -10);
            }

            Selection.activeGameObject = player;
            Debug.Log("Movement test setup created. Play: A/D or arrows = run, Space/Z = jump.");
        }

        static void MakePlatform(string name, Vector2 pos, Vector2 size, Sprite sprite, PhysicsMaterial2D mat)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = new Color(0.25f, 0.3f, 0.4f);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = size;
            bc.sharedMaterial = mat;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
#endif
