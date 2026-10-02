using System;
using System.IO;
using System.Linq;
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
            progress.GainExperience(140);
            Check(progress.level == 3 && progress.pendingLevels == 2 && progress.choices.Distinct().Count() == 3,
                "Multi-level EXP gain queues independent three-choice permanent upgrades");
            int[] choices = (int[])progress.choices.Clone();
            int picked = choices[1];
            Check(!progress.ChooseGrowth(3) && progress.ChooseGrowth(1) && progress.ranks[picked] == 1
                && progress.pendingLevels == 1, "Choosing a valid growth applies exactly one permanent rank");
            Check(progress.CompleteStage(1) && progress.unlockedStage == 2 && progress.UnlockedUnits == 2
                && progress.equipmentLevels[2] == 1,
                "First clear grants equipment, a new stage and an additional troop");
            Check(!progress.CompleteStage(1) && progress.unlockedStage == 2 && progress.equipmentLevels[2] == 1,
                "Repeated first-clear requests cannot duplicate campaign rewards");
            Check(!progress.TryEquip(0, 5) && !progress.TryEquip(1, 0) && progress.TryEquip(1, 2),
                "Campaign loadout rejects unowned and duplicate gear and accepts unlocked gear");
            string folder = Path.Combine(Application.dataPath, "../Docs/Validation/SaveTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "campaign.json");
            try
            {
                CampaignSaveStore store = new CampaignSaveStore(path);
                Check(store.Save(progress), "Campaign saves to an isolated test slot");
                CampaignProgress loaded = new CampaignSaveStore(path).Load();
                Check(loaded.IsValid() && loaded.ranks[picked] == 1 && loaded.equipmentLevels[2] == 1
                    && loaded.choices.SequenceEqual(progress.choices) && loaded.pendingLevels == 1
                    && loaded.loadout.SequenceEqual(progress.loadout),
                    "Save/load preserves growth, rewards, loadout and pending random choices without rerolling");
                loaded.ChooseGrowth(0);
                Check(store.Save(loaded) && File.Exists(path + ".bak"), "Replacement save keeps a recoverable previous record");
                File.WriteAllText(path, "{ damaged");
                CampaignSaveStore recovery = new CampaignSaveStore(path);
                CampaignProgress recovered = recovery.Load();
                Check(recovered.equipmentLevels[2] == 1 && recovery.CanSave && recovered.pendingLevels == 1,
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
            for (int stage = 2; stage <= CampaignProgress.StageCount; stage++)
                Check(progress.CompleteStage(stage), "Campaign stage " + stage + " can be unlocked and cleared sequentially");
            Check(progress.UnlockedUnits == 8 && progress.IsComplete && progress.IsValid(),
                "Late campaign unlocks Dragon and all earlier troops remain available");
        }
    }
}
