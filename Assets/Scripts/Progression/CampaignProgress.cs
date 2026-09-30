using System;
using Lightbringer.Player;
using UnityEngine;

namespace Lightbringer.Progression
{
    public enum GrowthKind { AuraSize, FoodProduction, ManaRecovery, Capacity, Leadership, AuraBuff, HeroPower, HeroVitality }

    [Serializable]
    public sealed class CampaignProgress
    {
        public const int StageCount = 8;
        public const int GrowthCount = 8;
        public static readonly string[] GrowthNames = { "Aura radius +0.6m", "Food production +1/s", "Mana recovery +2/s",
            "Max Food and Mana +10", "Summon cost -2.5% (max 50%)", "Aura damage bonus +5%", "Hero power +10%", "Hero health +20" };
        public int version = 1;
        public int unlockedStage = 1;
        public bool[] cleared = new bool[StageCount];
        public int gold;
        public int level = 1;
        public int experience;
        public int pendingLevels;
        public int[] choices = new int[0];
        public int[] ranks = new int[GrowthCount];
        public int[] equipmentLevels = { 1, 1, 0, 1, 0, 0 };
        public int[] loadout = { 0, 1, 3 };
        public int ExperienceToNext => 40 + level * 20;
        public int UnlockedUnits => Mathf.Min(Units.UnitCatalog.Count, unlockedStage);
        public bool IsComplete => cleared[StageCount - 1];

        public bool IsValid()
        {
            if (version != 1 || unlockedStage < 1 || unlockedStage > StageCount || level < 1 || level > 100
                || gold < 0 || experience < 0 || experience >= ExperienceToNext || pendingLevels < 0 || pendingLevels > 99
                || cleared == null || cleared.Length != StageCount || ranks == null || ranks.Length != GrowthCount
                || equipmentLevels == null || equipmentLevels.Length != EquipmentCatalog.Count
                || loadout == null || loadout.Length != EquipmentCatalog.Slots || choices == null
                || (pendingLevels == 0 ? choices.Length != 0 : choices.Length != 3)) return false;
            for (int i = 0; i < ranks.Length; i++) if (ranks[i] < 0 || ranks[i] > 99) return false;
            for (int i = 0; i < equipmentLevels.Length; i++) if (equipmentLevels[i] < 0 || equipmentLevels[i] > 99) return false;
            for (int i = 0; i < loadout.Length; i++)
            {
                int id = loadout[i];
                if (id < 0 || id >= equipmentLevels.Length || equipmentLevels[id] <= 0) return false;
                for (int j = 0; j < i; j++) if (loadout[j] == id) return false;
            }
            for (int i = 0; i < choices.Length; i++)
            {
                if (choices[i] < 0 || choices[i] >= GrowthCount) return false;
                for (int j = 0; j < i; j++) if (choices[j] == choices[i]) return false;
            }
            for (int i = 0; i < unlockedStage - 1; i++) if (!cleared[i]) return false;
            for (int i = unlockedStage; i < StageCount; i++) if (cleared[i]) return false;
            return true;
        }

        public void GainExperience(int amount)
        {
            if (amount <= 0 || level >= 100) return;
            long total = (long)experience + amount;
            while (level < 100 && total >= ExperienceToNext)
            {
                total -= ExperienceToNext;
                level++;
                pendingLevels++;
            }
            experience = level == 100 ? 0 : (int)total;
            EnsureChoices();
        }

        private void EnsureChoices()
        {
            if (pendingLevels <= 0) { choices = new int[0]; return; }
            if (choices.Length == 3) return;
            int[] pool = new int[GrowthCount];
            for (int i = 0; i < pool.Length; i++) pool[i] = i;
            System.Random random = new System.Random(Guid.NewGuid().GetHashCode());
            for (int i = pool.Length - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                int temp = pool[i]; pool[i] = pool[other]; pool[other] = temp;
            }
            choices = new[] { pool[0], pool[1], pool[2] };
        }

        public bool ChooseGrowth(int option)
        {
            if (pendingLevels <= 0 || option < 0 || option >= choices.Length) return false;
            ranks[choices[option]]++;
            pendingLevels--;
            choices = new int[0];
            EnsureChoices();
            return true;
        }

        public bool TryEquip(int slot, int id)
        {
            if (slot < 0 || slot >= loadout.Length || id < 0 || id >= equipmentLevels.Length || equipmentLevels[id] <= 0)
                return false;
            for (int i = 0; i < loadout.Length; i++) if (i != slot && loadout[i] == id) return false;
            loadout[slot] = id;
            return true;
        }

        public bool CompleteStage(int stage)
        {
            if (stage < 1 || stage > unlockedStage || stage > StageCount || cleared[stage - 1]) return false;
            cleared[stage - 1] = true;
            unlockedStage = Mathf.Max(unlockedStage, Mathf.Min(StageCount, stage + 1));
            gold += stage * 100;
            int reward = (stage + 1) % EquipmentCatalog.Count;
            equipmentLevels[reward] = Mathf.Max(equipmentLevels[reward], 1 + stage / 3);
            return true;
        }
    }
}
