// Mirror of schemas/unity_episode.v0.json and schemas/unity_readback.v0.json (JsonUtility-compatible).
// Field names intentionally use the schema's snake_case.
using System;
using UnityEngine;

namespace Mira.Data
{
    [Serializable]
    public class EpisodeSkeleton
    {
        public string skeleton_id;
        public string[] joint_names;
        public int[] parent_indices;
        public float[] rest_offsets_m_flat;
    }

    [Serializable]
    public class EpisodeMotion
    {
        public string clip_id;
        public float[] root_pos_m_flat;
        public float[] local_rot_quat_wxyz_flat;
        public string clip_sha256;
    }

    [Serializable]
    public class EpisodeTarget
    {
        public string target_id;
        public float[] position_m;
        public float radius_m;
        public string required_arm;
    }

    [Serializable]
    public class EpisodeScene
    {
        public string immersive_context;
        public float[] table_center_m;
        public float[] table_size_m;
        public float net_height_m;
        public float target_display_diameter_m;
    }

    [Serializable]
    public class EpisodeTaskEvents
    {
        public int target_presented_frame;
    }

    [Serializable]
    public class UnityEpisode
    {
        public const string SchemaId = "mira.unity_episode/0.1";
        public const int Soma30JointCount = 30;

        public string schema;
        public string episode_id;
        public int fps;
        public int n_frames;
        public string coordinate_frame;
        public EpisodeSkeleton skeleton;
        public EpisodeMotion motion;
        public EpisodeTarget target;
        public EpisodeScene scene;
        public EpisodeTaskEvents task_events;

        public static UnityEpisode FromJson(string json)
        {
            var ep = JsonUtility.FromJson<UnityEpisode>(json);
            ep.Validate();
            return ep;
        }

        public void Validate()
        {
            if (schema != SchemaId) throw new FormatException($"unexpected schema '{schema}'");
            if (fps != 30) throw new FormatException("canonical fps is 30");
            if (coordinate_frame != CoordinateConversion.KimodoFrame) throw new FormatException("episode must be in kimodo_rh_yup_zfwd");
            if (skeleton == null || skeleton.skeleton_id != "SOMA30") throw new FormatException("skeleton must be SOMA30");
            if (skeleton.joint_names.Length != Soma30JointCount || skeleton.parent_indices.Length != Soma30JointCount)
                throw new FormatException("SOMA30 requires 30 joints");
            if (skeleton.rest_offsets_m_flat.Length != 3 * Soma30JointCount) throw new FormatException("rest offsets must be 90 floats");
            if (motion.root_pos_m_flat.Length != 3 * n_frames) throw new FormatException("root_pos_m_flat must be 3*n_frames");
            if (motion.local_rot_quat_wxyz_flat.Length != 4 * Soma30JointCount * n_frames)
                throw new FormatException("local_rot_quat_wxyz_flat must be 4*30*n_frames");
            for (int j = 1; j < Soma30JointCount; j++)
                if (skeleton.parent_indices[j] < 0 || skeleton.parent_indices[j] >= j) throw new FormatException("parents must precede children");
        }

        /// Canonical-frame (Kimodo) root position of a frame.
        public Vector3 RootKimodo(int frame)
        {
            int o = 3 * frame;
            var r = motion.root_pos_m_flat;
            return new Vector3(r[o], r[o + 1], r[o + 2]);
        }

        /// Canonical-frame (Kimodo) local quaternion, returned as (w, x, y, z) in a Vector4 (x=w, y=x, z=y, w=z).
        public Vector4 LocalQuatKimodoWxyz(int frame, int joint)
        {
            int o = 4 * (frame * Soma30JointCount + joint);
            var q = motion.local_rot_quat_wxyz_flat;
            return new Vector4(q[o], q[o + 1], q[o + 2], q[o + 3]);
        }

        public Vector3 RestOffsetKimodo(int joint)
        {
            var r = skeleton.rest_offsets_m_flat;
            return new Vector3(r[3 * joint], r[3 * joint + 1], r[3 * joint + 2]);
        }

        public Vector3 TargetKimodo() => new Vector3(target.position_m[0], target.position_m[1], target.position_m[2]);
    }

    [Serializable]
    public class UnityReadback
    {
        public string schema = "mira.unity_readback/0.1";
        public string episode_id;
        public string coordinate_frame = CoordinateConversion.UnityFrame;
        public string unity_version;
        public int[] frames;
        public string[] joint_names;
        public float[] world_positions_m_flat;
    }

    [Serializable]
    public class ExpectedUnityPositions
    {
        public string coordinate_frame;
        public string[] joint_names;
        public int[] frames;
        public float[] world_positions_m_flat;
    }
}
