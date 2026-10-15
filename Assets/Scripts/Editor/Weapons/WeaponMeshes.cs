using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Procedural hard-surface weapon parts in weapon space (grip centre at the origin, blade or head along +Y),
    // built after the reference renders in Docs/ArtReference. Edged weapons cut forward: the edges face ±Z (the
    // swing direction of the slash clips) and the flats ±X. Round shields face +Z. Faceted parts are flat shaded
    // with box-mapped UVs so tiling surface textures (HammeredIron, WoodGrain) read the same on every face.
    internal static partial class WeaponMeshes
    {
        private const string TextureFolder = "Assets/Art/Weapons/Textures";
        private const float IronTiling = 10f; // texture repeats per metre
        private const float WoodTiling = 2f;

        // ---------- Blade family ----------

        public sealed class SwordSpec
        {
            public float Scale = 1f;                 // hilt size (guard, collars, pommel)
            public float GuardSpread = 1f;           // widens the cross-guard only
            public float BaseY, TaperY, TipY, Half;  // blade start, where the point begins, tip height, half width
            public float RidgeBase, RidgeTip;        // half thickness along the centre ridge
            public float ChipDepth;
            public float[] ChipRight, ChipLeft;      // heights of the edge chips
            public float GuardY;
            public float GripY0, GripY1, GripRadius, GripPitch;
        }

        // Chipped iron one-handed sword (Raider_03_Weapon_Sword_v1.png), 0.92 m.
        public static readonly SwordSpec SwordSpecRaider = new SwordSpec
        {
            BaseY = .15f, TaperY = .575f, TipY = .754f, Half = .041f, RidgeBase = .0075f, RidgeTip = .0045f, ChipDepth = .005f,
            ChipRight = new[] { .24f, .33f, .43f, .5f }, ChipLeft = new[] { .21f, .29f, .39f, .47f },
            GuardY = .148f, GripY0 = -.12f, GripY1 = .12f, GripRadius = .0175f, GripPitch = .032f,
        };

        // Clean, unchipped one-handed longsword for the allied side: the Raider's proportions without the damage.
        public static readonly SwordSpec SwordSpecAllied = new SwordSpec
        {
            BaseY = .15f, TaperY = .575f, TipY = .754f, Half = .038f, RidgeBase = .0075f, RidgeTip = .0045f, ChipDepth = 0f,
            ChipRight = new float[0], ChipLeft = new float[0],
            GuardY = .148f, GripY0 = -.12f, GripY1 = .12f, GripRadius = .0175f, GripPitch = .032f,
        };

        // Leaf-shaped chipped dagger with a flat plate guard (Swarmling_03_Weapon_Dagger_v1.png), 0.5 m.
        public static readonly SwordSpec DaggerSpec = new SwordSpec
        {
            Scale = .6f, BaseY = .07f, TaperY = .13f, TipY = .345f, Half = .024f, RidgeBase = .005f, RidgeTip = .003f, ChipDepth = .004f,
            ChipRight = new[] { .1f, .15f, .2f }, ChipLeft = new[] { .09f, .13f, .18f, .22f },
            GuardY = .062f, GripY0 = -.055f, GripY1 = .045f, GripRadius = .0135f, GripPitch = .022f,
        };

        // Blade, hilt (guard, collars, pommel) and wrapped grip. Returns the blade tip height.
        public static float Sword(string prefix, SwordSpec p, out Mesh blade, out Mesh hilt, out Mesh grip)
        {
            float Profile(float y)
            {
                if (y <= p.TaperY) return p.Half;
                float u = Mathf.Clamp01((y - p.TaperY) / (p.TipY - p.TaperY));
                return p.Half * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(u * Mathf.PI * .5f)), .7f); // cos(pi/2) can be -4e-8: NaN vertices make Unity drop the mesh
            }
            var heights = new SortedSet<float>();
            float step = Mathf.Max(.02f, (p.TaperY - p.BaseY) / 40f);
            for (float y = p.BaseY; y < p.TaperY; y += step) heights.Add(y);
            for (float y = p.TaperY; y < p.TipY; y += (p.TipY - p.TaperY) / 14f) heights.Add(y);
            heights.Add(p.TipY);
            foreach (float y in p.ChipRight) foreach (float k in ChipKeys) heights.Add(y + k);
            foreach (float y in p.ChipLeft) foreach (float k in ChipKeys) heights.Add(y + k);

            var samples = new List<BladeSample>();
            foreach (float y in heights)
            {
                float u = Mathf.InverseLerp(p.BaseY, p.TipY, y);
                float point = Mathf.Clamp01((p.TipY - y) / .03f);
                samples.Add(new BladeSample
                {
                    Y = y,
                    Right = Mathf.Max(0f, Profile(y) - Chip(y, p.ChipRight, p.ChipDepth)),
                    Left = Mathf.Max(0f, Profile(y) - Chip(y, p.ChipLeft, p.ChipDepth)),
                    Ridge = Mathf.Lerp(p.RidgeBase, p.RidgeTip, Mathf.Clamp01(u / .8f)) * point,
                    Edge = .0012f * point,
                });
            }
            var bladeBuilder = new FacetedMesh();
            bladeBuilder.Blade(samples);

            float s = p.Scale, r = p.GripRadius, g = p.GuardSpread;
            var hiltBuilder = new FacetedMesh();
            hiltBuilder.Box(new Vector3(-.019f * s, p.GuardY - .024f * s, -.017f * s), new Vector3(.019f * s, p.GuardY + .024f * s, .017f * s), .004f * s);
            foreach (int side in new[] { 1, -1 })
            {
                hiltBuilder.Box(Mirror(new Vector3(.017f * s, p.GuardY - .013f * s, -.012f * s), new Vector3(.017f * s + .06f * s * g, p.GuardY + .013f * s, .012f * s), side, out Vector3 max), max, .003f * s);
                hiltBuilder.Box(Mirror(new Vector3(.073f * s * g, p.GuardY - .018f * s, -.015f * s), new Vector3(.092f * s * g, p.GuardY + .018f * s, .015f * s), side, out max), max, .005f * s);
            }
            float top = p.GripY1, bottom = p.GripY0;
            hiltBuilder.Lathe(new[] { top - .002f * s, top + .001f * s, top + .011f * s, top + .014f * s }, new[] { r + .0015f, r + .006f, r + .006f, r + .0015f }, 8);
            hiltBuilder.Lathe(new[] { bottom - .014f * s, bottom - .011f * s, bottom - .001f * s, bottom + .002f * s }, new[] { r + .0005f, r + .005f, r + .005f, r + .0005f }, 8);
            hiltBuilder.Lathe(new[] { bottom - .014f * s, bottom - .021f * s, bottom - .032f * s, bottom - .044f * s, bottom - .051f * s },
                new[] { .013f * s, .026f * s, .031f * s, .021f * s, 0f }, 8);

            // Modelled with the flats towards ±Z as in the references; turn the edges forward.
            blade = EdgeForward(bladeBuilder.Build(prefix + "Blade", IronTiling));
            hilt = EdgeForward(hiltBuilder.Build(prefix + "Hilt", IronTiling));
            grip = EdgeForward(WrappedGrip(prefix + "Grip", p.GripY0, p.GripY1, r, .003f * Mathf.Max(.7f, s), p.GripPitch));
            return p.TipY;
        }

        // Quarter turn about +Y: a blade modelled across X ends up with its edges along Z.
        private static Mesh EdgeForward(Mesh mesh)
        {
            Quaternion turn = Quaternion.Euler(0f, 90f, 0f);
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = turn * vertices[i];
                normals[i] = turn * normals[i];
                Vector3 t = turn * (Vector3)tangents[i];
                tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------- Round shield ----------

        // Plank shield (Raider_03_Weapon_Shield_v1.png), 0.57 m across, face towards +Z, grip behind at the origin.
        public static void RoundShield(string prefix, out Mesh wood, out Mesh iron)
        {
            const float z = .06f, inner = .27f;
            var planks = new FacetedMesh();
            const int count = 7;
            float width = .56f / count;
            for (int i = 0; i < count; i++)
            {
                float x0 = -.28f + i * width + .002f, x1 = -.28f + (i + 1) * width - .002f;
                float half = Mathf.Sqrt(Mathf.Max(0f, inner * inner - Mathf.Max(x0 * x0, x1 * x1)));
                float front = z + .015f + (i % 3) * .0025f;
                planks.Box(new Vector3(x0, -half, z - .015f), new Vector3(x1, half, front), .004f);
            }
            // Backing board behind the planks: fills the lens-shaped gaps the straight planks leave against the rim.
            var backing = new FacetedMesh();
            backing.Lathe(new[] { 0f, .004f, .016f, .018f }, new[] { .25f, .266f, .266f, .25f }, 32);
            backing.Transform(Quaternion.Euler(90f, 0f, 0f), new Vector3(0f, 0f, z - .013f));
            planks.Append(backing);
            wood = planks.Build(prefix + "Planks", WoodTiling);

            var metal = new FacetedMesh();
            var rimProfile = new[]
            {
                new Vector2(.238f, -.015f), new Vector2(.279f, -.015f), new Vector2(.285f, -.009f), new Vector2(.285f, .009f),
                new Vector2(.279f, .015f), new Vector2(.238f, .015f), new Vector2(.232f, .009f), new Vector2(.232f, -.009f),
            };
            metal.Sweep(rimProfile, 32, new Vector3(0f, 0f, z));
            var plate = new[] { new Vector2(.07f, -.006f), new Vector2(.098f, -.006f), new Vector2(.1f, -.003f), new Vector2(.1f, .003f), new Vector2(.098f, .006f), new Vector2(.07f, .006f) };
            metal.Sweep(plate, 20, new Vector3(0f, 0f, z + .021f));
            var boss = new FacetedMesh();
            boss.Lathe(new[] { 0f, .012f, .03f, .045f, .052f }, new[] { .075f, .072f, .06f, .036f, 0f }, 14);
            boss.Transform(Quaternion.Euler(90f, 0f, 0f), new Vector3(0f, 0f, z + .025f));
            metal.Append(boss);
            for (int i = 0; i < 12; i++)
            {
                float a = (i + .5f) * Mathf.PI * 2f / 12f;
                metal.Append(Rivet(new Vector3(Mathf.Cos(a) * .2585f, Mathf.Sin(a) * .2585f, z + .015f), Vector3.forward, .013f));
            }
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                metal.Append(Rivet(new Vector3(Mathf.Cos(a) * .085f, Mathf.Sin(a) * .085f, z + .027f), Vector3.forward, .008f));
            }
            iron = metal.Build(prefix + "Iron", IronTiling);
        }

        // ---------- Spiked club ----------

        // Iron-capped spiked club (Brute_03_Weapon_Club_v1.png), 1.1 m: wrapped wooden haft, riveted bands,
        // a banded head with pyramid spikes and a pyramid cap. Returns the head's striking height.
        public static float SpikedClub(string prefix, out Mesh wood, out Mesh iron, out Mesh grip)
        {
            var haft = new FacetedMesh();
            haft.Lathe(new[] { -.12f, -.1f, .3f, .46f }, new[] { .026f, .03f, .03f, .034f }, 8);
            wood = haft.Build(prefix + "Haft", WoodTiling * 2f);
            grip = WrappedGrip(prefix + "Grip", -.09f, .17f, .03f, .004f, .045f);

            var metal = new FacetedMesh();
            metal.Lathe(new[] { -.16f, -.15f, -.12f, -.1f, -.095f }, new[] { .02f, .045f, .05f, .05f, .036f }, 8);
            metal.Lathe(new[] { .17f, .175f, .215f, .22f }, new[] { .036f, .043f, .043f, .036f }, 8);
            metal.Lathe(new[] { .4f, .405f, .445f, .45f }, new[] { .04f, .047f, .047f, .04f }, 8);
            metal.Lathe(new[] { .44f, .5f, .62f, .8f, .92f, .96f }, new[] { .07f, .1f, .115f, .118f, .105f, .085f }, 8);
            metal.Lathe(new[] { .5f, .505f, .56f, .565f }, new[] { .12f, .127f, .127f, .12f }, 8);
            foreach (Vector3 dir in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
            {
                Vector3 side = Vector3.Cross(dir, Vector3.up);
                Vector3 across = new Vector3(Mathf.Abs(side.x), 0f, Mathf.Abs(side.z)) * .0175f;
                Vector3 c1 = dir * .106f - across + Vector3.up * .6f, c2 = dir * .122f + across + Vector3.up * .91f;
                metal.Box(Vector3.Min(c1, c2), Vector3.Max(c1, c2), .004f);
                metal.Pyramid(dir * .105f + Vector3.up * .69f, dir, .045f, .09f);
                for (int i = 0; i < 6; i++) metal.Append(Rivet(dir * .124f + Vector3.up * (.62f + i * .055f), dir, .011f));
                Vector3 diagonal = (dir + Vector3.Cross(dir, Vector3.up)).normalized;
                metal.Pyramid(diagonal * .1f + Vector3.up * .83f, diagonal, .042f, .085f);
                metal.Append(Rivet(dir * .129f + Vector3.up * .535f, dir, .012f));
            }
            metal.Pyramid(Vector3.up * .95f, Vector3.up, .062f, .17f);
            iron = metal.Build(prefix + "Iron", IronTiling);
            return .8f;
        }

        // ---------- Hero staff ----------

        // Female hero's staff (HeroStaff_Concept.png), 2.0 m, butt at y = 0, head up +Y. The head is a flat
        // compass-star: rings and spikes are thin plates in the XY plane (facing ±Z) around a faceted orb, so the
        // side view stays slim. The gold is softly shaded and UV-mapped at GoldTiling for the AgedGold textures; the
        // orb is one solid faceted sapphire. Returns the orb centre height.
        private const float GoldTiling = 4f; // texture repeats per metre

        public static float HeroStaff(string prefix, out Mesh gold, out Mesh crystal, out Mesh orb, out Mesh shaft)
        {
            const float centre = 1.716f, orbRadius = .097f;
            var metal = new FacetedMesh();
            var blue = new FacetedMesh();
            var centreOffset = new Vector3(0f, centre, 0f);

            // Two thin rings around the orb, swept about Z.
            metal.Sweep(RingProfile(.158f, .182f, .014f), 40, centreOffset);
            metal.Sweep(RingProfile(.112f, .134f, .011f), 32, centreOffset);

            // Thorns on the outer ring.
            for (int i = 0; i < 12; i++)
            {
                float a = (i + .5f) * Mathf.PI * 2f / 12f;
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                metal.Pyramid(centreOffset + dir * .176f, dir, .009f, .034f);
            }

            // Four cardinal mounts crossing both rings, each ending in a blue diamond; the top one reaches 2.0 m.
            float[] reach = { .252f, .284f, .252f, .205f };
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * .5f;
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 side = Vector3.Cross(dir, Vector3.forward);
                Vector3 c1 = centreOffset + dir * (orbRadius - .008f) - side * .014f + Vector3.back * .014f;
                Vector3 c2 = centreOffset + dir * .2f + side * .014f + Vector3.forward * .014f;
                metal.Box(Vector3.Min(c1, c2), Vector3.Max(c1, c2), .004f);
                Vector3 mid = centreOffset + dir * .2f;
                blue.Pyramid(mid, dir, .02f, reach[i] - .2f);
                blue.Pyramid(mid, -dir, .02f, .03f);
            }

            // Four gold arrowheads between the mounts, outside the outer ring.
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 2 + 1) * Mathf.PI * .25f;
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                metal.Pyramid(centreOffset + dir * .17f, dir, .02f, .085f);
            }

            // Neck under the head: collar, swelling, blue shards front and back and to the sides.
            metal.Lathe(new[] { 1.255f, 1.268f, 1.286f, 1.305f, 1.34f, 1.385f, 1.43f, 1.475f, 1.52f },
                new[] { .022f, .036f, .036f, .024f, .03f, .021f, .028f, .02f, .026f }, 14);
            foreach (float y in new[] { 1.365f, 1.425f })
                foreach (Vector3 dir in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
                    blue.Pyramid(new Vector3(0f, y, 0f) + dir * .02f, dir, .014f, .035f);

            // Shaft bands and the butt spike.
            foreach (float y in new[] { .935f, .457f })
                metal.Lathe(new[] { y - .022f, y - .015f, y + .015f, y + .022f }, new[] { .026f, .032f, .032f, .026f }, 16);
            metal.Lathe(new[] { 0f, .07f, .17f, .245f, .27f, .285f }, new[] { 0f, .02f, .036f, .034f, .03f, .024f }, 10);
            foreach (Vector3 dir in new[] { Vector3.right, Vector3.left })
                blue.Pyramid(new Vector3(0f, .16f, 0f) + dir * .028f, dir, .012f, .018f);

            // Sphere-like faceted gem.
            var gem = new FacetedMesh();
            gem.Lathe(new[] { -orbRadius, -.082f, -.05f, 0f, .05f, .082f, orbRadius },
                new[] { 0f, .05f, .083f, orbRadius, .083f, .05f, 0f }, 14);
            gem.Transform(Quaternion.identity, centreOffset);

            var pole = new FacetedMesh();
            pole.Lathe(new[] { .27f, .285f, 1.268f, 1.285f }, new[] { .02f, .023f, .023f, .02f }, 16);

            gold = metal.BuildSmooth(prefix + "Gold", GoldTiling, 50f);
            crystal = blue.Build(prefix + "Crystal", 1f);
            orb = gem.Build(prefix + "Orb", 5f); // sapphire texture covers ~20 cm: spans the orb
            shaft = pole.BuildSmooth(prefix + "Shaft", 2f, 60f);
            return centre;
        }

        // Ring section (radius, along Z) for FacetedMesh.Sweep: a rectangle with chamfered corners, so the soft
        // shading rolls the edges and they catch light.
        private static Vector2[] RingProfile(float inner, float outer, float halfDepth)
        {
            float c = Mathf.Min(.005f, halfDepth * .45f);
            return new[]
            {
                new Vector2(inner + c, -halfDepth), new Vector2(outer - c, -halfDepth), new Vector2(outer, -halfDepth + c), new Vector2(outer, halfDepth - c),
                new Vector2(outer - c, halfDepth), new Vector2(inner + c, halfDepth), new Vector2(inner, halfDepth - c), new Vector2(inner, -halfDepth + c),
            };
        }

        // ---------- Parts ----------

        public struct BladeSample
        {
            public float Y, Right, Left, Ridge, Edge; // half-widths to each edge, ridge and edge half-thickness
        }

        // A chip: a sharp step into the edge that eases back out over 14 mm.
        private static readonly float[] ChipKeys = { 0f, .004f, .009f, .018f };

        private static float Chip(float y, float[] chips, float depth)
        {
            float result = 0f;
            foreach (float c in chips)
            {
                float d = y - c;
                if (d < 0f || d > .018f) continue;
                result = Mathf.Max(result, d < .004f ? depth * d / .004f : depth * (1f - (d - .004f) / .014f));
            }
            return result;
        }

        private static Vector3 Mirror(Vector3 min, Vector3 max, int side, out Vector3 mirroredMax)
        {
            if (side > 0) { mirroredMax = max; return min; }
            mirroredMax = new Vector3(-min.x, max.y, max.z);
            return new Vector3(-max.x, min.y, min.z);
        }

        // Domed rivet head standing on a surface: base at `position`, pointing along `direction`.
        private static FacetedMesh Rivet(Vector3 position, Vector3 direction, float radius)
        {
            var rivet = new FacetedMesh();
            rivet.Lathe(new[] { 0f, radius * .5f, radius * .85f }, new[] { radius, radius * .85f, 0f }, 6);
            rivet.Transform(Quaternion.FromToRotation(Vector3.up, direction), position);
            return rivet;
        }

        // Leather strap wound in a spiral around a cylinder: each turn stands proud and slopes into the next.
        public static Mesh WrappedGrip(string name, float y0, float y1, float radius, float wrap, float pitch, int segments = 16)
        {
            int rows = Mathf.CeilToInt((y1 - y0) / .004f) + 1;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int r = 0; r < rows; r++)
            {
                float y = Mathf.Lerp(y0, y1, r / (rows - 1f));
                for (int s = 0; s <= segments; s++)
                {
                    float turn = s / (float)segments, angle = turn * Mathf.PI * 2f;
                    float f = Mathf.Repeat(turn + y / pitch, 1f);
                    float band = f < .12f ? f / .12f : 1f - .3f * (f - .12f) / .88f;
                    float rad = radius + wrap * band;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * rad, y, Mathf.Sin(angle) * rad));
                    uv.Add(new Vector2(turn, y * 4f));
                }
            }
            int stride = segments + 1;
            for (int r = 0; r < rows - 1; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = r * stride + s, b = a + 1, c = a + stride, d = c + 1;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            // Weld the shading across the UV seam.
            Vector3[] normals = mesh.normals;
            for (int r = 0; r < rows; r++)
            {
                Vector3 n = (normals[r * stride] + normals[r * stride + segments]).normalized;
                normals[r * stride] = normals[r * stride + segments] = n;
            }
            mesh.normals = normals;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Flat-shaded triangle soup; each triangle faces away from the interior point given with it.
        private sealed class FacetedMesh
        {
            private readonly List<Vector3> vertices = new List<Vector3>();

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-14f) return;
                if (Vector3.Dot(normal, (a + b + c) / 3f - inside) < 0f) (b, c) = (c, b);
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside)
            {
                Triangle(a, b, c, inside);
                Triangle(a, c, d, inside);
            }

            public void Append(FacetedMesh other) => vertices.AddRange(other.vertices);

            public void Scale(float factor, Vector3 about)
            {
                for (int i = 0; i < vertices.Count; i++) vertices[i] = about + (vertices[i] - about) * factor;
            }

            public void Transform(Quaternion rotation, Vector3 offset)
            {
                for (int i = 0; i < vertices.Count; i++) vertices[i] = rotation * vertices[i] + offset;
            }

            // Six-sided section: right edge (thin flat), front ridge, left edge, back ridge.
            public void Blade(IList<BladeSample> samples)
            {
                Vector3[] Section(BladeSample s) => new[]
                {
                    new Vector3(s.Right, s.Y, s.Edge), new Vector3(0f, s.Y, s.Ridge), new Vector3(-s.Left, s.Y, s.Edge),
                    new Vector3(-s.Left, s.Y, -s.Edge), new Vector3(0f, s.Y, -s.Ridge), new Vector3(s.Right, s.Y, -s.Edge),
                };
                for (int i = 0; i < samples.Count - 1; i++)
                {
                    Vector3[] a = Section(samples[i]), b = Section(samples[i + 1]);
                    var inside = new Vector3(0f, (samples[i].Y + samples[i + 1].Y) * .5f, 0f);
                    for (int k = 0; k < 6; k++)
                    {
                        int n = (k + 1) % 6;
                        Quad(a[k], a[n], b[n], b[k], inside);
                    }
                }
            }

            // Box with every edge chamfered: an octagonal prism with inset end rings.
            public void Box(Vector3 min, Vector3 max, float chamfer)
            {
                Vector3 centre = (min + max) * .5f;
                Vector3[] Ring(float y, float inset)
                {
                    float x0 = min.x + inset, x1 = max.x - inset, z0 = min.z + inset, z1 = max.z - inset, c = chamfer;
                    return new[]
                    {
                        new Vector3(x0 + c, y, z0), new Vector3(x1 - c, y, z0), new Vector3(x1, y, z0 + c), new Vector3(x1, y, z1 - c),
                        new Vector3(x1 - c, y, z1), new Vector3(x0 + c, y, z1), new Vector3(x0, y, z1 - c), new Vector3(x0, y, z0 + c),
                    };
                }
                Vector3[][] rings = { Ring(min.y, chamfer), Ring(min.y + chamfer, 0f), Ring(max.y - chamfer, 0f), Ring(max.y, chamfer) };
                for (int r = 0; r < rings.Length - 1; r++)
                    for (int k = 0; k < 8; k++)
                        Quad(rings[r][k], rings[r][(k + 1) % 8], rings[r + 1][(k + 1) % 8], rings[r + 1][k], centre);
                foreach (Vector3[] cap in new[] { rings[0], rings[3] })
                    for (int k = 1; k < 7; k++) Triangle(cap[0], cap[k], cap[k + 1], centre);
            }

            // Faceted body of revolution; a non-zero end radius is capped.
            public void Lathe(float[] heights, float[] radii, int segments)
            {
                var centre = new Vector3(0f, (heights[0] + heights[heights.Length - 1]) * .5f, 0f);
                Vector3 Point(int r, int s)
                {
                    float angle = (s % segments + .5f) * Mathf.PI * 2f / segments;
                    return new Vector3(Mathf.Cos(angle) * radii[r], heights[r], Mathf.Sin(angle) * radii[r]);
                }
                for (int r = 0; r < heights.Length - 1; r++)
                    for (int s = 0; s < segments; s++)
                        Quad(Point(r, s), Point(r, s + 1), Point(r + 1, s + 1), Point(r + 1, s), centre);
                foreach (int r in new[] { 0, heights.Length - 1 })
                    if (radii[r] > 0f)
                        for (int s = 1; s < segments - 1; s++) Triangle(Point(r, 0), Point(r, s), Point(r, s + 1), centre);
            }

            // Closed profile (radius, along-axis) swept once around the Z axis through `origin`.
            public void Sweep(Vector2[] profile, int segments, Vector3 origin)
            {
                Vector2 centroid = Vector2.zero;
                foreach (Vector2 p in profile) centroid += p / profile.Length;
                Vector3 At(Vector2 p, float angle) => origin + new Vector3(Mathf.Cos(angle) * p.x, Mathf.Sin(angle) * p.x, p.y);
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                    Vector3 inside = At(centroid, (a0 + a1) * .5f);
                    for (int k = 0; k < profile.Length; k++)
                    {
                        Vector2 p = profile[k], q = profile[(k + 1) % profile.Length];
                        Quad(At(p, a0), At(p, a1), At(q, a1), At(q, a0), inside);
                    }
                }
            }

            // Four-sided spike standing on `baseCentre`, pointing along `axis`.
            public void Pyramid(Vector3 baseCentre, Vector3 axis, float half, float length)
            {
                axis.Normalize();
                Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) > .9f ? Vector3.forward : Vector3.up).normalized;
                Vector3 v = Vector3.Cross(axis, u);
                Vector3 apex = baseCentre + axis * length, inside = baseCentre + axis * length * .3f;
                Vector3[] corner =
                {
                    baseCentre + (u + v) * half, baseCentre + (u - v) * half, baseCentre + (-u - v) * half, baseCentre + (-u + v) * half,
                };
                for (int k = 0; k < 4; k++) Triangle(corner[k], corner[(k + 1) % 4], apex, inside);
            }

            // Build with soft shading: each vertex averages the normals of the facets meeting at its position that
            // bend away from it by less than `maxAngle` degrees, so chamfers and rounds read smooth while sharp
            // edges (spikes, gem facets) stay crisp.
            public Mesh BuildSmooth(string name, float tiling, float maxAngle)
            {
                Mesh mesh = Build(name, tiling);
                Vector3[] position = mesh.vertices, flat = mesh.normals;
                var groups = new Dictionary<Vector3Int, List<int>>();
                for (int i = 0; i < position.Length; i++)
                {
                    var key = new Vector3Int(Mathf.RoundToInt(position[i].x * 1e4f), Mathf.RoundToInt(position[i].y * 1e4f), Mathf.RoundToInt(position[i].z * 1e4f));
                    if (!groups.TryGetValue(key, out List<int> list)) groups[key] = list = new List<int>();
                    list.Add(i);
                }
                float limit = Mathf.Cos(maxAngle * Mathf.Deg2Rad);
                var soft = new Vector3[flat.Length];
                foreach (List<int> group in groups.Values)
                    foreach (int i in group)
                    {
                        Vector3 sum = Vector3.zero;
                        foreach (int j in group) if (Vector3.Dot(flat[i], flat[j]) >= limit) sum += flat[j];
                        soft[i] = sum.sqrMagnitude > 1e-8f ? sum.normalized : flat[i];
                    }
                mesh.normals = soft;
                mesh.RecalculateTangents();
                return mesh;
            }

            public Mesh Build(string name, float tiling)
            {
                var uv = new Vector2[vertices.Count];
                var triangles = new int[vertices.Count];
                for (int i = 0; i < vertices.Count; i += 3)
                {
                    Vector3 n = Vector3.Cross(vertices[i + 1] - vertices[i], vertices[i + 2] - vertices[i]);
                    float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                    for (int k = i; k < i + 3; k++)
                    {
                        Vector3 p = vertices[k] * tiling;
                        uv[k] = ax >= ay && ax >= az ? new Vector2(p.z, p.y) : ay >= az ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);
                        triangles[k] = k;
                    }
                }
                var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                mesh.SetVertices(vertices);
                mesh.uv = uv;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        // ---------- Surface ----------

        // URP Lit iron with a tiling hammered surface: facets, pits and dark rust-tinted seams.
        public static Material HammeredIron(string folder, string name, Color tint, float metallic, float smoothness)
            => Textured(folder, name, tint, metallic, smoothness, "HammeredIron", GenerateIron);

        // Weathered plank wood with vertical grain and dark cracks.
        public static Material WoodGrain(string folder, string name, Color tint)
            => Textured(folder, name, tint, 0f, .22f, "WoodGrain", GenerateWood);

        private static Material Textured(string folder, string name, Color tint, float metallic, float smoothness, string texture, Action generate)
        {
            string albedo = $"{TextureFolder}/{texture}_Albedo.png", normal = $"{TextureFolder}/{texture}_Normal.png";
            if (!File.Exists(albedo) || !File.Exists(normal)) generate();
            Material material = RigBuildUtility.MakeMaterial(folder, name, tint, metallic, smoothness);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo)
                ?? throw new InvalidOperationException(albedo + " could not be imported."));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal)
                ?? throw new InvalidOperationException(normal + " could not be imported."));
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SaveTextures(string name, Texture2D albedo, Texture2D bump)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art/Weapons")) AssetDatabase.CreateFolder("Assets/Art", "Weapons");
            if (!AssetDatabase.IsValidFolder(TextureFolder)) AssetDatabase.CreateFolder("Assets/Art/Weapons", "Textures");
            foreach ((Texture2D texture, bool isNormal) in new[] { (albedo, false), (bump, true) })
            {
                string path = $"{TextureFolder}/{name}_{(isNormal ? "Normal" : "Albedo")}.png";
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
        }

        // Tileable Voronoi hammering, deterministic: a shallow dent per cell and a fine seam between cells.
        private static void GenerateIron()
        {
            const int size = 256, cells = 22;
            var random = new System.Random(20261012);
            var seeds = new Vector2[cells];
            for (int i = 0; i < cells; i++) seeds[i] = new Vector2((float)random.NextDouble() * size, (float)random.NextDouble() * size);
            var height = new float[size, size];
            var seam = new float[size, size];
            float cell = size / Mathf.Sqrt(cells);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float f1 = float.MaxValue, f2 = float.MaxValue;
                    foreach (Vector2 seed in seeds)
                        for (int ox = -1; ox <= 1; ox++)
                            for (int oy = -1; oy <= 1; oy++)
                            {
                                float d = Vector2.Distance(new Vector2(x, y), seed + new Vector2(ox * size, oy * size));
                                if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
                            }
                    float bowl = Mathf.Clamp01(f1 / (cell * .6f));
                    float line = 1f - Mathf.SmoothStep(0f, 1f, (f2 - f1) / 3.5f);
                    float pit = Mathf.PerlinNoise(x * .21f + 3.1f, y * .21f + 7.7f) > .78f ? .35f : 0f;
                    height[x, y] = .55f * bowl * bowl - .5f * line - pit;
                    seam[x, y] = line;
                }
            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var bump = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color dark = new Color(.24f, .22f, .21f), light = new Color(.5f, .5f, .52f), rust = new Color(.33f, .2f, .14f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float grain = Mathf.PerlinNoise(x * .05f, y * .05f) * .5f + Mathf.PerlinNoise(x * .3f, y * .3f) * .5f;
                    Color c = Color.Lerp(dark, light, Mathf.Clamp01(.35f + .5f * height[x, y] + .3f * grain));
                    c = Color.Lerp(c, rust * .8f, seam[x, y] * .75f);
                    albedo.SetPixel(x, y, c);
                    float dx = height[(x + 1) % size, y] - height[(x + size - 1) % size, y];
                    float dy = height[x, (y + 1) % size] - height[x, (y + size - 1) % size];
                    Vector3 n = new Vector3(-dx * 2.2f, -dy * 2.2f, 1f).normalized;
                    bump.SetPixel(x, y, new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1f));
                }
            SaveTextures("HammeredIron", albedo, bump);
        }

        // Old plank wood: purple-brown stripes running up the texture, with the odd dark crack.
        private static void GenerateWood()
        {
            const int size = 256;
            var height = new float[size, size];
            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var bump = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color dark = new Color(.14f, .1f, .13f), mid = new Color(.3f, .2f, .22f), light = new Color(.45f, .3f, .24f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float stripe = Mathf.PerlinNoise(x * .1f + 11.3f, y * .012f);
                    float fine = Mathf.PerlinNoise(x * .55f, y * .03f);
                    float crackNoise = Mathf.PerlinNoise(x * .13f + 40f, y * .008f + 5f);
                    float crack = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(crackNoise - .62f) / .018f);
                    float value = Mathf.Clamp01(.65f * stripe + .35f * fine);
                    height[x, y] = value - .8f * crack;
                    Color c = value < .5f ? Color.Lerp(dark, mid, value * 2f) : Color.Lerp(mid, light, (value - .5f) * 2f);
                    albedo.SetPixel(x, y, Color.Lerp(c, dark * .6f, crack * .85f));
                }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = height[(x + 1) % size, y] - height[(x + size - 1) % size, y];
                    float dy = height[x, (y + 1) % size] - height[x, (y + size - 1) % size];
                    Vector3 n = new Vector3(-dx * 3f, -dy * 3f, 1f).normalized;
                    bump.SetPixel(x, y, new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1f));
                }
            SaveTextures("WoodGrain", albedo, bump);
        }
    }
}
