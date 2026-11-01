using UnityEngine;

namespace Hollowknight.Silksong
{
    // The world rectangle a room's camera must stay inside. In the original this is the room tilemap's size
    // (origin at 0,0): the camera centre is kept at least 14.6 units from the left/right edges and 8.3 from the
    // bottom/top (CameraController.KeepWithinSceneBounds). Here the rectangle is placed by hand or from code.
    public class RoomBounds : MonoBehaviour
    {
        [SerializeField] Vector2 size = new Vector2(60f, 30f);
        // Rectangle's lower-left corner, relative to this object's position.
        [SerializeField] Vector2 min = Vector2.zero;

        // World-space rectangle.
        public Rect Area
        {
            get
            {
                Vector2 origin = (Vector2)transform.position + min;
                return new Rect(origin, size);
            }
        }

        // For rooms built from code (tests, generated scenes): the rectangle in world space.
        public void Configure(Rect worldArea)
        {
            transform.position = Vector3.zero;
            min = worldArea.min;
            size = worldArea.size;
        }

        public static RoomBounds Create(Rect worldArea, string name = "RoomBounds")
        {
            var go = new GameObject(name);
            var rb = go.AddComponent<RoomBounds>();
            rb.Configure(worldArea);
            return rb;
        }

        public Vector2 ClampCameraCenter(Vector2 centre, Vector2 inset) => ClampCenter(Area, centre, inset);

        // Pure rule: keep the centre at least `inset` from each edge. If the room is narrower than twice the
        // inset on an axis there is no valid range, so the centre goes to the middle of the room on that axis
        // (the original would invert the range and pin to one edge; the middle is what the prototype did).
        public static Vector2 ClampCenter(Rect room, Vector2 centre, Vector2 inset)
        {
            return new Vector2(ClampAxis(centre.x, room.xMin, room.xMax, inset.x),
                               ClampAxis(centre.y, room.yMin, room.yMax, inset.y));
        }

        static float ClampAxis(float v, float lo, float hi, float inset)
        {
            float a = lo + inset, b = hi - inset;
            if (a > b) return (lo + hi) * 0.5f;
            return Mathf.Clamp(v, a, b);
        }

        void OnDrawGizmosSelected()
        {
            var r = Area;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(r.center, r.size);
        }
    }
}
