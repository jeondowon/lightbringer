namespace Lightbringer.Core
{
    // Battlefield-local metres, shared by routes, terrain and the hero boundary.
    public static class BattlefieldLayout
    {
        public const float AlliedBaseZ = -22f, EnemyBaseZ = 78f;
        public const float CentreZ = (AlliedBaseZ + EnemyBaseZ) * 0.5f;
        public const float HalfWidth = 34f, MinZ = -32f, MaxZ = 92f;

        // Strongholds are enlarged cubes: footprint (width, height, depth) in metres. The front face stays
        // where the original 6x4x4 box had it, so the extra depth grows backwards.
        public const float StrongholdWidth = 10f, StrongholdHeight = 7f, StrongholdDepth = 7f;
        public const float StrongholdScale = 1.75f;
        public const float AlliedBaseCentreZ = AlliedBaseZ - (StrongholdDepth - 4f) * 0.5f;
        public const float EnemyBaseCentreZ = EnemyBaseZ + (StrongholdDepth - 4f) * 0.5f;

        // The hero may not walk behind either stronghold: limits sit at the castles' rear faces.
        public const float HeroMinZ = AlliedBaseCentreZ - StrongholdDepth * 0.5f;
        public const float HeroMaxZ = EnemyBaseCentreZ + StrongholdDepth * 0.5f;
    }
}
