using UnityEngine;

namespace Hollowknight.Silksong
{
    // The nail swipe: a child sprite with its own animator, shown at swing start and hidden when its clip completes
    // or the swing is cut short. Built by HeroAttack (hero art is assembled in several places, so the effect does not
    // depend on any of them). It never touches the hitbox. Rules: HeroSlashEffectRules.
    public class HeroSlashEffect : MonoBehaviour
    {
        UnitySpriteAnimator animator;
        SpriteRenderer sprite;

        public bool IsShown => sprite != null && sprite.enabled;
        public string CurrentClip { get; private set; }
        public SpriteRenderer Renderer => sprite;

        // Null (and a stub line) when the hero has no imported effect clips, e.g. the plain-box hero.
        public static HeroSlashEffect TryCreate(Transform parent, ISpriteAnimator bodyAnimator)
        {
            var body = bodyAnimator as UnitySpriteAnimator;
            if (body == null)
            {
                Stubs.NotImplemented("Hero nail swipe effect needs imported effect clips (no effect for this hero)");
                return null;
            }
            var clips = new AnimationClip[3];
            string[] names = { HeroSlashEffectRules.SideClip, HeroSlashEffectRules.UpClip, HeroSlashEffectRules.DownClip };
            for (int i = 0; i < names.Length; i++)
            {
                if (!body.TryGetClip(names[i], out clips[i]))
                {
                    Stubs.NotImplemented("Hero nail swipe effect needs imported effect clips (no effect for this hero)");
                    return null;
                }
            }

            // The alternate swipe is optional: without it the first swipe is used for both swings.
            body.TryGetClip(HeroSlashEffectRules.AltClip, out var altClip);

            var go = new GameObject("NailSwipeEffect");
            go.transform.SetParent(parent, false);
            var effect = go.AddComponent<HeroSlashEffect>();
            effect.sprite = go.AddComponent<SpriteRenderer>();
            go.AddComponent<Animator>();
            effect.animator = go.AddComponent<UnitySpriteAnimator>();
            foreach (var c in clips) effect.animator.Register(c);
            effect.animator.Register(altClip);

            var bodyRenderer = body.GetComponent<SpriteRenderer>();
            if (bodyRenderer != null)
            {
                effect.sprite.sortingLayerID = bodyRenderer.sortingLayerID;
                effect.sprite.sortingOrder = bodyRenderer.sortingOrder + HeroSlashEffectRules.SortingOrderAboveBody;
            }
            effect.sprite.enabled = false;
            effect.animator.Completed += effect.OnClipCompleted;
            return effect;
        }

        public void Show(AttackDirection dir, bool goesRight, bool onWall, bool alternate = false)
        {
            string clip = HeroSlashEffectRules.ClipFor(dir, alternate);
            if (clip != HeroSlashEffectRules.ClipFor(dir) && !animator.TryGetClip(clip, out _)) clip = HeroSlashEffectRules.ClipFor(dir);
            transform.localPosition = HeroSlashEffectRules.LocalPosition(dir, goesRight, onWall);
            transform.localScale = HeroSlashEffectRules.LocalScale(dir);
            sprite.flipX = HeroSlashEffectRules.FlipX(goesRight);
            CurrentClip = clip;
            animator.Play(clip);   // a non-looping clip always restarts from frame 0
            sprite.enabled = true;
        }

        public void Hide()
        {
            if (sprite != null) sprite.enabled = false;
            CurrentClip = null;
        }

        void OnClipCompleted(string clip)
        {
            if (clip == CurrentClip) Hide();
        }
    }
}
