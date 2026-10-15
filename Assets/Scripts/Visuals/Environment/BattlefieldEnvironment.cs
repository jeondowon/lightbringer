using System.Collections.Generic;
using Lightbringer.Pathing;
using UnityEngine;
using UnityEngine.Rendering;
using Shapes = Lightbringer.Visuals.PropShapes.Shapes;
using Shape = Lightbringer.Visuals.PropShapes.Shape;
using Surface = Lightbringer.Visuals.PropShapes.Surface;

namespace Lightbringer.Visuals
{
    // Art Pass battlefield dressing: a terrain whose play area stays perfectly flat (so movement, deployment
    // and Paths are unchanged) rising into hills outside it, dirt roads along the Paths, natural stronghold grounds,
    // corrupted enemy grounds, trees/rocks/ruins kept clear of the lanes, and a hazy mountain and castle backdrop.
    // Deterministic per stage. Owns the meshes/materials it creates and destroys them with the battlefield.
    public sealed class BattlefieldEnvironment : MonoBehaviour
    {
        // Flat playable rectangle (world XZ): all Paths, bases and the hero start lie inside it.
        public const float PlayHalfWidth = Core.BattlefieldLayout.HalfWidth,
            PlayMinZ = Core.BattlefieldLayout.MinZ, PlayMaxZ = Core.BattlefieldLayout.MaxZ;
        private const float LaneClearance = 4.5f;

        private readonly List<Object> owned = new List<Object>();
        // Lower graphics tiers skip grass chunks far from the camera (see GraphicsQuality.GrassDistance).
        private readonly List<(MeshRenderer renderer, Bounds bounds)> grass = new List<(MeshRenderer, Bounds)>();
        private float grassCheck;
        private readonly List<(Vector2 a, Vector2 b)> lanes = new List<(Vector2, Vector2)>();
        private readonly List<Vector2> keepClear = new List<Vector2>();
        private readonly List<(Vector2 point, float radius)> propFootprints = new List<(Vector2, float)>();
        private static readonly int LanesId = Shader.PropertyToID("_LB_Lanes");
        private static readonly int LaneCountId = Shader.PropertyToID("_LB_LaneCount");
        private const int MaxShaderLanes = 16;
        // clearPoints[0] is the allied deploy gate, [1] the enemy gate; the strongholds stand behind them.
        private const float StrongholdBehindGate = 4.5f, EnemyStrongholdBehindGate = 5f;

        private Vector2? alliedGate, enemyGate;
        private System.Random random;
        private float seed;
        private ArtStyleLibrary style;
        private ForestEnvironmentProfile forest;
        private ActTwoScenery actTwo;
        private ActOneScenery actOne;
        private bool beaconClearing;
        private Vector2? beaconSite;
        private Material instanceSky, previousSky;

        public MeshCollider Terrain { get; private set; }
        // Victory beacon built for stages other than the opening glade (stage one's lives in the prop dressing).
        public BeaconSanctuary Beacon { get; private set; }
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
            actTwo = GetComponentInParent<ActTwoScenery>(true);
            actOne = GetComponentInParent<ActOneScenery>(true);
            BattlefieldBackdropProfile panorama = library.FindBackdrop(stage);
            // An explicit stage profile can refine a battlefield inside a larger backdrop region.
            forest = library.forestEnvironment != null && library.forestEnvironment.AppliesTo(stage)
                ? library.forestEnvironment
                : panorama != null && panorama.forest != null && panorama.forest.IsReady
                    ? panorama.forest : null;
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
            if (clearPoints.Length > 0) alliedGate = Local(clearPoints[0]);
            if (clearPoints.Length > 1) enemyGate = Local(clearPoints[1]);
            beaconClearing = stage == 1 && forest != null && forest.openingGlade;
            // Stage one keeps its authored glade; every other stage gets a beacon on a quiet flank, off the lanes.
            if (!beaconClearing) beaconSite = FindBeaconSite();
            PublishLanes();

            BuildTerrain();
            if (!beaconClearing) BuildBeacon();
            if (actTwo != null) actTwo.Dress(this, paths);
            else
            {
                BattlefieldPropDressing.Build(style.battlefieldProps, this, AlliedKeep, EnemyKeep, paths.Length, beaconClearing);
                if (actOne != null) actOne.Dress(this, paths);
            }
            // Imported forest tufts replace the old solid-colour blade carpet.
            if (forest == null && style.grassMaterial != null) BuildGrass();
            Shapes near = new Shapes(), far = new Shapes();
            if (forest == null)
            {
                if (actTwo == null) ScatterTrees(near, far);
                // Act 2 photoscanned rocks replace the procedural ones.
                if (actTwo == null || !actTwo.UsesPhotoscans) ScatterRocks(near);
            }
            else ForestEnvironmentDressing.Build(forest, this, stage);

            BuildDistantFoothills();
            AddMesh("Props (Near)", near.Bake("LB Env Props Near"), style.propsMaterial, true);
            AddMesh("Props (Far)", far.Bake("LB Env Props Far"), style.propsMaterial, false);
            if (style.skyMaterial != null)
            {
                Material sky = new Material(style.skyMaterial) { name = "Sky (Instance)" };
                sky.SetColor("_HorizonColor", style.fogColor);
                // Match the sun glow to the styled sun angle (applied after the environment is built).
                sky.SetVector("_SunDirection", -(Quaternion.Euler(style.sunEuler) * Vector3.forward));
                if (panorama != null) panorama.ApplyTo(sky);
                owned.Add(sky);
                previousSky = RenderSettings.skybox;
                instanceSky = sky;
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

        // Match the actual terrain triangles, not the smooth function between mesh vertices.
        public float SurfaceHeight(float x, float z)
        {
            const float dx = 2.2f, dz = 2.45f;
            float gx = Mathf.Clamp((x + 110f) / dx, 0f, 99.9999f);
            float gz = Mathf.Clamp((z + 95f) / dz, 0f, 99.9999f);
            int c = Mathf.FloorToInt(gx), r = Mathf.FloorToInt(gz);
            float u = gx - c, v = gz - r;
            float x0 = -110f + c * dx, z0 = -95f + r * dz;
            float a = Height(x0, z0), b = Height(x0, z0 + dz);
            float right = Height(x0 + dx, z0);
            return u + v <= 1f ? a + u * (right - a) + v * (b - a)
                : Height(x0 + dx, z0 + dz) * (u + v - 1f) + right * (1f - v) + b * (1f - u);
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
            // Terrain shader masks: lane distance (fallback for the per-pixel lanes), plaza, corruption, "present".
            List<Vector4> masks = new List<Vector4>(vertices.Length);
            foreach (Vector3 vertex in vertices)
            {
                Vector2 flat = Flat(vertex);
                // No stone paving: gates and sanctuaries use the stage's natural ground as well.
                masks.Add(new Vector4(Mathf.Min(DistanceToLanes(flat), 30f), 0f, CorruptionMask(flat), 1f));
            }
            mesh.SetUVs(0, masks);
            mesh.RecalculateBounds();
            MeshRenderer renderer = AddMesh("Terrain", mesh,
                forest != null && forest.terrain != null ? forest.terrain : style.terrainMaterial, false);
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
            // Hills darken with height; steep faces turn to rock.
            color = Color.Lerp(color, style.grassDark * 0.8f, Mathf.Clamp01(point.y / 20f) * 0.5f);
            color = Color.Lerp(color, style.rock, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 0.6f, normal.y)));
            color.a = road; // the terrain shader uses alpha as the road mask (gravel, no flowers)
            return color;
        }

        private float RoadMask(Vector2 flat) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.8f, 3.4f, DistanceToLanes(flat)));

        // Keep grass sparse around structures without painting stone onto the terrain.
        private float GroundClearingMask(Vector2 flat)
        {
            float plaza = 0f;
            if (beaconClearing)
            {
                float sanctuary = Vector2.Distance(flat, BeaconSanctuary.Centre);
                Vector2 approach = new Vector2(Mathf.Clamp(flat.x, BeaconSanctuary.Centre.x, 0f), BeaconSanctuary.Centre.y);
                plaza = Mathf.Max(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 7f, sanctuary)),
                    1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.2f, 2.5f, Vector2.Distance(flat, approach))));
            }
            if (beaconSite.HasValue)
                plaza = Mathf.Max(plaza, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.6f, 5.6f, Vector2.Distance(flat, beaconSite.Value))));
            foreach (Vector2 clear in keepClear)
            {
                plaza = Mathf.Max(plaza, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(6f, 8.5f, Vector2.Distance(flat, clear))));
            }
            return plaza;
        }

        // Quiet flank spot: clear of every lane and gate, near mid-field, preferring the outer edge and the west.
        private Vector2 FindBeaconSite()
        {
            foreach (float minLane in new[] { 7f, 5.5f, 4.5f })
            {
                Vector2? best = null;
                float bestScore = float.MinValue;
                for (float x = -29f; x <= 29f; x += 1f)
                    for (float z = 14f; z <= 50f; z += 2f)
                    {
                        Vector2 point = new Vector2(x, z);
                        float lane = DistanceToLanes(point);
                        if (lane < minLane) continue;
                        bool nearGate = false;
                        foreach (Vector2 gate in keepClear) if (Vector2.Distance(point, gate) < 20f) nearGate = true;
                        if (nearGate) continue;
                        float score = Mathf.Min(lane, 11f) * 2f - Mathf.Abs(z - Core.BattlefieldLayout.CentreZ) * 0.2f
                            + Mathf.Abs(x) * 0.12f + (x < 0f ? 1.5f : 0f);
                        if (score > bestScore) { bestScore = score; best = point; }
                    }
                if (best.HasValue) return best.Value;
            }
            return new Vector2(-26f, Core.BattlefieldLayout.CentreZ);
        }

        private void BuildBeacon()
        {
            BattlefieldPropAsset asset = style.battlefieldProps != null ? style.battlefieldProps.Find(BattlefieldPropKind.AlliedBeacon) : null;
            if (asset == null || asset.prefab == null || Terrain == null || !beaconSite.HasValue) return;
            Vector2 site = beaconSite.Value;
            Transform group = new GameObject("Beacon Sanctuary").transform;
            group.SetParent(transform, false);
            GameObject instance = Instantiate(asset.prefab, group);
            instance.name = BattlefieldPropKind.AlliedBeacon.ToString();
            instance.transform.localPosition = new Vector3(site.x, SurfaceHeight(site.x, site.y) - 0.025f, site.y);
            instance.transform.localRotation = Quaternion.Euler(0f, site.x < 0f ? 35f : -35f, 0f);
            instance.transform.localScale = Vector3.one * 1.9f;
            ReserveProp(site, 7f);
            Beacon = BeaconSanctuary.Build(group, instance, this, site);
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
                    for (int i = 0; i < (actTwo != null ? 100 : 1500); i++)
                    {
                        float x = cx + Range(0f, chunk), z = cz + Range(0f, chunk);
                        float outside = OutsideDistance(x, z);
                        float density = outside <= 0f ? 1f : Mathf.Clamp01(1f - outside / 26f) * 0.5f;
                        Vector2 flat = new Vector2(x, z);
                        if (!ClearOfProps(flat, 0.25f)) continue;
                        if (density <= 0f || random.NextDouble() > density) continue;
                        float road = RoadMask(flat);
                        if (random.NextDouble() < Mathf.Max(road * 1.3f, GroundClearingMask(flat))) continue;
                        float slope = Mathf.Abs(Height(x + 0.5f, z) - Height(x - 0.5f, z)) + Mathf.Abs(Height(x, z + 0.5f) - Height(x, z - 0.5f));
                        if (slope > 0.7f) continue;
                        Vector3 root = new Vector3(x, Height(x, z), z);
                        Color ground = GroundColor(root, Vector3.up);
                        // Corrupted ground near the enemy stronghold grows sparse, withered grass.
                        float corruption = CorruptionMask(flat);
                        if (random.NextDouble() < corruption * 0.75f) continue;
                        Color blade = Color.Lerp(Color.Lerp(ground, style.grassLight, 0.3f), style.dirt * 1.1f, road * 0.6f);
                        blade = Color.Lerp(blade, Color.Lerp(style.corruptStone, style.dirt, 0.4f) * 1.2f, corruption);
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
                    grass.Add((renderer, mesh.bounds));
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
            if (actTwo != null) height *= .6f;
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

        public bool ClearOfPlay(Vector2 point, float lane, float bases)
        {
            if (!ClearOfProps(point, 1.5f)) return false;
            // Leave both expanded castle courtyards free of scattered props as well as retired dressing.
            if (AlliedKeep.HasValue && Mathf.Abs(point.x - AlliedKeep.Value.x) < 12f
                && Mathf.Abs(point.y - AlliedKeep.Value.y) < 10f) return false;
            if (EnemyKeep.HasValue && Mathf.Abs(point.x - EnemyKeep.Value.x) < 12f
                && Mathf.Abs(point.y - EnemyKeep.Value.y) < 10f) return false;
            if (DistanceToLanes(point) < lane) return false;
            foreach (Vector2 clear in keepClear) if (Vector2.Distance(point, clear) < bases) return false;
            return true;
        }

        public void ReserveProp(Vector2 point, float radius) => propFootprints.Add((point, radius));

        public bool ClearOfProps(Vector2 point, float radius)
        {
            foreach (var footprint in propFootprints)
                if (Vector2.Distance(point, footprint.point) < radius + footprint.radius) return false;
            return true;
        }

        public bool ClearForProp(Vector2 point, float radius)
        {
            if (DistanceToLanes(point) < LaneClearance + radius || !ClearOfProps(point, radius)) return false;
            foreach (Vector2 gate in keepClear)
                if (Vector2.Distance(point, gate) < 6f + radius) return false;
            foreach (Vector2? keep in new[] { AlliedKeep, EnemyKeep })
                if (keep.HasValue && Mathf.Abs(point.x - keep.Value.x) < 9f + radius
                    && Mathf.Abs(point.y - keep.Value.y) < 8f + radius) return false;
            // Large plinths stay on the flat field; a tiny edge slope can be buried without floating.
            float y = SurfaceHeight(point.x, point.y);
            foreach (Vector2 offset in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
                if (Mathf.Abs(SurfaceHeight(point.x + offset.x * radius, point.y + offset.y * radius) - y) > 0.2f) return false;
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
                if (!ClearOfProps(new Vector2(x, z), 8f)) continue;
                Tree(d < 22f ? near : far, new Vector3(x, Height(x, z), z), Range(1.2f, 2.4f));
            }
            for (int i = 0; i < 60; i++)
            {
                Vector2 point = new Vector2(Range(-PlayHalfWidth + 2f, PlayHalfWidth - 2f), Range(PlayMinZ + 2f, PlayMaxZ - 2f));
                if (!ClearOfPlay(point, 8f, 13f) || !ClearOfProps(point, 6f)) continue;
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
            // Foliage never takes the stone pattern of the rocks or ruins it grows beside.
            Surface pattern = target.Pattern;
            target.Pattern = Surface.None;
            for (int c = 0; c < clumps; c++)
            {
                Vector3 offset = new Vector3(Range(-0.45f, 0.45f), Range(0f, 0.2f), Range(-0.45f, 0.45f)) * scale;
                target.Piece(ProceduralMeshes.Lumpy(random.Next(6)), leaves, centre + offset, new Vector3(1.2f, 0.85f, 1.2f) * scale * Range(0.8f, 1.1f),
                    new Vector3(0f, Range(0f, 360f), 0f), sway: 0.35f, softCenter: centre, softBlend: 0.7f, groundY: ground.y);
            }
            target.Pattern = pattern;
        }

        private void ScatterRocks(Shapes target)
        {
            Color moss = Color.Lerp(style.grassDark, style.foliage, 0.4f);
            target.Pattern = Surface.Rock;
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
            target.Pattern = Surface.None;
        }

        private void BuildDistantFoothills()
        {
            // Continue the terrain's exact perimeter into distant rolling hills, with no colliders.
            // The outer edge is fully in fog; unlike the old cone mountains it has no repeated peaks.
            const int perimeter = 400, rings = 5;
            Vector3[] vertices = new Vector3[perimeter * rings];
            Color[] colors = new Color[vertices.Length];
            var masks = new List<Vector4>(vertices.Length);
            Vector2 centre = new Vector2(0f, 27.5f);
            for (int ring = 0; ring < rings; ring++)
                for (int i = 0; i < perimeter; i++)
                {
                    int side = i / 100;
                    float t = i % 100 / 100f;
                    Vector2 edge = side == 0 ? new Vector2(Mathf.Lerp(-110f, 110f, t), -95f)
                        : side == 1 ? new Vector2(110f, Mathf.Lerp(-95f, 150f, t))
                        : side == 2 ? new Vector2(Mathf.Lerp(110f, -110f, t), 150f)
                        : new Vector2(-110f, Mathf.Lerp(150f, -95f, t));
                    Vector2 point = centre + (edge - centre) * (1f + ring * 0.42f);
                    float h = Height(point.x, point.y) + ring * 1.2f;
                    int index = ring * perimeter + i;
                    vertices[index] = new Vector3(point.x, h, point.y);
                    colors[index] = GroundColor(vertices[index], Vector3.up);
                    masks.Add(new Vector4(30f, 0f, 0f, 1f));
                }
            int[] triangles = new int[(rings - 1) * perimeter * 6];
            int cursor = 0;
            for (int ring = 0; ring < rings - 1; ring++)
                for (int i = 0; i < perimeter; i++)
                {
                    int a = ring * perimeter + i, b = ring * perimeter + (i + 1) % perimeter;
                    int c = a + perimeter, d = b + perimeter;
                    triangles[cursor++] = a; triangles[cursor++] = b; triangles[cursor++] = c;
                    triangles[cursor++] = b; triangles[cursor++] = d; triangles[cursor++] = c;
                }
            Mesh mesh = new Mesh { name = "LB Distant Foothills", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.SetUVs(0, masks);
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AddMesh("Distant Foothills", mesh,
                forest != null && forest.terrain != null ? forest.terrain : style.terrainMaterial, false);
        }

        private void OnEnable()
        {
            if (instanceSky != null && RenderSettings.skybox != instanceSky)
            {
                previousSky = RenderSettings.skybox;
                RenderSettings.skybox = instanceSky;
            }
        }

        private void OnDisable()
        {
            if (instanceSky != null && RenderSettings.skybox == instanceSky) RenderSettings.skybox = previousSky;
        }

        // ---------- Stronghold grounds ----------

        private Vector2? AlliedKeep => alliedGate.HasValue ? alliedGate.Value + Vector2.down * StrongholdBehindGate : (Vector2?)null;
        private Vector2? EnemyKeep => enemyGate.HasValue ? enemyGate.Value + Vector2.up * EnemyStrongholdBehindGate : (Vector2?)null;

        // 1 around the enemy stronghold, fading out by ~38 m: drives the corrupted soil and withered grass.
        private float CorruptionMask(Vector2 flat)
        {
            // Act 3 is the Demon King's domain: all ground uses the dark soil found at the enemy gate.
            if (actTwo != null && actTwo.IsActThree) return 1f;
            if (!EnemyKeep.HasValue) return 0f;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 38f, Vector2.Distance(flat, EnemyKeep.Value)));
        }

        // Lane segments for the terrain shader's per-pixel road (world XZ).
        private void PublishLanes()
        {
            Vector4[] data = new Vector4[MaxShaderLanes];
            int count = Mathf.Min(lanes.Count, MaxShaderLanes);
            for (int i = 0; i < count; i++)
            {
                Vector3 a = transform.TransformPoint(new Vector3(lanes[i].a.x, 0f, lanes[i].a.y));
                Vector3 b = transform.TransformPoint(new Vector3(lanes[i].b.x, 0f, lanes[i].b.y));
                data[i] = new Vector4(a.x, a.z, b.x, b.z);
            }
            Shader.SetGlobalVectorArray(LanesId, data);
            Shader.SetGlobalFloat(LaneCountId, count);
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

        // Four times a second is plenty: the camera moves at most a few metres between checks.
        private void Update()
        {
            if ((grassCheck -= Time.unscaledDeltaTime) > 0f || grass.Count == 0) return;
            grassCheck = 0.25f;
            Camera view = Camera.main;
            float range = Lightbringer.Core.GraphicsQuality.GrassDistance;
            Vector3 eye = view != null ? view.transform.position : Vector3.zero;
            foreach ((MeshRenderer chunk, Bounds bounds) in grass)
            {
                if (chunk == null) continue;
                bool visible = range <= 0f || view == null || bounds.SqrDistance(chunk.transform.InverseTransformPoint(eye)) <= range * range;
                if (chunk.enabled != visible) chunk.enabled = visible;
            }
        }

        private void OnDestroy()
        {
            OnDisable();
            foreach (Object item in owned)
                if (item != null)
                {
                    if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
                }
            owned.Clear();
            // Roads fall back to the mesh masks once this battlefield is gone.
            Shader.SetGlobalFloat(LaneCountId, 0f);
        }
    }
}
