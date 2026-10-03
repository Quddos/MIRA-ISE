// Phase 5C-18 batch entry points.
//
//   Readback (no graphics needed):
//     Unity.exe -batchmode -nographics -quit -projectPath <p>
//       -executeMethod Mira.EditorTools.MiraEmbodiment.Readback -miraClip <clip.unity.json> -miraOut <readback.json>
//
//   Capture (needs a graphics device: run WITHOUT -nographics):
//     Unity.exe -batchmode -projectPath <p> -executeMethod Mira.EditorTools.MiraEmbodiment.Capture
//       -miraClip <clip.unity.json> -miraOut <dir> -miraEnv <environment.json>
//       [-miraKind capture_single_pose|capture_motion_track|capture_trajectory_view|all]
//       [-miraCamera CAMERA_3QUARTER|all] [-miraFrame <n>] [-miraWidth 1280] [-miraHeight 960]
//
// Everything written here is derived from the frozen clip. Nothing is tuned per executor, and the camera
// is never adjusted by hand.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Mira.Embodiment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mira.EditorTools
{
    public static class MiraEmbodiment
    {
        static string Arg(string name, string fallback = null)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        static int IntArg(string name, int fallback) =>
            int.TryParse(Arg(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

        static void Finish(int code)
        {
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static readonly Color AnalyticColor = new Color(0.106f, 0.498f, 0.431f);
        static readonly Color KimodoColor = new Color(0.557f, 0.239f, 0.557f);
        static readonly Color TargetReachColor = new Color(0.97f, 0.76f, 0.20f);

        static Color BodyColor(Soma30MotionClip clip) => clip.executor_label == "analytic" ? AnalyticColor : KimodoColor;

        // ---------------------------------------------------------------- readback

        public static void Readback()
        {
            try
            {
                var clip = Soma30MotionClip.Load(Arg("-miraClip"));
                string outPath = Arg("-miraOut");
                var record = ReadbackClip(clip);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
                File.WriteAllText(outPath, JsonUtility.ToJson(record, true));
                Debug.Log($"[MIRA] 5C-18 readback {clip.clip_id}: {record.n_frames} frames, {record.n_represented} joints, state {record.clip_state_digest.Substring(0, 16)}");
                Finish(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[MIRA] 5C-18 readback failed: " + e);
                Finish(1);
            }
        }

        public static ClipReadback ReadbackClip(Soma30MotionClip clip)
        {
            using (var body = new Soma30PositionEmbodiment(clip, null, false, Color.white, Color.white))
            {
                var frames = new FramePoseState[clip.n_frames];
                var digests = new string[clip.n_frames];
                int hand = clip.RepresentedIndexOf("LeftHand");
                for (int f = 0; f < clip.n_frames; f++)
                {
                    body.ApplyFrame(f);
                    var flat = new List<float>(clip.n_represented * 3);
                    body.AppendAppliedPositions(flat);
                    var applied = flat.ToArray();
                    string digest = PoseDigest.Compute(clip.@case, clip.executor, f, clip.represented_joint_names, applied, clip.adapter_sha256);
                    digests[f] = digest;
                    frames[f] = new FramePoseState
                    {
                        frame = f,
                        tau = clip.Tau(f),
                        time_s = clip.fps > 0f ? f / clip.fps : 0f,
                        is_target_reaching_frame = f == clip.target_reaching_frame,
                        hand_to_target_distance_m = hand >= 0 ? Vector3.Distance(body.WorldPosition(hand), clip.Target) : float.NaN,
                        applied_positions_m_flat = applied,
                        pose_state_digest = digest,
                    };
                }
                return new ClipReadback
                {
                    clip_id = clip.clip_id,
                    @case = clip.@case,
                    executor = clip.executor,
                    executor_label = clip.executor_label,
                    unity_version = Application.unityVersion,
                    graphics_device = SystemInfo.graphicsDeviceName,
                    graphics_api = SystemInfo.graphicsDeviceType.ToString(),
                    n_frames = clip.n_frames,
                    n_represented = clip.n_represented,
                    represented_joint_names = clip.represented_joint_names,
                    not_represented_joint_names = clip.NotRepresented(),
                    target_reaching_frame = clip.target_reaching_frame,
                    target_reaching_rule = clip.target_reaching_rule,
                    target_unity_m = clip.target_unity_m,
                    tolerance_m = clip.tolerance_m,
                    adapter_version = clip.adapter_version,
                    adapter_sha256 = clip.adapter_sha256,
                    clip_record_sha256 = clip.clip_record_sha256,
                    unity_positions_sha256 = clip.unity_positions_sha256,
                    clip_state_digest = PoseDigest.Combine(clip.@case, clip.executor, digests),
                    frames = frames,
                    interpolation_applied = false,
                    resampling_applied = false,
                    smoothing_applied = false,
                    ik_applied = false,
                    physics_applied = false,
                    local_rotations_derived = false,
                    joints_fabricated = false,
                    snapshot_labels = clip.snapshot_labels,
                    snapshot_frames = clip.snapshot_frames,
                };
            }
        }

        // ---------------------------------------------------------------- capture

        public static void Capture()
        {
            try
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    throw new InvalidOperationException("no graphics device: run capture without -nographics");
                var clip = Soma30MotionClip.Load(Arg("-miraClip"));
                string outDir = Arg("-miraOut");
                Directory.CreateDirectory(outDir);
                string envPath = Arg("-miraEnv");
                var env = string.IsNullOrEmpty(envPath) ? null : EmbodimentEnvironment.Load(envPath);
                string framingPath = Arg("-miraFraming");
                var framing = string.IsNullOrEmpty(framingPath) ? null : CameraFraming.Load(framingPath);
                string kind = Arg("-miraKind", "all");
                string cameraArg = Arg("-miraCamera", "CAMERA_3QUARTER");
                string[] cameras = cameraArg == "all" ? CameraPresets.All : new[] { cameraArg };
                int width = IntArg("-miraWidth", framing != null ? framing.width : 1280);
                int height = IntArg("-miraHeight", framing != null ? framing.height : 960);
                int frame = IntArg("-miraFrame", clip.target_reaching_frame);

                var written = new List<string>();
                using (var scene = new EmbodimentScene(clip, env, framing, width, height))
                {
                    foreach (string camera in cameras)
                    {
                        scene.ApplyCamera(camera);
                        if (kind == "all" || kind == "capture_single_pose")
                            written.Add(CaptureSinglePose(clip, scene, env, frame, camera, outDir));
                        if (kind == "all" || kind == "capture_snapshot_poses")
                            for (int s = 0; s < clip.snapshot_labels.Length; s++)
                                written.Add(CaptureSnapshotPose(clip, scene, env, s, camera, outDir));
                        if (kind == "all" || kind == "capture_motion_track")
                            written.Add(CaptureMotionTrack(clip, scene, env, camera, outDir));
                        if (kind == "all" || kind == "capture_trajectory_view")
                            written.Add(CaptureTrajectoryView(clip, scene, env, camera, outDir));
                    }
                }
                Debug.Log($"[MIRA] 5C-18 capture {clip.clip_id}: {written.Count} image(s) -> {outDir}");
                Finish(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[MIRA] 5C-18 capture failed: " + e);
                Finish(1);
            }
        }

        static string CaptureSinglePose(Soma30MotionClip clip, EmbodimentScene scene, EmbodimentEnvironment env,
                                        int frame, string camera, string outDir)
        {
            using (var player = new PosePlayer(clip, scene.Root.transform, true, BodyColor(clip), BodyColor(clip)))
            {
                player.Seek(frame);
                var flat = new List<float>();
                player.Body.AppendAppliedPositions(flat);
                string digest = PoseDigest.Compute(clip.@case, clip.executor, frame, clip.represented_joint_names, flat.ToArray(), clip.adapter_sha256);
                return Write(clip, scene, env, "capture_single_pose", camera, outDir,
                             $"{clip.@case}.{clip.executor_label}.pose_f{frame:D3}.{camera}", frame, clip.Tau(frame), digest);
            }
        }

        /// One render per motion-track snapshot, same camera for every panel and every executor, so the
        /// paper layout can be composed from real renders of the frozen poses rather than from ghosts
        /// displaced in world space.
        static string CaptureSnapshotPose(Soma30MotionClip clip, EmbodimentScene scene, EmbodimentEnvironment env,
                                          int snapshotIndex, string camera, string outDir)
        {
            string label = clip.snapshot_labels[snapshotIndex];
            int frame = clip.snapshot_frames[snapshotIndex];
            using (var player = new PosePlayer(clip, scene.Root.transform, true, BodyColor(clip), BodyColor(clip)))
            {
                player.Seek(frame);
                var flat = new List<float>();
                player.Body.AppendAppliedPositions(flat);
                string digest = PoseDigest.Compute(clip.@case, clip.executor, frame, clip.represented_joint_names, flat.ToArray(), clip.adapter_sha256);
                return Write(clip, scene, env, "capture_single_pose", camera, outDir,
                             $"{clip.@case}.{clip.executor_label}.snapshot_{snapshotIndex}_{label}.{camera}",
                             frame, clip.Tau(frame), digest);
            }
        }

        static string CaptureMotionTrack(Soma30MotionClip clip, EmbodimentScene scene, EmbodimentEnvironment env,
                                         string camera, string outDir)
        {
            using (var track = new MotionTrackRenderer(clip, scene.Root.transform, BodyColor(clip), TargetReachColor))
            {
                var readback = ReadbackClip(clip);
                return Write(clip, scene, env, "capture_motion_track", camera, outDir,
                             $"{clip.@case}.{clip.executor_label}.motion_track.{camera}", -1, -1f, readback.clip_state_digest);
            }
        }

        static string CaptureTrajectoryView(Soma30MotionClip clip, EmbodimentScene scene, EmbodimentEnvironment env,
                                            string camera, string outDir)
        {
            using (var track = new MotionTrackRenderer(clip, scene.Root.transform, BodyColor(clip), TargetReachColor))
            {
                // Trajectory view: the hand path, its markers and the target, without the ghost bodies.
                foreach (var ghost in track.Ghosts) ghost.SetVisible(false);
                var readback = ReadbackClip(clip);
                return Write(clip, scene, env, "capture_trajectory_view", camera, outDir,
                             $"{clip.@case}.{clip.executor_label}.trajectory.{camera}", -1, -1f, readback.clip_state_digest);
            }
        }

        static string Write(Soma30MotionClip clip, EmbodimentScene scene, EmbodimentEnvironment env,
                            string kind, string camera, string outDir, string stem,
                            int frame, float tau, string digest)
        {
            byte[] png = scene.Render();
            string file = Path.Combine(outDir, stem + ".png");
            File.WriteAllBytes(file, png);
            var meta = new CaptureMetadata
            {
                capture_kind = kind,
                clip_id = clip.clip_id,
                @case = clip.@case,
                action_type = clip.action_type,
                executor = clip.executor,
                executor_label = clip.executor_label,
                frame = frame,
                normalized_time = tau,
                target_reaching_frame = clip.target_reaching_frame,
                target_reaching_rule = clip.target_reaching_rule,
                snapshot_labels = clip.snapshot_labels,
                snapshot_frames = clip.snapshot_frames,
                camera_preset = camera,
                camera_position = new[] { scene.Camera.transform.position.x, scene.Camera.transform.position.y, scene.Camera.transform.position.z },
                camera_rotation_euler_deg = new[] { scene.Camera.transform.eulerAngles.x, scene.Camera.transform.eulerAngles.y, scene.Camera.transform.eulerAngles.z },
                camera_fov_deg = scene.Camera.fieldOfView,
                width = scene.Camera.targetTexture.width,
                height = scene.Camera.targetTexture.height,
                unity_version = Application.unityVersion,
                graphics_device = SystemInfo.graphicsDeviceName,
                graphics_api = SystemInfo.graphicsDeviceType.ToString(),
                color_space = QualitySettings.activeColorSpace.ToString(),
                motion_artifact_sha256 = clip.clip_record_sha256,
                adapter_sha256 = clip.adapter_sha256,
                environment_source_sha256 = env != null ? env.source_sha256 : null,
                image_file = Path.GetFileName(file),
                image_sha256 = PoseDigest.Hash(png),
                pose_state_digest = digest,
                apex_label_used = false,
                debug_overlay = false,
                manual_camera_adjustment = false,
                per_executor_camera_tuning = false,
            };
            File.WriteAllText(Path.Combine(outDir, stem + ".metadata.json"), JsonUtility.ToJson(meta, true));
            return file;
        }
    }
}
