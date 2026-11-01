using System;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // A target that never dies: counts the hits it takes and tops its health back up after each one.
    // Its collider should be a trigger so the hero can pass through it (same convention as the prototype).
    [RequireComponent(typeof(HealthManager))]
    public class TrainingDummy : MonoBehaviour
    {
        HealthManager health;

        public int HitCount { get; private set; }
        public event Action<HitInstance> Hit;

        void Awake()
        {
            health = GetComponent<HealthManager>();
        }

        void OnEnable()
        {
            if (health == null) health = GetComponent<HealthManager>();
            health.Hit += OnHealthHit;
        }

        void OnDisable()
        {
            if (health != null) health.Hit -= OnHealthHit;
        }

        void OnHealthHit(HitInstance hit)
        {
            HitCount++;
            Hit?.Invoke(hit);
            // Refill last so a handler sees the damage that was just dealt, then the dummy stands again.
            health.RestoreFull();
        }
    }
}
