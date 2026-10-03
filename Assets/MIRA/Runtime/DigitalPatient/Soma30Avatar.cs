// SOMA30 synthetic_digital_patient avatar built procedurally from the episode skeleton.
// Joint transforms are driven by direct localRotation assignment (Generic rig, no Mecanim Humanoid
// retargeting), so measured motion is exactly the canonical clip. Visual primitives are siblings
// under each joint with their own scale; joint transforms always keep unit scale.
using System.Collections.Generic;
using Mira.Data;
using UnityEngine;

namespace Mira.DigitalPatient
{
    public enum AvatarStyle { None, StickFigure, Mannequin }

    public class Soma30Avatar
    {
        public readonly GameObject Root;
        public readonly Transform[] Joints;
        public readonly string[] JointNames;
        public readonly AvatarStyle Style;
        readonly UnityEpisode episode;

        public Soma30Avatar(UnityEpisode ep, Transform parent, bool withVisuals, Material material = null)
            : this(ep, parent, withVisuals ? AvatarStyle.StickFigure : AvatarStyle.None, material, material) { }

        public Soma30Avatar(UnityEpisode ep, Transform parent, AvatarStyle style, Material body, Material joint)
        {
            Style = style;
            episode = ep;
            Root = new GameObject("SyntheticDigitalPatient_SOMA30");
            Root.transform.SetParent(parent, false);
            int n = UnityEpisode.Soma30JointCount;
            Joints = new Transform[n];
            JointNames = ep.skeleton.joint_names;
            for (int j = 0; j < n; j++)
            {
                var go = new GameObject(JointNames[j]);
                int p = ep.skeleton.parent_indices[j];
                go.transform.SetParent(p < 0 ? Root.transform : Joints[p], false);
                go.transform.localPosition = p < 0 ? Vector3.zero : CoordinateConversion.KimodoToUnityPosition(ep.RestOffsetKimodo(j));
                go.transform.localRotation = Quaternion.identity;
                Joints[j] = go.transform;
            }
            if (style == AvatarStyle.StickFigure) BuildVisuals(body);
            else if (style == AvatarStyle.Mannequin) MannequinBuilder.Build(this, ep.skeleton.parent_indices, body, joint);
        }

        void BuildVisuals(Material material)
        {
            for (int j = 0; j < Joints.Length; j++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "vis_joint";
                Object.DestroyImmediate(sphere.GetComponent<Collider>());
                sphere.transform.SetParent(Joints[j], false);
                sphere.transform.localScale = Vector3.one * 0.035f;
                if (material != null) sphere.GetComponent<Renderer>().sharedMaterial = material;

                int p = episode.skeleton.parent_indices[j];
                if (p < 0) continue;
                Vector3 offset = Joints[j].localPosition;
                float len = offset.magnitude;
                if (len < 1e-4f) continue;
                var bone = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                bone.name = "vis_bone_" + JointNames[j];
                Object.DestroyImmediate(bone.GetComponent<Collider>());
                bone.transform.SetParent(Joints[p], false);
                bone.transform.localPosition = offset * 0.5f;
                bone.transform.localRotation = Quaternion.FromToRotation(Vector3.up, offset.normalized);
                bone.transform.localScale = new Vector3(0.025f, len * 0.5f, 0.025f);
                if (material != null) bone.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        public void ApplyFrame(int frame)
        {
            frame = Mathf.Clamp(frame, 0, episode.n_frames - 1);
            Joints[0].localPosition = CoordinateConversion.KimodoToUnityPosition(episode.RootKimodo(frame));
            for (int j = 0; j < Joints.Length; j++)
                Joints[j].localRotation = CoordinateConversion.KimodoToUnityQuaternion(episode.LocalQuatKimodoWxyz(frame, j));
        }

        public int JointIndex(string name) => System.Array.IndexOf(JointNames, name);

        public Vector3 WorldPosition(int joint) => Joints[joint].position;

        public void AppendWorldPositions(List<float> sink)
        {
            foreach (var t in Joints)
            {
                var p = t.position;
                sink.Add(p.x); sink.Add(p.y); sink.Add(p.z);
            }
        }
    }
}
