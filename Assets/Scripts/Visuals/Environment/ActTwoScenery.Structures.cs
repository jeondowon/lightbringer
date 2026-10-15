using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Photographic structures (Poly Haven CC0 stone, slate, planks and bark on TexturedShapes): the Act 2 depots,
    // storeyards and wagon yards, plus Act 3 roofless homes, gate towers and charred outposts.
    public sealed partial class ActTwoScenery
    {
        private const int StoneLayer = 0, RoofLayer = 1, TimberLayer = 2, BarkLayer = 3, GapLayer = 4, GlowLayer = 5,
            GrimeLayer = 6, LayerCount = 7;
        private Material[] structureMaterials;
        // Set while a landmark draws: photographic pieces collect here instead of the toon Shapes.
        private TexturedShapes structure;

        private bool HasStructures => palette.actTwoEnvironment != null && palette.actTwoEnvironment.HasStructureMaterials;
        public int PhotoStructures { get; private set; }

        private Material[] StructureMaterials()
        {
            if (structureMaterials != null) return structureMaterials;
            ActTwoEnvironmentProfile e = palette.actTwoEnvironment;
            // Act 3's stone is soot-dark; Act 2 keeps the scanned colour.
            Material stone = Tinted(e.castleStone, IsActThree ? .5f : 1);
            Material roof = Tinted(e.roofSlate, IsActThree ? .55f : 1);
            Material gap = Tinted(e.castleStone, .06f);
            gap.SetFloat("_BumpScale", .2f);
            // Rain-streaked, soot-darkened masonry for plinths and terraces: breaks up the evenly lit stone.
            Material grime = Tinted(e.castleStone, IsActThree ? .3f : .62f);
            structureMaterials = new[] { stone, roof, Tinted(e.timber, IsActThree ? .65f : 1),
                Tinted(e.bark, IsActThree ? .6f : 1), gap, e.windowGlow != null ? e.windowGlow : gap, grime };
            return structureMaterials;
        }

        private Material Tinted(Material source, float value)
        {
            if (value == 1) return source;
            Material result = Copy(source);
            result.SetColor("_BaseColor", source.GetColor("_BaseColor") * new Color(value, value, value, 1));
            return result;
        }

        private Transform StructureGroup(string label, TexturedShapes shapes)
        {
            Mesh mesh = shapes.Bake("Act Two " + label);
            owned.Add(mesh);
            GameObject item = new GameObject(label);
            item.transform.SetParent(terrain.transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = StructureMaterials();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            PhotoStructures++;
            return item.transform;
        }

        // Sharpened log stakes bound by two plank rails; some are burned down or gone.
        private void PhotoPalisade(Vector3 p, float width)
        {
            TexturedShapes s = structure;
            int count = Mathf.RoundToInt(width / .42f);
            for (int i = 0; i < count; i++)
            {
                double roll = random.NextDouble();
                if (roll < .07) continue;
                bool broken = roll > .88;
                float radius = Range(.17f, .22f), height = broken ? Range(.9f, 1.6f) : Range(2.5f, 3.1f);
                Vector3 lean = new Vector3(Range(-3, 3), 0, Range(-3, 3));
                Vector3 foot = p + new Vector3(-width * .5f + (i + .5f) * width / count, -.2f, 3.3f + Range(-.06f, .06f));
                s.Layer = BarkLayer;
                s.Column(foot, radius, height, 9, 1, lean);
                // Axe-cut points show the pale inner wood.
                s.Layer = TimberLayer;
                if (!broken) s.Column(foot + Quaternion.Euler(lean) * Vector3.up * height, radius, .5f, 9, .06f, lean, caps: false);
            }
            s.Layer = TimberLayer;
            for (int rail = 0; rail < 2; rail++)
                s.Box(p + new Vector3(0, .8f + rail * 1.15f, 3.03f), new Vector3(width - .3f, .17f, .09f),
                    new Vector3(0, 0, Range(-1.2f, 1.2f)));
        }

        // Open-fronted lean-to storehouse: bark posts, plank back and side walls, a shingle roof and a dry-stone wall.
        private void PhotoShed(Vector3 c, float yaw)
        {
            TexturedShapes s = structure;
            Quaternion r = Quaternion.Euler(0, yaw, 0);
            Vector3 P(float x, float y, float z) => c + r * new Vector3(x, y, z);
            Vector3 turn = new Vector3(0, yaw, 0);
            const float w = 3.8f, d = 2.8f, back = 2.7f, front = 2.05f;
            s.Layer = BarkLayer;
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    s.Column(P(x * (w * .5f - .12f), -.15f, z * (d * .5f - .12f)), .11f, (z > 0 ? back : front) + .1f, 8);
            // Top plates the rafters rest on.
            s.Column(P(-w * .5f - .15f, front, -d * .5f + .12f), .1f, w + .3f, 8, 1, new Vector3(0, yaw, -90));
            s.Column(P(-w * .5f - .15f, back, d * .5f - .12f), .1f, w + .3f, 8, 1, new Vector3(0, yaw, -90));
            s.Layer = TimberLayer;
            s.Box(P(0, back * .5f - .05f, d * .5f - .03f), new Vector3(w - .2f, back - .1f, .07f), turn);
            s.Box(P(-w * .5f + .03f, front * .5f, 0), new Vector3(.07f, front, d - .2f), turn);
            s.Box(P(w * .5f - .03f, .55f, .4f), new Vector3(.07f, 1.1f, d - 1), turn);
            float pitch = Mathf.Atan2(back - front, d) * Mathf.Rad2Deg;
            s.Layer = RoofLayer;
            s.Box(P(0, (back + front) * .5f + .17f, 0), new Vector3(w + .6f, .07f, d + .7f), new Vector3(-pitch, yaw, 0));
            // A low, half-collapsed dry-stone wall closes the open side.
            s.Layer = StoneLayer;
            s.Box(P(w * .5f + .55f, .4f, 0), new Vector3(.62f, .9f, d + .2f), new Vector3(0, yaw + 2, 0));
            s.Box(P(w * .5f + .55f, 1.0f, -.6f), new Vector3(.55f, .4f, 1.3f), new Vector3(0, yaw - 4, 3));
            s.Box(P(w * .5f + 1.1f, .18f, 1.2f), new Vector3(.5f, .36f, .6f), new Vector3(12, yaw + 30, 8));
        }

        private void PhotoDepot(Vector3 p)
        {
            PhotoPalisade(p, 8);
            PhotoShed(p + new Vector3(-.7f, 0, .8f), 0);
            ScanGoods(p + Vector3.back * 2.6f, 2.4f, 5);
        }

        private void PhotoRuinedHouse(Vector3 p)
        {
            structure.Layer = GrimeLayer;
            structure.Box(p + Vector3.up * .08f, new Vector3(6, .16f, 5));
            // Uneven wall courses leave the front doorway and broken window openings readable.
            structure.Layer = StoneLayer;
            for (int row = 0; row < 5; row++)
                for (int bay = -2; bay <= 2; bay++)
                {
                    if (row > 2 && Mathf.Abs(bay) == 1) continue;
                    structure.Box(p + new Vector3(bay * 1.1f, .35f + row * .55f, 2.2f),
                        new Vector3(1.06f, .52f, .5f));
                }
            for (int side = -1; side <= 1; side += 2)
            {
                structure.Box(p + new Vector3(side * 2.7f, side < 0 ? 1.1f : .65f, .3f),
                    new Vector3(.5f, side < 0 ? 2.2f : 1.3f, 4.3f));
                structure.Box(p + new Vector3(side * 1.9f, .6f, -2), new Vector3(1.8f, 1.2f, .5f));
            }
            structure.Layer = TimberLayer;
            structure.Box(p + new Vector3(.2f, .6f, .1f), new Vector3(.25f, .28f, 4.8f), new Vector3(12, 27, 0));
            structure.Box(p + new Vector3(-1.7f, .18f, -.8f), new Vector3(.25f, .23f, 2.6f), new Vector3(0, -34, 0));
            PhotoRubble(p, 3.4f, 12);
            ScanGoods(p, 1.6f, 2);
        }

        private void PhotoGateTower(Vector3 p)
        {
            structure.Layer = GrimeLayer;
            structure.Box(p + Vector3.up * .35f, new Vector3(4.6f, .7f, 4.6f));
            structure.Layer = StoneLayer;
            structure.Box(p + Vector3.up * 4.6f, new Vector3(3.5f, 8.5f, 3.5f));
            foreach (float height in new[] { 1.1f, 5.8f, 8.6f })
                structure.Box(p + Vector3.up * height, new Vector3(3.9f, .25f, 3.9f));
            for (int side = -1; side <= 1; side += 2)
                for (int row = -1; row <= 1; row++)
                    structure.Box(p + new Vector3(side * 1.6f, 9.2f, row * 1.35f), new Vector3(.65f, .9f, .65f));
            structure.Layer = GapLayer;
            structure.Box(p + new Vector3(0, 6, -1.76f), new Vector3(.35f, 1.6f, .025f));
            structure.Layer = StoneLayer;
            structure.Box(p + new Vector3(0, 6.8f, -.2f), new Vector3(5.5f, .8f, 2.4f));
            PhotoRubble(p, 3.2f, 10);
        }

        private void PhotoDarkOutpost(Vector3 p)
        {
            PhotoPalisade(p, 7);
            structure.Layer = GrimeLayer;
            structure.Box(p + new Vector3(0, .25f, .5f), new Vector3(4, .5f, 3.8f));
            structure.Layer = StoneLayer;
            structure.Box(p + new Vector3(0, 2.2f, .6f), new Vector3(3.4f, 4.4f, 3));
            for (int x = -1; x <= 1; x++)
                structure.Box(p + new Vector3(x * 1.2f, 4.75f, -.8f), new Vector3(.7f, .7f, .4f));
            structure.Layer = GapLayer;
            structure.Box(p + new Vector3(0, 1.1f, -.91f), new Vector3(1.1f, 2.2f, .03f));
            EnemyProp(p + new Vector3(2.5f, 0, -1.8f), BattlefieldPropKind.EnemyAltar, 1.5f);
            ScanGoods(p + new Vector3(-1.5f, 0, -2), 1.2f, 3);
            PhotoRubble(p, 3.2f, 8);
        }

        private void PhotoRubble(Vector3 centre, float radius, int count)
        {
            structure.Layer = GrimeLayer;
            for (int i = 0; i < count; i++)
            {
                float angle = Range(0, Mathf.PI * 2), distance = Range(1, radius);
                Vector3 point = centre + new Vector3(Mathf.Cos(angle) * distance, .16f, Mathf.Sin(angle) * distance);
                structure.Box(point, new Vector3(Range(.3f, .65f), Range(.2f, .4f), Range(.4f, .75f)),
                    new Vector3(Range(-12, 12), Range(0, 360), Range(-10, 10)));
            }
        }

        private void PhotoTimberYard(Vector3 p)
        {
            PhotoPalisade(p + Vector3.forward * 1.7f, 9);
            TexturedShapes s = structure;
            s.Layer = BarkLayer;
            // Two pyramids of felled trunks (3-2-1) laid along the yard.
            for (int stack = -1; stack <= 1; stack += 2)
                for (int i = 0; i < 6; i++)
                {
                    int row = i < 3 ? 0 : i < 5 ? 1 : 2;
                    float slot = i < 3 ? i - 1 : i < 5 ? i - 3.5f : 0;
                    float radius = Range(.27f, .33f);
                    s.Column(p + new Vector3(stack * 2.5f + slot * .62f, radius + row * .52f, -.6f + Range(-.2f, .2f)),
                        radius, Range(4.2f, 4.8f), 12, 1, new Vector3(90, Range(-2, 2), 0));
                }
            ScanGoods(p + Vector3.back * 3.3f, 2, 4);
        }

        private void PhotoWagonYard(Vector3 p)
        {
            PhotoPalisade(p, 8);
            TexturedShapes s = structure;
            ActTwoScatterAsset[] crates = ScanSet(ActTwoScatterKind.Crate);
            for (int wagon = -1; wagon <= 1; wagon += 2)
            {
                float yaw = wagon * 8;
                Quaternion r = Quaternion.Euler(0, yaw, 0);
                Vector3 c = p + new Vector3(wagon * 2.1f, 0, -.2f);
                Vector3 P(float x, float y, float z) => c + r * new Vector3(x, y, z);
                Vector3 turn = new Vector3(0, yaw, 0);
                s.Layer = TimberLayer;
                s.Box(P(0, .95f, 0), new Vector3(1.7f, .12f, 3), turn);
                for (int side = -1; side <= 1; side += 2)
                {
                    s.Box(P(side * .82f, 1.24f, 0), new Vector3(.06f, .46f, 3), turn);
                    s.Box(P(0, 1.24f, side * 1.47f), new Vector3(1.7f, .46f, .06f), turn);
                    // Axles and solid plank wheels; each shaft grows inward from its outer face.
                    s.Box(P(0, .58f, side), new Vector3(2.1f, .1f, .1f), turn);
                    for (int x = -1; x <= 1; x += 2)
                    {
                        s.Column(P(x * 1.02f, .58f, side), .56f, .1f, 16, 1, new Vector3(0, yaw, x * 90));
                        s.Layer = BarkLayer;
                        s.Column(P(x * 1.12f, .58f, side), .12f, .12f, 8, 1, new Vector3(0, yaw, x * 90));
                        s.Layer = TimberLayer;
                    }
                }
                // The draw pole rests on the ground in front.
                s.Box(P(0, .45f, -2.4f), new Vector3(.1f, .1f, 1.9f), new Vector3(-16, yaw, 0));
                if (crates.Length > 0)
                {
                    Vector3 load = P(0, 0, .3f);
                    // Lifted onto the wagon bed: PlaceScan's sink offset is negative for raised props.
                    PlaceScan(crates[random.Next(crates.Length)], new Vector2(load.x, load.z), 1.3f, Range(0, 360), 0,
                        -(1.01f + c.y - terrain.SurfaceHeight(load.x, load.z)) / 1.3f);
                }
            }
            ActTwoScatterAsset[] pit = ScanSet(ActTwoScatterKind.FirePit);
            if (pit.Length > 0) PlaceScan(pit[0], new Vector2(p.x, p.z) + new Vector2(0, -3.6f), 1.3f, 0);
            ScanGoods(p + Vector3.back * 3, 1.6f, 3);
        }
    }
}
