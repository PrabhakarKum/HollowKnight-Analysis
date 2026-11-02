namespace Hollowknight.Silksong
{
    // Coarse state of the game loop. Gates only fire in Playing; the transition manager moves the game
    // through ExitingLevel and EnteringLevel. The original also has INACTIVE, MAIN_MENU and PRIMER, which
    // nothing in the room-transition flow needs, so they are left out until a menu exists.
    public enum GameState
    {
        Loading,
        EnteringLevel,
        Playing,
        Paused,
        ExitingLevel,
        Cutscene
    }
}
