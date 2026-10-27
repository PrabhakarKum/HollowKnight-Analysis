using UnityEngine;

namespace Hollowknight.Enemies
{
    /// <summary>The generated projectile / effect sprites one enemy uses (filled in by the arena builder from Assets/EnemyArt).</summary>
    public class EnemyArtSet : MonoBehaviour
    {
        [Tooltip("The projectile's frames (a bullet, an arrow, or the spinning blade).")]
        public Sprite[] projectile;
        public float projectileFps = 16f;
        public Sprite[] muzzleFlash;
        public Sprite[] impact;
        public float effectFps = 30f;
    }
}
