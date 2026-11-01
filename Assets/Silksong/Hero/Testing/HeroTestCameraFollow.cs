using UnityEngine;

namespace Hollowknight.Silksong
{
    // PLACEHOLDER: a minimal follow camera so the hero test scene is playable. The real GameCamera
    // (room bounds, look-ahead, shake) comes in Task 11 and replaces this.
    public class HeroTestCameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector2 offset = new Vector2(0f, 2f);
        [SerializeField] float smoothTime = 0.15f;

        Vector3 velocity;

        public Transform Target { get => target; set => target = value; }

        // LateUpdate so the camera moves after the hero has moved this frame (no one-frame lag).
        void LateUpdate()
        {
            if (target == null) return;
            // Keep the camera's own Z: a 2D camera must stay in front of the sprites.
            var goal = new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
        }
    }
}
