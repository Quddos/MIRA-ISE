// Batch-mode entry points (no Play mode, no graphics required):
//   Unity.exe -batchmode -nographics -quit -projectPath <p> -executeMethod Mira.EditorTools.MiraBatch.Readback
//             -miraEpisode <episode.json> -miraOut <readback.json> [-miraFrames all|0,1,2]
//   Unity.exe ... -executeMethod Mira.EditorTools.MiraBatch.PlayEpisode -miraEpisode <episode.json> -miraOut <task_log.jsonl>
using System;
using System.IO;
using System.Linq;
using Mira.Playback;
using UnityEditor;
using UnityEngine;

namespace Mira.EditorTools
{
    public static class MiraBatch
    {
        static string Arg(string name, string fallback = null)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        static void Finish(int code)
        {
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        public static void Readback()
        {
            try
            {
                var ep = EpisodeRunner.Load(Arg("-miraEpisode"));
                string framesArg = Arg("-miraFrames", "all");
                int[] frames = framesArg == "all"
                    ? Enumerable.Range(0, ep.n_frames).ToArray()
                    : framesArg.Split(',').Select(int.Parse).ToArray();
                using (var runner = new EpisodeRunner(ep, withVisuals: false, logPath: null))
                {
                    File.WriteAllText(Arg("-miraOut"), JsonUtility.ToJson(runner.Readback(frames)));
                }
                Debug.Log($"[MIRA] readback written for {ep.episode_id} ({frames.Length} frames)");
                Finish(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[MIRA] readback failed: " + e);
                Finish(1);
            }
        }

        public static void PlayEpisode()
        {
            try
            {
                var ep = EpisodeRunner.Load(Arg("-miraEpisode"));
                using (var runner = new EpisodeRunner(ep, withVisuals: true, logPath: Arg("-miraOut")))
                {
                    runner.RunToEnd();
                    Debug.Log($"[MIRA] episode {ep.episode_id}: acquired={runner.Monitor.AcquisitionFrame.HasValue} min_dist={runner.Monitor.MinDistanceM:F4} m");
                }
                Finish(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[MIRA] playback failed: " + e);
                Finish(1);
            }
        }

        /// Visual sanity preview (NOT an observation pipeline; RGB observations are Phase 5B).
        /// Requires a graphics device: run without -nographics.
        ///   -miraEpisode <episode.json> -miraOut <dir> [-miraFrames 0,60,120]
        public static void RenderPreview()
        {
            try
            {
                var ep = EpisodeRunner.Load(Arg("-miraEpisode"));
                string outDir = Arg("-miraOut");
                Directory.CreateDirectory(outDir);
                int[] frames = Arg("-miraFrames", "0,45,120").Split(',').Select(int.Parse).ToArray();
                var camGo = new GameObject("PreviewCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 50f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
                camGo.transform.position = new Vector3(-1.3f, 1.95f, -1.7f);
                camGo.transform.LookAt(new Vector3(0.05f, 1.05f, 0.55f));
                var lightGo = new GameObject("PreviewLight");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                var rt = new RenderTexture(960, 720, 24);
                cam.targetTexture = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                using (var runner = new EpisodeRunner(ep, withVisuals: true, logPath: null))
                {
                    while (!runner.Done)
                    {
                        runner.Step();
                        if (!frames.Contains(runner.Frame)) continue;
                        cam.Render();
                        RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                        tex.Apply();
                        File.WriteAllBytes(Path.Combine(outDir, $"{ep.episode_id}_f{runner.Frame:D3}.png"), tex.EncodeToPNG());
                    }
                }
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(lightGo);
                Debug.Log("[MIRA] preview frames written to " + outDir);
                Finish(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[MIRA] preview failed: " + e);
                Finish(1);
            }
        }

        public static void BuildScene()
        {
            try
            {
                MiraSceneBuilder.BuildScene();
                Finish(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[MIRA] scene build failed: " + e);
                Finish(1);
            }
        }
    }
}
