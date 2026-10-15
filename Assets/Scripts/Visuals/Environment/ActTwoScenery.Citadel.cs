using UnityEngine;

namespace Lightbringer.Visuals
{
    // The Demon King's citadel in photographic stone and slate (FinalBattle.md; DemonKing_SkyReveal_Reference_v2): a
    // gothic fortress-cathedral on a rock crag behind the enemy gate. Tiered curtain walls with buttresses, corbelled
    // battlements and arrow slits, round towers under slate spires, a gatehouse with a scanned iron-bound door and
    // statues, arcaded viaducts into the flanking hills, and a cathedral keep whose crossing spire leads the eye up to
    // the Demon King's storm bank. Pure dressing: no colliders, outside the playable field.
    public sealed partial class ActTwoScenery
    {
        // The sandy scanned rock is darkened to the charcoal crag of the Act 3 reference.
        private const float CragShade = .38f;
        private TexturedShapes citadel;
        private float citadelZ, citadelScale, citadelBase;

        private Vector3 CW(float x, float y, float d) => new Vector3(x * citadelScale, citadelBase + y * citadelScale, citadelZ + d * citadelScale);

        // Box by its bottom-centre (x, y, d) and size, all in citadel metres.
        private void CBlock(int layer, float x, float y, float d, float w, float h, float depth, float yaw = 0, float pitch = 0, float roll = 0)
        {
            citadel.Layer = layer;
            citadel.Box(CW(x, y + h * .5f, d), new Vector3(w, h, depth) * citadelScale, new Vector3(pitch, yaw, roll));
        }

        private void CShaft(int layer, float x, float y, float d, float radius, float height, int segments, float top = 1,
            bool faceted = false, float yaw = 0)
        {
            citadel.Layer = layer;
            citadel.Column(CW(x, y, d), radius * citadelScale, height * citadelScale, segments, top, new Vector3(0, yaw, 0), faceted);
        }

        private void PhotoCitadel(float z, float scale, string label)
        {
            citadel = new TexturedShapes(LayerCount);
            citadelZ = z;
            citadelScale = scale;
            // Masonry starts at the lowest ground under the footprint so no wall ever floats over a dip.
            citadelBase = float.MaxValue;
            for (float x = -44; x <= 44; x += 4)
                for (float d = -10; d <= 40; d += 4)
                    citadelBase = Mathf.Min(citadelBase, terrain.SurfaceHeight(x * scale, z + d * scale));
            citadelBase -= .5f;

            const float crag = 5, ward = 12;
            // Outer ward floor on the crag, then the higher terrace carrying the inner ward and the cathedral.
            CBlock(GrimeLayer, 0, -4, 6, 66, crag + 4, 14);
            CBlock(GrimeLayer, 0, crag - 8, 23, 60, ward - crag + 8, 34);
            // Outer curtain with its four round towers and the central gatehouse.
            Curtain(-29, -17, 0, crag, 13, 3.4f);
            Curtain(-15, -7.2f, 0, crag, 13, 3.4f);
            Curtain(7.2f, 15, 0, crag, 13, 3.4f);
            Curtain(17, 29, 0, crag, 13, 3.4f);
            RoundTower(-31, .5f, crag, 4.4f, 22, 11, true);
            RoundTower(31, .5f, crag, 4.4f, 22, 11, false);
            RoundTower(-16, 0, crag, 3.4f, 18, 9, false);
            RoundTower(16, 0, crag, 3.4f, 18, 9, true);
            Gatehouse(crag);
            for (int side = -1; side <= 1; side += 2) Viaduct(side, crag);

            // Inner ward.
            CBlock(StoneLayer, 0, ward - 2.2f, 6.1f, 60.6f, .35f, .5f);
            Curtain(-21, -12.8f, 8, ward, 14, 2.8f);
            Curtain(-9.4f, 9.4f, 8, ward, 14, 2.8f);
            Curtain(12.8f, 21, 8, ward, 14, 2.8f);
            RoundTower(-24, 8.5f, ward, 3.4f, 26, 12, false);
            RoundTower(24, 8.5f, ward, 3.4f, 26, 12, true);
            RoundTower(-11, 8, ward, 2.4f, 21, 9, true);
            RoundTower(11, 8, ward, 2.4f, 21, 9, false);

            Cathedral(ward);
            // Slender rear turrets fill the silhouette between the spires, as in the reference.
            RoundTower(-15, 26, ward, 2.1f, 38, 11, true);
            RoundTower(15, 26, ward, 2.1f, 38, 11, false);
            RoundTower(-26, 20, ward - 3, 2.8f, 27, 10, false);
            RoundTower(26, 20, ward - 3, 2.8f, 27, 10, true);

            TexturedShapes built = citadel;
            citadel = null;
            StructureGroup(label, built);
            CitadelScans(crag);
            Landmarks++;
        }

        private void Curtain(float x0, float x1, float d, float floor, float height, float thick)
        {
            float length = x1 - x0, centre = (x0 + x1) * .5f, walk = floor + height - 1.3f, front = d - thick * .5f;
            CBlock(StoneLayer, centre, floor - 8, d, length, walk - floor + 8, thick);
            // Battered plinth, string course, corbels under a projecting parapet, then the merlons.
            CBlock(GrimeLayer, centre, floor - 8, d - .2f, length, 10, thick + 1.4f);
            CBlock(StoneLayer, centre, floor + 6.5f, front - .08f, length, .32f, .45f);
            for (float x = x0 + .55f; x < x1 - .4f; x += 1.3f)
                CBlock(StoneLayer, x, walk - .9f, front - .3f, .42f, .9f, .6f);
            CBlock(StoneLayer, centre, walk, front - .32f, length, 1.25f, .7f);
            for (float x = x0 + .9f; x < x1 - .6f; x += 2.1f)
                CBlock(StoneLayer, x, walk + 1.25f, front - .32f, 1.15f, 1.35f, .7f);
            // Rear parapet so the wall walk reads from the higher camera angles.
            CBlock(StoneLayer, centre, walk, d + thick * .5f - .3f, length, 1.1f, .6f);
            // Buttresses with sloped caps, arrow slits between them.
            for (float x = x0 + 2.6f; x < x1 - 1.8f; x += 5.2f)
            {
                float top = floor + height * .58f;
                CBlock(StoneLayer, x, floor - 8, front - .55f, 1.5f, top - floor + 8, 1.3f);
                CShaft(StoneLayer, x, top, front - .55f, 1.05f, 1.3f, 4, 0, true, 45);
                for (int row = 0; row < 2; row++)
                    CBlock(GapLayer, x + 2.6f > x1 - 1 ? x - 2.6f : x + 2.6f, floor + 3.5f + row * 4.2f, front - .03f, .3f, 1.7f, .1f);
            }
        }

        private void RoundTower(float x, float d, float floor, float radius, float height, float roof, bool lit)
        {
            float top = floor + height;
            CShaft(GrimeLayer, x, floor - 8, d, radius * 1.18f, 11, 20, .86f);
            CShaft(StoneLayer, x, floor - 8, d, radius, height + 8, 20, .97f);
            float rim = radius * .97f;
            // Ring of corbels carrying the overhanging parapet, then merlons around it.
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2 / 16;
                CBlock(StoneLayer, x + Mathf.Cos(a) * (rim + .2f), top - 2.6f, d + Mathf.Sin(a) * (rim + .2f), .42f, .9f, .5f, 90 - a * Mathf.Rad2Deg);
            }
            CShaft(StoneLayer, x, top - 1.7f, d, rim + .55f, 1.7f, 20);
            for (int i = 0; i < 12; i++)
            {
                float a = (i + .5f) * Mathf.PI * 2 / 12;
                CBlock(StoneLayer, x + Mathf.Cos(a) * (rim + .3f), top, d + Mathf.Sin(a) * (rim + .3f), 1.05f, 1.25f, .5f, 90 - a * Mathf.Rad2Deg);
            }
            CShaft(RoofLayer, x, top - .4f, d, rim + .2f, roof, 20, 0);
            CShaft(GapLayer, x, top - .5f + roof, d, .09f, 2.6f, 6);
            // Lancet windows facing the battlefield; the highest one of a lit tower glows with firelight.
            for (int row = 0; row < 3; row++)
            {
                float y = floor + 3 + row * (height - 6) / 3f;
                float r = radius * (1 - .03f * (y - floor + 8) / (height + 8));
                CBlock(lit && row == 2 ? GlowLayer : GapLayer, x, y, d - r + .06f, .5f, 1.9f, .24f);
            }
        }

        private void SquareTower(float x, float d, float floor, float width, float height, float spire, bool lit)
        {
            float top = floor + height, half = width * .5f;
            CBlock(StoneLayer, x, floor - 8, d, width, height + 8, width);
            for (float y = floor + 7; y < top - 4; y += 7) CBlock(StoneLayer, x, y, d, width + .3f, .3f, width + .3f);
            CBlock(StoneLayer, x, top - 1.3f, d, width + 1, 1.3f, width + 1);
            for (int side = 0; side < 4; side++)
            {
                float yaw = side * 90;
                Quaternion turn = Quaternion.Euler(0, yaw, 0);
                for (float u = -half + .2f; u <= half - .1f; u += 1.8f)
                {
                    Vector3 o = turn * new Vector3(u, 0, -half - .25f);
                    CBlock(StoneLayer, x + o.x, top, d + o.z, .95f, 1.2f, .5f, yaw);
                }
            }
            // Corner pinnacles around an octagonal slate spire.
            for (int cx = -1; cx <= 1; cx += 2)
                for (int cz = -1; cz <= 1; cz += 2)
                {
                    CShaft(StoneLayer, x + cx * half, top, d + cz * half, .5f, 1.6f, 8, 1, true);
                    CShaft(RoofLayer, x + cx * half, top + 1.6f, d + cz * half, .55f, 4, 8, 0, true);
                }
            CShaft(RoofLayer, x, top - .3f, d, half * 1.02f, spire, 8, 0, true, 22.5f);
            CShaft(GapLayer, x, top - .4f + spire, d, .09f, 2.8f, 6);
            for (float y = floor + 4; y < top - 5; y += 7)
                for (int w = -1; w <= 1; w += 2)
                    CBlock(lit && y + 7 >= top - 5 ? GlowLayer : GapLayer, x + w * width * .2f, y, d - half - .05f, .6f, 2.3f, .2f);
        }

        private void Gatehouse(float floor)
        {
            SquareTower(-5.2f, -1.5f, floor, 4.4f, 21, 9, true);
            SquareTower(5.2f, -1.5f, floor, 4.4f, 21, 9, true);
            citadel.Layer = StoneLayer;
            citadel.ArchWall(CW(0, floor, -2), 6 * citadelScale, 14 * citadelScale, 5 * citadelScale, 4.6f * citadelScale,
                5.8f * citadelScale, true);
            // The dark passage behind the door shows through the pointed head of the arch; its paved floor meets the forecourt.
            CBlock(GapLayer, 0, floor, .3f, 4.6f, 11, .4f);
            CBlock(StoneLayer, 0, floor - 1, -2, 4.7f, 1, 5.2f);
            CBlock(StoneLayer, 0, floor + 14, -2, 6.4f, .4f, 5.4f);
            for (float x = -2.6f; x <= 2.7f; x += 1.75f) CBlock(StoneLayer, x, floor + 14.4f, -4.3f, .9f, 1.2f, .5f);
            // Forecourt terrace before the gate, with plinths for the statues.
            CBlock(GrimeLayer, 0, floor - 9, -8.5f, 17, 9, 8.2f);
            CBlock(StoneLayer, 0, floor - .05f, -12.4f, 17.4f, .9f, .5f);
            for (int side = -1; side <= 1; side += 2)
            {
                CBlock(StoneLayer, side * 6.4f, floor, -9, 2.6f, 2.4f, 2.6f);
                CBlock(StoneLayer, side * 6.4f, floor + 2.4f, -9, 2.9f, .3f, 2.9f);
            }
        }

        // Round-arched viaduct running from the outer tower into the flanking hill.
        private void Viaduct(int side, float floor)
        {
            float deck = floor + 3, bay = 6.6f;
            for (int i = 0; i < 5; i++)
            {
                float x = side * (35.3f + bay * .5f + i * bay);
                citadel.Layer = StoneLayer;
                citadel.ArchWall(CW(x, -6, 2), bay * citadelScale, (deck + 6) * citadelScale, 3.6f * citadelScale,
                    4.4f * citadelScale, (deck + 6 - 4.6f) * citadelScale, false);
                for (float u = -bay * .5f + .7f; u < bay * .5f - .4f; u += 2.2f)
                    CBlock(StoneLayer, x + u, deck, .45f, 1.05f, 1.1f, .5f);
                CBlock(StoneLayer, x, deck - .9f, .1f, bay, .3f, .3f);
                CBlock(StoneLayer, x, deck, 3.6f, bay, .9f, .45f);
            }
        }

        private void Cathedral(float floor)
        {
            const float nave = 15, length = 22, start = 16, wall = 21;
            float centre = start + length * .5f;
            CBlock(StoneLayer, 0, floor - 4, centre, nave, wall + 4, length);
            // Buttresses with pinnacles march down both flanks; tall lit lancets between them.
            for (int side = -1; side <= 1; side += 2)
                for (float d = start + 1.5f; d < start + length; d += 4)
                {
                    CBlock(StoneLayer, side * (nave * .5f + .7f), floor - 4, d, 1.4f, wall - 3 + 4, 1.5f);
                    CShaft(StoneLayer, side * (nave * .5f + .7f), floor + wall - 3, d, .5f, 5, 8, 0, true);
                    if (d + 2 < start + length)
                        CBlock(d < start + 9 ? GlowLayer : GapLayer, side * (nave * .5f + .05f), floor + 7, d + 2, .2f, 9, 1.2f);
                }
            citadel.Layer = RoofLayer;
            citadel.Gable(CW(0, floor + wall - .1f, centre), (nave + 1) * citadelScale, (length + .6f) * citadelScale,
                11 * citadelScale, 0, StoneLayer);
            // West front between the twin towers: rose window over a pointed portal and three lancets.
            float face = start - .05f;
            citadel.Layer = GapLayer;
            citadel.Column(CW(0, floor + 15, face), 2.7f * citadelScale, .16f * citadelScale, 24, 1, new Vector3(-90, 0, 0));
            citadel.Layer = GlowLayer;
            citadel.Column(CW(0, floor + 15, face - .1f), 2.15f * citadelScale, .12f * citadelScale, 24, 1, new Vector3(-90, 0, 0));
            for (int i = -1; i <= 1; i++) CBlock(i == 0 ? GlowLayer : GapLayer, i * 2.4f, floor + 8.2f, face - .05f, .8f, i == 0 ? 3.8f : 3.2f, .2f);
            citadel.Layer = StoneLayer;
            citadel.ArchWall(CW(0, floor, start - 1.2f), 7 * citadelScale, 7.5f * citadelScale, 2.4f * citadelScale,
                3.4f * citadelScale, 3.6f * citadelScale, true);
            CBlock(GapLayer, 0, floor, start - .1f, 3.4f, 7, .2f);
            SquareTower(-9.6f, start + 1.5f, floor, 5.4f, 32, 15, true);
            SquareTower(9.6f, start + 1.5f, floor, 5.4f, 32, 15, false);
            // Crossing tower, octagonal lantern and the great spire.
            float cross = start + length - 5, tower = floor + 34;
            CBlock(StoneLayer, 0, floor, cross, 9.5f, 34, 9.5f);
            for (float y = floor + 8; y < tower - 3; y += 8) CBlock(StoneLayer, 0, y, cross, 9.8f, .3f, 9.8f);
            for (int w = -1; w <= 1; w += 2) CBlock(GlowLayer, w * 2.2f, tower - 9, cross - 4.8f, .8f, 3.6f, .2f);
            CShaft(StoneLayer, 0, tower, cross, 4.6f, 7, 8, .94f, true, 22.5f);
            for (int i = 0; i < 8; i++)
            {
                float a = (i * 45 + 22.5f) * Mathf.Deg2Rad;
                CShaft(StoneLayer, Mathf.Cos(a) * 4.9f, tower, cross + Mathf.Sin(a) * 4.9f, .45f, 8, 6, .8f, true);
                CShaft(RoofLayer, Mathf.Cos(a) * 4.9f, tower + 8, cross + Mathf.Sin(a) * 4.9f, .5f, 4.5f, 8, 0, true);
                // A lancet in every face of the lantern; the ones facing the battlefield glow.
                float b = i * 45 * Mathf.Deg2Rad;
                CBlock(i >= 5 && i <= 7 ? GlowLayer : GapLayer, Mathf.Cos(b) * 4.2f, tower + 1.6f, cross + Mathf.Sin(b) * 4.2f,
                    .7f, 3.4f, .25f, 90 - i * 45);
            }
            for (int cx = -1; cx <= 1; cx += 2)
                for (int cz = -1; cz <= 1; cz += 2)
                    CShaft(RoofLayer, cx * 4.75f, tower, cross + cz * 4.75f, .6f, 6, 8, 0, true);
            CShaft(RoofLayer, 0, tower + 6.8f, cross, 4.3f, 22, 8, 0, true, 22.5f);
            CShaft(GapLayer, 0, tower + 28.6f, cross, .12f, 4, 6);
        }

        // Scanned crag rocks, the iron-bound door and gate statues (Poly Haven CC0), placed by hand.
        private void CitadelScans(float crag)
        {
            ActTwoScatterAsset[] cliffs = ScanSet(ActTwoScatterKind.Cliff);
            // Crag faces under the outer curtain; |x| >= 19 keeps them clear of the forecourt, gate and statues.
            // Each entry: x, drop below the crag top, scale, depth.
            if (cliffs.Length > 0)
                foreach (Vector4 rock in new[] { new Vector4(-19, -6.5f, 7f, -4.5f), new Vector4(19, -6, 6.6f, -4.8f),
                    new Vector4(-28, -5, 7f, -4), new Vector4(28, -5.5f, 7.5f, -4.4f), new Vector4(-37, -4.5f, 6.6f, -3.6f),
                    new Vector4(37, -5, 6.2f, -3.8f), new Vector4(-23.5f, -9, 7.9f, -8), new Vector4(23.5f, -9, 7.7f, -8.2f),
                    new Vector4(-33, -7.5f, 7.5f, -7.4f), new Vector4(33, -7.5f, 7.3f, -7.6f) })
                    CitadelScan(cliffs[0], CW(rock.x, crag + rock.y, rock.w), rock.z, rock.x * 37, CragShade);
            ActTwoScatterAsset[] boulders = ScanSet(ActTwoScatterKind.Boulder);
            if (boulders.Length > 0)
                for (int i = 0; i < 14; i++)
                {
                    float x = (i % 2 == 0 ? -1 : 1) * Range(9, 40), d = Range(-14, -8);
                    Vector3 at = new Vector3(x * citadelScale, terrain.SurfaceHeight(x * citadelScale, citadelZ + d * citadelScale) - .4f, citadelZ + d * citadelScale);
                    CitadelScan(boulders[random.Next(boulders.Length)], at, Range(1.6f, 3.2f), Range(0, 360), CragShade);
                }
            ActTwoScatterAsset[] door = ScanSet(ActTwoScatterKind.Gate);
            if (door.Length > 0) CitadelScan(door[0], CW(0, crag, -1.2f), 4.4f / 2.01f, 180);
            ActTwoScatterAsset[] statue = ScanSet(ActTwoScatterKind.Statue);
            if (statue.Length > 0)
                for (int side = -1; side <= 1; side += 2)
                    CitadelScan(statue[0], CW(side * 6.4f, crag + 2.7f, -9), 1.5f, 180 - side * 12);
        }

        private void CitadelScan(ActTwoScatterAsset asset, Vector3 position, float scale, float yaw, float shade = 1)
        {
            GameObject item = Instantiate(asset.prefab, terrain.transform, false);
            item.name = "Citadel " + asset.prefab.name;
            item.transform.localPosition = position;
            item.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            item.transform.localScale = Vector3.one * scale * citadelScale;
            if (shade < 1)
                foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = Shaded(materials[i], shade);
                    renderer.sharedMaterials = materials;
                }
            if (Glossy(asset.kind)) Matte(item);
            ScannedProps++;
        }

        private readonly System.Collections.Generic.Dictionary<Material, Material> shadedScans =
            new System.Collections.Generic.Dictionary<Material, Material>();

        // Battlefield-local darker copy of a scan material (glTFast or URP Lit), shared by every instance.
        private Material Shaded(Material source, float shade)
        {
            if (source == null) return null;
            if (shadedScans.TryGetValue(source, out Material result)) return result;
            result = Copy(source);
            foreach (string property in new[] { "baseColorFactor", "_BaseColor", "_Color" })
                if (result.HasProperty(property))
                {
                    result.SetColor(property, result.GetColor(property) * new Color(shade, shade, shade, 1));
                    break;
                }
            shadedScans[source] = result;
            return result;
        }
    }
}
