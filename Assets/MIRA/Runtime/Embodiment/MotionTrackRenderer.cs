// Phase 5C-18: the multi-pose motion track -- START, EARLY_REACH, MID_REACH, TARGET_REACH, RETURN
// drawn simultaneously as ghosted bodies, with the acting-hand trajectory and its markers.
//
// MODE A (live sequence) is the PosePlayer driving one body. MODE B (motion track) is this renderer:
// several frozen frames of the SAME clip shown at once. Both read the same frozen positions; neither
// alters them. The ghosts are additional GameObjects, not a modification of the motion.
//
// The TARGET_REACH pose is the frame the frozen V2 rule selected. It is never labelled "apex": that
// word names the historical Phase 5C-17 tau = 0.5 rule, which this phase does not use.
using System.Collections.Generic;
using UnityEngine;

namespace Mira.Embodiment
{
    public class MotionTrackRenderer : System.IDisposable
    {
        public readonly GameObject Root;
        public readonly List<Soma30PositionEmbodiment> Ghosts = new List<Soma30PositionEmbodiment>();
        public readonly List<string> Labels = new List<string>();
        public readonly List<int> Frames = new List<int>();

        readonly Soma30MotionClip clip;
        LineRenderer trajectory;

        public MotionTrackRenderer(Soma30MotionClip clip, Transform parent, Color baseColor,
                                   Color targetReachColor, string handJoint = "LeftHand",
                                   bool drawTrajectory = true)
        {
            this.clip = clip;
            Root = new GameObject("MotionTrack_" + clip.clip_id);
            Root.transform.SetParent(parent, false);

            // Ghost opacity rises along the sequence so the reader can tell the order without any
            // per-executor tuning: the ramp depends on the snapshot index, never on the executor.
            for (int i = 0; i < clip.snapshot_labels.Length; i++)
            {
                string label = clip.snapshot_labels[i];
                int frame = clip.snapshot_frames[i];
                bool isTargetReach = label == "TARGET_REACH";
                float t = clip.snapshot_labels.Length > 1 ? i / (float)(clip.snapshot_labels.Length - 1) : 1f;
                Color tint = isTargetReach ? targetReachColor : Color.Lerp(baseColor * 0.55f, baseColor, t);
                tint.a = 1f;
                var ghost = new Soma30PositionEmbodiment(clip, Root.transform, true, tint, tint,
                                                         isTargetReach ? 0.055f : 0.042f,
                                                         isTargetReach ? 0.032f : 0.024f);
                ghost.Root.name = $"pose_{i}_{label}_f{frame:D3}";
                ghost.ApplyFrame(frame);
                Ghosts.Add(ghost);
                Labels.Add(label);
                Frames.Add(frame);
            }

            if (drawTrajectory) BuildTrajectory(handJoint, baseColor);
        }

        /// The acting-hand path through every frozen frame -- the actual trajectory, not a smoothed or
        /// resampled version of it, and not a spline through the snapshots.
        void BuildTrajectory(string handJoint, Color color)
        {
            int hand = clip.RepresentedIndexOf(handJoint);
            if (hand < 0) return;
            var go = new GameObject("hand_trajectory");
            go.transform.SetParent(Root.transform, false);
            trajectory = go.AddComponent<LineRenderer>();
            trajectory.useWorldSpace = true;
            trajectory.positionCount = clip.n_frames;
            trajectory.startWidth = trajectory.endWidth = 0.009f;
            trajectory.numCapVertices = 4;
            trajectory.sharedMaterial = Mira.Immersive.Materials.Lit(color);
            for (int f = 0; f < clip.n_frames; f++) trajectory.SetPosition(f, clip.Position(f, hand));

            Marker("marker_START", clip.Position(0, hand), color, 0.030f);
            Marker("marker_TARGET_REACH", clip.Position(clip.target_reaching_frame, hand), Color.white, 0.038f);
            Marker("marker_RETURN", clip.Position(clip.n_frames - 1, hand), color * 0.7f, 0.030f);
        }

        GameObject Marker(string name, Vector3 position, Color color, float diameter)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(Root.transform, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * diameter;
            go.GetComponent<Renderer>().sharedMaterial = Mira.Immersive.Materials.Lit(color);
            return go;
        }

        /// MODE B can also be shown sequentially: reveal one pose at a time without touching the data.
        public void ShowOnly(int snapshotIndex)
        {
            for (int i = 0; i < Ghosts.Count; i++) Ghosts[i].SetVisible(snapshotIndex < 0 || i == snapshotIndex);
        }

        public void ShowAll() => ShowOnly(-1);

        public void Dispose()
        {
            foreach (var ghost in Ghosts) ghost.Dispose();
            Ghosts.Clear();
            if (Root == null) return;
            if (Application.isPlaying) Object.Destroy(Root); else Object.DestroyImmediate(Root);
        }
    }
}
