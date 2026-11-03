namespace Hollowknight.Silksong
{
    // When the hero plays its "Turn" clip. Purely cosmetic: facing, movement and the hitbox never wait for it.
    // Silksong plays it when the hero is on the ground and its facing reverses, unless the hero was wall sliding
    // (or, here, clinging) on the step before. Walk zones (TurnWalk) do not exist in this project.
    public static class HeroTurnRules
    {
        public static bool ShouldPlayTurn(bool facingReversed, HeroState state, HeroState previousState, bool controlLocked)
        {
            if (!facingReversed) return false;
            if (state != HeroState.Grounded) return false;
            if (previousState == HeroState.WallClinging) return false;
            // A scripted room-transition move sets its own facing; it is not a player turn.
            if (controlLocked) return false;
            return true;
        }

        // Idle/Run requests wait while our own Turn is still playing; everything else is decided by the caller
        // before this is asked (airborne, dash, swing, hurt and death never reach it).
        public static bool ShouldHoldIdleOrRun(bool turning, bool turnClipPlaying) => turning && turnClipPlaying;
    }
}
