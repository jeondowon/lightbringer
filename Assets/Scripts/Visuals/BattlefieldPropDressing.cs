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
            if (alliedKeep.HasValue) dressing.AlliedCamp(alliedKeep.Value);
            if (enemyKeep.HasValue) dressing.EnemyCamp(enemyKeep.Value);
            // Bring the ruins into view on narrow early maps; move them out beside the outer lanes later.
            float shrineX = 14f + 9f * Mathf.Clamp(pathCount - 1, 0, 2);
            dressing.Ruins(new Vector2(-shrineX, 3f), 18f);
            dressing.Ruins(new Vector2(shrineX, 17f), -24f);
            if (openingGlade)
            {
                Transform debris = dressing.Group("Fallen Shrine Columns");
                dressing.FallenColumn(debris, new Vector2(-9f, -2f), 32f, 0.65f);
                dressing.FallenColumn(debris, new Vector2(-20f, -3f), -15f, 0.8f);
                dressing.FallenColumn(debris, new Vector2(20f, 12f), 55f, 0.7f);
                dressing.FallenColumn(debris, new Vector2(9f, 23f), -28f, 0.6f);
            }
            return dressing;
        }

        private void AlliedCamp(Vector2 keep)
        {
            Transform group = Group("Allied Courtyard");
            Place(group, BattlefieldPropKind.AlliedBeacon, keep + new Vector2(-12f, 1f), 15f);
            Place(group, BattlefieldPropKind.AlliedBeacon, keep + new Vector2(12f, 1f), -15f);
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

        private Transform Group(string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(transform, false);
            return group.transform;
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

        private void FallenColumn(Transform group, Vector2 point, float yaw, float scale)
        {
            BattlefieldPropAsset asset = profile.Find(BattlefieldPropKind.RuinColumn);
            if (asset == null) return;
            float radius = asset.size.magnitude * scale * 0.5f;
            if (!terrain.ClearForProp(point, radius)) return;
            GameObject instance = Instantiate(asset.prefab, group);
            instance.name = "Fallen Ruin Column";
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, 86f);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Vector3 target = terrain.transform.TransformPoint(new Vector3(point.x,
                    terrain.SurfaceHeight(point.x, point.y) - 0.08f, point.y));
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
