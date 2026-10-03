using System.IO;
using Mira.Capture;
using Mira.Data;
using Mira.DigitalPatient;
using Mira.Playback;
using NUnit.Framework;
using UnityEngine;

namespace Mira.Tests
{
    public class CaptureTests
    {
        static string EpisodePath(string name) => Path.Combine(Application.streamingAssetsPath, "MIRA", "episodes", name);

        [Test]
        public void BaselineCaptureConfigLoadsAndIsFixed()
        {
            var cfg = CaptureConfig.LoadDefault();
            Assert.NotNull(cfg, "StreamingAssets/MIRA/capture/baseline_over_shoulder_v1.json must be exported");
            Assert.AreEqual("baseline_over_shoulder_v1", cfg.camera.camera_id);
            Assert.AreEqual("perspective", cfg.camera.projection);
            Assert.IsFalse(cfg.environment.target_color_changes_on_acquisition);
            Assert.IsFalse(cfg.benchmark_mode.debug_overlays);
        }

        [Test]
        public void MannequinDoesNotChangeJointKinematics()
        {
            var ep = EpisodeRunner.Load(EpisodePath("GOLDEN_LATERALITY_V0.episode.json"));
            var expected = JsonUtility.FromJson<ExpectedUnityPositions>(File.ReadAllText(EpisodePath("GOLDEN_LATERALITY_V0.expected_unity_positions.json")));
            var mat = Mira.Immersive.Materials.Lit(Color.gray);
            var visuals = new RunnerVisuals { AvatarStyle = AvatarStyle.Mannequin, Body = mat, Joint = mat, Environment = true };
            using (var runner = new EpisodeRunner(ep, visuals, null))
            {
                var rb = runner.Readback(expected.frames);
                float max = 0f;
                for (int i = 0; i < rb.world_positions_m_flat.Length; i++)
                    max = Mathf.Max(max, Mathf.Abs(rb.world_positions_m_flat[i] - expected.world_positions_m_flat[i]));
                Assert.Less(max, CoordinateConversion.ReadbackToleranceM);
                foreach (var t in runner.Avatar.Joints) Assert.AreEqual(Vector3.one, t.localScale, "joint transforms must keep unit scale");
            }
        }

        [Test]
        public void OverlayUsesSuppliedObservedPositionsNotAvatarPose()
        {
            var ep = EpisodeRunner.Load(EpisodePath("GOLDEN_LATERALITY_V0.episode.json"));
            var cfg = CaptureConfig.LoadDefault();
            var root = new GameObject("test_root");
            try
            {
                var overlay = new SkeletonOverlay(root.transform, ep.skeleton.parent_indices, cfg.skeleton_overlay);
                var flat = new float[90];
                for (int j = 0; j < 30; j++) { flat[3 * j] = 0.01f * j; flat[3 * j + 1] = 2.0f; flat[3 * j + 2] = -0.5f; }
                overlay.Show(flat, 0, true);
                var markers = overlay.Root.GetComponentsInChildren<Transform>();
                int checkedMarkers = 0;
                foreach (var m in markers)
                {
                    if (m.name != "obs_joint") continue;
                    Assert.AreEqual(2.0f, m.position.y, 1e-5f);  // observed input, not the avatar (~1 m hips)
                    checkedMarkers++;
                }
                Assert.AreEqual(30, checkedMarkers);
                overlay.Show(flat, 0, false);
                Assert.IsFalse(overlay.Root.activeSelf, "dropped sensor frame draws no skeleton");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void AuditFlagsTextAndUnconfiguredColours()
        {
            var cfg = CaptureConfig.LoadDefault();
            var ep = EpisodeRunner.Load(EpisodePath("MIRA_000005.episode.json"));
            using (var scene = new CaptureScene(cfg))
            using (var runner = scene.BuildRunner(ep))
            {
                Assert.IsEmpty(scene.Audit(ep.episode_id, scene.AllowedColors()), "clean benchmark scene must pass the audit");
                var text = new GameObject("debug_label");
                text.AddComponent<TextMesh>().text = "rom_limitation";
                var odd = GameObject.CreatePrimitive(PrimitiveType.Cube);
                odd.GetComponent<Renderer>().sharedMaterial = Mira.Immersive.Materials.Lit(Color.magenta);
                try
                {
                    var findings = scene.Audit(ep.episode_id, scene.AllowedColors());
                    Assert.IsTrue(findings.Exists(f => f.Contains("TextMesh")));
                    Assert.IsTrue(findings.Exists(f => f.Contains("unconfigured colour")));
                }
                finally
                {
                    Object.DestroyImmediate(text);
                    Object.DestroyImmediate(odd);
                }
            }
        }
    }
}
