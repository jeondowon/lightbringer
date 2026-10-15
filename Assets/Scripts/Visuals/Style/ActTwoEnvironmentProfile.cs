using System;
using UnityEngine;

namespace Lightbringer.Visuals
{
    // Gate and Statue are never scattered; the final citadel places them by hand.
    // Sword, Spear and Mace are fallen allied arms (BattlefieldRelicSetup), appended to keep serialized values stable.
    public enum ActTwoScatterKind { Cliff, Boulder, Log, Stump, Brush, Crate, Barrel, Bucket, Shield, FirePit, Gate, Statue, Sword, Spear, Mace }

    // A photoscanned prop at real-world scale with a ground pivot; radius is its horizontal bounding-circle radius (m).
    [Serializable]
    public sealed class ActTwoScatterAsset
    {
        public ActTwoScatterKind kind;
        public GameObject prefab;
        public float radius = 1;
        // Longest dimension (m); 0 on scans built before it was recorded.
        public float length;
    }

    [CreateAssetMenu(menuName = "Lightbringer/Act Two Environment")]
    public sealed class ActTwoEnvironmentProfile : ScriptableObject
    {
        // Trees have unit height; landmarks have unit horizontal bounding-circle radius.
        public GameObject[] trees = new GameObject[0];
        public GameObject sanctuary;
        public GameObject monument;
        public GameObject watchtower;
        public GameObject brokenWall;
        public Material terrain;
        public ActTwoScatterAsset[] scatter = new ActTwoScatterAsset[0];
        // Weathered allied shields and arms left on old battlefields, built from the unit weapon meshes.
        // Kept apart from `scatter` so rebuilding the photoscans leaves them in place.
        public ActTwoScatterAsset[] relics = new ActTwoScatterAsset[0];
        // Photographic structure materials (Poly Haven CC0, metre-scale UVs from TexturedShapes):
        // Act 2 depot palisades and sheds, and the final citadel.
        public Material castleStone;
        public Material roofSlate;
        public Material timber;
        public Material bark;
        // Faint firelight in the citadel's windows (emission is baked into the asset so builds keep the variant).
        public Material windowGlow;

        public bool HasStructureMaterials => castleStone != null && roofSlate != null && timber != null && bark != null;

        public GameObject FindLandmark(string kind)
        {
            switch (kind)
            {
                case "Sanctuary": return sanctuary;
                case "Monument": return monument;
                case "Watchtower": return watchtower;
                case "BrokenWall": return brokenWall;
                default: return null;
            }
        }

        public ActTwoScatterAsset[] Find(ActTwoScatterKind kind)
        {
            var result = new System.Collections.Generic.List<ActTwoScatterAsset>();
            foreach (ActTwoScatterAsset[] set in new[] { scatter, relics })
                if (set != null)
                    foreach (ActTwoScatterAsset asset in set)
                        if (asset != null && asset.kind == kind && asset.prefab != null) result.Add(asset);
            return result.ToArray();
        }
    }
}
