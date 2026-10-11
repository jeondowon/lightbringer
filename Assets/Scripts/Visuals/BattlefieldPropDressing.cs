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

        public static BattlefieldPropDressing Build(BattlefieldPropProfile profile, BattlefieldEnvironment terrain,
            Vector2? alliedKeep, Vector2? enemyKeep, int pathCount)
        {
            if (profile == null) return null;
            GameObject root = new GameObject("Authored Battlefield Props");
            root.transform.SetParent(terrain.transform, false);
            var dressing = root.AddComponent<BattlefieldPropDressing>();
            dressing.profile = profile;
            dressing.terrain = terrain;
            if (alliedKeep.HasValue) dressing.AlliedCamp(alliedKeep.Value);
            if (enemyKeep.HasValue) dressing.EnemyCamp(enemyKeep.Value);
            // Bring the ruins into view on narrow early maps; move them out beside the outer lanes later.
            float shrineX = 14f + 9f * Mathf.Clamp(pathCount - 1, 0, 2);
            dressing.Ruins(new Vector2(-shrineX, 3f), 18f);
            dressing.Ruins(new Vector2(shrineX, 17f), -24f);
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
            placements.Add(new Placement(kind, point, radius, instance));
            terrain.ReserveProp(point, radius + 0.4f);
        }
    }
}
