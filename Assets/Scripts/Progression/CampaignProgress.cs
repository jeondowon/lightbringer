using System;
using Lightbringer.Player;
using UnityEngine;

namespace Lightbringer.Progression
{
    public enum GrowthKind { AuraSize, FoodProduction, ManaRecovery, Capacity, Leadership, AuraBuff, HeroPower, HeroVitality }

    [Serializable]
    public sealed class CampaignProgress
    {
        public const int StageCount = Core.CampaignStages.Count;
        public const int GrowthCount = 8;
        public const int Version = 5;
        public const int MaxLevel = 30;
        // EXP from level n to n + 1 (index n - 1). Tuned so stage bosses 8 / 16 / 24 expect Lv 15 / 25 / 30 with
        // replays: first clears of stages 1-7 give ~6,000 EXP (Lv 12) and a few stage 6-7 replays reach Lv 15.
        // Acts 2 and 3 use this existing curve; level pacing still needs manual playtesting.
        private static readonly int[] ExperienceTable =
        {
            100, 130, 170, 220, 290, 370, 480, 630, 820, 1060, 1380, 1790, 2330, 3030,
            3940, 4410, 4940, 5530, 6200, 6940, 7780, 8710, 9750, 10920, 12230, 13700, 15350, 17190, 19250,
        };
        public int version = Version;
        public int unlockedStage = 1;
        public bool[] cleared = new bool[StageCount];
        public int level = 1;
        public int experience;
        public int pendingLevels;
        public int[] choices = new int[0];
        public int[] ranks = new int[GrowthCount];
        // Level of each learned skill (SkillKind order); 0 = not learned yet.
        public int[] skillLevels = { 1, 0, 0, 0, 0 };
        // Skill in each slot (left click, right click, Q), SkillCatalog.Empty when the slot is empty.
        public int[] loadout = { (int)SkillKind.LightBeam, SkillCatalog.Empty, SkillCatalog.Empty };
        // Versions 1 and 2 kept staffs and rings here; read only to migrate (UpgradeSavedData).
        public int[] equipmentLevels = new int[0];
        // Light shards, the one currency (the field keeps its old name so saves stay readable).
        public int gold;
        public int[] unitRanks = new int[Units.UnitCatalog.Count];
        public bool IsMaxLevel => level >= MaxLevel;
        // 0 at max level, where no EXP is kept.
        public int ExperienceToNext => IsMaxLevel ? 0 : ExperienceTable[Mathf.Max(level, 1) - 1];
        public int UnlockedUnits => Mathf.Min(Units.UnitCatalog.Count, unlockedStage);
        public bool IsComplete => cleared[StageCount - 1];

        public bool IsValid()
        {
            if (version != Version || unlockedStage < 1 || unlockedStage > StageCount || level < 1 || level > MaxLevel
                || experience < 0 || (IsMaxLevel ? experience != 0 : experience >= ExperienceToNext)
                || pendingLevels < 0 || pendingLevels > MaxLevel - 1
                || cleared == null || cleared.Length != StageCount || ranks == null || ranks.Length != GrowthCount
                || skillLevels == null || skillLevels.Length != SkillCatalog.Count
                || loadout == null || loadout.Length != SkillCatalog.Slots || choices == null
                || gold < 0 || unitRanks == null || unitRanks.Length != Units.UnitCatalog.Count
                || (pendingLevels == 0 ? choices.Length != 0 : choices.Length != 3)) return false;
            for (int i = 0; i < ranks.Length; i++) if (ranks[i] < 0 || ranks[i] > GrowthCatalog.MaxRankOf((GrowthKind)i)) return false;
            for (int i = 0; i < skillLevels.Length; i++) if (skillLevels[i] < 0 || skillLevels[i] > SkillCatalog.MaxLevel) return false;
            for (int i = 0; i < unitRanks.Length; i++) if (unitRanks[i] < 0 || unitRanks[i] > Units.UnitCatalog.MaxRank || (i >= UnlockedUnits && unitRanks[i] > 0)) return false;
            for (int i = 0; i < loadout.Length; i++)
            {
                int id = loadout[i];
                if (id == SkillCatalog.Empty) continue;
                if (!SkillCatalog.IsSkill(id) || skillLevels[id] <= 0) return false;
                for (int j = 0; j < i; j++) if (loadout[j] == id) return false;
            }
            for (int i = 0; i < choices.Length; i++)
            {
                if (choices[i] < 0 || choices[i] >= GrowthCount || GrowthCatalog.IsMaxed((GrowthKind)choices[i], ranks[choices[i]]))
                    return false;
                for (int j = 0; j < i; j++) if (choices[j] == choices[i]) return false;
            }
            for (int i = 0; i < unlockedStage - 1; i++) if (!cleared[i]) return false;
            for (int i = unlockedStage; i < StageCount; i++) if (cleared[i]) return false;
            return true;
        }

        public void GainExperience(int amount)
        {
            if (amount <= 0 || IsMaxLevel) return;
            long total = (long)experience + amount;
            while (!IsMaxLevel && total >= ExperienceToNext)
            {
                total -= ExperienceToNext;
                level++;
                pendingLevels++;
            }
            experience = IsMaxLevel ? 0 : (int)total;
            EnsureChoices();
        }

        // Three distinct cards that can still rank up (GrowthCatalog.MaxRank guarantees at least three exist).
        private void EnsureChoices()
        {
            if (pendingLevels <= 0) { choices = new int[0]; return; }
            if (choices.Length == 3) return;
            System.Collections.Generic.List<int> pool = new System.Collections.Generic.List<int>();
            for (int i = 0; i < GrowthCount; i++) if (!GrowthCatalog.IsMaxed((GrowthKind)i, ranks[i])) pool.Add(i);
            System.Random random = new System.Random(Guid.NewGuid().GetHashCode());
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                int temp = pool[i]; pool[i] = pool[other]; pool[other] = temp;
            }
            choices = new[] { pool[0], pool[1], pool[2] };
        }

        // Version 1 saves used a 100-level curve, 99-rank growth cards (aura radius among them), 20-rank troops and
        // gear up to +99. Keeps the EXP earned (re-levelled on the new curve), returns aura and over-cap growth
        // ranks as unspent level-ups, refunds light shards for troop ranks above the cap and caps gear levels.
        public void UpgradeSavedData()
        {
            if (version == 1) UpgradeFromVersion1();
            if (version == 2) UpgradeFromVersion2();
            if (version == 3) ExtendStages(8);
            if (version == 4) ExtendStages(16);
        }

        // Version 3 saved 8 stages and version 4 saved 16. A finished act opens the next one; the rest is kept.
        private void ExtendStages(int savedStages)
        {
            if (cleared == null || cleared.Length != savedStages || unlockedStage < 1 || unlockedStage > savedStages) return;
            // Check the old progression before extending it; malformed records must still fail validation.
            for (int i = 0; i < unlockedStage - 1; i++) if (!cleared[i]) return;
            for (int i = unlockedStage; i < savedStages; i++) if (cleared[i]) return;
            bool finishedAct = cleared[savedStages - 1];
            Array.Resize(ref cleared, StageCount);
            if (finishedAct) unlockedStage = savedStages + 1;
            version = Version;
        }

        // Version 2 kept staffs and rings in equipmentLevels (Light, Healing, Rune, Food, Mana, Vitality). Rings and the
        // Rune staff are gone: Light and Healing carry over as skills, and every level of the others becomes light shards.
        private void UpgradeFromVersion2()
        {
            version = 3;
            int[] old = equipmentLevels ?? new int[0];
            int refund = 0;
            for (int i = 2; i < old.Length; i++) refund += Mathf.Clamp(old[i], 0, 99) * RetiredGearRefund;
            int[] learned = new int[SkillCatalog.Count];
            for (int i = 0; i < 2 && i < old.Length; i++) learned[i] = Mathf.Clamp(old[i], 0, SkillCatalog.MaxLevel);
            learned[(int)SkillKind.LightBeam] = Mathf.Max(learned[(int)SkillKind.LightBeam], 1);
            int[] slots = { SkillCatalog.Empty, SkillCatalog.Empty, SkillCatalog.Empty };
            if (loadout != null)
                for (int i = 0; i < slots.Length && i < loadout.Length; i++)
                    if (loadout[i] >= 0 && loadout[i] < 2 && learned[loadout[i]] > 0) slots[i] = loadout[i];
            skillLevels = learned;
            loadout = slots;
            equipmentLevels = new int[0];
            GainGold(refund);
        }

        // Light shards returned per level of a retired staff or ring.
        private const int RetiredGearRefund = 40;

        private void UpgradeFromVersion1()
        {
            version = 2;
            if (level >= 1 && level <= 100 && experience >= 0)
            {
                long total = 40L * (level - 1) + 10L * level * (level - 1) + experience;
                level = 1;
                while (!IsMaxLevel && total >= ExperienceToNext) { total -= ExperienceToNext; level++; }
                experience = IsMaxLevel ? 0 : (int)total;
            }
            if (ranks != null && ranks.Length == GrowthCount && level >= 1 && level <= MaxLevel)
            {
                int spent = 0;
                for (int i = 0; i < ranks.Length; i++)
                {
                    ranks[i] = Mathf.Clamp(ranks[i], 0, GrowthCatalog.MaxRankOf((GrowthKind)i));
                    spent += ranks[i];
                }
                // The new curve can leave fewer points than were spent: take them back from the highest ranks.
                for (; spent > level - 1; spent--)
                {
                    int top = 0;
                    for (int i = 1; i < ranks.Length; i++) if (ranks[i] > ranks[top]) top = i;
                    ranks[top]--;
                }
                pendingLevels = level - 1 - spent;
                choices = new int[0];
                EnsureChoices();
            }
            if (unitRanks != null && unitRanks.Length == Units.UnitCatalog.Count)
                for (int i = 0; i < unitRanks.Length; i++)
                    for (; unitRanks[i] > Units.UnitCatalog.MaxRank; unitRanks[i]--)
                        GainGold(Units.UnitCatalog.UpgradeCost((Units.UnitKind)i, unitRanks[i] - 1));
            if (equipmentLevels != null)
                for (int i = 0; i < equipmentLevels.Length; i++)
                    equipmentLevels[i] = Mathf.Min(equipmentLevels[i], 10);
        }

        public bool ChooseGrowth(int option)
        {
            if (pendingLevels <= 0 || option < 0 || option >= choices.Length
                || GrowthCatalog.IsMaxed((GrowthKind)choices[option], ranks[choices[option]])) return false;
            ranks[choices[option]]++;
            pendingLevels--;
            choices = new int[0];
            EnsureChoices();
            return true;
        }

        public void GainGold(int amount)
        {
            if (amount > 0) gold = (int)System.Math.Min((long)gold + amount, 99999999L);
        }

        public bool CanUpgradeUnit(int unit) => unit >= 0 && unit < UnlockedUnits && unitRanks[unit] < Units.UnitCatalog.MaxRank
            && gold >= Units.UnitCatalog.UpgradeCost((Units.UnitKind)unit, unitRanks[unit]);

        public bool TryUpgradeUnit(int unit)
        {
            if (!CanUpgradeUnit(unit)) return false;
            gold -= Units.UnitCatalog.UpgradeCost((Units.UnitKind)unit, unitRanks[unit]);
            unitRanks[unit]++;
            return true;
        }

        public bool CanUpgradeSkill(int id) => SkillCatalog.IsSkill(id) && skillLevels[id] > 0 && skillLevels[id] < SkillCatalog.MaxLevel
            && gold >= SkillCatalog.UpgradeCost(id, skillLevels[id]);

        public bool TryUpgradeSkill(int id)
        {
            if (!CanUpgradeSkill(id)) return false;
            gold -= SkillCatalog.UpgradeCost(id, skillLevels[id]);
            skillLevels[id]++;
            return true;
        }

        public bool IsLearned(int id) => SkillCatalog.IsSkill(id) && skillLevels[id] > 0;

        // Puts a learned skill into a slot (a skill sits in one slot only); SkillCatalog.Empty clears the slot.
        public bool TryEquip(int slot, int id)
        {
            if (slot < 0 || slot >= loadout.Length) return false;
            if (id != SkillCatalog.Empty)
            {
                if (!IsLearned(id)) return false;
                for (int i = 0; i < loadout.Length; i++) if (i != slot && loadout[i] == id) return false;
            }
            loadout[slot] = id;
            return true;
        }

        public bool CompleteStage(int stage) => CompleteStage(stage, out _);

        // `learned` is the skill this first clear taught, or SkillCatalog.Empty. A new skill goes into the first empty slot.
        public bool CompleteStage(int stage, out int learned)
        {
            learned = SkillCatalog.Empty;
            if (stage < 1 || stage > unlockedStage || stage > StageCount || cleared[stage - 1]) return false;
            cleared[stage - 1] = true;
            unlockedStage = Mathf.Max(unlockedStage, Mathf.Min(StageCount, stage + 1));
            int reward = SkillCatalog.RewardFor(stage);
            if (SkillCatalog.IsSkill(reward) && skillLevels[reward] == 0)
            {
                skillLevels[reward] = 1;
                learned = reward;
                int free = Array.IndexOf(loadout, SkillCatalog.Empty);
                if (free >= 0) loadout[free] = reward;
            }
            return true;
        }
    }
}
