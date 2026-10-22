#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Hollowknight.Player;

namespace Hollowknight.EditorTools
{
    public static class DamageSetupMenu
    {
        public const string RootName = "Damage Test Objects";

        [MenuItem("Hollowknight/Add Health + Damage Test Objects")]
        public static void Apply()
        {
            var player = Object.FindFirstObjectByType<PlayerController2D>();
            if (player == null) { Debug.LogError("No PlayerController2D in the scene."); return; }

            if (player.GetComponent<PlayerHealth2D>() == null) Undo.AddComponent<PlayerHealth2D>(player.gameObject);
            if (Object.FindFirstObjectByType<HealthHud2D>() == null)
            {
                var hud = new GameObject("Health HUD");
                Undo.RegisterCreatedObjectUndo(hud, "Add Health HUD");
                hud.AddComponent<HealthHud2D>();
            }

            var root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Add damage test objects");
                var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                MakeEnemy(root.transform, sprite);
                MakeSpikes(root.transform, sprite);
            }

            Debug.Log("Health + damage added. Walk into the red enemy (west end of the ground) to be knocked back; the grey spikes (east end) send you back to the last safe ground. Slash the enemy: it is also a pogo target.");
        }

        static void MakeEnemy(Transform parent, Sprite sprite)
        {
            var go = new GameObject("Test Enemy (patrol)");
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(-12.5f, 0.2f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.drawMode = SpriteDrawMode.Sliced; sr.size = new Vector2(1f, 1.4f);
            sr.color = new Color(0.85f, 0.25f, 0.25f); sr.sortingOrder = 2;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = sr.size; col.isTrigger = true;
            go.AddComponent<Rigidbody2D>();
            var patrol = go.AddComponent<SimplePatrol2D>();
            patrol.leftX = -14f; patrol.rightX = -11f; patrol.speed = 2.5f;
            go.AddComponent<DamageSource2D>().kind = DamageKind.Enemy;
            var t = go.AddComponent<HitTarget>();
            t.pogo = true; t.recoilsPlayer = true;
        }

        static void MakeSpikes(Transform parent, Sprite sprite)
        {
            var go = new GameObject("Test Spikes");
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(9.5f, -0.25f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.drawMode = SpriteDrawMode.Sliced; sr.size = new Vector2(1.6f, 0.5f);
            sr.color = new Color(0.6f, 0.6f, 0.65f); sr.sortingOrder = 2;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = sr.size; col.isTrigger = true;
            go.AddComponent<DamageSource2D>().kind = DamageKind.Hazard;
        }
    }
}
#endif
