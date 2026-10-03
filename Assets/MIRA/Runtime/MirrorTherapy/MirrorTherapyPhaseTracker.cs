// MIRA-ISE Milestone 1: deterministic task-phase logic for one mirror-therapy reach trial.
//
// This is NOT phase estimation. It is a fixed rule over environment-observable quantities only: the
// simulation frame, whether the target has been presented, the reference effector's distance to the active
// target, and its distance to the trial's rest position (the effector position at the trial's first frame).
// It never reads the patient's hidden phase schedule (evaluator-only ground truth in MIRA).
//
//   REST           -> REACH           effector leaves the rest zone after the target is presented
//   REST / REACH   -> TARGET_CONTACT  effector within the target radius (same rule as ReachingTaskMonitor)
//   REACH          -> REST            effector back in the rest zone without contact (aborted attempt)
//   TARGET_CONTACT -> RETURN          effector leaves the target radius
//   RETURN         -> TARGET_CONTACT  re-contact before returning
//   RETURN         -> REST            effector back in the rest zone
//   any            -> COMPLETE        set by the session controller after the last trial
//
// The contact rule `distance <= radius at a frame >= target_presented_frame` is identical to the existing MIRA
// acquisition rule (ReachingTaskMonitor / Python task engine), so a trial's success agrees with MIRA's own.
using UnityEngine;

namespace Mira.MirrorTherapy
{
    /// Task phases exposed to later milestones. Names follow the Milestone 1 specification.
    public enum MirrorTherapyPhase { REST, REACH, TARGET_CONTACT, RETURN, COMPLETE }

    public enum MirrorTherapyTrialOutcome { Pending, Success, Missed }

    public class MirrorTherapyPhaseTracker
    {
        public readonly float RestRadiusM;

        public MirrorTherapyPhase Phase { get; private set; } = MirrorTherapyPhase.REST;
        public Vector3 RestPosition { get; private set; }
        public Vector3 Target { get; private set; }
        public float TargetRadiusM { get; private set; }
        public int TargetPresentedFrame { get; private set; }
        public int? ContactFrame { get; private set; }
        public int? ReachOnsetFrame { get; private set; }
        public int? ReturnCompleteFrame { get; private set; }
        public float MinDistanceM { get; private set; } = float.PositiveInfinity;
        public bool Contacted => ContactFrame.HasValue;

        public MirrorTherapyPhaseTracker(float restRadiusM)
        {
            if (!(restRadiusM > 0f)) throw new System.ArgumentException("[MIRA-ISE] rest radius must be positive");
            RestRadiusM = restRadiusM;
        }

        public void BeginTrial(Vector3 restPosition, Vector3 target, float targetRadiusM, int targetPresentedFrame)
        {
            if (!(targetRadiusM > 0f)) throw new System.ArgumentException("[MIRA-ISE] target radius must be positive");
            if ((target - restPosition).magnitude <= targetRadiusM + RestRadiusM)
                throw new System.ArgumentException("[MIRA-ISE] rest zone and target zone overlap; phases would be ambiguous");
            RestPosition = restPosition;
            Target = target;
            TargetRadiusM = targetRadiusM;
            TargetPresentedFrame = targetPresentedFrame;
            ContactFrame = null;
            ReachOnsetFrame = null;
            ReturnCompleteFrame = null;
            MinDistanceM = float.PositiveInfinity;
            Phase = MirrorTherapyPhase.REST;
        }

        /// Advance one simulation frame. Returns the distance from the effector to the target.
        public float Step(int frame, Vector3 effector)
        {
            if (Phase == MirrorTherapyPhase.COMPLETE) return Vector3.Distance(effector, Target);
            float d = Vector3.Distance(effector, Target);
            if (d < MinDistanceM) MinDistanceM = d;
            bool presented = frame >= TargetPresentedFrame;
            bool inContact = presented && d <= TargetRadiusM;
            bool atRest = Vector3.Distance(effector, RestPosition) <= RestRadiusM;

            if (inContact && !ContactFrame.HasValue) ContactFrame = frame;

            switch (Phase)
            {
                case MirrorTherapyPhase.REST:
                    if (inContact) Phase = MirrorTherapyPhase.TARGET_CONTACT;
                    else if (presented && !atRest && !Contacted) { Phase = MirrorTherapyPhase.REACH; ReachOnsetFrame ??= frame; }
                    break;
                case MirrorTherapyPhase.REACH:
                    if (inContact) Phase = MirrorTherapyPhase.TARGET_CONTACT;
                    else if (atRest) Phase = MirrorTherapyPhase.REST;
                    break;
                case MirrorTherapyPhase.TARGET_CONTACT:
                    if (!inContact) Phase = MirrorTherapyPhase.RETURN;
                    break;
                case MirrorTherapyPhase.RETURN:
                    if (inContact) Phase = MirrorTherapyPhase.TARGET_CONTACT;
                    else if (atRest) { Phase = MirrorTherapyPhase.REST; ReturnCompleteFrame ??= frame; }
                    break;
            }
            return d;
        }

        public MirrorTherapyTrialOutcome Outcome => Contacted ? MirrorTherapyTrialOutcome.Success : MirrorTherapyTrialOutcome.Missed;

        public void MarkComplete() => Phase = MirrorTherapyPhase.COMPLETE;
    }
}
