// Mirror of schemas/capture_config.v1.json and schemas/capture_job.v1.json (JsonUtility-compatible).
using System;
using System.IO;
using UnityEngine;

namespace Mira.Capture
{
    [Serializable]
    public class CameraConfig
    {
        public string camera_id;
        public string projection;
        public string coordinate_frame;
        public float[] position;
        public float[] rotation_euler_deg;
        public float fov_deg;
        public float near_clip_m;
        public float far_clip_m;
        public int width;
        public int height;
        public float[] background_rgb;
    }

    [Serializable]
    public class LightingConfig
    {
        public float[] directional_rotation_euler_deg;
        public float directional_intensity;
        public float[] directional_rgb;
        public string shadows;
        public float shadow_strength;
        public float shadow_distance_m;
        public float[] ambient_rgb;
    }

    [Serializable]
    public class RenderConfig
    {
        public int msaa_samples;
        public string image_format;
    }

    [Serializable]
    public class AvatarVisualConfig
    {
        public string style;
        public float[] body_rgb;
        public float[] joint_rgb;
    }

    [Serializable]
    public class EnvironmentVisualConfig
    {
        public float[] floor_rgb;
        public float[] table_rgb;
        public float[] net_rgb;
        public float[] target_rgb;
        public bool target_color_changes_on_acquisition;
    }

    [Serializable]
    public class SkeletonOverlayConfig
    {
        public float[] rgb;
        public float joint_marker_diameter_m;
        public float bone_width_m;
        public bool draw_on_top;
    }

    [Serializable]
    public class BenchmarkModeConfig
    {
        public bool debug_overlays;
        public bool text_allowed;
    }

    [Serializable]
    public class CaptureConfig
    {
        public string schema;
        public string capture_config_id;
        public CameraConfig camera;
        public LightingConfig lighting;
        public RenderConfig render;
        public AvatarVisualConfig avatar;
        public EnvironmentVisualConfig environment;
        public SkeletonOverlayConfig skeleton_overlay;
        public BenchmarkModeConfig benchmark_mode;

        public const string DefaultStreamingPath = "MIRA/capture/baseline_over_shoulder_v1.json";

        public static CaptureConfig FromJson(string json)
        {
            var c = JsonUtility.FromJson<CaptureConfig>(json);
            if (c.schema != "mira.capture_config/0.1") throw new FormatException("unexpected capture config schema " + c.schema);
            if (c.camera.projection != "perspective") throw new FormatException("only perspective projection is supported");
            if (c.camera.coordinate_frame != "unity_lh_yup_zfwd") throw new FormatException("camera pose must be given in unity_lh_yup_zfwd");
            if (c.camera.width <= 0 || c.camera.height <= 0) throw new FormatException("invalid resolution");
            return c;
        }

        public static CaptureConfig LoadDefault()
        {
            string p = Path.Combine(Application.streamingAssetsPath, DefaultStreamingPath);
            return File.Exists(p) ? FromJson(File.ReadAllText(p)) : null;
        }

        public static Color Rgb(float[] v) => new Color(v[0], v[1], v[2], 1f);
        public static Vector3 V3(float[] v) => new Vector3(v[0], v[1], v[2]);
    }

    [Serializable]
    public class CaptureEpisodeJob
    {
        public string episode_file;
        public string output_dir;
        public int[] frames;
        public bool skeleton_overlay;
        /// Observed joint positions (canonical frame) per requested frame: 90 floats per frame,
        /// concatenated in the order of `frames`. `overlay_frame_valid[i] == false` = frame dropped by the sensor.
        public float[] overlay_positions_kimodo_flat;
        public bool[] overlay_frame_valid;
    }

    [Serializable]
    public class CaptureJob
    {
        public string schema;
        public string capture_config_file;
        public CaptureEpisodeJob[] episodes;
    }

    [Serializable]
    public class CapturedFrame
    {
        public int sample_index;
        public int source_frame;
        public float sim_time_s;
        public string file;
        public bool skeleton_overlay;
        public bool overlay_frame_valid;
    }

    [Serializable]
    public class CaptureRecord
    {
        public string schema = "mira.capture_record/0.1";
        public string episode_id;
        public string clip_sha256;
        public string capture_config_id;
        public string capture_config_sha256;
        public string camera_id;
        public int width;
        public int height;
        public string image_format;
        public int fps;
        public string unity_version;
        public string graphics_device;
        public string graphics_api;
        public string render_pipeline;
        public string color_space;
        public string avatar_style;
        public bool audit_passed;
        public string[] audit_findings;
        public CapturedFrame[] frames;
    }
}
