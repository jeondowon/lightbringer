using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // One gait cycle read from a Meshy-rigged GLB (Higgsfield animation library), for retargeting onto a
    // project-native rig. Everything is in a facing frame (forward +Z, up +Y, metres): the hips, each ankle's
    // forward offset from its bind spot (relative to the hips' mean travel), rise above its lowest point and
    // sole pitch (toe down +, from the bind), which samples have the foot planted, and per bone the world
    // rotation from the bind pose. In-place clips are fine: the ground speed is read from the planted feet.
    internal sealed class MocapGait
    {
        internal sealed class Foot
        {
            public float[] Forward, Rise, Pitch;
            public bool[] Planted;
        }

        public float Period;                 // seconds per cycle
        public int Count;                    // samples per cycle, sample i at phase i / Count
        public float LegLength;              // bind hip-to-ankle height
        public float GroundSpeed;            // m/s the planted feet move back relative to the hips
        public float[] HipsRise, HipsSway;   // height above the bind, forward offset about the mean travel
        public Foot Left, Right;
        public Dictionary<string, Quaternion[]> Delta = new Dictionary<string, Quaternion[]>();
        public Dictionary<string, Vector3[]> Joint = new Dictionary<string, Vector3[]>(); // relative to the hips

        private const float PlantedClearance = .025f;

        private struct Snapshot
        {
            public Dictionary<string, Vector3> Position;
            public Dictionary<string, Quaternion> Rotation;

            public static Snapshot Lerp(Snapshot a, Snapshot b, float t) => new Snapshot
            {
                Position = a.Position.ToDictionary(p => p.Key, p => Vector3.Lerp(p.Value, b.Position[p.Key], t)),
                Rotation = a.Rotation.ToDictionary(p => p.Key, p => Quaternion.Slerp(p.Value, b.Rotation[p.Key], t)),
            };
        }

        public static MocapGait Load(string glbPath, IEnumerable<string> extraBones, int count)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(glbPath) == null)
                AssetDatabase.ImportAsset(glbPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath)
                ?? throw new InvalidOperationException(glbPath + " is missing or glTFast could not import it.");
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(glbPath).OfType<AnimationClip>().OrderByDescending(c => c.length).FirstOrDefault()
                ?? throw new InvalidOperationException("No animation clip inside " + glbPath);

            string[] legs = { "Hips", "LeftUpLeg", "LeftFoot", "LeftToeBase", "RightUpLeg", "RightFoot", "RightToeBase" };
            string[] names = legs.Concat(extraBones).Distinct().ToArray();
            GameObject model = UnityEngine.Object.Instantiate(source);
            model.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Dictionary<string, Transform> bone = names.ToDictionary(n => n, n => model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n)
                    ?? throw new InvalidOperationException($"{glbPath}: bone {n} not found."));

                // Bind pose: the node pose as imported, before any sampling. (The skin's bind matrices already
                // carry the 0.01 armature scale that glTFast also puts on the mesh node, so they read 100x small.)
                var bind = new Snapshot
                {
                    Position = names.ToDictionary(n => n, n => bone[n].position),
                    Rotation = names.ToDictionary(n => n, n => bone[n].rotation),
                };
                // Facing from the bind feet (heel to toe).
                Vector3 forward = Vector3.ProjectOnPlane(bind.Position["LeftToeBase"] - bind.Position["LeftFoot"]
                    + bind.Position["RightToeBase"] - bind.Position["RightFoot"], Vector3.up).normalized;
                Quaternion facing = Quaternion.Inverse(Quaternion.LookRotation(forward, Vector3.up));
                bind = Face(bind, facing);

                // Key range of the clip; the cycle closes on the first key or one frame after the last.
                EditorCurveBinding any = AnimationUtility.GetCurveBindings(clip).First(b => b.propertyName.StartsWith("m_LocalRotation"));
                Keyframe[] keys = AnimationUtility.GetEditorCurve(clip, any).keys;
                float first = keys[0].time, last = keys[keys.Length - 1].time, step = (last - first) / Mathf.Max(1, keys.Length - 1);
                Snapshot Sample(float time)
                {
                    clip.SampleAnimation(model, time);
                    return Face(new Snapshot
                    {
                        Position = names.ToDictionary(n => n, n => bone[n].position),
                        Rotation = names.ToDictionary(n => n, n => bone[n].rotation),
                    }, facing);
                }
                Snapshot start = Sample(first), end = Sample(last);
                bool closed = start.Position.Keys.Max(n => Vector3.Distance(start.Position[n] - start.Position["Hips"], end.Position[n] - end.Position["Hips"])) < .01f;
                float period = closed ? last - first : last - first + step;

                var g = new MocapGait { Period = period, Count = count };
                var shots = new Snapshot[count];
                for (int i = 0; i < count; i++)
                {
                    float time = first + period * i / count;
                    shots[i] = time <= last ? Sample(time) : Snapshot.Lerp(end, start, (time - last) / step);
                }
                g.Read(shots, bind, names);
                return g;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(model);
            }
        }

        private static Snapshot Face(Snapshot s, Quaternion facing) => new Snapshot
        {
            Position = s.Position.ToDictionary(p => p.Key, p => facing * p.Value),
            Rotation = s.Rotation.ToDictionary(p => p.Key, p => facing * p.Value),
        };

        private void Read(Snapshot[] shots, Snapshot bind, string[] names)
        {
            float dt = Period / Count;
            // Mean hips travel (zero for an in-place clip).
            float[] times = Enumerable.Range(0, Count).Select(i => i * dt).ToArray();
            float[] hipsZ = shots.Select(s => s.Position["Hips"].z).ToArray();
            Fit(times, hipsZ, out float a, out float b);
            HipsSway = hipsZ.Select((z, i) => z - (a + b * times[i])).ToArray();
            HipsRise = shots.Select(s => s.Position["Hips"].y - bind.Position["Hips"].y).ToArray();
            LegLength = .5f * (bind.Position["LeftUpLeg"].y - bind.Position["LeftFoot"].y + bind.Position["RightUpLeg"].y - bind.Position["RightFoot"].y);

            Foot ReadFoot(string side)
            {
                string ankle = side + "Foot", toe = side + "ToeBase";
                float bindRel = bind.Position[ankle].z - bind.Position["Hips"].z;
                float Angle(Snapshot s) => Mathf.Atan2(s.Position[ankle].y - s.Position[toe].y, s.Position[toe].z - s.Position[ankle].z) * Mathf.Rad2Deg;
                float ankleLow = shots.Min(s => s.Position[ankle].y), toeLow = shots.Min(s => s.Position[toe].y);
                return new Foot
                {
                    Forward = shots.Select((s, i) => s.Position[ankle].z - (a + b * times[i]) - bindRel).ToArray(),
                    Rise = shots.Select(s => s.Position[ankle].y - ankleLow).ToArray(),
                    Pitch = shots.Select(s => Mathf.DeltaAngle(Angle(bind), Angle(s))).ToArray(),
                    Planted = shots.Select(s => Mathf.Min(s.Position[ankle].y - ankleLow, s.Position[toe].y - toeLow) < PlantedClearance).ToArray(),
                };
            }
            Left = ReadFoot("Left");
            Right = ReadFoot("Right");

            // Ground speed: how fast the planted toes slide back relative to the mean hips travel.
            var slopes = new List<float>();
            foreach (string side in new[] { "Left", "Right" })
            {
                Foot foot = side == "Left" ? Left : Right;
                foreach (int[] run in Runs(foot.Planted))
                {
                    if (run.Length < 3) continue;
                    Fit(run.Select(i => i * dt).ToArray(), run.Select(i => shots[i % Count].Position[side + "ToeBase"].z - (a + b * times[i % Count])).ToArray(), out _, out float slope);
                    slopes.Add(slope);
                }
            }
            if (slopes.Count == 0) throw new InvalidOperationException("No planted foot found in the gait cycle.");
            GroundSpeed = -slopes.Average();

            foreach (string n in names)
            {
                Quaternion inverse = Quaternion.Inverse(bind.Rotation[n]);
                Delta[n] = shots.Select(s => s.Rotation[n] * inverse).ToArray();
                Joint[n] = shots.Select(s => s.Position[n] - s.Position["Hips"]).ToArray();
            }
        }

        // Contiguous planted runs as sample indices, joined across the cycle's wrap (unwrapped past Count).
        public static List<int[]> Runs(bool[] planted)
        {
            int n = planted.Length, begin = Array.FindIndex(planted, p => !p);
            var runs = new List<int[]>();
            if (begin < 0) return runs;
            var current = new List<int>();
            for (int k = 1; k <= n; k++)
            {
                int i = begin + k;
                if (planted[i % n]) current.Add(i);
                else if (current.Count > 0) { runs.Add(current.ToArray()); current.Clear(); }
            }
            if (current.Count > 0) runs.Add(current.ToArray());
            return runs;
        }

        private static void Fit(float[] x, float[] y, out float intercept, out float slope)
        {
            float mx = x.Average(), my = y.Average(), sxx = 0f, sxy = 0f;
            for (int i = 0; i < x.Length; i++) { sxx += (x[i] - mx) * (x[i] - mx); sxy += (x[i] - mx) * (y[i] - my); }
            slope = sxx > 1e-8f ? sxy / sxx : 0f;
            intercept = my - slope * mx;
        }
    }
}
