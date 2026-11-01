using System;
using UnityEngine;

namespace Hollowknight.Silksong
{
    // Hit points of a target. Takes the damage of each landed hit, raises Hit for every landed hit and Died
    // once when health reaches zero. A dead target refuses further hits.
    public class HealthManager : MonoBehaviour, IHittable
    {
        [SerializeField] int startHealth = 5;

        public int StartHealth => startHealth;
        public int Health { get; private set; }
        public bool IsDead { get; private set; }

        public event Action<HitInstance> Hit;
        public event Action Died;

        void Awake()
        {
            Health = startHealth;
        }

        // Code-built targets (tests, the training room) set the starting health before first use.
        public void SetStartHealth(int value)
        {
            startHealth = Mathf.Max(1, value);
            Health = startHealth;
            IsDead = false;
        }

        public void RestoreFull()
        {
            Health = startHealth;
            IsDead = false;
        }

        public bool TakeHit(HitInstance hit)
        {
            if (IsDead) return false;
            Health -= Mathf.Max(0, hit.Damage);
            Hit?.Invoke(hit);
            if (Health <= 0 && !IsDead)
            {
                Health = 0;
                IsDead = true;
                Died?.Invoke();
            }
            return true;
        }
    }
}
