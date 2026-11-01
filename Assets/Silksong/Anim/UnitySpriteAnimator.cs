using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Hollowknight.Silksong
{
    // ISpriteAnimator over an Animator that drives a SpriteRenderer.
    // Clips are looked up by name and played through a PlayableGraph on the Animator, rather
    // than through an AnimatorController, so clips can be registered at runtime (tests build
    // them in code) and PlayFromFrame can seek without needing a state per clip.
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Animator))]
    public class UnitySpriteAnimator : MonoBehaviour, ISpriteAnimator
    {
        [SerializeField] AnimationClip[] clips = Array.Empty<AnimationClip>();

        readonly Dictionary<string, AnimationClip> clipsByName = new Dictionary<string, AnimationClip>();
        Animator animator;
        PlayableGraph graph;
        AnimationClipPlayable playable;
        AnimationClip current;
        bool finished;

        public event Action<string> Completed;
        public event Action<string, int> FrameEvent;

        void Awake()
        {
            animator = GetComponent<Animator>();
            foreach (var c in clips) Register(c);
        }

        // Clips are keyed by AnimationClip.name; the converter must name clips after the tk2d clip.
        public void Register(AnimationClip clip)
        {
            if (clip != null) clipsByName[clip.name] = clip;
        }

        public void Play(string clip) => Start(clip, 0);

        public void PlayFromFrame(string clip, int frame) => Start(clip, frame);

        public bool IsPlaying(string clip) =>
            current != null && !finished && current.name == clip;

        void Start(string clipName, int frame)
        {
            if (!clipsByName.TryGetValue(clipName, out var clip))
            {
                Debug.LogWarning($"[UnitySpriteAnimator] Unknown clip '{clipName}' on {name}");
                return;
            }
            if (animator == null) animator = GetComponent<Animator>();

            if (graph.IsValid()) graph.Destroy();
            graph = PlayableGraph.Create("UnitySpriteAnimator");
            // Manual mode: we advance in Update ourselves so a seek can be evaluated at once and
            // the sprite is correct the same frame PlayFromFrame returns.
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "Sprite", animator);
            playable = AnimationClipPlayable.Create(graph, clip);
            output.SetSourcePlayable(playable);

            current = clip;
            finished = false;
            float rate = clip.frameRate > 0f ? clip.frameRate : 12f;
            playable.SetTime(Mathf.Max(0, frame) / rate);
            graph.Evaluate(0f);
        }

        void Update()
        {
            if (!graph.IsValid() || finished) return;

            double next = playable.GetTime() + Time.deltaTime;
            bool loops = current.isLooping;
            if (!loops && next >= current.length)
            {
                // Clamp so the last frame stays on screen and events up to the end still fire.
                playable.SetTime(current.length);
                graph.Evaluate(0f);
                finished = true;
                Completed?.Invoke(current.name);
                return;
            }
            // Evaluate takes a delta, so seat the playable at the old time and step forward.
            graph.Evaluate(Time.deltaTime);
        }

        // Target for AnimationEvents the converter adds to clips: function name "OnFrameEvent",
        // intParameter = frame index, stringParameter = optional tk2d event name (unused here).
        // Animator-driven events call methods on components of the Animator's GameObject, which is
        // why this lives on the same object as the Animator.
        public void OnFrameEvent(AnimationEvent e)
        {
            if (current == null) return;
            FrameEvent?.Invoke(current.name, e.intParameter);
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
