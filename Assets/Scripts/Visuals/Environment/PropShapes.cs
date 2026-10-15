using System.Collections.Generic;
using UnityEngine;

namespace Lightbringer.Visuals
{
    // Vertex-palette primitive builder shared by the environment, props and projectile meshes, plus the
    // stronghold gate anchors that StrongholdVfx lights.
    public static class PropShapes
    {
        // Flame origins of the gate braziers in stronghold feet space.
        public static readonly Vector3[] AlliedBraziers = { new Vector3(-1.75f, 1.45f, 1.95f), new Vector3(1.75f, 1.45f, 1.95f) };
        public static readonly Vector3[] EnemyBraziers = { new Vector3(-1.8f, 1.5f, -1.95f), new Vector3(1.8f, 1.5f, -1.95f) };
        public const float AlliedCrystalHeight = 9.6f;

        internal enum Shape { Cube, Ball }

        // Procedural surface detail drawn by the toon shader from uv2.x (static props and buildings only).
        internal enum Surface { None = 0, Masonry = 1, RoofTiles = 2, Planks = 3, CorruptMasonry = 4, Rock = 5 }

        // Accumulates transformed primitives into one vertex-palette mesh (shared with the environment builder).
        internal sealed class Shapes
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<Vector2> surface = new List<Vector2>();
            private readonly List<Vector2> patterns = new List<Vector2>();
            private bool anyPattern;

            // Surface pattern stamped on every piece added while it is set.
            public Surface Pattern { get; set; }
            public bool IsEmpty => vertices.Count == 0;
            private readonly List<int> triangles = new List<int>();

            public void Add(Shape kind, Color color, Vector3 position, Vector3 scale, Vector3 euler = default,
                float emission = 0f, float spec = 0f)
                => Append(kind == Shape.Cube ? ProceduralMeshes.Box : ProceduralMeshes.Sphere, color, position, euler, scale, emission, spec);

            public void Pair(Shape kind, Color color, Vector3 position, Vector3 scale, Vector3 euler = default,
                float emission = 0f, float spec = 0f)
            {
                Add(kind, color, position, scale, euler, emission, spec);
                Add(kind, color, Mirror(position), scale, MirrorEuler(euler), emission, spec);
            }

            public void Cone(float topRatio, Color color, Vector3 basePosition, Vector3 scale, Vector3 euler = default,
                int segments = 12, float emission = 0f, float spec = 0f)
                => Append(ProceduralMeshes.Frustum(topRatio, segments), color, basePosition, euler, scale, emission, spec);

            public void PairCone(float topRatio, Color color, Vector3 basePosition, Vector3 scale, Vector3 euler = default,
                int segments = 12, float emission = 0f, float spec = 0f)
            {
                Cone(topRatio, color, basePosition, scale, euler, segments, emission, spec);
                Cone(topRatio, color, Mirror(basePosition), scale, MirrorEuler(euler), segments, emission, spec);
            }

            // Free-form piece for organic props. `sway` (0..1) marks how much the wind moves the top of the piece
            // (stored in uv1.y for the toon shader); `softCenter` bends normals toward a sphere around that point
            // so a clump of blobs shades as one soft canopy; `top` tints upward-facing surfaces (moss).
            // `groundY` bakes contact darkening into the lowest ~0.9 m so props sit on the ground (cheap AO).
            public void Piece(UnityEngine.Mesh source, Color color, Vector3 position, Vector3 scale, Vector3 euler = default,
                float sway = 0f, Vector3? softCenter = null, float softBlend = 0.7f, Color? top = null, float spec = 0f, float? groundY = null)
                => Append(source, color, position, euler, scale, 0f, spec, sway, softCenter, softBlend, top, groundY);

            private static Vector3 Mirror(Vector3 value) => new Vector3(-value.x, value.y, value.z);
            private static Vector3 MirrorEuler(Vector3 euler) => new Vector3(euler.x, -euler.y, -euler.z);

            private void Append(UnityEngine.Mesh source, Color color, Vector3 position, Vector3 euler, Vector3 scale,
                float emission, float spec, float sway = 0f, Vector3? softCenter = null, float softBlend = 0.7f, Color? top = null, float? groundY = null)
            {
                Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.Euler(euler), scale);
                Matrix4x4 normalMatrix = matrix.inverse.transpose;
                Vector3[] sourceVertices = source.vertices;
                Vector3[] sourceNormals = source.normals;
                int[] sourceTriangles = source.triangles;
                int offset = vertices.Count;
                Color baked = new Color(color.r, color.g, color.b, Mathf.Clamp01(emission));
                for (int i = 0; i < sourceVertices.Length; i++)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(sourceVertices[i]);
                    Vector3 normal = normalMatrix.MultiplyVector(sourceNormals[i]).normalized;
                    if (softCenter.HasValue)
                        normal = Vector3.Lerp(normal, (point - softCenter.Value).normalized, softBlend).normalized;
                    Color tinted = baked;
                    if (top.HasValue)
                    {
                        Color moss = Color.Lerp(baked, top.Value, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.85f, normal.y)));
                        tinted = new Color(moss.r, moss.g, moss.b, baked.a);
                    }
                    if (groundY.HasValue)
                    {
                        float occlusion = Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, (point.y - groundY.Value) / 0.9f));
                        tinted = new Color(tinted.r * occlusion, tinted.g * occlusion, tinted.b * occlusion, tinted.a);
                    }
                    vertices.Add(point);
                    normals.Add(normal);
                    colors.Add(tinted);
                    surface.Add(new Vector2(spec, sway * Mathf.Clamp01(sourceVertices[i].y + 0.5f)));
                    // Metal trim and glowing pieces stay clean whatever pattern is set.
                    patterns.Add(new Vector2(spec > 0.5f || emission > 0f ? 0f : (float)Pattern, 0f));
                    anyPattern |= Pattern != Surface.None;
                }
                // Negative scale would flip winding; none of the silhouettes use it.
                foreach (int index in sourceTriangles) triangles.Add(offset + index);
            }

            public UnityEngine.Mesh Bake(string name)
            {
                UnityEngine.Mesh mesh = new UnityEngine.Mesh { name = name, hideFlags = HideFlags.DontSave };
                if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetColors(colors);
                mesh.SetUVs(1, surface);
                if (anyPattern) mesh.SetUVs(2, patterns);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(false);
                return mesh;
            }
        }
    }
}
