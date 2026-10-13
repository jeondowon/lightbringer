using System;
using Lightbringer.Pathing;
using UnityEngine;
using Shapes = Lightbringer.Visuals.SilhouetteFactory.Shapes;
using Shape = Lightbringer.Visuals.SilhouetteFactory.Shape;
using Surface = Lightbringer.Visuals.SilhouetteFactory.Surface;

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
                    CitadelBackdrop();
                    break;
                default:
                    BuildActThreeLandmarks(paths);
                    break;
            }
            // The river basin links the whole inland region; never an ocean rim or alpine wall.
            if (!HasRiverCrossing) RiverBank();
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
                    draw(shapes, Ground(point.x, point.y));
                    MeshGroup(label, shapes);
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
            Palisade(shapes, p);
            shapes.Pattern = Surface.Masonry;
            Block(shapes, p + new Vector3(-1.8f, 1.1f, .5f), new Vector3(.55f, 2.2f, 3));
            Block(shapes, p + new Vector3(1.8f, .55f, .5f), new Vector3(.55f, 1.1f, 3));
            shapes.Pattern = Surface.Planks;
            Block(shapes, p + new Vector3(-.6f, 2.35f, .5f), new Vector3(2.4f, .22f, 3.5f), palette.bark,
                new Vector3(0, 0, -16));
            for (int i = 0; i < 4; i++)
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
            Palisade(shapes, p + Vector3.forward * 1.7f);
            SupplyProp(p + new Vector3(0, 0, -1.4f), BattlefieldPropKind.AlliedChest);
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
            Palisade(shapes, p);
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
            Shapes water = new Shapes(), bridges = new Shapes { Pattern = Surface.Masonry };
            // Shallow river crossing: the greybox keeps the same collision floor so the hero can wade.
            Block(water, new Vector3(0, .012f, 40), new Vector3(67, .02f, 6), new Color(.18f, .29f, .33f));
            foreach (WaypointPath path in paths)
                for (int i = 0; i < path.Count - 1; i++)
                {
                    Vector3 a = terrain.transform.InverseTransformPoint(path.GetPosition(i));
                    Vector3 b = terrain.transform.InverseTransformPoint(path.GetPosition(i + 1));
                    if (a.z > 40 || b.z <= 40) continue;
                    Vector3 p = Vector3.Lerp(a, b, (40 - a.z) / (b.z - a.z));
                    float yaw = Quaternion.LookRotation(b - a).eulerAngles.y;
                    Block(bridges, p + Vector3.up * .055f, new Vector3(10, .08f, 10), rotation: new Vector3(0, yaw, 0));
                    for (int side = -1; side <= 1; side += 2)
                        Block(bridges, p + Quaternion.Euler(0, yaw, 0) * new Vector3(side * 5, .5f, 0),
                            new Vector3(.55f, 1, 8), rotation: new Vector3(0, yaw, 0));
                    Bridges++;
                }
            for (int x = -33; x <= 33; x += 3) terrain.ReserveProp(new Vector2(x, 40), 4.5f);
            MeshGroup("Slate River Shallow Crossing", water, false);
            MeshGroup("Guarded Stone Bridges", bridges);
        }

        private void RiverBank()
        {
            // A narrow inland tributary on the flat western margin, not an ocean surrounding the region.
            Shapes water = new Shapes();
            for (int i = 0; i < 12; i++)
            {
                float z = -5 + i * 5;
                float x = -32 + Mathf.Sin(i * .5f) * .6f;
                Block(water, new Vector3(x, .012f, z), new Vector3(2.2f, .02f, 5.4f), new Color(.19f, .29f, .32f));
                terrain.ReserveProp(new Vector2(x, z), 2);
            }
            MeshGroup("Inland River Margin", water, false);
        }

        private void CitadelBackdrop()
        {
            Shapes fortress = new Shapes { Pattern = Surface.Masonry };
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 foot = Ground(side * 19, 97);
                Block(fortress, foot + Vector3.up * 8, new Vector3(8, 16, 8), palette.rock * .8f);
                for (int merlon = -1; merlon <= 1; merlon++)
                    Block(fortress, foot + new Vector3(merlon * 2.8f, 16.7f, -3.5f), new Vector3(1.5f, 1.6f, 1.5f));
                Block(fortress, Ground(side * 10, 100) + Vector3.up * 5, new Vector3(12, 10, 3), palette.rock * .8f);
            }
            MeshGroup("Standing Warlord Citadel Beyond the Gate", fortress);
            Landmarks++;
        }
    }
}
