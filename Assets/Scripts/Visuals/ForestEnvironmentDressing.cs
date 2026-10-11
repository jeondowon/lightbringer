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
        private System.Random random;
        private BattlefieldEnvironment terrain;
        private float nextCull;
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
            // Staggered forest edges frame the front without covering the central camera corridor.
            foreach (int side in new[] { -1, 1 })
                for (int row = 0; row < 3; row++)
                    for (int i = 0; i < 11; i++)
                    {
                        Vector2 p = new Vector2(side * (23f + row * 18f + Range(-3f, 3f)),
                            -34f + i * 12f + Range(-4f, 4f));
                        if (!terrain.ClearOfPlay(p, 12f, 18f)) continue;
                        Spawn(profile.tree, p, Range(0.8f, 1.35f), 1.5f);
                        trunks.Add(p);
                        TreeCount++;
                    }

            // Low foliage and stones tie the canopy to the ground. Roads and deployment plazas stay bare.
            Scatter(profile.sapling, 30, 9f, 14f, 0.7f, 1.2f, false);
            Scatter(profile.rock, 40, 7f, 13f, 0.7f, 1.7f, false);
            Scatter(profile.stone, 70, 4.5f, 10f, 0.6f, 1.4f, false);
            Scatter(profile.plant, 95, 4f, 10f, 0.7f, 1.2f, true);
            Scatter(profile.grass, 230, 5f, 11f, 0.45f, 0.85f, true);
            if (profile.wind != null)
            {
                GameObject wind = Instantiate(profile.wind, transform);
                wind.name = "Forest Wind";
                wind.transform.localPosition = Vector3.zero;
                wind.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
            }
        }

        private void Scatter(GameObject prefab, int count, float lane, float bases,
            float minScale, float maxScale, bool cull)
        {
            for (int attempt = 0, placed = 0; attempt < count * 6 && placed < count; attempt++)
            {
                Vector2 p = new Vector2(Range(-43f, 43f), Range(-32f, 69f));
                if (!terrain.ClearOfPlay(p, lane, bases)) continue;
                // Don't bury shrubs inside the large trunks.
                bool blocked = false;
                foreach (Vector2 trunk in trunks)
                    if ((p - trunk).sqrMagnitude < 4f) { blocked = true; break; }
                if (blocked) continue;
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
