// Generic motor-task layer (upper_limb_goal_directed_reaching). Knows targets and effectors,
// nothing about table tennis or impairments. Mirrors the Python task engine's acquisition rule:
// acquired when the effector joint is within the target radius at any frame.
using Mira.Data;
using UnityEngine;

namespace Mira.MotorTask
{
    public enum EpisodePhase { StartPose, TargetPresentation, Movement, EpisodeComplete }

    public class ReachingTaskMonitor
    {
        public const string TaskId = "upper_limb_goal_directed_reaching";

        public readonly Vector3 TargetUnity;
        public readonly float RadiusM;
        public readonly string EffectorJoint;
        public readonly int TargetPresentedFrame;
        public int? AcquisitionFrame { get; private set; }
        public float MinDistanceM { get; private set; } = float.PositiveInfinity;

        public ReachingTaskMonitor(UnityEpisode ep)
        {
            TargetUnity = CoordinateConversion.KimodoToUnityPosition(ep.TargetKimodo());
            RadiusM = ep.target.radius_m;
            EffectorJoint = (ep.target.required_arm == "left" ? "Left" : "Right") + "Hand";
            TargetPresentedFrame = ep.task_events.target_presented_frame;
        }

        /// Phase from environment-known events only (the monitor has no access to the patient's schedule).
        public EpisodePhase PhaseAt(int frame, int nFrames)
        {
            if (frame >= nFrames - 1) return EpisodePhase.EpisodeComplete;
            if (frame < TargetPresentedFrame) return EpisodePhase.StartPose;
            return AcquisitionFrame.HasValue ? EpisodePhase.Movement : EpisodePhase.TargetPresentation;
        }

        public float Update(int frame, Vector3 effectorUnity)
        {
            float d = Vector3.Distance(effectorUnity, TargetUnity);
            if (d < MinDistanceM) MinDistanceM = d;
            if (!AcquisitionFrame.HasValue && frame >= TargetPresentedFrame && d <= RadiusM) AcquisitionFrame = frame;
            return d;
        }
    }
}
