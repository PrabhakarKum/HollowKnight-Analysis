namespace Hollowknight.Silksong
{
    // Anything the needle can hit. Returns whether the hit landed (false for a dead or invulnerable target),
    // so the attacker can decide on feedback such as the pogo.
    public interface IHittable
    {
        bool TakeHit(HitInstance hit);
    }
}
