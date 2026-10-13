namespace Lightbringer.Core
{
    // Battlefield-local metres, shared by routes, terrain and the hero boundary.
    public static class BattlefieldLayout
    {
        public const float AlliedBaseZ = -22f, EnemyBaseZ = 78f;
        public const float CentreZ = (AlliedBaseZ + EnemyBaseZ) * 0.5f;
        public const float HalfWidth = 34f, MinZ = -32f, MaxZ = 92f;
    }
}
