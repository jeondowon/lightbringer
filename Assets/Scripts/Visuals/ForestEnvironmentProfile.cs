using UnityEngine;

namespace Lightbringer.Visuals
{
    [CreateAssetMenu(menuName = "Lightbringer/Forest Environment", fileName = "ForestEnvironment")]
    public sealed class ForestEnvironmentProfile : ScriptableObject
    {
        [Min(1)] public int firstStage = 1;
        [Min(1)] public int lastStage = 1;
        [Header("Grounded, metre-scale decorative prefabs")]
        public GameObject tree, sapling, rock, stone, grass, plant;
        public GameObject wind;
        public Material terrain;
        [Tooltip("Clustered undergrowth and fuller roadside grass for the opening forest glade.")]
        public bool openingGlade;

        public bool IsReady => tree != null && sapling != null && rock != null
            && stone != null && grass != null && plant != null;
        public bool AppliesTo(int stage) => IsReady && stage >= firstStage && stage <= lastStage;
    }
}
