// Immersive presentation layer: table_tennis_target_interception. Builds a half table, net, floor and
// a ball-shaped target at the task target position. It never moves the target or changes task logic.
// Colours come from the capture configuration; in benchmark capture mode the target never changes
// colour (acquisition feedback could otherwise act as a visual label).
using Mira.Data;
using UnityEngine;

namespace Mira.Immersive
{
    public class TableTennisInterceptionPresenter
    {
        public const string ContextId = "table_tennis_target_interception";
        public readonly GameObject Root;
        public readonly GameObject TargetBall;
        readonly bool colorOnAcquisition;
        readonly Material targetMaterial;
        readonly Color targetColor;

        public static readonly Color DefaultTable = new Color(0.05f, 0.25f, 0.45f);
        public static readonly Color DefaultNet = new Color(0.9f, 0.9f, 0.9f);
        public static readonly Color DefaultFloor = new Color(0.35f, 0.35f, 0.35f);
        public static readonly Color DefaultTarget = new Color(1f, 0.55f, 0.1f);

        public TableTennisInterceptionPresenter(UnityEpisode ep, Transform parent)
            : this(ep, parent, DefaultTable, DefaultNet, DefaultFloor, DefaultTarget, true) { }

        public TableTennisInterceptionPresenter(UnityEpisode ep, Transform parent, Color table, Color netColor, Color floorColor,
                                                Color target, bool changeColorOnAcquisition)
        {
            if (ep.scene.immersive_context != ContextId) throw new System.ArgumentException("episode is not a table-tennis interception episode");
            colorOnAcquisition = changeColorOnAcquisition;
            targetColor = target;
            Root = new GameObject("Immersive_TableTennisTargetInterception");
            Root.transform.SetParent(parent, false);

            var c = ep.scene.table_center_m;
            var s = ep.scene.table_size_m;
            var tableGo = Primitive(PrimitiveType.Cube, "Table", table);
            tableGo.transform.localPosition = CoordinateConversion.KimodoToUnityPosition(new Vector3(c[0], c[1], c[2]));
            tableGo.transform.localScale = new Vector3(s[0], s[1], s[2]);

            var net = Primitive(PrimitiveType.Cube, "Net", netColor);
            float topY = c[1] + s[1] * 0.5f;
            float farZ = c[2] + s[2] * 0.5f;
            net.transform.localPosition = new Vector3(0f, topY + ep.scene.net_height_m * 0.5f, farZ);
            net.transform.localScale = new Vector3(s[0] + 0.3f, ep.scene.net_height_m, 0.01f);

            var floor = Primitive(PrimitiveType.Plane, "Floor", floorColor);
            floor.transform.localScale = new Vector3(0.6f, 1f, 0.6f);

            TargetBall = Primitive(PrimitiveType.Sphere, "Target", target);
            targetMaterial = TargetBall.GetComponent<Renderer>().sharedMaterial;
            TargetBall.transform.localPosition = CoordinateConversion.KimodoToUnityPosition(ep.TargetKimodo());
            TargetBall.transform.localScale = Vector3.one * ep.scene.target_display_diameter_m;
            TargetBall.SetActive(false);
        }

        GameObject Primitive(PrimitiveType type, string name, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(Root.transform, false);
            go.GetComponent<Renderer>().sharedMaterial = Materials.Lit(color);
            return go;
        }

        public void SetTargetVisible(bool visible, bool acquired)
        {
            TargetBall.SetActive(visible);
            if (targetMaterial != null)
                targetMaterial.color = colorOnAcquisition && acquired ? new Color(0.2f, 0.9f, 0.3f) : targetColor;
        }
    }

    public static class Materials
    {
        public static Material Lit(Color color)
        {
            var shader = Shader.Find("Standard");
            var m = new Material(shader) { color = color, hideFlags = HideFlags.DontSave };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return m;
        }

        /// Unlit, always-on-top colour (used for the observed-skeleton overlay).
        public static Material OnTop(Color color)
        {
            var m = new Material(Shader.Find("Hidden/Internal-Colored")) { color = color, hideFlags = HideFlags.DontSave };
            m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            m.SetInt("_ZWrite", 0);
            m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            m.renderQueue = 4000;
            return m;
        }
    }
}
