// Phase 5C-6B: DIGITAL_PATIENT_MOTION_DRIVER_V1, the minimum runtime that executes one frozen
// analytic pose realization on the existing SOMA30 avatar.
//
// It applies motion; it never authors it. Every operation the Phase 5C-6 driver contract forbids is
// absent rather than present-and-unused: no smoothing, no filtering, no IK, no joint clamping, no
// retargeting, no physics or collision correction, no duration change, no resampling.
//
// Frame selection is the canonical index, one avatar frame per canonical frame, with no
// interpolation. Simulation time is frame / fps; wall-clock time is never read.
using System;
using System.Collections.Generic;
using Mira.Data;
using Mira.DigitalPatient;
using UnityEngine;

namespace Mira.PoseExecution
{
    /// One frame of runtime avatar state, read back from the live transforms.
    [Serializable]
    public class PoseFrameState
    {
        public int frame_index;
        public float canonical_timestamp_s;
        public float elapsed_s;
        public bool complete;
        public float[] root_position_unity;
        public float[] shoulder_position_unity;
        public float[] elbow_position_unity;
        public float[] effector_position_unity;
        public float[] joint_positions_unity_flat;   // 30 x 3, world space, Unity frame
        public float[] effector_position_canonical;  // converted back for comparison
        public float[] shoulder_position_canonical;
        public float[] elbow_position_canonical;
        public float[] joint_positions_canonical_flat;
    }

    [Serializable]
    public class PoseRunRecord
    {
        public string driver_version;
        public string adapter_version;
        public string interpolation;
        public string root_motion_mode;
        public string target_motion_sha256;
        public string motion_request_id;
        public string pose_method_version;
        public string rotation_provenance;
        public bool executor_native;
        public int n_frames;
        public float fps;
        public float requested_duration_s;
        public float realized_duration_s;
        public bool completed;
        public string unity_version;
        public string[] joint_names;
        public float[] target_position_canonical;
        public string effector_joint;
        public List<PoseFrameState> frames = new List<PoseFrameState>();
    }

    public sealed class PoseExecutionDriver : IDisposable
    {
        public const string DriverVersion = "DIGITAL_PATIENT_MOTION_DRIVER_V1";

        public readonly PoseExecutionArtifact Artifact;
        public readonly UnityEpisode Episode;
        public readonly Soma30Avatar Avatar;
        public readonly GameObject Root;
        readonly int shoulder, elbow, effector;

        public int Frame { get; private set; } = -1;
        public bool Complete => Frame >= Episode.n_frames - 1;
        public float ElapsedSeconds => Frame < 0 ? 0f : Frame / Artifact.motion.fps;

        public PoseExecutionDriver(PoseExecutionArtifact artifact, AvatarStyle style,
                                   Material body = null, Material joint = null,
                                   Transform parent = null)
        {
            Artifact = artifact;
            Episode = artifact.ToEpisode();
            Root = new GameObject("MIRA_PoseExecution");
            if (parent != null) Root.transform.SetParent(parent, false);
            Avatar = new Soma30Avatar(Episode, Root.transform, style, body, joint);

            string side = artifact.target.required_arm == "left" ? "Left" : "Right";
            shoulder = Avatar.JointIndex(side + "Arm");
            elbow = Avatar.JointIndex(side + "ForeArm");
            effector = Avatar.JointIndex(side + "Hand");
            if (shoulder < 0 || elbow < 0 || effector < 0)
                throw new InvalidOperationException("[MIRA] arm joints not found on the avatar");
        }

        /// Advance exactly one canonical frame. No interpolation, no time accumulation.
        public bool Step()
        {
            if (Complete) return false;
            Frame++;
            Avatar.ApplyFrame(Frame);
            return true;
        }

        public PoseFrameState CaptureState()
        {
            var joints = new List<float>(Soma30Count * 3);
            Avatar.AppendWorldPositions(joints);
            var canonical = new float[joints.Count];
            for (int j = 0; j < joints.Count; j += 3)
            {
                var unity = new Vector3(joints[j], joints[j + 1], joints[j + 2]);
                var back = CoordinateConversion.UnityToKimodoPosition(unity);
                canonical[j] = back.x; canonical[j + 1] = back.y; canonical[j + 2] = back.z;
            }
            return new PoseFrameState
            {
                frame_index = Frame,
                canonical_timestamp_s = Artifact.motion.timestamps_s[Frame],
                elapsed_s = ElapsedSeconds,
                complete = Complete,
                root_position_unity = Vec(Avatar.Joints[0].position),
                shoulder_position_unity = Vec(Avatar.WorldPosition(shoulder)),
                elbow_position_unity = Vec(Avatar.WorldPosition(elbow)),
                effector_position_unity = Vec(Avatar.WorldPosition(effector)),
                joint_positions_unity_flat = joints.ToArray(),
                effector_position_canonical = Canonical(Avatar.WorldPosition(effector)),
                shoulder_position_canonical = Canonical(Avatar.WorldPosition(shoulder)),
                elbow_position_canonical = Canonical(Avatar.WorldPosition(elbow)),
                joint_positions_canonical_flat = canonical,
            };
        }

        public PoseRunRecord RunToEnd()
        {
            var record = new PoseRunRecord
            {
                driver_version = DriverVersion,
                adapter_version = Artifact.playback.adapter_version,
                interpolation = Artifact.playback.interpolation,
                root_motion_mode = Artifact.pose_realization.root_motion_mode,
                target_motion_sha256 = Artifact.derived_from.target_motion_sha256,
                motion_request_id = Artifact.derived_from.motion_request_id,
                pose_method_version = Artifact.pose_realization.method_version,
                rotation_provenance = Artifact.pose_realization.rotation_provenance,
                executor_native = Artifact.pose_realization.executor_native,
                n_frames = Artifact.motion.n_frames,
                fps = Artifact.motion.fps,
                requested_duration_s = Artifact.motion.requested_duration_s,
                unity_version = Application.unityVersion,
                joint_names = Artifact.skeleton.joint_names,
                target_position_canonical = Artifact.target.position_m,
                effector_joint = (Artifact.target.required_arm == "left" ? "Left" : "Right") + "Hand",
            };
            while (Step()) record.frames.Add(CaptureState());
            record.completed = Complete;
            record.realized_duration_s = record.frames.Count > 0
                ? record.frames[record.frames.Count - 1].canonical_timestamp_s : 0f;
            return record;
        }

        const int Soma30Count = 30;

        static float[] Vec(Vector3 v) => new[] { v.x, v.y, v.z };

        static float[] Canonical(Vector3 unity)
        {
            var back = CoordinateConversion.UnityToKimodoPosition(unity);
            return new[] { back.x, back.y, back.z };
        }

        public void Dispose()
        {
            if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
        }

        // Deliberately absent, because the Phase 5C-6 driver contract forbids them:
        //   no Smooth, no Filter, no SolveIK, no ClampJoints, no Retarget, no SetDuration,
        //   no ModifyTarget, no physics or collision correction, no Animator IK, no Resample.
    }
}
