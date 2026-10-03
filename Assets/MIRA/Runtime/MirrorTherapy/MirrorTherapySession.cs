// MIRA-ISE Milestone 1: mirror-therapy session manifest (mira_ise.mirror_therapy_session/0.1).
// Written by scripts/mira_ise_m1/export_mirror_therapy_session.py from EXISTING Phase 5A benchmark cases. A session
// is an ordered list of trials; each trial is one unchanged unity_episode.v0 reach episode played by the existing
// EpisodeRunner. The session is also the target layout: target ID, position, radius, order and difficulty.
//
// Target positions are carried twice (session + episode) on purpose. The motion was generated for the episode's
// target, so a layout that disagreed with the episode would show a target the patient never reached for. Validate()
// fails closed on any mismatch instead of trusting either copy.
//
// Everything here is synthetic. `kinematics` values are computed by the existing MIRA kinematic engine
// (kinematic_summary_v2) on the canonical clip; they are computational state variables, not clinical measurements.
using System;
using System.IO;
using Mira.Data;
using UnityEngine;

namespace Mira.MirrorTherapy
{
    [Serializable]
    public class MirrorTherapyImpairmentLabel
    {
        public string condition;
        public string severity;
        public string[] mechanisms;
        public string severity_mapping_id;
        public string mira_affected_side;
        public string note;
    }

    /// Existing MIRA kinematic_summary_v2 fields for one trial. NaN = the existing engine reported null.
    [Serializable]
    public class MirrorTherapyTrialKinematics
    {
        public float movement_duration_s = float.NaN;
        public float path_length_m = float.NaN;
        public float reach_distance_m = float.NaN;
        public float trajectory_efficiency = float.NaN;
        public float log_dimensionless_jerk = float.NaN;
        public float sparc = float.NaN;
        public float peak_endpoint_speed_mps = float.NaN;
        public float peak_shoulder_elevation_deg = float.NaN;
        public float shoulder_elevation_excursion_deg = float.NaN;
        public float peak_elbow_flexion_deg = float.NaN;
        public float peak_trunk_flexion_deg = float.NaN;
        public float peak_trunk_rotation_deg = float.NaN;
        public float endpoint_min_distance_m = float.NaN;
        public bool target_acquired;
    }

    [Serializable]
    public class MirrorTherapyTrial
    {
        public int trial_index;
        public int order;
        public string case_id;
        public string episode_file;      // relative to StreamingAssets
        public string episode_sha256;
        public string clip_sha256;
        public string target_id;
        public float[] target_position_m; // canonical (kimodo_rh_yup_zfwd)
        public float target_radius_m;
        public string direction;
        public string distance_class;
        public int difficulty;           // ordinal layout label (near=1, middle=2, far=3), not a clinical grade
        public string required_arm;
        public int target_presented_frame;
        public int n_frames;
        public MirrorTherapyTrialKinematics kinematics;
        public string[] kinematics_null_fields;   // metrics the existing engine reported as null (omitted above)

        public Vector3 TargetUnity => CoordinateConversion.KimodoToUnityPosition(
            new Vector3(target_position_m[0], target_position_m[1], target_position_m[2]));
    }

    [Serializable]
    public class MirrorTherapySession
    {
        public const string Schema = "mira_ise.mirror_therapy_session/0.1";
        public const string DefaultFile = "MIRA/mirror_therapy/MT_NONE_NONE_V0.session.json";
        const float PositionToleranceM = 1e-5f;

        public string schema;
        public string session_id;
        public string task_name;
        public string scientific_motor_task;
        public string source_benchmark_id;
        public string patient_id;
        public string morphology_id;
        public MirrorTherapyImpairmentLabel impairment;
        public string reference_arm;
        public string mirrored_side;
        public int fps;
        public string kinematics_provenance;
        public string[] metrics_not_available;
        public bool synthetic_only;
        public MirrorTherapyTrial[] trials;

        public static string ResolvePath(string relative) => Path.Combine(Application.streamingAssetsPath, relative);

        public static MirrorTherapySession Load(string relativePath)
        {
            string path = ResolvePath(relativePath);
            if (!File.Exists(path)) throw new FileNotFoundException("[MIRA-ISE] session file not found", path);
            var s = JsonUtility.FromJson<MirrorTherapySession>(File.ReadAllText(path));
            s.ApplyNullMetrics();
            s.Validate();
            return s;
        }

        /// Metrics the exporter omitted because the existing engine reported null are set to NaN explicitly,
        /// so "not computed" can never read as a measured 0.
        void ApplyNullMetrics()
        {
            if (trials == null) return;
            foreach (var t in trials)
            {
                if (t.kinematics == null) t.kinematics = new MirrorTherapyTrialKinematics();
                if (t.kinematics_null_fields == null) continue;
                foreach (string f in t.kinematics_null_fields)
                {
                    var field = typeof(MirrorTherapyTrialKinematics).GetField(f);
                    if (field != null && field.FieldType == typeof(float)) field.SetValue(t.kinematics, float.NaN);
                }
            }
        }

        /// Fail closed: a session that does not describe itself consistently is not run.
        public void Validate()
        {
            if (schema != Schema) throw new FormatException($"[MIRA-ISE] unexpected session schema '{schema}'");
            if (!synthetic_only) throw new FormatException("[MIRA-ISE] Milestone 1 sessions must be synthetic_only");
            if (fps != 30) throw new FormatException("[MIRA-ISE] canonical fps is 30");
            if (reference_arm != "left" && reference_arm != "right") throw new FormatException($"[MIRA-ISE] invalid reference_arm '{reference_arm}'");
            if (mirrored_side != (reference_arm == "left" ? "right" : "left")) throw new FormatException("[MIRA-ISE] mirrored_side must be opposite reference_arm");
            if (trials == null || trials.Length == 0) throw new FormatException("[MIRA-ISE] a session needs at least one trial");
            for (int i = 0; i < trials.Length; i++)
            {
                var t = trials[i];
                if (t.trial_index != i) throw new FormatException($"[MIRA-ISE] trial {i} has trial_index {t.trial_index}");
                if (string.IsNullOrEmpty(t.target_id)) throw new FormatException($"[MIRA-ISE] trial {i} has no target_id");
                if (t.target_position_m == null || t.target_position_m.Length != 3) throw new FormatException($"[MIRA-ISE] trial {i} target position must be a 3-vector");
                if (!(t.target_radius_m > 0f)) throw new FormatException($"[MIRA-ISE] trial {i} target radius must be positive");
                if (t.required_arm != reference_arm) throw new FormatException($"[MIRA-ISE] trial {i} acts with '{t.required_arm}', session reference arm is '{reference_arm}'");
            }
        }

        /// Cross-check a loaded episode against the trial that references it.
        public static void CheckEpisodeMatchesTrial(UnityEpisode ep, MirrorTherapyTrial t)
        {
            if (ep.episode_id != t.case_id) throw new FormatException($"[MIRA-ISE] episode '{ep.episode_id}' does not match trial case '{t.case_id}'");
            if (ep.motion.clip_sha256 != t.clip_sha256) throw new FormatException($"[MIRA-ISE] clip hash mismatch for {t.case_id}");
            if (ep.target.target_id != t.target_id) throw new FormatException($"[MIRA-ISE] {t.case_id}: episode target {ep.target.target_id} != layout target {t.target_id}");
            if ((ep.TargetKimodo() - new Vector3(t.target_position_m[0], t.target_position_m[1], t.target_position_m[2])).magnitude > PositionToleranceM)
                throw new FormatException($"[MIRA-ISE] {t.case_id}: episode target position differs from the session layout");
            if (Mathf.Abs(ep.target.radius_m - t.target_radius_m) > 1e-6f) throw new FormatException($"[MIRA-ISE] {t.case_id}: target radius mismatch");
            if (ep.target.required_arm != t.required_arm) throw new FormatException($"[MIRA-ISE] {t.case_id}: acting arm mismatch");
            if (ep.n_frames != t.n_frames || ep.task_events.target_presented_frame != t.target_presented_frame)
                throw new FormatException($"[MIRA-ISE] {t.case_id}: frame count or target-presentation frame mismatch");
        }
    }
}
