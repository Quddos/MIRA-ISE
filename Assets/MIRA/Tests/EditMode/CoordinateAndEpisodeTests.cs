using System.IO;
using Mira.Data;
using Mira.MotorTask;
using Mira.Playback;
using NUnit.Framework;
using UnityEngine;

namespace Mira.Tests
{
    public class CoordinateConversionTests
    {
        [Test]
        public void GoldenValues()
        {
            Assert.AreEqual(new Vector3(-0.1f, 1.2f, 0.3f), CoordinateConversion.KimodoToUnityPosition(new Vector3(0.1f, 1.2f, 0.3f)));
            var q = CoordinateConversion.KimodoToUnityQuaternion(new Vector4(0.9f, 0.1f, 0.2f, 0.3f)); // (w,x,y,z)
            Assert.AreEqual(0.9f, q.w); Assert.AreEqual(0.1f, q.x); Assert.AreEqual(-0.2f, q.y); Assert.AreEqual(-0.3f, q.z);
        }

        [Test]
        public void Involution()
        {
            var p = new Vector3(0.3f, -1.1f, 2.5f);
            Assert.AreEqual(p, CoordinateConversion.UnityToKimodoPosition(CoordinateConversion.KimodoToUnityPosition(p)));
            var wxyz = new Vector4(0.5f, 0.5f, -0.5f, 0.5f);
            Assert.AreEqual(wxyz, CoordinateConversion.UnityToKimodoQuaternionWxyz(CoordinateConversion.KimodoToUnityQuaternion(wxyz)));
        }

        static Vector3 HamiltonRotate(Vector4 wxyz, Vector3 v)
        {
            // Right-handed canonical-frame rotation, same formula as mira.core.quaternion.rotate
            var u = new Vector3(wxyz.y, wxyz.z, wxyz.w);
            float w = wxyz.x;
            var t = 2f * Vector3.Cross(u, v);
            return v + w * t + Vector3.Cross(u, t);
        }

        [Test]
        public void ConversionCommutesWithRotation()
        {
            var rng = new System.Random(3);
            for (int i = 0; i < 200; i++)
            {
                var q = new Vector4((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f).normalized;
                var v = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f);
                Vector3 lhs = CoordinateConversion.KimodoToUnityPosition(HamiltonRotate(q, v));
                Vector3 rhs = CoordinateConversion.KimodoToUnityQuaternion(q) * CoordinateConversion.KimodoToUnityPosition(v);
                Assert.Less((lhs - rhs).magnitude, 1e-5f);
            }
        }
    }

    public class EpisodeTests
    {
        static string EpisodePath(string name) => Path.Combine(Application.streamingAssetsPath, "MIRA", "episodes", name);

        [Test]
        public void GoldenLateralityReadbackWithinOneMillimetre()
        {
            var ep = EpisodeRunner.Load(EpisodePath("GOLDEN_LATERALITY_V0.episode.json"));
            var expected = JsonUtility.FromJson<ExpectedUnityPositions>(File.ReadAllText(EpisodePath("GOLDEN_LATERALITY_V0.expected_unity_positions.json")));
            using (var runner = new EpisodeRunner(ep, withVisuals: false, logPath: null))
            {
                var rb = runner.Readback(expected.frames);
                Assert.AreEqual(expected.world_positions_m_flat.Length, rb.world_positions_m_flat.Length);
                float max = 0f;
                for (int i = 0; i < rb.world_positions_m_flat.Length; i++)
                    max = Mathf.Max(max, Mathf.Abs(rb.world_positions_m_flat[i] - expected.world_positions_m_flat[i]));
                Assert.Less(max, CoordinateConversion.ReadbackToleranceM, "Python FK vs Unity readback");

                // Laterality: at frame 1 the LEFT hand is raised forward and is on the character's left (Unity -x).
                runner.Avatar.ApplyFrame(1);
                var left = runner.Avatar.WorldPosition(runner.Avatar.JointIndex("LeftHand"));
                var right = runner.Avatar.WorldPosition(runner.Avatar.JointIndex("RightHand"));
                Assert.Less(left.x, 0f);
                Assert.Greater(right.x, 0f);
                Assert.Greater(left.z, 0.3f);
            }
        }

        [Test]
        public void BenchmarkEpisodeLoadsWithValidDimensions()
        {
            foreach (var f in Directory.GetFiles(Path.Combine(Application.streamingAssetsPath, "MIRA", "episodes"), "MIRA_*.episode.json"))
            {
                var ep = EpisodeRunner.Load(f);
                Assert.AreEqual(180, ep.n_frames);
                Assert.AreEqual(UnityEpisode.Soma30JointCount, ep.skeleton.joint_names.Length);
                for (int fr = 0; fr < ep.n_frames; fr += 17)
                    for (int j = 0; j < 30; j++)
                        Assert.AreEqual(1f, ep.LocalQuatKimodoWxyz(fr, j).magnitude, 1e-5f);
            }
        }

        [Test]
        public void DeterministicPlaybackAndTaskMonitor()
        {
            var ep = EpisodeRunner.Load(EpisodePath("MIRA_000005.episode.json"));
            float first;
            int? acq;
            using (var r = new EpisodeRunner(ep, withVisuals: false, logPath: null))
            {
                r.RunToEnd();
                first = r.Monitor.MinDistanceM;
                acq = r.Monitor.AcquisitionFrame;
                Assert.AreEqual(EpisodePhase.EpisodeComplete, r.Monitor.PhaseAt(r.Frame, ep.n_frames));
            }
            using (var r = new EpisodeRunner(ep, withVisuals: false, logPath: null))
            {
                r.RunToEnd();
                Assert.AreEqual(first, r.Monitor.MinDistanceM);
                Assert.AreEqual(acq, r.Monitor.AcquisitionFrame);
            }
        }

        [Test]
        public void TaskMonitorDoesNotAcquireBeforeTargetPresentation()
        {
            var ep = EpisodeRunner.Load(EpisodePath("MIRA_000005.episode.json"));
            var m = new ReachingTaskMonitor(ep);
            m.Update(0, m.TargetUnity);  // effector on target before presentation
            Assert.IsFalse(m.AcquisitionFrame.HasValue);
            m.Update(m.TargetPresentedFrame, m.TargetUnity);
            Assert.AreEqual(m.TargetPresentedFrame, m.AcquisitionFrame);
        }
    }
}
