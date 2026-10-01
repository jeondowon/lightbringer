using Lightbringer.Core;
using Lightbringer.Player;
using Lightbringer.Progression;
using Lightbringer.Resources;
using Lightbringer.Units;
using UnityEngine;

namespace Lightbringer.UI
{
    [RequireComponent(typeof(CampaignSession))]
    public sealed class CampaignHUD : MonoBehaviour
    {
        private CampaignSession session;
        private Vector2 scroll;
        private void Awake() => session = GetComponent<CampaignSession>();
        private void OnGUI()
        {
            if (session == null || session.Progress == null) return;
            CampaignProgress profile = session.Progress;
            if (session.Battle == null) { funRating = 0; playtestNote = ""; Preparation(profile); }
            else Battle(profile);
            if (session.IsChoosing) Choices(profile);
            string status = string.IsNullOrEmpty(session.PlaytestStatus) ? session.SaveStatus
                : session.PlaytestStatus + " " + session.SaveStatus;
            if (!string.IsNullOrEmpty(status))
                GUI.Box(new Rect(16, Screen.height - 48, Screen.width - 32, 38), status);
        }

        private void Preparation(CampaignProgress profile)
        {
            float width = Mathf.Min(700, Screen.width - 32);
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2, 20, width, Screen.height - 80), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("LIGHTBRINGER | CAMPAIGN PREPARATION");
            GUILayout.Label($"Level {profile.level} | EXP {profile.experience}/{profile.ExperienceToNext} | Gold {profile.gold}");
            GUILayout.Label(profile.IsComplete ? "Prototype campaign complete. All stages can be replayed." : "Choose a stage and three equipment items.");
            GUI.enabled = !session.IsChoosing;
            GUILayout.BeginHorizontal();
            for (int stage = 1; stage <= CampaignProgress.StageCount; stage++)
            {
                GUI.enabled = !session.IsChoosing && stage <= profile.unlockedStage;
                if (GUILayout.Button((stage == session.SelectedStage ? "> " : "") + stage + (profile.cleared[stage - 1] ? " *" : "")))
                    session.SelectStage(stage);
            }
            GUILayout.EndHorizontal();
            GUI.enabled = !session.IsChoosing;
            for (int slot = 0; slot < EquipmentCatalog.Slots; slot++)
            {
                GUILayout.Label($"Slot {slot + 1} ({(slot == 0 ? "LMB" : slot == 1 ? "Q" : "E")}): {EquipmentCatalog.Names[profile.loadout[slot]]}");
                GUILayout.BeginHorizontal();
                for (int id = 0; id < EquipmentCatalog.Count; id++)
                {
                    GUI.enabled = !session.IsChoosing && profile.equipmentLevels[id] > 0;
                    if (GUILayout.Button(EquipmentCatalog.Names[id] + " +" + profile.equipmentLevels[id])) session.TryEquip(slot, id);
                }
                GUILayout.EndHorizontal();
            }
            GUI.enabled = !session.IsChoosing;
            GUILayout.Label("Unlocked troops (all available during battle):");
            for (int i = 0; i < profile.UnlockedUnits; i++) GUILayout.Label(UnitCatalog.Names[i] + " — " + UnitCatalog.Cost((UnitKind)i) + " Food");
            if (GUILayout.Button("START STAGE " + session.SelectedStage, GUILayout.Height(38))) session.StartBattle();
            GUI.enabled = true;
            GUILayout.Label("Permanent growth:");
            for (int i = 0; i < profile.ranks.Length; i++) if (profile.ranks[i] > 0)
                GUILayout.Label(CampaignProgress.GrowthNames[i] + " × " + profile.ranks[i]);
            GUILayout.Label("Prototype balance. First clears award gold and gear; upgraded gear replaces lower levels.");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void Battle(CampaignProgress profile)
        {
            PrototypeBattle battle = session.Battle;
            GUILayout.BeginArea(new Rect(16, 16, 420, 390), GUI.skin.box);
            GUILayout.Label($"Stage {session.SelectedStage} | Level {profile.level} | EXP {profile.experience}/{profile.ExperienceToNext}");
            if (battle.Hero != null)
            {
                FoodResource food = battle.Hero.GetComponent<FoodResource>();
                ManaResource mana = battle.Hero.GetComponent<ManaResource>();
                GUILayout.Label($"HP {battle.Hero.CurrentHealth:F0}/{battle.Hero.MaximumHealth:F0} | Mana {mana.Current:F0}/{mana.Maximum:F0}");
                GUILayout.Label($"Food {food.CurrentFood:F1}/{food.MaximumFood:F0} | Tab: {battle.Summoner.SelectedPath?.name} | F: repeat {UnitCatalog.Names[(int)battle.Summoner.SelectedUnit]}");
                GUILayout.Label(TroopKeys(battle.Summoner, food.CurrentFood));
                for (int i = 0; i < 3; i++)
                    GUILayout.Label($"{(i == 0 ? "LMB" : i == 1 ? "Q" : "E")}: {EquipmentCatalog.Names[battle.Abilities.Equipped(i)]} | {battle.Abilities.CooldownRemaining(i):F1}s");
                GUILayout.Label(battle.Abilities.Feedback);
                GUILayout.Label(battle.Summoner.LastFeedback);
            }
            if (battle.AlliedBase != null) GUILayout.Label($"Our base: {battle.AlliedBase.CurrentHealth:F0}/{battle.AlliedBase.MaximumHealth:F0} HP");
            if (battle.Objective.EnemyBase != null) GUILayout.Label($"Enemy base: {battle.Objective.EnemyBase.CurrentHealth:F0} HP");
            GUILayout.Label($"Enemy waves {battle.Waves.WavesSpawned}/{battle.Waves.TotalWaves}");
            GUILayout.Label("WASD move | Mouse look | Wheel zoom | Esc cursor");
            GUILayout.EndArea();
            GUI.Label(new Rect(Screen.width / 2 - 8, Screen.height / 2 - 12, 24, 24), "+");
            if (!battle.Objective.HasEnded || session.IsChoosing) return;
            PlaytestRecord record = session.PlaytestRecord;
            float height = record == null ? 160 : 400;
            GUILayout.BeginArea(new Rect((Screen.width - 560) / 2, (Screen.height - height) / 2, 560, height), GUI.skin.box);
            GUILayout.Label(battle.Objective.HasWon ? "VICTORY" : "DEFEAT");
            GUILayout.Label(session.LastResult);
            if (record != null) PlaytestSummary(record);
            if (GUILayout.Button("RETURN TO PREPARATION", GUILayout.Height(36)))
            {
                session.SetPlaytestFeedback(funRating, playtestNote);
                session.ReturnToPreparation();
            }
            GUILayout.EndArea();
        }

        // "1 Swordsman 10 · 2 Archer 15 …" for unlocked troops; unaffordable ones are marked with "-".
        private static string TroopKeys(UnitSummoner summoner, float food)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            for (int i = 0; i < summoner.UnlockedUnitCount; i++)
            {
                float cost = summoner.CostOf((UnitKind)i);
                if (i > 0) text.Append(i % 4 == 0 ? "\n" : "   ");
                text.Append(i + 1).Append(food >= cost ? " " : " -").Append(UnitCatalog.Names[i]).Append(' ').Append(cost.ToString("0.#"));
            }
            return text.ToString();
        }

        private int funRating;
        private string playtestNote = "";

        private void PlaytestSummary(PlaytestRecord record)
        {
            GUILayout.Label($"PLAYTEST | {record.durationSeconds / 60f:0.0} min | Summoned {record.alliesSummoned}, lost {record.alliesLost} | Enemies defeated {record.enemiesDefeated}");
            float wastedPercent = record.foodProduced > 0f ? 100f * record.foodWasted / record.foodProduced : 0f;
            GUILayout.Label($"Food spent {record.foodSpent:0} | wasted at cap {wastedPercent:0}% | Lowest hero HP {record.lowestHeroHealthPercent:0}%");
            GUILayout.Label($"Allies in aura (avg) {record.averageAlliesInAura:0.0} | Enemy base left {record.enemyBaseHealthPercent:0}%");
            GUILayout.Label("How fun was this stage? (0 = skip)");
            GUILayout.BeginHorizontal();
            for (int rating = 0; rating <= 5; rating++)
                if (GUILayout.Toggle(funRating == rating, rating == 0 ? "-" : rating.ToString(), GUI.skin.button)) funRating = rating;
            GUILayout.EndHorizontal();
            GUILayout.Label("Note (what felt fun, slow, confusing or unfair?):");
            playtestNote = GUILayout.TextArea(playtestNote, 400, GUILayout.Height(60));
        }

        private void Choices(CampaignProgress profile)
        {
            float width = Mathf.Min(600, Screen.width - 32);
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2, (Screen.height - 230) / 2, width, 230), GUI.skin.box);
            GUILayout.Label("LEVEL UP — CHOOSE ONE PERMANENT UPGRADE");
            GUILayout.Label($"{profile.pendingLevels} choice(s) remaining. Battlefield paused.");
            for (int i = 0; i < profile.choices.Length; i++)
                if (GUILayout.Button(CampaignProgress.GrowthNames[profile.choices[i]], GUILayout.Height(36)))
                { session.ChooseGrowth(i); break; }
            GUILayout.EndArea();
        }
    }
}
