// Phase 5C-18: the digital patient driven by GLOBAL SOMA30 joint positions.
//
// This is deliberately not the rotation-driven Soma30Avatar used by the episode pipeline. The frozen
// Gate C realizations carry global joint positions, not local rotations, and reconstructing rotations
// from them would be an inverse-kinematics step -- a hidden scientific transformation the phase forbids.
// Driving positions directly also discharges the Phase 5C-16A hierarchy caveat outright: no local
// rotation is derived from any joint, including the four hand-end joints whose parents differ between
// SOMA77 and SOMA30.
//
// Joints a realization does not carry are NOT created. The analytic realization carries the acting arm
// chain only, so its avatar is that chain; nothing is invented to fill the rest of the body.
using System.Collections.Generic;
using UnityEngine;

namespace Mira.Embodiment
{
    public class Soma30PositionEmbodiment : System.IDisposable
    {
        public readonly GameObject Root;
        public readonly Soma30MotionClip Clip;
        public readonly Transform[] Joints;          // one per represented joint, in clip order
        public int Frame { get; private set; } = -1;

        readonly List<(Transform bone, int from, int to)> bones = new List<(Transform, int, int)>();
        readonly float jointDiameter;
        readonly float boneWidth;

        public Soma30PositionEmbodiment(Soma30MotionClip clip, Transform parent, bool withVisuals,
                                        Color jointColor, Color boneColor,
                                        float jointDiameterM = 0.045f, float boneWidthM = 0.028f)
        {
            Clip = clip;
            jointDiameter = jointDiameterM;
            boneWidth = boneWidthM;
            Root = new GameObject("Soma30PositionEmbodiment_" + clip.clip_id);
            Root.transform.SetParent(parent, false);

            // Joint transforms are siblings under one root, NOT a nested hierarchy: a nested hierarchy
            // would make each joint's world position depend on its ancestors' transforms, and the clip
            // specifies world positions directly.
            Joints = new Transform[clip.n_represented];
            for (int j = 0; j < clip.n_represented; j++)
            {
                var go = new GameObject(clip.represented_joint_names[j]);
                go.transform.SetParent(Root.transform, false);
                Joints[j] = go.transform;
                if (!withVisuals) continue;
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "vis_joint";
                Object.DestroyImmediate(sphere.GetComponent<Collider>());
                sphere.transform.SetParent(go.transform, false);
                sphere.transform.localScale = Vector3.one * jointDiameter;
                sphere.GetComponent<Renderer>().sharedMaterial = Mira.Immersive.Materials.Lit(jointColor);
            }

            if (!withVisuals) { ApplyFrame(0); return; }

            // A bone is drawn only where BOTH endpoints are represented, using the frozen SOMA30 parent
            // table. No bone is drawn across a joint the realization does not carry.
            for (int j = 0; j < clip.n_represented; j++)
            {
                int soma30 = clip.represented_joint_indices[j];
                int parentSoma30 = clip.parent_indices[soma30];
                if (parentSoma30 < 0) continue;
                int parentRepresented = System.Array.IndexOf(clip.represented_joint_indices, parentSoma30);
                if (parentRepresented < 0) continue;
                var bone = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                bone.name = "vis_bone_" + clip.represented_joint_names[j];
                Object.DestroyImmediate(bone.GetComponent<Collider>());
                bone.transform.SetParent(Root.transform, false);
                bone.GetComponent<Renderer>().sharedMaterial = Mira.Immersive.Materials.Lit(boneColor);
                bones.Add((bone.transform, parentRepresented, j));
            }
            ApplyFrame(0);
        }

        /// Place every represented joint at its frozen position for this frame. Nothing is interpolated,
        /// blended, smoothed or corrected; an out-of-range frame is a programming error, not something
        /// to clamp silently, so it throws.
        public void ApplyFrame(int frame)
        {
            if (frame < 0 || frame >= Clip.n_frames)
                throw new System.ArgumentOutOfRangeException(nameof(frame), $"frame {frame} outside [0, {Clip.n_frames - 1}]");
            Frame = frame;
            for (int j = 0; j < Joints.Length; j++) Joints[j].position = Clip.Position(frame, j);
            foreach (var (bone, from, to) in bones)
            {
                Vector3 a = Joints[from].position, b = Joints[to].position;
                Vector3 delta = b - a;
                float len = delta.magnitude;
                bone.position = a + delta * 0.5f;
                bone.rotation = len < 1e-6f ? Quaternion.identity : Quaternion.FromToRotation(Vector3.up, delta / len);
                bone.localScale = new Vector3(boneWidth, Mathf.Max(len * 0.5f, 1e-4f), boneWidth);
            }
        }

        public Vector3 WorldPosition(int representedIndex) => Joints[representedIndex].position;

        public Vector3 WorldPosition(string jointName)
        {
            int i = Clip.RepresentedIndexOf(jointName);
            if (i < 0) throw new System.ArgumentException(jointName + " is not represented by this realization");
            return Joints[i].position;
        }

        /// The applied joint positions, read back off the Unity transforms (not off the clip), which is
        /// what makes the readback a measurement of embodiment rather than a copy of the input.
        public void AppendAppliedPositions(List<float> sink)
        {
            foreach (var t in Joints) { var p = t.position; sink.Add(p.x); sink.Add(p.y); sink.Add(p.z); }
        }

        public void SetVisible(bool visible) => Root.SetActive(visible);

        public void Dispose()
        {
            if (Root == null) return;
            if (Application.isPlaying) Object.Destroy(Root); else Object.DestroyImmediate(Root);
        }
    }
}
