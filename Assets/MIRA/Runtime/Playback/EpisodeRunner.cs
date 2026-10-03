// Deterministic lockstep episode execution shared by Play mode (EpisodePlayer), edit-mode preview,
// batch validation (MiraBatch) and batch RGB capture (MiraCapture). Simulation time is frame / 30;
// wall-clock time is never used.
using System.Collections.Generic;
using System.IO;
using Mira.Data;
using Mira.DigitalPatient;
using Mira.Immersive;
using Mira.Logging;
using Mira.MotorTask;
using UnityEngine;

namespace Mira.Playback
{
    public class RunnerVisuals
    {
        public AvatarStyle AvatarStyle = AvatarStyle.None;
        public Material Body;
        public Material Joint;
        public bool Environment;
        public Color Table = TableTennisInterceptionPresenter.DefaultTable;
        public Color Net = TableTennisInterceptionPresenter.DefaultNet;
        public Color Floor = TableTennisInterceptionPresenter.DefaultFloor;
        public Color Target = TableTennisInterceptionPresenter.DefaultTarget;
        public bool TargetColorOnAcquisition = true;

        public static RunnerVisuals Phase5AStickFigure(bool withVisuals) =>
            new RunnerVisuals { AvatarStyle = withVisuals ? AvatarStyle.StickFigure : AvatarStyle.None, Environment = withVisuals };
    }

    public class EpisodeRunner : System.IDisposable
    {
        public readonly UnityEpisode Episode;
        public readonly Soma30Avatar Avatar;
        public readonly TableTennisInterceptionPresenter Presenter;
        public readonly ReachingTaskMonitor Monitor;
        public readonly GameObject Root;
        readonly TaskLogger logger;
        readonly int effector;
        public int Frame { get; private set; } = -1;

        public EpisodeRunner(UnityEpisode ep, bool withVisuals, string logPath)
            : this(ep, RunnerVisuals.Phase5AStickFigure(withVisuals), logPath) { }

        public EpisodeRunner(UnityEpisode ep, RunnerVisuals visuals, string logPath)
        {
            Episode = ep;
            Root = new GameObject("MIRA_Episode");
            Avatar = new Soma30Avatar(ep, Root.transform, visuals.AvatarStyle, visuals.Body, visuals.Joint);
            Presenter = visuals.Environment
                ? new TableTennisInterceptionPresenter(ep, Root.transform, visuals.Table, visuals.Net, visuals.Floor, visuals.Target, visuals.TargetColorOnAcquisition)
                : null;
            Monitor = new ReachingTaskMonitor(ep);
            effector = Avatar.JointIndex(Monitor.EffectorJoint);
            if (!string.IsNullOrEmpty(logPath)) logger = new TaskLogger(logPath);
        }

        public static UnityEpisode Load(string path) => UnityEpisode.FromJson(File.ReadAllText(path));

        public bool Done => Frame >= Episode.n_frames - 1;

        public void Step()
        {
            if (Done) return;
            Frame++;
            Avatar.ApplyFrame(Frame);
            Vector3 eff = Avatar.WorldPosition(effector);
            float d = Monitor.Update(Frame, eff);
            bool visible = Frame >= Monitor.TargetPresentedFrame;
            Presenter?.SetTargetVisible(visible, Monitor.AcquisitionFrame.HasValue);
            logger?.Frame(Episode.episode_id, Frame, Episode.fps, Monitor.PhaseAt(Frame, Episode.n_frames).ToString(), visible, eff, d, Monitor.AcquisitionFrame.HasValue);
            if (Done) logger?.Summary(Episode.episode_id, Episode.motion.clip_sha256, Episode.n_frames, Monitor.AcquisitionFrame, Monitor.MinDistanceM, Application.unityVersion);
        }

        public void RunToEnd()
        {
            while (!Done) Step();
        }

        /// Pose the episode at an arbitrary frame without stepping the task monitor (edit-mode preview).
        public void ShowFrame(int frame)
        {
            Avatar.ApplyFrame(frame);
            Presenter?.SetTargetVisible(frame >= Monitor.TargetPresentedFrame, false);
        }

        public UnityReadback Readback(int[] frames)
        {
            var flat = new List<float>(frames.Length * 90);
            foreach (int f in frames)
            {
                Avatar.ApplyFrame(f);
                Avatar.AppendWorldPositions(flat);
            }
            return new UnityReadback
            {
                episode_id = Episode.episode_id,
                unity_version = Application.unityVersion,
                frames = frames,
                joint_names = Avatar.JointNames,
                world_positions_m_flat = flat.ToArray(),
            };
        }

        public void Dispose()
        {
            logger?.Dispose();
            if (Root != null)
            {
                if (Application.isPlaying) Object.Destroy(Root); else Object.DestroyImmediate(Root);
            }
        }
    }
}
