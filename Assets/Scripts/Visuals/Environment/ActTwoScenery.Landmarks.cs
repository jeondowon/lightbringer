using System;
using System.Collections.Generic;
using Lightbringer.Pathing;
using UnityEngine;
using UnityEngine.Rendering;
using Shapes = Lightbringer.Visuals.PropShapes.Shapes;
using Shape = Lightbringer.Visuals.PropShapes.Shape;
using Surface = Lightbringer.Visuals.PropShapes.Surface;

namespace Lightbringer.Visuals
{
    public sealed partial class ActTwoScenery
    {
        private void BuildLandmarks(WaypointPath[] paths)
        {
            switch (Stage)
            {
                case 9:
                    Landmark("Roofless Pilgrim Sanctuary", new Vector2(0, 12), 6, Sanctuary);
                    PilgrimageRoad(paths);
                    Landmark("Fallen Boundary Wall", new Vector2(23, 4), 4, BrokenWall);
                    break;
                case 10:
                    Landmark("Broken Hero Monument", new Vector2(0, 18), 4, Monument);
                    Landmark("Ruined Shrine Courtyard", new Vector2(-25, 42), 5, Sanctuary);
                    PilgrimageRoad(paths);
                    break;
                case 11:
                    Landmark("Collapsed Watchtower", new Vector2(22, 22), 5, Watchtower);
                    Landmark("Abandoned Watch Post", new Vector2(-23, 42), 5, Watchtower);
                    Landmark("Old Pilgrim Wall", new Vector2(22, 54), 4, BrokenWall);
                    break;
                case 12:
                    Landmark("Occupied Supply Depot", new Vector2(-12, 20), 5, Depot);
                    Landmark("Supply Road Storehouse", new Vector2(13, 32), 5, Depot);
                    break;
                case 13:
                    Landmark("Timber Storeyard", new Vector2(0, 20), 7, TimberYard);
                    Landmark("Burned Supply Hut", new Vector2(0, 49), 5, Depot);
                    break;
                case 14:
                    Landmark("Fortified Wagon Yard", new Vector2(-13, 15), 5, WagonYard);
                    Landmark("Crossroads Picket", new Vector2(13, 34), 5, Depot);
                    break;
                case 15:
                    RiverCrossing(paths);
                    Landmark("Bridge Watchtower", new Vector2(-10, 8), 5, Watchtower);
                    Landmark("Bridge Supply Guard", new Vector2(10, 8), 5, Depot);
                    break;
                case 16:
                    Landmark("Citadel Western Outwork", new Vector2(-27, 53), 5, Watchtower);
                    Landmark("Citadel Eastern Outwork", new Vector2(27, 53), 5, Watchtower);
                    Landmark("Broken Siege Wall", new Vector2(0, 36), 5, BrokenWall);
                    break;
                default:
                    BuildActThreeLandmarks(paths);
                    break;
            }
        }

        private void Landmark(string label, Vector2 desired, float radius, Action<Shapes, Vector3> draw)
        {
            // Search nearby clear ground, with the complete footprint separated from every assigned route.
            for (int i = 0; i < 81; i++)
            {
                int ring = i == 0 ? 0 : (i - 1) / 8 + 1;
                float angle = (i % 8) * Mathf.PI * .25f;
                Vector2 point = desired + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring * 2;
                if (Mathf.Abs(point.x) + radius > 34 || point.y - radius < -8 || point.y + radius > 69
                    || !terrain.ClearForProp(point, radius)) continue;
                GameObject prefab = palette.actTwoEnvironment != null
                    ? palette.actTwoEnvironment.FindLandmark(draw.Method.Name) : null;
                if (prefab != null)
                {
                    GameObject item = Instantiate(prefab, terrain.transform, false);
                    item.name = label;
                    item.transform.localPosition = Ground(point.x, point.y);
                    item.transform.localRotation = Quaternion.Euler(0, Stage * 19 % 60 - 30, 0);
                    item.transform.localScale = Vector3.one * (radius * .9f);
                    ImportedLandmarks++;
                }
                else
                {
                    Shapes shapes = new Shapes { Pattern = Surface.Masonry };
                    // Act 2 builders that have a photographic version fill `structure` instead of the toon shapes.
                    structure = UsesPhotoscans && HasStructures ? new TexturedShapes(LayerCount) : null;
                    draw(shapes, Ground(point.x, point.y));
                    if (structure != null && !structure.IsEmpty)
                    {
                        Transform built = StructureGroup(label, structure);
                        if (!shapes.IsEmpty) MeshGroup(label + " Detail", shapes).SetParent(built, true);
                    }
                    else MeshGroup(label, shapes);
                    structure = null;
                }
                terrain.ReserveProp(point, radius);
                Landmarks++;
                return;
            }
        }

        private void Block(Shapes shapes, Vector3 centre, Vector3 size, Color? color = null, Vector3 rotation = default)
            => shapes.Add(Shape.Cube, color ?? palette.ruinStone, centre, size, rotation);

        private void Rubble(Shapes shapes, Vector3 centre, float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Range(0, Mathf.PI * 2), distance = Range(.5f, radius);
                Vector3 p = centre + new Vector3(Mathf.Cos(angle) * distance, .18f, Mathf.Sin(angle) * distance);
                shapes.Piece(ProceduralMeshes.Faceted(i % 6), palette.rock, p,
                    new Vector3(Range(.35f, .9f), Range(.3f, .65f), Range(.4f, 1.1f)),
                    new Vector3(10, Range(0, 360), 15));
            }
        }

        private void Sanctuary(Shapes shapes, Vector3 p)
        {
            Block(shapes, p + Vector3.up * .055f, new Vector3(7, .1f, 8));
            for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < 3; row++)
                {
                    Vector3 foot = p + new Vector3(side * 3.1f, .1f, (row - 1) * 3.1f);
                    float height = row == 2 ? 1.25f : side == -1 && row == 0 ? 2.4f : 4.4f;
                    Block(shapes, foot + Vector3.up * .15f, new Vector3(1.2f, .3f, 1.2f));
                    shapes.Cone(.85f, palette.ruinStone, foot + Vector3.up * .3f, new Vector3(.72f, height, .72f));
                    if (height > 4) Block(shapes, foot + Vector3.up * (height + .4f), new Vector3(1.3f, .35f, 1.2f));
                }
            // Surviving lintel with an open sky above: no new roof on the ancient sanctuary.
            Block(shapes, p + new Vector3(0, 4.95f, 0), new Vector3(7.4f, .65f, 1.1f));
            shapes.Cone(1, palette.ruinStone * .85f, p + new Vector3(-1.8f, .45f, 2),
                new Vector3(.8f, 3.2f, .8f), new Vector3(0, 30, 90));
            Rubble(shapes, p, 4.2f, 16);
        }

        private void Monument(Shapes shapes, Vector3 p)
        {
            Block(shapes, p + Vector3.up * .4f, new Vector3(4, .8f, 3.5f));
            Block(shapes, p + Vector3.up * 1.25f, new Vector3(2.4f, .9f, 2.2f));
            // A weathered, broken knight silhouette; no new character model dependency.
            Color stone = palette.ruinStone;
            Block(shapes, p + new Vector3(.45f, 2.25f, 0), new Vector3(.55f, 1.1f, .7f));
            Block(shapes, p + new Vector3(-.4f, 2.25f, 0), new Vector3(.55f, 1.1f, .7f));
            shapes.Cone(.65f, stone, p + Vector3.up * 2.8f, new Vector3(1.6f, 1.4f, 1));
            shapes.Add(Shape.Ball, stone, p + Vector3.up * 4.5f, new Vector3(.8f, 1, .75f));
            Block(shapes, p + new Vector3(-1, 3.35f, 0), new Vector3(.45f, .7f, .6f));
            Block(shapes, p + new Vector3(1.4f, .45f, 1), new Vector3(.4f, .6f, 1.8f), rotation: new Vector3(0, 25, 10));
            // The broken sword and arm lie at the base of the monument.
            Block(shapes, p + new Vector3(-1.3f, .88f, .8f), new Vector3(.3f, .2f, 1.9f));
            Rubble(shapes, p, 3, 12);
        }

        private void BrokenWall(Shapes shapes, Vector3 p)
        {
            for (int i = 0; i < 5; i++)
            {
                float h = i == 2 ? .5f : 1.5f + (i % 2) * 1.5f;
                Block(shapes, p + new Vector3((i - 2) * 1.2f, h * .5f, 0), new Vector3(1.1f, h, 1));
            }
            Rubble(shapes, p, 3, 18);
        }

        private void Watchtower(Shapes shapes, Vector3 p)
        {
            Block(shapes, p + Vector3.up * .2f, new Vector3(4.2f, .4f, 4.2f));
            for (int side = -1; side <= 1; side += 2)
            {
                float h = side == -1 ? 5.6f : 3.8f;
                Block(shapes, p + new Vector3(side * 1.7f, h * .5f, 0), new Vector3(.65f, h, 3.8f));
                Block(shapes, p + new Vector3(0, 1.6f, side * 1.7f), new Vector3(3.8f, 3.2f, .65f));
            }
            shapes.Pattern = Surface.Planks;
            Block(shapes, p + new Vector3(.5f, 2, 0), new Vector3(2.8f, .2f, 3.2f), palette.bark,
                new Vector3(28, 0, -20));
            shapes.Pattern = Surface.Masonry;
            Rubble(shapes, p, 3.8f, 20);
        }

        private void Palisade(Shapes shapes, Vector3 p)
        {
            shapes.Pattern = Surface.Planks;
            for (int i = 0; i < 9; i++)
                shapes.Cone(.25f, palette.bark, p + new Vector3((i - 4) * .85f, 0, 3.3f),
                    new Vector3(.5f, 2.4f + i % 3 * .25f, .5f), segments: 6);
            Block(shapes, p + new Vector3(0, 1, 3), new Vector3(7.5f, .25f, .3f), palette.bark * 1.3f);
        }

        private void Depot(Shapes shapes, Vector3 p)
        {
            if (structure != null) { PhotoDepot(p); return; }
            Palisade(shapes, p);
            ScanGoods(p + Vector3.back * 2, 3.2f, 5);
            shapes.Pattern = Surface.Masonry;
            Block(shapes, p + new Vector3(-1.8f, 1.1f, .5f), new Vector3(.55f, 2.2f, 3));
            Block(shapes, p + new Vector3(1.8f, .55f, .5f), new Vector3(.55f, 1.1f, 3));
            shapes.Pattern = Surface.Planks;
            Block(shapes, p + new Vector3(-.6f, 2.35f, .5f), new Vector3(2.4f, .22f, 3.5f), palette.bark,
                new Vector3(0, 0, -16));
            // Photoscanned goods (ScanGoods) replace the painted allied barrels and chests in Act 2.
            for (int i = 0; i < (UsesPhotoscans ? 0 : 4); i++)
            {
                Vector3 position = p + new Vector3(-2.4f + i * 1.3f, 0, -2);
                if (!SupplyProp(position, i % 2 == 0 ? BattlefieldPropKind.AlliedBarrel : BattlefieldPropKind.AlliedChest))
                    Block(shapes, position + Vector3.up * .5f, new Vector3(1, 1, 1), palette.bark * 1.55f);
            }
            shapes.Pattern = Surface.Masonry;
            Rubble(shapes, p, 3.2f, 9);
        }

        private void TimberYard(Shapes shapes, Vector3 p)
        {
            if (structure != null) { PhotoTimberYard(p); return; }
            Palisade(shapes, p + Vector3.forward * 1.7f);
            ScanGoods(p, 4, 4);
            if (!UsesPhotoscans) SupplyProp(p + new Vector3(0, 0, -1.4f), BattlefieldPropKind.AlliedChest);
            for (int stack = -1; stack <= 1; stack += 2)
                for (int i = 0; i < 6; i++)
                {
                    Vector3 start = p + new Vector3(stack * 2.5f + (i % 3 - 1) * .65f, .38f + i / 3 * .6f, -2);
                    shapes.Cone(.95f, palette.bark * 1.3f, start, new Vector3(.6f, 5, .6f), new Vector3(90, 0, 0), segments: 8);
                }
            shapes.Pattern = Surface.Masonry;
            Rubble(shapes, p + Vector3.back * 3, 2, 8);
        }

        private bool SupplyProp(Vector3 position, BattlefieldPropKind kind)
        {
            BattlefieldPropAsset asset = palette.battlefieldProps != null ? palette.battlefieldProps.Find(kind) : null;
            if (asset == null || asset.Radius <= 0) return false;
            GameObject item = Instantiate(asset.prefab, terrain.transform, false);
            item.name = "Abandoned Supply " + kind;
            item.transform.localPosition = Ground(position.x, position.z);
            item.transform.localRotation = Quaternion.Euler(0, kind == BattlefieldPropKind.AlliedChest ? -15 : 20, 0);
            item.transform.localScale = Vector3.one * Mathf.Min(1, .58f / asset.Radius);
            return true;
        }

        private void WagonYard(Shapes shapes, Vector3 p)
        {
            if (structure != null) { PhotoWagonYard(p); return; }
            Palisade(shapes, p);
            ScanGoods(p, 4, 6);
            ActTwoScatterAsset[] pit = ScanSet(ActTwoScatterKind.FirePit);
            if (!IsActThree && pit.Length > 0) PlaceScan(pit[0], new Vector2(p.x, p.z) + new Vector2(0, -3.4f), 1.3f, 0);
            for (int wagon = -1; wagon <= 1; wagon += 2)
            {
                Vector3 c = p + new Vector3(wagon * 2, 0, -.4f);
                Block(shapes, c + Vector3.up * .9f, new Vector3(2.1f, .2f, 3), palette.bark * 1.5f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Block(shapes, c + new Vector3(side, 1.4f, 0), new Vector3(.15f, .8f, 3), palette.bark);
                    for (int wheel = -1; wheel <= 1; wheel += 2)
                        shapes.Cone(1, palette.bark * .7f, c + new Vector3(side * 1.2f, .6f, wheel),
                            new Vector3(1.15f, .22f, 1.15f), new Vector3(0, 0, 90), segments: 12);
                }
                Block(shapes, c + new Vector3(0, 1.35f, 0), new Vector3(1.2f, .8f, 1.4f), palette.bark * 1.7f);
            }
        }

        private void PilgrimageRoad(WaypointPath[] paths)
        {
            // The scanned ground shows the worn road itself; flat toon slabs would float over it.
            if (UsesPhotoscans) return;
            Shapes paving = new Shapes { Pattern = Surface.Masonry };
            foreach (WaypointPath path in paths)
                for (int segment = 0; segment < path.Count - 2; segment++)
                {
                    Vector3 a = terrain.transform.InverseTransformPoint(path.GetPosition(segment));
                    Vector3 b = terrain.transform.InverseTransformPoint(path.GetPosition(segment + 1));
                    float yaw = Quaternion.LookRotation(b - a).eulerAngles.y;
                    for (float distance = 2; distance < Vector3.Distance(a, b); distance += 3.1f)
                    {
                        if (random.NextDouble() < .22) continue;
                        Vector3 point = Vector3.Lerp(a, b, distance / Vector3.Distance(a, b));
                        Block(paving, point + Vector3.up * .025f, new Vector3(3.6f, .04f, 1.7f),
                            palette.ruinStone * Range(.7f, 1), new Vector3(0, yaw + Range(-6, 6), 0));
                    }
                }
            MeshGroup("Fragmented Paved Pilgrimage Road", paving, false);
        }

        private void RiverCrossing(WaypointPath[] paths)
        {
            Shapes bridges = new Shapes { Pattern = Surface.Masonry };
            TexturedShapes photoBridges = UsesPhotoscans && HasStructures ? new TexturedShapes(LayerCount) : null;
            var crossings = new List<Vector3>();
            foreach (WaypointPath path in paths)
                for (int i = 0; i < path.Count - 1; i++)
                {
                    Vector3 a = terrain.transform.InverseTransformPoint(path.GetPosition(i));
                    Vector3 b = terrain.transform.InverseTransformPoint(path.GetPosition(i + 1));
                    if (a.z > 40 || b.z <= 40) continue;
                    Vector3 p = Vector3.Lerp(a, b, (40 - a.z) / (b.z - a.z));
                    float yaw = Quaternion.LookRotation(b - a).eulerAngles.y;
                    if (photoBridges != null) PhotoBridge(photoBridges, p, yaw);
                    else
                    {
                        Block(bridges, p + Vector3.up * .055f, new Vector3(10, .08f, 10), rotation: new Vector3(0, yaw, 0));
                        for (int side = -1; side <= 1; side += 2)
                            Block(bridges, p + Quaternion.Euler(0, yaw, 0) * new Vector3(side * 5, .5f, 0),
                                new Vector3(.55f, 1, 8), rotation: new Vector3(0, yaw, 0));
                    }
                    crossings.Add(p);
                    Bridges++;
                }
            // Shallow river crossing: the greybox keeps the same collision floor so the hero can wade.
            float extent = RiverExtent();
            if (palette.waterMaterial != null) BuildRiverWater(extent);
            else
            {
                Shapes water = new Shapes();
                Block(water, new Vector3(0, .012f, 40), new Vector3(67, .02f, 6), IsActThree ? new Color(.18f, .29f, .33f) : MurkyWater);
                MeshGroup("Slate River Shallow Crossing", water, false);
            }
            for (float x = -extent; x <= extent; x += 3) terrain.ReserveProp(new Vector2(x, RiverCentre(x)), 4.5f);
            BuildRiverBanks(crossings, extent - 2);
            if (photoBridges != null) StructureGroup("Guarded Stone Bridges", photoBridges);
            else MeshGroup("Guarded Stone Bridges", bridges);
        }

        // Low, flat stone bridge flush with the walkable floor: paved deck, water-stained edge beams and a dry-stone
        // parapet of uneven blocks on both sides, a few of them fallen.
        private void PhotoBridge(TexturedShapes s, Vector3 p, float yaw)
        {
            Quaternion turn = Quaternion.Euler(0, yaw, 0);
            Vector3 P(float x, float y, float z) => p + turn * new Vector3(x, y, z);
            Vector3 Turn(float pitch = 0, float twist = 0, float roll = 0) => new Vector3(pitch, yaw + twist, roll);
            // Paved in courses of uneven slabs: each block takes its own patch of the stone texture, so the deck never
            // shows one tile repeating.
            for (float z = -5.1f; z < 5.05f;)
            {
                float course = Mathf.Min(Range(.7f, 1.1f), 5.1f - z);
                for (float x = -4.6f; x < 4.55f;)
                {
                    float slab = Mathf.Min(Range(.8f, 1.6f), 4.6f - x);
                    s.Layer = StoneLayer;
                    s.Box(P(x + slab * .5f, -.06f + Range(-.012f, .012f), z + course * .5f), new Vector3(slab - .03f, .32f, course - .03f),
                        Turn(Range(-.4f, .4f), 0, Range(-.4f, .4f)));
                    x += slab;
                }
                z += course;
            }
            s.Layer = GapLayer;
            s.Box(P(0, -.1f, 0), new Vector3(9.2f, .32f, 10.2f), Turn());
            s.Layer = GrimeLayer;
            for (int side = -1; side <= 1; side += 2)
            {
                s.Box(P(side * 4.85f, -.1f, 0), new Vector3(.6f, .5f, 10.6f), Turn());
                float z = -4.6f;
                while (z < 4.6f)
                {
                    float length = Mathf.Min(Range(.9f, 1.5f), 4.6f - z);
                    float height = Range(.62f, .9f);
                    // Gaps where blocks have fallen; the loose block lies in the river beside the bridge.
                    if (Mathf.Abs(z) > 1.5f && random.Next(9) == 0)
                    {
                        s.Layer = StoneLayer;
                        s.Box(P(side * Range(5.6f, 6.4f), .05f, z + length * .5f), new Vector3(.5f, height * .7f, length * .9f),
                            Turn(Range(-20, 20), Range(-40, 40), Range(-15, 15)));
                    }
                    else
                    {
                        s.Layer = StoneLayer;
                        s.Box(P(side * 4.85f, .1f + height * .5f, z + length * .5f), new Vector3(.52f, height, length - .04f),
                            Turn(0, Range(-1.5f, 1.5f), Range(-2, 2)));
                        s.Layer = GrimeLayer;
                        s.Box(P(side * 4.85f, .1f + height + .06f, z + length * .5f), new Vector3(.62f, .12f, length + .02f), Turn());
                    }
                    z += length;
                }
                // Heavier end posts mark the four corners of the crossing.
                for (int end = -1; end <= 1; end += 2)
                {
                    s.Layer = StoneLayer;
                    s.Box(P(side * 4.85f, .65f, end * 4.85f), new Vector3(.8f, 1.3f, .8f), Turn());
                    s.Layer = GrimeLayer;
                    s.Box(P(side * 4.85f, 1.36f, end * 4.85f), new Vector3(.92f, .14f, .92f), Turn());
                }
            }
        }

        // The channel meanders gently around z = 40 and stays inside the 10 m bridge decks.
        private float RiverCentre(float x) => 40 + .6f * Mathf.Sin(x * .11f + Stage);
        private float RiverHalfWidth(float x) => 3.1f + .5f * Mathf.Sin(x * .23f + Stage * 1.7f) + .3f * Mathf.Sin(x * .61f + .8f);
        private const float RiverMud = 1.2f;

        // How far the flat valley floor runs either side of the centre line before the slopes rise.
        private float RiverExtent()
        {
            float extent = 33;
            while (extent < 60 && terrain.SurfaceHeight(-extent - 1, 40) < .05f && terrain.SurfaceHeight(extent + 1, 40) < .05f) extent++;
            return extent;
        }

        // A transparent strip across the flat valley floor: wet mud banks, murky water and a soft sky sheen
        // (Lightbringer/River Water). It ends with a fade where the valley sides begin to rise.
        private void BuildRiverWater(float extent)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var uv = new List<Vector2>();
            var indices = new List<int>();
            float[] across = { 0, .1f, .2f, .35f, .5f, .65f, .8f, .9f, 1 };
            int columns = Mathf.CeilToInt(extent * 2) + 1;
            for (int c = 0; c < columns; c++)
            {
                float x = Mathf.Lerp(-extent, extent, c / (columns - 1f));
                float half = RiverHalfWidth(x) + RiverMud, centre = RiverCentre(x);
                float fade = Mathf.SmoothStep(0, 1, (extent - Mathf.Abs(x)) / 5f);
                for (int r = 0; r < across.Length; r++)
                {
                    float z = centre + (across[r] * 2 - 1) * half;
                    vertices.Add(new Vector3(x, terrain.SurfaceHeight(x, z) + .03f, z));
                    uv.Add(new Vector2(across[r], x));
                    colors.Add(new Color(1, 1, 1, fade));
                    if (c == 0 || r == 0) continue;
                    int i = c * across.Length + r, left = i - across.Length;
                    indices.AddRange(new[] { left - 1, i, i - 1, left - 1, left, i });
                }
            }
            Mesh mesh = new Mesh { name = "River Water", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            owned.Add(mesh);

            Material water = Copy(palette.waterMaterial);
            if (palette.skyMaterial != null)
            {
                water.SetColor("_HorizonColor", palette.skyMaterial.GetColor("_HorizonColor"));
                water.SetColor("_ZenithColor", palette.skyMaterial.GetColor("_ZenithColor"));
            }
            water.SetColor("_DeepColor", IsActThree ? new Color(.10f, .12f, .13f) : new Color(.15f, .16f, .14f));
            water.SetColor("_ShallowColor", IsActThree ? new Color(.18f, .18f, .18f) : new Color(.27f, .25f, .19f));
            water.SetColor("_MudColor", IsActThree ? new Color(.09f, .09f, .09f) : palette.dirt * .42f);
            water.SetFloat("_MudWidth", RiverMud / (3.1f + RiverMud));

            GameObject item = new GameObject("Slate River Shallow Crossing");
            item.transform.SetParent(terrain.transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = water;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // Irregular clusters of half-sunk stones on both banks and (Act 2 only) dry brush, clear of the bridge approaches.
        // Photoscanned rocks where the scan set is imported; dark faceted stones otherwise.
        private void BuildRiverBanks(List<Vector3> crossings, float extent)
        {
            Shapes banks = new Shapes();
            ActTwoScatterAsset[] rocks = UsesPhotoscans ? ScanSet(ActTwoScatterKind.Boulder) : new ActTwoScatterAsset[0];
            ActTwoScatterAsset[] brush = UsesPhotoscans && !IsActThree ? ScanSet(ActTwoScatterKind.Brush) : new ActTwoScatterAsset[0];
            for (int side = -1; side <= 1; side += 2)
                for (float x = -extent + Range(0, 4); x <= extent; x += Range(4.5f, 9))
                {
                    bool nearBridge = false;
                    foreach (Vector3 crossing in crossings) nearBridge |= Mathf.Abs(crossing.x - x) < 7f;
                    if (nearBridge) continue;
                    int stones = random.Next(1, 4);
                    for (int i = 0; i < stones; i++)
                    {
                        float sx = x + Range(-1.4f, 1.4f);
                        // Waterline stones: some sit in the shallows, most on the wet mud.
                        float sz = RiverCentre(sx) + side * (RiverHalfWidth(sx) + Range(-.5f, 1f));
                        float radius = i == 0 ? Range(.45f, .8f) : Range(.2f, .45f);
                        if (rocks.Length > 0)
                        {
                            ActTwoScatterAsset asset = rocks[random.Next(rocks.Length)];
                            PlaceScan(asset, new Vector2(sx, sz), radius / Mathf.Max(.1f, asset.radius), Range(0, 360), Range(-12, 12), .3f);
                        }
                        else
                            banks.Piece(ProceduralMeshes.Faceted(random.Next(6)), palette.rock * Range(.55f, .75f),
                                Ground(sx, sz) + Vector3.up * radius * .15f, new Vector3(radius * Range(1.6f, 2.4f), radius * .8f, radius * 1.6f),
                                new Vector3(Range(-10, 10), Range(0, 360), Range(-10, 10)));
                    }
                    if (IsActThree || random.Next(3) != 0) continue;
                    float bx = x + Range(-2, 2), bz = RiverCentre(bx) + side * (RiverHalfWidth(bx) + RiverMud + Range(.2f, 1.2f));
                    if (brush.Length > 0)
                    {
                        ActTwoScatterAsset asset = brush[random.Next(brush.Length)];
                        PlaceScan(asset, new Vector2(bx, bz), Range(.7f, 1.1f) / Mathf.Max(.1f, asset.radius), Range(0, 360));
                    }
                    else
                    {
                        Color reed = Color.Lerp(new Color(.30f, .27f, .20f), new Color(.22f, .20f, .15f), Range(0, 1));
                        for (int blade = 0; blade < 9; blade++)
                            banks.Cone(.1f, reed, Ground(bx, bz) + new Vector3(Range(-.4f, .4f), 0, Range(-.4f, .4f)),
                                new Vector3(.05f, Range(.5f, 1.1f), .05f), new Vector3(Range(-16, 16), 0, Range(-16, 16)), segments: 4);
                    }
                }
            if (!banks.IsEmpty) MeshGroup("River Bank Stones and Reeds", banks, false);
        }
    }
}
