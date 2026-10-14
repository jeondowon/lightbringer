using System.Collections.Generic;
using Lightbringer.Pathing;
using UnityEngine;
using UnityEngine.Rendering;
using Shapes = Lightbringer.Visuals.PropShapes.Shapes;
using Shape = Lightbringer.Visuals.PropShapes.Shape;
using Surface = Lightbringer.Visuals.PropShapes.Surface;

namespace Lightbringer.Visuals
{
    // Act 3, the Demon King's domain (CampaignAtlas_Act3_v1/v2): the same basin fully withered. Skeletal black woods,
    // ash-grey ground, narrow dark fissures that never cut a Path, black mist in the valleys, outer strongholds and the
    // final gothic citadel. Ordinary earth and stone: no lava, ice, snow, purple corruption or chasms.
    public sealed partial class ActTwoScenery
    {
        private void DarkenForActThree()
        {
            ArtStyleLibrary s = palette;
            bool finale = Stage == 24;
            s.grassLight = new Color(.30f, .29f, .26f);
            s.grassDark = new Color(.19f, .19f, .18f);
            s.dirt = new Color(.32f, .29f, .26f);
            s.plazaStone = new Color(.36f, .36f, .36f);
            s.rock = new Color(.25f, .26f, .28f);
            s.ruinStone = new Color(.37f, .37f, .37f);
            s.bark = new Color(.10f, .09f, .09f);
            s.foliage = new Color(.22f, .26f, .20f);
            s.foliageDark = new Color(.14f, .16f, .13f);
            s.corruptStone = new Color(.13f, .12f, .14f);
            s.sunColor = new Color(.78f, .77f, .82f);
            s.sunIntensity = finale ? .75f : .9f;
            s.ambientSky = new Color(.30f, .31f, .38f);
            s.ambientEquator = new Color(.26f, .26f, .29f);
            s.ambientGround = new Color(.13f, .13f, .14f);
            s.fogColor = finale ? new Color(.19f, .18f, .22f) : new Color(.25f, .26f, .30f);
            s.fogStart = 60;
            // The finale keeps the sky readable far enough to see the Demon King over the citadel.
            s.fogEnd = finale ? 230 : 190;
            s.cloudShadowStrength = .58f;
            if (s.terrainMaterial != null)
            {
                s.terrainMaterial.SetFloat("_Weathering", .62f);
                s.terrainMaterial.SetColor("_CorruptSoil", s.corruptStone);
            }
            if (s.propsMaterial != null) s.propsMaterial.SetColor("_ShadowColor", new Color(.50f, .52f, .56f));
            if (s.skyMaterial != null)
            {
                s.skyMaterial.SetColor("_ZenithColor", finale ? new Color(.07f, .07f, .10f) : new Color(.11f, .12f, .16f));
                s.skyMaterial.SetColor("_HorizonColor", s.fogColor);
                s.skyMaterial.SetFloat("_SunStrength", .03f);
            }
        }

        private void BuildActThreeLandmarks(WaypointPath[] paths)
        {
            switch (Stage)
            {
                case 17:
                    Landmark("Abandoned Village House", new Vector2(-22, 16), 5, RuinedHouse);
                    Landmark("Burned Village Hall", new Vector2(23, 40), 5, RuinedHouse);
                    Landmark("Withered Boundary Wall", new Vector2(-24, 50), 4, BrokenWall);
                    break;
                case 18:
                    RiverCrossing(paths);
                    Landmark("Bridgehead Watchtower", new Vector2(-11, 8), 5, Watchtower);
                    Landmark("Ruined Ferry House", new Vector2(11, 56), 5, RuinedHouse);
                    break;
                case 19:
                    Landmark("Shattered Hill Sanctuary", new Vector2(-11, 26), 6, Sanctuary);
                    Landmark("Broken Boundary Wall", new Vector2(12, 30), 4, BrokenWall);
                    Landmark("Toppled Hero Monument", new Vector2(12, 54), 4, Monument);
                    break;
                case 20:
                    Landmark("Lost Woodcutter Hut", new Vector2(-10, 30), 4, RuinedHouse);
                    Landmark("Dead Wood Watchtower", new Vector2(24, 50), 5, Watchtower);
                    break;
                case 21:
                    Landmark("Outer Field Encampment", new Vector2(-11, 30), 5, DarkOutpost);
                    Landmark("Fissure Watch Camp", new Vector2(12, 50), 5, DarkOutpost);
                    break;
                case 22:
                    RockSpires();
                    Landmark("Canyon Stronghold", new Vector2(-11, 24), 5, DarkOutpost);
                    Landmark("Canyon Watchtower", new Vector2(12, 44), 5, Watchtower);
                    break;
                case 23:
                    Landmark("Outer Gate West Tower", new Vector2(-8, 56), 4, GateTower);
                    Landmark("Outer Gate East Tower", new Vector2(8, 56), 4, GateTower);
                    Landmark("Muster Ground Camp", new Vector2(-24, 30), 5, DarkOutpost);
                    GothicCitadel(150, .75f);
                    break;
                case 24:
                    Landmark("Fallen Siege Wall West", new Vector2(-12, 30), 4, BrokenWall);
                    Landmark("Fallen Siege Wall East", new Vector2(12, 30), 4, BrokenWall);
                    Landmark("Citadel Western Outwork", new Vector2(-27, 56), 5, Watchtower);
                    Landmark("Citadel Eastern Outwork", new Vector2(27, 56), 5, Watchtower);
                    GothicCitadel(104, 1);
                    break;
            }
        }

        private void RuinedHouse(Shapes shapes, Vector3 p)
        {
            // Roofless stone walls with a doorway gap and a fallen beam; the village is long abandoned.
            Block(shapes, p + Vector3.up * .05f, new Vector3(6, .1f, 5));
            Block(shapes, p + new Vector3(0, 1.4f, 2.3f), new Vector3(6, 2.8f, .5f));
            Block(shapes, p + new Vector3(-2.75f, 1.1f, 0), new Vector3(.5f, 2.2f, 4.6f));
            Block(shapes, p + new Vector3(2.75f, .7f, .6f), new Vector3(.5f, 1.4f, 3.4f));
            Block(shapes, p + new Vector3(-1.8f, .9f, -2.3f), new Vector3(2.4f, 1.8f, .5f));
            Block(shapes, p + new Vector3(2.1f, .45f, -2.3f), new Vector3(1.8f, .9f, .5f));
            // A gable stub on the back wall.
            shapes.Cone(0, palette.ruinStone, p + new Vector3(-1.2f, 2.8f, 2.3f), new Vector3(2.6f, 1.6f, .5f), segments: 4);
            shapes.Pattern = Surface.Planks;
            Block(shapes, p + new Vector3(.4f, 1.1f, 0), new Vector3(.3f, .3f, 5.2f), palette.bark * 1.4f, new Vector3(22, 15, 0));
            shapes.Pattern = Surface.Masonry;
            Rubble(shapes, p, 3.6f, 14);
        }

        private void GateTower(Shapes shapes, Vector3 p)
        {
            Color stone = palette.rock * 1.15f;
            Block(shapes, p + Vector3.up * 4.5f, new Vector3(3.6f, 9, 3.6f), stone);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Block(shapes, p + new Vector3(x * 1.35f, 9.5f, z * 1.35f), new Vector3(.9f, 1, .9f), stone);
            // The outer gate's arch is broken: only a jut of the lintel remains on each tower.
            Block(shapes, p + new Vector3(0, 6.8f, -.2f), new Vector3(5.6f, 1.1f, 2.4f), stone);
            Rubble(shapes, p, 3.4f, 16);
        }

        private void DarkOutpost(Shapes shapes, Vector3 p)
        {
            // An outer strongpoint of the citadel: charred palisade, a squat stone keep and the enemy's own markers.
            Palisade(shapes, p);
            shapes.Pattern = Surface.Masonry;
            Block(shapes, p + new Vector3(0, 2.2f, .6f), new Vector3(3.4f, 4.4f, 3), palette.rock * 1.1f);
            for (int x = -1; x <= 1; x++)
                Block(shapes, p + new Vector3(x * 1.2f, 4.75f, -.8f), new Vector3(.7f, .7f, .4f), palette.rock * 1.1f);
            if (!EnemyProp(p + new Vector3(-2.6f, 0, -2.2f), BattlefieldPropKind.EnemyBoneFence, 2.4f))
                Block(shapes, p + new Vector3(-2.6f, .5f, -2.2f), new Vector3(2, 1, .3f), palette.bone);
            if (!EnemyProp(p + new Vector3(2.6f, 0, -2), BattlefieldPropKind.EnemyAltar, 1.6f))
                Block(shapes, p + new Vector3(2.6f, .4f, -2), new Vector3(1.4f, .8f, 1), palette.corruptStone);
            Rubble(shapes, p, 3.2f, 8);
        }

        private bool EnemyProp(Vector3 position, BattlefieldPropKind kind, float width)
        {
            BattlefieldPropAsset asset = palette.battlefieldProps != null ? palette.battlefieldProps.Find(kind) : null;
            if (asset == null || asset.Radius <= 0) return false;
            GameObject item = Instantiate(asset.prefab, terrain.transform, false);
            item.name = "Outpost " + kind;
            item.transform.localPosition = Ground(position.x, position.z);
            item.transform.localRotation = Quaternion.Euler(0, Stage * 23 % 50 - 25, 0);
            item.transform.localScale = Vector3.one * Mathf.Min(1.5f, width * .5f / asset.Radius);
            return true;
        }

        // Weathered charcoal crags flank the canyon outside the playable field; never a wall across it.
        private void RockSpires()
        {
            Shapes rocks = new Shapes();
            for (int i = 0; i < 26; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                float x = side * Range(40, 62), z = Range(-20, 110);
                if (!terrain.ClearOfPlay(new Vector2(x, z), 8, 14)) continue;
                rocks.Piece(ProceduralMeshes.Faceted(i % 6), palette.rock * Range(.8f, 1.1f), Ground(x, z) + Vector3.up * 2,
                    new Vector3(Range(6, 11), Range(9, 18), Range(6, 10)), new Vector3(Range(-8, 8), Range(0, 360), Range(-6, 6)));
            }
            MeshGroup("Charcoal Canyon Crags", rocks);
            Landmarks++;
        }

        // The final citadel on its plateau beyond the enemy gate: layered curtain walls, round towers with spires and
        // a high central keep (Act 3 atlas, upper right). `scale` < 1 shows it far off from the last approach.
        private void GothicCitadel(float z, float scale)
        {
            Shapes fortress = new Shapes { Pattern = Surface.Masonry };
            Color dark = palette.rock * .75f, roof = palette.corruptStone * 1.3f;
            Vector3 Foot(float x, float depth) => Ground(x * scale, z + depth * scale);
            Vector3 S(float x, float y, float w) => new Vector3(x, y, w) * scale;
            // Rock plateau under the walls.
            fortress.Piece(ProceduralMeshes.Faceted(2), palette.rock * .8f, Foot(0, 8) + Vector3.up * 1.5f * scale, S(70, 6, 26));
            Block(fortress, Foot(0, 0) + Vector3.up * 7 * scale, S(62, 14, 4), dark);
            for (int x = -30; x <= 30; x += 4)
                Block(fortress, Foot(x, -1.6f) + Vector3.up * 14.6f * scale, S(1.6f, 1.4f, 1), dark);
            foreach (float x in new[] { -31f, -18f, 18f, 31f })
            {
                float height = Mathf.Abs(x) > 25 ? 20 : 25;
                fortress.Cone(.85f, dark, Foot(x, 1), S(7, height, 7), segments: 10);
                fortress.Cone(0, roof, Foot(x, 1) + Vector3.up * height * scale, S(6.4f, 9, 6.4f), segments: 10);
            }
            // Gatehouse with its arch, then the inner ward and keep rising behind.
            for (int side = -1; side <= 1; side += 2)
                Block(fortress, Foot(side * 5, -1.5f) + Vector3.up * 8 * scale, S(4.5f, 16, 5), dark);
            Block(fortress, Foot(0, -1.5f) + Vector3.up * 13.5f * scale, S(6, 3, 4.5f), dark);
            Block(fortress, Foot(0, 12) + Vector3.up * 11 * scale, S(40, 22, 4), dark);
            Block(fortress, Foot(0, 20) + Vector3.up * 17 * scale, S(18, 34, 14), dark);
            fortress.Cone(.55f, dark, Foot(0, 20) + Vector3.up * 34 * scale, S(9, 12, 9), segments: 10);
            fortress.Cone(0, roof, Foot(0, 20) + Vector3.up * 46 * scale, S(6, 20, 6), segments: 10);
            for (int side = -1; side <= 1; side += 2)
            {
                fortress.Cone(.8f, dark, Foot(side * 8, 15) + Vector3.up * 34 * scale, S(3.4f, 7, 3.4f), segments: 8);
                fortress.Cone(0, roof, Foot(side * 8, 15) + Vector3.up * 41 * scale, S(3.2f, 10, 3.2f), segments: 8);
                fortress.Cone(.85f, dark, Foot(side * 15, 13) + Vector3.up * 22 * scale, S(5, 14, 5), segments: 10);
                fortress.Cone(0, roof, Foot(side * 15, 13) + Vector3.up * 36 * scale, S(4.6f, 11, 4.6f), segments: 10);
            }
            MeshGroup(scale < 1 ? "Distant Final Citadel" : "Final Citadel of the Demon King", fortress);
            Landmarks++;
        }

        // Narrow dark cracks in the ash-grey ground: flat strips with no colliders, kept off every Path.
        private void BuildFissures()
        {
            Shapes cracks = new Shapes();
            int count = Stage == 21 ? 52 : 28;
            // Crowded layouts can reject most candidates. Retry only when fewer than six
            // cracks survived, keeping the same clearance rules and a bounded build cost.
            int maxAttempts = count * 4;
            Color deep = new Color(.05f, .045f, .05f);
            for (int i = 0; i < maxAttempts && (i < count || Fissures < 6); i++)
            {
                float x = Range(-33, 33), z = Range(-6, 70);
                float yaw = Range(0, 360);
                bool laid = false;
                for (int step = 0; step < 6; step++)
                {
                    float length = Range(1.6f, 3.2f);
                    Vector2 point = new Vector2(x, z);
                    if (!terrain.ClearForProp(point, 1.3f)) break;
                    float width = Range(.18f, .42f) * (1 - step * .12f);
                    Vector3 forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                    cracks.Add(Shape.Cube, deep, Ground(x, z) + forward * length * .5f + Vector3.up * .02f,
                        new Vector3(width, .03f, length), new Vector3(0, yaw, 0));
                    x += forward.x * length; z += forward.z * length;
                    yaw += Range(-38, 38);
                    laid = true;
                }
                if (laid) Fissures++;
                // Scorched ash patches between the cracks.
                if (i % 3 == 0 && terrain.ClearForProp(new Vector2(x, z), 2.6f))
                    cracks.Piece(ProceduralMeshes.Faceted(i % 6), new Color(.12f, .12f, .12f), Ground(x, z) + Vector3.up * .018f,
                        new Vector3(Range(2, 4), .025f, Range(2, 5)), new Vector3(0, Range(0, 360), 0));
            }
            MeshGroup("Dark Fissures and Ash", cracks, false);
        }

        // Black mist lying in the valleys outside the playable field and creeping toward the citadel.
        private void BuildValleyDarkness()
        {
            if (palette.vfxMaterial == null) return;
            Material mist = Copy(palette.vfxMaterial);
            mist.SetFloat("_Shape", 6);
            mist.SetFloat("_Additive", 0);
            mist.SetFloat("_Softness", 1.2f);
            mist.SetColor("_Color", new Color(.07f, .065f, .085f, 1));
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var uv = new List<Vector2>();
            var indices = new List<int>();
            for (int i = 0; i < 26; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                Vector3 centre = i < 22 ? new Vector3(side * Range(38, 52), Range(.6f, 1.6f), Range(-15, 100))
                    : new Vector3(side * Range(8, 28), Range(1, 2.5f), Range(86, 96));
                Vector3 right = Quaternion.Euler(0, Range(-25, 25), 0) * Vector3.forward * Range(8, 14);
                Vector3 across = Vector3.Cross(right.normalized, Vector3.up) * Range(4, 7);
                for (int plane = 0; plane < 2; plane++)
                {
                    Vector3 up = plane == 0 ? across : Vector3.up * Range(1.2f, 2.4f);
                    int start = vertices.Count;
                    vertices.Add(centre - right - up); vertices.Add(centre + right - up);
                    vertices.Add(centre + right + up); vertices.Add(centre - right + up);
                    uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
                    for (int c = 0; c < 4; c++) colors.Add(new Color(1, 1, 1, plane == 0 ? .5f : .32f));
                    indices.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                }
            }
            Mesh mesh = new Mesh { name = "Black Valley Mist", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            owned.Add(mesh);
            GameObject item = new GameObject("Black Valley Mist");
            item.transform.SetParent(terrain.transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mist;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
