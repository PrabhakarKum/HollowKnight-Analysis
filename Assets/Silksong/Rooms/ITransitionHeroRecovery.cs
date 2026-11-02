namespace Hollowknight.Silksong
{
    // Optional extra for the hero side: give control back after a transition failed or was cancelled.
    // ITransitionHero (frozen) has no such call, and EnterRoom needs a gate, which may not exist after a
    // failed load. A hero that does not implement this stays locked if no sending gate survives.
    public interface ITransitionHeroRecovery
    {
        void RestoreControl();
    }
}
