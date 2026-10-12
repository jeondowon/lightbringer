using UnityEngine;

namespace Lightbringer.Player
{
    public enum SkillKind { LightBeam, Heal, Ice, Shockwave, Meteor }

    // The hero carries one staff and learns skills. A skill is learned as a first-clear reward, levelled with light
    // shards and equipped to one of the three slots (left click, right click, Q) before a battle.
    public static class SkillCatalog
    {
        public const int Count = 5;
        public const int Slots = 3;
        public const int MaxLevel = 10;
        // A loadout slot with no skill in it.
        public const int Empty = -1;
        public static readonly string[] Names = { "빛 광선", "치유", "얼음 마법", "충격파", "메테오" };
        public static readonly string[] SlotKeys = { "LMB", "RMB", "Q" };
        public static readonly string[] Descriptions =
        {
            "조준한 적에게 빛줄기를 내려 단일 대상을 공격합니다.",
            "영웅 주변 6m 안의 아군을 회복합니다.",
            "조준한 적을 얼려 잠시 움직이지 못하게 합니다.",
            "지팡이를 내려쳐 주변 적에게 피해를 주고 밀어냅니다.",
            "지정한 지점에 운석을 떨어뜨려 넓은 범위를 공격합니다.",
        };
        private static readonly float[] Costs = { 8f, 25f, 20f, 25f, 50f };
        private static readonly float[] Cooldowns = { 0.5f, 5f, 6f, 8f, 15f };

        public static bool IsSkill(int id) => id >= 0 && id < Count;
        // Skills cast at the enemy under the aim; the others act around the hero (or, for Meteor, at a ground point).
        public static bool NeedsTarget(int id) => id == (int)SkillKind.LightBeam || id == (int)SkillKind.Ice;
        public static float Cost(int id) => IsSkill(id) ? Costs[id] : 0f;
        public static float Cooldown(int id) => IsSkill(id) ? Cooldowns[id] : 0f;

        // Damage / healing multiplier of a skill at `level` (1 = base).
        public static float Power(int level) => 1f + 0.2f * (Mathf.Max(level, 1) - 1);

        // Light shards to raise a skill from `level` to level + 1.
        public static int UpgradeCost(int id, int level) =>
            Mathf.RoundToInt((50 + 25 * id) * (1f + 0.5f * (Mathf.Max(level, 1) - 1)));

        // The skill a stage teaches on its first clear, or Empty (that stage only pays light shards). Act 1 (stages 1-8)
        // teaches Heal and Shockwave, Act 2 (9-16) Ice and, as the reward for beating the Act 2 boss, Meteor.
        public static int RewardFor(int stage)
        {
            switch (stage)
            {
                case 2: return (int)SkillKind.Heal;
                case 7: return (int)SkillKind.Shockwave;
                case 15: return (int)SkillKind.Ice;
                case 16: return (int)SkillKind.Meteor;
                default: return Empty;
            }
        }
    }
}
