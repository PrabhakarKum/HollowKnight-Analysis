namespace Hollowknight.Silksong
{
    // Which clip speeds the sprite animator accepts. A clip cannot play backwards or freeze through
    // this setting (pausing is done with timeScale), so anything unusable becomes normal speed.
    public static class AnimatorSpeedRules
    {
        public const float DefaultSpeed = 1f;

        public static float Sanitise(float speed)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f) return DefaultSpeed;
            return speed;
        }
    }
}
