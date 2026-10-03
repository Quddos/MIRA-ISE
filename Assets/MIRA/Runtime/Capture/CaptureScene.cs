// Deterministic capture scene: fixed camera, lighting and render settings from the capture config,
// lockstep RGB rendering to PNG, and a benchmark-mode audit of everything that can be rendered.
using System.Collections.Generic;
using System.IO;
using Mira.DigitalPatient;
using Mira.Immersive;
using Mira.Playback;
using UnityEngine;

namespace Mira.Capture
{
    public class CaptureScene : System.IDisposable
    {
        public readonly CaptureConfig Config;
        public readonly Camera Camera;
        public readonly GameObject Root;
        readonly GameObject lightGo;
        readonly RenderTexture rt;
        readonly Texture2D tex;

        public CaptureScene(CaptureConfig cfg)
        {
            Config = cfg;
            Root = new GameObject("MIRA_CaptureRig");
            var camGo = new GameObject("CaptureCamera_" + cfg.camera.camera_id);
            camGo.transform.SetParent(Root.transform, false);
            Camera = camGo.AddComponent<Camera>();
            ApplyCamera(Camera, cfg.camera);
            lightGo = new GameObject("CaptureDirectionalLight");
            lightGo.transform.SetParent(Root.transform, false);
            ApplyLighting(lightGo.AddComponent<Light>(), cfg.lighting);
            rt = new RenderTexture(cfg.camera.width, cfg.camera.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                antiAliasing = Mathf.Max(1, cfg.render.msaa_samples),
            };
            rt.Create();
            Camera.targetTexture = rt;
            tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        }

        public static void ApplyCamera(Camera cam, CameraConfig c)
        {
            cam.orthographic = false;
            cam.fieldOfView = c.fov_deg;
            cam.nearClipPlane = c.near_clip_m;
            cam.farClipPlane = c.far_clip_m;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = CaptureConfig.Rgb(c.background_rgb);
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.transform.position = CaptureConfig.V3(c.position);
            cam.transform.rotation = Quaternion.Euler(CaptureConfig.V3(c.rotation_euler_deg));
            cam.aspect = c.width / (float)c.height;
        }

        public static void ApplyLighting(Light light, LightingConfig l)
        {
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(CaptureConfig.V3(l.directional_rotation_euler_deg));
            light.intensity = l.directional_intensity;
            light.color = CaptureConfig.Rgb(l.directional_rgb);
            light.shadows = l.shadows == "hard" ? LightShadows.Hard : (l.shadows == "soft" ? LightShadows.Soft : LightShadows.None);
            light.shadowStrength = l.shadow_strength;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = CaptureConfig.Rgb(l.ambient_rgb);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            QualitySettings.shadowDistance = l.shadow_distance_m;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowCascades = 1;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.antiAliasing = 0;
        }

        public EpisodeRunner BuildRunner(Mira.Data.UnityEpisode ep)
        {
            var visuals = new RunnerVisuals
            {
                AvatarStyle = Config.avatar.style == "mannequin" ? AvatarStyle.Mannequin : AvatarStyle.StickFigure,
                Body = Materials.Lit(CaptureConfig.Rgb(Config.avatar.body_rgb)),
                Joint = Materials.Lit(CaptureConfig.Rgb(Config.avatar.joint_rgb)),
                Environment = true,
                Table = CaptureConfig.Rgb(Config.environment.table_rgb),
                Net = CaptureConfig.Rgb(Config.environment.net_rgb),
                Floor = CaptureConfig.Rgb(Config.environment.floor_rgb),
                Target = CaptureConfig.Rgb(Config.environment.target_rgb),
                TargetColorOnAcquisition = Config.environment.target_color_changes_on_acquisition,
            };
            var runner = new EpisodeRunner(ep, visuals, null);
            runner.Root.transform.SetParent(Root.transform, false);
            return runner;
        }

        public void RenderPng(string path)
        {
            Camera.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }

        /// Benchmark-mode audit: no text/UI anywhere, one active capture camera, every renderer uses an
        /// allowed configured colour, no object is named after a case/episode. Returns findings (empty = pass).
        public List<string> Audit(string episodeId, IEnumerable<Color> allowedColors)
        {
            var findings = new List<string>();
            var allowed = new List<Color>(allowedColors);
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
            {
                if (go.name.Contains(episodeId)) findings.Add($"object named after episode: {go.name}");
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    string t = comp.GetType().Name;
                    if (t.Contains("Text") || t.Contains("Canvas") || t.Contains("TMP") || t.Contains("GUI") || t.Contains("Sprite") || t.Contains("LineRenderer"))
                        findings.Add($"forbidden component {t} on {go.name}");
                }
                var r = go.GetComponent<Renderer>();
                if (r != null && go.activeInHierarchy && r.sharedMaterial != null)
                {
                    var c = r.sharedMaterial.color;
                    bool ok = false;
                    foreach (var a in allowed) if (Mathf.Abs(a.r - c.r) < 1e-4f && Mathf.Abs(a.g - c.g) < 1e-4f && Mathf.Abs(a.b - c.b) < 1e-4f) { ok = true; break; }
                    if (!ok) findings.Add($"renderer {go.name} uses unconfigured colour {c}");
                }
            }
            int cams = 0;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.InstanceID)) if (cam.targetTexture == rt) cams++;
            if (cams != 1) findings.Add($"expected exactly 1 camera rendering into the capture texture, found {cams}");
            if (Config.benchmark_mode.debug_overlays) findings.Add("debug overlays enabled in benchmark mode");
            return findings;
        }

        public IEnumerable<Color> AllowedColors()
        {
            var c = Config;
            yield return CaptureConfig.Rgb(c.avatar.body_rgb);
            yield return CaptureConfig.Rgb(c.avatar.joint_rgb);
            yield return CaptureConfig.Rgb(c.environment.floor_rgb);
            yield return CaptureConfig.Rgb(c.environment.table_rgb);
            yield return CaptureConfig.Rgb(c.environment.net_rgb);
            yield return CaptureConfig.Rgb(c.environment.target_rgb);
            yield return CaptureConfig.Rgb(c.skeleton_overlay.rgb);
        }

        public void Dispose()
        {
            Camera.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(Root);
        }
    }
}
