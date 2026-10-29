using UnityEngine;

namespace Lightbringer.Player
{
    public enum SkillKind { LightBeam, Heal, Ice, Shockwave, Meteor, RuneWall, Flame }

    // The hero carries one staff and learns skills. A skill is learned as a first-clear reward, levelled with light
    // shards and equipped to one of the four slots (left click, Q, E, right click) before a battle.
    public static class SkillCatalog
    {
        public const int Count = 7;
        public const int Slots = 4;
        public const int MaxLevel = 10;
        // A loadout slot with no skill in it.
        public const int Empty = -1;
        public static readonly string[] Names = { "빛 광선", "치유", "얼음 마법", "충격파", "메테오", "룬 결계", "화염 방사" };
        // Slot order is the on-screen order, left to right.
        public static readonly string[] SlotKeys = { "LMB", "Q", "E", "RMB" };
        // Left click always holds Light Beam and right click Shockwave, the hero's melee slam; they are never
        // re-equipped, and a fixed slot stays empty until its skill is learned. Q and E take any other learned skill.
        private static readonly int[] FixedSkills = { (int)SkillKind.LightBeam, Empty, Empty, (int)SkillKind.Shockwave };
        public const int FirstFreeSlot = 1;
        public static readonly string[] Descriptions =
        {
            "조준한 적에게 빛줄기를 내려 단일 대상을 공격합니다.",
            "영웅의 오라 안에 있는 아군을 회복합니다.",
            "조준한 적에게 얼음탄을 발사해 적중 지점 반경 3m의 적에게 피해를 주고 3초간 얼립니다.",
            "지팡이를 내려쳐 오라 범위의 적에게 피해를 주고 밀어냅니다.",
            "조준한 땅(최대 15m)에 운석을 떨어뜨립니다. 1.8초 뒤 반경 8m의 적에게 큰 피해를 줍니다.",
            "조준한 땅(최대 12m)에 룬 벽을 8초 동안 세웁니다. 근처의 적은 벽을 먼저 공격합니다.",
            "누르고 있는 동안 앞으로 불을 내뿜어 7m 안의 적을 태웁니다. 마나를 계속 소모합니다.",
        };
        // Flame is channelled: its cost is mana per second, plus FlameStartCost to light it.
        private static readonly float[] Costs = { 8f, 25f, 20f, 5f, 50f, 30f, 20f };
        private static readonly float[] Cooldowns = { 0.5f, 5f, 6f, 10f, 15f, 18f, 1f };
        public const float FlameStartCost = 5f;

        public static bool IsSkill(int id) => id >= 0 && id < Count;
        // The skill a fixed slot always holds, or Empty for a free slot (Q, E).
        public static int FixedSkillOf(int slot) => slot >= 0 && slot < Slots ? FixedSkills[slot] : Empty;
        public static bool IsFixedSlot(int slot) => FixedSkillOf(slot) != Empty;
        // The fixed slot that holds this skill, or -1 when the skill goes in Q or E.
        public static int FixedSlotOf(int id) => IsSkill(id) ? System.Array.IndexOf(FixedSkills, id) : -1;
        public static bool IsFixedSkill(int id) => FixedSlotOf(id) >= 0;
        // Skills cast at the enemy under the aim; the others act around the hero (or, for Meteor and Rune Wall, at a ground point).
        public static bool NeedsTarget(int id) => id == (int)SkillKind.LightBeam || id == (int)SkillKind.Ice;
        // Held down instead of pressed once; the cooldown starts when it is released.
        public static bool IsChanneled(int id) => id == (int)SkillKind.Flame;
        public static float Cost(int id) => IsSkill(id) ? Costs[id] : 0f;
        public static string CostLabel(int id) => IsChanneled(id) ? $"{Cost(id):0} MP/초" : $"{Cost(id):0} MP";
        public static float Cooldown(int id) => IsSkill(id) ? Cooldowns[id] : 0f;

        // Damage / healing multiplier of a skill at `level` (1 = base).
        public static float Power(int level) => 1f + 0.2f * (Mathf.Max(level, 1) - 1);

        // Light shards to raise a skill from `level` to level + 1.
        public static int UpgradeCost(int id, int level) =>
            Mathf.RoundToInt((50 + 25 * id) * (1f + 0.5f * (Mathf.Max(level, 1) - 1)));

        // The skill a stage teaches on its first clear, or Empty (that stage only pays light shards). Light Beam and
        // Shockwave are known from the start. Table: Docs/GameDesign/Unlocks.md.
        public static int RewardFor(int stage)
        {
            switch (stage)
            {
                case 4: return (int)SkillKind.Heal;
                case 8: return (int)SkillKind.RuneWall;
                case 12: return (int)SkillKind.Ice;
                case 14: return (int)SkillKind.Flame;
                case 17: return (int)SkillKind.Meteor;
                default: return Empty;
            }
        }
    }
}
