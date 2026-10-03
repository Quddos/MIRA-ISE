// Phase 5C-18: serializable records Unity writes for the Python validation side.
//
// The pose-state digest hashes the APPLIED joint positions plus the configuration that produced them
// (case, executor, frame, joint order, adapter digest). It never hashes rendered pixels: rendering
// varies with graphics configuration, while the pose state must not. Image hashes are recorded
// separately, as image hashes.
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Mira.Embodiment
{
    [Serializable]
    public class FramePoseState
    {
        public int frame;
        public float tau;
        public float time_s;
        public bool is_target_reaching_frame;
        public float hand_to_target_distance_m;
        /// n_represented * 3, read back off the Unity transforms after the pose was applied.
        public float[] applied_positions_m_flat;
        public string pose_state_digest;
    }

    [Serializable]
    public class ClipReadback
    {
        public string schema = "mira.phase5c18_unity_readback/0.1";
        public string clip_id;
        public string @case;
        public string executor;
        public string executor_label;
        public string unity_version;
        public string graphics_device;
        public string graphics_api;
        public int n_frames;
        public int n_represented;
        public string[] represented_joint_names;
        public string[] not_represented_joint_names;
        public int target_reaching_frame;
        public string target_reaching_rule;
        public float[] target_unity_m;
        public float tolerance_m;
        public string adapter_version;
        public string adapter_sha256;
        public string clip_record_sha256;
        public string unity_positions_sha256;
        public string clip_state_digest;
        public FramePoseState[] frames;
        public bool interpolation_applied;
        public bool resampling_applied;
        public bool smoothing_applied;
        public bool ik_applied;
        public bool physics_applied;
        public bool local_rotations_derived;
        public bool joints_fabricated;
        public string[] snapshot_labels;
        public int[] snapshot_frames;
    }

    [Serializable]
    public class CaptureMetadata
    {
        public string schema = "mira.phase5c18_capture_metadata/0.1";
        public string capture_kind;          // capture_single_pose | capture_motion_track | capture_trajectory_view
        public string clip_id;
        public string @case;
        public string action_type;
        public string executor;
        public string executor_label;
        public int frame;                    // -1 for multi-pose captures
        public float normalized_time;        // -1 for multi-pose captures
        public int target_reaching_frame;
        public string target_reaching_rule;
        public string[] snapshot_labels;
        public int[] snapshot_frames;
        public string camera_preset;
        public float[] camera_position;
        public float[] camera_rotation_euler_deg;
        public float camera_fov_deg;
        public int width;
        public int height;
        public string unity_version;
        public string graphics_device;
        public string graphics_api;
        public string color_space;
        public string motion_artifact_sha256;   // the frozen clip record digest
        public string adapter_sha256;
        public string environment_source_sha256;
        public string image_file;
        public string image_sha256;
        public string pose_state_digest;        // for a single pose; clip state digest otherwise
        public bool apex_label_used;
        public bool debug_overlay;
        public bool manual_camera_adjustment;
        public bool per_executor_camera_tuning;
    }

    public static class PoseDigest
    {
        /// Deterministic text encoding, hashed with SHA-256. G9 round-trips a 32-bit float exactly, and
        /// InvariantCulture keeps the encoding independent of the machine's locale.
        public static string Compute(string caseId, string executor, int frame, string[] jointNames,
                                     float[] positionsFlat, string adapterSha)
        {
            var sb = new StringBuilder();
            sb.Append("PHASE5C18_POSE_STATE_V1\n");
            sb.Append(caseId).Append('\n').Append(executor).Append('\n').Append(frame.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append(adapterSha).Append('\n');
            for (int j = 0; j < jointNames.Length; j++)
            {
                sb.Append(jointNames[j]).Append(':');
                for (int c = 0; c < 3; c++)
                {
                    sb.Append(positionsFlat[j * 3 + c].ToString("G9", CultureInfo.InvariantCulture));
                    sb.Append(c < 2 ? ',' : '\n');
                }
            }
            return Hash(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        public static string Combine(string caseId, string executor, string[] frameDigests)
        {
            var sb = new StringBuilder();
            sb.Append("PHASE5C18_CLIP_STATE_V1\n").Append(caseId).Append('\n').Append(executor).Append('\n');
            foreach (string d in frameDigests) sb.Append(d).Append('\n');
            return Hash(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        public static string Hash(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(data);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }

    public static class DebugOverlay
    {
        /// Engineering verification only. Every field here is already visible in the frozen artifacts:
        /// no latent impairment, no hidden patient parameter, no evaluator result, no agent output.
        public static string Text(PosePlayer player)
        {
            var c = player.Clip;
            return string.Join("\n", new[]
            {
                "case: " + c.@case + "  (" + c.action_type + ")",
                "executor: " + c.executor_label,
                "frame: " + player.Frame + " / " + (c.n_frames - 1),
                "normalized time: " + player.Tau.ToString("F3", CultureInfo.InvariantCulture),
                "target-reaching frame: " + c.target_reaching_frame + "  (" + c.target_reaching_rule + ")",
                "hand-to-target distance: " + player.HandToTargetDistance().ToString("F4", CultureInfo.InvariantCulture) + " m",
                "joints represented: " + c.n_represented + " / " + c.n_soma30_joints,
                "motion digest: " + (c.clip_record_sha256 ?? "").Substring(0, Mathf.Min(16, (c.clip_record_sha256 ?? "").Length)),
            });
        }
    }
}
