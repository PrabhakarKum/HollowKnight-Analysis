using UnityEngine;

namespace Hollowknight.Silksong
{
    // Pure rules for the nail swipe drawn on a swing (numbers from docs/study/15-attack-visuals.md, sections 1 and 9).
    // The effect art is drawn facing LEFT. The original flips it with the hero's scale; our hero flips art with
    // flipX, so the effect uses flipX and an offset whose sign follows the swing side.
    // Purely visual: nothing here touches the hitbox.
    public static class HeroSlashEffectRules
    {
        // Imported clip names (see HeroAnimationMap).
        public const string SideClip = "SlashEffect";
        public const string UpClip = "SlashUpEffect";
        public const string DownClip = "SlashDownEffect";

        // Drawn just in front of the body (which sits at z 0), and one sorting step above the body renderer.
        public const float LocalZ = -0.001f;
        public const int SortingOrderAboveBody = 1;

        public const float WallOffsetX = 0.61f;
        public const float DownOffsetX = 0.69f;
        public const float DownOffsetY = -0.37f;

        public static string ClipFor(AttackDirection dir)
        {
            switch (dir)
            {
                case AttackDirection.Up: return UpClip;
                case AttackDirection.Down: return DownClip;
                default: return SideClip;
            }
        }

        // goesRight: the swing side for a sideways swing, the hero's facing for up and down.
        // onWall: the sideways swing of a wall cling (offset 0.61 toward the swing side).
        public static Vector3 LocalPosition(AttackDirection dir, bool goesRight, bool onWall)
        {
            float sign = goesRight ? 1f : -1f;
            switch (dir)
            {
                case AttackDirection.Up:
                    return new Vector3(0f, 0f, LocalZ);
                case AttackDirection.Down:
                    // Native (facing left) the offset is +0.69; facing right it is mirrored to -0.69.
                    return new Vector3(-sign * DownOffsetX, DownOffsetY, LocalZ);
                default:
                    return new Vector3(onWall ? sign * WallOffsetX : 0f, 0f, LocalZ);
            }
        }

        public static Vector3 LocalScale(AttackDirection dir)
        {
            switch (dir)
            {
                case AttackDirection.Up: return new Vector3(1f, 0.9f, 1f);
                case AttackDirection.Down: return new Vector3(1.2f, 1.2f, 1f);
                default: return new Vector3(0.9f, 1f, 1f);
            }
        }

        // The art faces left, so it is mirrored when the swing / hero goes right.
        public static bool FlipX(bool goesRight) => goesRight;
    }
}
