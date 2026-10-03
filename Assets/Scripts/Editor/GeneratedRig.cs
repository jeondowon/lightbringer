using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Shared plumbing for the generated prototype rigs (Spearman, Shieldbearer, mounted Knight, Mage, Priest):
    // bones made with identity rotation (local axes equal the unit's axes at rest), nearest-segment skin
    // weights limited by per-unit region rules, baked legacy clips and the common validation probes.
    internal sealed class GeneratedRig
    {
        private readonly List<Transform> bones = new List<Transform>();
        private readonly List<Vector3> ends = new List<Vector3>();
        private readonly List<Vector3> restPositions = new List<Vector3>();
        private readonly Transform root;

        public GeneratedRig(Transform root) { this.root = root; }

        public IReadOnlyList<Transform> Bones => bones;

        public Transform Bone(string name, Transform parent, Vector3 start, Vector3 end)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.position = start;
            bones.Add(bone); ends.Add(end); restPositions.Add(bone.localPosition);
            return bone;
        }

        public Transform Find(string name) => bones.First(b => b.name == name);

        // The source FBX placed into unit space by `toUnit` and merged into one mesh (in a preview scene).
        public static Mesh BakePlaced(GameObject source, Matrix4x4 toUnit, UnityEngine.SceneManagement.Scene scene, string name)
        {
            var probe = UnityEngine.Object.Instantiate(source);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(probe, scene);
            try
            {
                probe.transform.SetPositionAndRotation(toUnit.GetColumn(3), toUnit.rotation);
                probe.transform.localScale = toUnit.lossyScale;
                CombineInstance[] parts = probe.GetComponentsInChildren<MeshFilter>()
                    .SelectMany(f => Enumerable.Range(0, f.sharedMesh.subMeshCount).Select(s => new CombineInstance
                        { mesh = f.sharedMesh, subMeshIndex = s, transform = f.transform.localToWorldMatrix }))
                    .ToArray();
                if (parts.Length == 0) throw new InvalidOperationException(source.name + " has no mesh.");
                var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(parts, true, true);
                return mesh;
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
        }

        // +1 for bones named Right*, -1 for Left*, 0 for the centre line.
        public static int Side(string name) => name.Contains("Right") ? 1 : name.Contains("Left") ? -1 : 0;

        // `rigid` may name one bone that takes the whole vertex (hands holding props). Otherwise the four
        // nearest eligible bone segments blend with a compact falloff that keeps armour mostly rigid.
        // Side bones only reach vertices on their own side of `centreX`.
        public void Skin(Mesh mesh, Func<Vector3, string> rigid, Func<Vector3, string, bool> eligible, float falloff = .006f, float centreX = 0f)
            => Skin(mesh, rigid, (p, name) => eligible(p, name) ? 1f : 0f, falloff, centreX);

        // `influence` scales each bone's distance weight (0 = not eligible), which lets regions fade into each
        // other, e.g. a sleeve that partly follows the arm and partly the chest.
        public void Skin(Mesh mesh, Func<Vector3, string> rigid, Func<Vector3, string, float> influence, float falloff = .006f, float centreX = 0f)
        {
            Vector3[] vertices = mesh.vertices;
            var weights = new BoneWeight[vertices.Length];
            string[] names = bones.Select(b => b.name).ToArray();
            int[] sides = names.Select(Side).ToArray();
            Vector3[] starts = bones.Select(b => b.position).ToArray();
            var distance = new float[names.Length];
            var scale = new float[names.Length];
            int[] best = new int[4];
            float[] top = new float[4];
            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3 p = vertices[v];
                string only = rigid?.Invoke(p);
                if (only != null)
                {
                    weights[v] = new BoneWeight { boneIndex0 = Array.IndexOf(names, only), weight0 = 1f };
                    continue;
                }
                int side = p.x >= centreX ? 1 : -1;
                float nearest = float.PositiveInfinity;
                for (int i = 0; i < names.Length; i++)
                {
                    scale[i] = sides[i] != 0 && sides[i] != side ? 0f : influence(p, names[i]);
                    if (scale[i] <= 0f) continue;
                    Vector3 a = starts[i], delta = ends[i] - a;
                    float t = Mathf.Clamp01(Vector3.Dot(p - a, delta) / Mathf.Max(delta.sqrMagnitude, .00001f));
                    distance[i] = (p - a - delta * t).sqrMagnitude;
                    nearest = Mathf.Min(nearest, distance[i]);
                }
                if (float.IsInfinity(nearest)) throw new InvalidOperationException($"No eligible bone at vertex {p} ({root.name}).");
                for (int j = 0; j < 4; j++) { best[j] = 0; top[j] = 0f; }
                for (int i = 0; i < names.Length; i++)
                {
                    if (scale[i] <= 0f) continue;
                    // Compact support keeps armour mostly rigid while blending across the nearest joint.
                    float w = scale[i] * Mathf.Exp(-(distance[i] - nearest) / falloff);
                    for (int j = 0; j < 4; j++)
                    {
                        if (w <= top[j]) continue;
                        for (int k = 3; k > j; k--) { top[k] = top[k - 1]; best[k] = best[k - 1]; }
                        top[j] = w; best[j] = i; break;
                    }
                }
                float sum = top[0] + top[1] + top[2] + top[3];
                if (sum <= 0f) throw new InvalidOperationException($"Zero skin weight at vertex {p} ({root.name}).");
                weights[v] = new BoneWeight
                {
                    boneIndex0 = best[0], boneIndex1 = best[1], boneIndex2 = best[2], boneIndex3 = best[3],
                    weight0 = top[0] / sum, weight1 = top[1] / sum, weight2 = top[2] / sum, weight3 = top[3] / sum,
                };
            }
            mesh.boneWeights = weights;
            mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * root.localToWorldMatrix).ToArray();
            mesh.RecalculateBounds();
        }

        public SkinnedMeshRenderer AddSkin(string name, Mesh mesh, Material material, Bounds localBounds)
        {
            var body = new GameObject(name);
            body.transform.SetParent(root, false);
            var skin = body.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.sharedMaterial = material;
            skin.bones = bones.ToArray();
            skin.rootBone = bones[0];
            skin.quality = SkinQuality.Bone4;
            skin.localBounds = localBounds;
            skin.updateWhenOffscreen = false;
            return skin;
        }

        public void ResetPose()
        {
            for (int i = 0; i < bones.Count; i++) { bones[i].localRotation = Quaternion.identity; bones[i].localPosition = restPositions[i]; }
        }

        public Vector3 RestPosition(Transform bone) => restPositions[bones.IndexOf(bone)];

        public static void CheckWeights(SkinnedMeshRenderer skin)
        {
            if (skin.sharedMesh.bindposes.Length != skin.bones.Length) throw new InvalidOperationException(skin.name + " skeleton/bind pose mismatch.");
            foreach (BoneWeight w in skin.sharedMesh.boneWeights)
                if (float.IsNaN(w.weight0) || Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1f) > .001f)
                    throw new InvalidOperationException(skin.name + " has unnormalized skin weights.");
        }

        // Largest vertex displacement from the bind pose with `clip` sampled at `phase`.
        public static float Motion(GameObject root, SkinnedMeshRenderer skin, AnimationClip clip, float phase, Vector3[] rest)
        {
            var posed = new Mesh();
            try
            {
                clip.SampleAnimation(root, clip.length * phase);
                skin.BakeMesh(posed);
                Vector3[] moved = posed.vertices;
                float largest = 0f;
                for (int i = 0; i < moved.Length; i++) largest = Mathf.Max(largest, (moved[i] - rest[i]).magnitude);
                return largest;
            }
            finally { UnityEngine.Object.DestroyImmediate(posed); }
        }

        // World-space centre of the given vertices in the current pose.
        public static Vector3 Centroid(SkinnedMeshRenderer skin, int[] indices)
        {
            var posed = new Mesh();
            try
            {
                skin.BakeMesh(posed);
                Vector3[] vertices = posed.vertices;
                Vector3 sum = Vector3.zero;
                foreach (int index in indices) sum += skin.transform.TransformPoint(vertices[index]);
                return sum / indices.Length;
            }
            finally { UnityEngine.Object.DestroyImmediate(posed); }
        }

        public static Vector3[] RestVertices(SkinnedMeshRenderer skin)
        {
            var rest = new Mesh();
            try { skin.BakeMesh(rest); return rest.vertices; }
            finally { UnityEngine.Object.DestroyImmediate(rest); }
        }

        // Indices and centre of the vertices matching `filter` (landmark measurement on the bind mesh).
        public static Vector3 Landmark(Vector3[] vertices, Func<Vector3, bool> filter, out int[] indices, string what)
        {
            var matches = new List<int>();
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < vertices.Length; i++)
                if (filter(vertices[i])) { sum += vertices[i]; matches.Add(i); }
            if (matches.Count == 0) throw new InvalidOperationException("Cannot locate landmark: " + what);
            indices = matches.ToArray();
            return sum / matches.Count;
        }

        public static Vector3 Landmark(Vector3[] vertices, Func<Vector3, bool> filter, string what) => Landmark(vertices, filter, out _, what);

        // Bakes legacy rotation curves for every bone (and position curves where `position` is not null).
        public static AnimationClip Clip(Transform root, IEnumerable<Transform> bones, string assetPath, float duration, WrapMode wrap,
            Func<Transform, float, Quaternion> rotation, Func<Transform, float, Vector3?> position = null)
        {
            var clip = new AnimationClip
            {
                name = System.IO.Path.GetFileNameWithoutExtension(assetPath), legacy = true, frameRate = 30, wrapMode = wrap,
            };
            int samples = Mathf.Max(2, Mathf.RoundToInt(duration * 30));
            foreach (Transform bone in bones)
            {
                string path = AnimationUtility.CalculateTransformPath(bone, root);
                var curves = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                bool moves = position != null && position(bone, 0f).HasValue;
                var px = new AnimationCurve(); var py = new AnimationCurve(); var pz = new AnimationCurve();
                for (int i = 0; i <= samples; i++)
                {
                    float phase = (float)i / samples, time = phase * duration;
                    Quaternion q = rotation(bone, phase);
                    curves[0].AddKey(time, q.x); curves[1].AddKey(time, q.y); curves[2].AddKey(time, q.z); curves[3].AddKey(time, q.w);
                    if (!moves) continue;
                    Vector3 p = position(bone, phase).Value;
                    px.AddKey(time, p.x); py.AddKey(time, p.y); pz.AddKey(time, p.z);
                }
                for (int k = 0; k < 4; k++) clip.SetCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[k], curves[k]);
                if (moves)
                {
                    clip.SetCurve(path, typeof(Transform), "m_LocalPosition.x", px);
                    clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y", py);
                    clip.SetCurve(path, typeof(Transform), "m_LocalPosition.z", pz);
                }
            }
            clip.EnsureQuaternionContinuity();
            return RigBuildUtility.SaveAsset(clip, assetPath);
        }

        // Ease helpers for one-shot actions: 0 -> 1 over [a, b], held, then back to 0 over [c, d].
        public static float Rise(float phase, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, phase));
        public static float Pulse(float phase, float a, float b, float c, float d) => Rise(phase, a, b) * (1f - Rise(phase, c, d));

        // Collider-free mesh made from a ring profile swept along +Y (tapered shafts, cones, blades).
        public static Mesh Lathe(string name, float[] heights, float[] radii, int segments = 10)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int r = 0; r < heights.Length; r++)
                for (int s = 0; s < segments; s++)
                {
                    float angle = s * Mathf.PI * 2f / segments;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radii[r], heights[r], Mathf.Sin(angle) * radii[r]));
                }
            for (int r = 0; r < heights.Length - 1; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = r * segments + s, b = r * segments + (s + 1) % segments, c = a + segments, d = b + segments;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            var mesh = new Mesh { name = name, vertices = vertices.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
