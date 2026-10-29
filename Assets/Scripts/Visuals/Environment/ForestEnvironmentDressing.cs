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
        private readonly List<Vector2> trunks = new List<Vector2>();
        private System.Random random;
        private BattlefieldEnvironment terrain;
        private bool openingGlade;
        public int TreeCount { get; private set; }
        public int PropCount { get; private set; }

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
            openingGlade = profile.openingGlade;
            // Staggered forest edges frame the front without covering the central camera corridor.
            foreach (int side in new[] { -1, 1 })
                for (int row = 0; row < 3; row++)
                    for (int i = 0; i < 15; i++)
                    {
                        Vector2 p = new Vector2(side * (23f + row * 18f + Range(-3f, 3f)),
                            -38f + i * 11f + Range(-4f, 4f));
                        if (openingGlade) p += new Vector2(Range(-5f, 5f), Range(-5f, 5f));
                        if (!terrain.ClearOfPlay(p, 12f, 18f) || !terrain.ClearOfProps(p, 11f)) continue;
                        Spawn(profile.tree, p, openingGlade ? Range(0.65f, 1.4f) : Range(0.8f, 1.35f), 1.5f);
                        trunks.Add(p);
                        TreeCount++;
                    }

            // Rear and forward groves soften the join from real terrain to the panoramic forest.
            foreach (float depth in new[] { -67f, 125f })
                for (int i = 0; i < 13; i++)
                {
                    Vector2 p = new Vector2(-86f + i * 14f + Range(-4f, 4f), depth + Range(-7f, 7f));
                    if (!terrain.ClearOfProps(p, 11f)) continue;
                    Spawn(profile.tree, p, Range(0.75f, 1.2f), 1.5f);
                    trunks.Add(p);
                    TreeCount++;
                }

            BuildWoodlandEdge(profile);
            // Low foliage and stones tie the canopy to the ground. Roads and deployment plazas stay bare.
            if (openingGlade) BuildGladeUnderstory(profile);
            else
            {
                Scatter(profile.sapling, 55, 9f, 14f, 0.7f, 1.2f);
                Scatter(profile.rock, 55, 7f, 13f, 0.7f, 1.4f);
                Scatter(profile.stone, 115, 4.5f, 10f, 0.4f, 1.1f);
            }
            // Last, so the hand-tuned field dressing above keeps its random sequence.
            BuildHillsideForest(profile);
            if (profile.wind != null)
            {
                GameObject wind = Instantiate(profile.wind, transform);
                wind.name = "Forest Wind";
                wind.transform.localPosition = Vector3.zero;
                wind.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
            }
        }

        private void BuildGladeUnderstory(ForestEnvironmentProfile profile)
        {
            foreach (int side in new[] { -1, 1 })
                foreach (float depth in new[] { -9f, 17f, 44f, 69f, 91f })
                {
                    Vector2 point = new Vector2(side * Range(29f, 34f), depth + Range(-3f, 3f));
                    if (!terrain.ClearOfPlay(point, 14f, 18f) || !terrain.ClearOfProps(point, 10f) || NearTrunk(point)) continue;
                    Spawn(profile.tree, point, Range(1.05f, 1.4f), 1.5f);
                    trunks.Add(point);
                    TreeCount++;
                }
            // Unequal woodland islands, with open sightlines between them. Small companion stones
            // and shrubs ground each outcrop instead of scattering isolated giant boulders.
            Vector2[] islands = {
                new Vector2(-20f, -14f), new Vector2(24f, -10f),
                new Vector2(-26f, 7f), new Vector2(21f, 2f),
                new Vector2(-19f, 22f), new Vector2(28f, 29f),
                new Vector2(-38f, 39f), new Vector2(41f, 48f),
                new Vector2(-30f, 65f), new Vector2(24f, 76f),
                new Vector2(-19f, 48f), new Vector2(18f, 53f),
                new Vector2(-26f, 88f), new Vector2(28f, 96f)
            };
            foreach (Vector2 centre in islands)
            {
                for (int i = 0; i < 12; i++)
                {
                    float angle = Range(0f, Mathf.PI * 2f);
                    float radius = i < 3 ? Range(0f, 2.8f) : Range(2.2f, 7.5f);
                    Vector2 point = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (!terrain.ClearOfPlay(point, 6f, 12f) || !terrain.ClearOfProps(point, 2f) || NearTrunk(point)) continue;
                    GameObject prefab = i < 3 ? profile.rock : i < 8 ? profile.stone : profile.sapling;
                    float scale = i < 3 ? Range(0.5f, 0.95f) : i < 8 ? Range(0.5f, 1.1f) : Range(0.65f, 1.3f);
                    GameObject prop = Spawn(prefab, point, scale);
                    if (i < 3) prop.transform.localScale = Vector3.Scale(prop.transform.localScale,
                        new Vector3(Range(1.15f, 1.6f), Range(0.65f, 0.85f), 1f));
                }
            }
            // Small pebbles soften the road edge while preserving its open centre.
            Scatter(profile.stone, 110, 4.5f, 11f, 0.25f, 0.65f);
        }

        private void BuildHillsideForest(ForestEnvironmentProfile profile)
        {
            // Dense woodland over the hills outside the field, so the ridgelines read as forest, not bare
            // lawn. Rare noise clearings break up the canopy. Stays inside the terrain mesh so roots ground.
            const float cell = 8.5f, minX = -106f, maxX = 106f, minZ = -91f, maxZ = 145f;
            float noise = random.Next(1000);
            for (float z = minZ; z <= maxZ; z += cell)
                for (float x = minX; x <= maxX; x += cell)
                {
                    Vector2 p = new Vector2(Mathf.Clamp(x + Range(-2.8f, 2.8f), minX, maxX),
                        Mathf.Clamp(z + Range(-2.8f, 2.8f), minZ, maxZ));
                    // Behind the allied gate the gameplay camera sits close; keep that slope clearer.
                    float outside = BattlefieldEnvironment.OutsideDistance(p.x, p.y);
                    if (outside < (p.y < BattlefieldLayout.MinZ ? 16f : 6f)) continue;
                    if (Mathf.PerlinNoise(p.x * 0.04f + noise, p.y * 0.04f) < 0.22f) continue;
                    // Trunks may stand close to hill ruins so the canopy overgrows them instead of leaving lawn.
                    if (NearTree(p, 6f) || !terrain.ClearOfProps(p, 3f)) continue;
                    Spawn(profile.tree, p, Range(0.9f, 1.45f), 1.5f);
                    trunks.Add(p);
                    TreeCount++;
                    // Saplings fill the gaps between nearer trunks; distant ones would vanish in the fog.
                    if (outside > 45f || random.NextDouble() < 0.5) continue;
                    Vector2 sapling = p + new Vector2(Range(-4f, 4f), Range(-4f, 4f));
                    if (NearTrunk(sapling) || !terrain.ClearOfProps(sapling, 2f)
                        || BattlefieldEnvironment.OutsideDistance(sapling.x, sapling.y) < 8f) continue;
                    Spawn(profile.sapling, sapling, Range(0.9f, 1.5f));
                }
        }

        private bool NearTree(Vector2 point, float radius)
        {
            foreach (Vector2 trunk in trunks)
                if ((point - trunk).sqrMagnitude < radius * radius) return true;
            return false;
        }

        private void BuildWoodlandEdge(ForestEnvironmentProfile profile)
        {
            // A visible woodland belt follows the playable boundary; irregular offsets hide the rectangle.
            for (int side = -1; side <= 1; side += 2)
                for (float z = BattlefieldLayout.MinZ; z <= BattlefieldLayout.MaxZ; z += 7f)
                    EdgeCluster(new Vector2(side * (BattlefieldLayout.HalfWidth + Range(1f, 5f)), z + Range(-2f, 2f)), profile);
            foreach (float z in new[] { BattlefieldLayout.MinZ - 4f, BattlefieldLayout.MaxZ + 4f })
                for (float x = -30f; x <= 30f; x += 7f)
                    EdgeCluster(new Vector2(x + Range(-2f, 2f), z + Range(-2f, 2f)), profile);
        }

        private void EdgeCluster(Vector2 centre, ForestEnvironmentProfile profile)
        {
            if (!NearTrunk(centre) && terrain.ClearOfPlay(centre, 10f, 12f) && terrain.ClearOfProps(centre, 8f))
            {
                Spawn(profile.tree, centre, Range(0.85f, 1.2f), 1.5f);
                trunks.Add(centre);
                TreeCount++;
            }
            for (int i = 0; i < 2; i++)
            {
                Vector2 point = centre + new Vector2(Range(-4f, 4f), Range(-4f, 4f));
                if (NearTrunk(point) || !terrain.ClearOfPlay(point, 6f, 11f) || !terrain.ClearOfProps(point, 2f)) continue;
                Spawn(profile.sapling, point, Range(0.8f, 1.4f));
            }
        }

        private bool NearTrunk(Vector2 point)
        {
            foreach (Vector2 trunk in trunks)
                if ((point - trunk).sqrMagnitude < 4f) return true;
            return false;
        }

        private void Scatter(GameObject prefab, int count, float lane, float bases,
            float minScale, float maxScale)
        {
            for (int attempt = 0, placed = 0; attempt < count * 6 && placed < count; attempt++)
            {
                Vector2 p = new Vector2(Range(-43f, 43f), Range(BattlefieldLayout.MinZ, BattlefieldLayout.MaxZ + 8f));
                if (!terrain.ClearOfPlay(p, lane, bases) || (openingGlade && !terrain.ClearOfProps(p, 1f))) continue;
                // Don't bury shrubs inside the large trunks.
                if (NearTrunk(p)) continue;
                Spawn(prefab, p, Range(minScale, maxScale));
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
            ForestGroundContact contact = instance.GetComponent<ForestGroundContact>();
            if (contact != null) contact.Ground(terrain);
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            PropCount++;
            return instance;
        }
    }
}
