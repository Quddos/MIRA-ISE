// Phase 5C-18: deterministic research camera presets.
//
// The framing is DATA, not a constant chosen by eye: prepare_inputs computes the union bounding box of
// all eight embodied clips and writes the centre and distance that contain it. One framing therefore
// serves every case and both executors, which is what makes an analytic/Kimodo pair comparable -- the
// camera cannot have been nudged to favour either, because it never saw which clip it was framing.
//
// Each preset is a fixed unit direction from that centre. Nothing here reads a trajectory.
using System;
using UnityEngine;

namespace Mira.Embodiment
{
    [Serializable]
    public class CameraFraming
    {
        public string schema;
        public string framing_id;
        public float[] center_unity_m;
        public float distance_m;
        public float fov_deg;
        public float top_fov_deg;
        public int width;
        public int height;
        public string derived_from;
        public bool depends_on_a_single_trajectory;
        public bool tuned_per_executor;

        public const string Schema = "mira.phase5c18_unity_camera/0.1";

        public static CameraFraming Load(string path)
        {
            var f = JsonUtility.FromJson<CameraFraming>(System.IO.File.ReadAllText(path));
            if (f.schema != Schema) throw new FormatException("unexpected camera framing schema " + f.schema);
            if (f.tuned_per_executor) throw new FormatException("a per-executor camera framing is not permitted");
            if (f.depends_on_a_single_trajectory) throw new FormatException("a camera framed on one trajectory is not permitted");
            if (f.distance_m <= 0f || f.fov_deg <= 0f) throw new FormatException("invalid camera framing");
            return f;
        }

        public Vector3 Center => new Vector3(center_unity_m[0], center_unity_m[1], center_unity_m[2]);

        /// Fallback framing, used only when no framing file is supplied (tests and ad-hoc previews).
        public static CameraFraming Default(Vector3 anchorUnity) => new CameraFraming
        {
            schema = Schema,
            framing_id = "FALLBACK_ANCHOR_FRAMING",
            center_unity_m = new[] { anchorUnity.x, anchorUnity.y - 0.05f, anchorUnity.z + 0.45f },
            distance_m = 2.6f,
            fov_deg = 42f,
            top_fov_deg = 45f,
            width = 1280,
            height = 960,
            derived_from = "the canonical embodiment anchor",
            depends_on_a_single_trajectory = false,
            tuned_per_executor = false,
        };
    }

    public static class CameraPresets
    {
        public const string Front = "CAMERA_FRONT";
        public const string Side = "CAMERA_SIDE";
        public const string ThreeQuarter = "CAMERA_3QUARTER";
        public const string Top = "CAMERA_TOP";
        public static readonly string[] All = { Front, Side, ThreeQuarter, Top };

        /// Unit direction from the framing centre to the camera. The acting arm is the left arm, which
        /// is -X in Unity, so the side view looks along it.
        public static Vector3 Direction(string preset)
        {
            switch (preset)
            {
                case Front: return new Vector3(0f, 0.10f, -1f).normalized;
                case Side: return new Vector3(-1f, 0.10f, 0f).normalized;
                case ThreeQuarter: return new Vector3(-0.72f, 0.34f, -0.72f).normalized;
                case Top: return new Vector3(0f, 1f, 0.02f).normalized;
                default: throw new ArgumentException("unknown camera preset " + preset);
            }
        }

        public static Vector3 Position(string preset, CameraFraming framing) =>
            framing.Center + Direction(preset) * framing.distance_m;

        public static float FieldOfView(string preset, CameraFraming framing) =>
            preset == Top ? framing.top_fov_deg : framing.fov_deg;
    }
}
