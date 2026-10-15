using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Real-scale textured primitives for the photographic structures (Act 2 depots, the final citadel). UVs run in
    // metres along every surface, so one material tiling reads the same on each block; one submesh per material layer.
    internal sealed class TexturedShapes
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int>[] layers;

        public TexturedShapes(int layerCount)
        {
            layers = new List<int>[layerCount];
            for (int i = 0; i < layerCount; i++) layers[i] = new List<int>();
        }

        // Submesh (material slot) that the following primitives go into.
        public int Layer { get; set; }
        public bool IsEmpty => vertices.Count == 0;

        public void Box(Vector3 centre, Vector3 size, Vector3 euler = default)
        {
            Quaternion rotation = Quaternion.Euler(euler);
            Vector3 h = size * .5f;
            // Texture offset per block so neighbouring stones never repeat the same patch.
            Vector2 offset = new Vector2(centre.x * .61f + centre.z * .37f, centre.y * .29f);
            Face(centre, rotation, Vector3.right, Vector3.forward, Vector3.up, h.x, size.z, size.y, offset);
            Face(centre, rotation, Vector3.left, Vector3.back, Vector3.up, h.x, size.z, size.y, offset);
            Face(centre, rotation, Vector3.forward, Vector3.left, Vector3.up, h.z, size.x, size.y, offset);
            Face(centre, rotation, Vector3.back, Vector3.right, Vector3.up, h.z, size.x, size.y, offset);
            Face(centre, rotation, Vector3.up, Vector3.right, Vector3.forward, h.y, size.x, size.z, offset);
            Face(centre, rotation, Vector3.down, Vector3.right, Vector3.back, h.y, size.x, size.z, offset);
        }

        private void Face(Vector3 centre, Quaternion rotation, Vector3 normal, Vector3 across, Vector3 up,
            float distance, float width, float height, Vector2 offset)
        {
            Vector3 n = rotation * normal;
            Vector3 c = centre + n * distance, a = rotation * across * width * .5f, b = rotation * up * height * .5f;
            Quad(c - a - b, c + a - b, c + a + b, c - a + b, n, n, n, n,
                offset + new Vector2(0, 0), offset + new Vector2(width, 0),
                offset + new Vector2(width, height), offset + new Vector2(0, height));
        }

        // Round (or, with few segments, polygonal) shaft from foot upward along the rotated Y axis. topRatio 0 makes a
        // spire. Faceted shading suits octagonal stone towers; smooth shading suits logs and round towers.
        public void Column(Vector3 foot, float radius, float height, int segments = 12, float topRatio = 1,
            Vector3 euler = default, bool faceted = false, bool caps = true)
        {
            Quaternion rotation = Quaternion.Euler(euler);
            float top = radius * topRatio;
            float slant = Mathf.Sqrt(height * height + (radius - top) * (radius - top));
            float step = Mathf.PI * 2 / segments;
            float uOffset = foot.x * .53f + foot.z * .41f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * step, a1 = (i + 1) * step;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 n0 = SideNormal(faceted ? (d0 + d1).normalized : d0, height, radius - top, rotation);
                Vector3 n1 = SideNormal(faceted ? (d0 + d1).normalized : d1, height, radius - top, rotation);
                float u0 = uOffset + i * step * radius, u1 = uOffset + (i + 1) * step * radius;
                Quad(foot + rotation * (d0 * radius), foot + rotation * (d1 * radius),
                    foot + rotation * (d1 * top + Vector3.up * height), foot + rotation * (d0 * top + Vector3.up * height),
                    n0, n1, n1, n0,
                    new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, slant), new Vector2(u0, slant));
            }
            if (!caps) return;
            Disc(foot, rotation, radius, segments, false);
            if (top > .01f) Disc(foot + rotation * Vector3.up * height, rotation, top, segments, true);
        }

        private static Vector3 SideNormal(Vector3 direction, float height, float drop, Quaternion rotation)
            => rotation * (direction * height + Vector3.up * drop).normalized;

        private void Disc(Vector3 centre, Quaternion rotation, float radius, int segments, bool up)
        {
            Vector3 n = rotation * (up ? Vector3.up : Vector3.down);
            float step = Mathf.PI * 2 / segments;
            for (int i = 0; i < segments; i++)
            {
                Vector2 p0 = new Vector2(Mathf.Cos(i * step), Mathf.Sin(i * step)) * radius;
                Vector2 p1 = new Vector2(Mathf.Cos((i + 1) * step), Mathf.Sin((i + 1) * step)) * radius;
                Triangle(centre, centre + rotation * new Vector3(p0.x, 0, p0.y), centre + rotation * new Vector3(p1.x, 0, p1.y),
                    n, Vector2.zero, p0, p1);
            }
        }

        // Pitched roof: two slopes on the current layer, the triangular gable ends on gableLayer.
        public void Gable(Vector3 eaveCentre, float width, float depth, float height, float yaw, int gableLayer)
        {
            Quaternion rotation = Quaternion.Euler(0, yaw, 0);
            Vector3 P(float x, float y, float z) => eaveCentre + rotation * new Vector3(x, y, z);
            float w = width * .5f, d = depth * .5f, slope = Mathf.Sqrt(w * w + height * height);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 n = rotation * new Vector3(side * height, w, 0).normalized;
                Quad(P(side * w, 0, -d), P(side * w, 0, d), P(0, height, d), P(0, height, -d), n, n, n, n,
                    new Vector2(0, 0), new Vector2(depth, 0), new Vector2(depth, slope), new Vector2(0, slope));
            }
            int roofLayer = Layer;
            Layer = gableLayer;
            for (int end = -1; end <= 1; end += 2)
            {
                Vector3 n = rotation * new Vector3(0, 0, end);
                Triangle(P(-w, 0, end * d), P(w, 0, end * d), P(0, height, end * d), n,
                    new Vector2(0, 0), new Vector2(width, 0), new Vector2(w, height));
            }
            Layer = roofLayer;
        }

        // A wall of width x height x thickness pierced by one arch of the given span; the opening is straight up to
        // `spring`, then a round or pointed (gothic) arch. Built as two piers plus the curved spandrel and soffit.
        public void ArchWall(Vector3 foot, float width, float height, float thickness, float span, float spring,
            bool pointed, float yaw = 0)
        {
            Quaternion rotation = Quaternion.Euler(0, yaw, 0);
            float pier = (width - span) * .5f, t = thickness * .5f;
            for (int side = -1; side <= 1; side += 2)
                Box(foot + rotation * new Vector3(side * (span * .5f + pier * .5f), height * .5f, 0),
                    new Vector3(pier, height, thickness), new Vector3(0, yaw, 0));
            const int steps = 14;
            var curve = new Vector2[steps + 1];
            float radius = pointed ? span * .82f : span * .5f;
            float apexAngle = Mathf.Acos(Mathf.Clamp((span * .5f - radius) / radius, -1, 1));
            for (int i = 0; i <= steps; i++)
            {
                // Left half rises from the left jamb to the apex, the right half mirrors it.
                float k = i / (float)steps;
                bool left = k <= .5f;
                float angle = Mathf.Lerp(Mathf.PI, apexAngle, left ? k * 2 : (1 - k) * 2);
                float x = -span * .5f + radius + radius * Mathf.Cos(angle);
                curve[i] = new Vector2(left ? x : -x, Mathf.Min(height - .05f, spring + radius * Mathf.Sin(angle)));
            }
            Vector3 P(float x, float y, float z) => foot + rotation * new Vector3(x, y, z);
            Vector3 front = rotation * Vector3.back, back = rotation * Vector3.forward;
            for (int i = 0; i < steps; i++)
            {
                Vector2 a = curve[i], b = curve[i + 1];
                Quad(P(a.x, a.y, -t), P(b.x, b.y, -t), P(b.x, height, -t), P(a.x, height, -t), front, front, front, front,
                    new Vector2(a.x, a.y), new Vector2(b.x, b.y), new Vector2(b.x, height), new Vector2(a.x, height));
                Quad(P(a.x, a.y, t), P(b.x, b.y, t), P(b.x, height, t), P(a.x, height, t), back, back, back, back,
                    new Vector2(-a.x, a.y), new Vector2(-b.x, b.y), new Vector2(-b.x, height), new Vector2(-a.x, height));
                // Soffit normals point into the opening, toward the arch's springing centre.
                Vector3 na = rotation * (Vector3)(new Vector2(0, spring) - a).normalized;
                Vector3 nb = rotation * (Vector3)(new Vector2(0, spring) - b).normalized;
                float arc = Vector2.Distance(a, b);
                Quad(P(a.x, a.y, -t), P(b.x, b.y, -t), P(b.x, b.y, t), P(a.x, a.y, t), na, nb, nb, na,
                    new Vector2(i * arc, 0), new Vector2((i + 1) * arc, 0), new Vector2((i + 1) * arc, thickness), new Vector2(i * arc, thickness));
            }
            Vector3 up = rotation * Vector3.up;
            Quad(P(-span * .5f, height, -t), P(span * .5f, height, -t), P(span * .5f, height, t), P(-span * .5f, height, t),
                up, up, up, up, new Vector2(0, 0), new Vector2(span, 0), new Vector2(span, thickness), new Vector2(0, thickness));
        }

        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd,
            Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            normals.Add(na); normals.Add(nb); normals.Add(nc); normals.Add(nd);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
            Indices(start, start + 1, start + 2);
            Indices(start, start + 2, start + 3);
        }

        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 n, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc);
            Indices(start, start + 1, start + 2);
        }

        // Winding follows the intended normal, so primitives never need hand-ordered corners.
        private void Indices(int a, int b, int c)
        {
            Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            List<int> target = layers[Mathf.Clamp(Layer, 0, layers.Length - 1)];
            if (Vector3.Dot(face, normals[a] + normals[b] + normals[c]) >= 0) { target.Add(a); target.Add(b); target.Add(c); }
            else { target.Add(a); target.Add(c); target.Add(b); }
        }

        public Mesh Bake(string name)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = layers.Length;
            for (int i = 0; i < layers.Length; i++) mesh.SetTriangles(layers[i], i, false);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.UploadMeshData(false);
            return mesh;
        }
    }
}
