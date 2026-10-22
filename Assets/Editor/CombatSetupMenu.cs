#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Hollowknight.Player;

namespace Hollowknight.EditorTools
{
    public static class CombatSetupMenu
    {
        [MenuItem("Hollowknight/Add Combat + Hit Dummies")]
        public static void Apply()
        {
            var player = Object.FindFirstObjectByType<PlayerController2D>();
            if (player == null) { Debug.LogError("No PlayerController2D in the scene."); return; }

            if (player.GetComponent<PlayerCombat2D>() == null) Undo.AddComponent<PlayerCombat2D>(player.gameObject);

            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (GameObject.Find("Hit Dummy (ground)") == null)
                MakeDummy("Hit Dummy (ground)", new Vector2(-2.5f, 0.2f), new Vector2(0.9f, 1.4f), sprite, new Color(0.85f, 0.4f, 0.35f));
            if (GameObject.Find("Hit Dummy (pogo, air)") == null)
                MakeDummy("Hit Dummy (pogo, air)", new Vector2(2.0f, 1.2f), new Vector2(1.0f, 1.0f), sprite, new Color(0.9f, 0.7f, 0.3f));

            Debug.Log("Combat added. J / C / left mouse / gamepad West = attack. Hold Up for an up-slash, Down in the air for a down-slash (pogo off the dummies).");
        }

        static void MakeDummy(string name, Vector2 pos, Vector2 size, Sprite sprite, Color color)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.drawMode = SpriteDrawMode.Sliced; sr.size = size; sr.color = color; sr.sortingOrder = 2;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size; col.isTrigger = true;                 // the player walks through it; the slash still finds it
            var t = go.AddComponent<HitTarget>();
            t.pogo = true; t.recoilsPlayer = true;
        }
    }
}
#endif
