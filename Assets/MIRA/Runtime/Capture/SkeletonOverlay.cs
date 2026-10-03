// V1 observed-skeleton overlay. Draws joint markers and bones from OBSERVED (noisy) joint positions
// supplied by Python from the observation record, never from the avatar's clean pose. One uniform
// colour and fixed marker sizes for every joint and every case; dropped sensor frames draw nothing.
using Mira.Data;
using Mira.Immersive;
using UnityEngine;

namespace Mira.Capture
{
    public class SkeletonOverlay
    {
        public readonly GameObject Root;
        readonly Transform[] markers;
        readonly Transform[] bones;
        readonly int[] parents;

        public SkeletonOverlay(Transform parent, int[] parentIndices, SkeletonOverlayConfig cfg)
        {
            parents = parentIndices;
            Root = new GameObject("ObservedSkeletonOverlay");
            Root.transform.SetParent(parent, false);
            var mat = cfg.draw_on_top ? Materials.OnTop(CaptureConfig.Rgb(cfg.rgb)) : Materials.Lit(CaptureConfig.Rgb(cfg.rgb));
            int n = parentIndices.Length;
            markers = new Transform[n];
            bones = new Transform[n];
            for (int j = 0; j < n; j++)
            {
                markers[j] = Make(PrimitiveType.Sphere, "obs_joint", mat, Vector3.one * cfg.joint_marker_diameter_m);
                if (parentIndices[j] >= 0)
                    bones[j] = Make(PrimitiveType.Cylinder, "obs_bone", mat, new Vector3(cfg.bone_width_m, 1f, cfg.bone_width_m));
            }
            Root.SetActive(false);
        }

        Transform Make(PrimitiveType type, string name, Material mat, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.transform.SetParent(Root.transform, false);
            go.transform.localScale = scale;
            return go.transform;
        }

        /// positionsKimodoFlat: 90 floats (30 joints x 3) in the canonical frame, starting at `offset`.
        public void Show(float[] positionsKimodoFlat, int offset, bool valid)
        {
            Root.SetActive(valid);
            if (!valid) return;
            int n = markers.Length;
            var p = new Vector3[n];
            for (int j = 0; j < n; j++)
            {
                int o = offset + 3 * j;
                p[j] = CoordinateConversion.KimodoToUnityPosition(new Vector3(positionsKimodoFlat[o], positionsKimodoFlat[o + 1], positionsKimodoFlat[o + 2]));
                markers[j].position = p[j];
            }
            for (int j = 0; j < n; j++)
            {
                if (bones[j] == null) continue;
                Vector3 a = p[parents[j]], b = p[j];
                Vector3 d = b - a;
                float len = d.magnitude;
                bones[j].gameObject.SetActive(len > 1e-4f);
                if (len <= 1e-4f) continue;
                bones[j].position = (a + b) * 0.5f;
                bones[j].rotation = Quaternion.FromToRotation(Vector3.up, d / len);
                var s = bones[j].localScale;
                bones[j].localScale = new Vector3(s.x, len * 0.5f, s.z);  // Unity cylinder height = 2 * scale.y
            }
        }

        public void Hide() => Root.SetActive(false);
    }
}
