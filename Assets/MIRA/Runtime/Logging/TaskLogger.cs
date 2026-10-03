// JSON Lines task log: one record per simulation frame + a summary record. Culture-invariant.
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Mira.Logging
{
    public class TaskLogger : System.IDisposable
    {
        readonly StreamWriter writer;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public TaskLogger(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            writer = new StreamWriter(path, false, new UTF8Encoding(false)) { NewLine = "\n" };
        }

        static string F(float v) => v.ToString("R", Inv);
        static string V(Vector3 v) => $"[{F(v.x)},{F(v.y)},{F(v.z)}]";

        public void Frame(string episodeId, int frame, int fps, string phase, bool targetVisible, Vector3 effectorUnity, float distanceM, bool acquired)
        {
            writer.WriteLine(
                $"{{\"record\":\"frame\",\"episode_id\":\"{episodeId}\",\"frame\":{frame},\"t_s\":{F(frame / (float)fps)}," +
                $"\"phase\":\"{phase}\",\"target_visible\":{(targetVisible ? "true" : "false")}," +
                $"\"effector_world_unity_m\":{V(effectorUnity)},\"distance_to_target_m\":{F(distanceM)},\"target_acquired\":{(acquired ? "true" : "false")}}}");
        }

        public void Summary(string episodeId, string clipSha, int nFrames, int? acquisitionFrame, float minDistanceM, string unityVersion)
        {
            writer.WriteLine(
                $"{{\"record\":\"summary\",\"episode_id\":\"{episodeId}\",\"clip_sha256\":\"{clipSha}\",\"n_frames\":{nFrames}," +
                $"\"target_acquired\":{(acquisitionFrame.HasValue ? "true" : "false")},\"acquisition_frame\":{(acquisitionFrame.HasValue ? acquisitionFrame.Value.ToString(Inv) : "null")}," +
                $"\"endpoint_min_distance_m\":{F(minDistanceM)},\"unity_version\":\"{unityVersion}\",\"coordinate_frame\":\"unity_lh_yup_zfwd\"}}");
        }

        public void Dispose() => writer.Dispose();
    }
}
