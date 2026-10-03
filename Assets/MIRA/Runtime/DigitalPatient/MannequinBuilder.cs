// Neutral rigid-segment mannequin for the SOMA30 Generic rig (Phase 5B visual representation).
// Every visual is a primitive parented to a SOMA30 joint transform, so the mannequin is driven by
// exactly the same direct joint control as the stick figure; joint transforms keep unit scale.
// Procedurally generated from Unity built-in primitives: no third-party mesh, no licence dependency.
// One uniform body colour and one uniform joint colour: nothing encodes laterality or impairment.
using System.Collections.Generic;
using UnityEngine;

namespace Mira.DigitalPatient
{
    public static class MannequinBuilder
    {
        // Segment cross-section radii (m), keyed by the CHILD joint of the bone (x = lateral, z = depth).
        static readonly Dictionary<string, Vector2> SegmentRadii = new Dictionary<string, Vector2>
        {
            { "Spine1", new Vector2(0.125f, 0.090f) }, { "Spine2", new Vector2(0.130f, 0.090f) },
            { "Chest", new Vector2(0.140f, 0.095f) }, { "Neck1", new Vector2(0.145f, 0.095f) },
            { "Neck2", new Vector2(0.045f, 0.045f) }, { "Head", new Vector2(0.042f, 0.042f) },
            { "LeftShoulder", new Vector2(0.060f, 0.060f) }, { "RightShoulder", new Vector2(0.060f, 0.060f) },
            { "LeftArm", new Vector2(0.050f, 0.050f) }, { "RightArm", new Vector2(0.050f, 0.050f) },
            { "LeftForeArm", new Vector2(0.046f, 0.046f) }, { "RightForeArm", new Vector2(0.046f, 0.046f) },
            { "LeftHand", new Vector2(0.038f, 0.038f) }, { "RightHand", new Vector2(0.038f, 0.038f) },
            { "LeftLeg", new Vector2(0.085f, 0.085f) }, { "RightLeg", new Vector2(0.085f, 0.085f) },
            { "LeftShin", new Vector2(0.068f, 0.068f) }, { "RightShin", new Vector2(0.068f, 0.068f) },
            { "LeftFoot", new Vector2(0.050f, 0.050f) }, { "RightFoot", new Vector2(0.050f, 0.050f) },
        };

        static readonly string[] JointBalls = { "LeftArm", "RightArm", "LeftForeArm", "RightForeArm", "LeftHand", "RightHand", "LeftShin", "RightShin", "LeftLeg", "RightLeg" };

        public static void Build(Soma30Avatar avatar, int[] parents, Material body, Material joint)
        {
            var joints = avatar.Joints;
            for (int j = 1; j < joints.Length; j++)
            {
                string name = avatar.JointNames[j];
                if (!SegmentRadii.TryGetValue(name, out var r)) continue;
                int p = parents[j];
                Vector3 offset = joints[j].localPosition;
                float len = offset.magnitude;
                if (len < 1e-4f) continue;
                var seg = Primitive(PrimitiveType.Capsule, "mq_seg_" + name, joints[p], body);
                seg.transform.localPosition = offset * 0.5f;
                seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, offset / len);
                // Capsule height = 2 * scale.y; add the radius so neighbouring segments overlap smoothly.
                seg.transform.localScale = new Vector3(2f * r.x, 0.5f * len + Mathf.Max(r.x, r.y) * 0.5f, 2f * r.y);
            }
            foreach (var n in JointBalls)
            {
                int j = avatar.JointIndex(n);
                if (j < 0) continue;
                var ball = Primitive(PrimitiveType.Sphere, "mq_joint_" + n, joints[j], joint);
                float d = n.EndsWith("Hand") ? 0.075f : (n.EndsWith("Leg") ? 0.15f : 0.095f);
                ball.transform.localScale = Vector3.one * d;
            }
            // Pelvis
            var pelvis = Primitive(PrimitiveType.Sphere, "mq_pelvis", joints[0], body);
            pelvis.transform.localScale = new Vector3(0.34f, 0.20f, 0.22f);
            // Head (ellipsoid above the Head joint) + nose block showing the facing direction.
            int head = avatar.JointIndex("Head");
            var skull = Primitive(PrimitiveType.Sphere, "mq_head", joints[head], body);
            skull.transform.localPosition = new Vector3(0f, 0.10f, 0.01f);
            skull.transform.localScale = new Vector3(0.18f, 0.23f, 0.21f);
            var nose = Primitive(PrimitiveType.Cube, "mq_nose", joints[head], joint);
            nose.transform.localPosition = new Vector3(0f, 0.09f, 0.11f);
            nose.transform.localScale = new Vector3(0.03f, 0.05f, 0.04f);
            // Hands (flattened blocks from wrist toward the middle-finger end).
            foreach (var side in new[] { "Left", "Right" })
            {
                int hand = avatar.JointIndex(side + "Hand");
                int tip = avatar.JointIndex(side + "HandMiddleEnd");
                Vector3 o = joints[tip].localPosition;
                var palm = Primitive(PrimitiveType.Cube, "mq_hand_" + side, joints[hand], body);
                palm.transform.localPosition = o * 0.45f;
                palm.transform.localRotation = Quaternion.FromToRotation(Vector3.right, o.normalized);
                palm.transform.localScale = new Vector3(o.magnitude * 0.9f, 0.035f, 0.085f);
                // Feet
                int foot = avatar.JointIndex(side + "Foot");
                int toe = avatar.JointIndex(side + "ToeBase");
                Vector3 f = joints[toe].localPosition;
                var shoe = Primitive(PrimitiveType.Cube, "mq_foot_" + side, joints[foot], body);
                shoe.transform.localPosition = f * 0.55f + new Vector3(0f, -0.03f, 0f);
                shoe.transform.localRotation = Quaternion.LookRotation(new Vector3(f.x, 0f, f.z).normalized, Vector3.up);
                shoe.transform.localScale = new Vector3(0.10f, 0.06f, f.magnitude * 1.6f);
            }
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
            return go;
        }
    }
}
