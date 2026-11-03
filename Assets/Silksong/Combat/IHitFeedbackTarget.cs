namespace Hollowknight.Silksong
{
    // Optional extra on an IHittable that tells the hero what a landed nail hit on it should feel like, so the hero
    // never has to know about enemy types. A target without it (a breakable wall, a training dummy) gives no silk and
    // no sideways or upward recoil. Study 14 section 4 ("hero feedback on hit").
    public interface IHitFeedbackTarget
    {
        // A damaging nail hit on this target gives the hero silk (false for the original's Shade / Armoured enemies).
        bool GivesSilk { get; }
        // A downward hit on this target bounces the hero (an active NonBouncer on it still wins and blocks the bounce).
        bool Bounces { get; }
    }
}
