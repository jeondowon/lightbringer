using UnityEngine;

namespace Lightbringer.Progression
{
    // Display data for permanent growth choices. Per-rank values mirror HeroAbilities.ApplyProgression.
    public static class GrowthCatalog
    {
        public const int LeadershipMaxRank = 20;

        public static readonly string[] Titles = { "Radiant Reach", "Bountiful Harvest", "Arcane Wellspring", "Deep Reserves",
            "Commander's Call", "Blessed Aura", "Lightbringer's Might", "Steadfast Heart" };
        public static readonly string[] Categories = { "AURA", "SUPPLY", "MAGIC", "SUPPLY", "COMMAND", "AURA", "HERO", "HERO" };
        public static readonly string[] Descriptions =
        {
            "Your aura spreads wider, covering more troops on the front.",
            "Food flows in faster, so troops can be summoned more often.",
            "Mana refills faster between staff abilities.",
            "Hold more Food and Mana before they reach the cap.",
            "Every troop costs less Food to summon.",
            "Troops inside your aura strike harder.",
            "Your staff strikes and spells hit harder.",
            "Raise your hero's maximum health.",
        };
        public static readonly string[] Effects = { "+0.6 m aura radius", "+1 Food per second", "+2 Mana per second",
            "+10 max Food and Mana", "-2.5% summon cost", "+5% aura attack bonus", "+10% hero damage", "+20 max health" };

        public static bool IsMaxed(GrowthKind kind, int rank) => kind == GrowthKind.Leadership && rank >= LeadershipMaxRank;

        // Total bonus granted at a rank, e.g. "+1.2 m".
        public static string Total(GrowthKind kind, int rank)
        {
            switch (kind)
            {
                case GrowthKind.AuraSize: return $"+{rank * 0.6f:0.0} m";
                case GrowthKind.FoodProduction: return $"+{rank}/s";
                case GrowthKind.ManaRecovery: return $"+{rank * 2}/s";
                case GrowthKind.Capacity: return $"+{rank * 10}";
                case GrowthKind.Leadership: return $"-{Mathf.Min(rank, LeadershipMaxRank) * 2.5f:0.#}%";
                case GrowthKind.AuraBuff: return $"+{rank * 5}%";
                case GrowthKind.HeroPower: return $"+{rank * 10}%";
                default: return $"+{rank * 20}";
            }
        }
    }
}
