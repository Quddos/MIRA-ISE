// Batch RGB capture (Phase 5B). Requires a graphics device: run WITHOUT -nographics.
//   Unity.exe -batchmode -projectPath <p> -executeMethod Mira.EditorTools.MiraCapture.Run -miraJob <capture_job.json> -logFile <log>
// For each episode: build the capture scene from the capture config, run the episode lockstep, and at
// each requested frame render frame_NNN.png (sample order). V1 jobs also draw the observed-skeleton
// overlay from the job's observed positions. A benchmark-mode audit runs before any frame is written;
// an audit failure aborts with exit code 2.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mira.Capture;
using Mira.Playback;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mira.EditorTools
{
    public static class MiraCapture
    {
        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static string Sha256Hex(byte[] data)
        {
            using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(data).Select(b => b.ToString("x2")));
        }

        public static void Run()
        {
            int code = 0;
            try
            {
                code = RunJob(Arg("-miraJob"));
            }
            catch (Exception e)
            {
                Debug.LogError("[MIRA] capture failed: " + e);
                code = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        public static int RunJob(string jobPath)
        {
            var job = JsonUtility.FromJson<CaptureJob>(File.ReadAllText(jobPath));
            if (job.schema != "mira.capture_job/0.1") throw new FormatException("unexpected job schema " + job.schema);
            byte[] cfgBytes = File.ReadAllBytes(job.capture_config_file);
            var cfg = CaptureConfig.FromJson(System.Text.Encoding.UTF8.GetString(cfgBytes));
            string cfgSha = Sha256Hex(cfgBytes);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("no graphics device: run capture without -nographics");

            int done = 0;
            foreach (var e in job.episodes)
            {
                var ep = EpisodeRunner.Load(e.episode_file);
                using (var scene = new CaptureScene(cfg))
                using (var runner = scene.BuildRunner(ep))
                {
                    SkeletonOverlay overlay = null;
                    if (e.skeleton_overlay)
                    {
                        if (e.overlay_positions_kimodo_flat == null || e.overlay_positions_kimodo_flat.Length != 90 * e.frames.Length)
                            throw new FormatException($"{ep.episode_id}: overlay positions must be 90 floats per requested frame");
                        overlay = new SkeletonOverlay(scene.Root.transform, ep.skeleton.parent_indices, cfg.skeleton_overlay);
                    }
                    var findings = scene.Audit(ep.episode_id, scene.AllowedColors());
                    if (findings.Count > 0)
                    {
                        Debug.LogError("[MIRA] capture audit failed: " + string.Join("; ", findings));
                        return 2;
                    }
                    Directory.CreateDirectory(e.output_dir);
                    var wanted = new Dictionary<int, int>();
                    for (int i = 0; i < e.frames.Length; i++) wanted[e.frames[i]] = i;
                    var frames = new List<CapturedFrame>();
                    while (!runner.Done)
                    {
                        runner.Step();
                        if (!wanted.TryGetValue(runner.Frame, out int k)) continue;
                        bool valid = true;
                        if (overlay != null)
                        {
                            valid = e.overlay_frame_valid[k];
                            overlay.Show(e.overlay_positions_kimodo_flat, 90 * k, valid);
                        }
                        string file = $"frame_{k:D3}.png";
                        scene.RenderPng(Path.Combine(e.output_dir, file));
                        overlay?.Hide();
                        frames.Add(new CapturedFrame { sample_index = k, source_frame = runner.Frame, sim_time_s = runner.Frame / (float)ep.fps, file = file, skeleton_overlay = overlay != null, overlay_frame_valid = valid });
                    }
                    if (frames.Count != e.frames.Length) throw new InvalidOperationException($"{ep.episode_id}: captured {frames.Count} of {e.frames.Length} frames");
                    var record = new CaptureRecord
                    {
                        episode_id = ep.episode_id,
                        clip_sha256 = ep.motion.clip_sha256,
                        capture_config_id = cfg.capture_config_id,
                        capture_config_sha256 = cfgSha,
                        camera_id = cfg.camera.camera_id,
                        width = cfg.camera.width,
                        height = cfg.camera.height,
                        image_format = cfg.render.image_format,
                        fps = ep.fps,
                        unity_version = Application.unityVersion,
                        graphics_device = SystemInfo.graphicsDeviceName,
                        graphics_api = SystemInfo.graphicsDeviceType.ToString(),
                        render_pipeline = GraphicsSettings.currentRenderPipeline == null ? "built-in" : GraphicsSettings.currentRenderPipeline.name,
                        color_space = QualitySettings.activeColorSpace.ToString(),
                        avatar_style = cfg.avatar.style,
                        audit_passed = true,
                        audit_findings = new string[0],
                        frames = frames.OrderBy(f => f.sample_index).ToArray(),
                    };
                    File.WriteAllText(Path.Combine(e.output_dir, "capture_record.json"), JsonUtility.ToJson(record, true));
                }
                done++;
                if (done % 10 == 0) Debug.Log($"[MIRA] captured {done}/{job.episodes.Length} episodes");
            }
            Debug.Log($"[MIRA] capture complete: {done} episodes");
            return 0;
        }
    }
}
