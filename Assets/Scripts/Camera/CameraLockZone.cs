using UnityEngine;
using Hollowknight.Player;

namespace Hollowknight.CameraSystem
{
    /// <summary>
    /// Trigger box that limits where the camera view may go while the player is inside.
    /// Size the BoxCollider2D to the area (a "room"); the camera clamps its view inside it.
    /// If the zone is smaller than the view, the camera centers on it.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class CameraLockZone : MonoBehaviour
    {
        BoxCollider2D box;
        public Bounds Bounds => box != null ? box.bounds : GetComponent<BoxCollider2D>().bounds;

        void Reset()
        {
            var b = GetComponent<BoxCollider2D>();
            b.isTrigger = true;
        }

        void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent<PlayerController2D>(out _)) return;
            var cam = FindFirstObjectByType<CameraFollow2D>();
            if (cam != null) cam.EnterZone(this);
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (!other.TryGetComponent<PlayerController2D>(out _)) return;
            var cam = FindFirstObjectByType<CameraFollow2D>();
            if (cam != null) cam.ExitZone(this);
        }

        void OnDrawGizmos()
        {
            var b = GetComponent<BoxCollider2D>();
            if (b == null) return;
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.9f);
            Gizmos.DrawWireCube(b.bounds.center, b.bounds.size);
        }
    }
}
