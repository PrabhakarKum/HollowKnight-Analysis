#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Hollowknight.CameraSystem;
using Hollowknight.Player;

namespace Hollowknight.EditorTools
{
    public static class CameraSetupMenu
    {
        [MenuItem("Hollowknight/Add Camera Follow + Lock Zone")]
        public static void AddCameraFollow()
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("No main camera in the scene."); return; }

            cam.orthographic = true;
            cam.orthographicSize = 6f;

            var follow = cam.GetComponent<CameraFollow2D>();
            if (follow == null) follow = Undo.AddComponent<CameraFollow2D>(cam.gameObject);
            if (cam.GetComponent<Hollowknight.Feel.CameraShake2D>() == null) Undo.AddComponent<Hollowknight.Feel.CameraShake2D>(cam.gameObject);   // screen shake rides on the follow camera

            var player = Object.FindFirstObjectByType<PlayerController2D>();
            if (player != null)
            {
                var so = new SerializedObject(follow);
                so.FindProperty("target").objectReferenceValue = player;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else Debug.LogWarning("No PlayerController2D found; run 'Create Movement Test Setup' first.");

            if (Object.FindFirstObjectByType<CameraLockZone>() == null)
            {
                var zone = new GameObject("Camera Lock Zone (Room 1)");
                zone.transform.position = new Vector3(0f, 4f, 0f);
                var box = zone.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(34f, 18f);
                zone.AddComponent<CameraLockZone>();
            }

            EditorUtility.SetDirty(cam.gameObject);
            Debug.Log("Camera follow added. Move/resize 'Camera Lock Zone' to shape the room; the view stays inside it.");
        }
    }
}
#endif
