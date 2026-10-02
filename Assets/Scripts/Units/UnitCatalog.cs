using UnityEngine;

namespace Lightbringer.Units
{
    public enum UnitKind { Swordsman, Archer, Shieldbearer, Spearman, Priest, Mage, Knight, Dragon }

    // Prototype balance data. All unlocked types remain available; this is not a unit deck.
    public static class UnitCatalog
    {
        public const int Count = 8;
        public static readonly string[] Names = { "Swordsman", "Archer", "Shieldbearer", "Spearman", "Priest", "Mage", "Knight", "Dragon" };
        private static readonly float[] Costs = { 10, 15, 20, 25, 30, 40, 55, 90 };
        private static readonly float[] Health = { 30, 22, 100, 45, 35, 30, 170, 300 };
        private static readonly float[] Damage = { 10, 7, 8, 14, 0, 18, 32, 40 };
        private static readonly float[] Ranges = { 1.25f, 7, 1.3f, 2.5f, 6, 6, 1.6f, 8 };
        public static float Cost(UnitKind kind) => Costs[(int)kind];
        public static float MaxHealth(UnitKind kind) => Health[(int)kind];
        public static float AttackDamage(UnitKind kind) => Damage[(int)kind];
        public static float Range(UnitKind kind) => Ranges[(int)kind];
        // Spearman is the anti-Heavy troop (Brute, Boss).
        public static float HeavyBonus(UnitKind kind) => kind == UnitKind.Spearman ? 2f : 1f;
        // Knight charge: after riding this far without attacking, the next hit deals bonus damage,
        // also strikes enemies around the impact and shoves non-Heavy enemies back.
        public const float ChargeDistance = 5f;
        public const float ChargeMultiplier = 2.5f;
        public const float ChargeRadius = 1.8f;
        public const float ChargeKnockback = 1.5f;
        public static float Speed(UnitKind kind) => kind == UnitKind.Shieldbearer ? 2.2f : kind == UnitKind.Knight ? 4f : 3f;
        public static Vector3 VisualScale(UnitKind kind) => kind == UnitKind.Dragon
            ? new Vector3(2.4f, 1.4f, 3f) : kind == UnitKind.Shieldbearer
            ? new Vector3(1f, 0.9f, 0.6f) : kind == UnitKind.Archer
            ? new Vector3(0.5f, 0.9f, 0.5f) : new Vector3(0.7f, 0.8f, 0.7f);
    }
}
