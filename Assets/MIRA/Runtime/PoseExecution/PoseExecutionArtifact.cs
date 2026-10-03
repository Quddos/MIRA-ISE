// Phase 5C-6B: the Unity-side view of one frozen analytic pose realization.
//
// This is NOT a second motion representation. It is a deterministic derivation of a single frozen
// TARGET_MOTION_V1, carrying POSE_REALIZATION_V1 plus the skeleton needed to rebuild the avatar, and
// it is never the source of truth: the source TargetMotion hash and every configuration hash travel
// with it so the artifact can be re-derived and checked.
//
// Coordinates arrive in the canonical frame (kimodo_rh_yup_zfwd). Conversion to Unity happens in
// CoordinateConversion and nowhere else.
using System;
using Mira.Data;
using UnityEngine;

namespace Mira.PoseExecution
{
    [Serializable]
    public class PoseDerivedFrom
    {
        public string target_motion_id;
        public string target_motion_sha256;
        public string target_motion_schema_version;
        public string motion_request_id;
        public string motion_request_sha256;
        public string action_type;
        public string executor_id;
        public string executor_version;
        public string realization_kind;
    }

    [Serializable]
    public class PoseRealizationInfo
    {
        public string contract_version;
        public string method_version;
        public string pose_configuration_sha256;
        public string rotation_provenance;      // RECONSTRUCTED for the analytic pathway
        public int n_native_rotations;
        public int n_reconstructed_rotations;
        public bool executor_native;            // false: reconstructed values are never executor-native
        public string root_position_source;
        public string root_motion_mode;         // ROOT_MOTION_STATIC_V1
        public string rotation_reconstruction_status;
    }

    [Serializable]
    public class PosePlayback
    {
        public string adapter_version;
        public string interpolation;            // NONE on the first scientific execution
        public bool resampling;
        public string frame_selection;
    }

    [Serializable]
    public class PoseCoordinateConfiguration
    {
        public string frame;
        public string units;
        public string configuration_sha256;
        public bool conversion_applied_in_unity_only;
    }

    [Serializable]
    public class PoseMappingConfiguration
    {
        public string configuration_sha256;
        public string mapping;
    }

    [Serializable]
    public class PoseSkeleton
    {
        public string skeleton_id;
        public string skeleton_sha256;
        public string skeleton_source;
        public string[] joint_names;
        public int[] parent_indices;
        public float[] rest_offsets_m_flat;
    }

    [Serializable]
    public class PoseMotion
    {
        public float fps;
        public int n_frames;
        public float requested_duration_s;
        public float[] root_pos_m_flat;
        public float[] local_rot_quat_wxyz_flat;
        public float[] timestamps_s;
    }

    [Serializable]
    public class PoseTarget
    {
        public string target_id;
        public float[] position_m;
        public float radius_m;
        public string required_arm;
        public string origin_joint;
    }

    [Serializable]
    public class PoseScene
    {
        public string immersive_context;
        public float[] table_center_m;
        public float[] table_size_m;
        public float net_height_m;
        public float target_display_diameter_m;
    }

    [Serializable]
    public class PoseTaskEvents
    {
        public int target_presented_frame;
    }

    [Serializable]
    public class PoseExecutionArtifact
    {
        public const string SchemaId = "mira.unity_pose_execution/0.1";
        public const string ExpectedArtifactVersion = "UNITY_POSE_EXECUTION_V1";
        public const string ExpectedPoseContract = "POSE_REALIZATION_V1";
        public const string ExpectedPoseMethod = "ANALYTIC_START_POSTURE_COMPOSITION_V1";
        public const string ExpectedRootMode = "ROOT_MOTION_STATIC_V1";
        public const int Soma30JointCount = 30;

        public string schema;
        public string artifact_version;
        public bool is_source_of_truth;
        public PoseDerivedFrom derived_from;
        public PoseRealizationInfo pose_realization;
        public PosePlayback playback;
        public PoseCoordinateConfiguration coordinate_configuration;
        public PoseMappingConfiguration mapping_configuration;
        public PoseSkeleton skeleton;
        public PoseMotion motion;
        public PoseTarget target;
        public PoseScene scene;
        public PoseTaskEvents task_events;

        public static PoseExecutionArtifact FromJson(string json)
        {
            var artifact = JsonUtility.FromJson<PoseExecutionArtifact>(json);
            artifact.Validate();
            return artifact;
        }

        /// Handoff section 13: every identity check, applied before anything is built.
        /// A mismatch throws; nothing is partially loaded and nothing is repaired.
        public void Validate()
        {
            Require(schema == SchemaId, $"schema is '{schema}', expected '{SchemaId}'");
            Require(artifact_version == ExpectedArtifactVersion,
                $"artifact_version is '{artifact_version}'");
            Require(pose_realization != null && pose_realization.contract_version == ExpectedPoseContract,
                "pose contract version mismatch");
            Require(pose_realization.method_version == ExpectedPoseMethod,
                $"pose method is '{pose_realization.method_version}'");
            Require(pose_realization.root_motion_mode == ExpectedRootMode,
                $"root motion mode is '{pose_realization.root_motion_mode}'");
            Require(!pose_realization.executor_native,
                "reconstructed rotations must never be marked executor-native");
            Require(playback != null && playback.interpolation == "NONE",
                "the first scientific execution must run without interpolation");
            Require(!playback.resampling, "resampling is forbidden");
            Require(coordinate_configuration != null
                && coordinate_configuration.frame == CoordinateConversion.KimodoFrame,
                $"coordinate frame is '{coordinate_configuration?.frame}'");
            Require(skeleton != null && skeleton.skeleton_id == "SOMA30", "skeleton is not SOMA30");
            // The frozen skeleton digest must travel with the artifact. Its VALUE is compared against
            // the frozen configuration by the Python section-13 gate before Unity is launched; Unity
            // cannot recompute a Python canonical-JSON digest, so here it is required to be present.
            Require(!string.IsNullOrEmpty(skeleton.skeleton_sha256),
                "the frozen skeleton hash is missing from the artifact");
            Require(skeleton.joint_names != null && skeleton.joint_names.Length == Soma30JointCount,
                $"expected {Soma30JointCount} joint names");
            Require(skeleton.parent_indices != null
                && skeleton.parent_indices.Length == Soma30JointCount, "parent index count mismatch");
            Require(skeleton.rest_offsets_m_flat != null
                && skeleton.rest_offsets_m_flat.Length == Soma30JointCount * 3, "rest offset count");
            Require(motion != null && motion.n_frames > 0, "no frames");
            Require(motion.root_pos_m_flat.Length == motion.n_frames * 3, "root array length");
            Require(motion.local_rot_quat_wxyz_flat.Length == motion.n_frames * Soma30JointCount * 4,
                "rotation array length");
            Require(motion.timestamps_s.Length == motion.n_frames, "timestamp count");
            for (int i = 0; i < motion.local_rot_quat_wxyz_flat.Length; i++)
                Require(IsFinite(motion.local_rot_quat_wxyz_flat[i]), $"non-finite rotation at {i}");
            for (int i = 0; i < motion.root_pos_m_flat.Length; i++)
                Require(IsFinite(motion.root_pos_m_flat[i]), $"non-finite root value at {i}");
            Require(target != null && target.position_m != null && target.position_m.Length == 3,
                "target position");
            Require(!is_source_of_truth, "this artifact must declare it is not the source of truth");
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[MIRA pose artifact] " + message);
        }

        /// Build the in-memory episode the existing frozen avatar consumes.
        /// No new avatar implementation and no second on-disk representation.
        public UnityEpisode ToEpisode()
        {
            return new UnityEpisode
            {
                schema = UnityEpisode.SchemaId,
                episode_id = derived_from.motion_request_id,
                fps = Mathf.RoundToInt(motion.fps),
                n_frames = motion.n_frames,
                coordinate_frame = coordinate_configuration.frame,
                skeleton = new EpisodeSkeleton
                {
                    skeleton_id = skeleton.skeleton_id,
                    joint_names = skeleton.joint_names,
                    parent_indices = skeleton.parent_indices,
                    rest_offsets_m_flat = skeleton.rest_offsets_m_flat,
                },
                motion = new EpisodeMotion
                {
                    clip_id = derived_from.target_motion_id,
                    root_pos_m_flat = motion.root_pos_m_flat,
                    local_rot_quat_wxyz_flat = motion.local_rot_quat_wxyz_flat,
                    clip_sha256 = derived_from.target_motion_sha256,
                },
                target = new EpisodeTarget
                {
                    target_id = target.target_id,
                    position_m = target.position_m,
                    radius_m = target.radius_m,
                    required_arm = target.required_arm,
                },
                scene = new EpisodeScene
                {
                    immersive_context = scene.immersive_context,
                    table_center_m = scene.table_center_m,
                    table_size_m = scene.table_size_m,
                    net_height_m = scene.net_height_m,
                    target_display_diameter_m = scene.target_display_diameter_m,
                },
                task_events = new EpisodeTaskEvents
                {
                    target_presented_frame = task_events.target_presented_frame,
                },
            };
        }
    }
}
