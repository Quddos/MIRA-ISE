// Phase 5C-18 EditMode tests: the adapter contract as Unity sees it.
//
// These build clips in memory rather than reading the phase artifacts, so they run in any checkout and
// test the embodiment rules themselves: positions are applied verbatim, frames are never interpolated,
// absent joints are never fabricated, the pose digest is deterministic and position-sensitive, and the
// camera presets do not depend on the executor.
using System.Collections.Generic;
using Mira.Embodiment;
using NUnit.Framework;
using UnityEngine;

namespace Mira.Tests
{
    public class EmbodimentTests
    {
        static Soma30MotionClip Clip(string label = "analytic", int nFrames = 4)
        {
            var clip = new Soma30MotionClip
            {
                schema = Soma30MotionClip.Schema,
                clip_id = "MT_" + label,
                @case = "MT",
                action_type = "MAINTAIN_TASK",
                executor = label == "analytic" ? "ANALYTIC_EXECUTOR_V1" : "KIMODO_FROZEN_OUTPUT",
                executor_label = label,
                n_frames = nFrames,
                fps = 30f,
                duration_s = nFrames / 30f,
                n_soma30_joints = 4,
                joint_names = new[] { "Hips", "LeftArm", "LeftForeArm", "LeftHand" },
                parent_indices = new[] { -1, 0, 1, 2 },
                n_represented = 3,
                represented_joint_indices = new[] { 1, 2, 3 },
                represented_joint_names = new[] { "LeftArm", "LeftForeArm", "LeftHand" },
                unity_positions_m_flat = new float[nFrames * 3 * 3],
                target_unity_m = new[] { 0.5f, 1.0f, 0.4f },
                anchor_unity_m = new[] { 0f, 1.4f, 0f },
                tolerance_m = 0.05f,
                target_reaching_frame = nFrames - 1,
                target_reaching_rule = Soma30MotionClip.ExpectedRule,
                snapshot_labels = new[] { "START", "EARLY_REACH", "MID_REACH", "TARGET_REACH", "RETURN" },
                snapshot_frames = new[] { 0, 1, nFrames / 2, nFrames - 1, nFrames - 1 },
                adapter_version = "PHASE5C18_SOMA30_TO_UNITY_POSITION_ADAPTER_V1",
                adapter_sha256 = "deadbeef",
                clip_record_sha256 = "feedface",
                unity_positions_sha256 = "cafebabe",
            };
            for (int f = 0; f < nFrames; f++)
                for (int j = 0; j < 3; j++)
                {
                    int i = (f * 3 + j) * 3;
                    clip.unity_positions_m_flat[i] = 0.1f * j;
                    clip.unity_positions_m_flat[i + 1] = 1.4f - 0.05f * j;
                    clip.unity_positions_m_flat[i + 2] = 0.1f * f + 0.2f * j;
                }
            clip.Validate();
            return clip;
        }

        [Test]
        public void AppliedPositionsAreTheClipPositionsVerbatim()
        {
            var clip = Clip();
            using (var body = new Soma30PositionEmbodiment(clip, null, false, Color.white, Color.white))
            {
                for (int f = 0; f < clip.n_frames; f++)
                {
                    body.ApplyFrame(f);
                    for (int j = 0; j < clip.n_represented; j++)
                        Assert.AreEqual(clip.Position(f, j), body.WorldPosition(j),
                                        "frame " + f + " joint " + j + " was altered on the way into Unity");
                }
            }
        }

        [Test]
        public void JointsTheRealizationDoesNotCarryAreNotCreated()
        {
            var clip = Clip();
            using (var body = new Soma30PositionEmbodiment(clip, null, false, Color.white, Color.white))
            {
                Assert.AreEqual(3, body.Joints.Length);
                CollectionAssert.AreEqual(new[] { "Hips" }, clip.NotRepresented());
                Assert.Throws<System.ArgumentException>(() => body.WorldPosition("Hips"));
            }
        }

        [Test]
        public void SteppingVisitsEveryFrameExactlyOnceAndNeverInterpolates()
        {
            var clip = Clip(nFrames: 6);
            using (var player = new PosePlayer(clip, null, false, Color.white, Color.white))
            {
                var visited = new List<int> { player.Frame };
                while (player.StepForward()) visited.Add(player.Frame);
                CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, visited);
                Assert.IsFalse(player.StepForward(), "a non-looping clip must stop at the last frame");
                for (int f = 0; f < clip.n_frames; f++)
                {
                    player.Seek(f);
                    Assert.AreEqual(clip.Position(f, 2), player.Body.WorldPosition(2));
                }
            }
        }

        [Test]
        public void PartialWallClockAdvanceHoldsThePoseInsteadOfBlending()
        {
            var clip = Clip(nFrames: 6);
            using (var player = new PosePlayer(clip, null, false, Color.white, Color.white))
            {
                Vector3 before = player.Body.WorldPosition(2);
                player.Advance(1f / 90f);                       // a third of one frame at 30 fps
                Assert.AreEqual(0, player.Frame);
                Assert.AreEqual(before, player.Body.WorldPosition(2));
                player.Advance(2f / 90f);                       // completes the frame
                Assert.AreEqual(1, player.Frame);
            }
        }

        [Test]
        public void SeekingOutsideTheClipIsRefused()
        {
            var clip = Clip();
            using (var body = new Soma30PositionEmbodiment(clip, null, false, Color.white, Color.white))
            {
                Assert.Throws<System.ArgumentOutOfRangeException>(() => body.ApplyFrame(clip.n_frames));
                Assert.Throws<System.ArgumentOutOfRangeException>(() => body.ApplyFrame(-1));
            }
        }

        [Test]
        public void TargetReachSnapshotUsesTheV2FrameAndIsNeverCalledApex()
        {
            var clip = Clip();
            Assert.AreEqual(clip.target_reaching_frame, clip.SnapshotFrame("TARGET_REACH"));
            Assert.AreEqual(Soma30MotionClip.ExpectedRule, clip.target_reaching_rule);
            CollectionAssert.DoesNotContain(clip.snapshot_labels, "APEX");
            clip.snapshot_labels[3] = "APEX";
            Assert.Throws<System.FormatException>(() => clip.Validate());
        }

        [Test]
        public void MotionTrackShowsOnePosePerSnapshotAndAltersNoPosition()
        {
            var clip = Clip(nFrames: 6);
            using (var track = new MotionTrackRenderer(clip, null, Color.cyan, Color.yellow))
            {
                Assert.AreEqual(clip.snapshot_labels.Length, track.Ghosts.Count);
                for (int i = 0; i < track.Ghosts.Count; i++)
                {
                    int frame = clip.snapshot_frames[i];
                    Assert.AreEqual(frame, track.Ghosts[i].Frame);
                    Assert.AreEqual(clip.Position(frame, 2), track.Ghosts[i].WorldPosition(2));
                }
            }
        }

        [Test]
        public void PoseDigestIsDeterministicAndPositionSensitive()
        {
            var clip = Clip();
            using (var body = new Soma30PositionEmbodiment(clip, null, false, Color.white, Color.white))
            {
                body.ApplyFrame(2);
                var a = new List<float>(); body.AppendAppliedPositions(a);
                body.ApplyFrame(0);
                body.ApplyFrame(2);
                var b = new List<float>(); body.AppendAppliedPositions(b);
                string first = PoseDigest.Compute(clip.@case, clip.executor, 2, clip.represented_joint_names, a.ToArray(), clip.adapter_sha256);
                string second = PoseDigest.Compute(clip.@case, clip.executor, 2, clip.represented_joint_names, b.ToArray(), clip.adapter_sha256);
                Assert.AreEqual(first, second, "the same pose must digest identically");

                var moved = b.ToArray();
                moved[0] += 1e-4f;
                Assert.AreNotEqual(first, PoseDigest.Compute(clip.@case, clip.executor, 2, clip.represented_joint_names, moved, clip.adapter_sha256));
                Assert.AreNotEqual(first, PoseDigest.Compute(clip.@case, clip.executor, 3, clip.represented_joint_names, b.ToArray(), clip.adapter_sha256));
            }
        }

        [Test]
        public void CameraPresetsDependOnTheAnchorOnlyAndNotOnTheExecutor()
        {
            var analytic = Clip("analytic");
            var kimodo = Clip("kimodo");
            var framing = CameraFraming.Default(analytic.Anchor);
            Assert.IsFalse(framing.depends_on_a_single_trajectory);
            Assert.IsFalse(framing.tuned_per_executor);
            foreach (string preset in CameraPresets.All)
            {
                // One framing record serves both clips: there is no executor input to the camera at all.
                Assert.AreEqual(CameraPresets.Position(preset, framing),
                                CameraPresets.Position(preset, CameraFraming.Default(kimodo.Anchor)),
                                preset + " differs between executors");
                Assert.AreEqual(1f, CameraPresets.Direction(preset).magnitude, 1e-5f);
            }
            Assert.Throws<System.ArgumentException>(() => CameraPresets.Direction("CAMERA_NOWHERE"));
        }

        [Test]
        public void AClipThatMisdescribesItselfIsRefused()
        {
            var clip = Clip();
            clip.n_frames = 99;
            Assert.Throws<System.FormatException>(() => clip.Validate());

            var other = Clip();
            other.target_reaching_rule = "TARGET_REACHING_APEX_TAU_0_5";
            Assert.Throws<System.FormatException>(() => other.Validate());

            var third = Clip();
            third.target_reaching_frame = third.n_frames;
            Assert.Throws<System.FormatException>(() => third.Validate());
        }

        [Test]
        public void DebugOverlayCarriesNoHiddenState()
        {
            var clip = Clip();
            using (var player = new PosePlayer(clip, null, false, Color.white, Color.white))
            {
                string text = DebugOverlay.Text(player);
                foreach (string forbidden in new[] { "impair", "latent", "ground truth", "evaluator", "answer" })
                    StringAssert.DoesNotContain(forbidden, text.ToLowerInvariant());
                StringAssert.Contains("target-reaching frame", text);
            }
        }
    }
}
