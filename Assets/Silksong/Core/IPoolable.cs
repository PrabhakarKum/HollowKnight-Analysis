namespace Hollowknight.Silksong
{
    // Pooled objects are reused, so Awake/Start run once. Reset per-life state here instead.
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }
}
