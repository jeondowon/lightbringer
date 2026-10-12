using UnityEngine;

namespace Lightbringer.Progression
{
    // Display data for permanent growth choices. Per-rank values mirror HeroAbilities.ApplyProgression.
    public static class GrowthCatalog
    {
        public const int LeadershipMaxRank = 20;
        public const float ManaRecoveryPerRank = 0.5f;
        public const float AuraRadiusPerRank = 0.3f;

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
        public static readonly string[] Effects = { $"오라 반경 +{AuraRadiusPerRank:0.0}m", "초당 식량 생산 +1", $"초당 마나 회복 +{ManaRecoveryPerRank:0.#}",
            "최대 식량·마나 +10", "소환 비용 -2.5%", "오라 공격력 보너스 +5%", "영웅 피해량 +10%", "최대 체력 +20" };

        public static bool IsMaxed(GrowthKind kind, int rank) => kind == GrowthKind.Leadership && rank >= LeadershipMaxRank;

        // Total bonus granted at a rank, e.g. "+1.2 m".
        public static string Total(GrowthKind kind, int rank)
        {
            switch (kind)
            {
                case GrowthKind.AuraSize: return $"+{rank * AuraRadiusPerRank:0.0} m";
                case GrowthKind.FoodProduction: return $"+{rank}/s";
                case GrowthKind.ManaRecovery: return $"+{rank * ManaRecoveryPerRank:0.#}/s";
                case GrowthKind.Capacity: return $"+{rank * 10}";
                case GrowthKind.Leadership: return $"-{Mathf.Min(rank, LeadershipMaxRank) * 2.5f:0.#}%";
                case GrowthKind.AuraBuff: return $"+{rank * 5}%";
                case GrowthKind.HeroPower: return $"+{rank * 10}%";
                default: return $"+{rank * 20}";
            }
        }
    }
}
