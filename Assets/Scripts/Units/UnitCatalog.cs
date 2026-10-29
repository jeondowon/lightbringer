using Lightbringer.Combat;
using UnityEngine;

namespace Lightbringer.Units
{
    public enum UnitKind { Swordsman, Archer, Shieldbearer, Spearman, Priest, Mage, Knight, Dragon }

    // Prototype balance data. All unlocked types remain available; this is not a unit deck.
    public static class UnitCatalog
    {
        public const int Count = 8;
        public static readonly string[] Names = { "검병", "궁병", "방패병", "창병", "사제", "마법사", "기사", "드래곤" };
        private static readonly float[] Costs = { 10, 15, 20, 25, 30, 40, 55, 90 };
        private static readonly float[] Health = { 30, 22, 100, 45, 35, 30, 170, 300 };
        private static readonly float[] Damage = { 10, 7, 8, 14, 0, 18, 32, 40 };
        private static readonly float[] Ranges = { 1.25f, 7, 1.3f, 2.5f, 6, 6, 1.6f, 8 };
        public static float Cost(UnitKind kind) => Costs[(int)kind];
        // First stage each troop can be deployed (it is the reward of the stage before). Rising in UnitKind order, so
        // the unlocked troops are always the first UnlockedAt(stage) kinds. Table: Docs/GameDesign/Unlocks.md.
        private static readonly int[] UnlockStages = { 1, 3, 6, 8, 10, 12, 16, 20 };
        public static int UnlockedAt(int stage)
        {
            int count = 0;
            while (count < Count && UnlockStages[count] <= stage) count++;
            return count;
        }
        // Gold upgrades: every rank raises health and damage by a share of the base value.
        public const int MaxRank = 10;
        public const float HealthPerRank = 0.12f, DamagePerRank = 0.08f;
        public static float MaxHealthAt(UnitKind kind, int rank) => Health[(int)kind] * (1f + HealthPerRank * Mathf.Clamp(rank, 0, MaxRank));
        public static float AttackDamageAt(UnitKind kind, int rank) => Damage[(int)kind] * (1f + DamagePerRank * Mathf.Clamp(rank, 0, MaxRank));
        public static int UpgradeCost(UnitKind kind, int rank) => Mathf.RoundToInt((40 + 20 * (int)kind) * (1f + 0.5f * rank));
        public static float MaxHealth(UnitKind kind) => Health[(int)kind];
        public static float AttackDamage(UnitKind kind) => Damage[(int)kind];
        public static float Range(UnitKind kind) => Ranges[(int)kind];
        // Where in the attack clip the blow lands or the shot is loosed (0-1 of the clip). Matches the rigged
        // clips: the Archer looses at the start, melee contact ~0.4-0.45, the Mage thrust and Dragon breath ~0.5.
        private static readonly float[] StrikePhases = { 0.4f, 0f, 0.45f, 0.45f, 0f, 0.5f, 0.45f, 0.5f };
        public static float StrikePhase(UnitKind kind) => StrikePhases[(int)kind];
        public static ProjectileKind Projectile(UnitKind kind) => kind == UnitKind.Archer ? ProjectileKind.Arrow
            : kind == UnitKind.Mage ? ProjectileKind.Bolt : ProjectileKind.None;
        // Flight speed (m/s) and arc height as a share of the distance. Arrows lob slightly; magic flies straight.
        public static float ProjectileSpeed(UnitKind kind) => kind == UnitKind.Archer ? 24f : 15f;
        public static float ProjectileArc(UnitKind kind) => kind == UnitKind.Archer ? 0.07f : 0f;
        // Spearman is the anti-Heavy troop (Brute, Boss).
        public static float HeavyBonus(UnitKind kind) => kind == UnitKind.Spearman ? 2f : 1f;
        // Knight charge: after riding this far without attacking, the next hit deals bonus damage,
        // also strikes enemies around the impact and shoves non-Heavy enemies back.
        public const float ChargeDistance = 5f;
        public const float ChargeMultiplier = 2.5f;
        public const float ChargeRadius = 1.8f;
        public const float ChargeKnockback = 1.5f;
        // Priest rear guard: stays this far behind the leading fighter on its Path and halts this close to enemies
        // (inside its 6 m heal pulse, so troops in melee are still mended).
        public const float PriestFollowGap = 2.5f;
        public const float PriestKeepAway = 4.5f;
        public static float Speed(UnitKind kind) => kind == UnitKind.Shieldbearer ? 1.1f : kind == UnitKind.Knight ? 4f : 1.5f;
        public static Vector3 VisualScale(UnitKind kind) => kind == UnitKind.Dragon
            ? new Vector3(2.4f, 1.4f, 3f) : kind == UnitKind.Shieldbearer
            ? new Vector3(1f, 0.9f, 0.6f) : kind == UnitKind.Archer
            ? new Vector3(0.5f, 0.9f, 0.5f) : new Vector3(0.7f, 0.8f, 0.7f);
    }
}
