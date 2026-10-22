using UnityEngine;

namespace Hollowknight.Player
{
    /// <summary>A kinematic test enemy that walks back and forth between two x positions.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class SimplePatrol2D : MonoBehaviour
    {
        public float leftX = -4f, rightX = -1f, speed = 2.5f;
        int dir = 1;
        Rigidbody2D rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        void FixedUpdate()
        {
            var p = rb.position;
            p.x += dir * speed * Time.fixedDeltaTime;
            if (p.x >= rightX) { p.x = rightX; dir = -1; }
            else if (p.x <= leftX) { p.x = leftX; dir = 1; }
            rb.MovePosition(p);
        }
    }
}
