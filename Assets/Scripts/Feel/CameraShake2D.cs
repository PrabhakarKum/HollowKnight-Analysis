using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hollowknight.Feel
{
    /// <summary>
    /// Camera shake manager (the original's CameraShakeManager). Keeps a list of running shakes, adds their offsets
    /// together every frame (the sum is never longer than the strongest running shake), and multiplies by the
    /// accessibility setting. It only computes the offset; CameraFollow2D adds it on top of where the camera really is,
    /// so the shake never feeds back into the follow damping.
    /// Like the original, the picture does not move while the game is fully stopped (timeScale 0) or in camera freeze-frames.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraShake2D : MonoBehaviour
    {
        public enum Setting { On, Reduced, Off }

        [Tooltip("The accessibility option: On = full shake, Reduced = scaled by the value below, Off = none.")]
        [SerializeField] Setting shakeSetting = Setting.On;
        [Range(0f, 1f)] [SerializeField] float reducedMultiplier = 0.4f;

        struct Tracker { public ShakeProfile Profile; public float Elapsed; }

        public static CameraShake2D Instance { get; private set; }

        readonly List<Tracker> running = new List<Tracker>();
        Coroutine freezeFrameRoutine;
        double blockUntil;          // after CancelAll(block) finishable shakes are ignored for a moment (the original's 0.5 s guard)

        public Setting ShakeSetting { get => shakeSetting; set => shakeSetting = value; }
        public float Multiplier => shakeSetting == Setting.On ? 1f : shakeSetting == Setting.Reduced ? reducedMultiplier : 0f;
        public int ActiveCount => running.Count;
        public bool IsShaking => running.Count > 0;
        public bool InFreezeFrames => freezeFrameRoutine != null;
        /// <summary>What was added to the camera on the last frame (zero while the game is stopped).</summary>
        public Vector2 AppliedOffset { get; private set; }
        /// <summary>Largest offset length seen since ResetStats (for tests).</summary>
        public float MaxAppliedLength { get; private set; }
        public int PlayedCount { get; private set; }
        public int FreezeFramesStarted { get; private set; }

        void OnEnable() { Instance = this; }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            running.Clear();
            if (freezeFrameRoutine != null) { StopCoroutine(freezeFrameRoutine); freezeFrameRoutine = null; GameTime.CameraShakeScale = 1f; }
            AppliedOffset = Vector2.zero;
        }

        public void ResetStats() { MaxAppliedLength = 0f; PlayedCount = 0; FreezeFramesStarted = 0; }

        public bool IsPlaying(ShakeProfile profile)
        {
            for (int i = 0; i < running.Count; i++) if (running[i].Profile == profile) return true;
            return false;
        }

        // ------------------------------------------------------------------ playing

        public static void Do(ShakePreset preset, bool freezeFrames = false)
        {
            var p = ShakePresets.Get(preset);
            if (p != null && Instance != null) Instance.Play(p, freezeFrames);
        }

        public static void Stop(ShakePreset preset)
        {
            var p = ShakePresets.Get(preset);
            if (p != null && Instance != null) Instance.Cancel(p);
        }

        /// <summary>Starts a shake. A finishable shake can overlap itself; a rumble that is already running is not added twice.</summary>
        public void Play(ShakeProfile profile, bool freezeFrames = false)
        {
            if (profile == null) return;
            if (Time.timeAsDouble < blockUntil && profile.CanFinish) return;

            bool alreadyRunning = false;
            for (int i = 0; i < running.Count; i++)
                if (running[i].Profile == profile) { alreadyRunning = true; break; }
            if (!profile.CanFinish && alreadyRunning) return;

            running.Add(new Tracker { Profile = profile, Elapsed = 0f });
            PlayedCount++;

            if (freezeFrames && profile.FreezeFrames > 0)
            {
                if (freezeFrameRoutine != null) { StopCoroutine(freezeFrameRoutine); GameTime.CameraShakeScale = 1f; }
                FreezeFramesStarted++;
                freezeFrameRoutine = StartCoroutine(FreezeFramesRoutine(profile.FreezeFrames));
            }
        }

        public void Cancel(ShakeProfile profile)
        {
            for (int i = running.Count - 1; i >= 0; i--)
                if (running[i].Profile == profile) running.RemoveAt(i);
        }

        /// <summary>Stops every shake. With block, new finishable shakes are ignored for half a second (the original does this on a scene change).</summary>
        public void CancelAll(bool block = false)
        {
            running.Clear();
            if (block) blockUntil = Time.timeAsDouble + 0.5;
            AppliedOffset = Vector2.zero;
        }

        /// <summary>A hit-stop of its own: the camera time scale is 0 (so the whole game stops) for N frames of 1/60 s, in real time.</summary>
        IEnumerator FreezeFramesRoutine(int frames)
        {
            GameTime.CameraShakeScale = 0f;
            yield return new WaitForSecondsRealtime(frames * 0.016667f);
            GameTime.CameraShakeScale = 1f;
            freezeFrameRoutine = null;
        }

        // ------------------------------------------------------------------ evaluating

        /// <summary>Advance every shake by dt and return the offset to add to the camera this frame.</summary>
        public Vector2 Evaluate(float dt)
        {
            Vector2 sum = Vector2.zero;
            float strongest = 0f;
            for (int i = 0; i < running.Count; i++)
            {
                var t = running[i];
                t.Elapsed += dt;
                running[i] = t;
                sum += t.Profile.GetOffset(t.Elapsed);
                if (t.Profile.magnitude > strongest) strongest = t.Profile.magnitude;
            }
            if (sum.magnitude > strongest) sum = sum.normalized * strongest;
            for (int i = running.Count - 1; i >= 0; i--)
                if (running[i].Profile.IsDone(running[i].Elapsed)) running.RemoveAt(i);

            sum *= Multiplier;
            bool stopped = Time.timeScale <= float.Epsilon || freezeFrameRoutine != null;
            AppliedOffset = stopped ? Vector2.zero : sum;
            if (AppliedOffset.magnitude > MaxAppliedLength) MaxAppliedLength = AppliedOffset.magnitude;
            return AppliedOffset;
        }
    }
}
