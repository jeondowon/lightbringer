using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Progression;
using Lightbringer.Resources;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lightbringer.EditorTools
{
    public static partial class GreyboxValidation
    {
        private static void ValidatePlaytestRecorder()
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GameObject host = new GameObject("Playtest validation host");
            CampaignSession session = host.AddComponent<CampaignSession>();
            CampaignProgress profile = new CampaignProgress();
            session.InitializeForValidation(profile);
            session.Configure(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"),
                material, Shader.Find("Universal Render Pipeline/Unlit"));
            string log = Path.Combine(Path.GetTempPath(), "lightbringer-playtest-" + System.Guid.NewGuid().ToString("N") + ".jsonl");
            try
            {
                Check(session.StartBattle() && session.PlaytestRecord != null && session.PlaytestRecord.stage == 1,
                    "Starting a stage opens a playtest record for that stage");
                PrototypeBattle battle = session.Battle;
                Invoke(battle.Objective, "OnEnable");
                PlaytestRecorder recorder = new PlaytestRecorder(log);
                recorder.Begin(battle, 1, profile);

                FoodResource food = battle.Hero.GetComponent<FoodResource>();
                float before = food.CurrentFood;
                Invoke(food, "GenerateFood", 100f);
                Check(food.TotalWasted > 0f && Mathf.Abs(food.TotalProduced - food.TotalWasted - (food.CurrentFood - before)) < 0.01f,
                    "Food tracks production lost at the cap separately from stored Food");
                Physics.SyncTransforms();
                Check(battle.Summoner.TrySummon() && recorder.Current.alliesSummoned == 1
                    && recorder.Current.summonsByUnit[0] == 1 && recorder.Current.summonsByPath[0] == 1,
                    "Playtest record counts each summon by troop and Path");
                Check(Mathf.Approximately(food.TotalSpent, battle.Summoner.SelectedCost), "Food tracks the amount spent on summons");

                Invoke(food, "GenerateFood", 10f);
                recorder.Tick(2f, true);
                Check(recorder.Current.durationSeconds == 0f, "Playtest clock stops while a level-up choice pauses the battle");
                recorder.Tick(2f, false);
                Check(recorder.Current.durationSeconds == 2f && recorder.Current.secondsAtFoodCap > 0f
                    && recorder.Current.heroSecondsNearPath[0] > 0f,
                    "Playtest clock records battle time, capped Food time and the hero's front");

                battle.Waves.Tick(0.1f);
                Combatant enemy = battle.Root.GetComponentsInChildren<Combatant>()
                    .First(x => x.Faction == Faction.Enemy && x != battle.Objective.EnemyBase);
                enemy.TakeDamage(1000, battle.Hero);
                Check(recorder.Current.enemiesDefeated == 1, "Playtest record counts allied kills");

                battle.Objective.EnemyBase.TakeDamage(10000, battle.Hero);
                recorder.Finish("victory");
                recorder.SetFeedback(4, "Validation note");
                Check(recorder.Current.enemyBaseHealthPercent == 0f && recorder.Current.foodSpent > 0f,
                    "Finishing a battle captures the objective and Food totals");
                Check(recorder.Commit() == "" && recorder.Current == null && File.Exists(log),
                    "Committing writes one record and clears the pending record");

                List<PlaytestRecord> records = PlaytestLogMenu.Load(log);
                Check(records.Count == 1 && records[0].result == "victory" && records[0].funRating == 4
                    && records[0].note == "Validation note",
                    "Playtest log round-trips result, rating and note");
                string report = PlaytestLogMenu.BuildReport(records);
                Check(report.Contains("| 1 | 1 | 1 / 0 / 0 |") && report.Contains("Validation note"),
                    "Playtest summary groups runs by stage and lists player notes");

                while (session.IsChoosing) session.ChooseGrowth(0);
                Check(session.ReturnToPreparation() && session.PlaytestRecord == null,
                    "Returning to preparation commits the session record (memory-only in validation)");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(material);
                if (File.Exists(log)) File.Delete(log);
                Physics.SyncTransforms();
            }
        }
    }
}
