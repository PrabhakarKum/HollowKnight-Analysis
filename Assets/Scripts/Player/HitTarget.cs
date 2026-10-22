using System;
using UnityEngine;
using Hollowknight.Feel;

namespace Hollowknight.Player
{
    /// <summary>
    /// Anything the player's slash can hit (an enemy, a breakable, a spike you can pogo off).
    /// Keep its collider a trigger so the player walks through it.
    /// </summary>
    public class HitTarget : MonoBehaviour
    {
        [Tooltip("A down-slash that hits this makes the player bounce.")]
        public bool pogo = true;
        [Tooltip("A sideways slash that hits this knocks the player back.")]
        public bool recoilsPlayer = true;
        [Tooltip("A nail hit on this gives the hero silk (the original has a per-hit 'does not give silk' option).")]
        public bool givesSilk = true;
        [SerializeField] SpriteRenderer flashRenderer;
        [SerializeField] Color flashColor = Color.white;
        [SerializeField] float flashTime = 0.1f;

        [Header("Death (0 = a dummy that never dies)")]
        [Tooltip("The target dies after this many hits.")]
        public int hitsToKill;
        [Tooltip("Hit-stop when it dies (the original's enemy death freeze).")]
        public FreezeType deathFreeze = FreezeType.EnemyDeath;
        [Tooltip("Camera shake when it dies.")]
        public ShakePreset deathShake = ShakePreset.EnemyKill;

        public int HitCount { get; private set; }
        public bool IsDead { get; private set; }
        public event Action<AttackDir> Hit;
        public event Action Died;

        Color baseColor;
        float flashUntil;

        void Awake()
        {
            if (flashRenderer == null) flashRenderer = GetComponentInChildren<SpriteRenderer>();
            if (flashRenderer != null) baseColor = flashRenderer.color;
        }

        public void TakeHit(AttackDir dir)
        {
            if (IsDead) return;
            HitCount++;
            flashUntil = Time.time + flashTime;
            Hit?.Invoke(dir);
            if (hitsToKill > 0 && HitCount >= hitsToKill) Die();
        }

        void Die()
        {
            IsDead = true;
            FreezeMoment.Start(deathFreeze);
            CameraShake2D.Do(deathShake, true);
            Died?.Invoke();
            foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
            foreach (var r in GetComponentsInChildren<SpriteRenderer>()) r.enabled = false;
        }

        void Update()
        {
            if (flashRenderer == null) return;
            flashRenderer.color = Time.time < flashUntil ? flashColor : baseColor;
        }
    }
}
