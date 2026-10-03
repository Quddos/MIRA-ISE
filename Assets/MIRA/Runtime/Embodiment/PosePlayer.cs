// Phase 5C-18: deterministic pose playback -- frame 0, 1, ... N-1 of the frozen clip.
//
// The player is a frame cursor and nothing more. It never interpolates between frames, never resamples,
// never blends and never advances by wall-clock time: playback speed scales how long a frame is held,
// not which poses are shown. For the same clip and the same frame the applied pose is identical, which
// is what the pose-state digest records.
//
// MotionData -> PosePlayer -> Visualization. A renderer (desktop today, VR later) observes the player;
// the player never depends on a renderer.
using System;
using UnityEngine;

namespace Mira.Embodiment
{
    public class PosePlayer : System.IDisposable
    {
        public Soma30MotionClip Clip { get; private set; }
        public Soma30PositionEmbodiment Body { get; private set; }
        public int Frame { get; private set; }
        public bool Paused { get; set; }
        public float Speed { get; set; } = 1f;
        public bool Loop { get; set; }

        public event Action<int> FrameChanged;

        readonly Transform parent;
        readonly bool withVisuals;
        readonly Color jointColor, boneColor;
        float accumulator;

        public PosePlayer(Soma30MotionClip clip, Transform parent, bool withVisuals, Color jointColor, Color boneColor)
        {
            this.parent = parent;
            this.withVisuals = withVisuals;
            this.jointColor = jointColor;
            this.boneColor = boneColor;
            Load(clip);
        }

        /// Case and executor switching: load a different frozen clip. The underlying data is never
        /// modified, and the two clips are never blended or aligned to one another.
        public void Load(Soma30MotionClip clip)
        {
            Body?.Dispose();
            Clip = clip ?? throw new ArgumentNullException(nameof(clip));
            Body = new Soma30PositionEmbodiment(clip, parent, withVisuals, jointColor, boneColor);
            Frame = 0;
            accumulator = 0f;
            Body.ApplyFrame(0);
            FrameChanged?.Invoke(0);
        }

        public bool AtEnd => Frame >= Clip.n_frames - 1;

        public void Reset()
        {
            Frame = 0;
            accumulator = 0f;
            Body.ApplyFrame(0);
            FrameChanged?.Invoke(0);
        }

        /// Step exactly one frozen frame forward. Returns false at the end of a non-looping clip.
        public bool StepForward()
        {
            if (AtEnd)
            {
                if (!Loop) return false;
                Frame = 0;
            }
            else Frame++;
            Body.ApplyFrame(Frame);
            FrameChanged?.Invoke(Frame);
            return true;
        }

        public bool StepBack()
        {
            if (Frame <= 0) return false;
            Frame--;
            Body.ApplyFrame(Frame);
            FrameChanged?.Invoke(Frame);
            return true;
        }

        public void Seek(int frame)
        {
            Frame = Mathf.Clamp(frame, 0, Clip.n_frames - 1);
            Body.ApplyFrame(Frame);
            FrameChanged?.Invoke(Frame);
        }

        public void SeekSnapshot(string label) => Seek(Clip.SnapshotFrame(label));

        /// Wall-clock advance for interactive playback. Whole frames only: a partial frame holds the
        /// current pose rather than producing an interpolated one.
        public void Advance(float deltaSeconds)
        {
            if (Paused || Clip.fps <= 0f) return;
            accumulator += Mathf.Max(0f, deltaSeconds) * Mathf.Max(0f, Speed) * Clip.fps;
            while (accumulator >= 1f)
            {
                accumulator -= 1f;
                if (!StepForward()) { accumulator = 0f; break; }
            }
        }

        public float Tau => Clip.Tau(Frame);
        public float TimeSeconds => Clip.fps > 0f ? Frame / Clip.fps : 0f;
        public bool AtTargetReachingFrame => Frame == Clip.target_reaching_frame;

        public float HandToTargetDistance(string handJoint = "LeftHand")
        {
            int i = Clip.RepresentedIndexOf(handJoint);
            return i < 0 ? float.NaN : Vector3.Distance(Body.WorldPosition(i), Clip.Target);
        }

        public void Dispose() => Body?.Dispose();
    }
}
