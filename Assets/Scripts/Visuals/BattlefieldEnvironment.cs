using System.Collections.Generic;
using Lightbringer.Pathing;
using UnityEngine;
using UnityEngine.Rendering;
using Shapes = Lightbringer.Visuals.SilhouetteFactory.Shapes;
using Shape = Lightbringer.Visuals.SilhouetteFactory.Shape;

namespace Lightbringer.Visuals
{
    // Art Pass battlefield dressing: a terrain whose play area stays perfectly flat (so movement, deployment
    // and Paths are unchanged) rising into hills outside it, dirt roads along the Paths, stone plazas at both
    // strongholds, trees/rocks/ruins kept clear of the lanes, and a hazy mountain and castle backdrop.
    // Deterministic per stage. Owns the meshes/materials it creates and destroys them with the battlefield.
    public sealed class BattlefieldEnvironment : MonoBehaviour
    {
        // Flat playable rectangle (world XZ): all Paths, bases and the hero start lie inside it.
        public const float PlayHalfWidth = 34f, PlayMinZ = -30f, PlayMaxZ = 48f;
        private const float LaneClearance = 4.5f;

        private readonly List<Object> owned = new List<Object>();
        private readonly List<(Vector2 a, Vector2 b)> lanes = new List<(Vector2, Vector2)>();
        private readonly List<Vector2> keepClear = new List<Vector2>();
        private System.Random random;
        private float seed;
        private ArtStyleLibrary style;

        public MeshCollider Terrain { get; private set; }
        public int GrassChunks { get; private set; }
        public float LaneDistance(Vector2 point) => DistanceToLanes(point);

        public static BattlefieldEnvironment Build(ArtStyleLibrary library, int stage, Transform root,
            WaypointPath[] paths, Vector3 deployPoint, Vector3[] clearPoints, Light sun)
        {
            if (library == null || library.terrainMaterial == null || library.propsMaterial == null) return null;
            GameObject item = new GameObject("Battlefield Environment");
            item.transform.SetParent(root, false);
            BattlefieldEnvironment environment = item.AddComponent<BattlefieldEnvironment>();
            environment.Generate(library, stage, paths, deployPoint, clearPoints, sun);
            return environment;
        }

        private void Generate(ArtStyleLibrary library, int stage, WaypointPath[] paths, Vector3 deploy, Vector3[] clearPoints, Light sun)
        {
            style = library;
            random = new System.Random(stage * 7919 + 13);
            seed = stage * 17.31f;
            foreach (WaypointPath path in paths)
            {
                if (path == null || !path.IsValid) continue;
                Vector2 previous = Local(deploy);
                for (int i = 0; i < path.Count; i++)
                {
                    Vector2 point = Local(path.GetPosition(i));
                    lanes.Add((previous, point));
                    previous = point;
                }
            }
            foreach (Vector3 point in clearPoints) keepClear.Add(Local(point));

            BuildTerrain();
            if (style.grassMaterial != null) BuildGrass();
            Shapes near = new Shapes(), far = new Shapes(), backdrop = new Shapes();
            ScatterTrees(near, far);
            ScatterRocks(near);
            BuildRuins(near);
            BuildBackdrop(backdrop);
            AddMesh("Props (Near)", near.Bake("LB Env Props Near"), style.propsMaterial, true);
            AddMesh("Props (Far)", far.Bake("LB Env Props Far"), style.propsMaterial, false);
            AddMesh("Backdrop", backdrop.Bake("LB Env Backdrop"), style.propsMaterial, false);
            if (style.skyMaterial != null)
            {
                Material sky = new Material(style.skyMaterial) { name = "Sky (Instance)" };
                sky.SetColor("_HorizonColor", style.fogColor);
                // Match the sun glow to the styled sun angle (applied after the environment is built).
                sky.SetVector("_SunDirection", -(Quaternion.Euler(style.sunEuler) * Vector3.forward));
                owned.Add(sky);
                RenderSettings.skybox = sky;
            }
        }

        // ---------- Terrain ----------

        public static float OutsideDistance(float x, float z)
        {
            float dx = Mathf.Max(Mathf.Abs(x) - PlayHalfWidth, 0f);
            float dz = Mathf.Max(Mathf.Max(PlayMinZ - z, z - PlayMaxZ), 0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public float Height(float x, float z)
        {
            float d = OutsideDistance(x, z);
            if (d <= 0f) return 0f;
            float rise = Mathf.SmoothStep(0f, 1f, d / 20f);
            float hills = 4f + 9f * Mathf.PerlinNoise(x * 0.025f + seed, z * 0.025f + seed);
            float ridges = Mathf.SmoothStep(0f, 1f, d / 60f) * 14f * Mathf.PerlinNoise(x * 0.012f - seed, z * 0.012f + 3.1f);
            return rise * hills + ridges;
        }

        private void BuildTerrain()
        {
            const int columns = 101, rows = 101;
            const float minX = -110f, maxX = 110f, minZ = -95f, maxZ = 150f;
            Vector3[] vertices = new Vector3[columns * rows];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                {
                    float x = Mathf.Lerp(minX, maxX, c / (columns - 1f));
                    float z = Mathf.Lerp(minZ, maxZ, r / (rows - 1f));
                    vertices[r * columns + c] = new Vector3(x, Height(x, z), z);
                }
            int[] triangles = new int[(columns - 1) * (rows - 1) * 6];
            int t = 0;
            for (int r = 0; r < rows - 1; r++)
                for (int c = 0; c < columns - 1; c++)
                {
                    int a = r * columns + c, b = a + columns;
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = a + 1;
                    triangles[t++] = a + 1; triangles[t++] = b; triangles[t++] = b + 1;
                }
            Mesh mesh = new Mesh { name = "LB Terrain", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            Vector3[] normals = mesh.normals;
            Color[] colors = new Color[vertices.Length];
            for (int i = 0; i < vertices.Length; i++) colors[i] = GroundColor(vertices[i], normals[i]);
            mesh.colors = colors;
            mesh.RecalculateBounds();
            MeshRenderer renderer = AddMesh("Terrain", mesh, style.terrainMaterial, false);
            renderer.receiveShadows = true;
            Terrain = renderer.gameObject.AddComponent<MeshCollider>();
            Terrain.sharedMesh = mesh;
        }

        private Color GroundColor(Vector3 point, Vector3 normal)
        {
            Vector2 flat = Flat(point);
            float variation = Mathf.PerlinNoise(point.x * 0.08f + seed, point.z * 0.08f);
            Color color = Color.Lerp(style.grassDark, style.grassLight, variation);
            // Dirt roads along every lane, with a soft edge.
            float road = RoadMask(flat);
            color = Color.Lerp(color, Color.Lerp(style.dirt, style.dirt * 0.85f, variation), road * 0.9f);
            // Stone plazas at the strongholds.
            color = Color.Lerp(color, style.plazaStone * (0.92f + 0.08f * variation), PlazaMask(flat));
            // Hills darken with height; steep faces turn to rock.
            color = Color.Lerp(color, style.grassDark * 0.8f, Mathf.Clamp01(point.y / 20f) * 0.5f);
            color = Color.Lerp(color, style.rock, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 0.6f, normal.y)));
            color.a = road; // the terrain shader uses alpha as the road mask (gravel, no flowers)
            return color;
        }

        private float RoadMask(Vector2 flat) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.8f, 3.4f, DistanceToLanes(flat)));

        private float PlazaMask(Vector2 flat)
        {
            float plaza = 0f;
            foreach (Vector2 clear in keepClear)
                plaza = Mathf.Max(plaza, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(6f, 8.5f, Vector2.Distance(flat, clear))));
            return plaza;
        }

        // ---------- Grass ----------

        // Blades are baked into 12 m chunks so off-screen chunks are culled; none on roads, plazas or steep slopes.
        private void BuildGrass()
        {
            const float chunk = 12f;
            int chunks = 0;
            for (float cx = -66f; cx < 66f; cx += chunk)
                for (float cz = -54f; cz < 84f; cz += chunk)
                {
                    List<Vector3> vertices = new List<Vector3>();
                    List<Color> colors = new List<Color>();
                    List<Vector2> uvs = new List<Vector2>();
                    List<int> triangles = new List<int>();
                    for (int i = 0; i < 1500; i++)
                    {
                        float x = cx + Range(0f, chunk), z = cz + Range(0f, chunk);
                        float outside = OutsideDistance(x, z);
                        float density = outside <= 0f ? 1f : Mathf.Clamp01(1f - outside / 26f) * 0.5f;
                        Vector2 flat = new Vector2(x, z);
                        if (density <= 0f || random.NextDouble() > density) continue;
                        float road = RoadMask(flat);
                        if (random.NextDouble() < Mathf.Max(road * 1.3f, PlazaMask(flat))) continue;
                        float slope = Mathf.Abs(Height(x + 0.5f, z) - Height(x - 0.5f, z)) + Mathf.Abs(Height(x, z + 0.5f) - Height(x, z - 0.5f));
                        if (slope > 0.7f) continue;
                        Vector3 root = new Vector3(x, Height(x, z), z);
                        Color ground = GroundColor(root, Vector3.up);
                        Color blade = Color.Lerp(Color.Lerp(ground, style.grassLight, 0.3f), style.dirt * 1.1f, road * 0.6f);
                        Blade(vertices, colors, uvs, triangles, root, blade, outside > 0f);
                    }
                    if (vertices.Count == 0) continue;
                    Mesh mesh = new Mesh { name = "LB Grass " + chunks, hideFlags = HideFlags.DontSave };
                    mesh.SetVertices(vertices);
                    mesh.SetColors(colors);
                    mesh.SetUVs(0, uvs);
                    mesh.SetTriangles(triangles, 0);
                    mesh.RecalculateBounds();
                    // Room for the wind and hero-push displacement so chunks are not culled too early.
                    Bounds bounds = mesh.bounds;
                    bounds.Expand(1.5f);
                    mesh.bounds = bounds;
                    MeshRenderer renderer = AddMesh("Grass " + chunks++, mesh, style.grassMaterial, false);
                    renderer.receiveShadows = true;
                    GrassChunks++;
                }
        }

        private void Blade(List<Vector3> vertices, List<Color> colors, List<Vector2> uvs, List<int> triangles,
            Vector3 root, Color color, bool hills)
        {
            float yaw = Range(0f, Mathf.PI * 2f);
            Vector3 side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
            Vector3 lean = new Vector3(Range(-1f, 1f), 0f, Range(-1f, 1f)) * 0.12f;
            float height = hills ? Range(0.4f, 0.75f) : Range(0.3f, 0.58f);
            float width = Range(0.08f, 0.13f);
            int start = vertices.Count;
            vertices.Add(root - side * width * 0.5f);
            vertices.Add(root + side * width * 0.5f);
            vertices.Add(root + Vector3.up * height * 0.5f + lean * 0.35f - side * width * 0.3f);
            vertices.Add(root + Vector3.up * height * 0.5f + lean * 0.35f + side * width * 0.3f);
            vertices.Add(root + Vector3.up * height + lean);
            for (int i = 0; i < 5; i++) colors.Add(color);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0f, 0.5f)); uvs.Add(new Vector2(1f, 0.5f));
            uvs.Add(new Vector2(0.5f, 1f));
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start + 1); triangles.Add(start + 2); triangles.Add(start + 3);
            triangles.Add(start + 2); triangles.Add(start + 4); triangles.Add(start + 3);
        }

        // ---------- Props ----------

        private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

        private bool ClearOfPlay(Vector2 point, float lane, float bases)
        {
            if (DistanceToLanes(point) < lane) return false;
            foreach (Vector2 clear in keepClear) if (Vector2.Distance(point, clear) < bases) return false;
            return true;
        }

        private void ScatterTrees(Shapes near, Shapes far)
        {
            // Forest belt on the hills, plus a few trees inside the play area away from lanes and bases.
            for (int i = 0; i < 300; i++)
            {
                float x = Range(-105f, 105f), z = Range(-90f, 140f);
                float d = OutsideDistance(x, z);
                if (d < 2f || d > 75f) continue;
                Tree(d < 22f ? near : far, new Vector3(x, Height(x, z), z), Range(1.2f, 2.4f));
            }
            for (int i = 0; i < 60; i++)
            {
                Vector2 point = new Vector2(Range(-PlayHalfWidth + 2f, PlayHalfWidth - 2f), Range(PlayMinZ + 2f, PlayMaxZ - 2f));
                if (!ClearOfPlay(point, 8f, 13f)) continue;
                Tree(near, new Vector3(point.x, 0f, point.y), Range(1f, 1.8f));
            }
        }

        private void Tree(Shapes target, Vector3 ground, float scale)
        {
            float yaw = Range(0f, 360f);
            Color leaves = Color.Lerp(style.foliageDark, style.foliage, Range(0.2f, 1f));
            Color bark = Color.Lerp(style.bark, style.bark * 1.25f, Range(0f, 1f));
            if (random.NextDouble() < 0.45)
            {
                // Pine: slim trunk and staggered, slightly tilted tiers with soft cylindrical shading.
                target.Piece(ProceduralMeshes.Frustum(0.55f, 8), bark, ground - Vector3.up * 0.2f, new Vector3(0.32f, 2.2f, 0.32f) * scale, new Vector3(0f, yaw, 0f), groundY: ground.y);
                int tiers = random.Next(3, 5);
                for (int t = 0; t < tiers; t++)
                {
                    float k = t / (float)tiers;
                    float width = Mathf.Lerp(2.6f, 0.9f, k) * scale;
                    Vector3 basePoint = ground + Vector3.up * (1.0f + t * 0.85f) * scale;
                    Vector3 axis = ground + Vector3.up * (1.6f + t * 0.85f) * scale;
                    target.Piece(ProceduralMeshes.Frustum(0f, 9), Color.Lerp(leaves, style.foliageDark, 0.25f - k * 0.25f), basePoint,
                        new Vector3(width, 1.8f * scale, width * Range(0.85f, 1.1f)),
                        new Vector3(Range(-5f, 5f), yaw + t * 23f, Range(-5f, 5f)), sway: 0.25f + k * 0.5f, softCenter: axis, softBlend: 0.45f);
                }
                return;
            }
            // Broadleaf: leaning trunk with two branches and a clumped canopy that shades as one soft volume.
            Vector3 lean = new Vector3(Range(-0.25f, 0.25f), 0f, Range(-0.25f, 0.25f)) * scale;
            target.Piece(ProceduralMeshes.Frustum(0.6f, 8), bark, ground - Vector3.up * 0.2f, new Vector3(0.42f, 2.4f, 0.42f) * scale,
                new Vector3(lean.z * 25f, yaw, -lean.x * 25f), groundY: ground.y);
            Vector3 crown = ground + lean + Vector3.up * 3.1f * scale;
            for (int b = 0; b < 2; b++)
            {
                float angle = yaw + b * 160f + Range(-20f, 20f);
                target.Cone(0.4f, bark, ground + lean * 0.7f + Vector3.up * 1.8f * scale, new Vector3(0.16f, 1.3f, 0.16f) * scale,
                    new Vector3(45f, angle, 0f), 6);
            }
            int clumps = random.Next(5, 8);
            for (int c = 0; c < clumps; c++)
            {
                float angle = c / (float)clumps * Mathf.PI * 2f + Range(-0.3f, 0.3f);
                float ring = c == 0 ? 0f : Range(0.7f, 1.2f);
                Vector3 offset = new Vector3(Mathf.Cos(angle) * ring, c == 0 ? 0.45f : Range(-0.35f, 0.35f), Mathf.Sin(angle) * ring) * scale;
                float size = (c == 0 ? 2.3f : Range(1.4f, 1.9f)) * scale;
                Color tone = Color.Lerp(leaves, style.foliage * 1.08f, Mathf.Clamp01(0.5f + offset.y / scale));
                target.Piece(ProceduralMeshes.Lumpy(random.Next(6)), tone, crown + offset, Vector3.one * size,
                    new Vector3(Range(0f, 360f), Range(0f, 360f), 0f), sway: 0.6f, softCenter: crown + Vector3.up * 0.3f * scale, softBlend: 0.75f);
            }
        }

        private void Bush(Shapes target, Vector3 ground, float scale)
        {
            Color leaves = Color.Lerp(style.foliageDark, style.foliage, Range(0.1f, 0.8f));
            Vector3 centre = ground + Vector3.up * 0.35f * scale;
            int clumps = random.Next(2, 4);
            for (int c = 0; c < clumps; c++)
            {
                Vector3 offset = new Vector3(Range(-0.45f, 0.45f), Range(0f, 0.2f), Range(-0.45f, 0.45f)) * scale;
                target.Piece(ProceduralMeshes.Lumpy(random.Next(6)), leaves, centre + offset, new Vector3(1.2f, 0.85f, 1.2f) * scale * Range(0.8f, 1.1f),
                    new Vector3(0f, Range(0f, 360f), 0f), sway: 0.35f, softCenter: centre, softBlend: 0.7f, groundY: ground.y);
            }
        }

        private void ScatterRocks(Shapes target)
        {
            Color moss = Color.Lerp(style.grassDark, style.foliage, 0.4f);
            for (int i = 0; i < 70; i++)
            {
                float x = Range(-60f, 60f), z = Range(-55f, 80f);
                float d = OutsideDistance(x, z);
                if (d > 30f || (d <= 0f && !ClearOfPlay(new Vector2(x, z), LaneClearance + 1.5f, 10f))) continue;
                Vector3 ground = new Vector3(x, Height(x, z), z);
                float size = Range(0.6f, d > 0f ? 2.6f : 1.5f);
                // A main stone with a couple of smaller ones half sunk around it; moss on top faces.
                int stones = random.Next(1, 4);
                for (int s = 0; s < stones; s++)
                {
                    float k = s == 0 ? 1f : Range(0.35f, 0.6f);
                    Vector3 offset = s == 0 ? Vector3.zero : new Vector3(Range(-1f, 1f), 0f, Range(-1f, 1f)).normalized * size * Range(0.6f, 0.9f);
                    Color stone = Color.Lerp(style.rock * 0.9f, style.rock * 1.12f, Range(0f, 1f));
                    target.Piece(ProceduralMeshes.Faceted(random.Next(6)), stone, ground + offset + Vector3.up * size * k * 0.15f,
                        new Vector3(size * k * Range(1f, 1.5f), size * k * Range(0.6f, 0.95f), size * k * Range(0.9f, 1.3f)),
                        new Vector3(Range(-12f, 12f), Range(0f, 360f), Range(-12f, 12f)), top: moss, groundY: ground.y);
                }
                if (d <= 0f && random.NextDouble() < 0.5) Bush(target, ground + new Vector3(Range(-1.5f, 1.5f), 0f, Range(-1.5f, 1.5f)), Range(0.7f, 1.1f));
            }
        }

        private void BuildRuins(Shapes target)
        {
            // Old Lightbringer shrines: plinthed pillars with broken, rubble-capped tops, an occasional arch with a
            // gilded lintel, scattered rubble and moss. Kept well clear of the lanes.
            Color moss = Color.Lerp(style.grassDark, style.foliage, 0.35f);
            int built = 0;
            for (int attempt = 0; attempt < 80 && built < 5; attempt++)
            {
                Vector2 centre = new Vector2(Range(-PlayHalfWidth + 4f, PlayHalfWidth - 4f), Range(PlayMinZ + 6f, PlayMaxZ - 6f));
                if (!ClearOfPlay(centre, LaneClearance + 3.5f, 14f)) continue;
                built++;
                Vector3 c = new Vector3(centre.x, 0f, centre.y);
                float floorYaw = Range(0f, 90f);
                // Cracked floor: a few offset slabs instead of one block.
                for (int s = 0; s < 4; s++)
                {
                    Vector3 slab = c + Quaternion.Euler(0f, floorYaw, 0f) * new Vector3((s % 2 - 0.5f) * 3.1f, 0f, (s / 2 - 0.5f) * 3.1f);
                    target.Piece(ProceduralMeshes.Box, Color.Lerp(style.ruinStone * 0.85f, style.ruinStone * 0.95f, Range(0f, 1f)),
                        slab + Vector3.up * Range(0.02f, 0.12f), new Vector3(3f, 0.28f, 3f),
                        new Vector3(Range(-2f, 2f), floorYaw + Range(-3f, 3f), Range(-2f, 2f)), top: moss, groundY: 0f);
                }
                int pillars = random.Next(4, 7);
                float start = Range(0f, 360f);
                bool arch = random.NextDouble() < 0.5;
                for (int p = 0; p < pillars; p++)
                {
                    float angle = (start + p * 360f / 7f) * Mathf.Deg2Rad;
                    Vector3 basePoint = c + new Vector3(Mathf.Cos(angle) * 2.8f, 0.2f, Mathf.Sin(angle) * 2.8f);
                    bool intact = arch ? p < 2 : random.NextDouble() < 0.3;
                    float height = intact ? 4.2f : Range(1.1f, 3f);
                    Color stone = Color.Lerp(style.ruinStone * 0.92f, style.ruinStone, Range(0f, 1f));
                    target.Piece(ProceduralMeshes.Box, stone * 0.95f, basePoint + Vector3.up * 0.18f, new Vector3(1.05f, 0.36f, 1.05f),
                        new Vector3(0f, Range(0f, 90f), 0f), top: moss, groundY: 0f);
                    target.Piece(ProceduralMeshes.Frustum(0.88f, 12), stone, basePoint + Vector3.up * 0.36f, new Vector3(0.72f, height, 0.72f), top: moss, groundY: 0f);
                    if (intact)
                    {
                        target.Piece(ProceduralMeshes.Frustum(1f, 12), style.gold, basePoint + Vector3.up * (0.36f + height), new Vector3(0.86f, 0.16f, 0.86f), spec: 1f);
                        target.Piece(ProceduralMeshes.Box, stone, basePoint + Vector3.up * (0.75f + height), new Vector3(1.05f, 0.42f, 1.05f), top: moss, groundY: 0f);
                    }
                    else
                    {
                        // Broken top: a chipped chunk and rubble at the foot.
                        target.Piece(ProceduralMeshes.Faceted(random.Next(6)), stone, basePoint + Vector3.up * (0.36f + height), new Vector3(0.7f, 0.45f, 0.7f),
                            new Vector3(Range(-20f, 20f), Range(0f, 360f), 0f), top: moss, groundY: 0f);
                        for (int r = 0; r < 3; r++)
                            target.Piece(ProceduralMeshes.Faceted(random.Next(6)), stone * 0.95f,
                                basePoint + new Vector3(Range(-1.2f, 1.2f), -0.05f, Range(-1.2f, 1.2f)), Vector3.one * Range(0.25f, 0.5f),
                                new Vector3(Range(0f, 360f), Range(0f, 360f), 0f), top: moss, groundY: 0f);
                    }
                }
                if (arch)
                {
                    // Lintel spanning the two intact pillars.
                    Vector3 a = c + new Vector3(Mathf.Cos(start * Mathf.Deg2Rad), 0f, Mathf.Sin(start * Mathf.Deg2Rad)) * 2.8f;
                    Vector3 b = c + new Vector3(Mathf.Cos((start + 360f / 7f) * Mathf.Deg2Rad), 0f, Mathf.Sin((start + 360f / 7f) * Mathf.Deg2Rad)) * 2.8f;
                    Vector3 middle = (a + b) * 0.5f + Vector3.up * 5.3f;
                    float span = Vector3.Distance(a, b) + 1.2f;
                    float lintelYaw = Mathf.Atan2(b.x - a.x, b.z - a.z) * Mathf.Rad2Deg;
                    target.Piece(ProceduralMeshes.Box, style.ruinStone, middle, new Vector3(0.9f, 0.6f, span), new Vector3(0f, lintelYaw, 0f), top: moss, groundY: 0f);
                    target.Piece(ProceduralMeshes.Box, style.gold, middle - Vector3.up * 0.33f, new Vector3(0.95f, 0.08f, span * 0.96f),
                        new Vector3(0f, lintelYaw, 0f), spec: 1f);
                }
                // A fallen column in pieces and a couple of bushes reclaiming the shrine.
                Vector3 fallen = c + new Vector3(Range(-1.5f, 1.5f), 0.32f, Range(-1.5f, 1.5f));
                float fallYaw = Range(0f, 360f);
                Vector3 along = Quaternion.Euler(0f, fallYaw, 0f) * Vector3.forward;
                for (int s = 0; s < 3; s++)
                    target.Piece(ProceduralMeshes.Frustum(0.9f, 12), style.ruinStone * 0.93f, fallen + along * s * 1.15f + Vector3.up * Range(-0.05f, 0.05f),
                        new Vector3(0.65f, 1.0f, 0.65f), new Vector3(90f, fallYaw + Range(-8f, 8f), 0f), top: moss, groundY: 0f);
                Bush(target, c + new Vector3(Range(-3.5f, 3.5f), 0f, Range(-3.5f, 3.5f)), Range(0.8f, 1.2f));
                Bush(target, c + new Vector3(Range(-3.5f, 3.5f), 0f, Range(-3.5f, 3.5f)), Range(0.7f, 1.1f));
            }
        }

        private void BuildBackdrop(Shapes target)
        {
            // Mountain ring inside the far fog band so it reads as a soft silhouette.
            for (int i = 0; i < 26; i++)
            {
                float angle = i / 26f * Mathf.PI * 2f + Range(-0.08f, 0.08f);
                float radius = Range(118f, 145f);
                Vector3 foot = new Vector3(Mathf.Cos(angle) * radius, -2f, 12f + Mathf.Sin(angle) * radius);
                float height = Range(35f, 72f), width = Range(45f, 85f);
                target.Cone(0.08f, style.mountain, foot, new Vector3(width, height, width * Range(0.8f, 1.2f)), new Vector3(0f, Range(0f, 360f), 0f), 7);
                target.Cone(0f, Color.Lerp(style.mountain, Color.white, 0.6f), foot + Vector3.up * height * 0.72f,
                    new Vector3(width * 0.3f, height * 0.3f, width * 0.3f), new Vector3(0f, Range(0f, 360f), 0f), 7);
            }
            // The Lightbringer citadel behind the allied stronghold.
            Vector3 citadel = new Vector3(0f, 0f, -72f);
            for (int i = -2; i <= 2; i++)
            {
                float height = 22f - Mathf.Abs(i) * 4f;
                Vector3 tower = citadel + new Vector3(i * 9f, Height(i * 9f, citadel.z) - 1f, Mathf.Abs(i) * 3f);
                target.Cone(0.9f, style.ivory, tower, new Vector3(5f, height, 5f), segments: 10);
                target.Cone(0f, style.royalBlue, tower + Vector3.up * height, new Vector3(6f, 7f, 6f), segments: 10);
                target.Cone(0f, style.gold, tower + Vector3.up * (height + 6.6f), new Vector3(0.6f, 2.5f, 0.6f), segments: 6, spec: 1f);
            }
            target.Add(Shape.Cube, style.ivory, citadel + new Vector3(0f, Height(0f, citadel.z) + 6f, 2f), new Vector3(36f, 12f, 6f));
            // Corrupted spires behind the enemy stronghold.
            Vector3 spires = new Vector3(0f, 0f, 92f);
            for (int i = -3; i <= 3; i++)
            {
                float height = 20f + (3 - Mathf.Abs(i)) * 6f;
                Vector3 spire = spires + new Vector3(i * 8f, Height(i * 8f, spires.z) - 2f, Mathf.Abs(i) * 2.5f);
                target.Cone(0.05f, style.corruptStone, spire, new Vector3(5f, height, 5f), new Vector3(0f, i * 13f, 0f), 6);
                target.Add(Shape.Cube, style.corruptGlow, spire + Vector3.up * height * 0.6f, new Vector3(0.6f, 2.5f, 0.6f), emission: 0.8f);
            }
        }

        // ---------- Helpers ----------

        private MeshRenderer AddMesh(string name, Mesh mesh, Material material, bool shadows)
        {
            owned.Add(mesh);
            GameObject item = new GameObject(name);
            item.transform.SetParent(transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        private float DistanceToLanes(Vector2 point)
        {
            float best = float.MaxValue;
            foreach ((Vector2 a, Vector2 b) in lanes)
            {
                Vector2 segment = b - a;
                float t = segment.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / segment.sqrMagnitude) : 0f;
                best = Mathf.Min(best, Vector2.Distance(point, a + segment * t));
            }
            return best;
        }

        private static Vector2 Flat(Vector3 point) => new Vector2(point.x, point.z);
        // Terrain and props are authored in this component's local space; Paths come in world space.
        private Vector2 Local(Vector3 world) => Flat(transform.InverseTransformPoint(world));

        private void OnDestroy()
        {
            foreach (Object item in owned)
                if (item != null)
                {
                    if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
                }
            owned.Clear();
        }
    }
}
