using System.Collections.Generic;
using Lightbringer.Pathing;
using UnityEngine;
using UnityEngine.Rendering;
using Shapes = Lightbringer.Visuals.PropShapes.Shapes;
using Shape = Lightbringer.Visuals.PropShapes.Shape;

namespace Lightbringer.Visuals
{
    // CampaignAtlas_Acts_v1/v2: inland sacred ruins -> occupied supply roads -> Warlord citadel (Act 2, stages 9-16).
    // Act 3 (17-24) reuses the same ruined-region builders, fully devastated: no surviving foliage, black valley mist,
    // dark fissures, outer strongholds and the final citadel (ActTwoScenery.ActThree.cs).
    // Only battlefield-local copies are edited; shared Act 1 art and character materials stay intact.
    public sealed partial class ActTwoScenery : MonoBehaviour
    {
        private readonly List<Object> owned = new List<Object>();
        private ArtStyleLibrary palette;
        private BattlefieldEnvironment terrain;
        private System.Random random;
        public int Stage { get; private set; }
        public int DeadTrees { get; private set; }
        public int Stumps { get; private set; }
        public int Landmarks { get; private set; }
        public int Bridges { get; private set; }
        public int ImportedTrees { get; private set; }
        public int ImportedLandmarks { get; private set; }
        public ArtStyleLibrary Palette => palette;
        public int Fissures { get; private set; }
        public static bool AppliesTo(int stage) => stage >= 9 && stage <= 24;
        public bool IsActThree => Stage >= 17;
        // Stages whose Paths cross a shallow river on stone bridges.
        public bool HasRiverCrossing => Stage == 15 || Stage == 18;

        public static ArtStyleLibrary CreatePalette(Transform root, ArtStyleLibrary source, int stage)
        {
            if (!AppliesTo(stage) || source == null) return source;
            ActTwoScenery scenery = root.gameObject.AddComponent<ActTwoScenery>();
            scenery.Stage = stage;
            scenery.palette = Instantiate(source);
            scenery.palette.name = stage >= 17 ? "Act Three Demon Domain Palette" : "Act Two Ashlands Palette";
            scenery.palette.hideFlags = HideFlags.DontSave;
            scenery.owned.Add(scenery.palette);
            ArtStyleLibrary s = scenery.palette;
            s.forestEnvironment = null;
            // Keep only a panorama made for this act (Act 2 has one; Act 3 stays on the gradient sky).
            BattlefieldBackdropProfile backdrop = source.FindBackdrop(stage);
            s.backdrops = backdrop != null ? new[] { backdrop } : new BattlefieldBackdropProfile[0];
            s.grassLight = new Color(.40f, .39f, .31f);
            s.grassDark = new Color(.25f, .26f, .22f);
            s.dirt = new Color(.43f, .39f, .33f);
            s.plazaStone = new Color(.48f, .49f, .47f);
            s.rock = new Color(.35f, .38f, .39f);
            s.ruinStone = new Color(.48f, .50f, .47f);
            s.bark = new Color(.20f, .19f, .17f);
            s.foliage = new Color(.29f, .32f, .23f);
            s.foliageDark = new Color(.20f, .24f, .19f);
            s.corruptStone = new Color(.24f, .25f, .25f);
            s.sunColor = new Color(.83f, .87f, .91f);
            s.sunIntensity = 1.1f;
            s.ambientSky = new Color(.43f, .48f, .55f);
            s.ambientEquator = new Color(.37f, .39f, .40f);
            s.ambientGround = new Color(.22f, .23f, .22f);
            s.fogColor = new Color(.40f, .45f, .49f);
            s.fogStart = 68;
            s.fogEnd = 205;
            s.cloudShadowStrength = .42f;
            s.propsMaterial = scenery.Copy(source.propsMaterial);
            if (s.propsMaterial != null)
            {
                s.propsMaterial.SetFloat("_RimStrength", .08f);
                s.propsMaterial.SetFloat("_PatternStrength", .5f);
                s.propsMaterial.SetFloat("_OutlineWidth", .008f);
                s.propsMaterial.SetColor("_ShadowColor", new Color(.59f, .62f, .64f));
                s.propsMaterial.SetColor("_BaseColor", new Color(.72f, .74f, .72f));
            }
            s.grassMaterial = scenery.Copy(source.grassMaterial);
            if (s.grassMaterial != null)
                s.grassMaterial.SetColor("_TipTint", new Color(.54f, .55f, .46f));
            Material terrainSource = s.actTwoEnvironment != null && s.actTwoEnvironment.terrain != null
                ? s.actTwoEnvironment.terrain : source.terrainMaterial;
            s.terrainMaterial = scenery.Copy(terrainSource);
            if (s.terrainMaterial != null)
            {
                s.terrainMaterial.SetFloat("_FlowerAmount", 0);
                if (terrainSource == source.terrainMaterial) s.terrainMaterial.SetFloat("_ForestTextureStrength", 0);
                s.terrainMaterial.SetFloat("_Weathering", .45f);
                s.terrainMaterial.SetColor("_CorruptGlow", Color.black);
                s.terrainMaterial.SetColor("_CorruptSoil", s.corruptStone);
            }
            s.skyMaterial = scenery.Copy(source.skyMaterial);
            if (s.skyMaterial != null)
            {
                s.skyMaterial.SetFloat("_PanoramaEnabled", 0);
                s.skyMaterial.SetColor("_ZenithColor", new Color(.23f, .28f, .34f));
                s.skyMaterial.SetColor("_HorizonColor", s.fogColor);
                s.skyMaterial.SetColor("_SunColor", new Color(.62f, .66f, .69f));
                s.skyMaterial.SetFloat("_SunStrength", .08f);
            }
            scenery.ApplyRealism();
            if (scenery.IsActThree) scenery.DarkenForActThree();
            return s;
        }

        private Material Copy(Material source)
        {
            if (source == null) return null;
            Material result = new Material(source) { name = source.name + " (Act Two)", hideFlags = HideFlags.DontSave };
            owned.Add(result);
            return result;
        }

        internal void Dress(BattlefieldEnvironment environment, WaypointPath[] paths)
        {
            terrain = environment;
            random = new System.Random(Stage * 4919);
            BuildLandmarks(paths);
            if (!IsActThree) BuildEnemyRearCover();
            if (!IsActThree && UsesPhotoscans) BuildHillBoulders();
            BuildDeadWood();
            if (IsActThree) BuildFissures(); else BuildFields();
            BuildPhotoscans();
            if (!IsActThree) BuildSlopeCover();
            if (HasRiverCrossing) BuildRiverMist();
            if (IsActThree) BuildValleyDarkness();
        }

        private float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        private Vector3 Ground(float x, float z) => new Vector3(x, terrain.SurfaceHeight(x, z), z);

        private void BuildDeadWood()
        {
            const float treeSize = 1.6f;
            const float crownRadius = 3f * treeSize;
            Shapes wood = new Shapes(), surviving = new Shapes();
            int count = Stage == 20 ? 380 : IsActThree ? 300 : 260;
            for (int i = 0; i < count; i++)
            {
                float x = Range(-85, 85), z = Range(-55, 125);
                Vector2 point = new Vector2(x, z);
                if (!terrain.ClearOfPlay(point, 3.5f + crownRadius, 11f + crownRadius)
                    || !terrain.ClearOfProps(point, crownRadius)) continue;
                Vector3 foot = Ground(x, z);
                float height = Range(3.5f, 7.5f), yaw = Range(0, 360);
                if (i % (IsActThree ? 3 : 4) == 0)
                {
                    ActTwoScatterAsset[] scannedStumps = UsesPhotoscans ? ScanSet(ActTwoScatterKind.Stump) : null;
                    if (scannedStumps != null && scannedStumps.Length > 0)
                        PlaceScan(scannedStumps[0], point, Range(.9f, 1.4f), yaw);
                    else
                        wood.Cone(.7f, palette.bark, foot, new Vector3(.65f, Range(.35f, .9f), .65f), segments: 7);
                    Stumps++;
                    continue;
                }
                GameObject[] trees = palette.actTwoEnvironment != null ? palette.actTwoEnvironment.trees : null;
                if (trees != null && trees.Length > 0 && trees[i % trees.Length] != null)
                {
                    GameObject tree = Instantiate(trees[i % trees.Length], terrain.transform, false);
                    tree.name = "Dry Tree " + i;
                    tree.transform.localPosition = foot;
                    tree.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                    // Crown clearance is bounded too, so wide varieties never overhang the lanes.
                    Bounds bounds = default;
                    bool first = true;
                    foreach (Renderer renderer in tree.GetComponentsInChildren<Renderer>(true))
                    {
                        if (first) { bounds = renderer.bounds; first = false; }
                        else bounds.Encapsulate(renderer.bounds);
                    }
                    float crown = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
                    // Apply after the old crown cap so even wide varieties grow by the full treeSize factor.
                    float scale = Mathf.Min(height, 3f / Mathf.Max(.01f, crown)) * treeSize;
                    tree.transform.localScale = Vector3.one * scale;
                    terrain.ReserveProp(point, Mathf.Max(.6f, crown * scale));
                    ImportedTrees++;
                    DeadTrees++;
                    continue;
                }
                height *= treeSize;
                wood.Cone(.28f, palette.bark, foot, new Vector3(.45f * treeSize, height, .45f * treeSize), new Vector3(4, yaw, 3), segments: 7);
                for (int branch = 0; branch < 4; branch++)
                {
                    float angle = yaw + branch * 100;
                    wood.Cone(.12f, palette.bark * 1.08f, foot + Vector3.up * height * (.32f + branch * .13f),
                        new Vector3(.19f * treeSize, height * .42f, .19f * treeSize), new Vector3(0, angle, 48), segments: 5);
                }
                // A little olive-grey life remains in Act 2; Act 3 is fully devastated past its first forest edge.
                if (i % 7 == 0 && (!IsActThree || (Stage == 17 && z < 20)))
                    surviving.Piece(ProceduralMeshes.Lumpy(i % 6), palette.foliage,
                        foot + Vector3.up * height * .77f, new Vector3(2.1f, .7f, 1.7f) * treeSize, new Vector3(0, yaw, 0));
                terrain.ReserveProp(point, crownRadius);
                DeadTrees++;
            }
            MeshGroup("Sparse Bare Trees and Shattered Stumps", wood);
            MeshGroup("Surviving Olive Grey Foliage", surviving);
        }

        private void BuildFields()
        {
            Shapes fields = new Shapes();
            // Photoscanned Act 2 ground already carries the burned soil; the flat toon ash patches would read as boards.
            int patches = UsesPhotoscans ? 0 : 38;
            for (int i = 0; i < patches; i++)
            {
                float x = Range(-31, 31), z = Range(-5, 68);
                Vector2 point = new Vector2(x, z);
                if (!terrain.ClearForProp(point, 2.6f)) continue;
                Color earth = i % 3 == 0 ? new Color(.16f, .17f, .16f) : palette.dirt * .62f;
                // Thin irregular ash patches and old furrows lie on the terrain, with no extra colliders.
                fields.Piece(ProceduralMeshes.Faceted(i % 6), earth, Ground(x, z) + Vector3.up * .018f,
                    new Vector3(Range(2, 4), .025f, Range(2, 5)), new Vector3(0, Range(0, 360), 0));
                if (i % 3 != 0)
                    for (int row = 0; row < 4; row++)
                        fields.Add(Shape.Cube, earth * 1.2f, Ground(x - 1.4f + row * .9f, z) + Vector3.up * .025f,
                            new Vector3(.2f, .03f, 4));
            }
            MeshGroup("Scorched Earth and Abandoned Fields", fields, false);
        }

        private Transform MeshGroup(string label, Shapes shapes, bool shadows = true)
        {
            Mesh mesh = shapes.Bake("Act Two " + label);
            owned.Add(mesh);
            GameObject item = new GameObject(label);
            item.transform.SetParent(terrain.transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = palette.propsMaterial;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return item.transform;
        }

        private void BuildRiverMist()
        {
            if (palette.vfxMaterial == null) return;
            Material mist = Copy(palette.vfxMaterial);
            mist.SetFloat("_Shape", 6);
            mist.SetFloat("_Additive", 0);
            mist.SetFloat("_Softness", 1.4f);
            // This premultiplied shader uses vertex alpha for both RGB and coverage.
            mist.SetColor("_Color", IsActThree ? new Color(.30f, .33f, .37f, 1) : (UsesPhotoscans ? new Color(.34f, .37f, .40f, 1) : new Color(.52f, .59f, .64f, 1)));
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var uv = new List<Vector2>();
            var indices = new List<int>();
            for (int i = 0; i < 10; i++)
            {
                // Along the shallow crossing at z = 40 (stages 15 and 18 only), in uneven drifting banks.
                Vector3 centre = new Vector3(-30 + i * 6 + Range(-2, 2), .8f, 40 + Range(-1.5f, 1.5f));
                if (random.Next(3) == 0) continue;
                // Low horizontal haze plus a shallow vertical wisp, both softened by the existing puff shader.
                // Kept faint: a dense band reads as a glossy highlight running down the river.
                for (int plane = 0; plane < 2; plane++)
                {
                    Vector3 right = Vector3.right * Range(5, 9);
                    Vector3 up = plane == 0 ? Vector3.forward * Range(2, 3.5f) : Vector3.up * 1.2f;
                    int start = vertices.Count;
                    vertices.Add(centre - right - up); vertices.Add(centre + right - up);
                    vertices.Add(centre + right + up); vertices.Add(centre - right + up);
                    uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
                    for (int c = 0; c < 4; c++) colors.Add(new Color(1, 1, 1, plane == 0 ? .05f : .08f));
                    indices.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                }
            }
            Mesh mesh = new Mesh { name = "Low River Mist", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            owned.Add(mesh);
            GameObject item = new GameObject("Low Cold River Mist");
            item.transform.SetParent(terrain.transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mist;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void OnDestroy()
        {
            foreach (Object item in owned)
                if (item != null) { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
            owned.Clear();
        }
    }
}
