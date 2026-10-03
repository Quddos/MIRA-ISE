// Scene entry point. In Edit mode it shows a non-saved preview of the episode (mannequin, table,
// target) posed at `previewFrame`, so the scene is visible without pressing Play. In Play mode it runs
// the episode lockstep (Time.captureFramerate = 30: one rendered frame = one simulation frame).
using System.IO;
using Mira.Capture;
using Mira.DigitalPatient;
using Mira.Immersive;
using UnityEngine;

namespace Mira.Playback
{
    [ExecuteAlways]
    public class EpisodePlayer : MonoBehaviour
    {
        [Tooltip("Episode file relative to StreamingAssets")]
        public string episodeFile = "MIRA/episodes/MIRA_000005.episode.json";
        [Tooltip("Frame shown in Edit mode (0-179)")]
        [Range(0, 179)] public int previewFrame = 45;
        public bool loop = true;
        public bool writeTaskLog = true;

        EpisodeRunner runner;
        int shownFrame = -1;
        string shownFile;

        void OnEnable()
        {
            if (!Application.isPlaying) BuildPreview();
        }

        void OnDisable() => Teardown();

        void OnValidate()
        {
            if (Application.isPlaying) return;
            // Rebuilding inside OnValidate is not allowed; defer to the next editor update.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled && !Application.isPlaying) BuildPreview(); };
#endif
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            Time.captureFramerate = 30;
            Load(true);
        }

        void Teardown()
        {
            runner?.Dispose();
            runner = null;
            shownFrame = -1;
            shownFile = null;
        }

        RunnerVisuals Visuals(bool benchmarkColours)
        {
            var cfg = CaptureConfig.LoadDefault();
            if (cfg == null)
                return new RunnerVisuals { AvatarStyle = AvatarStyle.Mannequin, Body = Materials.Lit(new Color(0.8f, 0.8f, 0.78f)), Joint = Materials.Lit(new Color(0.62f, 0.62f, 0.62f)), Environment = true };
            return new RunnerVisuals
            {
                AvatarStyle = cfg.avatar.style == "mannequin" ? AvatarStyle.Mannequin : AvatarStyle.StickFigure,
                Body = Materials.Lit(CaptureConfig.Rgb(cfg.avatar.body_rgb)),
                Joint = Materials.Lit(CaptureConfig.Rgb(cfg.avatar.joint_rgb)),
                Environment = true,
                Table = CaptureConfig.Rgb(cfg.environment.table_rgb),
                Net = CaptureConfig.Rgb(cfg.environment.net_rgb),
                Floor = CaptureConfig.Rgb(cfg.environment.floor_rgb),
                Target = CaptureConfig.Rgb(cfg.environment.target_rgb),
                TargetColorOnAcquisition = !benchmarkColours,
            };
        }

        void BuildPreview()
        {
            string path = Path.Combine(Application.streamingAssetsPath, episodeFile);
            if (!File.Exists(path)) return;
            if (runner != null && shownFile == episodeFile)
            {
                if (shownFrame != previewFrame) { runner.ShowFrame(previewFrame); shownFrame = previewFrame; }
                return;
            }
            Teardown();
            runner = new EpisodeRunner(EpisodeRunner.Load(path), Visuals(false), null);
            HideRecursively(runner.Root);
            runner.Root.transform.SetParent(transform, false);
            runner.ShowFrame(previewFrame);
            shownFrame = previewFrame;
            shownFile = episodeFile;
        }

        static void HideRecursively(GameObject go)
        {
            // Preview objects are visible and selectable but never saved into the scene file.
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
        }

        void Load(bool play)
        {
            Teardown();
            string path = Path.Combine(Application.streamingAssetsPath, episodeFile);
            var ep = EpisodeRunner.Load(path);
            string log = writeTaskLog ? Path.Combine(Application.dataPath, "..", "MIRA_Logs", ep.episode_id + ".task_log.jsonl") : null;
            runner = new EpisodeRunner(ep, Visuals(false), log);
            runner.Root.transform.SetParent(transform, false);
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                if (runner == null || shownFrame != previewFrame || shownFile != episodeFile) BuildPreview();
                return;
            }
            if (runner == null) return;
            if (runner.Done)
            {
                if (loop) Load(true);
                return;
            }
            runner.Step();
        }
    }
}
