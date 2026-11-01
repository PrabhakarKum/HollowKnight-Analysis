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
        // Frames whose start-frame event we fired by hand; swallows Unity's own dispatch of the
        // same event so a listener never sees it twice. Cleared at the next Update.
        readonly HashSet<int> manualFired = new HashSet<int>();

        bool initialised;
        Animator animator;
        PlayableGraph graph;
        AnimationPlayableOutput output;
        AnimationClipPlayable playable;
        AnimationClip current;
        bool currentLoops;
        bool finished;

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
            foreach (var c in clips) Register(c);

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

        public void Play(string clip)
        {
            EnsureInit();
            if (current != null && !finished && current.name == clip && currentLoops) return;
            StartClip(clip, 0);
        }

        public void PlayFromFrame(string clip, int frame)
        {
            EnsureInit();
            StartClip(clip, frame);
        }

        public bool IsPlaying(string clip) =>
            current != null && !finished && current.name == clip;

        void StartClip(string clipName, int frame)
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

            current = clip;
            currentLoops = loopByName[clipName];
            finished = false;
            manualFired.Clear();

            playable.SetTime(frame / rate);
            graph.Evaluate(0f);

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
            manualFired.Clear();
            if (current == null || finished || !graph.IsValid()) return;

            // Step normally first so events between the old time and the end still fire.
            graph.Evaluate(Time.deltaTime);
            if (!currentLoops && playable.GetTime() >= current.length)
            {
                finished = true;
                Completed?.Invoke(current.name);
            }
        }

        // Target for AnimationEvents the converter adds to clips: function name "OnFrameEvent",
        // intParameter = frame index. Animator-driven events call methods on components of the
        // Animator's GameObject, which is why this lives on the same object as the Animator.
        public void OnFrameEvent(AnimationEvent e)
        {
            if (current == null) return;
            if (manualFired.Remove(e.intParameter)) return;
            FrameEvent?.Invoke(current.name, e.intParameter);
        }

        // Pooled objects get disabled and reused; stale state would make IsPlaying lie and block
        // Play of the same looping clip.
        void OnDisable()
        {
            current = null;
            finished = false;
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
