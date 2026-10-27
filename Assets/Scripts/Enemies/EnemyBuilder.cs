#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Hollowknight.Player;

namespace Hollowknight.Enemies
{
    /// <summary>Palette and animation list for one enemy type (used by the art generator and the builder). Editor only.</summary>
    public class EnemyLook
    {
        public string name;
        public Color shadow, mid, light;      // the grey body is mapped onto this three-colour ramp
        public float accentHue;               // saturated pixels (the weapons' teal) are set to this hue (0..1)
        public string[] clips;                // pack animation clips this type needs
        public string[] projectileFolders;    // folders under Art/Sprites (Weapons/..., FX/...) copied and tinted
    }

    /// <summary>
    /// Builds enemies from the generated art (Hollowknight > Generate Enemy Variants) and the per-kind configs. Lives in the runtime assembly
    /// (editor only) so that both the menu items and the playtest bot can build enemies, in edit mode or in play mode.
    /// </summary>
    public static class EnemyBuilder
    {
        public const string RootName = "Enemy Test Arena";
        public const string ConfigDir = "Assets/ScriptableObjects/Enemies/";
        const float HalfHeight = 0.7f;

        static readonly string[] Common = { "Idle", "Walk", "Run", "JumpRise", "JumpMid", "JumpFall", "Land", "HitDamage", "Stunned", "Die" };

        public static readonly EnemyLook[] Looks =
        {
            new EnemyLook
            {
                name = "Gunner",
                shadow = new Color(0.06f, 0.16f, 0.10f), mid = new Color(0.22f, 0.58f, 0.32f), light = new Color(0.80f, 0.96f, 0.80f), accentHue = 0.09f,
                clips = Common.Concat(new[] { "GunWalk", "GunRun", "GunAim", "GunFire", "GunReload" }).ToArray(),
                projectileFolders = new[] { "Weapons/Bullet", "FX/MuzzleFlash", "FX/BulletImpact" },
            },
            new EnemyLook
            {
                name = "Archer",
                shadow = new Color(0.14f, 0.07f, 0.20f), mid = new Color(0.55f, 0.30f, 0.78f), light = new Color(0.93f, 0.84f, 0.99f), accentHue = 0.13f,
                clips = Common.Concat(new[] { "BowAim", "BowDraw", "BowFire" }).ToArray(),
                projectileFolders = new[] { "Weapons/Arrow", "FX/BulletImpact" },
            },
            new EnemyLook
            {
                name = "Brawler",
                shadow = new Color(0.22f, 0.06f, 0.05f), mid = new Color(0.80f, 0.28f, 0.14f), light = new Color(1.00f, 0.86f, 0.72f), accentHue = 0.58f,
                clips = Common.Concat(new[] { "Sprint", "PunchA", "PunchB", "KickA" }).ToArray(),
                projectileFolders = new string[0],
            },
            new EnemyLook
            {
                name = "BladeThrower",
                shadow = new Color(0.05f, 0.10f, 0.20f), mid = new Color(0.20f, 0.42f, 0.78f), light = new Color(0.82f, 0.90f, 1.00f), accentHue = 0.95f,
                clips = Common.Concat(new[] { "ThrowOverarm", "ThrowUnderarm" }).ToArray(),
                projectileFolders = new[] { "Weapons/BladeSpin", "Weapons/BladeThrow", "Weapons/BladeWallImpact" },
            },
        };

        /// <summary>Builds one enemy standing with its feet at the given position. Works in edit mode and in play mode.</summary>
        public static GameObject BuildEnemy(EnemyKind kind, Vector2 feet, Transform parent = null)
        {
            var cfg = GetConfig(kind);
            string artDir = "Assets/EnemyArt/" + kind + "/";
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(artDir + kind + ".controller");

            var go = new GameObject(NiceName(kind));
            if (parent != null) go.transform.SetParent(parent);
            go.transform.position = new Vector3(feet.x, feet.y + HalfHeight + 0.02f, 0f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            var body = go.AddComponent<BoxCollider2D>();
            body.size = new Vector2(0.7f, 1.4f);

            // the picture
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, -HalfHeight, 0f);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 4;
            var anim = visual.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            var idleSprite = Sprites(artDir + "Sprites/Idle").FirstOrDefault();
            if (idleSprite != null) sr.sprite = idleSprite;

            // damage: the body (if the kind hurts on touch) and the melee swing
            DamageSource2D contact = null, melee = null;
            if (cfg.contactDamage)
            {
                var c = new GameObject("Contact");
                c.transform.SetParent(go.transform, false);
                // as wide as the art (about +-0.25), so touching only hurts when the bodies really touch
                var cb = c.AddComponent<BoxCollider2D>(); cb.isTrigger = true; cb.size = new Vector2(0.5f, 1.3f);
                contact = c.AddComponent<DamageSource2D>(); contact.kind = DamageKind.Enemy; contact.damage = cfg.damage;
            }
            if (kind == EnemyKind.Brawler)
            {
                var m = new GameObject("MeleeHitbox");
                m.transform.SetParent(go.transform, false);
                var mb = m.AddComponent<BoxCollider2D>(); mb.isTrigger = true; mb.size = cfg.moves.Length > 0 ? cfg.moves[0].hitSize : new Vector2(0.65f, 0.7f);
                melee = m.AddComponent<DamageSource2D>(); melee.kind = DamageKind.Enemy; melee.damage = cfg.damage; melee.active = false;
            }

            // what the nail finds
            var target = go.AddComponent<HitTarget>();
            target.pogo = true; target.recoilsPlayer = true; target.givesSilk = true; target.hitsToKill = 0;
            var so = new SerializedObject(target);
            so.FindProperty("flashColor").colorValue = new Color(1f, 0.45f, 0.35f);
            so.FindProperty("flashRenderer").objectReferenceValue = sr;
            so.ApplyModifiedPropertiesWithoutUndo();

            // art for projectiles and effects
            var art = go.AddComponent<EnemyArtSet>();
            switch (kind)
            {
                case EnemyKind.Gunner:
                    art.projectile = Sprites(artDir + "Projectile/Bullet"); art.projectileFps = 20f;
                    art.muzzleFlash = Sprites(artDir + "FX/MuzzleFlash"); art.impact = Sprites(artDir + "FX/BulletImpact");
                    break;
                case EnemyKind.Archer:
                    art.projectile = Sprites(artDir + "Projectile/Arrow"); art.impact = Sprites(artDir + "FX/BulletImpact");
                    break;
                case EnemyKind.BladeThrower:
                    art.projectile = Sprites(artDir + "Projectile/BladeSpin"); art.projectileFps = 20f; art.impact = Sprites(artDir + "Projectile/BladeWallImpact");
                    break;
            }

            // behaviour (each part stores the config so it is saved with the scene)
            go.AddComponent<EnemyMotor2D>().Configure(cfg);
            var health = go.AddComponent<EnemyHealth2D>(); health.Configure(cfg);
            var brain = go.AddComponent<EnemyBrain2D>(); brain.Configure(cfg, melee, contact);
            go.AddComponent<EnemyVisuals2D>().Configure(cfg, anim, sr);
            return go;
        }

        public static string NiceName(EnemyKind k) => k == EnemyKind.BladeThrower ? "Blade Thrower" : k.ToString();

        static Sprite[] Sprites(string dir)
        {
            if (!AssetDatabase.IsValidFolder(dir)) return new Sprite[0];
            return AssetDatabase.FindAssets("t:Sprite", new[] { dir }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray();
        }

        // ------------------------------------------------------------------ configs

        public static EnemyConfig GetConfig(EnemyKind kind, bool reset = false)
        {
            Directory.CreateDirectory(Path.GetFullPath(ConfigDir));
            string path = ConfigDir + kind + ".asset";
            var cfg = AssetDatabase.LoadAssetAtPath<EnemyConfig>(path);
            bool isNew = cfg == null;
            if (isNew) { cfg = ScriptableObject.CreateInstance<EnemyConfig>(); AssetDatabase.CreateAsset(cfg, path); }
            if (isNew || reset)
            {
                Defaults(cfg, kind);
                EditorUtility.SetDirty(cfg);
                AssetDatabase.SaveAssets();
            }
            return cfg;
        }

        /// <summary>Fills a config with this kind's default numbers (what "Reset Enemy Configs To Defaults" writes). Public so tools can read the defaults without resetting anything.</summary>
        public static void Defaults(EnemyConfig c, EnemyKind kind)
        {
            var look = Looks.First(l => l.name == kind.ToString());
            // start from the class defaults every time so a reset really resets
            var fresh = ScriptableObject.CreateInstance<EnemyConfig>();
            EditorUtility.CopySerialized(fresh, c);
            Object.DestroyImmediate(fresh);
            c.name = kind.ToString();

            c.kind = kind;
            c.bodyMid = look.mid; c.bodyLight = look.light; c.accent = Color.HSVToRGB(look.accentHue, 0.8f, 1f);
            switch (kind)
            {
                case EnemyKind.Gunner:
                    c.maxHealth = 4; c.hitKnockback = new Vector2(4.5f, 3f);
                    c.walkSpeed = 2.0f; c.chaseSpeed = 3.5f; c.sightRange = 7f; c.sightHeight = 3.5f; c.patrolRange = 2f; c.patrolPause = 1.2f;
                    c.rangedMin = 4.5f; c.rangedMax = 8f;
                    c.telegraphTime = 0.6f; c.activeTime = 0.25f; c.recoverTime = 1.2f; c.cooldown = 0.3f;
                    c.projectileSpeed = 14f; c.burstCount = 3; c.burstGap = 0.16f; c.muzzleOffset = new Vector2(0.95f, 0.2f);
                    c.jumpForwardSpeed = 4f; c.maxJumpHeight = 3f; c.contactDamage = false;
                    c.walkClip = "GunWalk"; c.runClip = "GunRun"; c.aggroIdleClip = "GunAim";
                    c.telegraphClip = "GunAim"; c.attackClip = "GunFire"; c.recoverClip = "GunReload";
                    // GunFire (16 fps): frames 1-2 gun level, frame 3 (0.125 s) the recoil kick = the shot. Each bullet of the burst replays it from frame 2.
                    c.shot = new ShotMove { windupClip = "GunAim", windupClipLength = 0.0625f, windupTo = 0f, clip = "GunFire", clipLength = 0.3125f, releaseTime = 0.125f, leadIn = 0.0625f, speed = 1f };
                    break;
                case EnemyKind.Archer:
                    c.maxHealth = 3; c.hitKnockback = new Vector2(4.5f, 3f);
                    c.walkSpeed = 2.0f; c.chaseSpeed = 3.5f; c.sightRange = 9f; c.sightHeight = 3.8f; c.patrolRange = 1.5f; c.patrolPause = 1.4f;
                    c.rangedMin = 5f; c.rangedMax = 10f;
                    c.telegraphTime = 0.8f; c.activeTime = 0.25f; c.recoverTime = 0.9f; c.cooldown = 0.5f;
                    c.projectileSpeed = 15f; c.projectileGravity = 14f; c.muzzleOffset = new Vector2(0.9f, 0.35f);
                    c.jumpForwardSpeed = 4f; c.maxJumpHeight = 3f; c.contactDamage = false;
                    c.aggroIdleClip = "BowAim"; c.telegraphClip = "BowDraw"; c.attackClip = "BowFire"; c.recoverClip = "BowAim";
                    // BowDraw (0.44 s) is stretched over the telegraph up to its full-draw frame; BowFire (24 fps): frame 1 drawn, frame 2 (0.042 s) the release flash = the arrow leaves.
                    c.shot = new ShotMove { windupClip = "BowDraw", windupClipLength = 0.4375f, windupTo = 0.43f, clip = "BowFire", clipLength = 0.2083f, releaseTime = 0.0417f, leadIn = 0.0417f, speed = 1f };
                    break;
                case EnemyKind.Brawler:
                    c.maxHealth = 6; c.hitKnockback = new Vector2(3.5f, 2.5f); c.hurtTime = 0.25f;
                    c.walkSpeed = 2.4f; c.chaseSpeed = 5.2f; c.sightRange = 8f; c.sightHeight = 3.5f; c.patrolRange = 3f; c.patrolPause = 0.8f;
                    c.telegraphTime = 0.35f; c.recoverTime = 0.55f; c.cooldown = 0.4f;
                    c.jumpForwardSpeed = 6f; c.maxJumpHeight = 3.2f; c.contactDamage = true;
                    c.telegraphClip = "PunchA"; c.attackClip = "PunchA"; c.recoverClip = "Idle";
                    // Numbers measured from the generated art (32 px per unit, from the enemy's centre, facing right): the punch's fist is fully out
                    // on frame 3 (0.125 s) reaching 1.06; the kick's leg is out from frame 2 (0.0625 s) reaching 0.84, at head height. The hero's art is
                    // only +-0.22 wide inside its 0.8 collider, so each hitbox ends a little inside the limb tip and the move starts when the limb
                    // will overlap the hero's art by about a tenth of a unit.
                    c.moves = new[]
                    {
                        new MeleeMove { clip = "PunchA", clipLength = 0.375f, strikeTime = 0.125f, startRange = 1.15f, hitDelay = 0.04f, hitWindow = 0.08f, hitSpeed = 0.75f, swingSpeed = 1f, pullBack = 0.16f,
                                        limbReach = 1.06f, hitSize = new Vector2(0.65f, 0.7f), hitOffset = new Vector2(0.575f, -0.08f) },
                        new MeleeMove { clip = "KickA", clipLength = 0.5625f, strikeTime = 0.0625f, startRange = 0.95f, hitDelay = 0.05f, hitWindow = 0.12f, hitSpeed = 1.25f, swingSpeed = 1.25f, pullBack = 0.19f,
                                        limbReach = 0.84f, hitSize = new Vector2(0.52f, 0.6f), hitOffset = new Vector2(0.46f, 0.5f) },
                    };
                    break;
                case EnemyKind.BladeThrower:
                    c.maxHealth = 3; c.hitKnockback = new Vector2(4.5f, 3f);
                    c.walkSpeed = 2.2f; c.chaseSpeed = 3.5f; c.sightRange = 7f; c.sightHeight = 3.5f; c.patrolRange = 2.5f; c.patrolPause = 1f;
                    c.rangedMin = 4f; c.rangedMax = 8f;
                    c.telegraphTime = 0.4f; c.activeTime = 0.3f; c.recoverTime = 1.0f; c.cooldown = 0.4f;
                    c.projectileSpeed = 11f; c.boomerangDistance = 7f; c.muzzleOffset = new Vector2(0.7f, 0.3f);
                    c.jumpForwardSpeed = 4.5f; c.maxJumpHeight = 3f; c.contactDamage = false;
                    c.telegraphClip = "ThrowOverarm"; c.attackClip = "ThrowOverarm"; c.recoverClip = "Idle";
                    // ThrowOverarm (14 fps): frames 1-2 are the wind-up (played over the telegraph), frame 3 (0.143 s) is the throw itself = the blade leaves.
                    c.shot = new ShotMove { windupClip = "ThrowOverarm", windupClipLength = 0.357f, windupTo = 0.135f, clip = "ThrowOverarm", clipLength = 0.357f, releaseTime = 0.143f, leadIn = 0f, speed = 1f };
                    break;
            }
        }
    }
}
#endif
