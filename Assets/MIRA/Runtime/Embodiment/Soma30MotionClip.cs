// Phase 5C-18: the frozen canonical SOMA30 clip, already expressed in Unity coordinates by the
// documented Python adapter (PHASE5C18_SOMA30_TO_UNITY_POSITION_ADAPTER_V1).
//
// Unity performs NO conversion of its own here. The positions in this file are applied verbatim, so the
// axis convention, the constant embodiment placement and the joint order all live in exactly one place
// and are recorded in data/unity/phase5c18_v0/inputs/soma30_to_unity_adapter.json.
//
// Mirror of mira.phase5c18 prepare_inputs "mira.phase5c18_unity_clip_flat/0.1" (JsonUtility-compatible:
// flat float array, parallel name/index arrays, no dictionaries and no jagged arrays).
using System;
using System.IO;
using UnityEngine;

namespace Mira.Embodiment
{
    [Serializable]
    public class Soma30MotionClip
    {
        public string schema;
        public string clip_id;
        public string @case;
        public string action_type;
        public string executor;
        public string executor_label;
        public int n_frames;
        public float fps;
        public float duration_s;
        public int n_soma30_joints;
        public string[] joint_names;
        public int[] parent_indices;
        public int n_represented;
        public int[] represented_joint_indices;
        public string[] represented_joint_names;
        /// n_frames * n_represented * 3, frame-major then represented-joint order.
        public float[] unity_positions_m_flat;
        public float[] target_unity_m;
        public float[] anchor_unity_m;
        public float tolerance_m;
        public int target_reaching_frame;
        public string target_reaching_rule;
        public string[] snapshot_labels;
        public int[] snapshot_frames;
        public string adapter_version;
        public string adapter_sha256;
        public string clip_record_sha256;
        public string unity_positions_sha256;

        public const string Schema = "mira.phase5c18_unity_clip_flat/0.1";
        public const string ExpectedRule = "TARGET_REACHING_FRAME_RULE_V2";

        public static Soma30MotionClip Load(string path)
        {
            var clip = JsonUtility.FromJson<Soma30MotionClip>(File.ReadAllText(path));
            clip.Validate();
            return clip;
        }

        /// Fail closed. A clip that does not describe itself consistently is not played.
        public void Validate()
        {
            if (schema != Schema) throw new FormatException("unexpected clip schema " + schema);
            if (target_reaching_rule != ExpectedRule) throw new FormatException("unexpected target-reaching rule " + target_reaching_rule);
            if (n_frames <= 0) throw new FormatException("a clip needs at least one frame");
            if (n_represented <= 0 || represented_joint_indices.Length != n_represented) throw new FormatException("represented joint count mismatch");
            if (represented_joint_names.Length != n_represented) throw new FormatException("represented joint name count mismatch");
            int expected = n_frames * n_represented * 3;
            if (unity_positions_m_flat.Length != expected)
                throw new FormatException($"expected {expected} position components, got {unity_positions_m_flat.Length}");
            if (joint_names.Length != n_soma30_joints || parent_indices.Length != n_soma30_joints) throw new FormatException("skeleton arrays mismatch");
            if (target_unity_m.Length != 3 || anchor_unity_m.Length != 3) throw new FormatException("target and anchor must be 3-vectors");
            if (target_reaching_frame < 0 || target_reaching_frame >= n_frames) throw new FormatException("the target-reaching frame is out of range");
            if (snapshot_labels.Length != snapshot_frames.Length) throw new FormatException("snapshot arrays mismatch");
            foreach (int f in snapshot_frames)
                if (f < 0 || f >= n_frames) throw new FormatException("a snapshot frame is out of range");
            foreach (string label in snapshot_labels)
                if (label == "APEX") throw new FormatException("the apex label belongs to the historical Phase 5C-17 rule and is not used here");
        }

        public Vector3 Position(int frame, int representedIndex)
        {
            int i = (frame * n_represented + representedIndex) * 3;
            return new Vector3(unity_positions_m_flat[i], unity_positions_m_flat[i + 1], unity_positions_m_flat[i + 2]);
        }

        public Vector3 Target => new Vector3(target_unity_m[0], target_unity_m[1], target_unity_m[2]);

        public Vector3 Anchor => new Vector3(anchor_unity_m[0], anchor_unity_m[1], anchor_unity_m[2]);

        public float Tau(int frame) => n_frames > 1 ? frame / (float)(n_frames - 1) : 0f;

        public int RepresentedIndexOf(string jointName) => Array.IndexOf(represented_joint_names, jointName);

        public int SnapshotFrame(string label)
        {
            int i = Array.IndexOf(snapshot_labels, label);
            if (i < 0) throw new ArgumentException("unknown snapshot label " + label);
            return snapshot_frames[i];
        }

        /// The SOMA30 joints this realization does not carry. They are reported, never fabricated.
        public string[] NotRepresented()
        {
            var missing = new System.Collections.Generic.List<string>();
            foreach (string name in joint_names)
                if (Array.IndexOf(represented_joint_names, name) < 0) missing.Add(name);
            return missing.ToArray();
        }
    }
}
