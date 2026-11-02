namespace Hollowknight.Silksong
{
    // The one place that hands the hero its injected pieces. HeroController.Config / Input / Animator are
    // code-injected (not serialized), so every bootstrap (the single-room HeroSceneBootstrap and the persistent
    // RoomWorldBootstrap) must do the same thing. Kept here so the two cannot drift apart.
    // The config is only ever a REFERENCE to Hero/HeroConfig.asset, never a copy.
    public static class HeroWiring
    {
        public static void Inject(HeroController hero, HeroConfig config, IHeroInput input, ISpriteAnimator animator)
        {
            hero.Config = config;
            hero.Input = input;
            if (animator != null) hero.Animator = animator;
        }
    }
}
