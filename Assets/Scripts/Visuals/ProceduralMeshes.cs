using System.Collections.Generic;
using UnityEngine;

namespace Lightbringer.Visuals
{
    // Low-poly readable building blocks. Generated in code so they are always readable for
    // mesh combining in builds, and cheap enough for hundreds of troops.
    public static class ProceduralMeshes
    {
        private static Mesh box;
        private static Mesh sphere;
        private static Mesh groundQuad;

        // Unit quad on the XZ plane facing up, UV 0..1; used for ground decals such as the aura.
        public static Mesh GroundQuad
        {
            get
            {
                if (groundQuad != null) return groundQuad;
                groundQuad = new Mesh { name = "LB Ground Quad", hideFlags = HideFlags.DontSave };
                groundQuad.SetVertices(new List<Vector3> { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) });
                groundQuad.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
                groundQuad.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
                groundQuad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
                groundQuad.RecalculateBounds();
                return groundQuad;
            }
        }
        private static readonly Dictionary<int, Mesh> Frustums = new Dictionary<int, Mesh>();

        // Unit cube centred on the origin.
        public static Mesh Box
        {
            get
            {
                if (box != null) return box;
                List<Vector3> vertices = new List<Vector3>();
                List<Vector3> normals = new List<Vector3>();
                List<int> triangles = new List<int>();
                Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                foreach (Vector3 normal in axes)
                {
                    Vector3 tangent = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.up;
                    Vector3 bitangent = Vector3.Cross(normal, tangent);
                    int start = vertices.Count;
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i == 0 || i == 3 ? -0.5f : 0.5f;
                        float b = i < 2 ? -0.5f : 0.5f;
                        vertices.Add(normal * 0.5f + tangent * a + bitangent * b);
                        normals.Add(normal);
                    }
                    AddQuad(triangles, vertices, start, normal);
                }
                box = Create("LB Box", vertices, normals, triangles);
                return box;
            }
        }

        // Unit-diameter UV sphere centred on the origin; scale it into ellipsoids.
        public static Mesh Sphere
        {
            get
            {
                if (sphere != null) return sphere;
                const int longitude = 14, latitude = 9;
                List<Vector3> vertices = new List<Vector3>();
                List<Vector3> normals = new List<Vector3>();
                List<int> triangles = new List<int>();
                for (int y = 0; y <= latitude; y++)
                {
                    float v = Mathf.PI * y / latitude;
                    for (int x = 0; x <= longitude; x++)
                    {
                        float u = 2f * Mathf.PI * x / longitude;
                        Vector3 normal = new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u));
                        vertices.Add(normal * 0.5f);
                        normals.Add(normal);
                    }
                }
                for (int y = 0; y < latitude; y++)
                    for (int x = 0; x < longitude; x++)
                    {
                        int a = y * (longitude + 1) + x, b = a + longitude + 1;
                        triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                        triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
                    }
                sphere = Create("LB Sphere", vertices, normals, triangles);
                return sphere;
            }
        }

        // Capped frustum with its pivot at the base centre: diameter 1 at y = 0,
        // diameter `topRatio` at y = 1. topRatio 0 makes a cone, 1 a cylinder.
        public static Mesh Frustum(float topRatio, int segments = 12)
        {
            int key = Mathf.RoundToInt(Mathf.Clamp(topRatio, 0f, 4f) * 100f) * 64 + Mathf.Clamp(segments, 3, 48);
            if (Frustums.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            segments = Mathf.Clamp(segments, 3, 48);
            float top = Mathf.Clamp(topRatio, 0f, 4f) * 0.5f;
            const float bottom = 0.5f;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> triangles = new List<int>();
            float slope = bottom - top;
            for (int i = 0; i <= segments; i++)
            {
                float angle = 2f * Mathf.PI * i / segments;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 normal = (direction + Vector3.up * slope).normalized;
                vertices.Add(direction * bottom); normals.Add(normal);
                vertices.Add(direction * top + Vector3.up); normals.Add(normal);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 2);
                triangles.Add(a + 1); triangles.Add(a + 3); triangles.Add(a + 2);
            }
            AddCap(vertices, normals, triangles, segments, 0f, bottom, Vector3.down);
            if (top > 0.001f) AddCap(vertices, normals, triangles, segments, 1f, top, Vector3.up);
            Mesh mesh = Create("LB Frustum " + key, vertices, normals, triangles);
            Frustums[key] = mesh;
            return mesh;
        }

        private static readonly Dictionary<int, Mesh> Lumps = new Dictionary<int, Mesh>();
        private static readonly Dictionary<int, Mesh> Stones = new Dictionary<int, Mesh>();

        // Smooth, organic blob (unit diameter, centred) for foliage clumps and bushes. A few cached variants.
        public static Mesh Lumpy(int variant)
        {
            variant = Mathf.Abs(variant) % 6;
            if (Lumps.TryGetValue(variant, out Mesh cached) && cached != null) return cached;
            Mesh mesh = Icosphere("LB Lumpy " + variant, 2, 0.22f, 1.6f, variant * 13.7f, false);
            Lumps[variant] = mesh;
            return mesh;
        }

        // Faceted, chipped stone (unit diameter, centred, flat-shaded) for rocks and rubble.
        public static Mesh Faceted(int variant)
        {
            variant = Mathf.Abs(variant) % 6;
            if (Stones.TryGetValue(variant, out Mesh cached) && cached != null) return cached;
            Mesh mesh = Icosphere("LB Stone " + variant, 1, 0.3f, 1.1f, variant * 7.3f + 3f, true);
            Stones[variant] = mesh;
            return mesh;
        }

        private static Mesh Icosphere(string name, int subdivisions, float amplitude, float frequency, float seed, bool faceted)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            List<Vector3> points = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < points.Count; i++) points[i] = points[i].normalized;
            List<int> faces = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            for (int s = 0; s < subdivisions; s++)
            {
                Dictionary<long, int> midpoints = new Dictionary<long, int>();
                List<int> next = new List<int>();
                for (int f = 0; f < faces.Count; f += 3)
                {
                    int a = faces[f], b = faces[f + 1], c = faces[f + 2];
                    int ab = Midpoint(points, midpoints, a, b), bc = Midpoint(points, midpoints, b, c), ca = Midpoint(points, midpoints, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces = next;
            }
            // Low-frequency radial displacement makes every variant a distinct, irregular shape.
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i] * frequency + Vector3.one * seed;
                float noise = (Mathf.PerlinNoise(p.x, p.y) + Mathf.PerlinNoise(p.y + 5.1f, p.z) + Mathf.PerlinNoise(p.z + 9.7f, p.x)) / 3f;
                points[i] = points[i] * 0.5f * (1f + (noise - 0.5f) * 2f * amplitude);
            }
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            if (faceted)
            {
                for (int f = 0; f < faces.Count; f++) { vertices.Add(points[faces[f]]); triangles.Add(f); }
            }
            else
            {
                vertices.AddRange(points);
                triangles.AddRange(faces);
            }
            // Ensure every triangle faces outward (Unity front faces: Cross(b - a, c - a) along the normal).
            for (int f = 0; f < triangles.Count; f += 3)
            {
                Vector3 a = vertices[triangles[f]], b = vertices[triangles[f + 1]], c = vertices[triangles[f + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f)
                {
                    int swap = triangles[f + 1]; triangles[f + 1] = triangles[f + 2]; triangles[f + 2] = swap;
                }
            }
            Mesh mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int Midpoint(List<Vector3> points, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int index)) return index;
            points.Add(((points[a] + points[b]) * 0.5f).normalized);
            cache[key] = points.Count - 1;
            return points.Count - 1;
        }

        private static void AddCap(List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            int segments, float height, float radius, Vector3 normal)
        {
            int centre = vertices.Count;
            vertices.Add(Vector3.up * height); normals.Add(normal);
            for (int i = 0; i <= segments; i++)
            {
                float angle = 2f * Mathf.PI * i / segments;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius));
                normals.Add(normal);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = centre + 1 + i;
                if (normal.y > 0f) { triangles.Add(centre); triangles.Add(a + 1); triangles.Add(a); }
                else { triangles.Add(centre); triangles.Add(a); triangles.Add(a + 1); }
            }
        }

        private static void AddQuad(List<int> triangles, List<Vector3> vertices, int start, Vector3 normal)
        {
            // Unity front faces satisfy Cross(b - a, c - a) pointing along the outward normal.
            Vector3 face = Vector3.Cross(vertices[start + 1] - vertices[start], vertices[start + 2] - vertices[start]);
            if (Vector3.Dot(face, normal) > 0f)
            {
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
            else
            {
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
            }
        }

        private static Mesh Create(string name, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            Mesh mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
