#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Hollowknight.Player;

namespace Hollowknight.EditorTools
{
    public static class VisualsSetupMenu
    {
        const string ControllerPath = "Assets/DeadRevolver/PixelPrototypePlayerSprites/Art/Animations/Animators/PlayerAnimator.controller";
        const float TargetHeight = 1.4f; // collider height; the sprite is scaled so its standing frame is about this tall

        [MenuItem("Hollowknight/Use DeadRevolver Sprites On Player")]
        public static void Apply()
        {
            var player = Object.FindFirstObjectByType<PlayerController2D>();
            if (player == null) { Debug.LogError("No PlayerController2D in the scene."); return; }

            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (ctrl == null) { Debug.LogError("Animator controller not found at " + ControllerPath); return; }

            // placeholder box off
            var rootSr = player.GetComponent<SpriteRenderer>();
            if (rootSr != null) rootSr.enabled = false;

            var visual = player.transform.Find("Visual");
            if (visual == null)
            {
                var go = new GameObject("Visual");
                go.transform.SetParent(player.transform, false);
                visual = go.transform;
            }

            var sr = visual.GetComponent<SpriteRenderer>();
            if (sr == null) sr = visual.gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 5;
            var anim = visual.GetComponent<Animator>();
            if (anim == null) anim = visual.gameObject.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.updateMode = AnimatorUpdateMode.Normal;
            anim.applyRootMotion = false;

            // feet on the bottom of the collider
            var col = player.GetComponent<Collider2D>();
            float bottom = col != null ? -col.bounds.extents.y + (col.offset.y) : -0.7f;
            visual.localPosition = new Vector3(0f, bottom, 0f);
            visual.localScale = Vector3.one;

            var visuals = player.GetComponent<PlayerVisuals2D>();
            if (visuals == null) visuals = Undo.AddComponent<PlayerVisuals2D>(player.gameObject);
            var so = new SerializedObject(visuals);
            so.FindProperty("controller").objectReferenceValue = player;
            so.FindProperty("animator").objectReferenceValue = anim;
            so.ApplyModifiedPropertiesWithoutUndo();

            var pso = new SerializedObject(player);
            pso.FindProperty("spriteToFlip").objectReferenceValue = sr;
            pso.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(player.gameObject);
            Debug.Log("DeadRevolver visuals applied. Tune the 'Visual' child's scale/offset to fit the collider (target height " + TargetHeight + ").");
        }
    }
}
#endif
