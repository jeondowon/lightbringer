using System.Collections.Generic;
using Lightbringer.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lightbringer.Visuals
{
    // Decorative forest around the existing playable terrain. No collision or route changes.
    // Normalized copies retain the supplier's LOD groups and share meshes/materials.
    public sealed class ForestEnvironmentDressing : MonoBehaviour
    {
        private readonly List<GameObject> groundCover = new List<GameObject>();
        private readonly List<Vector2> trunks = new List<Vector2>();
        private readonly List<Vector2> grassCentres = new List<Vector2>();
        private readonly Dictionary<Material, Material> grassMaterials = new Dictionary<Material, Material>();
        private System.Random random;
        private BattlefieldEnvironment terrain;
        private float nextCull;
        public int TreeCount { get; private set; }
        public int PropCount { get; private set; }
        public int GrassCount { get; private set; }

        public static ForestEnvironmentDressing Build(ForestEnvironmentProfile profile,
            BattlefieldEnvironment terrain, int stage)
        {
            GameObject root = new GameObject("Fantasy Worlds Forest");
            root.transform.SetParent(terrain.transform, false);
            ForestEnvironmentDressing forest = root.AddComponent<ForestEnvironmentDressing>();
            forest.terrain = terrain;
            forest.random = new System.Random(4517 + stage * 7919);
            forest.Generate(profile);
            return forest;
        }

        private float Range(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());

        private void Generate(ForestEnvironmentProfile profile)
        {
            // Staggered forest edges frame the front without covering the central camera corridor.
            foreach (int side in new[] { -1, 1 })
                for (int row = 0; row < 3; row++)
                    for (int i = 0; i < 11; i++)
                    {
                        Vector2 p = new Vector2(side * (23f + row * 18f + Range(-3f, 3f)),
                            -34f + i * 12f + Range(-4f, 4f));
                        if (!terrain.ClearOfPlay(p, 12f, 18f) || !terrain.ClearOfProps(p, 11f)) continue;
                        Spawn(profile.tree, p, Range(0.8f, 1.35f), 1.5f);
                        trunks.Add(p);
                        TreeCount++;
                    }

            // Low foliage and stones tie the canopy to the ground. Roads and deployment plazas stay bare.
            Scatter(profile.sapling, 30, 9f, 14f, 0.7f, 1.2f, false);
            Scatter(profile.rock, 40, 7f, 13f, 0.7f, 1.7f, false);
            Scatter(profile.stone, 70, 4.5f, 10f, 0.6f, 1.4f, false);
            Scatter(profile.plant, 95, 4f, 10f, 0.7f, 1.2f, true);
            BuildGrassPatches(profile.grass);
            if (profile.wind != null)
            {
                GameObject wind = Instantiate(profile.wind, transform);
                wind.name = "Forest Wind";
                wind.transform.localPosition = Vector3.zero;
                wind.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
            }
        }

        private void BuildGrassPatches(GameObject prefab)
        {
            // Cull whole patches, sharing the supplier mesh and instanced material between tufts.
            // Gaps between patches leave the ground texture visible instead of making a solid carpet.
            for (int attempt = 0, placed = 0; attempt < 1400 && placed < 140; attempt++)
            {
                Vector2 centre = new Vector2(Range(-33f, 33f), Range(-27f, 62f));
                if (!terrain.ClearOfPlay(centre, 5f, 11f)) continue;
                bool overlaps = false;
                foreach (Vector2 other in grassCentres)
                    if ((centre - other).sqrMagnitude < 16f) { overlaps = true; break; }
                if (overlaps) continue;
                GameObject patch = new GameObject("Forest Grass Patch");
                patch.transform.SetParent(transform, false);
                patch.transform.localPosition = new Vector3(centre.x, terrain.SurfaceHeight(centre.x, centre.y), centre.y);
                float radius = Range(2.2f, 3.5f);
                int count = random.Next(24, 37);
                for (int i = 0; i < count; i++)
                {
                    float angle = Range(0f, Mathf.PI * 2f);
                    float distance = Mathf.Sqrt(Range(0f, 1f)) * radius;
                    Vector2 point = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                    // Test every tuft, including the patch edge, with room for the blade width.
                    if (!terrain.ClearOfPlay(point, 4.8f, 10.5f) || NearTrunk(point)) continue;
                    float slope = Mathf.Abs(terrain.SurfaceHeight(point.x + 0.5f, point.y) - terrain.SurfaceHeight(point.x - 0.5f, point.y))
                        + Mathf.Abs(terrain.SurfaceHeight(point.x, point.y + 0.5f) - terrain.SurfaceHeight(point.x, point.y - 0.5f));
                    if (slope > 0.6f) continue;
                    GameObject tuft = Spawn(prefab, point, 1f);
                    foreach (Renderer renderer in tuft.GetComponentsInChildren<Renderer>())
                    {
                        Material[] materials = renderer.sharedMaterials;
                        for (int m = 0; m < materials.Length; m++)
                        {
                            Material source = materials[m];
                            if (source == null) continue;
                            if (!grassMaterials.TryGetValue(source, out Material shared))
                            {
                                shared = new Material(source) { name = "Forest Grass (Instance)", enableInstancing = true };
                                // Match the warmer field palette without modifying the supplier asset.
                                if (shared.HasProperty("_RootColor")) shared.SetColor("_RootColor", new Color(0.3f, 0.38f, 0.14f));
                                if (shared.HasProperty("_Color")) shared.SetColor("_Color", new Color(0.95f, 1f, 0.72f));
                                if (shared.HasProperty("_RootMaskStrength")) shared.SetFloat("_RootMaskStrength", 0.1f);
                                grassMaterials.Add(source, shared);
                            }
                            materials[m] = shared;
                        }
                        renderer.sharedMaterials = materials;
                    }
                    float width = Range(1.3f, 2f);
                    float height = Range(0.7f, 1.1f) * Mathf.Lerp(1f, 0.7f, distance / radius);
                    tuft.transform.localScale = new Vector3(width, height, width);
                    tuft.transform.SetParent(patch.transform, true);
                    GrassCount++;
                }
                grassCentres.Add(centre);
                groundCover.Add(patch);
                placed++;
            }
        }

        private bool NearTrunk(Vector2 point)
        {
            foreach (Vector2 trunk in trunks)
                if ((point - trunk).sqrMagnitude < 4f) return true;
            return false;
        }

        private void Scatter(GameObject prefab, int count, float lane, float bases,
            float minScale, float maxScale, bool cull)
        {
            for (int attempt = 0, placed = 0; attempt < count * 6 && placed < count; attempt++)
            {
                Vector2 p = new Vector2(Range(-43f, 43f), Range(-32f, 69f));
                if (!terrain.ClearOfPlay(p, lane, bases)) continue;
                // Don't bury shrubs inside the large trunks.
                if (NearTrunk(p)) continue;
                GameObject instance = Spawn(prefab, p, Range(minScale, maxScale));
                if (cull) groundCover.Add(instance);
                placed++;
            }
        }

        private GameObject Spawn(GameObject prefab, Vector2 point, float scale, float rootRadius = 0f)
        {
            GameObject instance = Instantiate(prefab, transform);
            instance.name = prefab.name;
            float ground = terrain.SurfaceHeight(point.x, point.y);
            // Keep the downhill roots buried as well as the trunk centre on hills.
            for (int i = 0; rootRadius > 0f && i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                ground = Mathf.Min(ground, terrain.SurfaceHeight(point.x + Mathf.Cos(angle) * rootRadius * scale,
                    point.y + Mathf.Sin(angle) * rootRadius * scale));
            }
            instance.transform.localPosition = new Vector3(point.x, ground - (rootRadius > 0f ? 0.15f : 0.04f), point.y);
            instance.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            instance.transform.localScale = Vector3.one * scale;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            PropCount++;
            return instance;
        }

        private void OnDestroy()
        {
            foreach (Material material in grassMaterials.Values)
                if (material != null)
                {
                    if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
                }
            grassMaterials.Clear();
        }

        private void Update()
        {
            if ((nextCull -= Time.unscaledDeltaTime) > 0f) return;
            nextCull = 0.3f;
            Camera view = Camera.main;
            if (view == null) return;
            float distance = GraphicsQuality.GrassDistance;
            distance = distance > 0f ? Mathf.Min(distance, 65f) : 65f;
            foreach (GameObject prop in groundCover)
            {
                if (prop == null) continue;
                bool visible = (prop.transform.position - view.transform.position).sqrMagnitude < distance * distance;
                if (prop.activeSelf != visible) prop.SetActive(visible);
            }
        }
    }
}
