namespace Hollowknight.Silksong
{
    // The original Platform class hides per-console differences (save backend, load behaviour).
    // Milestone 1 only ships on Windows desktop, so we expose just the settings the hero slice
    // reads and return one fixed answer for each.
    public interface IPlatform
    {
        // True: start loading the next room while the screen fades out, so the transition hides
        // the load. The original turns this off only for a debug cheat, so on a normal desktop
        // build it is always on, and we have no cheat to honour.
        bool FetchScenesBeforeFade { get; }
    }

    public static class Platform
    {
        sealed class DesktopStub : IPlatform
        {
            public bool FetchScenesBeforeFade => true;
        }

        static readonly IPlatform Stub = new DesktopStub();

        public static IPlatform Current => Stub;
    }
}
