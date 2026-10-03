// Phase 5C-6B batch entry points. Reproducible, headless, no wall-clock dependence.
//
//   Unity.exe -batchmode -nographics -quit -projectPath <p>
//             -executeMethod Mira.EditorTools.MiraPoseExecution.Execute
//             -miraPose <artifact.json> -miraOut <run_record.json>
//
//   Unity.exe -batchmode -quit -projectPath <p>          (graphics required for capture)
//             -executeMethod Mira.EditorTools.MiraPoseExecution.Capture
//             -miraPose <artifact.json> -miraOut <dir> [-miraFrames 0,10,20,30]
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mira.DigitalPatient;
using Mira.Immersive;
using Mira.PoseExecution;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mira.EditorTools
{
    /// What the existing scene contained when it was opened. Recorded, never modified.
    [Serializable]
    public class SceneLoadRecord
    {
        public string scene_path;
        public string scene_name;
        public bool is_valid;
        public bool is_loaded;
        public bool is_dirty;
        public int n_root_objects;
        public string[] root_object_names;
        public string unity_version;
    }

    public static class MiraPoseExecution
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

        /// Execute the frozen analytic pose realization and write the full runtime state record.
        public static void Execute()
        {
            try
            {
                var artifact = PoseExecutionArtifact.FromJson(File.ReadAllText(Arg("-miraPose")));
                Debug.Log($"[MIRA] artifact validated: {artifact.derived_from.motion_request_id} " +
                          $"sha={artifact.derived_from.target_motion_sha256.Substring(0, 12)} " +
                          $"frames={artifact.motion.n_frames} " +
                          $"rotations={artifact.pose_realization.rotation_provenance} " +
                          $"skel={artifact.skeleton.skeleton_sha256.Substring(0, 12)}");

                using (var driver = new PoseExecutionDriver(artifact, AvatarStyle.None))
                {
                    if (driver.Avatar.Joints.Length != 30)
                        throw new InvalidOperationException(
                            $"[MIRA] avatar has {driver.Avatar.Joints.Length} joints, expected 30");
                    var record = driver.RunToEnd();
                    File.WriteAllText(Arg("-miraOut"), JsonUtility.ToJson(record));
                    Debug.Log($"[MIRA] executed {record.frames.Count} frames, completed={record.completed}, " +
                              $"realized_duration={record.realized_duration_s:F4}s");
                }
                Finish(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[MIRA] pose execution failed: " + e);
                Finish(1);
            }
        }

        /// Handoff section 21: verify the EXISTING scene still opens and loads.
        ///
        /// The motion execution above builds its avatar programmatically rather than from the scene,
        /// so scene loading would otherwise go unverified. This opens the scene read-only and reports
        /// what it contains. It executes no motion and writes nothing back to the scene.
        public static void VerifyScene()
        {
            try
            {
                string scenePath = Arg("-miraScene",
                    "Assets/MIRA/Scenes/MIRA_TableTennisInterception.unity");
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var names = roots.Select(r => r.name).ToArray();
                var record = new SceneLoadRecord
                {
                    scene_path = scenePath,
                    scene_name = scene.name,
                    is_valid = scene.IsValid(),
                    is_loaded = scene.isLoaded,
                    is_dirty = scene.isDirty,
                    n_root_objects = roots.Length,
                    root_object_names = names,
                    unity_version = Application.unityVersion,
                };
                File.WriteAllText(Arg("-miraOut"), JsonUtility.ToJson(record));
                Debug.Log($"[MIRA] scene loaded: {scene.name} valid={scene.IsValid()} " +
                          $"loaded={scene.isLoaded} dirty={scene.isDirty} roots={roots.Length} " +
                          $"[{string.Join(", ", names)}]");
                Finish(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[MIRA] scene verification failed: " + e);
                Finish(1);
            }
        }

        /// Render the demonstration frames. Requires a graphics device: run without -nographics.
        public static void Capture()
        {
            try
            {
                var artifact = PoseExecutionArtifact.FromJson(File.ReadAllText(Arg("-miraPose")));
                string outDir = Arg("-miraOut");
                Directory.CreateDirectory(outDir);
                string framesArg = Arg("-miraFrames", "all");
                var wanted = framesArg == "all"
                    ? Enumerable.Range(0, artifact.motion.n_frames).ToArray()
                    : framesArg.Split(',').Select(int.Parse).ToArray();

                var body = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                        ?? Shader.Find("Standard"));
                var root = new GameObject("MIRA_Capture");
                using (var driver = new PoseExecutionDriver(artifact, AvatarStyle.StickFigure,
                                                            body, body, root.transform))
                {
                    var presenter = new TableTennisInterceptionPresenter(
                        driver.Episode, root.transform,
                        TableTennisInterceptionPresenter.DefaultTable,
                        TableTennisInterceptionPresenter.DefaultNet,
                        TableTennisInterceptionPresenter.DefaultFloor,
                        TableTennisInterceptionPresenter.DefaultTarget, false);

                    // Presentation framing only. The camera observes the motion; it never affects it.
                    // Placed on the reaching-arm side (canonical +x is Unity -x) so the table recedes
                    // from the viewer instead of occluding the avatar, and close enough that the 0.04 m
                    // target sphere is resolvable. No motion, pose, duration or target value is altered.
                    var camera = new GameObject("MIRA_CaptureCamera").AddComponent<Camera>();
                    camera.transform.position = new Vector3(-2.0f, 1.60f, 0.90f);
                    camera.transform.LookAt(new Vector3(0f, 1.00f, 0.25f));
                    camera.fieldOfView = 45f;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.10f, 0.11f, 0.13f);
                    var light = new GameObject("MIRA_CaptureLight").AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.intensity = 1.1f;
                    light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

                    var texture = new RenderTexture(1280, 720, 24);
                    camera.targetTexture = texture;
                    var written = new List<string>();
                    while (driver.Step())
                    {
                        // Target visibility follows the artifact's presentation event, nothing else.
                        presenter.SetTargetVisible(
                            driver.Frame >= artifact.task_events.target_presented_frame, false);
                        if (!wanted.Contains(driver.Frame)) continue;
                        camera.Render();
                        RenderTexture.active = texture;
                        var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                        image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                        image.Apply();
                        RenderTexture.active = null;
                        string path = Path.Combine(outDir, $"frame_{driver.Frame:D4}.png");
                        File.WriteAllBytes(path, image.EncodeToPNG());
                        written.Add(Path.GetFileName(path));
                        UnityEngine.Object.DestroyImmediate(image);
                    }
                    camera.targetTexture = null;
                    UnityEngine.Object.DestroyImmediate(texture);
                    UnityEngine.Object.DestroyImmediate(camera.gameObject);
                    UnityEngine.Object.DestroyImmediate(light.gameObject);
                    Debug.Log($"[MIRA] captured {written.Count} frames to {outDir}");
                }
                UnityEngine.Object.DestroyImmediate(root);
                Finish(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[MIRA] capture failed: " + e);
                Finish(1);
            }
        }
    }
}
