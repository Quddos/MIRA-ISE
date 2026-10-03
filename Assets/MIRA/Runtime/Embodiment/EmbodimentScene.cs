// Phase 5C-18: the deterministic embodiment scene -- environment, target, lighting, camera presets.
//
// The environment is presentation context, not measurement: the table, net and floor come from the
// existing frozen MIRA immersive configuration and are recorded with its digest, never tuned per case or
// per executor.
//
// The target is an independent task-space object. Its position comes from the frozen MotionRequest-
// derived target carried by the clip. It is never read off the avatar hand and never moved to follow a
// trajectory.
//
// Camera presets are computed from the canonical embodiment anchor alone -- never from a trajectory --
// so the same preset yields the same camera for every case and both executors. That is what makes the
// analytic/Kimodo comparison figures comparable rather than individually flattering.
using System;
using UnityEngine;

namespace Mira.Embodiment
{
    [Serializable]
    public class EmbodimentEnvironment
    {
        public string schema;
        public string immersive_context;
        public string source;
        public string source_sha256;
        public float[] table_center_m;      // canonical (kimodo) frame
        public float[] table_size_m;
        public float net_height_m;
        public float target_display_diameter_m;

        public const string Schema = "mira.phase5c18_unity_environment/0.1";

        public static EmbodimentEnvironment Load(string path)
        {
            var e = JsonUtility.FromJson<EmbodimentEnvironment>(System.IO.File.ReadAllText(path));
            if (e.schema != Schema) throw new FormatException("unexpected environment schema " + e.schema);
            return e;
        }
    }

    public class EmbodimentScene : System.IDisposable
    {
        public readonly GameObject Root;
        public readonly Camera Camera;
        public readonly GameObject TargetMarker;
        public readonly GameObject ToleranceSphere;
        public string CameraPreset { get; private set; }

        readonly RenderTexture rt;
        readonly Texture2D tex;
        readonly Light light;

        public static readonly Color FloorColor = new Color(0.30f, 0.31f, 0.33f);
        public static readonly Color TableColor = new Color(0.05f, 0.25f, 0.45f);
        public static readonly Color NetColor = new Color(0.90f, 0.90f, 0.90f);
        public static readonly Color TargetColor = new Color(0.86f, 0.25f, 0.22f);
        public static readonly Color Background = new Color(0.16f, 0.17f, 0.19f);

        public readonly CameraFraming Framing;

        public EmbodimentScene(Soma30MotionClip clip, EmbodimentEnvironment env, CameraFraming framing, int width, int height)
        {
            Framing = framing ?? CameraFraming.Default(clip.Anchor);
            Root = new GameObject("MIRA_EmbodimentRig");

            var floor = Primitive(PrimitiveType.Plane, "Floor", FloorColor);
            floor.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

            if (env != null)
            {
                // Canonical -> Unity for the environment uses the same axis rule as the motion.
                var c = new Vector3(env.table_center_m[0], env.table_center_m[1], env.table_center_m[2]);
                var s = new Vector3(env.table_size_m[0], env.table_size_m[1], env.table_size_m[2]);
                var table = Primitive(PrimitiveType.Cube, "Table", TableColor);
                table.transform.position = new Vector3(-c.x, c.y, c.z);
                table.transform.localScale = s;

                var net = Primitive(PrimitiveType.Cube, "Net", NetColor);
                net.transform.position = new Vector3(0f, c.y + s.y * 0.5f + env.net_height_m * 0.5f, c.z + s.z * 0.5f);
                net.transform.localScale = new Vector3(s.x + 0.3f, env.net_height_m, 0.01f);
            }

            TargetMarker = Primitive(PrimitiveType.Sphere, "Target", TargetColor);
            TargetMarker.transform.position = clip.Target;
            TargetMarker.transform.localScale = Vector3.one * (env != null ? Mathf.Max(env.target_display_diameter_m, 0.04f) : 0.04f);

            // The frozen 0.05 m tolerance, shown as a sphere of that radius. It is a task-specification
            // quantity carried by the frozen MotionRequest and carries no clinical meaning here.
            ToleranceSphere = Primitive(PrimitiveType.Sphere, "TargetTolerance", new Color(0.86f, 0.25f, 0.22f, 0.18f));
            ToleranceSphere.transform.position = clip.Target;
            ToleranceSphere.transform.localScale = Vector3.one * (clip.tolerance_m * 2f);
            var toleranceRenderer = ToleranceSphere.GetComponent<Renderer>();
            toleranceRenderer.sharedMaterial = Transparent(new Color(0.86f, 0.25f, 0.22f, 0.16f));

            var lightGo = new GameObject("EmbodimentLight");
            lightGo.transform.SetParent(Root.transform, false);
            light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.45f);

            var camGo = new GameObject("EmbodimentCamera");
            camGo.transform.SetParent(Root.transform, false);
            Camera = camGo.AddComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Background;
            Camera.nearClipPlane = 0.05f;
            Camera.farClipPlane = 30f;
            Camera.allowHDR = false;
            Camera.allowMSAA = true;
            Camera.aspect = width / (float)height;
            ApplyCamera(CameraPresets.ThreeQuarter);

            rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
            rt.Create();
            Camera.targetTexture = rt;
            tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        }

        public void ApplyCamera(string preset)
        {
            CameraPreset = preset;
            Camera.transform.position = CameraPresets.Position(preset, Framing);
            Camera.transform.LookAt(Framing.Center, Vector3.up);
            Camera.fieldOfView = CameraPresets.FieldOfView(preset, Framing);
        }

        public byte[] Render()
        {
            Camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            return tex.EncodeToPNG();
        }

        GameObject Primitive(PrimitiveType type, string name, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(Root.transform, false);
            go.GetComponent<Renderer>().sharedMaterial = Mira.Immersive.Materials.Lit(color);
            return go;
        }

        static Material Transparent(Color color)
        {
            var m = new Material(Shader.Find("Standard")) { hideFlags = HideFlags.DontSave };
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = 3000;
            m.color = color;
            return m;
        }

        public void Dispose()
        {
            if (Camera != null) Camera.targetTexture = null;
            if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
            if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
        }
    }
}
