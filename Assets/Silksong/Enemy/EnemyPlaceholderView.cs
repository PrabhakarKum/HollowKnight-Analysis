using UnityEngine;

namespace Hollowknight.Silksong
{
    // A visible stand-in for the enemy until real art exists: a greenish 1.4 x 0.8 box with a darker
    // "eye" on the left (the art-facing side, so the flip is visible), plus a BoxCollider2D on the root.
    // Build creates objects only; it edits no scene. The caller adds Rigidbody2D / EnemyWalker to the root.
    // The root scale stays 1 (EnemyWalker flips its x sign); the box size lives on a child.
    public static class EnemyPlaceholderView
    {
        public static readonly Vector2 Size = new Vector2(1.4f, 0.8f);
        public static readonly Color BodyColor = new Color(0.35f, 0.65f, 0.3f, 1f);
        static Sprite whiteSprite;

        public static GameObject Build(Transform parent)
        {
            var go = new GameObject("EnemyPlaceholder");
            if (parent != null) go.transform.SetParent(parent, false);

            var box = go.AddComponent<BoxCollider2D>();
            box.size = Size;

            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(Size.x, Size.y, 1f);
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite();
            sr.color = BodyColor;

            var eye = new GameObject("Eye");
            eye.transform.SetParent(go.transform, false);
            eye.transform.localPosition = new Vector3(-Size.x * 0.35f, Size.y * 0.15f, 0f);
            eye.transform.localScale = new Vector3(0.2f, 0.2f, 1f);
            var esr = eye.AddComponent<SpriteRenderer>();
            esr.sprite = WhiteSprite();
            esr.color = new Color(0.1f, 0.25f, 0.1f, 1f);
            esr.sortingOrder = 1;
            return go;
        }

        // A 1x1-unit white sprite made from a 4x4 texture, shared.
        static Sprite WhiteSprite()
        {
            if (whiteSprite != null) return whiteSprite;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            whiteSprite.name = "EnemyPlaceholderWhite";
            return whiteSprite;
        }
    }
}
