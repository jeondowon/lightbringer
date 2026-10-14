using System.Collections.Generic;
using UnityEngine;

namespace Lightbringer.Visuals
{
    // Authored groups of imported props. Shared prefabs/materials, no gameplay colliders or per-frame work.
    public sealed class BattlefieldPropDressing : MonoBehaviour
    {
        public readonly struct Placement
        {
            public readonly BattlefieldPropKind Kind;
            public readonly Vector2 Point;
            public readonly float Radius;
            public readonly GameObject Instance;
            public Placement(BattlefieldPropKind kind, Vector2 point, float radius, GameObject instance)
            { Kind = kind; Point = point; Radius = radius; Instance = instance; }
        }

        private readonly List<Placement> placements = new List<Placement>();
        public IReadOnlyList<Placement> Placements => placements;
        private BattlefieldPropProfile profile;
        private BattlefieldEnvironment terrain;
        private readonly Dictionary<Material, Material> agedMaterials = new Dictionary<Material, Material>();
        private bool openingGlade;

        public static BattlefieldPropDressing Build(BattlefieldPropProfile profile, BattlefieldEnvironment terrain,
            Vector2? alliedKeep, Vector2? enemyKeep, int pathCount, bool openingGlade = false)
        {
            if (profile == null) return null;
            GameObject root = new GameObject("Authored Battlefield Props");
            root.transform.SetParent(terrain.transform, false);
            var dressing = root.AddComponent<BattlefieldPropDressing>();
            dressing.profile = profile;
            dressing.terrain = terrain;
            dressing.openingGlade = openingGlade;
            if (openingGlade) dressing.BuildSanctuary();
            if (alliedKeep.HasValue) dressing.AlliedCamp(alliedKeep.Value);
            if (enemyKeep.HasValue) dressing.EnemyCamp(enemyKeep.Value);
            // Bring the ruins into view on narrow early maps; move them out beside the outer lanes later.
            float shrineX = 14f + 9f * Mathf.Clamp(pathCount - 1, 0, 2);
            if (!openingGlade) dressing.Ruins(new Vector2(-shrineX, 3f), 18f);
            dressing.Ruins(new Vector2(shrineX, 17f), -24f);
            if (openingGlade)
            {
                Transform debris = dressing.Group("Fallen Shrine Columns");
                dressing.FallenColumn(debris, new Vector2(-9f, -2f), 32f, 0.65f);
                dressing.FallenColumn(debris, new Vector2(-20f, -3f), -15f, 0.8f);
                dressing.FallenColumn(debris, new Vector2(20f, 12f), 55f, 0.7f);
                dressing.FallenColumn(debris, new Vector2(9f, 23f), -28f, 0.6f);
                dressing.ForestRuins();
            }
            return dressing;
        }

        private void AlliedCamp(Vector2 keep)
        {
            Transform group = Group("Allied Courtyard");
            if (!openingGlade)
            {
                Place(group, BattlefieldPropKind.AlliedBeacon, keep + new Vector2(-12f, 1f), 15f);
                Place(group, BattlefieldPropKind.AlliedBeacon, keep + new Vector2(12f, 1f), -15f);
            }
            else
                foreach (int side in new[] { -1, 1 })
                    BeaconSanctuary.BuildCampLantern(group, new Vector3(keep.x + 12f * side, 0f, keep.y + 1f));
            Place(group, BattlefieldPropKind.AlliedMonument, keep + new Vector2(-15f, 6f), 25f);
            Place(group, BattlefieldPropKind.AlliedChest, keep + new Vector2(14f, 5f), -30f);
            Place(group, BattlefieldPropKind.AlliedBarrel, keep + new Vector2(17f, 5f), -12f);
            Place(group, BattlefieldPropKind.AlliedBarrel, keep + new Vector2(18f, 3f), 22f, 0.85f);
            Place(group, BattlefieldPropKind.AlliedBarrel, keep + new Vector2(16f, 1f), 8f, 0.9f);
        }

        private void EnemyCamp(Vector2 keep)
        {
            Transform group = Group("Enemy Ritual Grounds");
            Place(group, BattlefieldPropKind.EnemyCrystal, keep + new Vector2(-14f, -4f), 25f);
            Place(group, BattlefieldPropKind.EnemyCrystal, keep + new Vector2(15f, -2f), -45f, 0.85f);
            Place(group, BattlefieldPropKind.EnemyCrystal, keep + new Vector2(20f, 4f), 75f, 0.65f);
            Place(group, BattlefieldPropKind.EnemyAltar, keep + new Vector2(-17f, 4f), 180f);
            // Short side palisades, never a wall across the attack approach.
            foreach (int side in new[] { -1, 1 })
            {
                Place(group, BattlefieldPropKind.EnemyBoneFence, keep + new Vector2(side * 21f, -7f), side * 65f);
                Place(group, BattlefieldPropKind.EnemyBoneFence, keep + new Vector2(side * 23f, -1f), side * 85f);
            }
        }

        private void Ruins(Vector2 centre, float yaw)
        {
            Transform group = Group(centre.x < 0 ? "West Shrine Ruins" : "East Shrine Ruins");
            Place(group, BattlefieldPropKind.RuinArch, centre, yaw);
            Place(group, BattlefieldPropKind.RuinColumn, centre + new Vector2(-2.5f, -4f), yaw + 35f, 0.9f);
            Place(group, BattlefieldPropKind.RuinColumn, centre + new Vector2(2.5f, 4f), yaw - 18f, 0.75f);
        }

        // Overgrown temple remnants: two on the field fringe, four on the forested hills. Built before the
        // forest; hill ruins are oversized to read from the field. Sinks leave broken, buried stumps.
        private void ForestRuins()
        {
            Transform colonnade = Group("West Colonnade Ruins");
            Vector2 west = new Vector2(-29f, 47f);
            float[] sinks = { 0f, 1.7f, 0.5f, 2.8f };
            for (int i = 0; i < sinks.Length; i++)
                Ruin(colonnade, BattlefieldPropKind.RuinColumn, west + new Vector2(i * 0.4f, i * 3.6f), 20f + i * 37f, 0.9f,
                    sinks[i], i == 2 ? 5f : 0f);
            FallenColumn(colonnade, west + new Vector2(3.2f, 16f), 72f, 0.8f, true);

            Transform gate = Group("East Gate Ruins");
            Ruin(gate, BattlefieldPropKind.RuinArch, new Vector2(29f, 36f), 78f, 0.85f, 0.3f, 0f);
            Ruin(gate, BattlefieldPropKind.RuinColumn, new Vector2(30.5f, 41.5f), -30f, 0.8f, 2.2f, 0f);

            // Hilltop temple ring above the west forest edge: the arch faces the field.
            Transform temple = Group("West Hill Temple Ruins");
            Vector2 hill = new Vector2(-48f, 18f);
            Ruin(temple, BattlefieldPropKind.RuinArch, hill, 90f, 1.5f, 0.4f, 3f);
            float[] ringSinks = { 0.3f, 2.4f, -1f, 1.1f, 3f, -1f };
            for (int i = 0; i < ringSinks.Length; i++)
            {
                if (ringSinks[i] < 0f) continue; // missing columns
                float angle = (i * 60f + 15f) * Mathf.Deg2Rad;
                Ruin(temple, BattlefieldPropKind.RuinColumn, hill + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 7f,
                    i * 53f, 1.5f, ringSinks[i], i == 3 ? -6f : 0f);
            }
            FallenColumn(temple, hill + new Vector2(4.5f, -7.5f), 10f, 1.3f, true);

            Transform east = Group("East Hill Shrine Ruins");
            Ruin(east, BattlefieldPropKind.RuinArch, new Vector2(48f, 60f), 95f, 1.4f, 0.6f, -4f);
            Ruin(east, BattlefieldPropKind.RuinColumn, new Vector2(46f, 53f), 12f, 1.4f, 0.8f, 0f);
            Ruin(east, BattlefieldPropKind.RuinColumn, new Vector2(50f, 67f), 64f, 1.4f, 2.5f, 7f);
            FallenColumn(east, new Vector2(42f, 64f), -12f, 1.2f, true);

            Transform north = Group("North Hill Colonnade Ruins");
            for (int i = 0; i < 3; i++)
                Ruin(north, BattlefieldPropKind.RuinColumn, new Vector2(-40f + i * 4f, 96f + i * 1.2f), i * 41f, 1.4f,
                    i == 1 ? 2.6f : 0.4f * i, 0f);
            FallenColumn(north, new Vector2(-33f, 92f), 8f, 1.2f, true);

            Transform south = Group("South-East Hill Ruins");
            Ruin(south, BattlefieldPropKind.RuinColumn, new Vector2(45f, -6f), 30f, 1.3f, 1.9f, 4f);
            Ruin(south, BattlefieldPropKind.RuinColumn, new Vector2(47.5f, -1.5f), -12f, 1.4f, 0.2f, 0f);
            FallenColumn(south, new Vector2(43f, 3f), -10f, 1.1f, true);
        }

        // Like Place, but for uneven ground: the base sinks to the lowest point of the footprint
        // (plus 'sink' metres to read as a broken, buried stump) and may lean by 'tilt' degrees.
        private void Ruin(Transform group, BattlefieldPropKind kind, Vector2 point, float yaw, float scale, float sink, float tilt)
        {
            BattlefieldPropAsset asset = profile.Find(kind);
            if (asset == null) return;
            float radius = asset.Radius * scale;
            if (!RuinClear(point, radius)) return;
            GameObject instance = Instantiate(asset.prefab, group);
            instance.name = kind.ToString();
            float ground = LowestGround(point, radius);
            float lean = radius * Mathf.Sin(Mathf.Abs(tilt) * Mathf.Deg2Rad);
            instance.transform.localPosition = new Vector3(point.x, ground - 0.05f - lean - sink, point.y);
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tilt, 0f, 0f);
            instance.transform.localScale = Vector3.one * scale;
            AgeStone(instance);
            placements.Add(new Placement(kind, point, radius, instance));
            terrain.ReserveProp(point, radius + 0.4f);
        }

        private bool RuinClear(Vector2 point, float radius) =>
            terrain.ClearOfPlay(point, 4.5f + radius, 6f + radius) && terrain.ClearOfProps(point, radius);

        private float LowestGround(Vector2 point, float radius)
        {
            float ground = terrain.SurfaceHeight(point.x, point.y);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                ground = Mathf.Min(ground, terrain.SurfaceHeight(point.x + Mathf.Cos(angle) * radius,
                    point.y + Mathf.Sin(angle) * radius));
            }
            return ground;
        }

        private Transform Group(string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(transform, false);
            return group.transform;
        }

        private void BuildSanctuary()
        {
            Vector2 centre = BeaconSanctuary.Centre;
            Transform group = Group("First Beacon Sanctuary");
            Place(group, BattlefieldPropKind.AlliedBeacon, centre, 35f, 1.9f);
            if (placements.Count == 0) return;
            BeaconSanctuary.Build(group, placements[placements.Count - 1].Instance, terrain);
            Place(group, BattlefieldPropKind.RuinColumn, centre + new Vector2(-5f, -5f), 22f, 0.8f);
            Place(group, BattlefieldPropKind.RuinColumn, centre + new Vector2(-5f, 5f), -15f, 1.05f);
            terrain.ReserveProp(centre, 7f);
            for (float x = centre.x + 5f; x <= -4f; x += 2f)
                terrain.ReserveProp(new Vector2(x, centre.y), 2.5f);
        }

        private void Place(Transform group, BattlefieldPropKind kind, Vector2 point, float yaw, float scale = 1f)
        {
            BattlefieldPropAsset asset = profile.Find(kind);
            if (asset == null) return;
            float radius = asset.Radius * scale;
            // Whole footprints, rather than just pivots, must clear the routes and other props.
            if (!terrain.ClearForProp(point, radius)) return;
            GameObject instance = Instantiate(asset.prefab, group);
            instance.name = kind.ToString();
            instance.transform.localPosition = new Vector3(point.x, terrain.SurfaceHeight(point.x, point.y) - 0.025f, point.y);
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one * scale;
            if (openingGlade && (kind == BattlefieldPropKind.RuinArch || kind == BattlefieldPropKind.RuinColumn))
                AgeStone(instance);
            placements.Add(new Placement(kind, point, radius, instance));
            terrain.ReserveProp(point, radius + 0.4f);
        }

        private void FallenColumn(Transform group, Vector2 point, float yaw, float scale, bool slope = false)
        {
            BattlefieldPropAsset asset = profile.Find(BattlefieldPropKind.RuinColumn);
            if (asset == null) return;
            float radius = asset.size.magnitude * scale * 0.5f;
            if (slope ? !RuinClear(point, radius) : !terrain.ClearForProp(point, radius)) return;
            float ground = terrain.SurfaceHeight(point.x, point.y) - 0.08f;
            if (slope)
            {
                // Lie along the contour (yaw becomes a small offset) with the uphill side bedded in the soil.
                Vector2 downhill = new Vector2(
                    terrain.SurfaceHeight(point.x - 1f, point.y) - terrain.SurfaceHeight(point.x + 1f, point.y),
                    terrain.SurfaceHeight(point.x, point.y - 1f) - terrain.SurfaceHeight(point.x, point.y + 1f));
                if (downhill.sqrMagnitude > 0.0001f)
                    yaw += Mathf.Atan2(downhill.x, downhill.y) * Mathf.Rad2Deg;
                ground = LowestGround(point, radius * 0.6f) - 0.15f;
            }
            GameObject instance = Instantiate(asset.prefab, group);
            instance.name = "Fallen Ruin Column";
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, 86f);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Vector3 target = terrain.transform.TransformPoint(new Vector3(point.x, ground, point.y));
                instance.transform.position += target - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }
            AgeStone(instance);
            placements.Add(new Placement(BattlefieldPropKind.RuinColumn, point, radius, instance));
            terrain.ReserveProp(point, radius + 0.4f);
        }

        private void AgeStone(GameObject instance)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null) continue;
                    if (!agedMaterials.TryGetValue(source, out Material aged))
                    {
                        aged = new Material(source) { name = source.name + " (Forest Weathering)", enableInstancing = true };
                        if (aged.HasProperty("_BaseColor")) aged.SetColor("_BaseColor",
                            aged.GetColor("_BaseColor") * new Color(0.64f, 0.69f, 0.57f, 1f));
                        agedMaterials.Add(source, aged);
                    }
                    materials[i] = aged;
                }
                renderer.sharedMaterials = materials;
            }
        }

        private void OnDestroy()
        {
            foreach (Material material in agedMaterials.Values)
                if (material != null)
                {
                    if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
                }
            agedMaterials.Clear();
        }
    }
}
