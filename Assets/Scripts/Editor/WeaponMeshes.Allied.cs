using System.Collections.Generic;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    // Allied weapons: spear, tower and kite shields, flanged mace, lance, arrow, quiver, bow fittings and the two
    // casters' staffs. Same conventions as the rest of WeaponMeshes: grip at the origin, head along +Y, shields face +Z.
    // Every mesh is returned unsaved; callers attach them through RigBuildUtility.MeshPart.
    internal static partial class WeaponMeshes
    {
        // ---------- Helpers ----------

        private static Vector2 Left(Vector2 v) => new Vector2(-v.y, v.x);

        // Convex counter-clockwise outline pulled inwards by `d` (mitred corners).
        private static Vector2[] Inset(Vector2[] outline, float d)
        {
            int n = outline.Length;
            var result = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 a = outline[(i + n - 1) % n], b = outline[i], c = outline[(i + 1) % n];
                Vector2 n0 = Left((b - a).normalized), n1 = Left((c - b).normalized);
                result[i] = b + (n0 + n1) * d / (1f + Vector2.Dot(n0, n1));
            }
            return result;
        }

        // Closed profile of (inset from the outline, z) swept once around an outline: rims, slabs and domes.
        private static void Band(FacetedMesh mesh, Vector2[] outline, Vector2[] profile)
        {
            int n = outline.Length, m = profile.Length;
            var rings = new Vector2[m][];
            Vector2 centre = Vector2.zero;
            for (int j = 0; j < m; j++) { rings[j] = Inset(outline, profile[j].x); centre += profile[j] / m; }
            Vector2[] inner = Inset(outline, centre.x);
            for (int k = 0; k < n; k++)
            {
                int next = (k + 1) % n;
                Vector2 mid = (inner[k] + inner[next]) * .5f;
                var inside = new Vector3(mid.x, mid.y, centre.y);
                for (int j = 0; j < m; j++)
                {
                    int q = (j + 1) % m;
                    mesh.Quad(new Vector3(rings[j][k].x, rings[j][k].y, profile[j].y), new Vector3(rings[j][next].x, rings[j][next].y, profile[j].y),
                        new Vector3(rings[q][next].x, rings[q][next].y, profile[q].y), new Vector3(rings[q][k].x, rings[q][k].y, profile[q].y), inside);
                }
            }
        }

        private static void Fan(FacetedMesh mesh, Vector2[] outline, float z, float insideZ)
        {
            Vector3 P(int i) => new Vector3(outline[i].x, outline[i].y, z);
            var inside = new Vector3(outline[0].x, outline[0].y, insideZ);
            for (int k = 1; k < outline.Length - 1; k++) mesh.Triangle(P(0), P(k), P(k + 1), inside);
        }

        // Thin faceted rod from a to b, thicker at the start.
        private static FacetedMesh Rod(Vector3 a, Vector3 b, float radiusA, float radiusB, int sides = 6)
        {
            var rod = new FacetedMesh();
            float length = (b - a).magnitude;
            rod.Lathe(new[] { 0f, length }, new[] { radiusA, radiusB }, sides);
            rod.Transform(Quaternion.FromToRotation(Vector3.up, (b - a) / length), a);
            return rod;
        }

        private static FacetedMesh Shaft(float[] heights, float[] radii, int sides = 10)
        {
            var shaft = new FacetedMesh();
            shaft.Lathe(heights, radii, sides);
            return shaft;
        }

        // ---------- Spear ----------

        // Leaf-bladed spear: shaft butt at buttY, socket under the head at shaftTop, tip at tipY.
        public static void Spear(string prefix, float buttY, float shaftTop, float tipY, out Mesh wood, out Mesh grip, out Mesh steel, out Mesh gold)
        {
            wood = Shaft(new[] { buttY + .05f, -.1f, .15f, shaftTop - .02f }, new[] { .017f, .019f, .019f, .0165f }, 10).Build(prefix + "Shaft", WoodTiling);
            grip = WrappedGrip(prefix + "Grip", -.11f, .1f, .0215f, .003f, .032f);

            var metal = new FacetedMesh();
            metal.Lathe(new[] { buttY - .11f, buttY - .05f, buttY - .01f, buttY + .06f, buttY + .08f }, new[] { 0f, .014f, .026f, .026f, .02f }, 8);
            metal.Lathe(new[] { shaftTop - .02f, shaftTop + .01f, shaftTop + .06f, shaftTop + .095f }, new[] { .027f, .031f, .024f, .014f }, 8);
            float baseY = shaftTop + .085f, length = tipY - baseY;
            var samples = new List<BladeSample>();
            for (int i = 0; i <= 16; i++)
            {
                float u = i / 16f, width;
                if (u < .3f) width = Mathf.Lerp(.014f, .06f, Mathf.SmoothStep(0f, 1f, u / .3f));
                else width = .06f * Mathf.Pow(Mathf.Max(0f, Mathf.Cos((u - .3f) / .7f * Mathf.PI * .5f)), .8f);
                float point = Mathf.Clamp01((1f - u) / .06f);
                samples.Add(new BladeSample { Y = baseY + length * u, Right = width, Left = width, Ridge = .014f * (1f - .55f * u) * point, Edge = .0012f * point });
            }
            metal.Blade(samples);
            steel = metal.Build(prefix + "Steel", IronTiling);

            var trim = new FacetedMesh();
            trim.Lathe(new[] { shaftTop - .035f, shaftTop - .028f, shaftTop - .015f, shaftTop - .008f }, new[] { .02f, .03f, .03f, .02f }, 8);
            trim.Lathe(new[] { shaftTop + .005f, shaftTop + .018f, shaftTop + .03f }, new[] { .034f, .036f, .028f }, 8);
            foreach (int side in new[] { -1, 1 })
            {
                trim.Append(Rod(new Vector3(side * .02f, shaftTop + .024f, 0f), new Vector3(side * .085f, shaftTop + .05f, 0f), .011f, .005f));
                trim.Pyramid(new Vector3(side * .085f, shaftTop + .05f, 0f), new Vector3(side, .35f, 0f), .007f, .035f);
            }
            trim.Lathe(new[] { buttY - .008f, buttY + .0f, buttY + .03f, buttY + .045f }, new[] { .022f, .031f, .031f, .024f }, 8);
            gold = trim.BuildSmooth(prefix + "Gold", GoldTiling, 50f);
        }

        // ---------- Shields ----------

        private static readonly Vector2[] TowerOutline =
        {
            new Vector2(0f, -.70f), new Vector2(.22f, -.66f), new Vector2(.40f, -.56f), new Vector2(.40f, .52f), new Vector2(.34f, .64f),
            new Vector2(.18f, .70f), new Vector2(0f, .72f), new Vector2(-.18f, .70f), new Vector2(-.34f, .64f), new Vector2(-.40f, .52f),
            new Vector2(-.40f, -.56f), new Vector2(-.22f, -.66f),
        };

        private static readonly Vector2[] KiteOutline =
        {
            new Vector2(0f, -.38f), new Vector2(.12f, -.28f), new Vector2(.21f, -.12f), new Vector2(.26f, .06f), new Vector2(.26f, .30f),
            new Vector2(.19f, .37f), new Vector2(0f, .34f), new Vector2(-.19f, .37f), new Vector2(-.26f, .30f), new Vector2(-.26f, .06f),
            new Vector2(-.21f, -.12f), new Vector2(-.12f, -.28f),
        };

        // 0.80 x 1.40 m tower shield. Origin at the board centre, face towards +Z; the grip sits behind it at `gripAt`
        // (board space). Face: domed blue enamel plate in a stepped gold rim with an inlaid bead and a rivet row.
        // Gold: a cross pattee with pointed tips around a sunburst medallion, quadrant studs. Steel: boss and handle.
        public static void TowerShield(string prefix, Vector2 gripAt, out Mesh face, out Mesh gold, out Mesh steel)
        {
            var medallion = new Vector2(0f, .16f);
            face = PlateFace(prefix, TowerOutline, .06f, .2f, .024f, 1.5f);

            var trim = new FacetedMesh();
            Rim(trim, TowerOutline, .07f, .03f);
            Bead(trim, TowerOutline, .088f, .008f, .018f);
            RimRivets(trim, TowerOutline, .035f, .11f, .013f, .029f);
            Sunburst(trim, medallion, .1f, .125f, .06f, 14);
            Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            float[] reach = { .42f, .72f, .26f, .26f };
            for (int i = 0; i < 4; i++) Arm(trim, medallion + directions[i] * .13f, medallion + directions[i] * reach[i], .04f, .084f, .045f, .068f, .06f);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sy in new[] { -1f, 1f })
                {
                    Vector2 at = medallion + new Vector2(sx * .17f, sy * .24f);
                    trim.Append(BoardRivet(at, .03f, .05f));
                    trim.Pyramid(new Vector3(at.x, at.y, .05f), Vector3.forward, .016f, .03f);
                }
            gold = trim.BuildSmooth(prefix + "Gold", GoldTiling, 50f);

            var metal = new FacetedMesh();
            Boss(metal, medallion, .09f, .072f);
            Handle(metal, gripAt);
            steel = metal.BuildSmooth(prefix + "Steel", IronTiling, 50f);
        }

        // 0.52 x 0.75 m kite shield: same construction, with a chief bar, a chevron and a smaller medallion.
        public static void KiteShield(string prefix, Vector2 gripAt, out Mesh face, out Mesh gold, out Mesh steel)
        {
            var medallion = new Vector2(0f, .14f);
            face = PlateFace(prefix, KiteOutline, .04f, .1f, .02f, 1.5f);

            var trim = new FacetedMesh();
            Rim(trim, KiteOutline, .045f, .026f);
            Bead(trim, KiteOutline, .06f, .006f, .014f);
            RimRivets(trim, KiteOutline, .024f, .085f, .01f, .0255f);
            Sunburst(trim, medallion, .066f, .082f, .056f, 12);
            Arm(trim, new Vector2(-.235f, .275f), new Vector2(.235f, .275f), .052f, .052f, .04f, .06f);
            foreach (float s in new[] { -1f, 1f })
            {
                Arm(trim, new Vector2(0f, -.02f), new Vector2(s * .15f, -.17f), .05f, .062f, .04f, .06f);
                trim.Append(BoardRivet(new Vector2(s * .17f, .275f), .02f, .045f));
                trim.Pyramid(new Vector3(s * .12f, -.18f, .042f), Vector3.forward, .014f, .028f);
            }
            trim.Pyramid(new Vector3(0f, -.2f, .042f), Vector3.forward, .016f, .03f);
            gold = trim.BuildSmooth(prefix + "Gold", GoldTiling, 50f);

            var metal = new FacetedMesh();
            Boss(metal, medallion, .056f, .062f);
            Handle(metal, gripAt);
            steel = metal.BuildSmooth(prefix + "Steel", IronTiling, 50f);
        }

        // Dished enamel plate: a bevelled edge, a gentle slope, then a faceted dome rising to the middle.
        private static Mesh PlateFace(string prefix, Vector2[] outline, float rim, float slope, float domeHeight, float tiling)
        {
            var paint = new FacetedMesh();
            Vector2[] board = Inset(outline, rim);
            Band(paint, board, new[] { new Vector2(0f, -.012f), new Vector2(0f, .008f), new Vector2(slope, .03f), new Vector2(slope, -.012f) });
            Vector2[] top = Inset(board, slope);
            var apex = new Vector3(0f, 0f, .03f + domeHeight);
            var inside = new Vector3(0f, 0f, -.01f);
            for (int k = 0; k < top.Length; k++)
            {
                Vector2 a = top[k], b = top[(k + 1) % top.Length];
                paint.Triangle(new Vector3(a.x, a.y, .03f), new Vector3(b.x, b.y, .03f), apex, inside);
                // Back of the plate, so the shield is solid when seen from behind.
                paint.Triangle(new Vector3(a.x, a.y, -.012f), new Vector3(b.x, b.y, -.012f), new Vector3(0f, 0f, -.016f), new Vector3(0f, 0f, .01f));
            }
            return paint.BuildSmooth(prefix + "Face", tiling, 40f);
        }

        // Stepped rim along the whole edge: an outer lip and a rolled top.
        private static void Rim(FacetedMesh mesh, Vector2[] outline, float width, float top)
        {
            float b = width * .35f;
            Band(mesh, outline, new[] { new Vector2(0f, -.022f), new Vector2(0f, top), new Vector2(b, top + .008f), new Vector2(width, top - .002f), new Vector2(width, -.022f) });
        }

        // Thin raised line following the outline, `inset` in from the edge.
        private static void Bead(FacetedMesh mesh, Vector2[] outline, float inset, float baseZ, float height)
        {
            float w = .004f;
            Band(mesh, Inset(outline, inset), new[] { new Vector2(0f, baseZ), new Vector2(0f, baseZ + height), new Vector2(w, baseZ + height + .004f), new Vector2(w, baseZ) });
        }

        private static void RimRivets(FacetedMesh mesh, Vector2[] outline, float inset, float spacing, float radius, float z)
        {
            Vector2[] line = Inset(outline, inset);
            for (int i = 0; i < line.Length; i++)
            {
                Vector2 a = line[i], b = line[(i + 1) % line.Length];
                int count = Mathf.Max(1, Mathf.RoundToInt((b - a).magnitude / spacing));
                for (int k = 0; k < count; k++) mesh.Append(BoardRivet(Vector2.Lerp(a, b, k / (float)count), radius, z));
            }
        }

        // Tapered, bevelled bar lying on the plate from `from` to `to`, `w0` wide at the start and `w1` at the end,
        // ending in a short point.
        private static void Arm(FacetedMesh mesh, Vector2 from, Vector2 to, float w0, float w1, float baseZ, float topZ, float tip = 0f)
        {
            Vector2 d = (to - from).normalized, n = Left(d);
            var outline = new System.Collections.Generic.List<Vector2> { from - n * w0 * .5f, to - n * w1 * .5f };
            if (tip > 0f) outline.Add(to + d * tip);
            outline.Add(to + n * w1 * .5f);
            outline.Add(from + n * w0 * .5f);
            float b = Mathf.Min(w0, w1) * .3f;
            Band(mesh, outline.ToArray(), new[] { new Vector2(0f, baseZ - .004f), new Vector2(0f, topZ - .008f), new Vector2(b, topZ), new Vector2(b, baseZ - .004f) });
        }

        // Gold ring with a crown of rays around a boss.
        private static void Sunburst(FacetedMesh mesh, Vector2 centre, float inner, float outer, float z, int rays)
        {
            var at = new Vector3(centre.x, centre.y, z);
            mesh.Sweep(RingProfile(inner, outer, .012f), 36, at);
            for (int i = 0; i < rays; i++)
            {
                float a = (i + .5f) * Mathf.PI * 2f / rays;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                mesh.Pyramid(at + dir * (outer - .004f), dir, i % 2 == 0 ? .014f : .01f, i % 2 == 0 ? .075f : .045f);
            }
        }

        private static FacetedMesh BoardRivet(Vector2 at, float radius, float z)
        {
            var rivet = new FacetedMesh();
            rivet.Lathe(new[] { 0f, radius * .5f, radius * .9f }, new[] { radius, radius * .85f, 0f }, 8);
            rivet.Transform(Quaternion.Euler(90f, 0f, 0f), new Vector3(at.x, at.y, z));
            return rivet;
        }

        private static void Boss(FacetedMesh mesh, Vector2 centre, float radius, float z)
        {
            var boss = new FacetedMesh();
            boss.Lathe(new[] { 0f, .006f, .018f, .034f, .048f, .058f, .064f }, new[] { radius, radius * .96f, radius * .88f, radius * .7f, radius * .46f, radius * .2f, 0f }, 16);
            boss.Transform(Quaternion.Euler(90f, 0f, 0f), new Vector3(centre.x, centre.y, z - .006f));
            mesh.Append(boss);
        }

        // Arm strap and hand bar behind the board (-Z), centred on the palm.
        private static void Handle(FacetedMesh mesh, Vector2 at)
        {
            mesh.Box(new Vector3(at.x - .022f, at.y - .075f, -.075f), new Vector3(at.x + .022f, at.y + .075f, -.045f), .005f);
            mesh.Box(new Vector3(at.x - .035f, at.y - .09f, -.045f), new Vector3(at.x + .035f, at.y - .07f, -.02f), .004f);
            mesh.Box(new Vector3(at.x - .035f, at.y + .07f, -.045f), new Vector3(at.x + .035f, at.y + .09f, -.02f), .004f);
        }

        // ---------- Flanged mace ----------

        // Grip at the origin, haft up +Y, head centred at headY (about 0.56 m).
        public static void Mace(string prefix, float headY, out Mesh grip, out Mesh steel, out Mesh gold)
        {
            grip = WrappedGrip(prefix + "Grip", -.1f, .085f, .0205f, .003f, .03f);
            var metal = new FacetedMesh();
            metal.Lathe(new[] { .085f, .1f, headY - .08f, headY - .06f }, new[] { .021f, .022f, .02f, .028f }, 8);
            metal.Lathe(new[] { headY - .085f, headY - .06f, headY - .03f, headY, headY + .03f, headY + .06f, headY + .085f },
                new[] { .03f, .052f, .064f, .07f, .064f, .052f, .03f }, 10);
            for (int i = 0; i < 8; i++)
            {
                var flange = new FacetedMesh();
                flange.Box(new Vector3(.045f, headY - .07f, -.0085f), new Vector3(.112f, headY + .07f, .0085f), .004f);
                flange.Pyramid(new Vector3(.112f, headY, 0f), Vector3.right, .0085f, .018f);
                flange.Transform(Quaternion.Euler(0f, i * 45f, 0f), Vector3.zero);
                metal.Append(flange);
            }
            metal.Pyramid(new Vector3(0f, headY + .085f, 0f), Vector3.up, .03f, .075f);
            steel = metal.BuildSmooth(prefix + "Steel", IronTiling, 50f);

            var trim = new FacetedMesh();
            trim.Lathe(new[] { .08f, .088f, .108f, .116f }, new[] { .024f, .034f, .034f, .024f }, 8);
            trim.Lathe(new[] { headY - .1f, headY - .09f, headY - .075f }, new[] { .032f, .04f, .03f }, 10);
            trim.Lathe(new[] { headY + .075f, headY + .09f, headY + .1f }, new[] { .03f, .04f, .032f }, 10);
            trim.Lathe(new[] { -.2f, -.185f, -.16f, -.13f, -.105f, -.095f }, new[] { 0f, .02f, .036f, .038f, .03f, .02f }, 8);
            gold = trim.BuildSmooth(prefix + "Gold", GoldTiling, 50f);
        }

        // ---------- Lance ----------

        // Jousting lance: grip at the origin, butt at buttY, tip at tipY (about 2.2 m), steel vamplate in front of the hand.
        public static void Lance(string prefix, float buttY, float tipY, out Mesh wood, out Mesh grip, out Mesh steel, out Mesh gold)
        {
            wood = Shaft(new[] { buttY + .05f, -.12f, .12f, 1.2f, tipY - .22f }, new[] { .036f, .034f, .034f, .027f, .019f }, 12).Build(prefix + "Shaft", WoodTiling);
            grip = WrappedGrip(prefix + "Grip", -.12f, .1f, .0385f, .0035f, .036f, 20);

            var metal = new FacetedMesh();
            metal.Lathe(new[] { .1f, .125f, .16f, .21f, .27f, .34f }, new[] { .115f, .13f, .125f, .095f, .062f, .038f }, 16);
            float t = tipY - .22f;
            metal.Lathe(new[] { t, t + .03f, t + .08f, t + .14f, tipY }, new[] { .03f, .036f, .034f, .02f, 0f }, 6);
            metal.Lathe(new[] { buttY - .02f, buttY + .05f, buttY + .1f }, new[] { .044f, .049f, .04f }, 12);
            steel = metal.BuildSmooth(prefix + "Steel", IronTiling, 50f);

            var trim = new FacetedMesh();
            trim.Lathe(new[] { .094f, .1f, .125f, .13f }, new[] { .118f, .135f, .135f, .118f }, 16);
            trim.Lathe(new[] { t - .04f, t - .03f, t - .005f, t }, new[] { .024f, .034f, .034f, .024f }, 12);
            trim.Lathe(new[] { 1.0f, 1.012f, 1.04f, 1.052f }, new[] { .027f, .035f, .035f, .027f }, 12);
            trim.Lathe(new[] { buttY - .06f, buttY - .03f, buttY, buttY + .03f }, new[] { 0f, .036f, .05f, .045f }, 12);
            gold = trim.BuildSmooth(prefix + "Gold", GoldTiling, 50f);
        }

        // ---------- Arrow and quiver ----------

        // Arrow along +Z, nock at the origin: shaft, faceted bodkin head, three fletching vanes.
        public static void Arrow(string prefix, float length, out Mesh shaft, out Mesh head, out Mesh fletch)
        {
            const float r = .0065f;
            var wood = new FacetedMesh();
            wood.Lathe(new[] { 0f, .012f, length - .02f }, new[] { r * 1.5f, r, r }, 6);
            wood.Transform(Quaternion.Euler(90f, 0f, 0f), Vector3.zero);
            shaft = wood.Build(prefix + "Shaft", 1f);

            var tip = new FacetedMesh();
            tip.Lathe(new[] { length - .02f, length, length + .012f, length + .05f }, new[] { .008f, .014f, .014f, 0f }, 4);
            tip.Transform(Quaternion.Euler(90f, 0f, 0f), Vector3.zero);
            head = tip.Build(prefix + "Head", 1f);

            var vanes = new FacetedMesh();
            for (int i = 0; i < 3; i++)
            {
                var vane = new FacetedMesh();
                vane.Box(new Vector3(.003f, .012f, -.0014f), new Vector3(.03f, .1f, .0014f), .0007f);
                vane.Transform(Quaternion.Euler(0f, i * 120f, 0f), Vector3.zero);
                vanes.Append(vane);
            }
            vanes.Transform(Quaternion.Euler(90f, 0f, 0f), Vector3.zero);
            fletch = vanes.Build(prefix + "Fletch", 1f);
        }

        // Leather quiver centred at the origin (+-0.22 m along Y, about 0.05 m radius) with gold rim and bands.
        public static void Quiver(string prefix, out Mesh leather, out Mesh gold)
        {
            var body = new FacetedMesh();
            body.Lathe(new[] { -.225f, -.21f, -.1f, .05f, .17f, .215f }, new[] { .012f, .034f, .042f, .049f, .054f, .056f }, 10);
            leather = body.Build(prefix + "Leather", 1f);
            var trim = new FacetedMesh();
            trim.Lathe(new[] { .205f, .212f, .232f, .24f }, new[] { .056f, .064f, .064f, .056f }, 10);
            trim.Lathe(new[] { -.07f, -.062f, -.05f, -.042f }, new[] { .042f, .049f, .049f, .042f }, 10);
            trim.Lathe(new[] { -.215f, -.205f, -.19f, -.18f }, new[] { .019f, .035f, .035f, .02f }, 8);
            gold = trim.BuildSmooth(prefix + "Gold", GoldTiling, 50f);
        }

        // Wrapped leather grip, gold grip collars and one gold nock cap (pointing +Y at the origin) for the archer's bow.
        public static void BowFittings(string prefix, out Mesh grip, out Mesh gold, out Mesh tip)
        {
            grip = WrappedGrip(prefix + "Grip", -.075f, .075f, .0215f, .003f, .026f);
            var trim = new FacetedMesh();
            trim.Lathe(new[] { .075f, .082f, .1f, .108f }, new[] { .022f, .03f, .03f, .022f }, 8);
            trim.Lathe(new[] { -.108f, -.1f, -.082f, -.075f }, new[] { .022f, .03f, .03f, .022f }, 8);
            gold = trim.BuildSmooth(prefix + "Gold", GoldTiling, 50f);
            var cap = new FacetedMesh();
            cap.Lathe(new[] { -.012f, 0f, .03f, .045f }, new[] { .012f, .02f, .017f, 0f }, 8);
            tip = cap.Build(prefix + "Tip", IronTiling);
        }

        // ---------- Caster staffs ----------

        // Mage staff: gnarled wood shaft, gold ferrule and three claws holding a floating rune crystal.
        public static void MageStaff(string prefix, float buttY, float headY, out Mesh wood, out Mesh grip, out Mesh gold, out Mesh crystal)
        {
            wood = Shaft(new[] { buttY + .05f, -.12f, .1f, .3f, .5f, .66f, .72f },
                new[] { .017f, .02f, .022f, .019f, .025f, .02f, .028f }, 8).Build(prefix + "Shaft", WoodTiling);
            grip = WrappedGrip(prefix + "Grip", -.1f, .095f, .0235f, .003f, .03f);

            var trim = new FacetedMesh();
            trim.Lathe(new[] { buttY - .02f, buttY + .01f, buttY + .07f, buttY + .1f }, new[] { 0f, .026f, .027f, .022f }, 8);
            trim.Lathe(new[] { .66f, .674f, .72f, .735f }, new[] { .03f, .042f, .036f, .026f }, 8);
            for (int i = 0; i < 3; i++)
            {
                var claw = new FacetedMesh();
                Vector3[] path =
                {
                    new Vector3(.02f, .725f, 0f), new Vector3(.062f, .775f, 0f), new Vector3(.082f, .85f, 0f),
                    new Vector3(.07f, .935f, 0f), new Vector3(.032f, .99f, 0f),
                };
                for (int k = 0; k < path.Length - 1; k++) claw.Append(Rod(path[k], path[k + 1], .013f - k * .002f, .011f - k * .002f));
                claw.Pyramid(path[path.Length - 1], new Vector3(-.5f, .9f, 0f), .007f, .035f);
                claw.Transform(Quaternion.Euler(0f, i * 120f, 0f), Vector3.zero);
                trim.Append(claw);
            }
            var ring = new FacetedMesh();
            ring.Sweep(RingProfile(.1f, .114f, .006f), 28, Vector3.zero);
            ring.Transform(Quaternion.Euler(78f, 0f, 14f), new Vector3(0f, headY - .1f, 0f));
            trim.Append(ring);
            gold = trim.BuildSmooth(prefix + "Gold", GoldTiling, 50f);

            var gem = new FacetedMesh();
            float mid = (headY + .78f) * .5f, tall = headY - .78f;
            gem.Lathe(new[] { mid - tall * .5f, mid - tall * .24f, mid, mid + tall * .22f, mid + tall * .5f }, new[] { 0f, .052f, .068f, .05f, 0f }, 6);
            crystal = gem.Build(prefix + "Crystal", 1f);
        }

        // Priest staff: a pale wood shepherd's crook with a holy lantern hanging from the hook. The silhouette is
        // deliberately nothing like the hero's star-crowned staff: asymmetric, hooked, and lit from a hanging flame.
        public static void PriestStaff(string prefix, float buttY, float headY, out Mesh ivory, out Mesh grip, out Mesh gold, out Mesh orb)
        {
            const float hookRadius = .15f, top = .9f, lanternY = .43f;
            var hook = new Vector3(hookRadius, top, 0f);
            var wood = new FacetedMesh();
            wood.Lathe(new[] { buttY + .05f, -.12f, .1f, .5f, top }, new[] { .019f, .023f, .023f, .023f, .024f }, 12);
            var path = new List<Vector3>();
            for (int i = 0; i <= 10; i++)
            {
                float a = Mathf.Deg2Rad * (180f - i * 22f);
                path.Add(hook + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * hookRadius);
            }
            for (int i = 0; i < path.Count - 1; i++)
                wood.Append(Rod(path[i], path[i + 1], Mathf.Lerp(.024f, .017f, i / 10f), Mathf.Lerp(.024f, .017f, (i + 1) / 10f), 10));
            ivory = wood.BuildSmooth(prefix + "Shaft", 2f, 60f);
            grip = WrappedGrip(prefix + "Grip", -.1f, .095f, .0215f, .003f, .03f);

            var trim = new FacetedMesh();
            foreach (float y in new[] { buttY + .06f, -.13f, .13f, .42f })
                trim.Lathe(new[] { y - .014f, y - .008f, y + .008f, y + .014f }, new[] { .02f, .028f, .028f, .02f }, 10);
            trim.Lathe(new[] { buttY - .02f, buttY + .01f, buttY + .04f }, new[] { 0f, .026f, .02f }, 8);
            trim.Lathe(new[] { top - .06f, top - .045f, top - .02f, top - .005f }, new[] { .02f, .033f, .033f, .02f }, 10);
            // Hook finial and the leaf-shaped wings that spring from the collar.
            Vector3 end = path[path.Count - 1];
            var finial = new FacetedMesh();
            finial.Lathe(new[] { -.012f, 0f, .022f, .04f }, new[] { .014f, .02f, .016f, 0f }, 8);
            finial.Transform(Quaternion.FromToRotation(Vector3.up, (path[path.Count - 1] - path[path.Count - 2]).normalized), end);
            trim.Append(finial);
            foreach (int side in new[] { -1, 1 })
            {
                var wing = new FacetedMesh();
                for (int k = 0; k < 3; k++)
                    wing.Pyramid(new Vector3(side * .03f, top - .04f + k * .016f, 0f), new Vector3(side * (1f + k * .45f), -.35f + k * .3f, 0f), .008f - k * .001f, .12f - k * .025f);
                trim.Append(wing);
            }

            // Lantern: hexagonal cage of gold posts around a glowing flame, hung by a two-link chain from the finial.
            float lx = end.x;
            var lantern = new FacetedMesh();
            lantern.Lathe(new[] { -.15f, -.135f, -.11f, -.1f, -.092f }, new[] { 0f, .02f, .045f, .06f, .058f }, 6);
            lantern.Lathe(new[] { .092f, .1f, .112f, .14f, .17f, .19f }, new[] { .058f, .066f, .052f, .032f, .013f, 0f }, 6);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f, b = (i + 1) * Mathf.PI / 3f;
                var pa = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * .054f;
                var pb = new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * .054f;
                lantern.Append(Rod(pa + Vector3.down * .092f, pa + Vector3.up * .092f, .0065f, .0065f, 6));
                lantern.Append(Rod(pa, pb, .005f, .005f, 6));
                lantern.Pyramid(pa + Vector3.up * .1f, new Vector3(pa.x, .9f, pa.z), .007f, .03f);
            }
            lantern.Sweep(RingProfile(.012f, .022f, .005f), 14, new Vector3(0f, .205f, 0f));
            lantern.Transform(Quaternion.identity, new Vector3(lx, lanternY, 0f));
            lantern.Scale(1.45f, new Vector3(lx, lanternY, 0f));
            trim.Append(lantern);
            for (int k = 0; k < 2; k++)
            {
                var link = new FacetedMesh();
                link.Sweep(RingProfile(.007f, .013f, .0035f), 10, Vector3.zero);
                link.Transform(Quaternion.Euler(0f, k * 90f, 0f), new Vector3(lx, lanternY + .322f + k * .02f, 0f));
                trim.Append(link);
            }
            gold = trim.BuildSmooth(prefix + "Gold", GoldTiling, 50f);

            var flame = new FacetedMesh();
            flame.Lathe(new[] { -.075f, -.04f, 0f, .04f, .075f, .105f }, new[] { 0f, .036f, .046f, .034f, .016f, 0f }, 10);
            flame.Transform(Quaternion.identity, new Vector3(lx, lanternY, 0f));
            flame.Scale(1.45f, new Vector3(lx, lanternY, 0f));
            orb = flame.Build(prefix + "Orb", 1f);
        }
    }
}
