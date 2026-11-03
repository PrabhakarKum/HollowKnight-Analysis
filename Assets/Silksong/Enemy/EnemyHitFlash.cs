using UnityEngine;

namespace Hollowknight.Silksong
{
    // White flash on a hit: strength 0.85 held 0.07 s, then falling to 0 over 0.05 s (study 14, row 21).
    // Placeholder look: the sprite colour is blended toward white by the flash strength. A real white fill needs a
    // flash shader (plain sprite tint can only darken), which is stubbed. Does nothing without a SpriteRenderer.
    // Time comes from Update unless ManualStepping is on; tests then call Advance themselves.
    public class EnemyHitFlash : MonoBehaviour
    {
        EnemyHealth health;
        SpriteRenderer[] renderers;
        Color[] baseColors;
        float elapsed;
        bool flashing;

        public bool ManualStepping { get; set; }
        public bool IsFlashing => flashing;
        public float CurrentStrength { get; private set; }
        public int FlashCount { get; private set; }

        float Hold => health != null && health.Config != null ? health.Config.flashHoldSeconds : SilksongEnemyConfigDefaults.FlashHoldSeconds;
        float Fall => health != null && health.Config != null ? health.Config.flashFallSeconds : SilksongEnemyConfigDefaults.FlashFallSeconds;
        float Strength => health != null && health.Config != null ? health.Config.flashStrength : SilksongEnemyConfigDefaults.FlashStrength;

        void Awake()
        {
            health = GetComponent<EnemyHealth>();
            renderers = GetComponentsInChildren<SpriteRenderer>();
        }

        void OnEnable()
        {
            if (health == null) health = GetComponent<EnemyHealth>();
            if (renderers == null) renderers = GetComponentsInChildren<SpriteRenderer>();
            if (health == null) return;
            health.Hit += OnHit;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            if (health != null)
            {
                health.Hit -= OnHit;
                health.Died -= OnDied;
            }
            Restore();
        }

        void OnHit(HitInstance _) => StartFlash();

        void OnDied() => Restore();

        public void StartFlash()
        {
            if (renderers == null || renderers.Length == 0) return;
            if (!flashing)
            {
                baseColors = new Color[renderers.Length];
                for (int i = 0; i < renderers.Length; i++)
                    baseColors[i] = renderers[i] != null ? renderers[i].color : Color.white;
            }
            Stubs.NotImplemented("Enemy hit flash white-fill shader");
            flashing = true;
            elapsed = 0f;
            FlashCount++;
            Apply();
        }

        void Update()
        {
            if (!ManualStepping) Advance(Time.deltaTime);
        }

        public void Advance(float dt)
        {
            if (!flashing) return;
            elapsed += dt;
            if (elapsed >= EnemyRules.FlashDuration(Hold, Fall)) Restore();
            else Apply();
        }

        void Apply()
        {
            CurrentStrength = EnemyRules.FlashStrength(elapsed, Hold, Fall, Strength);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].color = Color.Lerp(baseColors[i], Color.white, CurrentStrength);
        }

        void Restore()
        {
            if (flashing && renderers != null)
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null) renderers[i].color = baseColors[i];
            flashing = false;
            CurrentStrength = 0f;
        }
    }
}
