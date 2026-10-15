using System;
using UnityEngine;

namespace Lightbringer.Visuals
{
    public enum BattlefieldPropKind
    {
        AlliedBeacon, AlliedMonument, AlliedBarrel, AlliedChest,
        // Value 4 is retired; keep existing serialized prop IDs stable.
        EnemyBoneFence = 5, EnemyAltar = 6, RuinColumn = 7, RuinArch = 8
    }

    [Serializable]
    public sealed class BattlefieldPropAsset
    {
        public BattlefieldPropKind kind;
        public GameObject prefab;
        public Vector3 size;
        public float Radius => new Vector2(size.x, size.z).magnitude * 0.5f;
    }

    [CreateAssetMenu(menuName = "Lightbringer/Battlefield Props", fileName = "BattlefieldProps")]
    public sealed class BattlefieldPropProfile : ScriptableObject
    {
        public BattlefieldPropAsset[] assets = Array.Empty<BattlefieldPropAsset>();

        public BattlefieldPropAsset Find(BattlefieldPropKind kind)
        {
            foreach (BattlefieldPropAsset asset in assets)
                if (asset != null && asset.kind == kind && asset.prefab != null) return asset;
            return null;
        }
    }
}
