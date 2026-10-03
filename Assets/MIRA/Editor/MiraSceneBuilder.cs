// Builds Assets/MIRA/Scenes/MIRA_TableTennisInterception.unity (menu or -executeMethod).
// Camera and lighting come from the baseline capture config, so the Game view shows what the
// benchmark capture renders. The EpisodePlayer shows a non-saved edit-mode preview of the episode.
using Mira.Capture;
using Mira.Playback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mira.EditorTools
{
    public static class MiraSceneBuilder
    {
        public const string ScenePath = "Assets/MIRA/Scenes/MIRA_TableTennisInterception.unity";

        [MenuItem("MIRA/Build Table-Tennis Interception Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cfg = CaptureConfig.LoadDefault();

            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            var camera = cam.AddComponent<Camera>();
            var light = new GameObject("Directional Light");
            var l = light.AddComponent<Light>();
            if (cfg != null)
            {
                CaptureScene.ApplyCamera(camera, cfg.camera);
                CaptureScene.ApplyLighting(l, cfg.lighting);
            }
            else
            {
                camera.fieldOfView = 55f;
                cam.transform.position = new Vector3(-1.35f, 2.3f, -0.7f);
                cam.transform.rotation = Quaternion.Euler(32.3f, 50.7f, 0f);
                l.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            var player = new GameObject("MIRA_EpisodePlayer");
            var ep = player.AddComponent<EpisodePlayer>();
            ep.episodeFile = "MIRA/episodes/MIRA_000005.episode.json";
            ep.previewFrame = 45;

            System.IO.Directory.CreateDirectory("Assets/MIRA/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MIRA] scene written: " + ScenePath);
        }

        [MenuItem("MIRA/Open Scene And Frame Patient")]
        public static void OpenAndFrame()
        {
            EditorSceneManager.OpenScene(ScenePath);
            FramePatient();
        }

        [MenuItem("MIRA/Frame Patient In Scene View")]
        public static void FramePatient()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            view.LookAt(new Vector3(0.05f, 1.15f, 0.45f), Quaternion.Euler(32.3f, 50.7f, 0f), 2.4f);
            view.Repaint();
        }
    }
}
