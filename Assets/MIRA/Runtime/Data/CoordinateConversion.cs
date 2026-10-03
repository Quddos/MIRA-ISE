// The ONLY place where canonical (Kimodo) coordinates become Unity coordinates.
//   Kimodo: right-handed, +Y up, +Z forward, +X = character left (meters)
//   Unity : left-handed,  +Y up, +Z forward, +X = character right (meters)
//   position   (x, y, z)    -> (-x, y, z)
//   quaternion (w, x, y, z) -> (w, x, -y, -z)
// Must stay identical to src/mira/core/coordinates.py (golden tests on both sides).
using UnityEngine;

namespace Mira.Data
{
    public static class CoordinateConversion
    {
        public const string KimodoFrame = "kimodo_rh_yup_zfwd";
        public const string UnityFrame = "unity_lh_yup_zfwd";
        public const float ReadbackToleranceM = 0.001f;

        public static Vector3 KimodoToUnityPosition(Vector3 p) => new Vector3(-p.x, p.y, p.z);

        public static Vector3 UnityToKimodoPosition(Vector3 p) => new Vector3(-p.x, p.y, p.z);

        /// wxyz packed as Vector4(w, x, y, z). Unity's Quaternion constructor order is (x, y, z, w).
        public static Quaternion KimodoToUnityQuaternion(Vector4 wxyz) => new Quaternion(wxyz.y, -wxyz.z, -wxyz.w, wxyz.x);

        public static Vector4 UnityToKimodoQuaternionWxyz(Quaternion q) => new Vector4(q.w, q.x, -q.y, -q.z);
    }
}
