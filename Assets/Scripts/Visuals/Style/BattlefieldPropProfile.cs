using System;
using UnityEngine;

namespace Lightbringer.Visuals
{
    public enum BattlefieldPropKind
    {
        AlliedBeacon, AlliedMonument, AlliedBarrel, AlliedChest,
        EnemyCrystal, EnemyBoneFence, EnemyAltar, RuinColumn, RuinArch
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
