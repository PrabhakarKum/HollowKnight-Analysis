using UnityEngine;

namespace Hollowknight.Silksong
{
    // World-unit facts about the hero's art and body in the original, so the scene builder, the importer and the
    // tests all use one set of numbers. NONE of these duplicate a HeroConfig value: HeroConfig holds how the hero
    // moves; this holds how big the art is. Sources are in docs/study/08-camera.md ("Units and scale").
    public static class SilksongUnits
    {
        // tk2d sprite collections store texelSize 0.015625 = 1/64: one texture pixel is 1/64 of a world unit.
        // Sprites imported with this PPU at scale 1 are exactly as big, in world units, as in the original, so
        // every distance in HeroConfig (RunSpeed 8.25 u/s, jump apex, dash 28 u/s for 0.1 s, attack boxes) means
        // the same thing here as there.
        public const float PixelsPerUnit = 64f;

        // Hero_Hornet.prefab: root scale (1,1,1), body collider BoxCollider2D (the prefab's component 61 on the root).
        public static readonly Vector2 HeroColliderSize = new Vector2(0.5f, 2.078899f);
        // Relative to the hero root. The root sits about 1.04 units above the feet; the sprite origin (tk2d 0,0)
        // is the root.
        public static readonly Vector2 HeroColliderOffset = new Vector2(0f, -0.51323676f);
        public const float HeroRootScale = 1f;

        // _GameCameras.prefab main camera orthographic size (16:9 view of about 30.97 x 17.42 units).
        public const float CameraOrthographicSize = 8.710664f;
    }

    // The animation names the hero code asks ISpriteAnimator for (HeroMovement, HeroDash, HeroWall, HeroAttack,
    // HeroHealth), plus the extra Silksong clips the importer also brings in. Clip assets are named with these.
    public static class HeroClipNames
    {
        public const string ImportFolder = "Assets/Silksong/Imported/HeroAnimations";

        // Played by the current hero code. All of these must exist as imported clips.
        public static readonly string[] PlayedByHeroCode =
        {
            "Idle", "Run", "Jump", "Fall", "Dash", "WallCling", "Slash", "SlashUp", "SlashDown", "Hurt", "Death"
        };

        // In the plan's list but not yet played by hero code (landing, wall slide, wall jump, alternate swing).
        public static readonly string[] ImportedForLater =
        {
            "Land", "WallSlide", "WallJump", "SlashAlt"
        };
    }
}
