using UnityEngine;

namespace Lightbringer.Progression
{
    // Display data and per-rank values for permanent growth choices; HeroAbilities.ApplyProgression reads the values.
    // Aura radius is not a card: it grows with the hero level (AuraRadiusAt). AuraSize keeps its slot in the saved
    // ranks array so older saves line up, but it is never offered and its rank stays 0.
    public static class GrowthCatalog
    {
        // Every card can be taken MaxRank times. Seven cards x 6 = 42 ranks for 29 level-up points, so at any pick at
        // least 14 ranks remain and they span three or more cards: a level-up always offers three distinct cards.
        public const int MaxRank = 6;
        public const float FoodProductionPerRank = 1f;
        public const float ManaRecoveryPerRank = 0.5f;
        public const int CapacityPerRank = 10;
        public const float LeadershipPerRank = 0.04f;
        public const float AuraBuffPerRank = 0.05f;
        public const float HeroPowerPerRank = 0.1f;
        public const int VitalityPerRank = 20;

        // Starts barely around the hero; at max level it covers one Path's whole fighting group (front-line melee to
        // the healers behind) without reaching the next Path, which is 22 m away.
        public const float AuraRadiusAtLevelOne = 1.5f;
        public const float AuraRadiusAtMaxLevel = 8f;

        public static float AuraRadiusAt(int level) => Mathf.Lerp(AuraRadiusAtLevelOne, AuraRadiusAtMaxLevel,
            (Mathf.Clamp(level, 1, CampaignProgress.MaxLevel) - 1f) / (CampaignProgress.MaxLevel - 1f));

        public static readonly string[] Titles = { "찬란한 빛", "풍요로운 수확", "마력의 샘", "넉넉한 비축",
            "지휘관의 부름", "축복의 오라", "빛의 권능", "굳건한 심장" };
        public static readonly string[] Categories = { "AURA", "SUPPLY", "MAGIC", "SUPPLY", "COMMAND", "AURA", "HERO", "HERO" };
        // Style identifiers above remain stable; these names are shown to the player.
        public static readonly string[] CategoryNames = { "오라", "보급", "마법", "보급", "지휘", "오라", "영웅", "영웅" };
        public static readonly string[] Descriptions =
        {
            "오라 범위가 넓어져 더 많은 아군을 지원합니다.",
            "식량 생산이 빨라져 병력을 더 자주 소환할 수 있습니다.",
            "마나 회복 속도가 증가합니다.",
            "식량과 마나의 최대 보유량이 증가합니다.",
            "모든 병종의 소환 식량 비용이 감소합니다.",
            "오라 안에 있는 아군의 공격력이 증가합니다.",
            "영웅의 지팡이 공격과 마법 피해가 증가합니다.",
            "영웅의 최대 체력이 증가합니다.",
        };
        public static readonly string[] Effects = { "레벨에 따라 자동 증가", $"초당 식량 생산 +{FoodProductionPerRank:0}",
            $"초당 마나 회복 +{ManaRecoveryPerRank:0.#}", $"최대 식량·마나 +{CapacityPerRank}", $"소환 비용 -{LeadershipPerRank * 100:0.#}%",
            $"오라 공격력 보너스 +{AuraBuffPerRank * 100:0}%", $"영웅 피해량 +{HeroPowerPerRank * 100:0}%", $"최대 체력 +{VitalityPerRank}" };

        public static bool IsCard(GrowthKind kind) => kind != GrowthKind.AuraSize;
        public static int MaxRankOf(GrowthKind kind) => IsCard(kind) ? MaxRank : 0;
        public static bool IsMaxed(GrowthKind kind, int rank) => rank >= MaxRankOf(kind);

        // Total bonus granted at a rank, e.g. "+12/s".
        public static string Total(GrowthKind kind, int rank)
        {
            switch (kind)
            {
                case GrowthKind.AuraSize: return "";
                case GrowthKind.FoodProduction: return $"+{rank * FoodProductionPerRank:0}/s";
                case GrowthKind.ManaRecovery: return $"+{rank * ManaRecoveryPerRank:0.#}/s";
                case GrowthKind.Capacity: return $"+{rank * CapacityPerRank}";
                case GrowthKind.Leadership: return $"-{rank * LeadershipPerRank * 100:0.#}%";
                case GrowthKind.AuraBuff: return $"+{rank * AuraBuffPerRank * 100:0}%";
                case GrowthKind.HeroPower: return $"+{rank * HeroPowerPerRank * 100:0}%";
                default: return $"+{rank * VitalityPerRank}";
            }
        }
    }
}
