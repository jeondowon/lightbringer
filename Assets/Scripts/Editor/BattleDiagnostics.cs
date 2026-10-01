using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Lightbringer.Combat;
using Lightbringer.Core;
using Lightbringer.Progression;
using Lightbringer.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Lightbringer.EditorTools
{
    // Headless stage-8 battle: deploys every troop on every Path for 90 simulated seconds and reports
    // units that stopped moving, with the reason (target out of reach, blocked line of sight, route end...).
    // Output: Docs/Diagnostics/BattleDiagnostics.txt. Also runs when Docs/Diagnostics/battle.request exists;
    // Docs/Diagnostics/validate.request runs the full Greybox validation (results in Docs/Validation).
    [InitializeOnLoad]
    public static class BattleDiagnostics
    {
        private const float Step = 1f / 30f;
        private const float Duration = 90f;
        private const float StuckWindow = 8f;
        private static double nextProbe;

        static BattleDiagnostics() => EditorApplication.update += ProbeRequest;

        private static string Folder => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Diagnostics");

        private static void ProbeRequest()
        {
            if (EditorApplication.timeSinceStartup < nextProbe || Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            nextProbe = EditorApplication.timeSinceStartup + 2d;
            string validate = Path.Combine(Folder, "validate.request");
            if (File.Exists(validate))
            {
                File.Delete(validate);
                GreyboxValidation.RunChecks();
            }
            string request = Path.Combine(Folder, "battle.request");
            if (!File.Exists(request)) return;
            File.Delete(request);
            Run();
        }

        [MenuItem("Lightbringer/Diagnostics/Simulate Stage 8 Battle")]
        public static void Run()
        {
            if (Application.isPlaying) { Debug.LogWarning("Stop Play mode before running battle diagnostics."); return; }
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            StringBuilder report = new StringBuilder();
            try
            {
                SceneManager.SetActiveScene(scene);
                GameObject host = new GameObject("Diagnostics Session");
                CampaignSession session = host.AddComponent<CampaignSession>();
                CampaignProgress profile = new CampaignProgress();
                for (int stage = 1; stage < CampaignProgress.StageCount; stage++) profile.CompleteStage(stage);
                session.InitializeForValidation(profile);
                session.Configure(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"),
                    material, Shader.Find("Universal Render Pipeline/Unlit"));
                session.SelectStage(8);
                if (!session.StartBattle()) { Debug.LogError("Diagnostics: battle did not start."); return; }
                PrototypeBattle battle = session.Battle;
                typeof(StageObjective).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(battle.Objective, null);
                battle.Hero.SetMaximumHealth(1000000f);
                battle.Hero.Heal(1000000f);
                // Keep the battle running for the whole window so late-game crowding is observable.
                battle.Objective.EnemyBase.SetMaximumHealth(10000000f);
                battle.Objective.EnemyBase.Heal(10000000f);
                battle.AlliedBase.SetMaximumHealth(10000000f);
                battle.AlliedBase.Heal(10000000f);
                Simulate(battle, report);
            }
            finally
            {
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
                Object.DestroyImmediate(material);
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Path.Combine(Folder, "BattleDiagnostics.txt"), report.ToString());
                Debug.Log("Battle diagnostics written to " + Path.Combine(Folder, "BattleDiagnostics.txt"));
            }
        }

        private static void Simulate(PrototypeBattle battle, StringBuilder report)
        {
            Lightbringer.Resources.FoodResource food = battle.Hero.GetComponent<Lightbringer.Resources.FoodResource>();
            MethodInfo generate = typeof(Lightbringer.Resources.FoodResource).GetMethod("GenerateFood", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo lineOfSight = typeof(UnitCombat).GetMethod("HasLineOfSight", BindingFlags.Instance | BindingFlags.NonPublic);
            var history = new Dictionary<Combatant, List<Vector3>>();
            int summons = 0;
            float nextSummon = 0f;
            for (float time = 0f; time < Duration && !battle.Objective.HasEnded; time += Step)
            {
                if (time >= nextSummon)
                {
                    nextSummon += 5f;
                    generate.Invoke(food, new object[] { 1000f });
                    int kind = summons % UnitCatalog.Count;
                    battle.Summoner.TrySelectPath(summons / UnitCatalog.Count % battle.Paths.Length);
                    battle.Summoner.TrySelectUnit(kind);
                    Physics.SyncTransforms();
                    if (battle.Summoner.TrySummon()) summons++;
                    else nextSummon -= 4.9f;
                }
                Physics.SyncTransforms();
                battle.Waves.Tick(Step);
                foreach (UnitCombat unit in battle.Root.GetComponentsInChildren<UnitCombat>()) unit.Tick(Step);
                foreach (UnitSupport unit in battle.Root.GetComponentsInChildren<UnitSupport>()) unit.Tick(Step);
                foreach (UnitPathFollower unit in battle.Root.GetComponentsInChildren<UnitPathFollower>()) unit.Tick(Step);
                // Sample positions once per simulated second.
                if (Mathf.Repeat(time, 1f) < Step)
                    foreach (Combatant unit in battle.Root.GetComponentsInChildren<Combatant>())
                    {
                        // Only moving troops: skip the hero and both strongholds.
                        if (unit == battle.Hero || unit.GetComponent<UnitPathFollower>() == null || !unit.IsAlive) continue;
                        if (!history.TryGetValue(unit, out List<Vector3> samples)) history[unit] = samples = new List<Vector3>();
                        samples.Add(unit.transform.position);
                    }
            }

            report.AppendLine("Lightbringer battle diagnostics (stage 8, 3 Paths, editor simulation, hero invulnerable)");
            report.AppendLine($"Summoned {summons} allies over {Duration}s. Battle ended: {battle.Objective.HasEnded} (won {battle.Objective.HasWon}).");
            report.AppendLine($"Enemy base HP: {(battle.Objective.EnemyBase != null ? battle.Objective.EnemyBase.CurrentHealth : 0f):0}. Waves {battle.Waves.WavesSpawned}/{battle.Waves.TotalWaves}.");
            var reasons = new Dictionary<string, List<string>>();
            int alive = 0;
            foreach (var pair in history)
            {
                Combatant unit = pair.Key;
                if (unit == null || !unit.IsAlive) continue;
                alive++;
                List<Vector3> samples = pair.Value;
                int window = Mathf.RoundToInt(StuckWindow);
                if (samples.Count <= window) continue;
                Vector3 now = samples[samples.Count - 1], before = samples[samples.Count - 1 - window];
                Vector2 moved = new Vector2(now.x - before.x, now.z - before.z);
                if (moved.magnitude > 0.5f) continue;
                UnitCombat combat = unit.GetComponent<UnitCombat>();
                UnitPathFollower path = unit.GetComponent<UnitPathFollower>();
                string reason;
                if (combat == null || !combat.enabled) reason = path.HasReachedEnd ? "support unit at route end" : "support unit (no combat) idle";
                else if (combat.Target == null) reason = path.HasReachedEnd ? "route end, no target" : "no target, not at route end (blocked?)";
                else
                {
                    Vector3 aim = combat.Target.GetAimPoint(unit.transform.position);
                    Vector3 offset = aim - unit.transform.position;
                    float horizontal = new Vector2(offset.x, offset.z).magnitude;
                    bool sight = (bool)lineOfSight.Invoke(combat, new object[] { combat.Target });
                    if (offset.magnitude <= combat.AttackRange)
                        reason = sight ? "in range and attacking (fight in progress)" : "in range but line of sight blocked";
                    else if (horizontal <= combat.AttackRange)
                        reason = $"target out of reach vertically (dy {offset.y:0.0}, range {combat.AttackRange:0.0}) - target {combat.Target.name}";
                    else
                        reason = sight ? "out of range, cannot get closer (crowd)" : "out of range and line of sight blocked";
                }
                string key = unit.Faction + ": " + reason;
                if (!reasons.TryGetValue(key, out List<string> names)) reasons[key] = names = new List<string>();
                UnitPathFollower follower = unit.GetComponent<UnitPathFollower>();
                names.Add($"{unit.name} @({now.x:0.0},{now.y:0.00},{now.z:0.0}) waypoint-end {follower.HasReachedEnd} path {(follower.AssignedPath != null ? follower.AssignedPath.name : "none")}");
            }
            report.AppendLine($"Alive tracked units: {alive}. Units that moved < 0.5 m in the last {StuckWindow}s, by reason:");
            foreach (var pair in reasons.OrderByDescending(p => p.Value.Count))
            {
                report.AppendLine($"- {pair.Key}: {pair.Value.Count}");
                foreach (string name in pair.Value.Take(6)) report.AppendLine("    " + name);
            }
        }
    }
}
