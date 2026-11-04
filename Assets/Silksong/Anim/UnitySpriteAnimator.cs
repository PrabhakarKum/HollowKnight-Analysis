using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Hollowknight.Silksong
{
    // ISpriteAnimator over an Animator that drives a SpriteRenderer.
    // Clips are looked up by name and played through one PlayableGraph on the Animator, rather
    // than through an AnimatorController, so clips can be registered at runtime (tests build
    // them in code) and PlayFromFrame can seek without needing a state per clip.
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Animator))]
    public class UnitySpriteAnimator : MonoBehaviour, ISpriteAnimator
    {
        [SerializeField] AnimationClip[] clips = Array.Empty<AnimationClip>();

        // Graphs are native objects; tests assert this does not grow with every Play call.
        public static int LiveGraphCount { get; private set; }

        readonly Dictionary<string, AnimationClip> clipsByName = new Dictionary<string, AnimationClip>();
        // Read once at Register time so playback never depends on re-querying clip settings.
        readonly Dictionary<string, bool> loopByName = new Dictionary<string, bool>();
        // Frames whose start-frame event we fired by hand. Unity may dispatch its own copy of that
        // event in the first step after the start; OnFrameEvent swallows it once. Cleared after that step.
        readonly HashSet<int> manualFired = new HashSet<int>();

        bool initialised;
        Animator animator;
        PlayableGraph graph;
        AnimationPlayableOutput output;
        AnimationClipPlayable playable;
        AnimationClip current;
        bool currentLoops;
        bool finished;
        float currentSpeed = AnimatorSpeedRules.DefaultSpeed;
        // True only inside the seek Evaluate(0); anything Unity dispatches then is covered by our hand-fired events.
        bool seeking;

        public event Action<string> Completed;
        public event Action<string, int> FrameEvent;

        void Awake() => EnsureInit();

        // Lazy so Register/Play work even when called before Awake (e.g. on an inactive object
        // that was just created or taken from a pool).
        void EnsureInit()
        {
            if (initialised) return;
            initialised = true;
            animator = GetComponent<Animator>();
            foreach (var c in clips) if (c != null && !clipsByName.ContainsKey(c.name)) Register(c); // runtime-registered clips win

            graph = PlayableGraph.Create("UnitySpriteAnimator");
            LiveGraphCount++;
            // Manual mode: we advance in Update ourselves so a seek is evaluated at once and the
            // sprite is correct the same frame PlayFromFrame returns. Time.deltaTime is scaled,
            // so timeScale = 0 (pause, hit-stop) freezes animation, which is what we want.
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            output = AnimationPlayableOutput.Create(graph, "Sprite", animator);
        }

        // Clips are keyed by AnimationClip.name; the converter must name clips after the tk2d clip.
        public void Register(AnimationClip clip)
        {
            if (clip == null) return;
            clipsByName[clip.name] = clip;
            loopByName[clip.name] = clip.isLooping;
        }

        // A clip registered under this name (or listed in the serialized array), for code that builds a second
        // animator from the same clips (the nail swipe effect).
        public bool TryGetClip(string clipName, out AnimationClip clip)
        {
            EnsureInit();
            return clipsByName.TryGetValue(clipName, out clip);
        }

        public void Play(string clip) => Play(clip, AnimatorSpeedRules.DefaultSpeed);

        // Speed is a multiple of the clip's own rate. The no-restart cases below still adopt the requested
        // speed, so a plain Play(name) always brings a running clip back to speed 1.
        public void Play(string clip, float speed)
        {
            EnsureInit();
            speed = AnimatorSpeedRules.Sanitise(speed);
            if (current != null && !finished)
            {
                // A clip with a registered "<name> Loop" continuation (a tk2d LoopSection clip) is one animation in
                // two parts: repeating Play(name) while either part is current must not restart it.
                bool keepRunning = (current.name == clip && currentLoops) ||
                    (HasLoopPart(clip) && (current.name == clip || current.name == clip + LoopSuffix));
                if (keepRunning)
                {
                    SetSpeed(speed);
                    return;
                }
            }
            StartClip(clip, 0, speed);
        }

        // Speed of the clip now playing (1 when nothing is).
        public float CurrentSpeed => currentSpeed;

        void SetSpeed(float speed)
        {
            currentSpeed = speed;
            if (playable.IsValid()) playable.SetSpeed(speed);
        }

        public void PlayFromFrame(string clip, int frame)
        {
            EnsureInit();
            StartClip(clip, frame, AnimatorSpeedRules.DefaultSpeed);
        }

        // Playing "Jump" counts while its "Jump Loop" continuation runs.
        public bool IsPlaying(string clip) =>
            current != null && !finished && (current.name == clip || (HasLoopPart(clip) && current.name == clip + LoopSuffix));

        // Suffix the converter gives the looping part of a LoopSection clip.
        public const string LoopSuffix = " Loop";

        bool HasLoopPart(string name) => clipsByName.ContainsKey(name + LoopSuffix);

        void StartClip(string clipName, int frame, float speed)
        {
            if (!clipsByName.TryGetValue(clipName, out var clip))
            {
                Debug.LogWarning($"[UnitySpriteAnimator] Unknown clip '{clipName}' on {name}");
                return;
            }
            float rate = clip.frameRate;
            if (rate <= 0f)
            {
                Debug.LogError($"[UnitySpriteAnimator] Clip '{clipName}' has no frame rate");
                return;
            }
            int frameCount = Mathf.Max(1, Mathf.RoundToInt(clip.length * rate));
            if (frame < 0 || frame >= frameCount)
            {
                Debug.LogError($"[UnitySpriteAnimator] Frame {frame} out of range 0..{frameCount - 1} for '{clipName}'");
                return;
            }

            // Reuse the graph; only the clip playable is swapped.
            if (playable.IsValid()) playable.Destroy();
            playable = AnimationClipPlayable.Create(graph, clip);
            output.SetSourcePlayable(playable);
            SetSpeed(speed);

            current = clip;
            currentLoops = loopByName[clipName];
            finished = false;
            manualFired.Clear();

            playable.SetTime(frame / rate);
            seeking = true;
            graph.Evaluate(0f);
            seeking = false;

            // A zero-length step does not reliably dispatch events sitting exactly on the start
            // time, so fire those ourselves. Rule: events on the start frame fire, earlier ones don't.
            foreach (var e in clip.events)
            {
                if (e.functionName != nameof(OnFrameEvent)) continue;
                if (Mathf.RoundToInt(e.time * rate) != frame) continue;
                manualFired.Add(e.intParameter);
                FrameEvent?.Invoke(clip.name, e.intParameter);
            }
        }

        void Update()
        {
            if (current == null || finished || !graph.IsValid()) return;

            // Step normally first so events between the old time and the end still fire.
            graph.Evaluate(Time.deltaTime);
            // Only the first step after a start can carry Unity's copy of a start-frame event.
            manualFired.Clear();
            if (!currentLoops && playable.GetTime() >= current.length)
            {
                // End of an intro: continue straight into its loop part. Completed is NOT raised for the intro;
                // it is raised only when a non-looping clip ends with no continuation.
                string next = current.name + LoopSuffix;
                if (clipsByName.ContainsKey(next)) { StartClip(next, 0, currentSpeed); return; }
                finished = true;
                Completed?.Invoke(current.name);
            }
        }

        // Target for AnimationEvents the converter adds to clips: function name "OnFrameEvent",
        // intParameter = frame index. Animator-driven events call methods on components of the
        // Animator's GameObject, which is why this lives on the same object as the Animator.
        public void OnFrameEvent(AnimationEvent e)
        {
            if (current == null || seeking) return;
            if (manualFired.Remove(e.intParameter)) return;
            FrameEvent?.Invoke(current.name, e.intParameter);
        }

        // Pooled objects get disabled and reused; stale state would make IsPlaying lie and block
        // Play of the same looping clip.
        void OnDisable()
        {
            current = null;
            finished = false;
            currentSpeed = AnimatorSpeedRules.DefaultSpeed;
            manualFired.Clear();
        }

        void OnDestroy()
        {
            if (!initialised) return;
            if (graph.IsValid())
            {
                graph.Destroy();
                LiveGraphCount--;
            }
        }
    }
}
