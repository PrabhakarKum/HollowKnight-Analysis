using UnityEngine;

namespace Hollowknight.Silksong
{
    // What a gate should do about the hero touching it.
    public enum TransitionDecision
    {
        Ignore,     // do nothing at all
        PushBack,   // hero faces the wrong way or is busy: stop it and move it out of the gate box
        Leave       // start the room change
    }

    // The few facts about the hero that the gate rules need. Filled by the hero side (ITransitionHero) so the
    // rules never read hero internals or hero tuning numbers.
    public struct HeroTransitionState
    {
        public bool FacingRight;
        // Walking backwards while sprinting: moves against the way it faces, so "outward" is reversed.
        public bool BackSprinting;
        // Binding, recoiling or in a scripted cutscene move: never allowed through a gate.
        public bool Busy;
        public float VelocityY;
        // True once the hero finished being placed in the room (needed for the bottom-gate exception).
        public bool InPosition;
    }

    // Pure decision logic for a hero touching a gate, ported from TransitionPoint.TryDoTransition
    // (decompiled TransitionPoint.cs lines 194-292). Kept free of scene objects so it is unit tested.
    public static class TransitionRules
    {
        public static TransitionDecision Evaluate(GatePosition gate, GameState state, bool transitionRunning,
            bool hasTarget, HeroTransitionState hero)
        {
            // A second touch while the first transition runs would start a second load (GameManager ignores
            // it too, line 629). Checked first so a walking-out hero is not pushed back either.
            if (transitionRunning) return TransitionDecision.Ignore;

            if (state == GameState.EnteringLevel)
            {
                // The one exception: arriving through a bottom gate by falling, the hero may leave straight
                // back out of it.
                if (gate != GatePosition.Bottom || !hero.InPosition || hero.VelocityY >= 0f)
                    return TransitionDecision.Ignore;
            }
            else if (state != GameState.Playing)
            {
                return TransitionDecision.Ignore;
            }

            // Doors are opened by an interact press, not by touching (stubbed in this milestone).
            if (gate == GatePosition.Door) return TransitionDecision.Ignore;

            bool wrongWay = false;
            if (hero.Busy)
            {
                wrongWay = true;
            }
            else if (gate == GatePosition.Right)
            {
                // Outward is to the right. Back-sprinting flips the meaning of facing.
                wrongWay = hero.FacingRight == hero.BackSprinting;
            }
            else if (gate == GatePosition.Left)
            {
                wrongWay = hero.FacingRight != hero.BackSprinting;
            }
            // Top and bottom: the original has no facing test, only the busy test above.

            // Unknown has no side to push away from (the original's push-back only covers the four sides).
            if (wrongWay && gate != GatePosition.Unknown) return TransitionDecision.PushBack;

            // Without both target scene and entry gate the original does nothing (after push-back).
            return hasTarget ? TransitionDecision.Leave : TransitionDecision.Ignore;
        }

        // How far to translate the hero so it sits just outside the gate box on the side it came from.
        public static Vector2 PushBackOffset(GatePosition gate, Bounds gateBounds, Bounds heroBounds)
        {
            switch (gate)
            {
                case GatePosition.Right: return new Vector2(gateBounds.min.x - heroBounds.max.x, 0f);
                case GatePosition.Left: return new Vector2(gateBounds.max.x - heroBounds.min.x, 0f);
                case GatePosition.Top: return new Vector2(0f, gateBounds.min.y - heroBounds.max.y);
                case GatePosition.Bottom: return new Vector2(0f, gateBounds.max.y - heroBounds.min.y);
                default: return Vector2.zero;
            }
        }
    }
}
