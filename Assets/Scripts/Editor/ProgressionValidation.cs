using System;
using System.IO;
using System.Linq;
using Lightbringer.Player;
using Lightbringer.Progression;
using UnityEngine;

namespace Lightbringer.EditorTools
{
    public static partial class GreyboxValidation
    {
        private static void ValidateProgression()
        {
            CampaignProgress progress = new CampaignProgress();
            Check(progress.IsValid() && progress.UnlockedUnits == 1 && progress.unlockedStage == 1,
                "New campaign starts with stage one and one unlocked troop");
            Check(!progress.CompleteStage(2), "Locked stages cannot grant rewards");
            progress.GainExperience(230);
            Check(progress.level == 3 && progress.pendingLevels == 2 && progress.choices.Distinct().Count() == 3,
                "Multi-level EXP gain queues independent three-choice permanent upgrades");
            int[] choices = (int[])progress.choices.Clone();
            int picked = choices[1];
            Check(!progress.ChooseGrowth(3) && progress.ChooseGrowth(1) && progress.ranks[picked] == 1
                && progress.pendingLevels == 1, "Choosing a valid growth applies exactly one permanent rank");
            int heal = (int)SkillKind.Heal;
            Check(progress.CompleteStage(1, out int learned) && learned == SkillCatalog.Empty && progress.unlockedStage == 2
                && progress.UnlockedUnits == 2 && progress.skillLevels.SequenceEqual(new[] { 1, 0, 0, 0, 0 })
                && progress.loadout.SequenceEqual(new[] { 0, SkillCatalog.Empty, SkillCatalog.Empty }),
                "A first clear without a skill reward unlocks a stage and a troop and teaches nothing");
            Check(!progress.CompleteStage(1) && progress.unlockedStage == 2, "Repeated first-clear requests cannot duplicate campaign rewards");
            Check(progress.CompleteStage(2, out learned) && learned == heal && progress.unlockedStage == 3
                && progress.skillLevels[heal] == 1 && progress.loadout[1] == heal,
                "The stage 2 first clear teaches Heal into the first empty slot");
            Check(!progress.CompleteStage(2, out learned) && learned == SkillCatalog.Empty && progress.skillLevels[heal] == 1,
                "A repeated skill reward cannot be learned twice");
            Check(!progress.TryEquip(0, (int)SkillKind.Meteor) && !progress.TryEquip(2, (int)SkillKind.LightBeam)
                && !progress.TryEquip(2, heal) && progress.TryEquip(1, SkillCatalog.Empty) && progress.TryEquip(2, heal)
                && progress.loadout.SequenceEqual(new[] { (int)SkillKind.LightBeam, SkillCatalog.Empty, heal }),
                "Skill loadout rejects unlearned and duplicate skills, accepts learned ones and allows empty slots");
            int lightCost = SkillCatalog.UpgradeCost((int)SkillKind.LightBeam, 1);
            Check(!progress.TryUpgradeSkill((int)SkillKind.LightBeam), "Skills cannot be upgraded without enough light shards");
            progress.GainGold(lightCost);
            Check(!progress.CanUpgradeSkill((int)SkillKind.Meteor) && progress.TryUpgradeSkill((int)SkillKind.LightBeam)
                && progress.skillLevels[(int)SkillKind.LightBeam] == 2 && progress.gold == 0 && progress.IsValid(),
                "Light shards upgrade a learned skill by one level and never an unlearned one");
            string folder = Path.Combine(Application.dataPath, "../Docs/Validation/SaveTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "campaign.json");
            try
            {
                CampaignSaveStore store = new CampaignSaveStore(path);
                Check(store.Save(progress), "Campaign saves to an isolated test slot");
                CampaignProgress loaded = new CampaignSaveStore(path).Load();
                Check(loaded.IsValid() && loaded.ranks[picked] == 1 && loaded.skillLevels[(int)SkillKind.Heal] == 1
                    && loaded.choices.SequenceEqual(progress.choices) && loaded.pendingLevels == 1
                    && loaded.loadout.SequenceEqual(progress.loadout),
                    "Save/load preserves growth, rewards, loadout and pending random choices without rerolling");
                loaded.ChooseGrowth(0);
                Check(store.Save(loaded) && File.Exists(path + ".bak"), "Replacement save keeps a recoverable previous record");
                File.WriteAllText(path, "{ damaged");
                CampaignSaveStore recovery = new CampaignSaveStore(path);
                CampaignProgress recovered = recovery.Load();
                Check(recovered.skillLevels[(int)SkillKind.Heal] == 1 && recovery.CanSave && recovered.pendingLevels == 1,
                    "Corrupted main save recovers the valid backup");
                Check(recovery.Save(recovered) && Directory.GetFiles(folder, "*.corrupt-*").Length == 1,
                    "Backup recovery preserves the damaged original before replacing it");
                File.WriteAllText(path, "{}");
                File.WriteAllText(path + ".bak", "{}");
                CampaignSaveStore blocked = new CampaignSaveStore(path);
                blocked.Load();
                Check(!blocked.CanSave && !blocked.Save(progress) && File.ReadAllText(path) == "{}",
                    "Invalid main and backup saves are preserved and never silently overwritten");
            }
            finally
            {
                // Only this uniquely-created validation directory is removed; never the real save slot.
                foreach (string file in Directory.GetFiles(folder)) File.Delete(file);
                Directory.Delete(folder);
            }
            for (int stage = 3; stage <= CampaignProgress.StageCount; stage++)
                Check(progress.CompleteStage(stage), "Campaign stage " + stage + " can be unlocked and cleared sequentially");
            Check(progress.skillLevels[(int)SkillKind.Shockwave] == 1 && progress.skillLevels[(int)SkillKind.Ice] == 0
                && progress.skillLevels[(int)SkillKind.Meteor] == 0 && progress.IsValid(),
                "Act 1 teaches Heal and Shockwave; Ice and Meteor wait for Act 2");
            Check(SkillCatalog.RewardFor(7) == (int)SkillKind.Shockwave && SkillCatalog.RewardFor(15) == (int)SkillKind.Ice
                && SkillCatalog.RewardFor(16) == (int)SkillKind.Meteor && SkillCatalog.RewardFor(8) == SkillCatalog.Empty,
                "The skill reward table: Shockwave at stage 7, Ice at 15, Meteor at 16");
            Check(progress.UnlockedUnits == 8 && progress.IsComplete && progress.IsValid(),
                "Late campaign unlocks Dragon and all earlier troops remain available");

            CampaignProgress capped = new CampaignProgress();
            capped.GainExperience(int.MaxValue);
            Check(capped.level == CampaignProgress.MaxLevel && capped.experience == 0
                && capped.pendingLevels == CampaignProgress.MaxLevel - 1 && capped.IsValid(), "EXP stops at the maximum hero level");
            bool alwaysThree = true;
            while (capped.pendingLevels > 0)
            {
                alwaysThree &= capped.choices.Length == 3 && capped.choices.Distinct().Count() == 3
                    && !capped.choices.Contains((int)GrowthKind.AuraSize);
                // Always take the highest-ranked card so caps are reached as early as possible.
                int best = 0;
                for (int i = 1; i < 3; i++) if (capped.ranks[capped.choices[i]] > capped.ranks[capped.choices[best]]) best = i;
                capped.ChooseGrowth(best);
            }
            Check(alwaysThree && capped.ranks.All(rank => rank <= GrowthCatalog.MaxRank) && capped.ranks[(int)GrowthKind.AuraSize] == 0
                && capped.ranks.Sum() == CampaignProgress.MaxLevel - 1 && capped.IsValid(),
                "Every level-up offers three growable cards, aura is never a card and no card passes its cap");
            Check(Mathf.Approximately(GrowthCatalog.AuraRadiusAt(1), 1.5f) && Mathf.Approximately(GrowthCatalog.AuraRadiusAt(CampaignProgress.MaxLevel), 8f),
                "Aura radius grows with hero level from 1.5 m to 8 m");

            CampaignProgress old = new CampaignProgress { version = 1, level = 40 };
            old.ranks = new[] { 5, 10, 0, 0, 20, 4, 0, 0 };
            old.unitRanks[0] = 15;
            old.equipmentLevels = new[] { 25, 1, 0, 1, 0, 0 };
            old.UpgradeSavedData();
            Check(old.IsValid() && old.level == 16 && old.experience == 420 && old.ranks[(int)GrowthKind.AuraSize] == 0
                && old.ranks.Sum() == 15 && old.ranks.Max() <= GrowthCatalog.MaxRank && old.pendingLevels == 0
                && old.unitRanks[0] == Lightbringer.Units.UnitCatalog.MaxRank && old.gold == 1400 + 40
                && old.version == CampaignProgress.Version && old.skillLevels[0] == SkillCatalog.MaxLevel,
                "Version 1 saves keep their EXP on the new curve, return over-cap growth, refund troop ranks and carry gear into skills");

            CampaignProgress v2 = new CampaignProgress { version = 2, equipmentLevels = new[] { 3, 2, 1, 2, 0, 1 }, loadout = new[] { 0, 2, 3 } };
            v2.UpgradeSavedData();
            Check(v2.IsValid() && v2.version == CampaignProgress.Version && v2.skillLevels.SequenceEqual(new[] { 3, 2, 0, 0, 0 })
                && v2.loadout.SequenceEqual(new[] { 0, SkillCatalog.Empty, SkillCatalog.Empty }) && v2.gold == 4 * 40 && v2.equipmentLevels.Length == 0,
                "Version 2 saves keep Light and Healing as skills and refund retired staff and ring levels as light shards");
        }
    }
}
