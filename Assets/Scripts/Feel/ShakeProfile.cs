using UnityEngine;

namespace Hollowknight.Feel
{
    /// <summary>How a shake behaves over time. Same three kinds as the original's CameraShakeProfile.</summary>
    public enum ShakeType
    {
        /// <summary>Random jitter that fades out over the duration (the decay curve). Ends by itself.</summary>
        Shake = 0,
        /// <summary>Constant random jitter that keeps going until it is cancelled (a long fall).</summary>
        Rumble = 1,
        /// <summary>Smooth curves in x and y (optionally mixed with random jitter); ends by itself if endAfterDuration is on.</summary>
        Sway = 2
    }

    /// <summary>
    /// One kind of camera shake. The math follows the original's CameraShakeProfile.GetOffset: a random point in a
    /// circle (picked once per frame) times the magnitude, scaled by the decay curve at progress = elapsed / duration.
    /// The numbers inside the presets (ShakePresets) are our own - the original keeps them in asset files.
    /// </summary>
    public class ShakeProfile
    {
        public string name;
        public ShakeType type = ShakeType.Shake;
        public float duration = 1f;
        public float magnitude = 1f;
        /// <summary>Camera freeze-frames (frames of 1/60 s the picture stops) when played with freeze-frames on. Ignored by sway.</summary>
        public int freezeFrames = 1;
        public AnimationCurve decay = AnimationCurve.Constant(0f, 1f, 1f);
        public AnimationCurve swayX = AnimationCurve.Constant(0f, 1f, 1f);
        public AnimationCurve swayY = AnimationCurve.Constant(0f, 1f, 1f);
        public bool useRandomShake;
        public bool endAfterDuration = true;

        int randomFrame = -1;
        Vector2 randomThisFrame;

        public int FreezeFrames => type == ShakeType.Sway ? 0 : freezeFrames;

        /// <summary>Can this shake end on its own? (Rumbles and open-ended sways cannot, so playing one twice does nothing.)</summary>
        public bool CanFinish => type == ShakeType.Shake || (type == ShakeType.Sway && endAfterDuration);

        public bool IsDone(float elapsed)
        {
            switch (type)
            {
                case ShakeType.Shake: return elapsed >= duration;
                case ShakeType.Sway: return endAfterDuration && elapsed >= duration;
                default: return false;
            }
        }

        public Vector2 GetOffset(float elapsed)
        {
            if (duration <= 0f) return Vector2.zero;
            float progress = elapsed / duration;
            if (Time.frameCount != randomFrame)
            {
                randomFrame = Time.frameCount;
                randomThisFrame = Random.insideUnitCircle;
            }
            switch (type)
            {
                case ShakeType.Shake: return randomThisFrame * (magnitude * decay.Evaluate(progress));
                case ShakeType.Rumble: return randomThisFrame * magnitude;
                case ShakeType.Sway:
                    if (!useRandomShake) return new Vector2(swayX.Evaluate(progress), swayY.Evaluate(progress)) * magnitude;
                    var r = randomThisFrame * magnitude;
                    return new Vector2(r.x * swayX.Evaluate(progress), r.y * swayY.Evaluate(progress));
            }
            return Vector2.zero;
        }
    }
}
